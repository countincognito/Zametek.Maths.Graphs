//! Ports of `.../Entities/GraphTests.cs`. The null-argument constructor guards
//! have no Rust counterpart (the arguments are non-nullable by type) and are
//! omitted.

use zametek_maths_graphs_primitives::{Activity, Edge, Event, Graph, Node, NodeType};

type G = Graph<i32, Event<i32>, Activity<i32, i32, i32>>;

fn build_simple_graph() -> G {
    let edge = Edge::new(Event::new(10));
    let mut node1 = Node::with_type(NodeType::Start, Activity::<i32, i32, i32>::new(1, 0));
    node1.outgoing.insert(10);
    let mut node2 = Node::with_type(NodeType::End, Activity::<i32, i32, i32>::new(2, 0));
    node2.incoming.insert(10);
    Graph::with_content(vec![edge], vec![node1, node2])
}

#[test]
fn graph_given_default_then_empty() {
    let graph = G::new();
    assert!(graph.edges.is_empty());
    assert!(graph.nodes.is_empty());
}

#[test]
fn graph_given_equals_when_same_structure_then_equal() {
    assert_eq!(build_simple_graph(), build_simple_graph());
}

#[test]
fn graph_given_equals_when_different_structure_then_not_equal() {
    assert_ne!(build_simple_graph(), G::new());
}
