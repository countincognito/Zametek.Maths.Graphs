//! Ports of `.../Entities/NodeTests.cs`. The null-content constructor guard has
//! no Rust counterpart and is omitted; the C# `IncomingEdges`/`OutgoingEdges`
//! throwing accessors map to the `Result`-returning `incoming_edges` /
//! `outgoing_edges`.

use indexmap::IndexSet;
use zametek_maths_graphs_primitives::{Event, Node, NodeType};

type Nd = Node<i32, Event<i32>>;

#[test]
fn node_given_ctor_with_content_only_then_node_type_is_normal() {
    let node = Nd::new(Event::new(1));
    assert_eq!(node.node_type(), NodeType::Normal);
    assert_eq!(node.id(), 1);
}

#[test]
fn node_given_incoming_edges_when_start_node_then_err() {
    let node = Nd::with_type(NodeType::Start, Event::new(1));
    assert!(node.incoming_edges().is_err());
}

#[test]
fn node_given_incoming_edges_when_isolated_node_then_err() {
    let node = Nd::with_type(NodeType::Isolated, Event::new(1));
    assert!(node.incoming_edges().is_err());
}

#[test]
fn node_given_outgoing_edges_when_end_node_then_err() {
    let node = Nd::with_type(NodeType::End, Event::new(1));
    assert!(node.outgoing_edges().is_err());
}

#[test]
fn node_given_outgoing_edges_when_isolated_node_then_err() {
    let node = Nd::with_type(NodeType::Isolated, Event::new(1));
    assert!(node.outgoing_edges().is_err());
}

#[test]
fn node_given_set_node_type_then_node_type_changes() {
    let mut node = Nd::with_type(NodeType::Start, Event::new(1));
    node.set_node_type(NodeType::Normal);
    assert_eq!(node.node_type(), NodeType::Normal);
}

#[test]
fn node_given_equals_when_same_id_type_and_edges_then_equal() {
    let mut node1 = Nd::new(Event::new(1));
    node1.incoming.insert(10);
    node1.outgoing.insert(20);
    let mut node2 = Nd::new(Event::new(1));
    node2.incoming.insert(10);
    node2.outgoing.insert(20);
    assert_eq!(node1, node2);
}

#[test]
fn node_given_equals_when_different_node_type_then_not_equal() {
    let node1 = Nd::with_type(NodeType::Normal, Event::new(1));
    let node2 = Nd::with_type(NodeType::End, Event::new(1));
    assert_ne!(node1, node2);
}

#[test]
fn node_given_equals_when_different_edges_then_not_equal() {
    let mut node1 = Nd::new(Event::new(1));
    node1.incoming.insert(10);
    let mut node2 = Nd::new(Event::new(1));
    node2.incoming.insert(11);
    assert_ne!(node1, node2);
}

#[test]
fn node_given_clone_then_type_content_and_edges_preserved() {
    let mut node = Nd::with_type(NodeType::Normal, Event::with_times(1, Some(2), Some(3)));
    node.incoming.insert(10);
    node.outgoing.insert(20);

    let clone = node.clone();

    assert_eq!(clone.node_type(), NodeType::Normal);
    assert_eq!(clone.content.earliest_finish_time, Some(2));
    assert_eq!(clone.incoming, IndexSet::from([10]));
    assert_eq!(clone.outgoing, IndexSet::from([20]));
}

#[test]
fn node_given_clone_then_edge_sets_are_independent_copies() {
    let mut node = Nd::new(Event::new(1));
    node.incoming.insert(10);

    let mut clone = node.clone();
    clone.incoming.insert(11);

    assert_eq!(node.incoming.len(), 1);
    assert_eq!(clone.incoming.len(), 2);
}
