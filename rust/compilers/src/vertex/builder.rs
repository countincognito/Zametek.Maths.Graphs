use super::state::{VertexState, VertexTraversal};
use super::{cpm, reducer};
use crate::constraint_checker;
use crate::error_formatter;
use crate::id_gen::IdGenerator;
use crate::messages;
use crate::scheduling::{self, ResourceSchedulingGraph};
use indexmap::{IndexMap, IndexSet};
use zametek_maths_graphs_primitives::{
    CircularDependency, DependentActivity, Edge, Event, Graph, GraphCompilationError,
    GraphCompilationErrorCode, GraphError, InvalidConstraint, Key, LogicalOperator, Node, NodeType,
    Resource, ResourceSchedule, ScheduledActivity, UnavailableResources,
};

/// Builds and maintains an Activity-on-Vertex graph (activities on nodes,
/// events on edges): dynamic dependency resolution, transitive reduction,
/// critical-path calculation and resource scheduling — the counterpart of the
/// C# `VertexGraphBuilder`. Prefer driving it through
/// [`super::VertexGraphCompiler`].
///
/// Events created by this builder are removable (the C# default
/// `RemovableEventGenerator`).
pub struct VertexGraphBuilder<K: Key, R: Key, W: Key> {
    pub(crate) state: VertexState<K, R, W>,
    edge_id_generator: IdGenerator<K>,
    /// When true, the critical-path passes process remaining elements in a
    /// random order on each iteration (results are identical either way; used
    /// to prove order-independence).
    pub shuffle_processing_order: bool,
}

impl<K: Key, R: Key, W: Key> VertexGraphBuilder<K, R, W> {
    /// Creates a builder using the given edge (event) ID generator.
    pub fn new(edge_id_generator: IdGenerator<K>) -> Self {
        Self {
            state: VertexState::new(),
            edge_id_generator,
            shuffle_processing_order: false,
        }
    }

    /// Creates a builder by assimilating an existing graph.
    pub fn from_graph(
        graph: crate::VertexGraph<K, R, W>,
        edge_id_generator: IdGenerator<K>,
    ) -> Result<Self, GraphError> {
        let mut builder = Self::new(edge_id_generator);

        for edge in graph.edges {
            builder.state.add_edge(edge);
        }

        for node in graph.nodes {
            // Assimilate incoming edges.
            if !matches!(node.node_type(), NodeType::Start | NodeType::Isolated) {
                for edge_id in &node.incoming {
                    builder.state.set_edge_head_node(*edge_id, node.id());
                }
            }
            // Assimilate outgoing edges.
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

        // Check Start and End nodes when normal nodes are present.
        if !builder.state.nodes_of_type(NodeType::Normal).is_empty() {
            if builder.state.nodes_of_type(NodeType::Start).is_empty() {
                return Err(GraphError::new(
                    messages::MSG_VERTEX_GRAPH_NORMAL_NODES_WITHOUT_START_NODES,
                ));
            }
            if builder.state.nodes_of_type(NodeType::End).is_empty() {
                return Err(GraphError::new(
                    messages::MSG_VERTEX_GRAPH_NORMAL_NODES_WITHOUT_END_NODES,
                ));
            }
        }

        Ok(builder)
    }

    fn generate_event(&mut self) -> Edge<K, Event<K>> {
        let edge_id = self.edge_id_generator.generate();
        let mut event = Event::new(edge_id);
        // Vertex-graph events live on structural edges, so they are removable.
        event.set_as_removable();
        Edge::new(event)
    }

    // -- Properties ---------------------------------------------------------

    pub fn start_nodes(&self) -> Vec<&Node<K, DependentActivity<K, R, W>>> {
        self.nodes_by_type(NodeType::Start)
    }

    pub fn end_nodes(&self) -> Vec<&Node<K, DependentActivity<K, R, W>>> {
        self.nodes_by_type(NodeType::End)
    }

    pub fn normal_nodes(&self) -> Vec<&Node<K, DependentActivity<K, R, W>>> {
        self.nodes_by_type(NodeType::Normal)
    }

    pub fn isolated_nodes(&self) -> Vec<&Node<K, DependentActivity<K, R, W>>> {
        self.nodes_by_type(NodeType::Isolated)
    }

    fn nodes_by_type(&self, node_type: NodeType) -> Vec<&Node<K, DependentActivity<K, R, W>>> {
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

    /// The activities carried on the nodes.
    pub fn activities(&self) -> impl Iterator<Item = &DependentActivity<K, R, W>> {
        self.state.nodes.values().map(|n| &n.content)
    }

    /// The events carried on the edges.
    pub fn events(&self) -> impl Iterator<Item = &Event<K>> {
        self.state.edges.values().map(|e| &e.content)
    }

    pub fn activity_ids(&self) -> Vec<K> {
        self.activities().map(|a| a.id()).collect()
    }

    pub fn event_ids(&self) -> Vec<K> {
        self.events().map(|e| e.id()).collect()
    }

    pub fn edges(&self) -> impl Iterator<Item = &Edge<K, Event<K>>> {
        self.state.edges.values()
    }

    pub fn nodes(&self) -> impl Iterator<Item = &Node<K, DependentActivity<K, R, W>>> {
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

    // -- Lookups -------------------------------------------------------------

    /// Resolves the activity with the given ID.
    pub fn activity(&self, key: K) -> Option<&DependentActivity<K, R, W>> {
        self.state.node(key).map(|n| &n.content)
    }

    /// Resolves the activity with the given ID, mutably.
    pub fn activity_mut(&mut self, key: K) -> Option<&mut DependentActivity<K, R, W>> {
        self.state.node_mut(key).map(|n| &mut n.content)
    }

    /// Resolves the event with the given ID.
    pub fn event(&self, key: K) -> Option<&Event<K>> {
        self.state.edge(key).map(|e| &e.content)
    }

    /// Resolves the edge with the given ID.
    pub fn edge(&self, key: K) -> Option<&Edge<K, Event<K>>> {
        self.state.edge(key)
    }

    /// Resolves the node with the given ID.
    pub fn node(&self, key: K) -> Option<&Node<K, DependentActivity<K, R, W>>> {
        self.state.node(key)
    }

    /// Resolves the node the given edge points to.
    pub fn edge_head_node(&self, key: K) -> Option<&Node<K, DependentActivity<K, R, W>>> {
        self.state
            .edge_head_node_id(key)
            .and_then(|id| self.state.node(id))
    }

    /// Resolves the node the given edge starts from.
    pub fn edge_tail_node(&self, key: K) -> Option<&Node<K, DependentActivity<K, R, W>>> {
        self.state
            .edge_tail_node_id(key)
            .and_then(|id| self.state.node(id))
    }

    // -- Mutations ------------------------------------------------------------

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
        if self.state.contains_node(activity_id) {
            return false;
        }
        if dependencies.contains(&activity_id) {
            return false;
        }
        // Create a new Isolated node for the activity.
        let node = Node::with_type(NodeType::Isolated, activity);
        self.state.add_node(node);

        // We expect dependencies at some point.
        if !dependencies.is_empty() {
            self.state
                .node_mut(activity_id)
                .expect("node was just added")
                .set_node_type(NodeType::End);

            // Check which of the expected dependencies currently exist
            // (iterated in node-insertion order, as in the C# LINQ Intersect).
            let existing_dependencies: Vec<K> = self
                .state
                .node_ids()
                .into_iter()
                .filter(|id| dependencies.contains(id))
                .collect();
            let non_existing_dependencies: Vec<K> = dependencies
                .iter()
                .filter(|id| !existing_dependencies.contains(id))
                .copied()
                .collect();

            // If any expected dependencies currently exist, generate an edge to
            // connect them.
            for dependency_id in existing_dependencies {
                self.wire_dependency_edge(dependency_id, activity_id);
            }

            // If any expected dependencies currently do not exist, then record
            // their IDs and add this node as an unsatisfied successor.
            for dependency_id in non_existing_dependencies {
                self.state
                    .add_unsatisfied_successor(dependency_id, activity_id);
            }
        }
        self.resolve_unsatisfied_successor_activities(activity_id);
        true
    }

    // Creates a new event edge from dependency_id to successor_id, converting
    // node types as necessary.
    fn wire_dependency_edge(&mut self, dependency_id: K, successor_id: K) {
        let edge = self.generate_event();
        let edge_id = edge.id();

        self.state
            .node_mut(successor_id)
            .expect("successor node must exist")
            .incoming
            .insert(edge_id);
        self.state.set_edge_head_node(edge_id, successor_id);

        // If the dependency node is an End or Isolated node, then convert it.
        let dependency_node = self
            .state
            .node_mut(dependency_id)
            .expect("dependency node must exist");
        match dependency_node.node_type() {
            NodeType::End => dependency_node.set_node_type(NodeType::Normal),
            NodeType::Isolated => dependency_node.set_node_type(NodeType::Start),
            _ => {}
        }

        dependency_node.outgoing.insert(edge_id);
        self.state.set_edge_tail_node(edge_id, dependency_id);
        self.state.add_edge(edge);
    }

    /// Adds the given dependencies to an existing activity.
    pub fn add_activity_dependencies(&mut self, activity_id: K, dependencies: IndexSet<K>) -> bool {
        if !self.state.contains_node(activity_id) {
            return false;
        }
        if dependencies.is_empty() {
            return true;
        }
        if dependencies.contains(&activity_id) {
            return false;
        }

        // If the node is a Start or Isolated node, then convert it.
        let node = self.state.node_mut(activity_id).expect("node must exist");
        match node.node_type() {
            NodeType::Start => node.set_node_type(NodeType::Normal),
            NodeType::Isolated => node.set_node_type(NodeType::End),
            _ => {}
        }

        // Check which of the expected dependencies currently exist.
        let existing_dependencies: Vec<K> = self
            .state
            .node_ids()
            .into_iter()
            .filter(|id| dependencies.contains(id))
            .collect();
        let non_existing_dependencies: Vec<K> = dependencies
            .iter()
            .filter(|id| !existing_dependencies.contains(id))
            .copied()
            .collect();

        for dependency_id in existing_dependencies {
            self.wire_dependency_edge(dependency_id, activity_id);
        }

        for dependency_id in non_existing_dependencies {
            self.state
                .add_unsatisfied_successor(dependency_id, activity_id);
        }
        true
    }

    /// Removes the activity with the given ID. Returns false if it cannot be removed.
    pub fn remove_activity(&mut self, activity_id: K) -> bool {
        // Retrieve the activity's node.
        let Some(node) = self.state.node(activity_id) else {
            return false;
        };
        if !node.content.can_be_removed() {
            return false;
        }

        let node_type = node.node_type();
        let incoming: Vec<K> = node.incoming.iter().copied().collect();
        let outgoing: Vec<K> = node.outgoing.iter().copied().collect();

        // If the activity was an unsatisfied successor, then remove it from the lookup.
        if matches!(node_type, NodeType::End | NodeType::Normal) {
            self.state
                .remove_activity_from_all_unsatisfied_successors(activity_id);
        }
        self.state.remove_node(activity_id);

        if node_type == NodeType::Isolated {
            return true;
        }

        if matches!(node_type, NodeType::End | NodeType::Normal) {
            // Remove the incoming edges.
            for edge_id in incoming {
                let tail_node_id = self
                    .state
                    .edge_tail_node_id(edge_id)
                    .expect("edge tail must exist");

                // Remove the edge from the tail node.
                let tail_node = self
                    .state
                    .node_mut(tail_node_id)
                    .expect("tail node must exist");
                tail_node.outgoing.shift_remove(&edge_id);
                let tail_now_empty = tail_node.outgoing.is_empty();
                self.state.remove_edge_tail_node(edge_id);
                if tail_now_empty {
                    Self::downgrade_outbound_node_type(
                        self.state
                            .node_mut(tail_node_id)
                            .expect("tail node must exist"),
                    );
                }

                // Remove the edge from the (already removed) head node and drop it.
                self.state.remove_edge_head_node(edge_id);
                self.state.remove_edge(edge_id);
            }
        }

        if matches!(node_type, NodeType::Start | NodeType::Normal) {
            // Remove the outgoing edges.
            for edge_id in outgoing {
                let head_node_id = self
                    .state
                    .edge_head_node_id(edge_id)
                    .expect("edge head must exist");

                // Remove the edge from the head node.
                let head_node = self
                    .state
                    .node_mut(head_node_id)
                    .expect("head node must exist");
                head_node.incoming.shift_remove(&edge_id);
                let head_now_empty = head_node.incoming.is_empty();
                self.state.remove_edge_head_node(edge_id);
                if head_now_empty {
                    Self::downgrade_inbound_node_type(
                        self.state
                            .node_mut(head_node_id)
                            .expect("head node must exist"),
                    );
                }

                // Remove the edge from the (already removed) tail node and drop it.
                self.state.remove_edge_tail_node(edge_id);
                self.state.remove_edge(edge_id);
            }
        }
        true
    }

    // When a node loses its last outgoing edge it can no longer be Start or Normal.
    fn downgrade_outbound_node_type(node: &mut Node<K, DependentActivity<K, R, W>>) {
        match node.node_type() {
            NodeType::Normal => node.set_node_type(NodeType::End),
            NodeType::Start => node.set_node_type(NodeType::Isolated),
            _ => {}
        }
    }

    // When a node loses its last incoming edge it can no longer be End or Normal.
    fn downgrade_inbound_node_type(node: &mut Node<K, DependentActivity<K, R, W>>) {
        match node.node_type() {
            NodeType::Normal => node.set_node_type(NodeType::Start),
            NodeType::End => node.set_node_type(NodeType::Isolated),
            _ => {}
        }
    }

    /// Removes the given dependencies from an existing activity.
    pub fn remove_activity_dependencies(
        &mut self,
        activity_id: K,
        dependencies: IndexSet<K>,
    ) -> bool {
        if !self.state.contains_node(activity_id) {
            return false;
        }
        if dependencies.is_empty() {
            return true;
        }

        // If the activity was an unsatisfied successor for these dependencies,
        // then remove them from the lookup.
        for dependency_id in &dependencies {
            self.state
                .remove_activity_from_unsatisfied_successor(*dependency_id, activity_id);
        }

        let node = self.state.node(activity_id).expect("node must exist");
        if matches!(node.node_type(), NodeType::Start | NodeType::Isolated) {
            return true;
        }

        // Remove edges whose tail node is in the specified dependency set.
        let existing_dependency_lookup: IndexSet<K> = self
            .state
            .node_ids()
            .into_iter()
            .filter(|id| dependencies.contains(id))
            .collect();

        let incoming: Vec<K> = node.incoming.iter().copied().collect();
        for edge_id in incoming {
            let tail_node_id = self
                .state
                .edge_tail_node_id(edge_id)
                .expect("edge tail must exist");
            if !existing_dependency_lookup.contains(&tail_node_id) {
                continue;
            }

            // Remove the edge from the tail node.
            let tail_node = self
                .state
                .node_mut(tail_node_id)
                .expect("tail node must exist");
            tail_node.outgoing.shift_remove(&edge_id);
            let tail_now_empty = tail_node.outgoing.is_empty();
            self.state.remove_edge_tail_node(edge_id);
            if tail_now_empty {
                Self::downgrade_outbound_node_type(
                    self.state
                        .node_mut(tail_node_id)
                        .expect("tail node must exist"),
                );
            }

            // Remove the edge from the head node.
            self.state
                .node_mut(activity_id)
                .expect("node must exist")
                .incoming
                .shift_remove(&edge_id);
            self.state.remove_edge_head_node(edge_id);
            // Remove the edge completely.
            self.state.remove_edge(edge_id);
        }

        if self
            .state
            .node(activity_id)
            .expect("node must exist")
            .incoming
            .is_empty()
        {
            Self::downgrade_inbound_node_type(
                self.state.node_mut(activity_id).expect("node must exist"),
            );
        }

        true
    }

    /// Returns the IDs of the activities the given activity currently depends
    /// on within the graph.
    pub fn activity_dependency_ids(&self, activity_id: K) -> Vec<K> {
        let Some(node) = self.state.node(activity_id) else {
            return Vec::new();
        };
        if matches!(node.node_type(), NodeType::Start | NodeType::Isolated) {
            return Vec::new();
        }
        node.incoming
            .iter()
            .map(|edge_id| {
                self.state
                    .edge_tail_node_id(*edge_id)
                    .expect("edge tail must exist")
            })
            .collect()
    }

    /// Returns the strong (resolved) dependency IDs for the given activity ID:
    /// dummy dependencies are transparent, real dependencies terminate the walk.
    pub fn strong_activity_dependency_ids(&self, activity_id: K) -> Vec<K> {
        let Some(node) = self.state.node(activity_id) else {
            return Vec::new();
        };
        if matches!(node.node_type(), NodeType::Start | NodeType::Isolated) {
            return Vec::new();
        }
        // Iterative walk so a long dummy chain cannot overflow the stack.
        // Expanded dummy node IDs are tracked so a shared dummy sub-path is
        // walked only once; callers use the result as a set.
        let mut output: Vec<K> = Vec::new();
        let mut expanded_dummies: IndexSet<K> = IndexSet::new();
        let mut stack: Vec<K> = vec![activity_id];
        while let Some(current_id) = stack.pop() {
            let current_node = self.state.node(current_id).expect("node must exist");
            // Start/Isolated nodes have no incoming edges to follow.
            if matches!(
                current_node.node_type(),
                NodeType::Start | NodeType::Isolated
            ) {
                continue;
            }
            for incoming_edge_id in &current_node.incoming {
                let tail_node_id = self
                    .state
                    .edge_tail_node_id(*incoming_edge_id)
                    .expect("edge tail must exist");
                let tail_node = self.state.node(tail_node_id).expect("node must exist");
                if tail_node.content.is_dummy() {
                    if expanded_dummies.insert(tail_node_id) {
                        stack.push(tail_node_id);
                    }
                } else {
                    output.push(tail_node_id);
                }
            }
        }
        output
    }

    /// Finds the strongly-connected circular dependencies in the graph.
    pub fn find_strong_circular_dependencies(&self) -> Vec<CircularDependency<K>> {
        crate::tarjan::find_strongly_circular_dependencies(
            &VertexTraversal { state: &self.state },
            true,
        )
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
        reducer::get_ancestor_nodes_lookup(&self.state)
    }

    /// Performs transitive reduction, removing all redundant edges. Returns
    /// false if it cannot be performed.
    pub fn transitive_reduction(&mut self) -> bool {
        reducer::reduce_graph(&mut self.state)
    }

    /// Redirects redundant edges; a documented no-op for vertex graphs.
    pub fn redirect_edges(&mut self) -> bool {
        // Edges should not need to be redirected in a vertex graph.
        true
    }

    /// Removes transitively-implied edges; a documented no-op for vertex graphs.
    pub fn remove_redundant_edges(&mut self) -> bool {
        // All redundant edges should have been removed by other methods.
        true
    }

    /// Runs the edge clean-up sequence (redirection then redundant-edge removal).
    pub fn clean_up_edges(&mut self) -> bool {
        if !self.redirect_edges() {
            return false;
        }
        if !self.remove_redundant_edges() {
            return false;
        }
        true
    }

    /// Clears the computed critical-path values from all activities and events.
    pub fn clear_critical_path_variables(&mut self) {
        for node in self.state.nodes.values_mut() {
            node.content.free_slack = None;
            node.content.earliest_start_time = None;
            node.content.latest_finish_time = None;
        }
        for edge in self.state.edges.values_mut() {
            edge.content.earliest_finish_time = None;
            edge.content.latest_finish_time = None;
        }
    }

    /// Runs the forward (earliest times) critical-path pass.
    pub fn calculate_critical_path_forward_flow(&mut self) -> Result<bool, GraphError> {
        if !self.all_dependencies_satisfied() {
            return Ok(false);
        }
        if !self.find_invalid_pre_compilation_constraints().is_empty() {
            return Ok(false);
        }
        cpm::calculate_critical_path_forward_flow(
            &mut self.state,
            &[],
            self.shuffle_processing_order,
        )
    }

    /// Runs the backward (latest times and slack) critical-path pass.
    pub fn calculate_critical_path_backward_flow(&mut self) -> Result<bool, GraphError> {
        if !self.all_dependencies_satisfied() {
            return Ok(false);
        }
        if !self.find_invalid_pre_compilation_constraints().is_empty() {
            return Ok(false);
        }
        cpm::calculate_critical_path_backward_flow(
            &mut self.state,
            &[],
            self.shuffle_processing_order,
        )
    }

    /// Calculates the critical path across the whole graph.
    pub fn calculate_critical_path(&mut self) -> Result<(), GraphError> {
        if !self.remove_redundant_edges() {
            return Err(GraphError::new(messages::MSG_CANNOT_REMOVE_REDUNDANT_EDGES));
        }

        self.clear_critical_path_variables();

        let constraints = if self.all_dependencies_satisfied() {
            self.find_invalid_pre_compilation_constraints()
        } else {
            Vec::new()
        };

        if !cpm::calculate_critical_path_forward_flow(
            &mut self.state,
            &constraints,
            self.shuffle_processing_order,
        )? {
            return Err(GraphError::new(
                messages::MSG_CANNOT_CALCULATE_CRITICAL_PATH_FORWARD_FLOW,
            ));
        }

        if !cpm::calculate_critical_path_backward_flow(
            &mut self.state,
            &constraints,
            self.shuffle_processing_order,
        )? {
            return Err(GraphError::new(
                messages::MSG_CANNOT_CALCULATE_CRITICAL_PATH_BACKWARD_FLOW,
            ));
        }

        if !self.redirect_edges() {
            return Err(GraphError::new(
                messages::MSG_CANNOT_PERFORM_EDGE_REDIRECTION,
            ));
        }
        Ok(())
    }

    /// Fills in critical-path values for isolated activities, which the flow
    /// passes do not reach.
    pub fn back_fill_isolated_nodes(&mut self) -> bool {
        let constraints = if self.all_dependencies_satisfied() {
            self.find_invalid_pre_compilation_constraints()
        } else {
            Vec::new()
        };
        cpm::back_fill_isolated_nodes(&mut self.state, &constraints)
    }

    /// Returns the activity IDs in scheduling priority order (most critical first).
    pub fn calculate_critical_path_priority_list(&mut self) -> Result<Vec<K>, GraphError> {
        let mut tmp_graph_builder = self.clone_builder()?;
        Self::calculate_critical_path_priority_list_on(&mut tmp_graph_builder)
    }

    fn calculate_critical_path_priority_list_on(
        graph_builder: &mut VertexGraphBuilder<K, R, W>,
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
        if self.state.nodes.is_empty() {
            return Ok(Vec::new());
        }

        // If resources are 0, assume infinite.
        let infinite_resources = resources.is_empty();

        // Filter out inactive resources.
        let filtered_resources: Vec<Resource<R, W>> = resources
            .iter()
            .filter(|x| !x.is_inactive)
            .cloned()
            .collect();

        // If resources are limited, check to make sure all activities can be accepted.
        if !infinite_resources {
            self.validate_activities_against_resources(&filtered_resources)?;
        }

        let mut tmp_graph_builder = self.clone_builder()?;

        // Use a separate clone for the priority list calculation so that
        // tmp_graph_builder retains original activity durations for the
        // scheduling loop below.
        let mut priority_clone = tmp_graph_builder.clone_builder()?;
        let priority_list = Self::calculate_critical_path_priority_list_on(&mut priority_clone)?;

        scheduling::calculate_resource_schedules(
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
    pub fn to_graph(&mut self) -> Result<crate::VertexGraph<K, R, W>, GraphError> {
        if !self.clean_up_edges() {
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
    }

    /// Clones the builder (via graph export, as in the C# `CloneObject`). Only
    /// the edge ID generator is recreated, stepping downward from below the
    /// minimum exported edge ID; the shuffle flag resets to false.
    pub fn clone_builder(&mut self) -> Result<Self, GraphError> {
        let graph = self.to_graph()?;
        let min_edge_id = graph
            .edges
            .iter()
            .map(|e| e.id())
            .min()
            .unwrap_or_default()
            .previous();
        Self::from_graph(graph, IdGenerator::Previous(min_edge_id))
    }

    /// Replaces an activity's compiled and planning dependencies, reconciling
    /// them with any existing resource dependencies already wired into the graph.
    pub fn set_activity_dependencies(
        &mut self,
        activity_id: K,
        dependencies: IndexSet<K>,
        planning_dependencies: IndexSet<K>,
    ) -> bool {
        if !self.state.contains_node(activity_id) {
            return false;
        }

        let (resource_deps, compiled_deps, planning_deps) = {
            let activity = self.activity(activity_id).expect("activity must exist");
            (
                activity.resource_dependencies.clone(),
                activity.dependencies.clone(),
                activity.planning_dependencies.clone(),
            )
        };

        let intersect = |a: &IndexSet<K>, b: &IndexSet<K>| -> IndexSet<K> {
            a.iter().filter(|x| b.contains(*x)).copied().collect()
        };
        let union = |a: &IndexSet<K>, b: &IndexSet<K>| -> IndexSet<K> {
            a.iter().chain(b.iter()).copied().collect()
        };
        let except = |a: &IndexSet<K>, b: &IndexSet<K>| -> IndexSet<K> {
            a.iter().filter(|x| !b.contains(*x)).copied().collect()
        };

        let resource_and_compiled = intersect(&resource_deps, &compiled_deps);
        let resource_and_planning = intersect(&resource_deps, &planning_deps);

        let resource_or_compiled = union(&resource_deps, &compiled_deps);
        let resource_or_planning = union(&resource_deps, &planning_deps);

        let compiled_not_resource = except(&compiled_deps, &resource_deps);
        let planning_not_resource = except(&planning_deps, &resource_deps);

        let resource_not_compiled = except(&resource_deps, &compiled_deps);
        let resource_not_planning = except(&resource_deps, &planning_deps);

        let mut successfully_removed = true;
        let mut successfully_added = true;

        let current_union = |activity: &DependentActivity<K, R, W>| -> IndexSet<K> {
            activity
                .dependencies
                .iter()
                .chain(activity.planning_dependencies.iter())
                .chain(activity.resource_dependencies.iter())
                .copied()
                .collect()
        };

        // Resource: 1, Core: 1, New: 0
        {
            let to_be_removed_from_compiled = except(&resource_and_compiled, &dependencies);
            let to_be_removed_from_planning =
                except(&resource_and_planning, &planning_dependencies);
            {
                let activity = self.activity_mut(activity_id).expect("activity must exist");
                for dependency_id in &to_be_removed_from_compiled {
                    activity.dependencies.shift_remove(dependency_id);
                }
                for dependency_id in &to_be_removed_from_planning {
                    activity.planning_dependencies.shift_remove(dependency_id);
                }
            }

            let updated = current_union(self.activity(activity_id).expect("activity must exist"));
            let current: IndexSet<K> = self
                .activity_dependency_ids(activity_id)
                .into_iter()
                .collect();
            let mut to_be_removed = except(&current, &updated);
            to_be_removed.extend(to_be_removed_from_compiled.iter().copied());
            to_be_removed.extend(to_be_removed_from_planning.iter().copied());
            successfully_removed &= self.remove_activity_dependencies(activity_id, to_be_removed);
        }

        // Resource: 1, Core: 0, New: 1
        {
            let to_be_added_to_compiled = intersect(&resource_not_compiled, &dependencies);
            let to_be_added_to_planning = intersect(&resource_not_planning, &planning_dependencies);
            {
                let activity = self.activity_mut(activity_id).expect("activity must exist");
                for dependency_id in &to_be_added_to_compiled {
                    activity.dependencies.insert(*dependency_id);
                }
                for dependency_id in &to_be_added_to_planning {
                    activity.planning_dependencies.insert(*dependency_id);
                }
            }

            let updated = current_union(self.activity(activity_id).expect("activity must exist"));
            let current: IndexSet<K> = self
                .activity_dependency_ids(activity_id)
                .into_iter()
                .collect();
            let mut to_be_added = except(&updated, &current);
            to_be_added.extend(to_be_added_to_compiled.iter().copied());
            to_be_added.extend(to_be_added_to_planning.iter().copied());
            successfully_added &= self.add_activity_dependencies(activity_id, to_be_added);
        }

        // Resource: 0, Core: 1, New: 0
        {
            let to_be_removed_from_compiled = except(&compiled_not_resource, &dependencies);
            let to_be_removed_from_planning =
                except(&planning_not_resource, &planning_dependencies);
            {
                let activity = self.activity_mut(activity_id).expect("activity must exist");
                for dependency_id in &to_be_removed_from_compiled {
                    activity.dependencies.shift_remove(dependency_id);
                }
                for dependency_id in &to_be_removed_from_planning {
                    activity.planning_dependencies.shift_remove(dependency_id);
                }
            }

            let updated = current_union(self.activity(activity_id).expect("activity must exist"));
            let current: IndexSet<K> = self
                .activity_dependency_ids(activity_id)
                .into_iter()
                .collect();
            let mut to_be_removed = except(&current, &updated);
            to_be_removed.extend(to_be_removed_from_compiled.iter().copied());
            to_be_removed.extend(to_be_removed_from_planning.iter().copied());
            successfully_removed &= self.remove_activity_dependencies(activity_id, to_be_removed);
        }

        // Resource: 0, Core: 0, New: X
        {
            let to_be_added_to_compiled = except(&dependencies, &resource_or_compiled);
            let to_be_added_to_planning = except(&planning_dependencies, &resource_or_planning);
            {
                let activity = self.activity_mut(activity_id).expect("activity must exist");
                for dependency_id in &to_be_added_to_compiled {
                    activity.dependencies.insert(*dependency_id);
                }
                for dependency_id in &to_be_added_to_planning {
                    activity.planning_dependencies.insert(*dependency_id);
                }
            }

            let updated = current_union(self.activity(activity_id).expect("activity must exist"));
            let current: IndexSet<K> = self
                .activity_dependency_ids(activity_id)
                .into_iter()
                .collect();
            let mut to_be_added = except(&updated, &current);
            to_be_added.extend(to_be_added_to_compiled.iter().copied());
            to_be_added.extend(to_be_added_to_planning.iter().copied());
            successfully_added &= self.add_activity_dependencies(activity_id, to_be_added);
        }

        successfully_removed && successfully_added
    }

    /// Clears allocated-resource state from the given activities ahead of a new
    /// scheduling pass.
    pub fn reset_resource_state(&mut self, activity_ids: &[K]) {
        for activity_id in activity_ids {
            let Some(activity) = self.activity(*activity_id) else {
                continue;
            };
            let core_dependencies: IndexSet<K> = activity
                .dependencies
                .iter()
                .chain(activity.planning_dependencies.iter())
                .copied()
                .collect();
            let resource_only: IndexSet<K> = activity
                .resource_dependencies
                .iter()
                .filter(|x| !core_dependencies.contains(*x))
                .copied()
                .collect();
            self.remove_activity_dependencies(*activity_id, resource_only);
            let activity = self
                .activity_mut(*activity_id)
                .expect("activity must exist");
            activity.resource_dependencies.clear();
            activity.allocated_to_resources.clear();
        }
    }

    /// Wires resource dependencies into the graph from the finished schedules.
    pub fn assign_resource_dependencies(
        &mut self,
        resource_schedules: &[ResourceSchedule<K, R, W>],
    ) {
        for schedule in resource_schedules {
            let resource_id = schedule.resource.as_ref().map(|r| r.id);
            let mut previous_id: Option<K> = None;

            let mut scheduled: Vec<&ScheduledActivity<K>> =
                schedule.scheduled_activities.iter().collect();
            scheduled.sort_by_key(|x| x.start_time);

            for scheduled_activity in scheduled {
                let current_id = scheduled_activity.id;
                let Some(activity) = self.activity_mut(current_id) else {
                    continue;
                };

                if let Some(rid) = resource_id {
                    activity.allocated_to_resources.insert(rid);
                }

                if let Some(prev) = previous_id {
                    activity.resource_dependencies.insert(prev);
                    let core_dependencies: IndexSet<K> = activity
                        .dependencies
                        .iter()
                        .chain(activity.planning_dependencies.iter())
                        .copied()
                        .collect();
                    let resource_only: IndexSet<K> = activity
                        .resource_dependencies
                        .iter()
                        .filter(|x| !core_dependencies.contains(*x))
                        .copied()
                        .collect();
                    self.add_activity_dependencies(current_id, resource_only);
                }

                previous_id = Some(scheduled_activity.id);
            }
        }
    }

    /// Removes dependencies that exist only because of resource allocation.
    pub fn remove_resource_only_dependencies(&mut self, activity_ids: &[K]) {
        for activity_id in activity_ids {
            let Some(activity) = self.activity(*activity_id) else {
                continue;
            };
            let core_dependencies: IndexSet<K> = activity
                .dependencies
                .iter()
                .chain(activity.planning_dependencies.iter())
                .copied()
                .collect();
            let resource_only: IndexSet<K> = activity
                .resource_dependencies
                .iter()
                .filter(|x| !core_dependencies.contains(*x))
                .copied()
                .collect();
            self.remove_activity_dependencies(*activity_id, resource_only);
        }
    }

    /// Populates each activity's successors from the current dependencies.
    pub fn update_activity_successors(&mut self, activity_ids: &[K]) {
        for activity_id in activity_ids {
            let Some(node) = self.state.node(*activity_id) else {
                continue;
            };
            let node_type = node.node_type();
            let successor_node_ids: Vec<K> =
                if matches!(node_type, NodeType::Start | NodeType::Normal) {
                    node.outgoing
                        .iter()
                        .map(|edge_id| {
                            self.state
                                .edge_head_node_id(*edge_id)
                                .expect("edge head must exist")
                        })
                        .collect()
                } else {
                    Vec::new()
                };

            let activity = self
                .activity_mut(*activity_id)
                .expect("activity must exist");
            activity.successors.clear();
            if matches!(node_type, NodeType::Start | NodeType::Normal) {
                activity.successors.extend(successor_node_ids);
            }
        }
    }

    /// Checks all pre-compilation conditions and appends any errors found.
    pub fn add_pre_compilation_errors(
        &mut self,
        errors: &mut Vec<GraphCompilationError>,
        filtered_resources: &[Resource<R, W>],
        infinite_resources: bool,
    ) {
        let invalid_dependencies = self.invalid_dependencies();
        let circular_dependencies = self.find_strong_circular_dependencies();
        let invalid_precompilation_constraints = self.find_invalid_pre_compilation_constraints();

        // P0010
        if !invalid_dependencies.is_empty() {
            let activities: Vec<&DependentActivity<K, R, W>> = self.activities().collect();
            errors.push(GraphCompilationError::new(
                GraphCompilationErrorCode::P0010,
                error_formatter::build_invalid_dependencies_error_message(
                    &invalid_dependencies,
                    &activities,
                ),
            ));
        }

        // P0020
        if !circular_dependencies.is_empty() {
            errors.push(GraphCompilationError::new(
                GraphCompilationErrorCode::P0020,
                error_formatter::build_circular_dependencies_error_message(&circular_dependencies),
            ));
        }

        // P0030
        if !invalid_precompilation_constraints.is_empty() {
            errors.push(GraphCompilationError::new(
                GraphCompilationErrorCode::P0030,
                error_formatter::build_invalid_constraints_error_message(
                    &invalid_precompilation_constraints,
                ),
            ));
        }

        // P0040
        let all_resources_explicit_but_not_all_activities_targeted = !infinite_resources
            && filtered_resources.iter().all(|x| x.is_explicit_target)
            && self
                .activities()
                .any(|x| !x.is_dummy() && x.target_resources.is_empty());
        if all_resources_explicit_but_not_all_activities_targeted {
            errors.push(GraphCompilationError::new(
                GraphCompilationErrorCode::P0040,
                format!(
                    "{}\n",
                    messages::MSG_ALL_RESOURCES_EXPLICIT_TARGETS_NOT_ALL_ACTIVITIES_TARGETED
                ),
            ));
        }

        // P0050
        if !self.clean_up_edges() {
            errors.push(GraphCompilationError::new(
                GraphCompilationErrorCode::P0050,
                format!("{}\n", messages::MSG_UNABLE_TO_REMOVE_UNNECESSARY_EDGES),
            ));
        }

        // Check if any activities are obliged to use only explicit target
        // resources that are unavailable.
        let unavailable_resources_set: Vec<UnavailableResources<K, R>> = if infinite_resources {
            Vec::new()
        } else {
            scheduling::gather_unavailable_resources(
                self.activities().map(|a| &a.activity),
                filtered_resources,
            )
        };
        // P0060
        if !unavailable_resources_set.is_empty() {
            errors.push(GraphCompilationError::new(
                GraphCompilationErrorCode::P0060,
                error_formatter::build_unavailable_resources_error_message(
                    &unavailable_resources_set,
                ),
            ));
        }
    }

    /// Appends any post-compilation constraint errors to the error list.
    pub fn add_post_compilation_errors(&self, errors: &mut Vec<GraphCompilationError>) {
        let invalid_postcompilation_constraints = self.find_invalid_post_compilation_constraints();
        // C0010
        if !invalid_postcompilation_constraints.is_empty() {
            errors.push(GraphCompilationError::new(
                GraphCompilationErrorCode::C0010,
                error_formatter::build_invalid_constraints_error_message(
                    &invalid_postcompilation_constraints,
                ),
            ));
        }
    }

    fn resolve_unsatisfied_successor_activities(&mut self, activity_id: K) {
        // Check to make sure the node really exists.
        if !self.state.contains_node(activity_id) {
            return;
        }

        // Check to see if any existing activities were expecting this activity
        // as a dependency. If so, then hook their nodes to this activity with
        // an edge.
        let Some(successor_ids) = self.state.unsatisfied_successors.get(&activity_id) else {
            return;
        };
        let successor_ids: Vec<K> = successor_ids.iter().copied().collect();

        // If the dependency node is an End or Isolated node, then convert it.
        let dependency_node = self.state.node_mut(activity_id).expect("node must exist");
        match dependency_node.node_type() {
            NodeType::End => dependency_node.set_node_type(NodeType::Normal),
            NodeType::Isolated => dependency_node.set_node_type(NodeType::Start),
            _ => {}
        }

        for successor_id in successor_ids {
            let edge = self.generate_event();
            let edge_id = edge.id();
            self.state
                .node_mut(activity_id)
                .expect("node must exist")
                .outgoing
                .insert(edge_id);
            self.state.set_edge_tail_node(edge_id, activity_id);
            self.state
                .node_mut(successor_id)
                .expect("successor node must exist")
                .incoming
                .insert(edge_id);
            self.state.set_edge_head_node(edge_id, successor_id);
            self.state.add_edge(edge);
        }
        self.state.remove_unsatisfied_successors(activity_id);
    }
}

impl<K: Key, R: Key, W: Key> ResourceSchedulingGraph<K, R, W> for VertexGraphBuilder<K, R, W> {
    fn activity(&self, id: K) -> &DependentActivity<K, R, W> {
        VertexGraphBuilder::activity(self, id).expect("activity must exist")
    }

    fn activity_mut(&mut self, id: K) -> &mut DependentActivity<K, R, W> {
        VertexGraphBuilder::activity_mut(self, id).expect("activity must exist")
    }

    fn strong_activity_dependency_ids(&self, id: K) -> Vec<K> {
        VertexGraphBuilder::strong_activity_dependency_ids(self, id)
    }

    fn clone_activities(&self) -> Vec<DependentActivity<K, R, W>> {
        self.activities().cloned().collect()
    }
}
