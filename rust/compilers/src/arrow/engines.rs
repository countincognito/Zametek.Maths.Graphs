//! Default engines for Activity-on-Arrow graphs, and the engines bundle passed
//! to the builder - the counterparts of the C# default engine classes and
//! `ArrowGraphBuilderEngines`.

use super::orchestrator::DummyEdgeOrchestrator;
use super::state::{ArrowGraphState, ArrowTraversal};
use super::{cpm, reducer};
use crate::contracts::{
    IActivityGenerator, IArrowCriticalPathEngine, IArrowStronglyConnectedComponentsFinder,
    IArrowTransitiveReducer, IDummyEdgeOrchestrator, IEventGenerator, IIdGenerator,
    IResourceSchedulingEngine,
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
