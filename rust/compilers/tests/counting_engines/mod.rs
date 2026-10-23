//! Engines that count the calls made to them, and pass each call on to the
//! engine that they wrap. A test uses them to show that the engine that it
//! injected is the engine that does the work. They are the counterpart of the C#
//! `CountingProxy`: Rust has no proxy that works for any trait, and so there is
//! one wrapper for each engine trait.
//!
//! `counted_vertex_engines` and `counted_arrow_engines` make an engines bundle in
//! which every engine is a counting wrapper around the standard engine, and the
//! counters of the engines. The bundle is moved into a builder or a factory, and
//! the test keeps the counters.

#![allow(dead_code)]

use indexmap::{IndexMap, IndexSet};
use std::sync::atomic::{AtomicUsize, Ordering};
use std::sync::Arc;
use zametek_maths_graphs_compilers::arrow::{ArrowGraphBuilderEngines, ArrowGraphState};
use zametek_maths_graphs_compilers::contracts::{
    IActivityGenerator, IArrowCriticalPathEngine, IArrowStronglyConnectedComponentsFinder,
    IArrowTransitiveReducer, IDummyEdgeOrchestrator, IEventGenerator, IIdGenerator,
    IResourceSchedulingEngine, IResourceSchedulingGraph, IVertexCriticalPathEngine,
    IVertexStronglyConnectedComponentsFinder, IVertexTransitiveReducer,
};
use zametek_maths_graphs_compilers::primitives::{
    Activity, CircularDependency, DependentActivity, Event, GraphError, InvalidConstraint,
    Resource, ResourceSchedule, UnavailableResources,
};
use zametek_maths_graphs_compilers::vertex::{
    IncrementalCriticalPath, VertexGraphBuilderEngines, VertexGraphState,
};

/// The number of calls made to an engine. The engine records them, and the
/// test, which keeps a clone, reads them.
#[derive(Clone, Default)]
pub struct Calls(Arc<AtomicUsize>);

impl Calls {
    pub fn count(&self) -> usize {
        self.0.load(Ordering::Relaxed)
    }

    fn record(&self) {
        self.0.fetch_add(1, Ordering::Relaxed);
    }
}

// A struct that holds the engine to wrap and the counter, with `wrap` to make
// it. The calls to the engine are counted by the impl of the trait.
macro_rules! counting {
    ($name:ident, $engine:ty) => {
        pub struct $name {
            inner: Arc<$engine>,
            calls: Calls,
        }

        impl $name {
            /// Wraps the engine. The `Calls` is the one to read.
            pub fn wrap(inner: Arc<$engine>) -> (Self, Calls) {
                let calls = Calls::default();
                (
                    Self {
                        inner,
                        calls: calls.clone(),
                    },
                    calls,
                )
            }
        }
    };
}

type Act = DependentActivity<i32, i32, i32>;
type VertexState = VertexGraphState<i32, i32, i32>;
type ArrowState = ArrowGraphState<i32, i32, i32>;
type Lookup = Option<IndexMap<i32, IndexSet<i32>>>;

// -- The wrappers -------------------------------------------------------------

counting!(CountingEventGenerator, dyn IEventGenerator<i32>);

impl IEventGenerator<i32> for CountingEventGenerator {
    fn generate(&self, id: i32) -> Event<i32> {
        self.calls.record();
        self.inner.generate(id)
    }

    fn generate_with_times(
        &self,
        id: i32,
        earliest_finish_time: Option<i32>,
        latest_finish_time: Option<i32>,
    ) -> Event<i32> {
        self.calls.record();
        self.inner
            .generate_with_times(id, earliest_finish_time, latest_finish_time)
    }
}

counting!(
    CountingActivityGenerator,
    dyn IActivityGenerator<i32, i32, i32>
);

impl IActivityGenerator<i32, i32, i32> for CountingActivityGenerator {
    fn generate(&self, id: i32) -> Act {
        self.calls.record();
        self.inner.generate(id)
    }
}

counting!(
    CountingVertexSccFinder,
    dyn IVertexStronglyConnectedComponentsFinder<i32, i32, i32>
);

impl IVertexStronglyConnectedComponentsFinder<i32, i32, i32> for CountingVertexSccFinder {
    fn find_strongly_connected_components(
        &self,
        state: &VertexState,
        ignore_dummies: bool,
    ) -> Vec<CircularDependency<i32>> {
        self.calls.record();
        self.inner
            .find_strongly_connected_components(state, ignore_dummies)
    }

    fn find_strongly_circular_dependencies(
        &self,
        state: &VertexState,
        ignore_dummies: bool,
    ) -> Vec<CircularDependency<i32>> {
        self.calls.record();
        self.inner
            .find_strongly_circular_dependencies(state, ignore_dummies)
    }
}

counting!(
    CountingArrowSccFinder,
    dyn IArrowStronglyConnectedComponentsFinder<i32, i32, i32>
);

impl IArrowStronglyConnectedComponentsFinder<i32, i32, i32> for CountingArrowSccFinder {
    fn find_strongly_connected_components(
        &self,
        state: &ArrowState,
        ignore_dummies: bool,
    ) -> Vec<CircularDependency<i32>> {
        self.calls.record();
        self.inner
            .find_strongly_connected_components(state, ignore_dummies)
    }

    fn find_strongly_circular_dependencies(
        &self,
        state: &ArrowState,
        ignore_dummies: bool,
    ) -> Vec<CircularDependency<i32>> {
        self.calls.record();
        self.inner
            .find_strongly_circular_dependencies(state, ignore_dummies)
    }
}

counting!(
    CountingVertexCriticalPathEngine,
    dyn IVertexCriticalPathEngine<i32, i32, i32>
);

impl IVertexCriticalPathEngine<i32, i32, i32> for CountingVertexCriticalPathEngine {
    fn calculate_critical_path_forward_flow(
        &self,
        state: &mut VertexState,
        invalid_constraints: &[InvalidConstraint<i32>],
        shuffle: bool,
    ) -> Result<bool, GraphError> {
        self.calls.record();
        self.inner
            .calculate_critical_path_forward_flow(state, invalid_constraints, shuffle)
    }

    fn calculate_critical_path_backward_flow(
        &self,
        state: &mut VertexState,
        invalid_constraints: &[InvalidConstraint<i32>],
        shuffle: bool,
    ) -> Result<bool, GraphError> {
        self.calls.record();
        self.inner
            .calculate_critical_path_backward_flow(state, invalid_constraints, shuffle)
    }

    fn back_fill_isolated_nodes(
        &self,
        state: &mut VertexState,
        invalid_constraints: &[InvalidConstraint<i32>],
    ) -> bool {
        self.calls.record();
        self.inner
            .back_fill_isolated_nodes(state, invalid_constraints)
    }

    fn begin_incremental_critical_path(
        &self,
        state: &VertexState,
    ) -> Option<IncrementalCriticalPath<i32>> {
        self.calls.record();
        self.inner.begin_incremental_critical_path(state)
    }
}

counting!(
    CountingArrowCriticalPathEngine,
    dyn IArrowCriticalPathEngine<i32, i32, i32>
);

impl IArrowCriticalPathEngine<i32, i32, i32> for CountingArrowCriticalPathEngine {
    fn calculate_event_earliest_finish_times(
        &self,
        state: &mut ArrowState,
        invalid_constraints: &[InvalidConstraint<i32>],
        shuffle: bool,
    ) -> Result<bool, GraphError> {
        self.calls.record();
        self.inner
            .calculate_event_earliest_finish_times(state, invalid_constraints, shuffle)
    }

    fn calculate_event_latest_finish_times(
        &self,
        state: &mut ArrowState,
        invalid_constraints: &[InvalidConstraint<i32>],
        shuffle: bool,
    ) -> Result<bool, GraphError> {
        self.calls.record();
        self.inner
            .calculate_event_latest_finish_times(state, invalid_constraints, shuffle)
    }

    fn calculate_critical_path_variables(
        &self,
        state: &mut ArrowState,
        invalid_constraints: &[InvalidConstraint<i32>],
    ) -> Result<bool, GraphError> {
        self.calls.record();
        self.inner
            .calculate_critical_path_variables(state, invalid_constraints)
    }
}

counting!(
    CountingScheduler,
    dyn IResourceSchedulingEngine<i32, i32, i32>
);

impl IResourceSchedulingEngine<i32, i32, i32> for CountingScheduler {
    fn calculate_resource_schedules(
        &self,
        priority_list: &[i32],
        filtered_resources: &[Resource<i32, i32>],
        infinite_resources: bool,
        graph: &mut dyn IResourceSchedulingGraph<i32, i32, i32>,
    ) -> Result<Vec<ResourceSchedule<i32, i32, i32>>, GraphError> {
        self.calls.record();
        self.inner.calculate_resource_schedules(
            priority_list,
            filtered_resources,
            infinite_resources,
            graph,
        )
    }

    fn gather_unavailable_resources(
        &self,
        activities: &[&Activity<i32, i32, i32>],
        filtered_resources: &[Resource<i32, i32>],
    ) -> Vec<UnavailableResources<i32, i32>> {
        self.calls.record();
        self.inner
            .gather_unavailable_resources(activities, filtered_resources)
    }

    fn replace_with_synthetic_resources(
        &self,
        resource_schedules: Vec<ResourceSchedule<i32, i32, i32>>,
    ) -> Vec<ResourceSchedule<i32, i32, i32>> {
        self.calls.record();
        self.inner
            .replace_with_synthetic_resources(resource_schedules)
    }

    fn rebuild_aligned_resource_schedules(
        &self,
        resource_schedules: &[ResourceSchedule<i32, i32, i32>],
        infinite_resources: bool,
        graph: &dyn IResourceSchedulingGraph<i32, i32, i32>,
        final_activities: &[Activity<i32, i32, i32>],
        start_time: i32,
        finish_time: i32,
    ) -> Result<Vec<ResourceSchedule<i32, i32, i32>>, GraphError> {
        self.calls.record();
        self.inner.rebuild_aligned_resource_schedules(
            resource_schedules,
            infinite_resources,
            graph,
            final_activities,
            start_time,
            finish_time,
        )
    }

    fn collect_indirect_resource_schedules(
        &self,
        filtered_resources: &[Resource<i32, i32>],
        scheduled_resources: &[ResourceSchedule<i32, i32, i32>],
        final_activities: &[Activity<i32, i32, i32>],
        start_time: i32,
        finish_time: i32,
    ) -> Result<Vec<ResourceSchedule<i32, i32, i32>>, GraphError> {
        self.calls.record();
        self.inner.collect_indirect_resource_schedules(
            filtered_resources,
            scheduled_resources,
            final_activities,
            start_time,
            finish_time,
        )
    }

    fn get_resource_phases_used(
        &self,
        total_schedules: &[ResourceSchedule<i32, i32, i32>],
        workstreams_used: &IndexSet<i32>,
    ) -> IndexSet<i32> {
        self.calls.record();
        self.inner
            .get_resource_phases_used(total_schedules, workstreams_used)
    }
}

counting!(
    CountingVertexTransitiveReducer,
    dyn IVertexTransitiveReducer<i32, i32, i32>
);

impl IVertexTransitiveReducer<i32, i32, i32> for CountingVertexTransitiveReducer {
    fn get_ancestor_nodes_lookup(
        &self,
        state: &VertexState,
        scc_finder: &dyn IVertexStronglyConnectedComponentsFinder<i32, i32, i32>,
    ) -> Lookup {
        self.calls.record();
        self.inner.get_ancestor_nodes_lookup(state, scc_finder)
    }

    fn reduce_graph(
        &self,
        state: &mut VertexState,
        scc_finder: &dyn IVertexStronglyConnectedComponentsFinder<i32, i32, i32>,
    ) -> bool {
        self.calls.record();
        self.inner.reduce_graph(state, scc_finder)
    }
}

counting!(
    CountingArrowTransitiveReducer,
    dyn IArrowTransitiveReducer<i32, i32, i32>
);

impl IArrowTransitiveReducer<i32, i32, i32> for CountingArrowTransitiveReducer {
    fn get_ancestor_nodes_lookup(
        &self,
        state: &ArrowState,
        scc_finder: &dyn IArrowStronglyConnectedComponentsFinder<i32, i32, i32>,
    ) -> Lookup {
        self.calls.record();
        self.inner.get_ancestor_nodes_lookup(state, scc_finder)
    }

    fn reduce_graph(
        &self,
        state: &mut ArrowState,
        scc_finder: &dyn IArrowStronglyConnectedComponentsFinder<i32, i32, i32>,
        orchestrator: &dyn IDummyEdgeOrchestrator<i32, i32, i32>,
    ) -> Result<bool, GraphError> {
        self.calls.record();
        self.inner.reduce_graph(state, scc_finder, orchestrator)
    }
}

counting!(
    CountingDummyEdgeOrchestrator,
    dyn IDummyEdgeOrchestrator<i32, i32, i32>
);

impl IDummyEdgeOrchestrator<i32, i32, i32> for CountingDummyEdgeOrchestrator {
    fn connect_with_dummy_edge(
        &self,
        state: &mut ArrowState,
        edge_id_generator: &mut dyn IIdGenerator<i32>,
        activity_generator: &dyn IActivityGenerator<i32, i32, i32>,
        tail_node_id: i32,
        head_node_id: i32,
    ) {
        self.calls.record();
        self.inner.connect_with_dummy_edge(
            state,
            edge_id_generator,
            activity_generator,
            tail_node_id,
            head_node_id,
        )
    }

    fn remove_dummy_activity(
        &self,
        state: &mut ArrowState,
        activity_id: i32,
    ) -> Result<bool, GraphError> {
        self.calls.record();
        self.inner.remove_dummy_activity(state, activity_id)
    }

    fn redirect_dummy_edges(
        &self,
        state: &mut ArrowState,
        scc_finder: &dyn IArrowStronglyConnectedComponentsFinder<i32, i32, i32>,
    ) -> Result<bool, GraphError> {
        self.calls.record();
        self.inner.redirect_dummy_edges(state, scc_finder)
    }

    fn remove_redundant_dummy_edges(
        &self,
        state: &mut ArrowState,
        scc_finder: &dyn IArrowStronglyConnectedComponentsFinder<i32, i32, i32>,
    ) -> Result<bool, GraphError> {
        self.calls.record();
        self.inner.remove_redundant_dummy_edges(state, scc_finder)
    }
}

// -- The bundles --------------------------------------------------------------

/// The counters of the engines of a bundle, by the name of the field of the
/// bundle.
pub struct Counters(Vec<(&'static str, Calls)>);

impl Counters {
    /// The number of calls to the engine with the given field name.
    pub fn calls_to(&self, engine: &str) -> usize {
        self.0
            .iter()
            .find(|x| x.0 == engine)
            .unwrap_or_else(|| panic!("there is no engine named {engine}"))
            .1
            .count()
    }

    /// The names of the engines that have had no call.
    pub fn idle(&self) -> Vec<&'static str> {
        let mut names: Vec<&'static str> = self
            .0
            .iter()
            .filter(|x| x.1.count() == 0)
            .map(|x| x.0)
            .collect();
        names.sort_unstable();
        names
    }
}

/// An engines bundle for a vertex builder in which every engine counts its
/// calls, and the counters.
pub fn counted_vertex_engines() -> (VertexGraphBuilderEngines<i32, i32, i32>, Counters) {
    let standard = VertexGraphBuilderEngines::<i32, i32, i32>::default();
    let (event_generator, event_generator_calls) =
        CountingEventGenerator::wrap(standard.event_generator);
    let (scc_finder, scc_finder_calls) = CountingVertexSccFinder::wrap(standard.scc_finder);
    let (critical_path_engine, critical_path_engine_calls) =
        CountingVertexCriticalPathEngine::wrap(standard.critical_path_engine);
    let (transitive_reducer, transitive_reducer_calls) =
        CountingVertexTransitiveReducer::wrap(standard.transitive_reducer);
    let (scheduler, scheduler_calls) = CountingScheduler::wrap(standard.resource_scheduling_engine);
    (
        VertexGraphBuilderEngines {
            edge_id_generator: standard.edge_id_generator,
            event_generator: Arc::new(event_generator),
            scc_finder: Arc::new(scc_finder),
            critical_path_engine: Arc::new(critical_path_engine),
            transitive_reducer: Arc::new(transitive_reducer),
            resource_scheduling_engine: Arc::new(scheduler),
        },
        Counters(vec![
            ("event_generator", event_generator_calls),
            ("scc_finder", scc_finder_calls),
            ("critical_path_engine", critical_path_engine_calls),
            ("transitive_reducer", transitive_reducer_calls),
            ("resource_scheduling_engine", scheduler_calls),
        ]),
    )
}

/// An engines bundle for an arrow builder in which every engine counts its
/// calls, and the counters.
pub fn counted_arrow_engines() -> (ArrowGraphBuilderEngines<i32, i32, i32>, Counters) {
    let standard = ArrowGraphBuilderEngines::<i32, i32, i32>::default();
    let (dummy_activity_generator, dummy_activity_generator_calls) =
        CountingActivityGenerator::wrap(standard.dummy_activity_generator);
    let (event_generator, event_generator_calls) =
        CountingEventGenerator::wrap(standard.event_generator);
    let (scc_finder, scc_finder_calls) = CountingArrowSccFinder::wrap(standard.scc_finder);
    let (critical_path_engine, critical_path_engine_calls) =
        CountingArrowCriticalPathEngine::wrap(standard.critical_path_engine);
    let (transitive_reducer, transitive_reducer_calls) =
        CountingArrowTransitiveReducer::wrap(standard.transitive_reducer);
    let (dummy_edge_orchestrator, dummy_edge_orchestrator_calls) =
        CountingDummyEdgeOrchestrator::wrap(standard.dummy_edge_orchestrator);
    let (scheduler, scheduler_calls) = CountingScheduler::wrap(standard.resource_scheduling_engine);
    (
        ArrowGraphBuilderEngines {
            edge_id_generator: standard.edge_id_generator,
            node_id_generator: standard.node_id_generator,
            dummy_activity_generator: Arc::new(dummy_activity_generator),
            event_generator: Arc::new(event_generator),
            scc_finder: Arc::new(scc_finder),
            critical_path_engine: Arc::new(critical_path_engine),
            transitive_reducer: Arc::new(transitive_reducer),
            dummy_edge_orchestrator: Arc::new(dummy_edge_orchestrator),
            resource_scheduling_engine: Arc::new(scheduler),
        },
        Counters(vec![
            ("dummy_activity_generator", dummy_activity_generator_calls),
            ("event_generator", event_generator_calls),
            ("scc_finder", scc_finder_calls),
            ("critical_path_engine", critical_path_engine_calls),
            ("transitive_reducer", transitive_reducer_calls),
            ("dummy_edge_orchestrator", dummy_edge_orchestrator_calls),
            ("resource_scheduling_engine", scheduler_calls),
        ]),
    )
}
