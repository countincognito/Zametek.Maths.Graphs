//! Ports of `VertexGraphBuilderTests.cs`. The two null-argument constructor
//! tests (`NullEdgeIdGenerator`, `NullGraph`) have no Rust counterpart - the
//! generator is a typed value and `from_graph` takes an owned graph - and are
//! omitted. `ShuffleProcessingOrder` is set to mirror the C# tests; it only
//! influences the CPM processing order, never the deterministic edge-ID
//! assignment these structural tests assert on.

use indexmap::IndexSet;
use zametek_maths_graphs_compilers::{NextIdGenerator, VertexGraphBuilder};
use zametek_maths_graphs_primitives::{DependentActivity, Edge, Event, Node, NodeType};

type Builder = VertexGraphBuilder<i32, i32, i32>;
type Act = DependentActivity<i32, i32, i32>;

fn new_builder() -> Builder {
    let mut builder = VertexGraphBuilder::<i32, i32, i32>::new(NextIdGenerator::new(0));
    builder.shuffle_processing_order = true;
    builder
}

/// The 5-activity network shared by the constructor-from-graph tests: 1 and 2
/// are roots, 3 depends on 1 and 4, 4 on 2, 5 on 1. Node 4 is the only Normal
/// node; 1 and 2 are Start; 3 and 5 are End.
fn build_standard_network() -> Builder {
    let mut builder = new_builder();
    builder.add_activity(Act::new(1, 0));
    builder.add_activity(Act::new(2, 0));
    builder.add_activity_with_dependencies(Act::new(3, 0), IndexSet::from([1, 4]));
    builder.add_activity_with_dependencies(Act::new(4, 0), IndexSet::from([2]));
    builder.add_activity_with_dependencies(Act::new(5, 0), IndexSet::from([1]));
    builder
}

#[test]
fn constructor_then_no_exception() {
    let builder = new_builder();

    assert!(builder.edge_ids().is_empty());
    assert!(builder.node_ids().is_empty());
    assert!(builder.all_dependencies_satisfied());
    assert!(builder.start_nodes().is_empty());
    assert!(builder.end_nodes().is_empty());
}

#[test]
fn single_activity_no_dependencies_then_no_start_or_end_nodes() {
    let mut builder = new_builder();

    assert!(builder.add_activity(Act::new(1, 0)));

    assert!(builder.edge_ids().is_empty());
    assert_eq!(builder.node_ids().len(), 1);
    assert!(builder.all_dependencies_satisfied());
    assert!(builder.start_nodes().is_empty());
    assert!(builder.end_nodes().is_empty());

    assert_eq!(builder.node(1).unwrap().id(), 1);
    assert_eq!(builder.node(1).unwrap().node_type(), NodeType::Isolated);
    assert_eq!(builder.activity(1).unwrap().id(), 1);
    assert_eq!(builder.activities().count(), 1);
    assert_eq!(builder.edges().count(), 0);
}

#[test]
fn two_activities_one_dependency_then_activities_hooked_up_by_edge() {
    let event_id1 = 1;
    let mut builder = new_builder();

    assert!(builder.add_activity(Act::new(1, 0)));

    assert!(builder.edge_ids().is_empty());
    assert_eq!(builder.node_ids().len(), 1);
    assert!(builder.all_dependencies_satisfied());
    assert!(builder.start_nodes().is_empty());
    assert!(builder.end_nodes().is_empty());
    assert_eq!(builder.node(1).unwrap().node_type(), NodeType::Isolated);
    assert_eq!(builder.activities().count(), 1);
    assert_eq!(builder.edges().count(), 0);

    assert!(builder.add_activity_with_dependencies(Act::new(2, 0), IndexSet::from([1])));

    assert_eq!(builder.edge_ids().len(), 1);
    assert_eq!(builder.node_ids().len(), 2);
    assert!(builder.all_dependencies_satisfied());
    assert_eq!(builder.start_nodes().len(), 1);
    assert_eq!(builder.end_nodes().len(), 1);

    // First activity.
    assert_eq!(builder.start_nodes()[0].id(), 1);
    assert_eq!(builder.edge_tail_node(event_id1).unwrap().id(), 1);
    assert_eq!(builder.start_nodes()[0].outgoing.len(), 1);
    assert!(builder.start_nodes()[0].outgoing.contains(&event_id1));
    assert_eq!(builder.edge_tail_node(event_id1).unwrap().outgoing.len(), 1);
    assert!(builder
        .edge_tail_node(event_id1)
        .unwrap()
        .outgoing
        .contains(&event_id1));

    // Second activity.
    assert_eq!(builder.end_nodes()[0].id(), 2);
    assert_eq!(builder.edge_head_node(event_id1).unwrap().id(), 2);
    assert_eq!(builder.end_nodes()[0].incoming.len(), 1);
    assert!(builder.end_nodes()[0].incoming.contains(&event_id1));
    assert_eq!(builder.edge_head_node(event_id1).unwrap().incoming.len(), 1);
    assert!(builder
        .edge_head_node(event_id1)
        .unwrap()
        .incoming
        .contains(&event_id1));
}

#[test]
fn two_activities_one_dependency_reverse_order_then_activities_hooked_up_by_edge() {
    let event_id1 = 1;
    let mut builder = new_builder();

    assert!(builder.add_activity_with_dependencies(Act::new(2, 0), IndexSet::from([1])));

    assert!(builder.edge_ids().is_empty());
    assert_eq!(builder.node_ids().len(), 1);
    assert!(!builder.all_dependencies_satisfied());
    assert!(builder.start_nodes().is_empty());
    assert_eq!(builder.end_nodes().len(), 1);
    assert_eq!(builder.end_nodes()[0].id(), 2);
    assert!(builder.end_nodes()[0].incoming.is_empty());

    assert!(builder.add_activity(Act::new(1, 0)));

    assert_eq!(builder.edge_ids().len(), 1);
    assert_eq!(builder.node_ids().len(), 2);
    assert!(builder.all_dependencies_satisfied());
    assert_eq!(builder.start_nodes().len(), 1);
    assert_eq!(builder.end_nodes().len(), 1);

    // First activity.
    assert_eq!(builder.start_nodes()[0].id(), 1);
    assert_eq!(builder.edge_tail_node(event_id1).unwrap().id(), 1);
    assert_eq!(builder.start_nodes()[0].outgoing.len(), 1);
    assert!(builder.start_nodes()[0].outgoing.contains(&event_id1));

    // Second activity.
    assert_eq!(builder.end_nodes()[0].id(), 2);
    assert_eq!(builder.edge_head_node(event_id1).unwrap().id(), 2);
    assert_eq!(builder.end_nodes()[0].incoming.len(), 1);
    assert!(builder.end_nodes()[0].incoming.contains(&event_id1));
}

#[test]
fn three_activities_one_dependent_on_other_two_then_hooked_up_by_two_edges() {
    let event_id1 = 1;
    let event_id2 = 2;
    let mut builder = new_builder();

    assert!(builder.add_activity(Act::new(1, 0)));
    assert!(builder.add_activity(Act::new(2, 0)));

    assert!(builder.edge_ids().is_empty());
    assert_eq!(builder.node_ids().len(), 2);
    assert_eq!(builder.activities().count(), 2);
    assert_eq!(builder.events().count(), 0);
    assert_eq!(builder.edges().count(), 0);

    assert!(builder.add_activity_with_dependencies(Act::new(3, 0), IndexSet::from([1, 2])));

    assert_eq!(builder.edge_ids().len(), 2);
    assert_eq!(builder.node_ids().len(), 3);
    assert!(builder.all_dependencies_satisfied());
    assert_eq!(builder.start_nodes().len(), 2);
    assert_eq!(builder.end_nodes().len(), 1);

    assert_eq!(builder.node(3).unwrap().node_type(), NodeType::End);
    assert_eq!(builder.activities().count(), 3);
    assert_eq!(builder.nodes().count(), 3);
    assert_eq!(builder.events().count(), 2);
    assert_eq!(builder.edges().count(), 2);

    // First activity.
    assert_eq!(builder.node(1).unwrap().node_type(), NodeType::Start);
    assert_eq!(builder.edge_tail_node(event_id1).unwrap().id(), 1);
    assert_eq!(builder.edge_tail_node(event_id1).unwrap().outgoing.len(), 1);
    assert!(builder
        .edge_tail_node(event_id1)
        .unwrap()
        .outgoing
        .contains(&event_id1));

    // Second activity.
    assert_eq!(builder.node(2).unwrap().node_type(), NodeType::Start);
    assert_eq!(builder.edge_tail_node(event_id2).unwrap().id(), 2);
    assert_eq!(builder.edge_tail_node(event_id2).unwrap().outgoing.len(), 1);
    assert!(builder
        .edge_tail_node(event_id2)
        .unwrap()
        .outgoing
        .contains(&event_id2));

    // Third activity.
    assert_eq!(builder.edge_head_node(event_id1).unwrap().id(), 3);
    assert_eq!(builder.edge_head_node(event_id2).unwrap().id(), 3);
    assert_eq!(builder.edge_head_node(event_id1).unwrap().incoming.len(), 2);
    assert!(builder
        .edge_head_node(event_id1)
        .unwrap()
        .incoming
        .contains(&event_id1));
    assert!(builder
        .edge_head_node(event_id2)
        .unwrap()
        .incoming
        .contains(&event_id2));
}

#[test]
fn three_activities_one_dependent_on_other_two_reverse_order_then_hooked_up_by_two_edges() {
    let event_id1 = 1;
    let event_id2 = 2;
    let mut builder = new_builder();

    // The dependent activity is added first, before either of its dependencies.
    assert!(builder.add_activity_with_dependencies(Act::new(3, 0), IndexSet::from([1, 2])));

    assert_eq!(builder.edge_ids().len(), 0);
    assert_eq!(builder.node_ids().len(), 1);
    assert!(!builder.all_dependencies_satisfied());
    assert!(builder.start_nodes().is_empty());
    assert_eq!(builder.end_nodes().len(), 1);
    assert_eq!(builder.node(3).unwrap().node_type(), NodeType::End);
    assert_eq!(builder.activities().count(), 1);
    assert_eq!(builder.events().count(), 0);
    assert_eq!(builder.edges().count(), 0);

    assert!(builder.add_activity(Act::new(2, 0)));

    assert_eq!(builder.edge_ids().len(), 1);
    assert_eq!(builder.node_ids().len(), 2);
    assert!(!builder.all_dependencies_satisfied());
    assert_eq!(builder.start_nodes().len(), 1);
    assert_eq!(builder.end_nodes().len(), 1);
    assert_eq!(builder.node(2).unwrap().node_type(), NodeType::Start);
    assert_eq!(builder.events().count(), 1);
    assert_eq!(builder.edges().count(), 1);

    assert!(builder.add_activity(Act::new(1, 0)));

    assert_eq!(builder.edge_ids().len(), 2);
    assert_eq!(builder.node_ids().len(), 3);
    assert!(builder.all_dependencies_satisfied());
    assert_eq!(builder.start_nodes().len(), 2);
    assert_eq!(builder.end_nodes().len(), 1);
    assert_eq!(builder.activities().count(), 3);
    assert_eq!(builder.events().count(), 2);
    assert_eq!(builder.edges().count(), 2);

    // First activity (the second edge, minted when activity 1 was added).
    assert_eq!(builder.node(1).unwrap().node_type(), NodeType::Start);
    assert_eq!(builder.edge_tail_node(event_id2).unwrap().id(), 1);
    assert_eq!(builder.edge_tail_node(event_id2).unwrap().outgoing.len(), 1);
    assert!(builder
        .edge_tail_node(event_id2)
        .unwrap()
        .outgoing
        .contains(&event_id2));

    // Second activity (the first edge, minted when activity 2 was added).
    assert_eq!(builder.node(2).unwrap().node_type(), NodeType::Start);
    assert_eq!(builder.edge_tail_node(event_id1).unwrap().id(), 2);
    assert_eq!(builder.edge_tail_node(event_id1).unwrap().outgoing.len(), 1);
    assert!(builder
        .edge_tail_node(event_id1)
        .unwrap()
        .outgoing
        .contains(&event_id1));

    // Third activity.
    assert_eq!(builder.node(3).unwrap().node_type(), NodeType::End);
    assert_eq!(builder.edge_head_node(event_id1).unwrap().id(), 3);
    assert_eq!(builder.edge_head_node(event_id2).unwrap().id(), 3);
    assert_eq!(builder.edge_head_node(event_id1).unwrap().incoming.len(), 2);
    assert!(builder
        .edge_head_node(event_id1)
        .unwrap()
        .incoming
        .contains(&event_id1));
    assert!(builder
        .edge_head_node(event_id2)
        .unwrap()
        .incoming
        .contains(&event_id2));
}

#[test]
fn three_activities_removed_in_stages_then_structure_as_expected() {
    let event_id1 = 1;
    let event_id2 = 2;
    let mut builder = new_builder();

    builder.add_activity(Act::new(1, 0));
    builder.add_activity(Act::new(2, 0));
    builder.add_activity_with_dependencies(Act::new(3, 0), IndexSet::from([1, 2]));

    assert_eq!(builder.activities().count(), 3);
    assert_eq!(builder.nodes().count(), 3);
    assert_eq!(builder.events().count(), 2);
    assert_eq!(builder.edges().count(), 2);

    // Remove activity 3 (only succeeds once it is marked removable).
    assert!(!builder.remove_activity(3));
    builder.activity_mut(3).unwrap().set_as_removable();
    assert!(builder.remove_activity(3));

    assert_eq!(builder.activities().count(), 2);
    assert_eq!(builder.nodes().count(), 2);
    assert_eq!(builder.events().count(), 0);
    assert_eq!(builder.edges().count(), 0);
    assert!(builder.all_dependencies_satisfied());
    assert_eq!(builder.node(1).unwrap().node_type(), NodeType::Isolated);
    assert!(builder.edge_tail_node(event_id1).is_none());
    assert_eq!(builder.node(2).unwrap().node_type(), NodeType::Isolated);
    assert!(builder.edge_tail_node(event_id2).is_none());
    assert!(!builder.edge_ids().contains(&3));

    // Remove activity 2.
    assert!(!builder.remove_activity(2));
    builder.activity_mut(2).unwrap().set_as_removable();
    assert!(builder.remove_activity(2));

    assert_eq!(builder.activities().count(), 1);
    assert_eq!(builder.nodes().count(), 1);
    assert!(builder.all_dependencies_satisfied());
    assert_eq!(builder.node(1).unwrap().node_type(), NodeType::Isolated);

    // Remove activity 1.
    assert!(!builder.remove_activity(1));
    builder.activity_mut(1).unwrap().set_as_removable();
    assert!(builder.remove_activity(1));

    assert_eq!(builder.activities().count(), 0);
    assert_eq!(builder.nodes().count(), 0);
    assert_eq!(builder.events().count(), 0);
    assert_eq!(builder.edges().count(), 0);
    assert!(builder.all_dependencies_satisfied());
}

#[test]
fn four_activities_get_ancestor_nodes_lookup_then_ancestors_as_expected() {
    let mut builder = new_builder();
    builder.add_activity(Act::new(1, 0));
    builder.add_activity(Act::new(2, 0));
    builder.add_activity_with_dependencies(Act::new(3, 0), IndexSet::from([2]));
    builder.add_activity_with_dependencies(Act::new(4, 0), IndexSet::from([1, 2, 3]));

    let lookup = builder.get_ancestor_nodes_lookup().unwrap();

    assert_eq!(lookup[&1].len(), 0);
    assert_eq!(lookup[&2].len(), 0);

    assert_eq!(lookup[&3].len(), 1);
    assert!(lookup[&3].contains(&2));

    assert_eq!(lookup[&4].len(), 3);
    assert!(lookup[&4].contains(&1));
    assert!(lookup[&4].contains(&2));
    assert!(lookup[&4].contains(&3));
}

#[test]
fn five_activities_with_two_unnecessary_dependencies_then_transitive_reduction_as_expected() {
    let mut builder = new_builder();
    builder.add_activity(Act::new(1, 0));
    builder.add_activity_with_dependencies(Act::new(2, 0), IndexSet::from([5]));
    builder.add_activity_with_dependencies(Act::new(3, 0), IndexSet::from([1, 2, 5]));
    builder.add_activity_with_dependencies(Act::new(4, 0), IndexSet::from([1, 2, 3]));
    builder.add_activity(Act::new(5, 0));

    assert_eq!(builder.edge_ids().len(), 7);
    assert_eq!(builder.node_ids().len(), 5);

    // Transitive reduction removes the two redundant edges (1->3 and 2->4).
    assert!(builder.transitive_reduction());

    assert_eq!(builder.edge_ids().len(), 4);
    assert_eq!(builder.node_ids().len(), 5);
    assert!(builder.all_dependencies_satisfied());

    assert_eq!(builder.node(1).unwrap().node_type(), NodeType::Start);
    assert_eq!(builder.node(2).unwrap().node_type(), NodeType::Normal);
    assert_eq!(builder.node(3).unwrap().node_type(), NodeType::Normal);
    assert_eq!(builder.node(4).unwrap().node_type(), NodeType::End);
    assert_eq!(builder.node(5).unwrap().node_type(), NodeType::Start);

    // Each remaining node keeps exactly the edges of the reduced network.
    assert_eq!(builder.node(1).unwrap().outgoing.len(), 1);
    assert_eq!(builder.node(5).unwrap().outgoing.len(), 1);
    assert_eq!(builder.node(4).unwrap().incoming.len(), 1);
}

#[test]
fn ctor_with_graph_then_graph_successfully_assimilated() {
    let mut builder = build_standard_network();
    let first_graph = builder.to_graph().unwrap();

    let mut builder2 = VertexGraphBuilder::from_graph(first_graph.clone(), NextIdGenerator::new(0))
        .expect("assimilation should succeed");
    let second_graph = builder2.to_graph().unwrap();

    assert_eq!(second_graph, first_graph);
}

#[test]
fn ctor_with_graph_with_missing_edge_then_error() {
    let mut builder = build_standard_network();
    let mut graph = builder.to_graph().unwrap();

    graph.edges.remove(0);

    assert!(VertexGraphBuilder::from_graph(graph, NextIdGenerator::new(0)).is_err());
}

#[test]
fn ctor_with_graph_with_too_many_edges_then_error() {
    let mut builder = build_standard_network();
    let mut graph = builder.to_graph().unwrap();

    // A fresh event ID (no node references it). C# reuses `eventId.Next()` which
    // collides; a non-colliding ID forces the same "edges do not match" error
    // without depending on how duplicate IDs are folded.
    graph.edges.push(Edge::new(Event::new(100)));

    assert!(VertexGraphBuilder::from_graph(graph, NextIdGenerator::new(0)).is_err());
}

#[test]
fn ctor_with_graph_with_missing_node_then_error() {
    let mut builder = build_standard_network();
    let mut graph = builder.to_graph().unwrap();

    let normal_id = graph
        .nodes
        .iter()
        .find(|n| n.node_type() == NodeType::Normal)
        .unwrap()
        .id();
    graph.nodes.retain(|n| n.id() != normal_id);

    assert!(VertexGraphBuilder::from_graph(graph, NextIdGenerator::new(0)).is_err());
}

#[test]
fn ctor_with_graph_with_too_many_nodes_then_error() {
    let mut builder = build_standard_network();
    let mut graph = builder.to_graph().unwrap();

    // A fresh Normal node with no edges: its ID appears among non-isolated nodes
    // but never among the edge endpoints, so assimilation rejects it.
    graph.nodes.push(Node::new(Act::new(100, 0)));

    assert!(VertexGraphBuilder::from_graph(graph, NextIdGenerator::new(0)).is_err());
}

#[test]
fn ctor_with_graph_with_no_start_node_then_error() {
    let mut builder = build_standard_network();
    let mut graph = builder.to_graph().unwrap();

    for node in &mut graph.nodes {
        if node.node_type() == NodeType::Start {
            node.set_node_type(NodeType::Normal);
        }
    }

    assert!(VertexGraphBuilder::from_graph(graph, NextIdGenerator::new(0)).is_err());
}

#[test]
fn ctor_with_graph_with_no_end_node_then_error() {
    let mut builder = build_standard_network();
    let mut graph = builder.to_graph().unwrap();

    for node in &mut graph.nodes {
        if node.node_type() == NodeType::End {
            node.set_node_type(NodeType::Normal);
        }
    }

    assert!(VertexGraphBuilder::from_graph(graph, NextIdGenerator::new(0)).is_err());
}

#[test]
fn ctor_with_graph_with_only_isolated_nodes_then_no_exception() {
    let mut builder = new_builder();
    builder.add_activity(Act::new(1, 0));
    builder.add_activity(Act::new(2, 0));
    let graph = builder.to_graph().unwrap();

    let builder2 = VertexGraphBuilder::from_graph(graph, NextIdGenerator::new(0)).unwrap();

    assert!(builder2.edge_ids().is_empty());
    assert_eq!(builder2.node_ids().len(), 2);
    assert!(builder2.all_dependencies_satisfied());
    assert!(builder2.start_nodes().is_empty());
    assert!(builder2.end_nodes().is_empty());
    assert!(builder2.normal_nodes().is_empty());
    assert_eq!(builder2.isolated_nodes().len(), 2);
}

#[test]
fn ctor_with_graph_with_unconnected_start_node_then_error() {
    let mut builder = build_standard_network();
    let mut graph = builder.to_graph().unwrap();

    let node = graph
        .nodes
        .iter()
        .find(|n| n.node_type() == NodeType::Normal)
        .cloned()
        .unwrap();
    graph.nodes.retain(|n| n.id() != node.id());

    let mut new_node = Node::with_type(NodeType::Start, node.content.clone());
    for edge_id in &node.outgoing {
        new_node.outgoing.insert(*edge_id);
    }
    graph.nodes.push(new_node);

    assert!(VertexGraphBuilder::from_graph(graph, NextIdGenerator::new(0)).is_err());
}

#[test]
fn ctor_with_graph_with_unconnected_end_node_then_error() {
    let mut builder = build_standard_network();
    let mut graph = builder.to_graph().unwrap();

    let node = graph
        .nodes
        .iter()
        .find(|n| n.node_type() == NodeType::Normal)
        .cloned()
        .unwrap();
    graph.nodes.retain(|n| n.id() != node.id());

    let mut new_node = Node::with_type(NodeType::End, node.content.clone());
    for edge_id in &node.incoming {
        new_node.incoming.insert(*edge_id);
    }
    graph.nodes.push(new_node);

    assert!(VertexGraphBuilder::from_graph(graph, NextIdGenerator::new(0)).is_err());
}

#[test]
fn all_dummy_activities_find_circular_dependencies_then_finds_circular_dependency() {
    let mut builder = VertexGraphBuilder::<i32, i32, i32>::new(NextIdGenerator::new(0));
    builder.add_activity(Act::new(1, 0));
    builder.add_activity_with_dependencies(Act::new(2, 0), IndexSet::from([7]));
    builder.add_activity(Act::new(3, 0));
    builder.add_activity_with_dependencies(Act::new(4, 0), IndexSet::from([2]));
    builder.add_activity_with_dependencies(Act::new(5, 0), IndexSet::from([1, 2, 3, 8]));
    builder.add_activity_with_dependencies(Act::new(6, 0), IndexSet::from([3]));
    builder.add_activity_with_dependencies(Act::new(7, 0), IndexSet::from([4]));
    builder.add_activity_with_dependencies(Act::new(8, 0), IndexSet::from([9, 6]));
    builder.add_activity_with_dependencies(Act::new(9, 0), IndexSet::from([5]));

    assert_circular_dependencies(&builder);
}

#[test]
fn find_circular_dependencies_then_finds_circular_dependency() {
    let mut builder = VertexGraphBuilder::<i32, i32, i32>::new(NextIdGenerator::new(0));
    builder.add_activity(Act::new(1, 10));
    builder.add_activity_with_dependencies(Act::new(2, 10), IndexSet::from([7]));
    builder.add_activity(Act::new(3, 10));
    builder.add_activity_with_dependencies(Act::new(4, 10), IndexSet::from([2]));
    builder.add_activity_with_dependencies(Act::new(5, 10), IndexSet::from([1, 2, 3, 8]));
    builder.add_activity_with_dependencies(Act::new(6, 10), IndexSet::from([3]));
    builder.add_activity_with_dependencies(Act::new(7, 10), IndexSet::from([4]));
    builder.add_activity_with_dependencies(Act::new(8, 10), IndexSet::from([9, 6]));
    builder.add_activity_with_dependencies(Act::new(9, 10), IndexSet::from([5]));

    assert_circular_dependencies(&builder);
}

/// Both circular-dependency tests expect the same two cycles, `{2, 4, 7}` and
/// `{5, 8, 9}`, order-independent within and between cycles.
fn assert_circular_dependencies(builder: &Builder) {
    let circular = builder.find_strong_circular_dependencies();
    assert_eq!(circular.len(), 2);

    let mut cycles: Vec<Vec<i32>> = circular
        .iter()
        .map(|c| {
            let mut ids: Vec<i32> = c.dependencies.to_vec();
            ids.sort();
            ids
        })
        .collect();
    cycles.sort();

    assert_eq!(cycles, vec![vec![2, 4, 7], vec![5, 8, 9]]);
}
