use super::builder::VertexGraphBuilder;
use crate::id_gen::IdGenerator;
use crate::messages;
use crate::scheduling;
use indexmap::IndexSet;
use zametek_maths_graphs_primitives::{
    Activity, DependentActivity, GraphCompilation, GraphCompilationError, GraphError, Key,
    Resource, WorkStream,
};

/// Compiler for Activity-on-Vertex graphs: a coordinator around a
/// [`VertexGraphBuilder`] — the counterpart of the C# `VertexGraphCompiler`.
/// This is the compiler to use for analysis; [`VertexGraphCompiler::compile`]
/// runs the full pipeline including resource scheduling.
///
/// The C# original serialises access with an internal lock; in Rust the
/// `&mut self` receivers provide the same guarantee (wrap the compiler in a
/// `Mutex` to share it across threads).
pub struct VertexGraphCompiler<K: Key, R: Key, W: Key> {
    builder: VertexGraphBuilder<K, R, W>,
}

impl<K: Key, R: Key, W: Key> Default for VertexGraphCompiler<K, R, W> {
    fn default() -> Self {
        Self::new()
    }
}

impl<K: Key, R: Key, W: Key> VertexGraphCompiler<K, R, W> {
    /// Creates a compiler wired with the default engines.
    pub fn new() -> Self {
        Self {
            builder: VertexGraphBuilder::new(IdGenerator::Previous(K::default())),
        }
    }

    /// Creates a compiler around the given builder.
    pub fn with_builder(builder: VertexGraphBuilder<K, R, W>) -> Self {
        Self { builder }
    }

    pub fn builder(&self) -> &VertexGraphBuilder<K, R, W> {
        &self.builder
    }

    pub fn builder_mut(&mut self) -> &mut VertexGraphBuilder<K, R, W> {
        &mut self.builder
    }

    /// The earliest start time across all activities.
    pub fn start_time(&self) -> i32 {
        self.builder.start_time()
    }

    /// The latest finish time across all activities.
    pub fn finish_time(&self) -> i32 {
        self.builder.finish_time()
    }

    /// The cyclomatic complexity of the network (a measure of its parallelism).
    pub fn cyclomatic_complexity(&self) -> i32 {
        let edge_count = self.builder.edge_ids().len() as i32;
        let node_count = self.builder.node_ids().len() as i32;
        let extra_nodes = 2;
        let extra_edges =
            (self.builder.start_nodes().len() + self.builder.end_nodes().len()) as i32;
        let isolated_node_count = self.builder.isolated_nodes().len() as i32;
        (edge_count + extra_edges) - (node_count + extra_nodes) + 2 * (1 + isolated_node_count)
    }

    /// Returns an unused activity ID.
    pub fn get_next_activity_id(&self) -> K {
        self.builder
            .activity_ids()
            .into_iter()
            .max()
            .unwrap_or_default()
            .next()
    }

    /// Clears all activities and returns the compiler to its initial state.
    pub fn reset(&mut self) {
        self.builder.reset();
    }

    /// Adds an activity, wiring its compiled and planning dependencies into
    /// the graph. Returns false if the ID already exists.
    pub fn add_activity(&mut self, activity: DependentActivity<K, R, W>) -> bool {
        let dependencies: IndexSet<K> = activity
            .dependencies
            .iter()
            .chain(activity.planning_dependencies.iter())
            .copied()
            .collect();
        self.builder
            .add_activity_with_dependencies(activity, dependencies)
    }

    /// Removes an activity and detaches it from its dependents.
    pub fn remove_activity(&mut self, activity_id: K) -> bool {
        {
            // Clear out the activity from compiled dependencies.
            let dependent_activity_ids: Vec<K> = self
                .builder
                .activities()
                .filter(|x| x.dependencies.contains(&activity_id))
                .map(|x| x.id())
                .collect();
            for id in dependent_activity_ids {
                self.builder
                    .activity_mut(id)
                    .expect("activity must exist")
                    .dependencies
                    .shift_remove(&activity_id);
            }
        }
        {
            // Clear out the activity from planning dependencies.
            let dependent_activity_ids: Vec<K> = self
                .builder
                .activities()
                .filter(|x| x.planning_dependencies.contains(&activity_id))
                .map(|x| x.id())
                .collect();
            for id in dependent_activity_ids {
                self.builder
                    .activity_mut(id)
                    .expect("activity must exist")
                    .planning_dependencies
                    .shift_remove(&activity_id);
            }
        }
        if let Some(activity) = self.builder.activity_mut(activity_id) {
            activity.set_as_removable();
        }
        self.builder.remove_activity(activity_id)
    }

    /// Strips redundant dependencies, keeping only the minimal edge set.
    pub fn transitive_reduction(&mut self) -> Result<(), GraphError> {
        if !self.builder.transitive_reduction() {
            return Err(GraphError::new(
                messages::MSG_CANNOT_PERFORM_TRANSITIVE_REDUCTION,
            ));
        }

        // Now set the compiled and planning dependencies to match the actual
        // remaining dependencies.
        for activity_id in self.builder.activity_ids() {
            let actual_dependency_ids: IndexSet<K> = self
                .builder
                .activity_dependency_ids(activity_id)
                .into_iter()
                .collect();
            let activity = self
                .builder
                .activity(activity_id)
                .expect("activity must exist");
            let remaining_compiled: IndexSet<K> = activity
                .dependencies
                .iter()
                .filter(|x| actual_dependency_ids.contains(*x))
                .copied()
                .collect();
            let remaining_planning: IndexSet<K> = activity
                .planning_dependencies
                .iter()
                .filter(|x| actual_dependency_ids.contains(*x))
                .copied()
                .collect();
            self.builder.set_activity_dependencies(
                activity_id,
                remaining_compiled,
                remaining_planning,
            );
        }
        Ok(())
    }

    /// Exports the compiled Activity-on-Vertex structure.
    pub fn to_graph(&mut self) -> Result<crate::VertexGraph<K, R, W>, GraphError> {
        self.builder.to_graph()
    }

    /// Replaces an activity's compiled and planning dependencies.
    pub fn set_activity_dependencies(
        &mut self,
        activity_id: K,
        dependencies: IndexSet<K>,
        planning_dependencies: IndexSet<K>,
    ) -> bool {
        self.builder
            .set_activity_dependencies(activity_id, dependencies, planning_dependencies)
    }

    /// Compiles with infinite resources - the pure critical-path schedule.
    pub fn compile(&mut self) -> Result<GraphCompilation<K, R, W>, GraphError> {
        self.compile_with_resources(&[])
    }

    /// Compiles, scheduling activities onto the given resources (an empty
    /// slice means infinite resources).
    pub fn compile_with_resources(
        &mut self,
        resources: &[Resource<R, W>],
    ) -> Result<GraphCompilation<K, R, W>, GraphError> {
        self.compile_with_resources_and_work_streams(resources, &[])
    }

    /// Compiles with resources and reports which of the given work streams were used.
    pub fn compile_with_resources_and_work_streams(
        &mut self,
        resources: &[Resource<R, W>],
        work_streams: &[WorkStream<W>],
    ) -> Result<GraphCompilation<K, R, W>, GraphError> {
        // If resources are 0, assume infinite resources.
        let infinite_resources = resources.is_empty();
        // Filter out disabled resources.
        let filtered_resources: Vec<Resource<R, W>> = resources
            .iter()
            .filter(|x| !x.is_inactive)
            .cloned()
            .collect();

        let all_activity_ids = self.builder.activity_ids();
        self.builder.reset_resource_state(&all_activity_ids);

        let mut compilation_errors: Vec<GraphCompilationError> = Vec::new();
        self.builder.add_pre_compilation_errors(
            &mut compilation_errors,
            &filtered_resources,
            infinite_resources,
        );

        if !compilation_errors.is_empty() {
            return Ok(GraphCompilation::new(
                self.builder.activities().cloned().collect(),
                Vec::new(),
                Vec::new(),
                compilation_errors,
            ));
        }

        // First CPM pass -> schedule -> wire resource dependencies -> second CPM pass.
        self.builder.calculate_critical_path()?;
        let mut resource_schedules = self
            .builder
            .calculate_resource_schedules_by_priority_list(&filtered_resources)?;

        // If the previous calculation was performed with infinite resources,
        // then it will not be possible to handle resource dependencies. So here
        // we need to create fake resources for resource dependencies to work in
        // the next step.
        if infinite_resources {
            resource_schedules = scheduling::replace_with_synthetic_resources(resource_schedules);
        }

        // Determine the resource dependencies and add them to the compiled dependencies.
        self.builder
            .assign_resource_dependencies(&resource_schedules);
        self.builder.calculate_critical_path()?;

        if !self.builder.back_fill_isolated_nodes() {
            return Err(GraphError::new(
                messages::MSG_CANNOT_BACKFILL_ISOLATED_NODES,
            ));
        }

        let all_activity_ids = self.builder.activity_ids();
        self.builder
            .remove_resource_only_dependencies(&all_activity_ids);
        self.builder
            .add_post_compilation_errors(&mut compilation_errors);
        let all_activity_ids = self.builder.activity_ids();
        self.builder.update_activity_successors(&all_activity_ids);

        // Rebuild schedules aligned to final CPM times, collect indirect
        // resource schedules.
        let final_activities: Vec<Activity<K, R, W>> = self
            .builder
            .activities()
            .map(|x| x.activity.clone())
            .collect();
        let start_time = self.builder.start_time();
        let finish_time = self.builder.finish_time();

        let new_schedules = scheduling::rebuild_aligned_resource_schedules(
            &resource_schedules,
            infinite_resources,
            &self.builder,
            &final_activities,
            start_time,
            finish_time,
        )?;
        let indirect_schedules = scheduling::collect_indirect_resource_schedules(
            &filtered_resources,
            &new_schedules,
            &final_activities,
            start_time,
            finish_time,
        )?;
        let mut total_schedules = new_schedules;
        total_schedules.extend(indirect_schedules);

        // Now calculate the used work streams.
        let workstreams_used: IndexSet<W> = final_activities
            .iter()
            .flat_map(|x| x.target_work_streams.iter().copied())
            .collect();
        let resource_phases_used =
            scheduling::get_resource_phases_used(&total_schedules, &workstreams_used);

        Ok(GraphCompilation::new(
            self.builder.activities().cloned().collect(),
            total_schedules,
            work_streams
                .iter()
                .filter(|x| resource_phases_used.contains(&x.id))
                .cloned()
                .collect(),
            compilation_errors,
        ))
    }
}
