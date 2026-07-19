use super::engines::ArrowGraphBuilderEngines;
use super::orchestrator;
use super::state::ArrowGraphState;
use crate::constraint_checker;
use crate::contracts::{
    IActivityGenerator, IArrowCriticalPathEngine, IArrowStronglyConnectedComponentsFinder,
    IArrowTransitiveReducer, IDummyEdgeOrchestrator, IEventGenerator, IIdGenerator,
    IResourceSchedulingEngine, IResourceSchedulingGraph,
};
use crate::id_gen::PreviousIdGenerator;
use crate::messages;
use indexmap::{IndexMap, IndexSet};
use std::sync::Arc;
use zametek_maths_graphs_primitives::{
    CircularDependency, DependentActivity, Edge, Event, Graph, GraphError, InvalidConstraint, Key,
    LogicalOperator, Node, NodeType, Resource, ResourceSchedule, UnavailableResources,
};

/// Builds and maintains an Activity-on-Arrow graph (activities on edges,
/// events on nodes): dynamic dependency resolution via dummy edges, transitive
/// reduction and critical-path calculation — the counterpart of the C#
/// `ArrowGraphBuilder`. Prefer driving it through
/// [`super::ArrowGraphCompiler`].
///
/// Dummy activities created by this builder are removable zero-duration
/// activities; events are read-only (the C# defaults). The C# original also
/// declares `AddActivityDependencies`/`RemoveActivity`/
/// `RemoveActivityDependencies`, all of which throw `NotImplementedException`;
/// they are omitted here.
pub struct ArrowGraphBuilder<K: Key, R: Key, W: Key> {
    pub(crate) state: ArrowGraphState<K, R, W>,
    edge_id_generator: Box<dyn IIdGenerator<K>>,
    node_id_generator: Box<dyn IIdGenerator<K>>,
    dummy_activity_generator: Arc<dyn IActivityGenerator<K, R, W>>,
    event_generator: Arc<dyn IEventGenerator<K>>,
    scc_finder: Arc<dyn IArrowStronglyConnectedComponentsFinder<K, R, W>>,
    critical_path_engine: Arc<dyn IArrowCriticalPathEngine<K, R, W>>,
    transitive_reducer: Arc<dyn IArrowTransitiveReducer<K, R, W>>,
    dummy_edge_orchestrator: Arc<dyn IDummyEdgeOrchestrator<K, R, W>>,
    resource_scheduling_engine: Arc<dyn IResourceSchedulingEngine<K, R, W>>,
    /// When true, the critical-path passes process remaining elements in a
    /// random order on each iteration (results are identical either way; used
    /// to prove order-independence).
    pub shuffle_processing_order: bool,
}

impl<K: Key, R: Key, W: Key> ArrowGraphBuilder<K, R, W> {
    // Builds the struct from a bundle, without initialising the Start/End nodes
    // or assimilating a graph — the shared core of `with_engines` and
    // `from_graph_with_engines`.
    fn from_engines_raw(engines: ArrowGraphBuilderEngines<K, R, W>) -> Self {
        Self {
            state: ArrowGraphState::new(),
            edge_id_generator: engines.edge_id_generator,
            node_id_generator: engines.node_id_generator,
            dummy_activity_generator: engines.dummy_activity_generator,
            event_generator: engines.event_generator,
            scc_finder: engines.scc_finder,
            critical_path_engine: engines.critical_path_engine,
            transitive_reducer: engines.transitive_reducer,
            dummy_edge_orchestrator: engines.dummy_edge_orchestrator,
            resource_scheduling_engine: engines.resource_scheduling_engine,
            shuffle_processing_order: false,
        }
    }

    /// Creates a builder with default engines, using the given edge (activity)
    /// and node (event) ID generators.
    pub fn new(
        edge_id_generator: impl IIdGenerator<K> + 'static,
        node_id_generator: impl IIdGenerator<K> + 'static,
    ) -> Self {
        Self::with_engines(ArrowGraphBuilderEngines {
            edge_id_generator: Box::new(edge_id_generator),
            node_id_generator: Box::new(node_id_generator),
            ..Default::default()
        })
    }

    /// Creates a builder from an engines bundle; every unset bundle field
    /// defaults to the standard implementation. The Start and End nodes are
    /// created immediately.
    pub fn with_engines(engines: ArrowGraphBuilderEngines<K, R, W>) -> Self {
        let mut builder = Self::from_engines_raw(engines);
        builder.initialize();
        builder
    }

    /// Creates a builder by assimilating an existing graph, with default engines.
    pub fn from_graph(
        graph: crate::ArrowGraph<K, R, W>,
        edge_id_generator: impl IIdGenerator<K> + 'static,
        node_id_generator: impl IIdGenerator<K> + 'static,
    ) -> Result<Self, GraphError> {
        Self::from_graph_with_engines(
            graph,
            ArrowGraphBuilderEngines {
                edge_id_generator: Box::new(edge_id_generator),
                node_id_generator: Box::new(node_id_generator),
                ..Default::default()
            },
        )
    }

    /// Creates a builder by assimilating an existing graph, from an engines bundle.
    pub fn from_graph_with_engines(
        graph: crate::ArrowGraph<K, R, W>,
        engines: ArrowGraphBuilderEngines<K, R, W>,
    ) -> Result<Self, GraphError> {
        let mut builder = Self::from_engines_raw(engines);

        for edge in graph.edges {
            builder.state.add_edge(edge);
        }

        for node in graph.nodes {
            if !matches!(node.node_type(), NodeType::Start | NodeType::Isolated) {
                for edge_id in &node.incoming {
                    builder.state.set_edge_head_node(*edge_id, node.id());
                }
            }
            if !matches!(node.node_type(), NodeType::End | NodeType::Isolated) {
                for edge_id in &node.outgoing {
                    builder.state.set_edge_tail_node(*edge_id, node.id());
                }
            }
            builder.state.add_node(node);
        }

        // Check all edges are used.
        if !builder
            .state
            .edge_keys_match(builder.state.edge_head.keys().copied().collect::<Vec<_>>())
        {
            return Err(GraphError::new(
                messages::MSG_LIST_OF_EDGE_IDS_AND_HEAD_NODES_DO_NOT_MATCH,
            ));
        }
        if !builder
            .state
            .edge_keys_match(builder.state.edge_tail.keys().copied().collect::<Vec<_>>())
        {
            return Err(GraphError::new(
                messages::MSG_LIST_OF_EDGE_IDS_AND_TAIL_NODES_DO_NOT_MATCH,
            ));
        }

        // Check all nodes are used.
        let mut edge_node_lookup_ids: IndexSet<K> = IndexSet::new();
        edge_node_lookup_ids.extend(builder.state.edge_head.values().copied());
        edge_node_lookup_ids.extend(builder.state.edge_tail.values().copied());
        let mut edge_node_ids: Vec<K> = edge_node_lookup_ids.into_iter().collect();
        edge_node_ids.sort();
        let mut non_isolated_node_ids: Vec<K> = builder
            .state
            .nodes
            .values()
            .filter(|n| n.node_type() != NodeType::Isolated)
            .map(|n| n.id())
            .collect();
        non_isolated_node_ids.sort();
        if non_isolated_node_ids != edge_node_ids {
            return Err(GraphError::new(
                messages::MSG_LIST_OF_NODE_IDS_AND_TAIL_NODES_DO_NOT_MATCH,
            ));
        }

        // Check Start and End nodes.
        let start_nodes = builder.state.nodes_of_type(NodeType::Start);
        if start_nodes.len() == 1 {
            builder.state.start_node_id = Some(start_nodes[0]);
        } else {
            return Err(GraphError::new(
                messages::MSG_ARROW_GRAPH_CONTAINS_MORE_THAN_ONE_START_NODE,
            ));
        }
        let end_nodes = builder.state.nodes_of_type(NodeType::End);
        if end_nodes.len() == 1 {
            builder.state.end_node_id = Some(end_nodes[0]);
        } else {
            return Err(GraphError::new(
                messages::MSG_ARROW_GRAPH_CONTAINS_MORE_THAN_ONE_END_NODE,
            ));
        }

        Ok(builder)
    }

    fn initialize(&mut self) {
        let start_event_id = self.node_id_generator.generate();
        // Arrow-graph events represent real milestones, so the default generator
        // makes them read-only (not removable); the start event begins at time zero.
        let start_event =
            self.event_generator
                .generate_with_times(start_event_id, Some(0), Some(0));
        let start_node = Node::with_type(NodeType::Start, start_event);
        self.state.start_node_id = Some(start_node.id());
        self.state.add_node(start_node);
        let end_event_id = self.node_id_generator.generate();
        let end_event = self.event_generator.generate(end_event_id);
        let end_node = Node::with_type(NodeType::End, end_event);
        self.state.end_node_id = Some(end_node.id());
        self.state.add_node(end_node);
    }

    // -- Properties -----------------------------------------------------------

    /// The single start node of the arrow graph.
    pub fn start_node(&self) -> &Node<K, Event<K>> {
        let id = self.state.start_node_id.expect("start node must exist");
        self.state.node(id).expect("start node must exist")
    }

    /// The single end node of the arrow graph.
    pub fn end_node(&self) -> &Node<K, Event<K>> {
        let id = self.state.end_node_id.expect("end node must exist");
        self.state.node(id).expect("end node must exist")
    }

    pub fn start_nodes(&self) -> Vec<&Node<K, Event<K>>> {
        self.nodes_by_type(NodeType::Start)
    }

    pub fn end_nodes(&self) -> Vec<&Node<K, Event<K>>> {
        self.nodes_by_type(NodeType::End)
    }

    pub fn normal_nodes(&self) -> Vec<&Node<K, Event<K>>> {
        self.nodes_by_type(NodeType::Normal)
    }

    pub fn isolated_nodes(&self) -> Vec<&Node<K, Event<K>>> {
        self.nodes_by_type(NodeType::Isolated)
    }

    fn nodes_by_type(&self, node_type: NodeType) -> Vec<&Node<K, Event<K>>> {
        self.state
            .nodes
            .values()
            .filter(|n| n.node_type() == node_type)
            .collect()
    }

    pub fn edge_ids(&self) -> Vec<K> {
        self.state.edge_ids()
    }

    pub fn node_ids(&self) -> Vec<K> {
        self.state.node_ids()
    }

    /// The activities carried on the edges (including dummy activities).
    pub fn activities(&self) -> impl Iterator<Item = &DependentActivity<K, R, W>> {
        self.state.edges.values().map(|e| &e.content)
    }

    /// The events carried on the nodes.
    pub fn events(&self) -> impl Iterator<Item = &Event<K>> {
        self.state.nodes.values().map(|n| &n.content)
    }

    pub fn activity_ids(&self) -> Vec<K> {
        self.activities().map(|a| a.id()).collect()
    }

    pub fn event_ids(&self) -> Vec<K> {
        self.events().map(|e| e.id()).collect()
    }

    pub fn edges(&self) -> impl Iterator<Item = &Edge<K, DependentActivity<K, R, W>>> {
        self.state.edges.values()
    }

    pub fn nodes(&self) -> impl Iterator<Item = &Node<K, Event<K>>> {
        self.state.nodes.values()
    }

    /// The IDs of dependencies that are referenced but not yet present in the graph.
    pub fn invalid_dependencies(&self) -> Vec<K> {
        self.state.invalid_dependencies()
    }

    /// Whether every referenced dependency is present in the graph.
    pub fn all_dependencies_satisfied(&self) -> bool {
        self.state.all_dependencies_satisfied()
    }

    /// The earliest start time across all activities.
    pub fn start_time(&self) -> i32 {
        self.activities()
            .map(|a| a.earliest_start_time.unwrap_or(0))
            .min()
            .unwrap_or(0)
    }

    /// The latest finish time across all activities.
    pub fn finish_time(&self) -> i32 {
        self.activities()
            .map(|a| a.latest_finish_time.unwrap_or(0))
            .max()
            .unwrap_or(0)
    }

    // -- Lookups --------------------------------------------------------------

    /// Resolves the activity with the given ID.
    pub fn activity(&self, key: K) -> Option<&DependentActivity<K, R, W>> {
        self.state.edge(key).map(|e| &e.content)
    }

    /// Resolves the activity with the given ID, mutably.
    pub fn activity_mut(&mut self, key: K) -> Option<&mut DependentActivity<K, R, W>> {
        self.state.edge_mut(key).map(|e| &mut e.content)
    }

    /// Resolves the event with the given ID.
    pub fn event(&self, key: K) -> Option<&Event<K>> {
        self.state.node(key).map(|n| &n.content)
    }

    /// Resolves the edge with the given ID.
    pub fn edge(&self, key: K) -> Option<&Edge<K, DependentActivity<K, R, W>>> {
        self.state.edge(key)
    }

    /// Resolves the node with the given ID.
    pub fn node(&self, key: K) -> Option<&Node<K, Event<K>>> {
        self.state.node(key)
    }

    /// Resolves the node the given edge points to.
    pub fn edge_head_node(&self, key: K) -> Option<&Node<K, Event<K>>> {
        self.state
            .edge_head_node_id(key)
            .and_then(|id| self.state.node(id))
    }

    /// Resolves the node the given edge starts from.
    pub fn edge_tail_node(&self, key: K) -> Option<&Node<K, Event<K>>> {
        self.state
            .edge_tail_node_id(key)
            .and_then(|id| self.state.node(id))
    }

    // -- Mutations --------------------------------------------------------------

    /// Adds an activity with no dependencies. Returns false if the ID already exists.
    pub fn add_activity(&mut self, activity: DependentActivity<K, R, W>) -> bool {
        self.add_activity_with_dependencies(activity, IndexSet::new())
    }

    /// Adds an activity and wires up the given dependencies. Returns false if
    /// the ID already exists.
    pub fn add_activity_with_dependencies(
        &mut self,
        activity: DependentActivity<K, R, W>,
        dependencies: IndexSet<K>,
    ) -> bool {
        let activity_id = activity.id();
        if self.state.contains_edge(activity_id) {
            return false;
        }
        if dependencies.contains(&activity_id) {
            return false;
        }

        // Create a new edge for the activity.
        let edge = Edge::new(activity);
        self.state.add_edge(edge);

        // We expect dependencies at some point.
        if !dependencies.is_empty() {
            // Since we use dummy edges to connect all tail nodes, we can create
            // a new tail node for this edge.
            let tail_event_id = self.node_id_generator.generate();
            let tail_event = self.event_generator.generate(tail_event_id);
            let mut tail_node = Node::new(tail_event);
            tail_node.outgoing.insert(activity_id);
            let tail_node_id = tail_node.id();
            self.state.set_edge_tail_node(activity_id, tail_node_id);
            self.state.add_node(tail_node);

            // Check which of the expected dependencies currently exist.
            let existing_dependencies: Vec<K> = dependencies
                .iter()
                .filter(|id| self.state.contains_edge(**id))
                .copied()
                .collect();
            let non_existing_dependencies: Vec<K> = dependencies
                .iter()
                .filter(|id| !self.state.contains_edge(**id))
                .copied()
                .collect();

            // If any expected dependencies currently exist, then hook up their
            // head node to this edge's tail node with dummy edges.
            for dependency_id in existing_dependencies {
                let dependency_head_node_id = self
                    .state
                    .edge_head_node_id(dependency_id)
                    .expect("dependency head must exist");

                let dummy_edge = orchestrator::generate_dummy_activity(
                    &mut *self.edge_id_generator,
                    &*self.dummy_activity_generator,
                );
                let dummy_edge_id = dummy_edge.id();
                self.state
                    .node_mut(tail_node_id)
                    .expect("tail node must exist")
                    .incoming
                    .insert(dummy_edge_id);
                self.state.set_edge_head_node(dummy_edge_id, tail_node_id);

                // If the head node of the dependency is the End node, then convert it.
                let dependency_head_node = self
                    .state
                    .node_mut(dependency_head_node_id)
                    .expect("dependency head must exist");
                if dependency_head_node.node_type() == NodeType::End {
                    dependency_head_node.set_node_type(NodeType::Normal);
                }
                dependency_head_node.outgoing.insert(dummy_edge_id);
                self.state
                    .set_edge_tail_node(dummy_edge_id, dependency_head_node_id);
                self.state.add_edge(dummy_edge);
            }

            // If any expected dependencies currently do not exist, then record
            // their IDs and add this edge's tail node as an unsatisfied successor.
            for dependency_id in non_existing_dependencies {
                self.state
                    .add_unsatisfied_successor(dependency_id, tail_node_id);
            }
        } else {
            // No dependencies, so attach it directly to the start node.
            let start_node_id = self.state.start_node_id.expect("start node must exist");
            self.state
                .node_mut(start_node_id)
                .expect("start node must exist")
                .outgoing
                .insert(activity_id);
            self.state.set_edge_tail_node(activity_id, start_node_id);
        }
        self.resolve_unsatisfied_successor_activities(activity_id);
        true
    }

    fn resolve_unsatisfied_successor_activities(&mut self, activity_id: K) {
        // Check to make sure the edge really exists.
        if !self.state.contains_edge(activity_id) {
            return;
        }

        let head_event_id = self.node_id_generator.generate();
        let head_event = self.event_generator.generate(head_event_id);
        let mut head_node = Node::new(head_event);
        head_node.incoming.insert(activity_id);
        let head_node_id = head_node.id();
        self.state.set_edge_head_node(activity_id, head_node_id);
        self.state.add_node(head_node);

        // Check to see if any existing activities were expecting this activity
        // as a dependency. If so, then hook up their tail nodes to this
        // activity's head node with a dummy edge.
        if let Some(tail_node_ids) = self.state.unsatisfied_successors.get(&activity_id) {
            let tail_node_ids: Vec<K> = tail_node_ids.iter().copied().collect();
            let orchestrator = Arc::clone(&self.dummy_edge_orchestrator);
            for tail_node_id in tail_node_ids {
                orchestrator.connect_with_dummy_edge(
                    &mut self.state,
                    &mut *self.edge_id_generator,
                    &*self.dummy_activity_generator,
                    head_node_id,
                    tail_node_id,
                );
            }
            self.state.remove_unsatisfied_successors(activity_id);
        } else {
            // No existing activities were expecting this activity as a
            // dependency, so attach it directly to the end node via a dummy.
            let end_node_id = self.state.end_node_id.expect("end node must exist");
            let orchestrator = Arc::clone(&self.dummy_edge_orchestrator);
            orchestrator.connect_with_dummy_edge(
                &mut self.state,
                &mut *self.edge_id_generator,
                &*self.dummy_activity_generator,
                head_node_id,
                end_node_id,
            );
        }
    }

    /// Removes a dummy activity edge, merging adjacent nodes where possible.
    pub fn remove_dummy_activity(&mut self, activity_id: K) -> Result<bool, GraphError> {
        let orchestrator = Arc::clone(&self.dummy_edge_orchestrator);
        orchestrator.remove_dummy_activity(&mut self.state, activity_id)
    }

    /// Returns the IDs of the activities the given activity currently depends
    /// on within the graph.
    pub fn activity_dependency_ids(&self, activity_id: K) -> Vec<K> {
        let Some(tail_node_id) = self.state.edge_tail_node_id(activity_id) else {
            return Vec::new();
        };
        let tail_node = self.state.node(tail_node_id).expect("tail node must exist");
        if matches!(tail_node.node_type(), NodeType::Start | NodeType::Isolated) {
            return Vec::new();
        }
        tail_node.incoming.iter().copied().collect()
    }

    /// Returns the strong (resolved) dependency IDs for the given activity ID:
    /// dummy edges are transparent, real edges terminate the walk.
    pub fn strong_activity_dependency_ids(&self, activity_id: K) -> Vec<K> {
        let Some(tail_node_id) = self.state.edge_tail_node_id(activity_id) else {
            return Vec::new();
        };
        let tail_node = self.state.node(tail_node_id).expect("tail node must exist");
        if matches!(tail_node.node_type(), NodeType::Start | NodeType::Isolated) {
            return Vec::new();
        }
        // Iterative walk so a long dummy chain cannot overflow the stack. Each
        // node's incoming edges are walked once; callers use the result as a set.
        let mut output: Vec<K> = Vec::new();
        let mut visited_nodes: IndexSet<K> = IndexSet::new();
        let mut stack: Vec<K> = vec![tail_node_id];
        while let Some(current_node_id) = stack.pop() {
            if !visited_nodes.insert(current_node_id) {
                continue;
            }
            let current_node = self.state.node(current_node_id).expect("node must exist");
            if matches!(
                current_node.node_type(),
                NodeType::Start | NodeType::Isolated
            ) {
                continue;
            }
            for incoming_edge_id in &current_node.incoming {
                let incoming_edge = self.state.edge(*incoming_edge_id).expect("edge must exist");
                if incoming_edge.content.is_dummy() {
                    stack.push(
                        self.state
                            .edge_tail_node_id(*incoming_edge_id)
                            .expect("edge tail must exist"),
                    );
                } else {
                    output.push(*incoming_edge_id);
                }
            }
        }
        output
    }

    /// Finds the strongly-connected circular dependencies in the graph.
    pub fn find_strong_circular_dependencies(&self) -> Vec<CircularDependency<K>> {
        self.scc_finder
            .find_strongly_circular_dependencies(&self.state, true)
    }

    /// Finds activity constraints that are self-contradictory before compilation.
    pub fn find_invalid_pre_compilation_constraints(&self) -> Vec<InvalidConstraint<K>> {
        constraint_checker::find_invalid_pre_compilation_constraints(
            self.activities().map(|a| &a.activity),
        )
    }

    /// Finds activity constraints violated by the computed times after compilation.
    pub fn find_invalid_post_compilation_constraints(&self) -> Vec<InvalidConstraint<K>> {
        constraint_checker::find_invalid_post_compilation_constraints(
            self.activities().map(|a| &a.activity),
        )
    }

    /// Builds a lookup from each node ID to the full set of its ancestor node
    /// IDs. Returns `None` if the graph has unsatisfied or circular dependencies.
    pub fn get_ancestor_nodes_lookup(&self) -> Option<IndexMap<K, IndexSet<K>>> {
        self.transitive_reducer
            .get_ancestor_nodes_lookup(&self.state, &*self.scc_finder)
    }

    /// Performs transitive reduction, removing all redundant dummy edges.
    /// Returns `Ok(false)` if it cannot be performed.
    pub fn transitive_reduction(&mut self) -> Result<bool, GraphError> {
        let reducer = Arc::clone(&self.transitive_reducer);
        let scc_finder = Arc::clone(&self.scc_finder);
        let orchestrator = Arc::clone(&self.dummy_edge_orchestrator);
        reducer.reduce_graph(&mut self.state, &*scc_finder, &*orchestrator)
    }

    /// Redirects redundant dummy edges (canonical arrow-graph normalisation).
    pub fn redirect_edges(&mut self) -> Result<bool, GraphError> {
        let orchestrator = Arc::clone(&self.dummy_edge_orchestrator);
        let scc_finder = Arc::clone(&self.scc_finder);
        orchestrator.redirect_dummy_edges(&mut self.state, &*scc_finder)
    }

    /// Removes dummy edges that are transitively implied.
    pub fn remove_redundant_edges(&mut self) -> Result<bool, GraphError> {
        let orchestrator = Arc::clone(&self.dummy_edge_orchestrator);
        let scc_finder = Arc::clone(&self.scc_finder);
        orchestrator.remove_redundant_dummy_edges(&mut self.state, &*scc_finder)
    }

    /// Redirects and then removes redundant dummy edges until the graph is minimal.
    pub fn clean_up_edges(&mut self) -> Result<bool, GraphError> {
        if !self.redirect_edges()? {
            return Ok(false);
        }
        if !self.remove_redundant_edges()? {
            return Ok(false);
        }
        Ok(true)
    }

    fn clear_critical_path_variables(&mut self) {
        for edge in self.state.edges.values_mut() {
            edge.content.free_slack = None;
            edge.content.earliest_start_time = None;
            edge.content.latest_finish_time = None;
        }
        for node in self.state.nodes.values_mut() {
            node.content.earliest_finish_time = None;
            node.content.latest_finish_time = None;
        }
    }

    /// Calculates the critical path across the whole graph.
    pub fn calculate_critical_path(&mut self) -> Result<(), GraphError> {
        if !self.remove_redundant_edges()? {
            return Err(GraphError::new(messages::MSG_CANNOT_REMOVE_REDUNDANT_EDGES));
        }

        self.clear_critical_path_variables();

        let constraints = if self.all_dependencies_satisfied() {
            self.find_invalid_pre_compilation_constraints()
        } else {
            Vec::new()
        };

        let engine = Arc::clone(&self.critical_path_engine);

        if !engine.calculate_event_earliest_finish_times(
            &mut self.state,
            &constraints,
            self.shuffle_processing_order,
        )? {
            return Err(GraphError::new(
                messages::MSG_CANNOT_CALCULATE_EVENT_EARLIEST_FINISH_TIMES,
            ));
        }

        if !engine.calculate_event_latest_finish_times(
            &mut self.state,
            &constraints,
            self.shuffle_processing_order,
        )? {
            return Err(GraphError::new(
                messages::MSG_CANNOT_CALCULATE_EVENT_LATEST_FINISH_TIMES,
            ));
        }

        if !engine.calculate_critical_path_variables(&mut self.state, &constraints)? {
            return Err(GraphError::new(
                messages::MSG_CANNOT_CALCULATE_CRITICAL_PATH,
            ));
        }

        if !self.redirect_edges()? {
            return Err(GraphError::new(
                messages::MSG_CANNOT_PERFORM_EDGE_REDIRECTION,
            ));
        }
        Ok(())
    }

    /// Returns the activity IDs in scheduling priority order (most critical first).
    pub fn calculate_critical_path_priority_list(&mut self) -> Result<Vec<K>, GraphError> {
        let mut tmp_graph_builder = self.clone_builder()?;
        Self::calculate_critical_path_priority_list_on(&mut tmp_graph_builder)
    }

    fn calculate_critical_path_priority_list_on(
        graph_builder: &mut ArrowGraphBuilder<K, R, W>,
    ) -> Result<Vec<K>, GraphError> {
        let mut priority_list: Vec<K> = Vec::new();
        loop {
            graph_builder.calculate_critical_path()?;

            // Get the critical path in order of earliest start time.
            let min_float = graph_builder
                .activities()
                .filter(|x| !x.is_dummy() && x.total_slack().is_some())
                .map(|x| x.total_slack().expect("total slack must exist"))
                .min()
                .unwrap_or(0);

            let mut critical_activities: Vec<(Option<i32>, K)> = graph_builder
                .activities()
                .filter(|x| x.total_slack() == Some(min_float) && !x.is_dummy())
                .map(|x| (x.earliest_start_time, x.id()))
                .collect();
            critical_activities.sort_by_key(|(est, _)| *est);

            if let Some((_, critical_activity_id)) = critical_activities.first().copied() {
                priority_list.push(critical_activity_id);
                // Set the processed activity to dummy.
                graph_builder
                    .activity_mut(critical_activity_id)
                    .expect("activity must exist")
                    .duration = 0;
            } else {
                break;
            }
        }
        if graph_builder.activities().any(|x| !x.is_dummy()) {
            return Err(GraphError::new(
                messages::MSG_CANNOT_CALCULATE_CRITICAL_PATH_PRIORITY_LIST,
            ));
        }
        Ok(priority_list)
    }

    /// Schedules the activities onto the given resources in priority order and
    /// returns the per-resource schedules.
    pub fn calculate_resource_schedules_by_priority_list(
        &mut self,
        resources: &[Resource<R, W>],
    ) -> Result<Vec<ResourceSchedule<K, R, W>>, GraphError> {
        if self.state.edges.is_empty() {
            return Ok(Vec::new());
        }

        let infinite_resources = resources.is_empty();
        let filtered_resources: Vec<Resource<R, W>> = resources
            .iter()
            .filter(|x| !x.is_inactive)
            .cloned()
            .collect();

        if !infinite_resources {
            self.validate_activities_against_resources(&filtered_resources)?;
        }

        let mut tmp_graph_builder = self.clone_builder()?;
        let mut priority_clone = tmp_graph_builder.clone_builder()?;
        let priority_list = Self::calculate_critical_path_priority_list_on(&mut priority_clone)?;

        let engine = Arc::clone(&self.resource_scheduling_engine);
        engine.calculate_resource_schedules(
            &priority_list,
            &filtered_resources,
            infinite_resources,
            &mut tmp_graph_builder,
        )
    }

    fn validate_activities_against_resources(
        &self,
        filtered_resources: &[Resource<R, W>],
    ) -> Result<(), GraphError> {
        let mut unavailable_resources_set: Vec<UnavailableResources<K, R>> = Vec::new();
        for activity in self.activities() {
            if activity.target_resources.is_empty() {
                continue;
            }
            match activity.target_resource_operator {
                LogicalOperator::And => {
                    let unavailable: Vec<R> = activity
                        .target_resources
                        .iter()
                        .filter(|r| !filtered_resources.iter().any(|x| x.id == **r))
                        .copied()
                        .collect();
                    if !unavailable.is_empty() {
                        unavailable_resources_set
                            .push(UnavailableResources::new(activity.id(), unavailable));
                    }
                }
                LogicalOperator::Or | LogicalOperator::ActiveAnd => {
                    let has_intersection = activity
                        .target_resources
                        .iter()
                        .any(|r| filtered_resources.iter().any(|x| x.id == *r));
                    if !has_intersection {
                        unavailable_resources_set.push(UnavailableResources::new(
                            activity.id(),
                            activity.target_resources.iter().copied(),
                        ));
                    }
                }
            }
        }
        if !unavailable_resources_set.is_empty() {
            return Err(GraphError::new(
                messages::MSG_AT_LEAST_ONE_TARGET_RESOURCE_NOT_AVAILABLE,
            ));
        }

        let all_resources_are_explicit_targets =
            filtered_resources.iter().all(|x| x.is_explicit_target);
        let at_least_one_activity_requires_non_explicit_target_resource = self
            .activities()
            .any(|x| !x.is_dummy() && x.target_resources.is_empty());
        if all_resources_are_explicit_targets
            && at_least_one_activity_requires_non_explicit_target_resource
        {
            return Err(GraphError::new(
                messages::MSG_AT_LEAST_ONE_ACTIVITY_REQUIRES_NON_EXPLICIT_TARGET_RESOURCE,
            ));
        }
        Ok(())
    }

    /// Exports the graph structure (cloned edges and nodes). Fails if the graph
    /// cannot be cleaned up.
    pub fn to_graph(&mut self) -> Result<crate::ArrowGraph<K, R, W>, GraphError> {
        if !self.clean_up_edges()? {
            // A graph that cannot be cleaned up cannot be faithfully exported.
            return Err(GraphError::new(
                messages::MSG_UNABLE_TO_REMOVE_UNNECESSARY_EDGES,
            ));
        }
        Ok(Graph::with_content(
            self.state.edges.values().cloned().collect(),
            self.state.nodes.values().cloned().collect(),
        ))
    }

    /// Clears the graph and returns the builder to its initial state.
    pub fn reset(&mut self) {
        self.state.clear();
        self.initialize();
    }

    /// Clones the builder (via graph export, as in the C# `CloneObject`). Only
    /// the ID generators are recreated, stepping downward from below the
    /// minimum exported IDs; the shuffle flag resets to false.
    pub fn clone_builder(&mut self) -> Result<Self, GraphError> {
        let graph = self.to_graph()?;
        let min_node_id = graph
            .nodes
            .iter()
            .map(|n| n.id())
            .min()
            .unwrap_or_default()
            .previous();
        let min_edge_id = graph
            .edges
            .iter()
            .map(|e| e.id())
            .min()
            .unwrap_or_default()
            .previous();
        // Preserve the injected (stateless) engines on the clone; only the ID
        // generators are recreated, since they carry per-graph counters.
        Self::from_graph_with_engines(
            graph,
            ArrowGraphBuilderEngines {
                edge_id_generator: Box::new(PreviousIdGenerator::new(min_edge_id)),
                node_id_generator: Box::new(PreviousIdGenerator::new(min_node_id)),
                dummy_activity_generator: Arc::clone(&self.dummy_activity_generator),
                event_generator: Arc::clone(&self.event_generator),
                scc_finder: Arc::clone(&self.scc_finder),
                critical_path_engine: Arc::clone(&self.critical_path_engine),
                transitive_reducer: Arc::clone(&self.transitive_reducer),
                dummy_edge_orchestrator: Arc::clone(&self.dummy_edge_orchestrator),
                resource_scheduling_engine: Arc::clone(&self.resource_scheduling_engine),
            },
        )
    }
}

impl<K: Key, R: Key, W: Key> IResourceSchedulingGraph<K, R, W> for ArrowGraphBuilder<K, R, W> {
    fn activity(&self, id: K) -> &DependentActivity<K, R, W> {
        ArrowGraphBuilder::activity(self, id).expect("activity must exist")
    }

    fn activity_mut(&mut self, id: K) -> &mut DependentActivity<K, R, W> {
        ArrowGraphBuilder::activity_mut(self, id).expect("activity must exist")
    }

    fn strong_activity_dependency_ids(&self, id: K) -> Vec<K> {
        ArrowGraphBuilder::strong_activity_dependency_ids(self, id)
    }

    fn clone_activities(&self) -> Vec<DependentActivity<K, R, W>> {
        self.activities().cloned().collect()
    }
}
