//! Shared helpers for the in-crate engine unit tests: constructing the
//! crate-private graph state directly, mirroring the C# test builders.

use crate::{ArrowGraphState, VertexGraphState};
use zametek_maths_graphs_primitives::{DependentActivity, Edge, Event, Node, NodeType};

pub(crate) type VState = VertexGraphState<i32, i32, i32>;
pub(crate) type AState = ArrowGraphState<i32, i32, i32>;

/// Adds a vertex node (activity-on-node) of the given type, duration and
/// removability.
pub(crate) fn vertex_node(
    state: &mut VState,
    id: i32,
    node_type: NodeType,
    duration: i32,
    removable: bool,
) {
    state.add_node(Node::with_type(
        node_type,
        DependentActivity::new_removable(id, duration, removable),
    ));
}

/// Connects `tail -> head` with an edge (event) of the given ID.
pub(crate) fn vertex_edge(state: &mut VState, edge_id: i32, tail: i32, head: i32) {
    state.node_mut(tail).unwrap().outgoing.insert(edge_id);
    state.node_mut(head).unwrap().incoming.insert(edge_id);
    state.add_edge(Edge::new(Event::new(edge_id)));
    state.set_edge_tail_node(edge_id, tail);
    state.set_edge_head_node(edge_id, head);
}

/// Connects `tail -> head` with a *removable* edge (event) of the given ID.
pub(crate) fn vertex_removable_edge(state: &mut VState, edge_id: i32, tail: i32, head: i32) {
    state.node_mut(tail).unwrap().outgoing.insert(edge_id);
    state.node_mut(head).unwrap().incoming.insert(edge_id);
    let mut event = Event::new(edge_id);
    event.set_as_removable();
    state.add_edge(Edge::new(event));
    state.set_edge_tail_node(edge_id, tail);
    state.set_edge_head_node(edge_id, head);
}

/// Adds an arrow node (event-on-node) of the given type.
pub(crate) fn arrow_node(state: &mut AState, id: i32, node_type: NodeType) {
    state.add_node(Node::with_type(node_type, Event::new(id)));
}

/// Connects `tail -> head` with an edge (activity) of the given ID, duration and
/// removability.
pub(crate) fn arrow_edge(
    state: &mut AState,
    edge_id: i32,
    tail: i32,
    head: i32,
    duration: i32,
    removable: bool,
) {
    state.node_mut(tail).unwrap().outgoing.insert(edge_id);
    state.node_mut(head).unwrap().incoming.insert(edge_id);
    state.add_edge(Edge::new(DependentActivity::new_removable(
        edge_id, duration, removable,
    )));
    state.set_edge_tail_node(edge_id, tail);
    state.set_edge_head_node(edge_id, head);
}
