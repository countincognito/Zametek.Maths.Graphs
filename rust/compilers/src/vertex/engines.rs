//! Default engines for Activity-on-Vertex graphs, and the engines bundle passed
//! to the builder - the counterparts of the C# default engine classes and
//! `VertexGraphBuilderEngines`.

use super::builder::VertexGraphBuilder;
use super::compiler::VertexGraphCompiler;
use super::incremental::IncrementalCriticalPath;
use super::state::{VertexGraphState, VertexTraversal};
use super::{cpm, reducer};
use crate::contracts::{
    IEventGenerator, IIdGenerator, IResourceSchedulingEngine, IVertexCriticalPathEngine,
    IVertexGraphFactory, IVertexStronglyConnectedComponentsFinder, IVertexTransitiveReducer,
};
use crate::generators::RemovableEventGenerator;
use crate::id_gen::PreviousIdGenerator;
use crate::scheduling::PriorityListResourceScheduler;
use crate::tarjan;
use indexmap::{IndexMap, IndexSet};
use std::sync::Arc;
use zametek_maths_graphs_primitives::{CircularDependency, InvalidConstraint, Key};

/// Default Tarjan SCC finder for Activity-on-Vertex graphs (node-space
/// traversal) - the counterpart of the C#
/// `VertexTarjanStronglyConnectedComponentsFinder<…>`.
#[derive(Debug, Clone, Copy, Default)]
pub struct VertexTarjanStronglyConnectedComponentsFinder;

impl<K: Key, R: Key, W: Key> IVertexStronglyConnectedComponentsFinder<K, R, W>
    for VertexTarjanStronglyConnectedComponentsFinder
{
    fn find_strongly_connected_components(
        &self,
        state: &VertexGraphState<K, R, W>,
        ignore_dummies: bool,
    ) -> Vec<CircularDependency<K>> {
        tarjan::find_strongly_connected_components(&VertexTraversal { state }, ignore_dummies)
    }

    fn find_strongly_circular_dependencies(
        &self,
        state: &VertexGraphState<K, R, W>,
        ignore_dummies: bool,
    ) -> Vec<CircularDependency<K>> {
        tarjan::find_strongly_circular_dependencies(&VertexTraversal { state }, ignore_dummies)
    }
}

/// Default critical-path engine for Activity-on-Vertex graphs - the counterpart
/// of the C# `VertexCriticalPathEngine<…>`.
#[derive(Debug, Clone, Copy, Default)]
pub struct VertexCriticalPathEngine;

impl<K: Key, R: Key, W: Key> IVertexCriticalPathEngine<K, R, W> for VertexCriticalPathEngine {
    fn calculate_critical_path_forward_flow(
        &self,
        state: &mut VertexGraphState<K, R, W>,
        invalid_constraints: &[InvalidConstraint<K>],
        shuffle: bool,
    ) -> Result<bool, zametek_maths_graphs_primitives::GraphError> {
        cpm::calculate_critical_path_forward_flow(state, invalid_constraints, shuffle)
    }

    fn calculate_critical_path_backward_flow(
        &self,
        state: &mut VertexGraphState<K, R, W>,
        invalid_constraints: &[InvalidConstraint<K>],
        shuffle: bool,
    ) -> Result<bool, zametek_maths_graphs_primitives::GraphError> {
        cpm::calculate_critical_path_backward_flow(state, invalid_constraints, shuffle)
    }

    fn back_fill_isolated_nodes(
        &self,
        state: &mut VertexGraphState<K, R, W>,
        invalid_constraints: &[InvalidConstraint<K>],
    ) -> bool {
        cpm::back_fill_isolated_nodes(state, invalid_constraints)
    }

    fn begin_incremental_critical_path(
        &self,
        state: &VertexGraphState<K, R, W>,
    ) -> Option<IncrementalCriticalPath<K>> {
        IncrementalCriticalPath::begin(state)
    }
}

/// Default transitive reducer for Activity-on-Vertex graphs - the counterpart
/// of the C# `VertexTransitiveReducer<…>`.
#[derive(Debug, Clone, Copy, Default)]
pub struct VertexTransitiveReducer;

impl<K: Key, R: Key, W: Key> IVertexTransitiveReducer<K, R, W> for VertexTransitiveReducer {
    fn get_ancestor_nodes_lookup(
        &self,
        state: &VertexGraphState<K, R, W>,
        scc_finder: &dyn IVertexStronglyConnectedComponentsFinder<K, R, W>,
    ) -> Option<IndexMap<K, IndexSet<K>>> {
        reducer::get_ancestor_nodes_lookup(state, scc_finder)
    }

    fn reduce_graph(
        &self,
        state: &mut VertexGraphState<K, R, W>,
        scc_finder: &dyn IVertexStronglyConnectedComponentsFinder<K, R, W>,
    ) -> bool {
        reducer::reduce_graph(state, scc_finder)
    }
}

/// A bundle of the engines a [`VertexGraphBuilder`](super::VertexGraphBuilder)
/// relies on, each defaulting to the standard implementation - the counterpart
/// of the C# `VertexGraphBuilderEngines`. Set only the fields you want to
/// customise; the rest keep their defaults.
///
/// Injected engines are shared (`Arc`) and preserved across a builder clone;
/// the ID generator carries a per-graph counter, so it is held by value and
/// recreated on clone.
pub struct VertexGraphBuilderEngines<K: Key, R: Key, W: Key> {
    pub edge_id_generator: Box<dyn IIdGenerator<K>>,
    pub event_generator: Arc<dyn IEventGenerator<K>>,
    pub scc_finder: Arc<dyn IVertexStronglyConnectedComponentsFinder<K, R, W>>,
    pub critical_path_engine: Arc<dyn IVertexCriticalPathEngine<K, R, W>>,
    pub transitive_reducer: Arc<dyn IVertexTransitiveReducer<K, R, W>>,
    pub resource_scheduling_engine: Arc<dyn IResourceSchedulingEngine<K, R, W>>,
}

impl<K: Key, R: Key, W: Key> Default for VertexGraphBuilderEngines<K, R, W> {
    fn default() -> Self {
        Self {
            edge_id_generator: Box::new(PreviousIdGenerator::default()),
            event_generator: Arc::new(RemovableEventGenerator::new()),
            scc_finder: Arc::new(VertexTarjanStronglyConnectedComponentsFinder),
            critical_path_engine: Arc::new(VertexCriticalPathEngine),
            transitive_reducer: Arc::new(VertexTransitiveReducer),
            resource_scheduling_engine: Arc::new(PriorityListResourceScheduler),
        }
    }
}

/// The standard factory of Activity-on-Vertex graphs - the counterpart of the C#
/// `VertexGraphFactory`. Its builders have the engines of a
/// [`VertexGraphBuilderEngines`] bundle (the standard engines, for
/// [`VertexGraphFactory::new`]), and its compilers are a
/// [`VertexGraphCompiler`] around such a builder.
///
/// The factory gives the same engines to every builder that it makes, as a
/// builder clone does. The engines are [`Send`] + [`Sync`], and they must hold
/// no state or be safe for use by many threads. All the standard engines are. An
/// engine that holds state needs a factory of your own, one that makes new
/// engines each time: implement [`IVertexGraphFactory`].
///
/// A generator of IDs is different: it holds the count of one graph, and so
/// builders cannot share one. The factory gives each builder a new
/// [`PreviousIdGenerator`], and it does not use the `edge_id_generator` of the
/// bundle. The factory keeps the engines of the bundle, and the bundle is moved
/// into it, so nothing can change them afterwards.
#[derive(Clone)]
pub struct VertexGraphFactory<K: Key, R: Key, W: Key> {
    event_generator: Arc<dyn IEventGenerator<K>>,
    scc_finder: Arc<dyn IVertexStronglyConnectedComponentsFinder<K, R, W>>,
    critical_path_engine: Arc<dyn IVertexCriticalPathEngine<K, R, W>>,
    transitive_reducer: Arc<dyn IVertexTransitiveReducer<K, R, W>>,
    resource_scheduling_engine: Arc<dyn IResourceSchedulingEngine<K, R, W>>,
}

impl<K: Key, R: Key, W: Key> Default for VertexGraphFactory<K, R, W> {
    fn default() -> Self {
        Self::with_engines(VertexGraphBuilderEngines::default())
    }
}

impl<K: Key, R: Key, W: Key> VertexGraphFactory<K, R, W> {
    /// Creates a factory with the standard engines.
    pub fn new() -> Self {
        Self::default()
    }

    /// Creates a factory whose builders have the engines of the bundle. The
    /// `edge_id_generator` of the bundle is dropped: each builder has an ID
    /// generator of its own.
    pub fn with_engines(engines: VertexGraphBuilderEngines<K, R, W>) -> Self {
        Self {
            event_generator: engines.event_generator,
            scc_finder: engines.scc_finder,
            critical_path_engine: engines.critical_path_engine,
            transitive_reducer: engines.transitive_reducer,
            resource_scheduling_engine: engines.resource_scheduling_engine,
        }
    }
}

impl<K: Key, R: Key, W: Key> IVertexGraphFactory<K, R, W> for VertexGraphFactory<K, R, W> {
    fn create_builder(&self) -> VertexGraphBuilder<K, R, W> {
        VertexGraphBuilder::with_engines(VertexGraphBuilderEngines {
            edge_id_generator: Box::new(PreviousIdGenerator::default()),
            event_generator: Arc::clone(&self.event_generator),
            scc_finder: Arc::clone(&self.scc_finder),
            critical_path_engine: Arc::clone(&self.critical_path_engine),
            transitive_reducer: Arc::clone(&self.transitive_reducer),
            resource_scheduling_engine: Arc::clone(&self.resource_scheduling_engine),
        })
    }

    fn create_compiler(&self) -> VertexGraphCompiler<K, R, W> {
        VertexGraphCompiler::with_builder(self.create_builder())
    }
}
