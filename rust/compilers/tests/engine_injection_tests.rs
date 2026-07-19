//! Ports of `EngineInjectionTests` and `EngineBundleInjectionTests` from the C#
//! suite, demonstrating that the engine seams let a consumer inject custom
//! engines through the public builder constructors, programming only against
//! the public trait/state contracts.
//!
//! The C# spy counters use `private set`; here they use `AtomicUsize` (the
//! engine traits take `&self`, so the counters need interior mutability;
//! atomics keep the spies `Send + Sync`).
//!
//! Both ports inject the engines directly - they are stateless, so the builder
//! passes them the graph state and any collaborators per call - so these tests
//! assert the injected engine instance is invoked, that a custom reducer
//! consults the injected SCC finder, and that engines shared via `Arc` survive a
//! builder clone.

use std::sync::atomic::{AtomicUsize, Ordering};
use std::sync::Arc;

use indexmap::{IndexMap, IndexSet};
use zametek_maths_graphs_compilers::arrow::ArrowGraphState;
use zametek_maths_graphs_compilers::contracts::{
    IActivityGenerator, IArrowStronglyConnectedComponentsFinder, IDummyEdgeOrchestrator,
    IEventGenerator, IIdGenerator, IVertexCriticalPathEngine,
    IVertexStronglyConnectedComponentsFinder, IVertexTransitiveReducer,
};
use zametek_maths_graphs_compilers::vertex::VertexGraphState;
use zametek_maths_graphs_compilers::{
    ArrowGraphBuilder, ArrowGraphBuilderEngines, DummyEdgeOrchestrator, RemovableEventGenerator,
    VertexCriticalPathEngine, VertexGraphBuilder, VertexGraphBuilderEngines, VertexGraphCompiler,
    VertexTarjanStronglyConnectedComponentsFinder, VertexTransitiveReducer,
};
use zametek_maths_graphs_primitives::{
    CircularDependency, DependentActivity, Event, GraphError, InvalidConstraint,
};

type Act = DependentActivity<i32, i32, i32>;

// -- Spy engines --------------------------------------------------------------

/// Wraps the default vertex CPM engine and records how often each pass runs.
#[derive(Default)]
struct SpyVertexCriticalPathEngine {
    inner: VertexCriticalPathEngine,
    forward: AtomicUsize,
    backward: AtomicUsize,
    backfill: AtomicUsize,
}

impl IVertexCriticalPathEngine<i32, i32, i32> for SpyVertexCriticalPathEngine {
    fn calculate_critical_path_forward_flow(
        &self,
        state: &mut VertexGraphState<i32, i32, i32>,
        invalid_constraints: &[InvalidConstraint<i32>],
        shuffle: bool,
    ) -> Result<bool, GraphError> {
        self.forward.fetch_add(1, Ordering::Relaxed);
        self.inner
            .calculate_critical_path_forward_flow(state, invalid_constraints, shuffle)
    }

    fn calculate_critical_path_backward_flow(
        &self,
        state: &mut VertexGraphState<i32, i32, i32>,
        invalid_constraints: &[InvalidConstraint<i32>],
        shuffle: bool,
    ) -> Result<bool, GraphError> {
        self.backward.fetch_add(1, Ordering::Relaxed);
        self.inner
            .calculate_critical_path_backward_flow(state, invalid_constraints, shuffle)
    }

    fn back_fill_isolated_nodes(
        &self,
        state: &mut VertexGraphState<i32, i32, i32>,
        invalid_constraints: &[InvalidConstraint<i32>],
    ) -> bool {
        self.backfill.fetch_add(1, Ordering::Relaxed);
        self.inner
            .back_fill_isolated_nodes(state, invalid_constraints)
    }
}

/// Wraps the default event generator and counts how often it is asked for an event.
#[derive(Default)]
struct CountingEventGenerator {
    inner: RemovableEventGenerator,
    count: AtomicUsize,
}

impl IEventGenerator<i32> for CountingEventGenerator {
    fn generate(&self, id: i32) -> Event<i32> {
        self.count.fetch_add(1, Ordering::Relaxed);
        self.inner.generate(id)
    }

    fn generate_with_times(
        &self,
        id: i32,
        earliest_finish_time: Option<i32>,
        latest_finish_time: Option<i32>,
    ) -> Event<i32> {
        self.count.fetch_add(1, Ordering::Relaxed);
        self.inner
            .generate_with_times(id, earliest_finish_time, latest_finish_time)
    }
}

/// Wraps the default vertex reducer and counts how often reduction runs.
#[derive(Default)]
struct CountingVertexTransitiveReducer {
    inner: VertexTransitiveReducer,
    reduce_count: AtomicUsize,
}

impl IVertexTransitiveReducer<i32, i32, i32> for CountingVertexTransitiveReducer {
    fn get_ancestor_nodes_lookup(
        &self,
        state: &VertexGraphState<i32, i32, i32>,
        scc_finder: &dyn IVertexStronglyConnectedComponentsFinder<i32, i32, i32>,
    ) -> Option<IndexMap<i32, IndexSet<i32>>> {
        self.inner.get_ancestor_nodes_lookup(state, scc_finder)
    }

    fn reduce_graph(
        &self,
        state: &mut VertexGraphState<i32, i32, i32>,
        scc_finder: &dyn IVertexStronglyConnectedComponentsFinder<i32, i32, i32>,
    ) -> bool {
        self.reduce_count.fetch_add(1, Ordering::Relaxed);
        self.inner.reduce_graph(state, scc_finder)
    }
}

/// Wraps the default vertex SCC finder and counts how often it is consulted, so
/// a test can prove the injected finder drives the reducer's cycle detection.
#[derive(Default)]
struct SpyVertexSccFinder {
    inner: VertexTarjanStronglyConnectedComponentsFinder,
    circular_calls: AtomicUsize,
}

impl IVertexStronglyConnectedComponentsFinder<i32, i32, i32> for SpyVertexSccFinder {
    fn find_strongly_connected_components(
        &self,
        state: &VertexGraphState<i32, i32, i32>,
        ignore_dummies: bool,
    ) -> Vec<CircularDependency<i32>> {
        self.inner
            .find_strongly_connected_components(state, ignore_dummies)
    }

    fn find_strongly_circular_dependencies(
        &self,
        state: &VertexGraphState<i32, i32, i32>,
        ignore_dummies: bool,
    ) -> Vec<CircularDependency<i32>> {
        self.circular_calls.fetch_add(1, Ordering::Relaxed);
        self.inner
            .find_strongly_circular_dependencies(state, ignore_dummies)
    }
}

/// Wraps the default dummy-edge orchestrator and counts how often it wires an edge.
#[derive(Default)]
struct CountingDummyEdgeOrchestrator {
    inner: DummyEdgeOrchestrator,
    connect_count: AtomicUsize,
}

impl IDummyEdgeOrchestrator<i32, i32, i32> for CountingDummyEdgeOrchestrator {
    fn connect_with_dummy_edge(
        &self,
        state: &mut ArrowGraphState<i32, i32, i32>,
        edge_id_generator: &mut dyn IIdGenerator<i32>,
        activity_generator: &dyn IActivityGenerator<i32, i32, i32>,
        tail_node_id: i32,
        head_node_id: i32,
    ) {
        self.connect_count.fetch_add(1, Ordering::Relaxed);
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
        state: &mut ArrowGraphState<i32, i32, i32>,
        activity_id: i32,
    ) -> Result<bool, GraphError> {
        self.inner.remove_dummy_activity(state, activity_id)
    }

    fn redirect_dummy_edges(
        &self,
        state: &mut ArrowGraphState<i32, i32, i32>,
        scc_finder: &dyn IArrowStronglyConnectedComponentsFinder<i32, i32, i32>,
    ) -> Result<bool, GraphError> {
        self.inner.redirect_dummy_edges(state, scc_finder)
    }

    fn remove_redundant_dummy_edges(
        &self,
        state: &mut ArrowGraphState<i32, i32, i32>,
        scc_finder: &dyn IArrowStronglyConnectedComponentsFinder<i32, i32, i32>,
    ) -> Result<bool, GraphError> {
        self.inner.remove_redundant_dummy_edges(state, scc_finder)
    }
}

// -- EngineInjectionTests -----------------------------------------------------

#[test]
fn vertex_given_injected_critical_path_engine_then_custom_engine_is_used_during_compile() {
    let spy = Arc::new(SpyVertexCriticalPathEngine::default());
    let engine: Arc<dyn IVertexCriticalPathEngine<i32, i32, i32>> = spy.clone();

    let builder = VertexGraphBuilder::with_engines(VertexGraphBuilderEngines {
        critical_path_engine: engine,
        ..Default::default()
    });
    let mut compiler = VertexGraphCompiler::with_builder(builder);

    compiler.add_activity(Act::new(1, 3));
    compiler.add_activity(Act::with_dependencies(2, 5, [1]));

    compiler.compile().unwrap();

    assert!(
        spy.forward.load(Ordering::Relaxed) > 0,
        "forward pass should have run"
    );
    assert!(
        spy.backward.load(Ordering::Relaxed) > 0,
        "backward pass should have run"
    );
}

#[test]
fn vertex_given_injected_event_generator_then_custom_generator_is_used() {
    let counting = Arc::new(CountingEventGenerator::default());
    let event_generator: Arc<dyn IEventGenerator<i32>> = counting.clone();

    let mut builder = VertexGraphBuilder::with_engines(VertexGraphBuilderEngines {
        event_generator,
        ..Default::default()
    });

    // Activity 2 depends on activity 1, so linking them creates an edge whose
    // event is produced by the injected generator.
    builder.add_activity(Act::new(1, 3));
    builder.add_activity_with_dependencies(Act::new(2, 5), IndexSet::from([1]));

    assert!(
        counting.count.load(Ordering::Relaxed) > 0,
        "event generator should have been used"
    );
}

#[test]
fn vertex_given_injected_scc_finder_then_reduction_consults_it() {
    let spy = Arc::new(SpyVertexSccFinder::default());
    let scc_finder: Arc<dyn IVertexStronglyConnectedComponentsFinder<i32, i32, i32>> = spy.clone();

    let mut builder = VertexGraphBuilder::with_engines(VertexGraphBuilderEngines {
        scc_finder,
        ..Default::default()
    });
    builder.add_activity(Act::new(1, 1));
    builder.add_activity_with_dependencies(Act::new(2, 1), IndexSet::from([1]));
    builder.add_activity_with_dependencies(Act::new(3, 1), IndexSet::from([1, 2]));

    let before = spy.circular_calls.load(Ordering::Relaxed);
    assert!(builder.transitive_reduction());

    // The reducer drives its cycle guard through the *injected* SCC finder, not a
    // hard-wired default - this is the parity gap the stateless refactor closed.
    assert!(
        spy.circular_calls.load(Ordering::Relaxed) > before,
        "reduction should consult the injected SCC finder"
    );
}

// -- EngineBundleInjectionTests -----------------------------------------------

#[test]
fn vertex_given_default_engines_bundle_then_compiles_successfully() {
    let builder = VertexGraphBuilder::with_engines(VertexGraphBuilderEngines::default());
    let mut compiler = VertexGraphCompiler::with_builder(builder);

    compiler.add_activity(Act::new(1, 3));
    compiler.add_activity(Act::with_dependencies(2, 5, [1]));

    let output = compiler.compile().unwrap();

    assert!(output.compilation_errors.is_empty());
    assert_eq!(compiler.finish_time(), 8);
}

#[test]
fn vertex_given_injected_transitive_reducer_then_reducer_is_used() {
    let reducer = Arc::new(CountingVertexTransitiveReducer::default());
    let transitive_reducer: Arc<dyn IVertexTransitiveReducer<i32, i32, i32>> = reducer.clone();

    let mut builder = VertexGraphBuilder::with_engines(VertexGraphBuilderEngines {
        transitive_reducer,
        ..Default::default()
    });

    builder.add_activity(Act::new(1, 1));
    builder.add_activity_with_dependencies(Act::new(2, 1), IndexSet::from([1]));
    builder.add_activity_with_dependencies(Act::new(3, 1), IndexSet::from([1, 2]));

    assert!(builder.transitive_reduction());

    // The injected reducer performed the reduction: the direct 1 -> 3 dependency
    // is redundant (implied via 2) and is removed.
    assert_eq!(builder.activity_dependency_ids(3), vec![2]);
    assert!(
        reducer.reduce_count.load(Ordering::Relaxed) > 0,
        "injected reducer should have run"
    );
}

#[test]
fn vertex_given_injected_transitive_reducer_then_reducer_survives_clone() {
    let reducer = Arc::new(CountingVertexTransitiveReducer::default());
    let transitive_reducer: Arc<dyn IVertexTransitiveReducer<i32, i32, i32>> = reducer.clone();

    let mut builder = VertexGraphBuilder::with_engines(VertexGraphBuilderEngines {
        transitive_reducer,
        ..Default::default()
    });
    builder.add_activity(Act::new(1, 1));
    builder.add_activity_with_dependencies(Act::new(2, 1), IndexSet::from([1]));
    builder.add_activity_with_dependencies(Act::new(3, 1), IndexSet::from([1, 2]));

    let count_before_clone = reducer.reduce_count.load(Ordering::Relaxed);

    // The clone shares the injected engines (Arc), so reducing on the clone
    // drives the same counter - the injected reducer survives the clone.
    let mut clone = builder.clone_builder().unwrap();
    assert!(clone.transitive_reduction());

    assert!(
        reducer.reduce_count.load(Ordering::Relaxed) > count_before_clone,
        "clone should use the injected reducer"
    );
    assert_eq!(clone.activity_dependency_ids(3), vec![2]);
}

#[test]
fn arrow_given_default_engines_bundle_then_builds_graph() {
    let mut builder =
        ArrowGraphBuilder::<i32, i32, i32>::with_engines(ArrowGraphBuilderEngines::default());

    builder.add_activity_with_dependencies(Act::new(1, 3), IndexSet::new());
    builder.add_activity_with_dependencies(Act::new(2, 5), IndexSet::from([1]));

    // The arrow builder also mints dummy activities, so check containment.
    assert!(builder.activity_ids().contains(&1));
    assert!(builder.activity_ids().contains(&2));
}

#[test]
fn arrow_given_injected_orchestrator_then_orchestrator_is_used() {
    let orchestrator = Arc::new(CountingDummyEdgeOrchestrator::default());
    let dummy_edge_orchestrator: Arc<dyn IDummyEdgeOrchestrator<i32, i32, i32>> =
        orchestrator.clone();

    let mut builder = ArrowGraphBuilder::<i32, i32, i32>::with_engines(ArrowGraphBuilderEngines {
        dummy_edge_orchestrator,
        ..Default::default()
    });

    builder.add_activity_with_dependencies(Act::new(1, 3), IndexSet::new());
    builder.add_activity_with_dependencies(Act::new(2, 5), IndexSet::from([1]));

    // The injected orchestrator wired up the dummy edges.
    assert!(builder.edge_ids().len() > 2);
    assert!(
        orchestrator.connect_count.load(Ordering::Relaxed) > 0,
        "injected orchestrator should have wired dummy edges"
    );
}
