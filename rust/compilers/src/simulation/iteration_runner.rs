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
//! The builder is the caller's. It holds the engines that decide how a plan is
//! scheduled (the critical-path engine, the resource scheduler and the others),
//! and so a plan is simulated with the engines that its caller compiles it
//! with. The builder must be new and empty.

use crate::messages;
use crate::vertex::VertexGraphBuilder;
use indexmap::IndexSet;
use zametek_maths_graphs_primitives::{
    graph_limits, DependentActivity, GraphError, Key, Resource, SimulationResult,
};

/// The finish time of an iteration that failed.
pub(crate) const FAILED: i32 = SimulationResult::<i32>::FAILED_FINISH_TIME;

pub(crate) struct SimulationIterationRunner<K: Key, R: Key, W: Key> {
    builder: VertexGraphBuilder<K, R, W>,
    activity_ids: Vec<K>,
    active_resources: Vec<Resource<R, W>>,
    stop_after_the_first_pass: bool,
    replace_with_synthetic_resources: bool,
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
        })
    }

    /// The builder, for reading the times and the critical flags of the
    /// activities after a run.
    pub(crate) fn builder(&self) -> &VertexGraphBuilder<K, R, W> {
        &self.builder
    }

    /// Gives each activity its duration (in the order of the plan), schedules
    /// the plan, and returns the finish time, or [`FAILED`] when the resource
    /// scheduler could not place every activity or when the schedule runs past
    /// the limit.
    pub(crate) fn run(&mut self, durations: &[i32]) -> Result<i32, GraphError> {
        for (id, duration) in self.activity_ids.iter().zip(durations) {
            self.builder
                .activity_mut(*id)
                .expect("activity must exist")
                .duration = *duration;
        }
        let ids = self.activity_ids.clone();
        self.builder.reset_resource_state(&ids);

        self.builder.calculate_critical_path()?;

        if !self.stop_after_the_first_pass {
            let mut schedules = match self
                .builder
                .calculate_resource_schedules_by_priority_list(&self.active_resources)
            {
                Ok(schedules) => schedules,
                Err(error) if error.is_resource_scheduling_stall() => return Ok(FAILED),
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
            return Ok(FAILED);
        }

        if !self.builder.back_fill_isolated_nodes() {
            return Err(GraphError::new(
                messages::MSG_CANNOT_BACKFILL_ISOLATED_NODES,
            ));
        }
        Ok(self.builder.finish_time())
    }
}
