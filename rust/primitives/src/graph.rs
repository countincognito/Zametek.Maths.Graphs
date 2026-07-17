use crate::edge::{Edge, HasId};
use crate::key::Key;
use crate::node::Node;

/// A raw directed-graph structure: the edges and nodes with their content
/// payloads, as exported by the graph builders. Equality compares the full
/// edge and node sets (order-insensitive), as in C#.
#[derive(Debug, Clone)]
pub struct Graph<K: Key, EC: HasId<K>, NC: HasId<K>> {
    pub edges: Vec<Edge<K, EC>>,
    pub nodes: Vec<Node<K, NC>>,
}

impl<K: Key, EC: HasId<K>, NC: HasId<K>> Graph<K, EC, NC> {
    /// Creates an empty graph.
    pub fn new() -> Self {
        Self {
            edges: Vec::new(),
            nodes: Vec::new(),
        }
    }

    /// Creates a graph from the given edges and nodes.
    pub fn with_content(edges: Vec<Edge<K, EC>>, nodes: Vec<Node<K, NC>>) -> Self {
        Self { edges, nodes }
    }
}

impl<K: Key, EC: HasId<K>, NC: HasId<K>> Default for Graph<K, EC, NC> {
    fn default() -> Self {
        Self::new()
    }
}

impl<K: Key, EC: HasId<K>, NC: HasId<K>> PartialEq for Graph<K, EC, NC> {
    fn eq(&self, other: &Self) -> bool {
        if self.edges.len() != other.edges.len() || self.nodes.len() != other.nodes.len() {
            return false;
        }
        let sorted_ids = |edges: &[Edge<K, EC>]| {
            let mut ids: Vec<K> = edges.iter().map(|e| e.id()).collect();
            ids.sort();
            ids
        };
        if sorted_ids(&self.edges) != sorted_ids(&other.edges) {
            return false;
        }
        let mut a: Vec<&Node<K, NC>> = self.nodes.iter().collect();
        let mut b: Vec<&Node<K, NC>> = other.nodes.iter().collect();
        a.sort_by_key(|n| n.id());
        b.sort_by_key(|n| n.id());
        a.iter().zip(b.iter()).all(|(x, y)| x == y)
    }
}

impl<K: Key, EC: HasId<K>, NC: HasId<K>> Eq for Graph<K, EC, NC> {}
