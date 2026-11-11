use crate::duration::DurationDistribution;
use crate::error::GraphError;
use crate::graph_limits;
use crate::key::Key;

/// How one step of a critical path follows the step before it - the
/// counterpart of the C# `CriticalPathLink`.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub enum CriticalPathLink {
    /// The first step of a path, which does not follow another step.
    None = 0,
    /// The activity depends on the one before it, as the plan says: directly, or
    /// through milestones, which are not on the path.
    Dependency = 1,
    /// The activity does not depend on the one before it, directly or through
    /// milestones. It starts when it does because the two share a resource, and
    /// the resource finishes the one before it starts the other.
    Resource = 2,
}

/// Why an iteration of a simulation failed - the counterpart of the C#
/// `SimulationIterationFailure`. A failed iteration has no finish time, no
/// critical activities and no critical paths, and so the statistics of a
/// simulation leave it out.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Default)]
pub enum SimulationIterationFailure {
    /// The iteration did not fail.
    #[default]
    None = 0,
    /// The resource scheduler could not place every activity.
    ResourceSchedulingStall = 1,
    /// The schedule ran past [`graph_limits::MAXIMUM_TIME_VALUE`].
    MaximumTimeValueExceeded = 2,
    /// An activity could not finish by its maximum latest finish time with the
    /// durations of the iteration. The schedule is then not valid. A compile
    /// with the same durations reports C0010, or P0030 when the duration of an
    /// activity is longer than the window from its minimum earliest start time
    /// to its maximum latest finish time.
    InvalidConstraints = 3,
}

/// One activity on a critical path, and how it follows the activity before it.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub struct CriticalPathStep<K: Key> {
    /// The activity.
    pub activity_id: K,
    /// How the activity follows the one before it: [`CriticalPathLink::None`]
    /// for the first step.
    pub link: CriticalPathLink,
}

/// A critical path of a schedule: a chain of critical activities, each of which
/// starts at the moment that the one before it finishes, from the start of the
/// plan to its finish. The chain holds only the activities that take time: a
/// milestone is passed through and does not appear, and so two chains that
/// differ only by a milestone are one path. A resource link between two
/// activities that the plan also ties, directly or through milestones, is the
/// same tie. The link is [`CriticalPathLink::Dependency`], and the two chains are
/// one path.
#[derive(Debug, Clone, PartialEq, Eq, Hash)]
pub struct CriticalPath<K: Key> {
    /// The steps, in the order of the schedule.
    pub steps: Vec<CriticalPathStep<K>>,
}

/// What a simulation does: how many iterations, from which seed, and from which
/// distribution it draws the durations - the counterpart of the C#
/// `SimulationSettings`.
///
/// The same settings, on the same plan, give the same result for any value of
/// [`SimulationSettings::max_degree_of_parallelism`], and the .NET library
/// gives the same result too. The fields are plain; [`SimulationSettings::validate`]
/// checks them, and a simulation calls it first.
#[derive(Debug, Clone, PartialEq)]
pub struct SimulationSettings {
    /// The number of iterations: from 1 to
    /// [`graph_limits::MAXIMUM_SIMULATION_ITERATIONS`].
    pub iterations: usize,
    /// The seed of the random numbers.
    pub seed: u64,
    /// The distribution of the duration of an activity that has a spread.
    pub distribution: DurationDistribution,
    /// The shape value of [`DurationDistribution::ModifiedBetaPert`]: a number
    /// above 0.
    pub modified_beta_shape: f64,
    /// The most threads that the simulation uses, or 0 for as many as the
    /// machine has processors. The result does not depend on the value.
    pub max_degree_of_parallelism: usize,
    /// Whether the result holds, for each iteration, which activities were
    /// critical.
    pub record_critical_activities: bool,
    /// Whether the result holds, for each iteration, its critical paths.
    pub record_critical_paths: bool,
    /// The most critical paths that one iteration records: 1 or more.
    pub maximum_critical_paths_per_iteration: usize,
    /// With no resources, the compile schedules the plan onto synthetic
    /// resources. When this is false, a simulation of a plan with no resources
    /// stops after the critical-path calculation, which gives the same finish
    /// times and the critical activities of the plan alone. When it is true,
    /// the simulation does what the compile does.
    pub schedule_onto_synthetic_resources: bool,
}

impl SimulationSettings {
    /// The default number of iterations.
    pub const DEFAULT_ITERATIONS: usize = 5_000;

    /// The default number of critical paths that one iteration records.
    pub const DEFAULT_MAXIMUM_CRITICAL_PATHS_PER_ITERATION: usize = 32;

    /// The default shape value of the modified PERT beta distribution.
    pub const DEFAULT_MODIFIED_BETA_SHAPE: f64 = 4.0;

    /// Checks the settings.
    pub fn validate(&self) -> Result<(), GraphError> {
        if self.iterations < 1 || self.iterations > graph_limits::MAXIMUM_SIMULATION_ITERATIONS {
            return Err(GraphError::new(format!(
                "The number of iterations must be from 1 to {}.",
                graph_limits::MAXIMUM_SIMULATION_ITERATIONS
            )));
        }
        if self.modified_beta_shape.is_nan()
            || self.modified_beta_shape <= 0.0
            || self.modified_beta_shape.is_infinite()
        {
            return Err(GraphError::new("The shape value must be a number above 0."));
        }
        if self.maximum_critical_paths_per_iteration < 1 {
            return Err(GraphError::new("The number of paths must be 1 or more."));
        }
        Ok(())
    }
}

impl Default for SimulationSettings {
    fn default() -> Self {
        Self {
            iterations: Self::DEFAULT_ITERATIONS,
            seed: 0,
            distribution: DurationDistribution::BetaPert,
            modified_beta_shape: Self::DEFAULT_MODIFIED_BETA_SHAPE,
            max_degree_of_parallelism: 0,
            record_critical_activities: true,
            record_critical_paths: true,
            maximum_critical_paths_per_iteration:
                Self::DEFAULT_MAXIMUM_CRITICAL_PATHS_PER_ITERATION,
            schedule_onto_synthetic_resources: false,
        }
    }
}

/// The raw result of a simulation: for each iteration, the finish time of the
/// schedule, the activities that were critical, and the critical paths - the
/// counterpart of the C# `SimulationResult`. It holds no statistics.
///
/// An iteration is numbered from 0, and an activity is numbered by its place in
/// [`SimulationResult::activity_ids`], which is the order in which the plan gave
/// its activities.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SimulationResult<K: Key> {
    activity_ids: Vec<K>,
    finish_times: Vec<i32>,
    words_per_iteration: usize,
    critical_bits: Option<Vec<u64>>,
    critical_paths: Vec<CriticalPath<K>>,
    path_offsets: Option<Vec<usize>>,
    path_indexes: Vec<usize>,
    paths_complete: Vec<bool>,
    failure_reasons: Option<Vec<SimulationIterationFailure>>,
    maximum_latest_finish_time_misses: Option<Vec<usize>>,
}

impl<K: Key> SimulationResult<K> {
    /// The finish time that an iteration has when it failed: when the resource
    /// scheduler could not place every activity, when the schedule ran past
    /// [`graph_limits::MAXIMUM_TIME_VALUE`], or when an activity could not
    /// finish by its maximum latest finish time.
    /// [`Self::failure_reason`] tells why it failed.
    pub const FAILED_FINISH_TIME: i32 = -1;

    /// Creates a result that does not say why its failed iterations failed.
    /// The parts are those of the C# constructor: see the remarks there for the
    /// layout of the bits and of the paths. [`Self::with_failure_reasons`] adds
    /// the reasons.
    pub fn new(
        activity_ids: Vec<K>,
        finish_times: Vec<i32>,
        critical_bits: Option<Vec<u64>>,
        critical_paths: Vec<CriticalPath<K>>,
        path_offsets: Option<Vec<usize>>,
        path_indexes: Vec<usize>,
        paths_complete: Vec<bool>,
    ) -> Result<Self, GraphError> {
        let words_per_iteration = activity_ids.len().div_ceil(64);
        if let Some(bits) = &critical_bits {
            if bits.len() != finish_times.len() * words_per_iteration {
                return Err(GraphError::new(
                    "The bits of the critical activities do not match the iterations.",
                ));
            }
        }
        if let Some(offsets) = &path_offsets {
            if offsets.len() != finish_times.len() + 1 || paths_complete.len() != finish_times.len()
            {
                return Err(GraphError::new("The paths do not match the iterations."));
            }
        }
        Ok(Self {
            activity_ids,
            finish_times,
            words_per_iteration,
            critical_bits,
            critical_paths,
            path_offsets,
            path_indexes,
            paths_complete,
            failure_reasons: None,
            maximum_latest_finish_time_misses: None,
        })
    }

    /// The result with the reasons of its failures: why each iteration failed
    /// ([`SimulationIterationFailure::None`] for an iteration with a finish
    /// time), and for each activity the number of iterations in which it could
    /// not finish by its maximum latest finish time. Fails when the parts do
    /// not match the iterations and the activities, or when a reason does not
    /// match the finish time of its iteration.
    pub fn with_failure_reasons(
        mut self,
        failure_reasons: Vec<SimulationIterationFailure>,
        maximum_latest_finish_time_misses: Vec<usize>,
    ) -> Result<Self, GraphError> {
        if failure_reasons.len() != self.finish_times.len() {
            return Err(GraphError::new(
                "The reasons of the failures do not match the iterations.",
            ));
        }
        for (iteration, (reason, finish_time)) in
            failure_reasons.iter().zip(&self.finish_times).enumerate()
        {
            if (*reason == SimulationIterationFailure::None)
                != (*finish_time != Self::FAILED_FINISH_TIME)
            {
                return Err(GraphError::new(format!(
                    "Iteration {iteration} has a finish time of {finish_time} and the reason {reason:?}. Only a failed iteration has a reason."
                )));
            }
        }
        if maximum_latest_finish_time_misses.len() != self.activity_ids.len() {
            return Err(GraphError::new(
                "The misses of the maximum latest finish times do not match the activities.",
            ));
        }
        self.failure_reasons = Some(failure_reasons);
        self.maximum_latest_finish_time_misses = Some(maximum_latest_finish_time_misses);
        Ok(self)
    }

    /// The activities, in the order in which the plan gave them.
    pub fn activity_ids(&self) -> &[K] {
        &self.activity_ids
    }

    /// The number of iterations, including the ones that failed.
    pub fn iteration_count(&self) -> usize {
        self.finish_times.len()
    }

    /// The finish time of each iteration, or [`Self::FAILED_FINISH_TIME`] for
    /// one that failed.
    pub fn finish_times(&self) -> &[i32] {
        &self.finish_times
    }

    /// The number of iterations that failed.
    pub fn failed_iteration_count(&self) -> usize {
        self.finish_times
            .iter()
            .filter(|x| **x == Self::FAILED_FINISH_TIME)
            .count()
    }

    /// Whether the result says which activities were critical in each iteration.
    pub fn has_critical_activities(&self) -> bool {
        self.critical_bits.is_some()
    }

    /// Whether an activity was critical in an iteration. `None` when the
    /// critical activities were not recorded, or when the iteration or the
    /// activity does not exist.
    pub fn is_critical(&self, iteration: usize, activity_index: usize) -> Option<bool> {
        let bits = self.critical_bits.as_ref()?;
        if iteration >= self.iteration_count() || activity_index >= self.activity_ids.len() {
            return None;
        }
        let word = bits[(iteration * self.words_per_iteration) + (activity_index >> 6)];
        Some(((word >> (activity_index & 63)) & 1) != 0)
    }

    /// Whether the result holds the critical paths of each iteration.
    pub fn has_critical_paths(&self) -> bool {
        self.path_offsets.is_some()
    }

    /// The distinct critical paths of all the iterations, in the order in which
    /// they first appear.
    pub fn critical_paths(&self) -> &[CriticalPath<K>] {
        &self.critical_paths
    }

    /// The critical paths of an iteration, as indexes into
    /// [`Self::critical_paths`]. `None` when the paths were not recorded, or
    /// when the iteration does not exist.
    pub fn critical_path_indexes(&self, iteration: usize) -> Option<&[usize]> {
        let offsets = self.path_offsets.as_ref()?;
        if iteration >= self.iteration_count() {
            return None;
        }
        Some(&self.path_indexes[offsets[iteration]..offsets[iteration + 1]])
    }

    /// Whether the paths that [`Self::critical_path_indexes`] gives for an
    /// iteration are all of its critical paths. `None` when the paths were not
    /// recorded, or when the iteration does not exist.
    pub fn is_critical_path_list_complete(&self, iteration: usize) -> Option<bool> {
        self.path_offsets.as_ref()?;
        self.paths_complete.get(iteration).copied()
    }

    /// Whether the result says why each failed iteration failed.
    pub fn has_failure_reasons(&self) -> bool {
        self.failure_reasons.is_some()
    }

    /// Why an iteration failed, or [`SimulationIterationFailure::None`] for an
    /// iteration that has a finish time. `None` when the reasons were not
    /// recorded, or when the iteration does not exist.
    pub fn failure_reason(&self, iteration: usize) -> Option<SimulationIterationFailure> {
        self.failure_reasons.as_ref()?.get(iteration).copied()
    }

    /// The number of iterations in which an activity could not finish by its
    /// maximum latest finish time. Each of these iterations failed with
    /// [`SimulationIterationFailure::InvalidConstraints`]. An iteration can miss
    /// more than one maximum. The number is 0 for an activity that has no
    /// maximum. `None` when the reasons were not recorded, or when the activity
    /// does not exist.
    pub fn maximum_latest_finish_time_miss_count(&self, activity_index: usize) -> Option<usize> {
        self.maximum_latest_finish_time_misses
            .as_ref()?
            .get(activity_index)
            .copied()
    }
}
