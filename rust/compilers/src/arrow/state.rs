use crate::ancestor::AncestorGraphView;
use crate::tarjan::GraphTraversal;
use indexmap::{IndexMap, IndexSet};
use zametek_maths_graphs_primitives::{DependentActivity, Edge, Event, Key, Node, NodeType};

/// All mutable graph state for an Activity-on-Arrow graph - the counterpart of
/// the C# `ArrowGraphState` (and its public `IArrowGraphState` contract).
/// Activities live on edges; events live on nodes. There is a single Start node
/// and a single End node.
///
/// The struct is public so injected engines can operate on it, but its fields
/// are crate-private: only the builder constructs and owns the raw lookups, and
/// external engines go through the method surface below (mirroring the way the
/// C# state is an `internal sealed` class behind a public interface).
pub struct ArrowGraphState<K: Key, R: Key, W: Key> {
    pub(crate) edges: IndexMap<K, Edge<K, DependentActivity<K, R, W>>>,
    pub(crate) nodes: IndexMap<K, Node<K, Event<K>>>,
    /// Dependency activity ID -> the tail-node IDs waiting for it to appear.
    pub(crate) unsatisfied_successors: IndexMap<K, IndexSet<K>>,
    pub(crate) edge_head: IndexMap<K, K>,
    pub(crate) edge_tail: IndexMap<K, K>,
    pub(crate) start_node_id: Option<K>,
    pub(crate) end_node_id: Option<K>,
}

impl<K: Key, R: Key, W: Key> ArrowGraphState<K, R, W> {
    pub(crate) fn new() -> Self {
        Self {
            edges: IndexMap::new(),
            nodes: IndexMap::new(),
            unsatisfied_successors: IndexMap::new(),
            edge_head: IndexMap::new(),
            edge_tail: IndexMap::new(),
            start_node_id: None,
            end_node_id: None,
        }
    }

    pub fn contains_edge(&self, edge_id: K) -> bool {
        self.edges.contains_key(&edge_id)
    }

    pub fn contains_node(&self, node_id: K) -> bool {
        self.nodes.contains_key(&node_id)
    }

    pub fn node(&self, node_id: K) -> Option<&Node<K, Event<K>>> {
        self.nodes.get(&node_id)
    }

    pub fn node_mut(&mut self, node_id: K) -> Option<&mut Node<K, Event<K>>> {
        self.nodes.get_mut(&node_id)
    }

    pub fn edge(&self, edge_id: K) -> Option<&Edge<K, DependentActivity<K, R, W>>> {
        self.edges.get(&edge_id)
    }

    pub fn edge_mut(&mut self, edge_id: K) -> Option<&mut Edge<K, DependentActivity<K, R, W>>> {
        self.edges.get_mut(&edge_id)
    }

    pub fn edge_head_node_id(&self, edge_id: K) -> Option<K> {
        self.edge_head.get(&edge_id).copied()
    }

    pub fn edge_tail_node_id(&self, edge_id: K) -> Option<K> {
        self.edge_tail.get(&edge_id).copied()
    }

    pub fn add_edge(&mut self, edge: Edge<K, DependentActivity<K, R, W>>) {
        let previous = self.edges.insert(edge.id(), edge);
        debug_assert!(previous.is_none(), "duplicate edge id");
    }

    pub fn remove_edge(&mut self, edge_id: K) -> bool {
        self.edges.shift_remove(&edge_id).is_some()
    }

    pub fn add_node(&mut self, node: Node<K, Event<K>>) {
        let previous = self.nodes.insert(node.id(), node);
        debug_assert!(previous.is_none(), "duplicate node id");
    }

    pub fn remove_node(&mut self, node_id: K) -> bool {
        self.nodes.shift_remove(&node_id).is_some()
    }

    pub fn set_edge_head_node(&mut self, edge_id: K, node_id: K) {
        let previous = self.edge_head.insert(edge_id, node_id);
        debug_assert!(previous.is_none(), "duplicate edge head");
    }

    pub fn remove_edge_head_node(&mut self, edge_id: K) -> bool {
        self.edge_head.shift_remove(&edge_id).is_some()
    }

    pub fn set_edge_tail_node(&mut self, edge_id: K, node_id: K) {
        let previous = self.edge_tail.insert(edge_id, node_id);
        debug_assert!(previous.is_none(), "duplicate edge tail");
    }

    pub fn remove_edge_tail_node(&mut self, edge_id: K) -> bool {
        self.edge_tail.shift_remove(&edge_id).is_some()
    }

    pub fn add_unsatisfied_successor(&mut self, dependency_id: K, successor_node_id: K) {
        self.unsatisfied_successors
            .entry(dependency_id)
            .or_default()
            .insert(successor_node_id);
    }

    pub fn remove_unsatisfied_successors(&mut self, dependency_id: K) -> bool {
        self.unsatisfied_successors
            .shift_remove(&dependency_id)
            .is_some()
    }

    pub fn all_dependencies_satisfied(&self) -> bool {
        self.unsatisfied_successors.is_empty()
    }

    pub fn invalid_dependencies(&self) -> Vec<K> {
        self.unsatisfied_successors.keys().copied().collect()
    }

    pub fn node_ids(&self) -> Vec<K> {
        self.nodes.keys().copied().collect()
    }

    pub fn edge_ids(&self) -> Vec<K> {
        self.edges.keys().copied().collect()
    }

    pub fn nodes_of_type(&self, node_type: NodeType) -> Vec<K> {
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
        self.start_node_id = None;
        self.end_node_id = None;
    }

    /// Validation helper used by the graph-loading constructor.
    pub fn edge_keys_match(&self, other_keys: impl IntoIterator<Item = K>) -> bool {
        let mut a: Vec<K> = self.edges.keys().copied().collect();
        let mut b: Vec<K> = other_keys.into_iter().collect();
        a.sort();
        b.sort();
        a == b
    }
}

/// Edge-space traversal for the shared Tarjan algorithm - the counterpart of
/// the C# `ArrowGraphTraversal`. Internal, used by the default SCC finder.
pub(crate) struct ArrowTraversal<'a, K: Key, R: Key, W: Key> {
    pub(crate) state: &'a ArrowGraphState<K, R, W>,
}

impl<K: Key, R: Key, W: Key> GraphTraversal<K> for ArrowTraversal<'_, K, R, W> {
    fn keys(&self) -> Vec<K> {
        self.state.edge_ids()
    }

    fn predecessor_keys(&self, reference_id: K) -> Vec<K> {
        // The predecessors of an edge are the incoming edges of its tail node.
        let Some(tail_node_id) = self.state.edge_tail_node_id(reference_id) else {
            return Vec::new();
        };
        let Some(tail_node) = self.state.node(tail_node_id) else {
            return Vec::new();
        };
        if matches!(tail_node.node_type(), NodeType::End | NodeType::Normal) {
            tail_node.incoming.iter().copied().collect()
        } else {
            Vec::new()
        }
    }

    fn is_removable(&self, reference_id: K) -> bool {
        self.state
            .edge(reference_id)
            .map(|e| e.content.can_be_removed())
            .unwrap_or(false)
    }
}

/// Node-space view for the shared ancestor calculation - the counterpart of the
/// C# `ArrowAncestorGraphView`. Internal, used by the default reducer.
pub(crate) struct ArrowAncestorView<'a, K: Key, R: Key, W: Key> {
    pub(crate) state: &'a ArrowGraphState<K, R, W>,
}

impl<K: Key, R: Key, W: Key> AncestorGraphView<K> for ArrowAncestorView<'_, K, R, W> {
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
