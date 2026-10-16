//! A Monte Carlo simulation of a schedule. Port of `ScheduleSimulator.cs`.
//!
//! Each iteration draws a duration for every activity that has a spread, from
//! the three estimates of the activity, and then schedules the plan again, onto
//! its resources, as a compile does. The result holds, for each iteration, the
//! finish time, the critical activities and the critical paths.
//!
//! The simulation is repeatable. The same plan and the same settings give the
//! same result, for any number of threads, and the .NET library gives the same
//! result too. The iterations are split into chunks of 64. Each chunk has a
//! generator of random numbers of its own, and its seed comes from the seed of
//! the settings and the number of the chunk.
//!
//! The durations are drawn in the order in which the plan gave its activities,
//! and an activity with no spread takes no random number. A draw is rounded to
//! whole time units, with halves up, and it stays within the range from O to P.
//! A draw of 0 stays 0: the activity is then as long as a milestone.

use super::iteration_runner::{SimulationIterationRunner, FAILED};
use super::random::SimulationRandom;
use super::sampler;
use super::tracer::CriticalPathTracer;
use crate::vertex::VertexGraphCompiler;
use std::collections::HashMap;
use std::sync::atomic::{AtomicUsize, Ordering};
use zametek_maths_graphs_primitives::{
    CriticalPath, CriticalPathLink, CriticalPathStep, DependentActivity, DurationEstimate,
    GraphCompilationError, GraphError, Key, Resource, SimulationResult, SimulationSettings,
    WorkStream,
};

/// The number of iterations that share a generator of random numbers. It is
/// part of the result: a different size gives different draws for the same
/// seed.
pub const CHUNK_SIZE: usize = 64;

/// A simulation of a plan: see the module documentation.
pub struct ScheduleSimulator<K: Key, R: Key, W: Key> {
    activity_ids: Vec<K>,
    activities: Vec<DependentActivity<K, R, W>>,
    resources: Vec<Resource<R, W>>,
    estimates: Option<Vec<DurationEstimate>>,
    compilation_errors: Vec<GraphCompilationError>,
    planned_finish_time: i32,
}

// What one chunk of iterations gives. The paths are held as encoded lists,
// numbered in the order in which they first appear in the chunk.
struct ChunkResult {
    count: usize,
    finish_times: Vec<i32>,
    critical_bits: Option<Vec<u64>>,
    paths: Vec<Vec<u32>>,
    path_offsets: Vec<usize>,
    path_indexes: Vec<usize>,
    paths_complete: Vec<bool>,
}

impl<K: Key + Send + Sync, R: Key + Send + Sync, W: Key + Send + Sync> ScheduleSimulator<K, R, W> {
    /// Prepares a simulation of a plan. The activities are the ones that a
    /// compile takes: their dependencies and constraints, and the duration that
    /// the plan schedules (the expected duration). The simulation copies
    /// everything. The order of the activities is part of the contract: it is
    /// the order in which a simulation draws the durations, and the order of the
    /// columns of the result.
    ///
    /// An activity that has no entry in `estimates` keeps its duration in every
    /// iteration. Fails when an estimate belongs to an activity that is not in
    /// the plan, or when two activities have the same ID.
    pub fn new(
        activities: &[DependentActivity<K, R, W>],
        resources: &[Resource<R, W>],
        work_streams: &[WorkStream<W>],
        estimates: &HashMap<K, DurationEstimate>,
    ) -> Result<Self, GraphError> {
        let activity_ids: Vec<K> = activities.iter().map(|x| x.id()).collect();
        let mut index_by_id: HashMap<K, usize> = HashMap::with_capacity(activity_ids.len());
        for (index, id) in activity_ids.iter().enumerate() {
            if index_by_id.insert(*id, index).is_some() {
                return Err(GraphError::new(format!(
                    "The plan has two activities with the ID {id}."
                )));
            }
        }
        for id in estimates.keys() {
            if !index_by_id.contains_key(id) {
                return Err(GraphError::new(format!(
                    "There is an estimate for the activity {id}, which is not in the plan."
                )));
            }
        }

        // The plan is compiled once, as it stands, to find out whether it can be
        // scheduled at all. The iterations check nothing: they would find the
        // same errors again and again. The compile also gives the finish time
        // that the plan itself has.
        let mut compiler: VertexGraphCompiler<K, R, W> = VertexGraphCompiler::new();
        for activity in activities {
            compiler.add_activity(activity.clone());
        }
        let compilation =
            compiler.compile_with_resources_and_work_streams(resources, work_streams)?;
        let compilation_errors = compilation.compilation_errors;

        let mut planned_finish_time = 0;
        let mut built_estimates = None;
        if compilation_errors.is_empty() {
            planned_finish_time = compiler.finish_time();
            let mut list = Vec::with_capacity(activities.len());
            for activity in activities {
                list.push(match estimates.get(&activity.id()) {
                    Some(estimate) => *estimate,
                    None => DurationEstimate::fixed(activity.duration)?,
                });
            }
            built_estimates = Some(list);
        }

        Ok(Self {
            activity_ids,
            activities: activities.to_vec(),
            resources: resources.to_vec(),
            estimates: built_estimates,
            compilation_errors,
            planned_finish_time,
        })
    }

    /// The activities, in the order in which the plan gave them: the order of
    /// the columns of a result.
    pub fn activity_ids(&self) -> &[K] {
        &self.activity_ids
    }

    /// The errors that a compile of the plan reports. A plan with errors cannot
    /// be simulated: [`Self::run`] fails.
    pub fn compilation_errors(&self) -> &[GraphCompilationError] {
        &self.compilation_errors
    }

    /// The finish time of the plan as it stands, with the durations that it
    /// schedules (the expected durations). It is 0 when the plan has
    /// compilation errors.
    pub fn planned_finish_time(&self) -> i32 {
        self.planned_finish_time
    }

    /// Runs a simulation.
    pub fn run(&self, settings: &SimulationSettings) -> Result<SimulationResult<K>, GraphError> {
        settings.validate()?;
        let estimates = self.estimates.as_ref().ok_or_else(|| {
            GraphError::new("The plan has compilation errors, and cannot be simulated.")
        })?;

        let chunk_count = settings.iterations.div_ceil(CHUNK_SIZE);
        let threads = if settings.max_degree_of_parallelism == 0 {
            std::thread::available_parallelism().map_or(1, usize::from)
        } else {
            settings.max_degree_of_parallelism
        }
        .min(chunk_count)
        .max(1);

        let next_chunk = AtomicUsize::new(0);
        let mut chunks: Vec<Option<ChunkResult>> = (0..chunk_count).map(|_| None).collect();

        let outcome: Result<Vec<(usize, ChunkResult)>, GraphError> = std::thread::scope(|scope| {
            let handles: Vec<_> = (0..threads)
                .map(|_| {
                    scope.spawn(|| -> Result<Vec<(usize, ChunkResult)>, GraphError> {
                        let mut worker = Worker::new(self, settings, estimates);
                        let mut done = Vec::new();
                        loop {
                            let chunk = next_chunk.fetch_add(1, Ordering::Relaxed);
                            if chunk >= chunk_count {
                                break;
                            }
                            let first = chunk * CHUNK_SIZE;
                            let count = CHUNK_SIZE.min(settings.iterations - first);
                            done.push((chunk, worker.run(chunk, count)?));
                        }
                        Ok(done)
                    })
                })
                .collect();

            let mut all = Vec::new();
            for handle in handles {
                all.extend(handle.join().expect("a simulation thread panicked")?);
            }
            Ok(all)
        });

        for (chunk, result) in outcome? {
            chunks[chunk] = Some(result);
        }
        let chunks: Vec<ChunkResult> = chunks
            .into_iter()
            .map(|x| x.expect("every chunk was run"))
            .collect();
        self.merge(settings, chunks)
    }

    // Puts the chunks together in the order of their numbers. The paths get their
    // numbers in the order in which they first appear in an iteration, which a
    // merge in the order of the chunks keeps the same.
    fn merge(
        &self,
        settings: &SimulationSettings,
        chunks: Vec<ChunkResult>,
    ) -> Result<SimulationResult<K>, GraphError> {
        let iterations = settings.iterations;
        let words_per_iteration = self.activity_ids.len().div_ceil(64);

        let mut finish_times: Vec<i32> = Vec::with_capacity(iterations);
        let mut bits: Option<Vec<u64>> = settings
            .record_critical_activities
            .then(|| Vec::with_capacity(iterations * words_per_iteration));
        let mut offsets: Vec<usize> = Vec::new();
        let mut complete: Vec<bool> = Vec::new();
        let mut path_indexes: Vec<usize> = Vec::new();
        let mut distinct_paths: HashMap<Vec<u32>, usize> = HashMap::new();
        let mut paths: Vec<CriticalPath<K>> = Vec::new();

        for chunk in &chunks {
            finish_times.extend_from_slice(&chunk.finish_times);
            if let (Some(all_bits), Some(chunk_bits)) =
                (bits.as_mut(), chunk.critical_bits.as_ref())
            {
                all_bits.extend_from_slice(chunk_bits);
            }

            if settings.record_critical_paths {
                // The number of each of the paths of the chunk among all the paths.
                let mut global_index = Vec::with_capacity(chunk.paths.len());
                for path in &chunk.paths {
                    let index = match distinct_paths.get(path) {
                        Some(index) => *index,
                        None => {
                            let index = paths.len();
                            distinct_paths.insert(path.clone(), index);
                            paths.push(self.decode(path));
                            index
                        }
                    };
                    global_index.push(index);
                }

                for k in 0..chunk.count {
                    offsets.push(path_indexes.len());
                    complete.push(chunk.paths_complete[k]);
                    for p in chunk.path_offsets[k]..chunk.path_offsets[k + 1] {
                        path_indexes.push(global_index[chunk.path_indexes[p]]);
                    }
                }
            }
        }
        if settings.record_critical_paths {
            offsets.push(path_indexes.len());
        }

        SimulationResult::new(
            self.activity_ids.clone(),
            finish_times,
            bits,
            paths,
            settings.record_critical_paths.then_some(offsets),
            path_indexes,
            complete,
        )
    }

    fn decode(&self, encoded: &[u32]) -> CriticalPath<K> {
        CriticalPath {
            steps: encoded
                .iter()
                .map(|x| CriticalPathStep {
                    activity_id: self.activity_ids[(*x >> 2) as usize],
                    link: match *x & 3 {
                        0 => CriticalPathLink::None,
                        1 => CriticalPathLink::Dependency,
                        _ => CriticalPathLink::Resource,
                    },
                })
                .collect(),
        }
    }
}

// The state that one thread keeps for the chunks that it runs: its own copy of
// the plan.
struct Worker<'a, K: Key, R: Key, W: Key> {
    settings: &'a SimulationSettings,
    estimates: &'a [DurationEstimate],
    runner: SimulationIterationRunner<K, R, W>,
    tracer: CriticalPathTracer<K>,
    durations: Vec<i32>,
}

impl<'a, K: Key, R: Key, W: Key> Worker<'a, K, R, W> {
    fn new(
        simulator: &'a ScheduleSimulator<K, R, W>,
        settings: &'a SimulationSettings,
        estimates: &'a [DurationEstimate],
    ) -> Self {
        let runner = SimulationIterationRunner::new(
            &simulator.activities,
            &simulator.resources,
            settings.schedule_onto_synthetic_resources,
        );
        let tracer = CriticalPathTracer::new(&simulator.activity_ids);
        Self {
            settings,
            estimates,
            runner,
            tracer,
            durations: vec![0; simulator.activity_ids.len()],
        }
    }

    fn run(&mut self, chunk: usize, count: usize) -> Result<ChunkResult, GraphError> {
        let mut random =
            SimulationRandom::new(SimulationRandom::chunk_seed(self.settings.seed, chunk));
        let words_per_iteration = self.durations.len().div_ceil(64);

        let mut result = ChunkResult {
            count,
            finish_times: vec![0; count],
            critical_bits: self
                .settings
                .record_critical_activities
                .then(|| vec![0u64; count * words_per_iteration]),
            paths: Vec::new(),
            path_offsets: vec![0; count + 1],
            path_indexes: Vec::new(),
            paths_complete: vec![true; count],
        };
        let mut local_index: HashMap<Vec<u32>, usize> = HashMap::new();
        let mut path_lists: Vec<Vec<u32>> = Vec::new();

        for k in 0..count {
            self.draw_durations(&mut random);
            let finish = self.runner.run(&self.durations)?;
            result.finish_times[k] = finish;
            result.path_offsets[k] = result.path_indexes.len();
            if finish == FAILED {
                continue;
            }

            if let Some(bits) = result.critical_bits.as_mut() {
                self.tracer.fill_critical_bits(
                    self.runner.builder(),
                    bits,
                    k * words_per_iteration,
                );
            }
            if self.settings.record_critical_paths {
                path_lists.clear();
                result.paths_complete[k] = self.tracer.trace(
                    self.runner.builder(),
                    finish,
                    &self.durations,
                    self.settings.maximum_critical_paths_per_iteration,
                    &mut path_lists,
                );
                for path in path_lists.drain(..) {
                    let index = match local_index.get(&path) {
                        Some(index) => *index,
                        None => {
                            let index = result.paths.len();
                            local_index.insert(path.clone(), index);
                            result.paths.push(path);
                            index
                        }
                    };
                    result.path_indexes.push(index);
                }
            }
        }
        result.path_offsets[count] = result.path_indexes.len();
        Ok(result)
    }

    // The durations of one iteration, in the order of the plan.
    fn draw_durations(&mut self, random: &mut SimulationRandom) {
        for (duration, estimate) in self.durations.iter_mut().zip(self.estimates) {
            *duration = if estimate.has_spread() {
                sampler::round_to_whole_units(
                    sampler::sample(
                        self.settings.distribution,
                        random,
                        *estimate,
                        self.settings.modified_beta_shape,
                    ),
                    *estimate,
                )
            } else {
                estimate.most_likely()
            };
        }
    }
}
