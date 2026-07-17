use crate::ancestor::AncestorGraphView;
use crate::tarjan::GraphTraversal;
use indexmap::{IndexMap, IndexSet};
use zametek_maths_graphs_primitives::{DependentActivity, Edge, Event, Key, Node, NodeType};

/// All mutable graph state for an Activity-on-Vertex graph — the counterpart
/// of the C# `VertexGraphState`. Activities live on nodes; events live on
/// edges.
///
/// The C# class stores node references in its lookups; this port stores node
/// IDs (nodes are uniquely identified within one graph). Insertion-ordered
/// maps stand in for the C# `Dictionary`/`HashSet`, whose iteration order the
/// original relies on for deterministic output.
pub(crate) struct VertexState<K: Key, R: Key, W: Key> {
    pub(crate) edges: IndexMap<K, Edge<K, Event<K>>>,
    pub(crate) nodes: IndexMap<K, Node<K, DependentActivity<K, R, W>>>,
    /// Dependency ID -> the node IDs waiting for that dependency to appear.
    pub(crate) unsatisfied_successors: IndexMap<K, IndexSet<K>>,
    /// Edge ID -> the node the edge points to.
    pub(crate) edge_head: IndexMap<K, K>,
    /// Edge ID -> the node the edge starts from.
    pub(crate) edge_tail: IndexMap<K, K>,
}

impl<K: Key, R: Key, W: Key> VertexState<K, R, W> {
    pub(crate) fn new() -> Self {
        Self {
            edges: IndexMap::new(),
            nodes: IndexMap::new(),
            unsatisfied_successors: IndexMap::new(),
            edge_head: IndexMap::new(),
            edge_tail: IndexMap::new(),
        }
    }

    // Kept for API parity with the C# state class.
    #[allow(dead_code)]
    pub(crate) fn contains_edge(&self, edge_id: K) -> bool {
        self.edges.contains_key(&edge_id)
    }

    pub(crate) fn contains_node(&self, node_id: K) -> bool {
        self.nodes.contains_key(&node_id)
    }

    pub(crate) fn node(&self, node_id: K) -> Option<&Node<K, DependentActivity<K, R, W>>> {
        self.nodes.get(&node_id)
    }

    pub(crate) fn node_mut(
        &mut self,
        node_id: K,
    ) -> Option<&mut Node<K, DependentActivity<K, R, W>>> {
        self.nodes.get_mut(&node_id)
    }

    pub(crate) fn edge(&self, edge_id: K) -> Option<&Edge<K, Event<K>>> {
        self.edges.get(&edge_id)
    }

    pub(crate) fn edge_mut(&mut self, edge_id: K) -> Option<&mut Edge<K, Event<K>>> {
        self.edges.get_mut(&edge_id)
    }

    pub(crate) fn edge_head_node_id(&self, edge_id: K) -> Option<K> {
        self.edge_head.get(&edge_id).copied()
    }

    pub(crate) fn edge_tail_node_id(&self, edge_id: K) -> Option<K> {
        self.edge_tail.get(&edge_id).copied()
    }

    pub(crate) fn add_edge(&mut self, edge: Edge<K, Event<K>>) {
        let previous = self.edges.insert(edge.id(), edge);
        debug_assert!(previous.is_none(), "duplicate edge id");
    }

    pub(crate) fn remove_edge(&mut self, edge_id: K) -> bool {
        self.edges.shift_remove(&edge_id).is_some()
    }

    pub(crate) fn add_node(&mut self, node: Node<K, DependentActivity<K, R, W>>) {
        let previous = self.nodes.insert(node.id(), node);
        debug_assert!(previous.is_none(), "duplicate node id");
    }

    pub(crate) fn remove_node(&mut self, node_id: K) -> bool {
        self.nodes.shift_remove(&node_id).is_some()
    }

    pub(crate) fn set_edge_head_node(&mut self, edge_id: K, node_id: K) {
        let previous = self.edge_head.insert(edge_id, node_id);
        debug_assert!(previous.is_none(), "duplicate edge head");
    }

    pub(crate) fn remove_edge_head_node(&mut self, edge_id: K) -> bool {
        self.edge_head.shift_remove(&edge_id).is_some()
    }

    pub(crate) fn set_edge_tail_node(&mut self, edge_id: K, node_id: K) {
        let previous = self.edge_tail.insert(edge_id, node_id);
        debug_assert!(previous.is_none(), "duplicate edge tail");
    }

    pub(crate) fn remove_edge_tail_node(&mut self, edge_id: K) -> bool {
        self.edge_tail.shift_remove(&edge_id).is_some()
    }

    pub(crate) fn add_unsatisfied_successor(&mut self, dependency_id: K, successor_node_id: K) {
        self.unsatisfied_successors
            .entry(dependency_id)
            .or_default()
            .insert(successor_node_id);
    }

    pub(crate) fn remove_unsatisfied_successors(&mut self, dependency_id: K) -> bool {
        self.unsatisfied_successors
            .shift_remove(&dependency_id)
            .is_some()
    }

    /// Removes an activity from the unsatisfied-successor set keyed under
    /// `dependency_id`. If that set becomes empty, drops the entry entirely.
    pub(crate) fn remove_activity_from_unsatisfied_successor(
        &mut self,
        dependency_id: K,
        activity_id: K,
    ) {
        if let Some(nodes) = self.unsatisfied_successors.get_mut(&dependency_id) {
            nodes.shift_remove(&activity_id);
            if nodes.is_empty() {
                self.unsatisfied_successors.shift_remove(&dependency_id);
            }
        }
    }

    /// Removes the activity from every unsatisfied-successor set it appears
    /// in, dropping any sets that become empty as a result.
    pub(crate) fn remove_activity_from_all_unsatisfied_successors(&mut self, activity_id: K) {
        let keys_containing: Vec<K> = self
            .unsatisfied_successors
            .iter()
            .filter(|(_, nodes)| nodes.contains(&activity_id))
            .map(|(key, _)| *key)
            .collect();

        for key in keys_containing {
            if let Some(nodes) = self.unsatisfied_successors.get_mut(&key) {
                nodes.shift_remove(&activity_id);
                if nodes.is_empty() {
                    self.unsatisfied_successors.shift_remove(&key);
                }
            }
        }
    }

    pub(crate) fn all_dependencies_satisfied(&self) -> bool {
        self.unsatisfied_successors.is_empty()
    }

    pub(crate) fn invalid_dependencies(&self) -> Vec<K> {
        self.unsatisfied_successors.keys().copied().collect()
    }

    pub(crate) fn node_ids(&self) -> Vec<K> {
        self.nodes.keys().copied().collect()
    }

    pub(crate) fn edge_ids(&self) -> Vec<K> {
        self.edges.keys().copied().collect()
    }

    pub(crate) fn nodes_of_type(&self, node_type: NodeType) -> Vec<K> {
        self.nodes
            .values()
            .filter(|n| n.node_type() == node_type)
            .map(|n| n.id())
            .collect()
    }

    pub(crate) fn clear(&mut self) {
        self.edges.clear();
        self.nodes.clear();
        self.unsatisfied_successors.clear();
        self.edge_head.clear();
        self.edge_tail.clear();
    }

    /// Validation helper used by the graph-loading constructor.
    pub(crate) fn edge_keys_match(&self, other_keys: impl IntoIterator<Item = K>) -> bool {
        let mut a: Vec<K> = self.edges.keys().copied().collect();
        let mut b: Vec<K> = other_keys.into_iter().collect();
        a.sort();
        b.sort();
        a == b
    }
}

/// Node-space traversal for the shared Tarjan algorithm — the counterpart of
/// the C# `VertexGraphTraversal`.
pub(crate) struct VertexTraversal<'a, K: Key, R: Key, W: Key> {
    pub(crate) state: &'a VertexState<K, R, W>,
}

impl<K: Key, R: Key, W: Key> GraphTraversal<K> for VertexTraversal<'_, K, R, W> {
    fn keys(&self) -> Vec<K> {
        self.state.node_ids()
    }

    fn predecessor_keys(&self, reference_id: K) -> Vec<K> {
        // The predecessors of a node are the tail nodes of its incoming edges.
        let Some(node) = self.state.node(reference_id) else {
            return Vec::new();
        };
        if matches!(node.node_type(), NodeType::End | NodeType::Normal) {
            node.incoming
                .iter()
                .filter_map(|edge_id| self.state.edge_tail_node_id(*edge_id))
                .collect()
        } else {
            Vec::new()
        }
    }

    fn is_removable(&self, reference_id: K) -> bool {
        self.state
            .node(reference_id)
            .map(|n| n.content.can_be_removed())
            .unwrap_or(false)
    }
}

/// Node-space view for the shared ancestor calculation — the counterpart of
/// the C# `VertexAncestorGraphView`.
pub(crate) struct VertexAncestorView<'a, K: Key, R: Key, W: Key> {
    pub(crate) state: &'a VertexState<K, R, W>,
}

impl<K: Key, R: Key, W: Key> AncestorGraphView<K> for VertexAncestorView<'_, K, R, W> {
    fn end_node_ids(&self) -> Vec<K> {
        self.state.nodes_of_type(NodeType::End)
    }

    fn is_root_node(&self, node_id: K) -> bool {
        self.state
            .node(node_id)
            .map(|n| matches!(n.node_type(), NodeType::Start | NodeType::Isolated))
            .unwrap_or(false)
    }

    fn parent_node_ids(&self, node_id: K) -> Vec<K> {
        self.state
            .node(node_id)
            .map(|n| {
                n.incoming
                    .iter()
                    .filter_map(|edge_id| self.state.edge_tail_node_id(*edge_id))
                    .collect()
            })
            .unwrap_or_default()
    }
}
