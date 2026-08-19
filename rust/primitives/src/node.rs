use crate::edge::HasId;
use crate::enums::NodeType;
use crate::error::GraphError;
use crate::insertion_order_set::InsertionOrderSet;
use crate::key::Key;

pub const MSG_CANNOT_REQUEST_INCOMING_EDGES: &str =
    "Cannot request Incoming Edges of a Start or Isolated Node";
pub const MSG_CANNOT_REQUEST_OUTGOING_EDGES: &str =
    "Cannot request Outgoing Edges of an End or Isolated Node";

/// A directed-graph node carrying a content payload (an event in arrow graphs;
/// an activity in vertex graphs) - the counterpart of the C# `Node<T, TContent>`.
///
/// The C# `IncomingEdges`/`OutgoingEdges` properties throw for node types that
/// cannot have them; the checked accessors [`Node::incoming_edges`] and
/// [`Node::outgoing_edges`] reproduce that contract. The raw sets are also
/// exposed (`incoming`/`outgoing`) for algorithm code that has already
/// established the node type, as the C# internals do.
///
/// The edge sets iterate in insertion order, which the golden tests encode, but
/// remove in O(1) - see [`InsertionOrderSet`] for why that matters here.
#[derive(Debug, Clone)]
pub struct Node<K: Key, C> {
    node_type: NodeType,
    pub content: C,
    pub incoming: InsertionOrderSet<K>,
    pub outgoing: InsertionOrderSet<K>,
}

impl<K: Key, C: HasId<K>> Node<K, C> {
    /// Creates a normal node carrying the given content.
    pub fn new(content: C) -> Self {
        Self::with_type(NodeType::Normal, content)
    }

    /// Creates a node of the given type carrying the given content.
    pub fn with_type(node_type: NodeType, content: C) -> Self {
        Self {
            node_type,
            content,
            incoming: InsertionOrderSet::new(),
            outgoing: InsertionOrderSet::new(),
        }
    }

    pub fn id(&self) -> K {
        self.content.id()
    }

    /// The position of the node within the graph.
    pub fn node_type(&self) -> NodeType {
        self.node_type
    }

    /// Changes the position classification of the node.
    pub fn set_node_type(&mut self, node_type: NodeType) {
        self.node_type = node_type;
    }

    /// The IDs of the edges pointing into this node (invalid for Start and Isolated nodes).
    pub fn incoming_edges(&self) -> Result<&InsertionOrderSet<K>, GraphError> {
        if matches!(self.node_type, NodeType::Start | NodeType::Isolated) {
            return Err(GraphError::new(MSG_CANNOT_REQUEST_INCOMING_EDGES));
        }
        Ok(&self.incoming)
    }

    /// The IDs of the edges leaving this node (invalid for End and Isolated nodes).
    pub fn outgoing_edges(&self) -> Result<&InsertionOrderSet<K>, GraphError> {
        if matches!(self.node_type, NodeType::End | NodeType::Isolated) {
            return Err(GraphError::new(MSG_CANNOT_REQUEST_OUTGOING_EDGES));
        }
        Ok(&self.outgoing)
    }
}

impl<K: Key, C: HasId<K>> PartialEq for Node<K, C> {
    /// Equality by ID, node type and edge sets (order-insensitive), as in C#.
    fn eq(&self, other: &Self) -> bool {
        if self.id() != other.id() || self.node_type != other.node_type {
            return false;
        }
        let mut a: Vec<K> = self.incoming.iter().copied().collect();
        let mut b: Vec<K> = other.incoming.iter().copied().collect();
        a.sort();
        b.sort();
        if a != b {
            return false;
        }
        let mut a: Vec<K> = self.outgoing.iter().copied().collect();
        let mut b: Vec<K> = other.outgoing.iter().copied().collect();
        a.sort();
        b.sort();
        a == b
    }
}

impl<K: Key, C: HasId<K>> Eq for Node<K, C> {}
