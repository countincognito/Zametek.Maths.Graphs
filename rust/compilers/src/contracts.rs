//! The engine seams — the counterparts of the C# `I…` engine interfaces.
//!
//! Trait names deliberately keep the C# `I` prefix (e.g. [`IVertexCriticalPathEngine`])
//! so they grep 1:1 against the reference implementation and never collide with
//! the identically-named default structs (`VertexCriticalPathEngine`, …). This
//! is non-idiomatic Rust, chosen for the parity contract.
//!
//! Unlike C#, the engines are **stateless**: they take the graph state (and any
//! collaborators) as parameters rather than being constructed around it. That
//! removes the need for the C# `…Factory` seams — the factory+product pairs
//! collapse into a single trait each.

use crate::arrow::ArrowGraphState;
use crate::vertex::VertexGraphState;
use indexmap::{IndexMap, IndexSet};
use zametek_maths_graphs_primitives::{
    Activity, CircularDependency, DependentActivity, Event, GraphError, InvalidConstraint, Key,
    Resource, ResourceSchedule, UnavailableResources,
};

/// Sequential ID generation — the counterpart of the C# `IIdGenerator<T>`.
/// The one stateful seam (it carries a per-graph counter), so it is held by
/// value (`Box`) and recreated, not shared, when a builder clones.
pub trait IIdGenerator<K: Key> {
    fn generate(&mut self) -> K;
}

/// Creates the events placed on a graph's edges (vertex) or nodes (arrow) —
/// the counterpart of the C# `IEventGenerator<T>`.
pub trait IEventGenerator<K: Key> {
    fn generate(&self, id: K) -> Event<K>;
    fn generate_with_times(
        &self,
        id: K,
        earliest_finish_time: Option<i32>,
        latest_finish_time: Option<i32>,
    ) -> Event<K>;
}

/// Creates the dummy (zero-duration) activities that preserve dependencies in
/// arrow graphs — the counterpart of the C# `IActivityGenerator<…>`.
pub trait IActivityGenerator<K: Key, R: Key, W: Key> {
    fn generate(&self, id: K) -> DependentActivity<K, R, W>;
}

/// Detects circular dependencies in an Activity-on-Vertex graph — the
/// counterpart of the C# `IVertexStronglyConnectedComponentsFinder<…>`.
pub trait IVertexStronglyConnectedComponentsFinder<K: Key, R: Key, W: Key> {
    fn find_strongly_connected_components(
        &self,
        state: &VertexGraphState<K, R, W>,
        ignore_dummies: bool,
    ) -> Vec<CircularDependency<K>>;

    fn find_strongly_circular_dependencies(
        &self,
        state: &VertexGraphState<K, R, W>,
        ignore_dummies: bool,
    ) -> Vec<CircularDependency<K>>;
}

/// Detects circular dependencies in an Activity-on-Arrow graph — the
/// counterpart of the C# `IArrowStronglyConnectedComponentsFinder<…>`.
pub trait IArrowStronglyConnectedComponentsFinder<K: Key, R: Key, W: Key> {
    fn find_strongly_connected_components(
        &self,
        state: &ArrowGraphState<K, R, W>,
        ignore_dummies: bool,
    ) -> Vec<CircularDependency<K>>;

    fn find_strongly_circular_dependencies(
        &self,
        state: &ArrowGraphState<K, R, W>,
        ignore_dummies: bool,
    ) -> Vec<CircularDependency<K>>;
}

/// Critical-path calculation for Activity-on-Vertex graphs — the counterpart of
/// the C# `IVertexCriticalPathEngine<…>`. `Ok(false)` mirrors the C# `false`
/// (preconditions unmet); `Err` mirrors the C# throw (a cycle mid-flow).
pub trait IVertexCriticalPathEngine<K: Key, R: Key, W: Key> {
    fn calculate_critical_path_forward_flow(
        &self,
        state: &mut VertexGraphState<K, R, W>,
        invalid_constraints: &[InvalidConstraint<K>],
        shuffle: bool,
    ) -> Result<bool, GraphError>;

    fn calculate_critical_path_backward_flow(
        &self,
        state: &mut VertexGraphState<K, R, W>,
        invalid_constraints: &[InvalidConstraint<K>],
        shuffle: bool,
    ) -> Result<bool, GraphError>;

    fn back_fill_isolated_nodes(
        &self,
        state: &mut VertexGraphState<K, R, W>,
        invalid_constraints: &[InvalidConstraint<K>],
    ) -> bool;
}

/// Critical-path calculation for Activity-on-Arrow graphs — the counterpart of
/// the C# `IArrowCriticalPathEngine<…>` (event-time passes).
pub trait IArrowCriticalPathEngine<K: Key, R: Key, W: Key> {
    fn calculate_event_earliest_finish_times(
        &self,
        state: &mut ArrowGraphState<K, R, W>,
        invalid_constraints: &[InvalidConstraint<K>],
        shuffle: bool,
    ) -> Result<bool, GraphError>;

    fn calculate_event_latest_finish_times(
        &self,
        state: &mut ArrowGraphState<K, R, W>,
        invalid_constraints: &[InvalidConstraint<K>],
        shuffle: bool,
    ) -> Result<bool, GraphError>;

    fn calculate_critical_path_variables(
        &self,
        state: &mut ArrowGraphState<K, R, W>,
        invalid_constraints: &[InvalidConstraint<K>],
    ) -> Result<bool, GraphError>;
}

/// The view of a graph builder the resource scheduler needs — the counterpart
/// of the C# `IResourceSchedulingGraph<…>`.
pub trait IResourceSchedulingGraph<K: Key, R: Key, W: Key> {
    fn activity(&self, id: K) -> &DependentActivity<K, R, W>;
    fn activity_mut(&mut self, id: K) -> &mut DependentActivity<K, R, W>;
    fn strong_activity_dependency_ids(&self, id: K) -> Vec<K>;
    fn clone_activities(&self) -> Vec<DependentActivity<K, R, W>>;
}

/// Resource scheduling and its surrounding pipeline — the counterpart of the C#
/// `IResourceSchedulingEngine<…>`.
pub trait IResourceSchedulingEngine<K: Key, R: Key, W: Key> {
    fn calculate_resource_schedules(
        &self,
        priority_list: &[K],
        filtered_resources: &[Resource<R, W>],
        infinite_resources: bool,
        graph: &mut dyn IResourceSchedulingGraph<K, R, W>,
    ) -> Result<Vec<ResourceSchedule<K, R, W>>, GraphError>;

    fn gather_unavailable_resources(
        &self,
        activities: &[&Activity<K, R, W>],
        filtered_resources: &[Resource<R, W>],
    ) -> Vec<UnavailableResources<K, R>>;

    fn replace_with_synthetic_resources(
        &self,
        resource_schedules: Vec<ResourceSchedule<K, R, W>>,
    ) -> Vec<ResourceSchedule<K, R, W>>;

    #[allow(clippy::too_many_arguments)]
    fn rebuild_aligned_resource_schedules(
        &self,
        resource_schedules: &[ResourceSchedule<K, R, W>],
        infinite_resources: bool,
        graph: &dyn IResourceSchedulingGraph<K, R, W>,
        final_activities: &[Activity<K, R, W>],
        start_time: i32,
        finish_time: i32,
    ) -> Result<Vec<ResourceSchedule<K, R, W>>, GraphError>;

    fn collect_indirect_resource_schedules(
        &self,
        filtered_resources: &[Resource<R, W>],
        scheduled_resources: &[ResourceSchedule<K, R, W>],
        final_activities: &[Activity<K, R, W>],
        start_time: i32,
        finish_time: i32,
    ) -> Result<Vec<ResourceSchedule<K, R, W>>, GraphError>;

    fn get_resource_phases_used(
        &self,
        total_schedules: &[ResourceSchedule<K, R, W>],
        workstreams_used: &IndexSet<W>,
    ) -> IndexSet<W>;
}

/// Transitive reduction for Activity-on-Vertex graphs — the counterpart of the
/// C# `ITransitiveReducer<T>` as implemented by `VertexTransitiveReducer`.
///
/// The C# reducer is bound to the state and SCC finder at construction (via a
/// factory); here the state is a parameter and the reducer uses the default
/// cycle detection internally. The injected SCC finder still governs the
/// builder's reported cycle detection ([`crate::VertexGraphBuilder::find_strong_circular_dependencies`]).
pub trait IVertexTransitiveReducer<K: Key, R: Key, W: Key> {
    fn get_ancestor_nodes_lookup(
        &self,
        state: &VertexGraphState<K, R, W>,
    ) -> Option<IndexMap<K, IndexSet<K>>>;

    fn reduce_graph(&self, state: &mut VertexGraphState<K, R, W>) -> bool;
}

/// Transitive reduction for Activity-on-Arrow graphs — the counterpart of the
/// C# `ITransitiveReducer<T>` as implemented by `ArrowTransitiveReducer`. Only
/// dummy edges are reduced.
pub trait IArrowTransitiveReducer<K: Key, R: Key, W: Key> {
    fn get_ancestor_nodes_lookup(
        &self,
        state: &ArrowGraphState<K, R, W>,
    ) -> Option<IndexMap<K, IndexSet<K>>>;

    fn reduce_graph(&self, state: &mut ArrowGraphState<K, R, W>) -> Result<bool, GraphError>;
}

/// All dummy-edge operations for Activity-on-Arrow graphs — the counterpart of
/// the C# `IDummyEdgeOrchestrator<…>`. Edge/activity IDs come from the injected
/// generators, passed per call (the builder owns them). The internal cycle
/// guards use the default Tarjan, as with the reducer.
pub trait IDummyEdgeOrchestrator<K: Key, R: Key, W: Key> {
    fn connect_with_dummy_edge(
        &self,
        state: &mut ArrowGraphState<K, R, W>,
        edge_id_generator: &mut dyn IIdGenerator<K>,
        activity_generator: &dyn IActivityGenerator<K, R, W>,
        tail_node_id: K,
        head_node_id: K,
    );

    fn remove_dummy_activity(
        &self,
        state: &mut ArrowGraphState<K, R, W>,
        activity_id: K,
    ) -> Result<bool, GraphError>;

    fn redirect_dummy_edges(
        &self,
        state: &mut ArrowGraphState<K, R, W>,
    ) -> Result<bool, GraphError>;

    fn remove_redundant_dummy_edges(
        &self,
        state: &mut ArrowGraphState<K, R, W>,
    ) -> Result<bool, GraphError>;
}
