//! One scheduling pass of the simulation, on a graph that is built once and
//! then given new durations. Port of `SimulationIterationRunner.cs`.
//!
//! This is the seam behind which the cost of an iteration lives: everything
//! that the simulation needs from a schedule comes out of this struct, and
//! nothing outside it touches the builder. A compile does a good deal that an
//! iteration does not need; the runner keeps the four steps that decide the
//! finish time and the critical activities:
//!
//! 1. the first critical-path calculation,
//! 2. the priority-list scheduling onto the resources,
//! 3. the resource dependencies that the schedule implies,
//! 4. the second critical-path calculation, which includes them.
//!
//! Then it checks each maximum LFT, because a maximum that the durations cannot
//! meet pulls its activity back, and the finish time with it. Such an iteration
//! fails, as a compile reports an error for it.
//!
//! The builder is the caller's. It holds the engines that decide how a plan is
//! scheduled (the critical-path engine, the resource scheduler and the others),
//! and so a plan is simulated with the engines that its caller compiles it
//! with. The builder must be new and empty.

use crate::messages;
use crate::vertex::VertexGraphBuilder;
use indexmap::IndexSet;
use zametek_maths_graphs_primitives::{
    graph_limits, DependentActivity, GraphError, Key, Resource, SimulationIterationFailure,
    SimulationResult,
};

/// The finish time of an iteration that failed.
pub(crate) const FAILED: i32 = SimulationResult::<i32>::FAILED_FINISH_TIME;

pub(crate) struct SimulationIterationRunner<K: Key, R: Key, W: Key> {
    builder: VertexGraphBuilder<K, R, W>,
    activity_ids: Vec<K>,
    active_resources: Vec<Resource<R, W>>,
    stop_after_the_first_pass: bool,
    replace_with_synthetic_resources: bool,
    constrained_activities: Vec<usize>,
    missed_maximum_latest_finish_times: Vec<usize>,
    failure: SimulationIterationFailure,
}

impl<K: Key, R: Key, W: Key> SimulationIterationRunner<K, R, W> {
    /// The runner changes the durations and the times of the activities that it
    /// is given, and so the caller gives it copies. Fails when the builder
    /// already holds activities.
    pub(crate) fn new(
        mut builder: VertexGraphBuilder<K, R, W>,
        activities: &[DependentActivity<K, R, W>],
        resources: &[Resource<R, W>],
        schedule_onto_synthetic_resources: bool,
    ) -> Result<Self, GraphError> {
        if builder.activities().next().is_some() {
            return Err(GraphError::new(
                "The builder already holds activities. A simulation needs a new, empty builder from its factory each time.",
            ));
        }

        let mut activity_ids = Vec::with_capacity(activities.len());
        for activity in activities {
            activity_ids.push(activity.id());
            let dependencies: IndexSet<K> = activity
                .dependencies
                .iter()
                .chain(activity.planning_dependencies.iter())
                .copied()
                .collect();
            builder.add_activity_with_dependencies(activity.clone(), dependencies);
        }

        // The compile treats an empty list of resources as infinite resources,
        // and a list of resources that are all inactive as no resources to use.
        // It is the list before the filter that decides.
        let infinite = resources.is_empty();

        // Only a maximum LFT can pull an activity back, and so only these
        // activities can make an iteration invalid (see `run`).
        let constrained_activities = activities
            .iter()
            .enumerate()
            .filter(|(_, x)| x.maximum_latest_finish_time.is_some())
            .map(|(i, _)| i)
            .collect();

        Ok(Self {
            builder,
            activity_ids,
            active_resources: resources
                .iter()
                .filter(|x| !x.is_inactive)
                .cloned()
                .collect(),
            stop_after_the_first_pass: infinite && !schedule_onto_synthetic_resources,
            replace_with_synthetic_resources: infinite,
            constrained_activities,
            missed_maximum_latest_finish_times: Vec::new(),
            failure: SimulationIterationFailure::None,
        })
    }

    /// The builder, for reading the times and the critical flags of the
    /// activities after a run.
    pub(crate) fn builder(&self) -> &VertexGraphBuilder<K, R, W> {
        &self.builder
    }

    /// Why the last run failed, or [`SimulationIterationFailure::None`].
    pub(crate) fn failure(&self) -> SimulationIterationFailure {
        self.failure
    }

    /// The activities (by their places in the plan) that could not finish by
    /// their maximum LFTs in the last run. Empty unless the run failed with
    /// [`SimulationIterationFailure::InvalidConstraints`].
    pub(crate) fn missed_maximum_latest_finish_times(&self) -> &[usize] {
        &self.missed_maximum_latest_finish_times
    }

    /// Gives each activity its duration (in the order of the plan), schedules
    /// the plan, and returns the finish time, or [`FAILED`] when the resource
    /// scheduler could not place every activity, when the schedule runs past
    /// the limit, or when an activity cannot finish by its maximum LFT.
    /// [`Self::failure`] then tells why.
    pub(crate) fn run(&mut self, durations: &[i32]) -> Result<i32, GraphError> {
        self.failure = SimulationIterationFailure::None;
        self.missed_maximum_latest_finish_times.clear();

        for (id, duration) in self.activity_ids.iter().zip(durations) {
            self.builder
                .activity_mut(*id)
                .expect("activity must exist")
                .duration = *duration;
        }
        let ids = self.activity_ids.clone();
        self.builder.reset_resource_state(&ids);

        // A minimum EST and a maximum LFT fix an activity to a window. A duration
        // that is longer than the window cannot meet the maximum. A compile
        // refuses such a plan before it starts (P0030), and the critical-path
        // calculation fails, and so the iteration fails here.
        for n in 0..self.constrained_activities.len() {
            let index = self.constrained_activities[n];
            let activity = self
                .builder
                .activity(self.activity_ids[index])
                .expect("activity must exist");
            if let (Some(minimum_earliest_start_time), Some(maximum_latest_finish_time)) = (
                activity.minimum_earliest_start_time,
                activity.maximum_latest_finish_time,
            ) {
                if minimum_earliest_start_time + activity.duration > maximum_latest_finish_time {
                    self.missed_maximum_latest_finish_times.push(index);
                }
            }
        }
        if !self.missed_maximum_latest_finish_times.is_empty() {
            self.failure = SimulationIterationFailure::InvalidConstraints;
            return Ok(FAILED);
        }

        self.builder.calculate_critical_path()?;

        if !self.stop_after_the_first_pass {
            let mut schedules = match self
                .builder
                .calculate_resource_schedules_by_priority_list(&self.active_resources)
            {
                Ok(schedules) => schedules,
                Err(error) if error.is_resource_scheduling_stall() => {
                    self.failure = SimulationIterationFailure::ResourceSchedulingStall;
                    return Ok(FAILED);
                }
                Err(error) => return Err(error),
            };

            // Without resources the first calculation could not hold resource
            // dependencies, and so the compile makes synthetic resources for
            // them to work.
            if self.replace_with_synthetic_resources {
                schedules = self
                    .builder
                    .resource_scheduling_engine()
                    .replace_with_synthetic_resources(schedules);
            }

            self.builder.assign_resource_dependencies(&schedules);
            self.builder.calculate_critical_path()?;
        }

        // The compile refuses a schedule that runs past the horizon, because the
        // allocation streams that it builds next would be unbounded. An
        // iteration fails in the same way.
        if self.builder.finish_time() > graph_limits::MAXIMUM_TIME_VALUE {
            self.failure = SimulationIterationFailure::MaximumTimeValueExceeded;
            return Ok(FAILED);
        }

        if !self.builder.back_fill_isolated_nodes() {
            return Err(GraphError::new(
                messages::MSG_CANNOT_BACKFILL_ISOLATED_NODES,
            ));
        }

        // A maximum LFT that the durations cannot meet pulls its activity back in
        // the forward pass. The latest LFT of the plan then comes too early, and
        // the finish time of the iteration with it. The schedule is not valid,
        // and a compile with these durations reports C0010. The check of the
        // compile confirms the failure, so that an engine of the caller that
        // pulls activities back in another way also fails the iteration.
        if !self.constrained_activities.is_empty() {
            for n in 0..self.constrained_activities.len() {
                let index = self.constrained_activities[n];
                if self.is_pulled_back(self.activity_ids[index]) {
                    self.missed_maximum_latest_finish_times.push(index);
                }
            }
            if !self.missed_maximum_latest_finish_times.is_empty()
                || !self
                    .builder
                    .find_invalid_post_compilation_constraints()
                    .is_empty()
            {
                self.failure = SimulationIterationFailure::InvalidConstraints;
                return Ok(FAILED);
            }
        }
        Ok(self.builder.finish_time())
    }

    /// Whether the forward pass pulled an activity back to meet its maximum
    /// LFT. The activity then starts before time 0, before its minimum EST, or
    /// before a predecessor hands on its finish: the finish plus the minimum
    /// free slack of the predecessor (an activity cannot have both a minimum
    /// free slack and a maximum LFT). The predecessors are those of the plan
    /// and those that the resources imply.
    fn is_pulled_back(&self, activity_id: K) -> bool {
        let activity = self
            .builder
            .activity(activity_id)
            .expect("activity must exist");
        let earliest_possible_start_time = activity
            .dependencies
            .iter()
            .chain(activity.planning_dependencies.iter())
            .chain(activity.resource_dependencies.iter())
            .map(|predecessor_id| {
                let predecessor = self
                    .builder
                    .activity(*predecessor_id)
                    .expect("predecessor must exist");
                predecessor.earliest_finish_time().unwrap_or(0)
                    + predecessor.minimum_free_slack.unwrap_or(0)
            })
            .fold(
                activity.minimum_earliest_start_time.unwrap_or(0).max(0),
                i32::max,
            );
        activity.earliest_start_time.unwrap_or(0) < earliest_possible_start_time
    }
}
