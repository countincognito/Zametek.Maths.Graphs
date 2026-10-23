//! Default engines for Activity-on-Arrow graphs, and the engines bundle passed
//! to the builder - the counterparts of the C# default engine classes and
//! `ArrowGraphBuilderEngines`.

use super::builder::ArrowGraphBuilder;
use super::compiler::ArrowGraphCompiler;
use super::orchestrator::DummyEdgeOrchestrator;
use super::state::{ArrowGraphState, ArrowTraversal};
use super::{cpm, reducer};
use crate::contracts::{
    IActivityGenerator, IArrowCriticalPathEngine, IArrowGraphFactory,
    IArrowStronglyConnectedComponentsFinder, IArrowTransitiveReducer, IDummyEdgeOrchestrator,
    IEventGenerator, IIdGenerator, IResourceSchedulingEngine,
};
use crate::generators::{DummyActivityGenerator, EventGenerator};
use crate::id_gen::PreviousIdGenerator;
use crate::scheduling::PriorityListResourceScheduler;
use crate::tarjan;
use indexmap::{IndexMap, IndexSet};
use std::sync::Arc;
use zametek_maths_graphs_primitives::{CircularDependency, GraphError, InvalidConstraint, Key};

/// Default Tarjan SCC finder for Activity-on-Arrow graphs (edge-space
/// traversal) - the counterpart of the C#
/// `ArrowTarjanStronglyConnectedComponentsFinder<…>`.
#[derive(Debug, Clone, Copy, Default)]
pub struct ArrowTarjanStronglyConnectedComponentsFinder;

impl<K: Key, R: Key, W: Key> IArrowStronglyConnectedComponentsFinder<K, R, W>
    for ArrowTarjanStronglyConnectedComponentsFinder
{
    fn find_strongly_connected_components(
        &self,
        state: &ArrowGraphState<K, R, W>,
        ignore_dummies: bool,
    ) -> Vec<CircularDependency<K>> {
        tarjan::find_strongly_connected_components(&ArrowTraversal { state }, ignore_dummies)
    }

    fn find_strongly_circular_dependencies(
        &self,
        state: &ArrowGraphState<K, R, W>,
        ignore_dummies: bool,
    ) -> Vec<CircularDependency<K>> {
        tarjan::find_strongly_circular_dependencies(&ArrowTraversal { state }, ignore_dummies)
    }
}

/// Default critical-path engine for Activity-on-Arrow graphs - the counterpart
/// of the C# `ArrowCriticalPathEngine<…>`.
#[derive(Debug, Clone, Copy, Default)]
pub struct ArrowCriticalPathEngine;

impl<K: Key, R: Key, W: Key> IArrowCriticalPathEngine<K, R, W> for ArrowCriticalPathEngine {
    fn calculate_event_earliest_finish_times(
        &self,
        state: &mut ArrowGraphState<K, R, W>,
        invalid_constraints: &[InvalidConstraint<K>],
        shuffle: bool,
    ) -> Result<bool, GraphError> {
        cpm::calculate_event_earliest_finish_times(state, invalid_constraints, shuffle)
    }

    fn calculate_event_latest_finish_times(
        &self,
        state: &mut ArrowGraphState<K, R, W>,
        invalid_constraints: &[InvalidConstraint<K>],
        shuffle: bool,
    ) -> Result<bool, GraphError> {
        cpm::calculate_event_latest_finish_times(state, invalid_constraints, shuffle)
    }

    fn calculate_critical_path_variables(
        &self,
        state: &mut ArrowGraphState<K, R, W>,
        invalid_constraints: &[InvalidConstraint<K>],
    ) -> Result<bool, GraphError> {
        cpm::calculate_critical_path_variables(state, invalid_constraints)
    }
}

/// Default transitive reducer for Activity-on-Arrow graphs - the counterpart of
/// the C# `ArrowTransitiveReducer<…>`.
#[derive(Debug, Clone, Copy, Default)]
pub struct ArrowTransitiveReducer;

impl<K: Key, R: Key, W: Key> IArrowTransitiveReducer<K, R, W> for ArrowTransitiveReducer {
    fn get_ancestor_nodes_lookup(
        &self,
        state: &ArrowGraphState<K, R, W>,
        scc_finder: &dyn IArrowStronglyConnectedComponentsFinder<K, R, W>,
    ) -> Option<IndexMap<K, IndexSet<K>>> {
        reducer::get_ancestor_nodes_lookup(state, scc_finder)
    }

    fn reduce_graph(
        &self,
        state: &mut ArrowGraphState<K, R, W>,
        scc_finder: &dyn IArrowStronglyConnectedComponentsFinder<K, R, W>,
        orchestrator: &dyn IDummyEdgeOrchestrator<K, R, W>,
    ) -> Result<bool, GraphError> {
        reducer::reduce_graph(state, scc_finder, orchestrator)
    }
}

/// A bundle of the engines an [`ArrowGraphBuilder`](super::ArrowGraphBuilder)
/// relies on, each defaulting to the standard implementation - the counterpart
/// of the C# `ArrowGraphBuilderEngines`. Set only the fields you want to
/// customise; the rest keep their defaults.
///
/// Injected engines are shared (`Arc`) and preserved across a builder clone;
/// the ID generators carry per-graph counters, so they are held by value and
/// recreated on clone.
pub struct ArrowGraphBuilderEngines<K: Key, R: Key, W: Key> {
    pub edge_id_generator: Box<dyn IIdGenerator<K>>,
    pub node_id_generator: Box<dyn IIdGenerator<K>>,
    pub dummy_activity_generator: Arc<dyn IActivityGenerator<K, R, W>>,
    pub event_generator: Arc<dyn IEventGenerator<K>>,
    pub scc_finder: Arc<dyn IArrowStronglyConnectedComponentsFinder<K, R, W>>,
    pub critical_path_engine: Arc<dyn IArrowCriticalPathEngine<K, R, W>>,
    pub transitive_reducer: Arc<dyn IArrowTransitiveReducer<K, R, W>>,
    pub dummy_edge_orchestrator: Arc<dyn IDummyEdgeOrchestrator<K, R, W>>,
    pub resource_scheduling_engine: Arc<dyn IResourceSchedulingEngine<K, R, W>>,
}

impl<K: Key, R: Key, W: Key> Default for ArrowGraphBuilderEngines<K, R, W> {
    fn default() -> Self {
        Self {
            edge_id_generator: Box::new(PreviousIdGenerator::default()),
            node_id_generator: Box::new(PreviousIdGenerator::default()),
            dummy_activity_generator: Arc::new(DummyActivityGenerator),
            // Arrow-graph events are real milestones: read-only, not removable.
            event_generator: Arc::new(EventGenerator),
            scc_finder: Arc::new(ArrowTarjanStronglyConnectedComponentsFinder),
            critical_path_engine: Arc::new(ArrowCriticalPathEngine),
            transitive_reducer: Arc::new(ArrowTransitiveReducer),
            dummy_edge_orchestrator: Arc::new(DummyEdgeOrchestrator),
            resource_scheduling_engine: Arc::new(PriorityListResourceScheduler),
        }
    }
}

/// The standard factory of Activity-on-Arrow graphs - the counterpart of the C#
/// `ArrowGraphFactory`. Its builders have the engines of an
/// [`ArrowGraphBuilderEngines`] bundle (the standard engines, for
/// [`ArrowGraphFactory::new`]), and its compilers are an
/// [`ArrowGraphCompiler`] around such a builder.
///
/// The factory gives the same engines to every builder that it makes, as a
/// builder clone does. The engines are [`Send`] + [`Sync`], and they must hold
/// no state or be safe for use by many threads. All the standard engines are. An
/// engine that holds state needs a factory of your own, one that makes new
/// engines each time: implement [`IArrowGraphFactory`].
///
/// A generator of IDs is different: it holds the count of one graph, and so
/// builders cannot share one. The factory gives each builder two new
/// [`PreviousIdGenerator`] objects, and it does not use the `edge_id_generator`
/// and the `node_id_generator` of the bundle. The factory keeps the engines of
/// the bundle, and the bundle is moved into it, so nothing can change them
/// afterwards.
#[derive(Clone)]
pub struct ArrowGraphFactory<K: Key, R: Key, W: Key> {
    dummy_activity_generator: Arc<dyn IActivityGenerator<K, R, W>>,
    event_generator: Arc<dyn IEventGenerator<K>>,
    scc_finder: Arc<dyn IArrowStronglyConnectedComponentsFinder<K, R, W>>,
    critical_path_engine: Arc<dyn IArrowCriticalPathEngine<K, R, W>>,
    transitive_reducer: Arc<dyn IArrowTransitiveReducer<K, R, W>>,
    dummy_edge_orchestrator: Arc<dyn IDummyEdgeOrchestrator<K, R, W>>,
    resource_scheduling_engine: Arc<dyn IResourceSchedulingEngine<K, R, W>>,
}

impl<K: Key, R: Key, W: Key> Default for ArrowGraphFactory<K, R, W> {
    fn default() -> Self {
        Self::with_engines(ArrowGraphBuilderEngines::default())
    }
}

impl<K: Key, R: Key, W: Key> ArrowGraphFactory<K, R, W> {
    /// Creates a factory with the standard engines.
    pub fn new() -> Self {
        Self::default()
    }

    /// Creates a factory whose builders have the engines of the bundle. The
    /// `edge_id_generator` and the `node_id_generator` of the bundle are
    /// dropped: each builder has ID generators of its own.
    pub fn with_engines(engines: ArrowGraphBuilderEngines<K, R, W>) -> Self {
        Self {
            dummy_activity_generator: engines.dummy_activity_generator,
            event_generator: engines.event_generator,
            scc_finder: engines.scc_finder,
            critical_path_engine: engines.critical_path_engine,
            transitive_reducer: engines.transitive_reducer,
            dummy_edge_orchestrator: engines.dummy_edge_orchestrator,
            resource_scheduling_engine: engines.resource_scheduling_engine,
        }
    }
}

impl<K: Key, R: Key, W: Key> IArrowGraphFactory<K, R, W> for ArrowGraphFactory<K, R, W> {
    fn create_builder(&self) -> ArrowGraphBuilder<K, R, W> {
        ArrowGraphBuilder::with_engines(ArrowGraphBuilderEngines {
            edge_id_generator: Box::new(PreviousIdGenerator::default()),
            node_id_generator: Box::new(PreviousIdGenerator::default()),
            dummy_activity_generator: Arc::clone(&self.dummy_activity_generator),
            event_generator: Arc::clone(&self.event_generator),
            scc_finder: Arc::clone(&self.scc_finder),
            critical_path_engine: Arc::clone(&self.critical_path_engine),
            transitive_reducer: Arc::clone(&self.transitive_reducer),
            dummy_edge_orchestrator: Arc::clone(&self.dummy_edge_orchestrator),
            resource_scheduling_engine: Arc::clone(&self.resource_scheduling_engine),
        })
    }

    fn create_compiler(&self) -> ArrowGraphCompiler<K, R, W> {
        ArrowGraphCompiler::with_builder(self.create_builder())
    }
}
