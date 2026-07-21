//! Ports of `ArrowGraphBuilderTests.cs` (plus two compiler-level smoke tests).
//! ID expectations are copied verbatim from the C# tests (same generators,
//! same seeds), so the graph structure matches edge for edge. The three C#
//! null-argument constructor tests have no Rust counterpart (typed,
//! non-nullable arguments) and are omitted.

use indexmap::IndexSet;
use zametek_maths_graphs_compilers::{ArrowGraphBuilder, ArrowGraphCompiler, NextIdGenerator};
use zametek_maths_graphs_primitives::{DependentActivity, Edge, Event, Node, NodeType};

type Builder = ArrowGraphBuilder<i32, i32, i32>;
type Act = DependentActivity<i32, i32, i32>;

fn new_builder(dummy_activity_id: i32, event_id: i32) -> Builder {
    Builder::new(
        NextIdGenerator::new(dummy_activity_id),
        NextIdGenerator::new(event_id),
    )
}

#[test]
fn given_ctor_then_no_exception() {
    let builder = new_builder(0, 0);

    assert!(builder.edge_ids().is_empty());
    assert_eq!(builder.node_ids().len(), 2);
    assert!(builder.all_dependencies_satisfied());
    assert_eq!(builder.start_node().outgoing.len(), 0);
    assert_eq!(builder.end_node().incoming.len(), 0);
}

#[test]
fn given_access_outgoing_edges_of_end_node_then_error() {
    let builder = new_builder(0, 0);
    assert!(builder.end_node().outgoing_edges().is_err());
    assert!(builder.start_node().incoming_edges().is_err());
}

#[test]
fn given_single_activity_no_dependencies_then_hooks_up_to_start_and_end_nodes() {
    let activity_id = 1;
    let dummy_activity_id = activity_id;
    let dummy_activity_id1 = dummy_activity_id + 1;
    let mut builder = new_builder(dummy_activity_id, 0);
    builder.shuffle_processing_order = true;

    assert!(builder.add_activity(Act::new(activity_id, 0)));

    assert_eq!(builder.edge_ids().len(), 2);
    assert_eq!(builder.node_ids().len(), 3);
    assert!(builder.all_dependencies_satisfied());
    assert_eq!(
        builder.edge_tail_node(activity_id).unwrap().id(),
        builder.start_node().id()
    );
    assert_eq!(
        builder.edge_tail_node(dummy_activity_id1).unwrap().id(),
        builder.edge_head_node(activity_id).unwrap().id()
    );
    assert_eq!(
        builder.edge_head_node(dummy_activity_id1).unwrap().id(),
        builder.end_node().id()
    );
    assert_eq!(builder.start_node().outgoing.len(), 1);
    assert!(builder.start_node().outgoing.contains(&activity_id));
    assert_eq!(builder.end_node().incoming.len(), 1);
    assert!(builder.end_node().incoming.contains(&dummy_activity_id1));
}

#[test]
fn given_two_activities_one_dependency_then_activities_hooked_up_by_dummy_edge() {
    let activity_id1 = 1;
    let activity_id2 = activity_id1 + 1;
    let dummy_activity_id = activity_id2 + 1;
    let dummy_activity_id1 = dummy_activity_id + 1;
    let dummy_activity_id2 = dummy_activity_id1 + 1;
    let dummy_activity_id3 = dummy_activity_id2 + 1;
    let mut builder = new_builder(dummy_activity_id, 0);
    builder.shuffle_processing_order = true;

    assert!(builder.add_activity(Act::new(activity_id1, 0)));

    assert_eq!(builder.edge_ids().len(), 2);
    assert_eq!(builder.node_ids().len(), 3);
    assert!(builder.all_dependencies_satisfied());
    assert_eq!(
        builder.edge_tail_node(activity_id1).unwrap().id(),
        builder.start_node().id()
    );
    assert_eq!(
        builder.edge_tail_node(dummy_activity_id1).unwrap().id(),
        builder.edge_head_node(activity_id1).unwrap().id()
    );
    assert_eq!(
        builder.edge_head_node(dummy_activity_id1).unwrap().id(),
        builder.end_node().id()
    );

    assert!(builder
        .add_activity_with_dependencies(Act::new(activity_id2, 0), IndexSet::from([activity_id1])));

    assert_eq!(builder.edge_ids().len(), 5);
    assert_eq!(builder.node_ids().len(), 5);
    assert!(builder.all_dependencies_satisfied());

    // First activity.
    assert_eq!(
        builder.edge_tail_node(activity_id1).unwrap().id(),
        builder.start_node().id()
    );
    assert_eq!(builder.start_node().outgoing.len(), 1);
    assert!(builder.start_node().outgoing.contains(&activity_id1));

    let head1 = builder.edge_head_node(activity_id1).unwrap();
    assert_eq!(head1.incoming.len(), 1);
    assert!(head1.incoming.contains(&activity_id1));
    assert_eq!(head1.outgoing.len(), 2);
    assert!(head1.outgoing.contains(&dummy_activity_id1));
    assert!(head1.outgoing.contains(&dummy_activity_id2));

    assert_eq!(
        builder.edge_tail_node(dummy_activity_id1).unwrap().id(),
        head1.id()
    );
    assert_eq!(
        builder.edge_tail_node(dummy_activity_id2).unwrap().id(),
        head1.id()
    );

    // Dummy activities.
    assert!(builder.edge(dummy_activity_id1).unwrap().content.is_dummy());
    assert!(builder.edge(dummy_activity_id2).unwrap().content.is_dummy());
    assert!(builder.edge(dummy_activity_id3).unwrap().content.is_dummy());

    let end_node = builder.edge_head_node(dummy_activity_id1).unwrap();
    assert_eq!(end_node.id(), builder.end_node().id());
    assert_eq!(end_node.incoming.len(), 2);
    assert!(end_node.incoming.contains(&dummy_activity_id1));
    assert!(end_node.incoming.contains(&dummy_activity_id3));

    // Second activity.
    let tail2 = builder.edge_tail_node(activity_id2).unwrap();
    assert_eq!(tail2.incoming.len(), 1);
    assert!(tail2.incoming.contains(&dummy_activity_id2));
    assert_eq!(tail2.outgoing.len(), 1);
    assert!(tail2.outgoing.contains(&activity_id2));

    let head2 = builder.edge_head_node(activity_id2).unwrap();
    assert_eq!(head2.incoming.len(), 1);
    assert!(head2.incoming.contains(&activity_id2));
    assert_eq!(head2.outgoing.len(), 1);
    assert!(head2.outgoing.contains(&dummy_activity_id3));

    assert_eq!(
        builder.edge_head_node(dummy_activity_id3).unwrap().id(),
        builder.end_node().id()
    );
}

#[test]
fn given_two_activities_one_dependency_reverse_order_then_activities_hooked_up_by_dummy_edge() {
    let activity_id1 = 1;
    let activity_id2 = activity_id1 + 1;
    let dummy_activity_id = activity_id2 + 1;
    let dummy_activity_id1 = dummy_activity_id + 1;
    let dummy_activity_id2 = dummy_activity_id1 + 1;
    let mut builder = new_builder(dummy_activity_id, 0);
    builder.shuffle_processing_order = true;

    assert!(builder
        .add_activity_with_dependencies(Act::new(activity_id2, 0), IndexSet::from([activity_id1])));

    assert_eq!(builder.edge_ids().len(), 2);
    assert_eq!(builder.node_ids().len(), 4);
    assert!(!builder.all_dependencies_satisfied());
    assert_ne!(
        builder.edge_tail_node(activity_id2).unwrap().id(),
        builder.start_node().id()
    );
    assert_eq!(builder.start_node().outgoing.len(), 0);

    let tail2 = builder.edge_tail_node(activity_id2).unwrap();
    assert_eq!(tail2.incoming.len(), 0);
    assert_eq!(tail2.outgoing.len(), 1);
    assert!(tail2.outgoing.contains(&activity_id2));

    let head2 = builder.edge_head_node(activity_id2).unwrap();
    assert_eq!(head2.incoming.len(), 1);
    assert!(head2.incoming.contains(&activity_id2));

    assert!(builder.add_activity(Act::new(activity_id1, 0)));

    assert_eq!(builder.edge_ids().len(), 4);
    assert_eq!(builder.node_ids().len(), 5);
    assert!(builder.all_dependencies_satisfied());

    // First activity.
    assert_eq!(
        builder.edge_tail_node(activity_id1).unwrap().id(),
        builder.start_node().id()
    );
    assert_eq!(builder.start_node().outgoing.len(), 1);
    assert!(builder.start_node().outgoing.contains(&activity_id1));

    let head1 = builder.edge_head_node(activity_id1).unwrap();
    assert_eq!(head1.incoming.len(), 1);
    assert!(head1.incoming.contains(&activity_id1));
    assert_eq!(head1.outgoing.len(), 1);
    assert!(head1.outgoing.contains(&dummy_activity_id2));

    assert_eq!(
        builder.edge_tail_node(dummy_activity_id2).unwrap().id(),
        head1.id()
    );

    // Dummy activity that connects activity 2's head to the end node.
    let tail_d1 = builder.edge_tail_node(dummy_activity_id1).unwrap();
    assert_eq!(tail_d1.incoming.len(), 1);
    assert!(tail_d1.incoming.contains(&activity_id2));
    assert_eq!(tail_d1.outgoing.len(), 1);
    assert!(tail_d1.outgoing.contains(&dummy_activity_id1));

    assert!(builder.edge(dummy_activity_id1).unwrap().content.is_dummy());

    assert_eq!(
        builder.edge_head_node(dummy_activity_id1).unwrap().id(),
        builder.end_node().id()
    );
    assert!(builder.end_node().incoming.contains(&dummy_activity_id1));

    assert_eq!(
        builder.edge_head_node(activity_id2).unwrap().id(),
        builder.edge_tail_node(dummy_activity_id1).unwrap().id()
    );

    // The dummy edge created to resolve the unsatisfied dependency.
    assert_eq!(
        builder.edge_head_node(dummy_activity_id2).unwrap().id(),
        builder.edge_tail_node(activity_id2).unwrap().id()
    );
    assert!(builder.edge(dummy_activity_id2).unwrap().content.is_dummy());
}

#[test]
fn given_arrow_graph_compiler_compile_then_critical_path_calculated() {
    let mut compiler: ArrowGraphCompiler<i32, i32, i32> = ArrowGraphCompiler::new();
    compiler.add_activity(Act::new(1, 6));
    compiler.add_activity(Act::new(2, 7));
    compiler.add_activity(Act::new(3, 8));
    compiler.add_activity(Act::with_dependencies(4, 11, [2]));
    compiler.add_activity(Act::with_dependencies(5, 8, [1, 2, 3]));
    compiler.add_activity(Act::with_dependencies(6, 7, [3]));
    compiler.add_activity(Act::with_dependencies(7, 4, [4]));
    compiler.add_activity(Act::with_dependencies(8, 4, [4, 6]));
    compiler.add_activity(Act::with_dependencies(9, 10, [5]));

    compiler.compile().unwrap();

    // The CPM values must match the vertex-compiler values for the same network.
    let builder = compiler.builder();
    let expectations: &[(i32, i32, i32, i32)] = &[
        // (id, EST, EFT, LFT)
        (1, 0, 6, 8),
        (2, 0, 7, 8),
        (3, 0, 8, 8),
        (4, 7, 18, 22),
        (5, 8, 16, 16),
        (6, 8, 15, 22),
        (7, 18, 22, 26),
        (8, 18, 22, 26),
        (9, 16, 26, 26),
    ];
    for (id, est, eft, lft) in expectations {
        let activity = builder.activity(*id).unwrap();
        assert_eq!(activity.earliest_start_time, Some(*est), "EST of {id}");
        assert_eq!(activity.earliest_finish_time(), Some(*eft), "EFT of {id}");
        assert_eq!(activity.latest_finish_time, Some(*lft), "LFT of {id}");
    }

    assert_eq!(compiler.start_time(), 0);
    assert_eq!(compiler.finish_time(), 26);

    // Export succeeds and retains every real activity.
    let graph = compiler.to_graph().unwrap();
    let mut real_ids: Vec<i32> = graph
        .edges
        .iter()
        .filter(|e| !e.content.is_dummy())
        .map(|e| e.id())
        .collect();
    real_ids.sort();
    assert_eq!(real_ids, vec![1, 2, 3, 4, 5, 6, 7, 8, 9]);

    // A single Start and a single End node.
    assert_eq!(
        graph
            .nodes
            .iter()
            .filter(|n| n.node_type() == NodeType::Start)
            .count(),
        1
    );
    assert_eq!(
        graph
            .nodes
            .iter()
            .filter(|n| n.node_type() == NodeType::End)
            .count(),
        1
    );
}

#[test]
fn given_arrow_graph_compiler_with_invalid_dependencies_then_compile_fails() {
    let mut compiler: ArrowGraphCompiler<i32, i32, i32> = ArrowGraphCompiler::new();
    compiler.add_activity(Act::with_dependencies(1, 6, [99]));

    let result = compiler.compile();
    assert!(result.is_err());
    assert_eq!(
        result.unwrap_err().message(),
        "Cannot construct arrow graph due to invalid dependencies"
    );
}

#[test]
fn given_builder_clone_then_structure_preserved() {
    let mut builder = new_builder(10, 0);
    builder.add_activity(Act::new(1, 3));
    builder.add_activity_with_dependencies(Act::new(2, 5), IndexSet::from([1]));

    let mut clone = builder.clone_builder().unwrap();

    assert_eq!(clone.edge_ids().len(), builder.edge_ids().len());
    assert_eq!(clone.node_ids().len(), builder.node_ids().len());
    let graph_a = builder.to_graph().unwrap();
    let graph_b = clone.to_graph().unwrap();
    assert_eq!(graph_a, graph_b);
}

#[test]
fn given_three_activities_one_dependent_on_other_two_then_dependent_activity_hooked_up_by_two_dummy_edges(
) {
    let mut builder = new_builder(4, 0);
    builder.shuffle_processing_order = true;
    assert!(builder.add_activity(Act::new(1, 0)));
    assert_eq!(builder.edge_ids().len(), 2);
    assert_eq!(builder.node_ids().len(), 3);
    assert!(builder.all_dependencies_satisfied());
    // First activity.
    assert_eq!(
        builder.edge_tail_node(1).unwrap().id(),
        builder.start_node().id()
    );
    assert_eq!(builder.start_node().outgoing.len(), 1);
    assert!(builder.start_node().outgoing.contains(&1));
    assert_eq!(builder.edge_head_node(1).unwrap().incoming.len(), 1);
    assert!(builder.edge_head_node(1).unwrap().incoming.contains(&1));
    assert_eq!(
        builder.edge_tail_node(5).unwrap().id(),
        builder.edge_head_node(1).unwrap().id()
    );
    assert_eq!(builder.edge_tail_node(5).unwrap().outgoing.len(), 1);
    assert!(builder.edge_tail_node(5).unwrap().outgoing.contains(&5));
    assert_eq!(
        builder.edge_head_node(5).unwrap().id(),
        builder.end_node().id()
    );
    assert_eq!(builder.end_node().incoming.len(), 1);
    assert!(builder.end_node().incoming.contains(&5));
    assert!(builder.add_activity(Act::new(2, 0)));
    assert_eq!(builder.edge_ids().len(), 4);
    assert_eq!(builder.node_ids().len(), 4);
    assert!(builder.all_dependencies_satisfied());
    // Second activity.
    assert_eq!(
        builder.edge_tail_node(2).unwrap().id(),
        builder.start_node().id()
    );
    assert_eq!(builder.start_node().outgoing.len(), 2);
    assert!(builder.start_node().outgoing.contains(&2));
    assert_eq!(builder.edge_head_node(2).unwrap().incoming.len(), 1);
    assert!(builder.edge_head_node(2).unwrap().incoming.contains(&2));
    assert_eq!(
        builder.edge_tail_node(6).unwrap().id(),
        builder.edge_head_node(2).unwrap().id()
    );
    assert_eq!(builder.edge_tail_node(6).unwrap().outgoing.len(), 1);
    assert!(builder.edge_tail_node(6).unwrap().outgoing.contains(&6));
    assert_eq!(
        builder.edge_head_node(6).unwrap().id(),
        builder.end_node().id()
    );
    assert_eq!(builder.end_node().incoming.len(), 2);
    assert!(builder.end_node().incoming.contains(&6));
    // Dummy activity.
    assert_eq!(builder.edge_tail_node(5).unwrap().incoming.len(), 1);
    assert!(builder.edge_tail_node(5).unwrap().incoming.contains(&1));
    assert_eq!(builder.edge_tail_node(5).unwrap().outgoing.len(), 1);
    assert!(builder.edge_tail_node(5).unwrap().outgoing.contains(&5));
    assert!(builder.edge(5).unwrap().content.is_dummy());
    assert_eq!(builder.edge_head_node(5).unwrap().incoming.len(), 2);
    assert!(builder.edge_head_node(5).unwrap().incoming.contains(&5));
    assert!(builder.end_node().incoming.contains(&5));
    assert_eq!(
        builder.edge_head_node(1).unwrap().id(),
        builder.edge_tail_node(5).unwrap().id()
    );
    assert!(builder.add_activity_with_dependencies(Act::new(3, 0), IndexSet::from([1, 2])));
    assert_eq!(builder.edge_ids().len(), 8);
    assert_eq!(builder.node_ids().len(), 6);
    assert!(builder.all_dependencies_satisfied());
    // First activity.
    assert_eq!(
        builder.edge_tail_node(1).unwrap().id(),
        builder.start_node().id()
    );
    assert_ne!(
        builder.edge_head_node(1).unwrap().id(),
        builder.end_node().id()
    );
    assert_eq!(builder.start_node().outgoing.len(), 2);
    assert!(builder.start_node().outgoing.contains(&1));
    assert_eq!(builder.edge_tail_node(1).unwrap().outgoing.len(), 2);
    assert!(builder.edge_tail_node(1).unwrap().outgoing.contains(&1));
    assert_eq!(builder.edge_head_node(1).unwrap().incoming.len(), 1);
    assert!(builder.edge_head_node(1).unwrap().incoming.contains(&1));
    assert_eq!(builder.edge_head_node(1).unwrap().outgoing.len(), 2);
    assert!(builder.edge_head_node(1).unwrap().outgoing.contains(&5));
    assert!(builder.edge_head_node(1).unwrap().outgoing.contains(&7));
    assert_eq!(
        builder.edge_tail_node(5).unwrap().id(),
        builder.edge_head_node(1).unwrap().id()
    );
    // Second activity.
    assert_eq!(
        builder.edge_tail_node(2).unwrap().id(),
        builder.start_node().id()
    );
    assert_ne!(
        builder.edge_head_node(2).unwrap().id(),
        builder.end_node().id()
    );
    assert_eq!(builder.start_node().outgoing.len(), 2);
    assert!(builder.start_node().outgoing.contains(&2));
    assert_eq!(builder.edge_tail_node(2).unwrap().outgoing.len(), 2);
    assert!(builder.edge_tail_node(2).unwrap().outgoing.contains(&2));
    assert_eq!(builder.edge_head_node(2).unwrap().incoming.len(), 1);
    assert!(builder.edge_head_node(2).unwrap().incoming.contains(&2));
    assert_eq!(builder.edge_head_node(2).unwrap().outgoing.len(), 2);
    assert!(builder.edge_head_node(2).unwrap().outgoing.contains(&6));
    assert!(builder.edge_head_node(2).unwrap().outgoing.contains(&8));
    assert_eq!(
        builder.edge_tail_node(6).unwrap().id(),
        builder.edge_head_node(2).unwrap().id()
    );
    // First dummy activity.
    assert_eq!(builder.edge_tail_node(5).unwrap().incoming.len(), 1);
    assert!(builder.edge_tail_node(5).unwrap().incoming.contains(&1));
    assert_eq!(builder.edge_tail_node(5).unwrap().outgoing.len(), 2);
    assert!(builder.edge_tail_node(5).unwrap().outgoing.contains(&5));
    assert!(builder.edge_tail_node(7).unwrap().outgoing.contains(&5));
    assert!(builder.edge(5).unwrap().content.is_dummy());
    assert_eq!(builder.edge_head_node(5).unwrap().incoming.len(), 3);
    assert!(builder.edge_head_node(5).unwrap().incoming.contains(&5));
    assert!(builder.edge_head_node(5).unwrap().incoming.contains(&6));
    assert!(builder.edge_head_node(5).unwrap().incoming.contains(&9));
    assert_eq!(
        builder.edge_head_node(5).unwrap().id(),
        builder.end_node().id()
    );
    // Second dummy activity.
    assert_eq!(builder.edge_tail_node(6).unwrap().incoming.len(), 1);
    assert!(builder.edge_tail_node(6).unwrap().incoming.contains(&2));
    assert_eq!(builder.edge_tail_node(6).unwrap().outgoing.len(), 2);
    assert!(builder.edge_tail_node(6).unwrap().outgoing.contains(&6));
    assert!(builder.edge_tail_node(8).unwrap().outgoing.contains(&6));
    assert!(builder.edge(6).unwrap().content.is_dummy());
    assert_eq!(builder.edge_head_node(6).unwrap().incoming.len(), 3);
    assert!(builder.edge_head_node(6).unwrap().incoming.contains(&5));
    assert!(builder.edge_head_node(6).unwrap().incoming.contains(&6));
    assert!(builder.edge_head_node(6).unwrap().incoming.contains(&9));
    assert_eq!(
        builder.edge_head_node(6).unwrap().id(),
        builder.end_node().id()
    );
    // Third dummy activity.
    assert_eq!(builder.edge_tail_node(7).unwrap().incoming.len(), 1);
    assert!(builder.edge_tail_node(7).unwrap().incoming.contains(&1));
    assert_eq!(builder.edge_tail_node(7).unwrap().outgoing.len(), 2);
    assert!(builder.edge_tail_node(5).unwrap().outgoing.contains(&5));
    assert!(builder.edge_tail_node(7).unwrap().outgoing.contains(&5));
    assert!(builder.edge(7).unwrap().content.is_dummy());
    assert_eq!(builder.edge_head_node(7).unwrap().incoming.len(), 2);
    assert!(builder.edge_head_node(7).unwrap().incoming.contains(&7));
    assert!(builder.edge_head_node(7).unwrap().incoming.contains(&8));
    // Forth dummy activity.
    assert_eq!(builder.edge_tail_node(8).unwrap().incoming.len(), 1);
    assert!(builder.edge_tail_node(8).unwrap().incoming.contains(&2));
    assert_eq!(builder.edge_tail_node(8).unwrap().outgoing.len(), 2);
    assert!(builder.edge_tail_node(6).unwrap().outgoing.contains(&6));
    assert!(builder.edge_tail_node(8).unwrap().outgoing.contains(&6));
    assert!(builder.edge(8).unwrap().content.is_dummy());
    assert_eq!(builder.edge_head_node(8).unwrap().incoming.len(), 2);
    assert!(builder.edge_head_node(8).unwrap().incoming.contains(&7));
    assert!(builder.edge_head_node(8).unwrap().incoming.contains(&8));
    // Fifth dummy activity.
    assert_eq!(builder.edge_tail_node(9).unwrap().incoming.len(), 1);
    assert!(builder.edge_tail_node(9).unwrap().incoming.contains(&3));
    assert_eq!(builder.edge_tail_node(9).unwrap().outgoing.len(), 1);
    assert!(builder.edge_tail_node(9).unwrap().outgoing.contains(&9));
    assert!(builder.edge(9).unwrap().content.is_dummy());
    assert_eq!(builder.edge_head_node(9).unwrap().incoming.len(), 3);
    assert!(builder.edge_head_node(9).unwrap().incoming.contains(&5));
    assert!(builder.edge_head_node(9).unwrap().incoming.contains(&6));
    assert!(builder.edge_head_node(9).unwrap().incoming.contains(&9));
    assert_eq!(
        builder.edge_head_node(9).unwrap().id(),
        builder.end_node().id()
    );
    // Third activity.
    assert_eq!(builder.edge_tail_node(3).unwrap().incoming.len(), 2);
    assert!(builder.edge_tail_node(3).unwrap().incoming.contains(&7));
    assert!(builder.edge_tail_node(3).unwrap().incoming.contains(&8));
    assert_eq!(builder.edge_tail_node(3).unwrap().outgoing.len(), 1);
    assert!(builder.edge_tail_node(3).unwrap().outgoing.contains(&3));
    assert_eq!(builder.edge_head_node(3).unwrap().incoming.len(), 1);
    assert!(builder.edge_head_node(3).unwrap().incoming.contains(&3));
    assert_eq!(builder.edge_head_node(3).unwrap().outgoing.len(), 1);
    assert!(builder.edge_head_node(3).unwrap().outgoing.contains(&9));
}

#[test]
fn given_three_activities_one_dependent_on_other_two_reverse_order_then_dependent_activity_hooked_up_by_two_dummy_edges(
) {
    let mut builder = new_builder(4, 0);
    builder.shuffle_processing_order = true;
    assert!(builder.add_activity_with_dependencies(Act::new(3, 0), IndexSet::from([1, 2])));
    assert_eq!(builder.edge_ids().len(), 2);
    assert_eq!(builder.node_ids().len(), 4);
    assert!(!builder.all_dependencies_satisfied());
    assert_ne!(
        builder.edge_tail_node(3).unwrap().id(),
        builder.start_node().id()
    );
    assert_eq!(builder.start_node().outgoing.len(), 0);
    assert_eq!(builder.edge_tail_node(3).unwrap().incoming.len(), 0);
    assert_eq!(builder.edge_tail_node(3).unwrap().outgoing.len(), 1);
    assert!(builder.edge_tail_node(3).unwrap().outgoing.contains(&3));
    assert!(builder.add_activity(Act::new(2, 0)));
    assert_eq!(builder.edge_ids().len(), 4);
    assert_eq!(builder.node_ids().len(), 5);
    assert!(!builder.all_dependencies_satisfied());
    assert_ne!(
        builder.edge_tail_node(3).unwrap().id(),
        builder.start_node().id()
    );
    // Second activity.
    assert_eq!(
        builder.edge_tail_node(2).unwrap().id(),
        builder.start_node().id()
    );
    assert_ne!(
        builder.edge_head_node(2).unwrap().id(),
        builder.end_node().id()
    );
    assert_eq!(builder.start_node().outgoing.len(), 1);
    assert!(builder.start_node().outgoing.contains(&2));
    assert_eq!(builder.edge_tail_node(2).unwrap().outgoing.len(), 1);
    assert!(builder.edge_tail_node(2).unwrap().outgoing.contains(&2));
    assert_eq!(builder.edge_head_node(2).unwrap().incoming.len(), 1);
    assert!(builder.edge_head_node(2).unwrap().incoming.contains(&2));
    assert_eq!(builder.edge_head_node(2).unwrap().outgoing.len(), 1);
    assert!(builder.edge_head_node(2).unwrap().outgoing.contains(&6));
    assert_eq!(
        builder.edge_tail_node(6).unwrap().id(),
        builder.edge_head_node(2).unwrap().id()
    );
    // Second dummy activity.
    assert_eq!(builder.edge_tail_node(6).unwrap().incoming.len(), 1);
    assert!(builder.edge_tail_node(6).unwrap().incoming.contains(&2));
    assert_eq!(builder.edge_tail_node(6).unwrap().outgoing.len(), 1);
    assert!(builder.edge_tail_node(6).unwrap().outgoing.contains(&6));
    assert!(builder.edge(6).unwrap().content.is_dummy());
    assert_eq!(builder.edge_head_node(6).unwrap().incoming.len(), 1);
    assert!(builder.edge_head_node(6).unwrap().incoming.contains(&6));
    assert_eq!(builder.edge_head_node(6).unwrap().outgoing.len(), 1);
    assert!(builder.edge_head_node(6).unwrap().outgoing.contains(&3));
    assert_eq!(
        builder.edge_tail_node(3).unwrap().id(),
        builder.edge_head_node(6).unwrap().id()
    );
    // Third activity.
    assert_eq!(builder.edge_tail_node(3).unwrap().incoming.len(), 1);
    assert!(builder.edge_tail_node(3).unwrap().incoming.contains(&6));
    assert_eq!(builder.edge_tail_node(3).unwrap().outgoing.len(), 1);
    assert!(builder.edge_tail_node(3).unwrap().outgoing.contains(&3));
    assert_eq!(builder.edge_head_node(3).unwrap().incoming.len(), 1);
    assert!(builder.edge_head_node(3).unwrap().incoming.contains(&3));
    assert_ne!(
        builder.edge_head_node(3).unwrap().id(),
        builder.end_node().id()
    );
    assert_ne!(
        builder.edge_tail_node(3).unwrap().id(),
        builder.start_node().id()
    );
    assert!(builder.add_activity(Act::new(1, 0)));
    assert_eq!(builder.edge_ids().len(), 6);
    assert_eq!(builder.node_ids().len(), 6);
    assert!(builder.all_dependencies_satisfied());
    // First activity.
    assert_eq!(
        builder.edge_tail_node(1).unwrap().id(),
        builder.start_node().id()
    );
    assert_ne!(
        builder.edge_head_node(1).unwrap().id(),
        builder.end_node().id()
    );
    assert_eq!(builder.start_node().outgoing.len(), 2);
    assert!(builder.start_node().outgoing.contains(&1));
    assert!(builder.start_node().outgoing.contains(&2));
    assert_eq!(builder.edge_tail_node(1).unwrap().outgoing.len(), 2);
    assert!(builder.edge_tail_node(1).unwrap().outgoing.contains(&1));
    assert!(builder.edge_tail_node(1).unwrap().outgoing.contains(&2));
    assert_eq!(builder.edge_head_node(1).unwrap().incoming.len(), 1);
    assert!(builder.edge_head_node(1).unwrap().incoming.contains(&1));
    assert_eq!(builder.edge_head_node(1).unwrap().outgoing.len(), 1);
    assert!(builder.edge_head_node(1).unwrap().outgoing.contains(&7));
    assert_eq!(
        builder.edge_tail_node(7).unwrap().id(),
        builder.edge_head_node(1).unwrap().id()
    );
    // Second activity.
    assert_eq!(
        builder.edge_tail_node(2).unwrap().id(),
        builder.start_node().id()
    );
    assert_ne!(
        builder.edge_head_node(2).unwrap().id(),
        builder.end_node().id()
    );
    assert_eq!(builder.start_node().outgoing.len(), 2);
    assert!(builder.start_node().outgoing.contains(&2));
    assert_eq!(builder.edge_tail_node(2).unwrap().outgoing.len(), 2);
    assert!(builder.edge_tail_node(2).unwrap().outgoing.contains(&2));
    assert_eq!(builder.edge_head_node(2).unwrap().incoming.len(), 1);
    assert!(builder.edge_head_node(2).unwrap().incoming.contains(&2));
    assert_eq!(builder.edge_head_node(2).unwrap().outgoing.len(), 1);
    assert!(builder.edge_head_node(2).unwrap().outgoing.contains(&6));
    assert_eq!(
        builder.edge_tail_node(6).unwrap().id(),
        builder.edge_head_node(2).unwrap().id()
    );
    // First dummy activity.
    assert_eq!(builder.edge_tail_node(5).unwrap().incoming.len(), 1);
    assert!(builder.edge_tail_node(5).unwrap().incoming.contains(&3));
    assert_eq!(builder.edge_tail_node(5).unwrap().outgoing.len(), 1);
    assert!(builder.edge_tail_node(5).unwrap().outgoing.contains(&5));
    assert!(builder.edge(5).unwrap().content.is_dummy());
    assert_eq!(builder.edge_head_node(5).unwrap().incoming.len(), 1);
    assert!(builder.edge_head_node(5).unwrap().incoming.contains(&5));
    assert_eq!(
        builder.edge_head_node(5).unwrap().id(),
        builder.end_node().id()
    );
    assert!(builder.end_node().incoming.contains(&5));
    // Second dummy activity.
    assert_eq!(builder.edge_tail_node(6).unwrap().incoming.len(), 1);
    assert!(builder.edge_tail_node(6).unwrap().incoming.contains(&2));
    assert_eq!(builder.edge_tail_node(6).unwrap().outgoing.len(), 1);
    assert!(builder.edge_tail_node(6).unwrap().outgoing.contains(&6));
    assert!(builder.edge(6).unwrap().content.is_dummy());
    assert_eq!(builder.edge_head_node(6).unwrap().incoming.len(), 2);
    assert!(builder.edge_head_node(6).unwrap().incoming.contains(&6));
    assert!(builder.edge_head_node(6).unwrap().incoming.contains(&7));
    assert_eq!(builder.edge_head_node(6).unwrap().outgoing.len(), 1);
    assert!(builder.edge_head_node(6).unwrap().outgoing.contains(&3));
    assert_eq!(
        builder.edge_tail_node(3).unwrap().id(),
        builder.edge_head_node(6).unwrap().id()
    );
    // Third dummy activity.
    assert_eq!(builder.edge_tail_node(7).unwrap().incoming.len(), 1);
    assert!(builder.edge_tail_node(7).unwrap().incoming.contains(&1));
    assert_eq!(builder.edge_tail_node(7).unwrap().outgoing.len(), 1);
    assert!(builder.edge_tail_node(7).unwrap().outgoing.contains(&7));
    assert!(builder.edge(7).unwrap().content.is_dummy());
    assert_eq!(builder.edge_head_node(7).unwrap().incoming.len(), 2);
    assert!(builder.edge_head_node(7).unwrap().incoming.contains(&6));
    assert!(builder.edge_head_node(7).unwrap().incoming.contains(&7));
    assert_eq!(builder.edge_head_node(7).unwrap().outgoing.len(), 1);
    assert!(builder.edge_head_node(7).unwrap().outgoing.contains(&3));
    assert_eq!(
        builder.edge_tail_node(3).unwrap().id(),
        builder.edge_head_node(7).unwrap().id()
    );
    // Third activity.
    assert_eq!(builder.edge_tail_node(3).unwrap().incoming.len(), 2);
    assert!(builder.edge_tail_node(3).unwrap().incoming.contains(&6));
    assert!(builder.edge_tail_node(3).unwrap().incoming.contains(&7));
    assert_eq!(builder.edge_tail_node(3).unwrap().outgoing.len(), 1);
    assert!(builder.edge_tail_node(3).unwrap().outgoing.contains(&3));
    assert_eq!(builder.edge_head_node(3).unwrap().incoming.len(), 1);
    assert!(builder.edge_head_node(3).unwrap().incoming.contains(&3));
    assert_eq!(builder.edge_head_node(3).unwrap().outgoing.len(), 1);
    assert!(builder.edge_head_node(3).unwrap().outgoing.contains(&5));
}

#[test]
fn given_three_activities_one_dependent_on_other_two_removed_in_stages_then_structure_as_expected()
{
    let mut builder = new_builder(4, 0);
    builder.shuffle_processing_order = true;
    assert!(builder.add_activity(Act::new_removable(1, 0, true)));
    assert!(builder.add_activity(Act::new_removable(2, 0, true)));
    assert!(builder
        .add_activity_with_dependencies(Act::new_removable(3, 0, true), IndexSet::from([1, 2])));
    assert_eq!(builder.edge_ids().len(), 8);
    assert_eq!(builder.node_ids().len(), 6);
    assert!(builder.all_dependencies_satisfied());
    assert!(builder.remove_dummy_activity(5).unwrap());
    assert!(builder.remove_dummy_activity(6).unwrap());
    assert!(builder.remove_dummy_activity(7).unwrap());
    assert_eq!(builder.edge_ids().len(), 5);
    assert_eq!(builder.node_ids().len(), 5);
    assert!(builder.all_dependencies_satisfied());
    // First activity.
    assert_eq!(
        builder.edge_tail_node(1).unwrap().id(),
        builder.start_node().id()
    );
    assert_ne!(
        builder.edge_head_node(1).unwrap().id(),
        builder.end_node().id()
    );
    assert_eq!(builder.start_node().outgoing.len(), 2);
    assert!(builder.start_node().outgoing.contains(&2));
    assert!(builder.start_node().outgoing.contains(&1));
    assert_eq!(builder.edge_tail_node(1).unwrap().outgoing.len(), 2);
    assert!(builder.edge_tail_node(1).unwrap().outgoing.contains(&2));
    assert!(builder.edge_tail_node(1).unwrap().outgoing.contains(&1));
    assert_eq!(builder.edge_head_node(1).unwrap().incoming.len(), 2);
    assert!(builder.edge_head_node(1).unwrap().incoming.contains(&1));
    assert!(builder.edge_head_node(1).unwrap().incoming.contains(&8));
    assert_eq!(builder.edge_head_node(1).unwrap().outgoing.len(), 1);
    assert!(builder.edge_head_node(1).unwrap().outgoing.contains(&3));
    // Second activity.
    assert_eq!(
        builder.edge_tail_node(2).unwrap().id(),
        builder.start_node().id()
    );
    assert_ne!(
        builder.edge_head_node(2).unwrap().id(),
        builder.end_node().id()
    );
    assert_eq!(builder.start_node().outgoing.len(), 2);
    assert!(builder.start_node().outgoing.contains(&2));
    assert!(builder.start_node().outgoing.contains(&1));
    assert_eq!(builder.edge_tail_node(2).unwrap().outgoing.len(), 2);
    assert!(builder.edge_tail_node(2).unwrap().outgoing.contains(&2));
    assert!(builder.edge_tail_node(2).unwrap().outgoing.contains(&1));
    assert_eq!(builder.edge_head_node(2).unwrap().incoming.len(), 1);
    assert!(builder.edge_head_node(2).unwrap().incoming.contains(&2));
    assert_eq!(builder.edge_head_node(2).unwrap().outgoing.len(), 1);
    assert!(builder.edge_head_node(2).unwrap().outgoing.contains(&8));
    assert_eq!(
        builder.edge_tail_node(8).unwrap().id(),
        builder.edge_head_node(2).unwrap().id()
    );
    // Third activity.
    assert_eq!(builder.edge_tail_node(3).unwrap().incoming.len(), 2);
    assert!(builder.edge_tail_node(3).unwrap().incoming.contains(&1));
    assert!(builder.edge_tail_node(3).unwrap().incoming.contains(&8));
    assert_eq!(builder.edge_tail_node(3).unwrap().outgoing.len(), 1);
    assert!(builder.edge_tail_node(3).unwrap().outgoing.contains(&3));
    assert_eq!(builder.edge_head_node(3).unwrap().incoming.len(), 1);
    assert!(builder.edge_head_node(3).unwrap().incoming.contains(&3));
    assert_eq!(builder.edge_head_node(3).unwrap().outgoing.len(), 1);
    assert!(builder.edge_head_node(3).unwrap().outgoing.contains(&9));
    assert_ne!(
        builder.edge_head_node(3).unwrap().id(),
        builder.end_node().id()
    );
    assert_ne!(
        builder.edge_tail_node(3).unwrap().id(),
        builder.start_node().id()
    );
    assert!(builder.remove_dummy_activity(3).unwrap());
    assert_eq!(builder.edge_ids().len(), 4);
    assert_eq!(builder.node_ids().len(), 4);
    assert!(builder.all_dependencies_satisfied());
    // First activity.
    assert_eq!(
        builder.edge_tail_node(1).unwrap().id(),
        builder.start_node().id()
    );
    assert_ne!(
        builder.edge_head_node(1).unwrap().id(),
        builder.end_node().id()
    );
    assert_eq!(builder.start_node().outgoing.len(), 2);
    assert!(builder.start_node().outgoing.contains(&2));
    assert!(builder.start_node().outgoing.contains(&1));
    assert_eq!(builder.edge_tail_node(1).unwrap().outgoing.len(), 2);
    assert!(builder.edge_tail_node(1).unwrap().outgoing.contains(&2));
    assert!(builder.edge_tail_node(1).unwrap().outgoing.contains(&1));
    assert_eq!(builder.edge_head_node(1).unwrap().incoming.len(), 2);
    assert!(builder.edge_head_node(1).unwrap().incoming.contains(&1));
    assert!(builder.edge_head_node(1).unwrap().incoming.contains(&8));
    assert_eq!(builder.edge_head_node(1).unwrap().outgoing.len(), 1);
    assert!(builder.edge_head_node(1).unwrap().outgoing.contains(&9));
    // Second activity.
    assert_eq!(
        builder.edge_tail_node(2).unwrap().id(),
        builder.start_node().id()
    );
    assert_ne!(
        builder.edge_head_node(2).unwrap().id(),
        builder.end_node().id()
    );
    assert_eq!(builder.start_node().outgoing.len(), 2);
    assert!(builder.start_node().outgoing.contains(&2));
    assert!(builder.start_node().outgoing.contains(&1));
    assert_eq!(builder.edge_tail_node(2).unwrap().outgoing.len(), 2);
    assert!(builder.edge_tail_node(2).unwrap().outgoing.contains(&2));
    assert!(builder.edge_tail_node(2).unwrap().outgoing.contains(&1));
    assert_eq!(builder.edge_head_node(2).unwrap().incoming.len(), 1);
    assert!(builder.edge_head_node(2).unwrap().incoming.contains(&2));
    assert_eq!(builder.edge_head_node(2).unwrap().outgoing.len(), 1);
    assert!(builder.edge_head_node(2).unwrap().outgoing.contains(&8));
    assert_eq!(
        builder.edge_tail_node(8).unwrap().id(),
        builder.edge_head_node(2).unwrap().id()
    );
    // Third activity.
    assert!(!builder.edge_ids().contains(&3));
    assert!(builder.remove_dummy_activity(9).unwrap());
    assert_eq!(builder.edge_ids().len(), 3);
    assert_eq!(builder.node_ids().len(), 3);
    assert!(builder.all_dependencies_satisfied());
    // First activity.
    assert_eq!(
        builder.edge_tail_node(1).unwrap().id(),
        builder.start_node().id()
    );
    assert_eq!(
        builder.edge_head_node(1).unwrap().id(),
        builder.end_node().id()
    );
    assert_eq!(builder.start_node().outgoing.len(), 2);
    assert!(builder.start_node().outgoing.contains(&2));
    assert!(builder.start_node().outgoing.contains(&1));
    assert_eq!(builder.edge_tail_node(1).unwrap().outgoing.len(), 2);
    assert!(builder.edge_tail_node(1).unwrap().outgoing.contains(&2));
    assert!(builder.edge_tail_node(1).unwrap().outgoing.contains(&1));
    assert_eq!(builder.edge_head_node(1).unwrap().incoming.len(), 2);
    assert!(builder.edge_head_node(1).unwrap().incoming.contains(&1));
    assert!(builder.edge_head_node(1).unwrap().incoming.contains(&8));
    // Second activity.
    assert_eq!(
        builder.edge_tail_node(2).unwrap().id(),
        builder.start_node().id()
    );
    assert_ne!(
        builder.edge_head_node(2).unwrap().id(),
        builder.end_node().id()
    );
    assert_eq!(builder.start_node().outgoing.len(), 2);
    assert!(builder.start_node().outgoing.contains(&2));
    assert!(builder.start_node().outgoing.contains(&1));
    assert_eq!(builder.edge_tail_node(2).unwrap().outgoing.len(), 2);
    assert!(builder.edge_tail_node(2).unwrap().outgoing.contains(&2));
    assert!(builder.edge_tail_node(2).unwrap().outgoing.contains(&1));
    assert_eq!(builder.edge_head_node(2).unwrap().incoming.len(), 1);
    assert!(builder.edge_head_node(2).unwrap().incoming.contains(&2));
    assert_eq!(builder.edge_head_node(2).unwrap().outgoing.len(), 1);
    assert!(builder.edge_head_node(2).unwrap().outgoing.contains(&8));
    assert_eq!(
        builder.edge_tail_node(8).unwrap().id(),
        builder.edge_head_node(2).unwrap().id()
    );
    // Third activity.
    assert!(!builder.edge_ids().contains(&3));
    assert!(!builder.remove_dummy_activity(1).unwrap());
    assert!(!builder.remove_dummy_activity(2).unwrap());
    assert!(!builder.remove_dummy_activity(8).unwrap());
}

#[test]
fn given_three_activities_one_dependent_on_other_two_redirect_dummy_edges_then_dummies_redirected_as_expected(
) {
    let mut builder = new_builder(4, 0);
    builder.shuffle_processing_order = true;
    assert!(builder.add_activity(Act::new(1, 0)));
    assert!(builder.add_activity(Act::new(2, 0)));
    assert!(builder.add_activity_with_dependencies(Act::new(3, 0), IndexSet::from([1, 2])));
    assert_eq!(builder.edge_ids().len(), 8);
    assert_eq!(builder.node_ids().len(), 6);
    assert!(builder.all_dependencies_satisfied());
    assert!(builder.redirect_edges().unwrap());
    assert_eq!(builder.edge_ids().len(), 7);
    assert_eq!(builder.node_ids().len(), 6);
    assert!(builder.all_dependencies_satisfied());
    // First activity.
    assert_eq!(
        builder.edge_tail_node(1).unwrap().id(),
        builder.start_node().id()
    );
    assert_ne!(
        builder.edge_head_node(1).unwrap().id(),
        builder.end_node().id()
    );
    assert_eq!(builder.start_node().outgoing.len(), 2);
    assert!(builder.start_node().outgoing.contains(&1));
    assert_eq!(builder.edge_tail_node(1).unwrap().outgoing.len(), 2);
    assert!(builder.edge_tail_node(1).unwrap().outgoing.contains(&1));
    assert_eq!(builder.edge_head_node(1).unwrap().incoming.len(), 2);
    assert!(builder.edge_head_node(1).unwrap().incoming.contains(&1));
    assert_eq!(builder.edge_head_node(1).unwrap().outgoing.len(), 2);
    assert!(builder.edge_head_node(1).unwrap().outgoing.contains(&5));
    assert!(builder.edge_head_node(1).unwrap().outgoing.contains(&7));
    assert_eq!(
        builder.edge_tail_node(5).unwrap().id(),
        builder.edge_head_node(1).unwrap().id()
    );
    assert_eq!(
        builder.edge_tail_node(7).unwrap().id(),
        builder.edge_head_node(1).unwrap().id()
    );
    // Second activity.
    assert_eq!(
        builder.edge_tail_node(2).unwrap().id(),
        builder.start_node().id()
    );
    assert_ne!(
        builder.edge_head_node(2).unwrap().id(),
        builder.end_node().id()
    );
    assert_eq!(builder.start_node().outgoing.len(), 2);
    assert!(builder.start_node().outgoing.contains(&2));
    assert_eq!(builder.edge_tail_node(2).unwrap().outgoing.len(), 2);
    assert!(builder.edge_tail_node(2).unwrap().outgoing.contains(&2));
    assert_eq!(builder.edge_head_node(2).unwrap().incoming.len(), 1);
    assert!(builder.edge_head_node(2).unwrap().incoming.contains(&2));
    assert_eq!(builder.edge_head_node(2).unwrap().outgoing.len(), 1);
    assert!(builder.edge_head_node(2).unwrap().outgoing.contains(&6));
    assert_eq!(
        builder.edge_tail_node(6).unwrap().id(),
        builder.edge_head_node(2).unwrap().id()
    );
    // First dummy activity.
    assert_eq!(builder.edge_tail_node(5).unwrap().incoming.len(), 2);
    assert!(builder.edge_tail_node(5).unwrap().incoming.contains(&1));
    assert!(builder.edge_tail_node(5).unwrap().incoming.contains(&6));
    assert_eq!(builder.edge_tail_node(5).unwrap().outgoing.len(), 2);
    assert!(builder.edge_tail_node(5).unwrap().outgoing.contains(&5));
    assert!(builder.edge_tail_node(5).unwrap().outgoing.contains(&7));
    assert!(builder.edge(5).unwrap().content.is_dummy());
    assert_eq!(builder.edge_head_node(5).unwrap().incoming.len(), 2);
    assert!(builder.edge_head_node(5).unwrap().incoming.contains(&5));
    assert!(builder.edge_head_node(5).unwrap().incoming.contains(&3));
    assert_eq!(builder.edge_head_node(5).unwrap().outgoing.len(), 1);
    assert!(builder.edge_head_node(5).unwrap().outgoing.contains(&9));
    assert_eq!(
        builder.edge_tail_node(9).unwrap().id(),
        builder.edge_head_node(5).unwrap().id()
    );
    // Second dummy activity.
    assert_eq!(builder.edge_tail_node(6).unwrap().incoming.len(), 1);
    assert!(builder.edge_tail_node(6).unwrap().incoming.contains(&2));
    assert_eq!(builder.edge_tail_node(6).unwrap().outgoing.len(), 1);
    assert!(builder.edge_tail_node(6).unwrap().outgoing.contains(&6));
    assert!(builder.edge(6).unwrap().content.is_dummy());
    assert_eq!(builder.edge_head_node(6).unwrap().incoming.len(), 2);
    assert!(builder.edge_head_node(6).unwrap().incoming.contains(&1));
    assert!(builder.edge_head_node(6).unwrap().incoming.contains(&6));
    assert_eq!(builder.edge_head_node(6).unwrap().outgoing.len(), 2);
    assert!(builder.edge_head_node(6).unwrap().outgoing.contains(&5));
    assert!(builder.edge_head_node(6).unwrap().outgoing.contains(&7));
    assert_eq!(
        builder.edge_tail_node(5).unwrap().id(),
        builder.edge_head_node(6).unwrap().id()
    );
    assert_eq!(
        builder.edge_tail_node(7).unwrap().id(),
        builder.edge_head_node(6).unwrap().id()
    );
    // Third activity.
    assert_eq!(builder.edge_tail_node(3).unwrap().incoming.len(), 1);
    assert!(builder.edge_tail_node(3).unwrap().incoming.contains(&7));
    assert_eq!(builder.edge_tail_node(3).unwrap().outgoing.len(), 1);
    assert!(builder.edge_tail_node(3).unwrap().outgoing.contains(&3));
    assert_eq!(builder.edge_head_node(3).unwrap().incoming.len(), 2);
    assert!(builder.edge_head_node(3).unwrap().incoming.contains(&5));
    assert!(builder.edge_head_node(3).unwrap().incoming.contains(&3));
    assert_eq!(builder.edge_head_node(3).unwrap().outgoing.len(), 1);
    assert!(builder.edge_head_node(3).unwrap().outgoing.contains(&9));
    assert_eq!(
        builder.edge_tail_node(9).unwrap().id(),
        builder.edge_head_node(3).unwrap().id()
    );
    // Third dummy activity.
    assert_eq!(builder.edge_tail_node(7).unwrap().incoming.len(), 2);
    assert!(builder.edge_tail_node(7).unwrap().incoming.contains(&1));
    assert!(builder.edge_tail_node(7).unwrap().incoming.contains(&6));
    assert_eq!(builder.edge_tail_node(7).unwrap().outgoing.len(), 2);
    assert!(builder.edge_tail_node(7).unwrap().outgoing.contains(&5));
    assert!(builder.edge_tail_node(7).unwrap().outgoing.contains(&7));
    assert!(builder.edge(7).unwrap().content.is_dummy());
    assert_eq!(builder.edge_head_node(7).unwrap().incoming.len(), 1);
    assert!(builder.edge_head_node(7).unwrap().incoming.contains(&7));
    assert_eq!(builder.edge_head_node(7).unwrap().outgoing.len(), 1);
    assert!(builder.edge_head_node(7).unwrap().outgoing.contains(&3));
    assert_eq!(
        builder.edge_tail_node(3).unwrap().id(),
        builder.edge_head_node(7).unwrap().id()
    );
    // Fourth dummy activity.
    assert!(!builder.edge_ids().contains(&8));
    // Fifth dummy activity.
    assert_eq!(builder.edge_tail_node(9).unwrap().incoming.len(), 2);
    assert!(builder.edge_tail_node(9).unwrap().incoming.contains(&3));
    assert!(builder.edge_tail_node(9).unwrap().incoming.contains(&5));
    assert_eq!(builder.edge_tail_node(9).unwrap().outgoing.len(), 1);
    assert!(builder.edge_tail_node(9).unwrap().outgoing.contains(&9));
    assert!(builder.edge(9).unwrap().content.is_dummy());
    assert_eq!(builder.edge_head_node(9).unwrap().incoming.len(), 1);
    assert!(builder.edge_head_node(9).unwrap().incoming.contains(&9));
    assert_eq!(
        builder.edge_head_node(9).unwrap().id(),
        builder.end_node().id()
    );
}

#[test]
fn given_four_activities_one_dependent_on_other_three_redirect_dummy_edges_then_dummies_redirected_as_expected(
) {
    let mut builder = new_builder(5, 0);
    builder.shuffle_processing_order = true;
    assert!(builder.add_activity(Act::new(1, 0)));
    assert!(builder.add_activity(Act::new(2, 0)));
    assert!(builder.add_activity(Act::new(3, 0)));
    assert!(builder.add_activity_with_dependencies(Act::new(4, 0), IndexSet::from([1, 2, 3])));
    assert!(builder.redirect_edges().unwrap());
    assert_eq!(builder.edge_ids().len(), 9);
    assert_eq!(builder.node_ids().len(), 7);
    assert!(builder.all_dependencies_satisfied());
    // First activity.
    assert_eq!(
        builder.edge_tail_node(1).unwrap().id(),
        builder.start_node().id()
    );
    assert_ne!(
        builder.edge_head_node(1).unwrap().id(),
        builder.end_node().id()
    );
    assert_eq!(builder.start_node().outgoing.len(), 3);
    assert!(builder.start_node().outgoing.contains(&1));
    assert!(builder.start_node().outgoing.contains(&2));
    assert!(builder.start_node().outgoing.contains(&3));
    assert_eq!(builder.edge_tail_node(1).unwrap().outgoing.len(), 3);
    assert!(builder.edge_tail_node(1).unwrap().outgoing.contains(&1));
    assert!(builder.edge_tail_node(1).unwrap().outgoing.contains(&2));
    assert!(builder.edge_tail_node(1).unwrap().outgoing.contains(&3));
    assert_eq!(builder.edge_head_node(1).unwrap().incoming.len(), 2);
    assert!(builder.edge_head_node(1).unwrap().incoming.contains(&1));
    assert!(builder.edge_head_node(1).unwrap().incoming.contains(&7));
    assert_eq!(builder.edge_head_node(1).unwrap().outgoing.len(), 2);
    assert!(builder.edge_head_node(1).unwrap().outgoing.contains(&6));
    assert!(builder.edge_head_node(1).unwrap().outgoing.contains(&9));
    assert_eq!(
        builder.edge_tail_node(6).unwrap().id(),
        builder.edge_head_node(1).unwrap().id()
    );
    assert_eq!(
        builder.edge_tail_node(9).unwrap().id(),
        builder.edge_head_node(1).unwrap().id()
    );
    // Second activity.
    assert_eq!(
        builder.edge_tail_node(2).unwrap().id(),
        builder.start_node().id()
    );
    assert_ne!(
        builder.edge_head_node(2).unwrap().id(),
        builder.end_node().id()
    );
    assert_eq!(builder.start_node().outgoing.len(), 3);
    assert!(builder.start_node().outgoing.contains(&1));
    assert!(builder.start_node().outgoing.contains(&2));
    assert!(builder.start_node().outgoing.contains(&3));
    assert_eq!(builder.edge_tail_node(2).unwrap().outgoing.len(), 3);
    assert!(builder.edge_tail_node(2).unwrap().outgoing.contains(&1));
    assert!(builder.edge_tail_node(2).unwrap().outgoing.contains(&2));
    assert!(builder.edge_tail_node(2).unwrap().outgoing.contains(&3));
    assert_eq!(builder.edge_head_node(2).unwrap().incoming.len(), 2);
    assert!(builder.edge_head_node(2).unwrap().incoming.contains(&2));
    assert!(builder.edge_head_node(2).unwrap().incoming.contains(&8));
    assert_eq!(builder.edge_head_node(2).unwrap().outgoing.len(), 1);
    assert!(builder.edge_head_node(2).unwrap().outgoing.contains(&7));
    assert_eq!(
        builder.edge_tail_node(7).unwrap().id(),
        builder.edge_head_node(2).unwrap().id()
    );
    // Third activity.
    assert_eq!(
        builder.edge_tail_node(3).unwrap().id(),
        builder.start_node().id()
    );
    assert_ne!(
        builder.edge_head_node(3).unwrap().id(),
        builder.end_node().id()
    );
    assert_eq!(builder.start_node().outgoing.len(), 3);
    assert!(builder.start_node().outgoing.contains(&1));
    assert!(builder.start_node().outgoing.contains(&2));
    assert!(builder.start_node().outgoing.contains(&3));
    assert_eq!(builder.edge_tail_node(3).unwrap().outgoing.len(), 3);
    assert!(builder.edge_tail_node(3).unwrap().outgoing.contains(&1));
    assert!(builder.edge_tail_node(3).unwrap().outgoing.contains(&2));
    assert!(builder.edge_tail_node(3).unwrap().outgoing.contains(&3));
    assert_eq!(builder.edge_head_node(3).unwrap().incoming.len(), 1);
    assert!(builder.edge_head_node(3).unwrap().incoming.contains(&3));
    assert_eq!(builder.edge_head_node(3).unwrap().outgoing.len(), 1);
    assert!(builder.edge_head_node(3).unwrap().outgoing.contains(&8));
    assert_eq!(
        builder.edge_tail_node(8).unwrap().id(),
        builder.edge_head_node(3).unwrap().id()
    );
    // Fourth activity.
    assert_eq!(builder.edge_tail_node(4).unwrap().incoming.len(), 1);
    assert!(builder.edge_tail_node(4).unwrap().incoming.contains(&9));
    assert_eq!(builder.edge_tail_node(4).unwrap().outgoing.len(), 1);
    assert!(builder.edge_tail_node(4).unwrap().outgoing.contains(&4));
    assert_eq!(builder.edge_head_node(4).unwrap().incoming.len(), 2);
    assert!(builder.edge_head_node(4).unwrap().incoming.contains(&4));
    assert!(builder.edge_head_node(4).unwrap().incoming.contains(&6));
    // First dummy activity.
    assert_eq!(builder.edge_tail_node(6).unwrap().incoming.len(), 2);
    assert!(builder.edge_tail_node(6).unwrap().incoming.contains(&1));
    assert!(builder.edge_tail_node(6).unwrap().incoming.contains(&7));
    assert_eq!(builder.edge_tail_node(6).unwrap().outgoing.len(), 2);
    assert!(builder.edge_tail_node(6).unwrap().outgoing.contains(&6));
    assert!(builder.edge_tail_node(6).unwrap().outgoing.contains(&9));
    assert!(builder.edge(6).unwrap().content.is_dummy());
    assert_eq!(builder.edge_head_node(6).unwrap().incoming.len(), 2);
    assert!(builder.edge_head_node(6).unwrap().incoming.contains(&6));
    assert!(builder.edge_head_node(6).unwrap().incoming.contains(&4));
    assert_eq!(builder.edge_head_node(6).unwrap().outgoing.len(), 1);
    assert!(builder.edge_head_node(6).unwrap().outgoing.contains(&12));
    assert_eq!(
        builder.edge_tail_node(12).unwrap().id(),
        builder.edge_head_node(6).unwrap().id()
    );
    // Second dummy activity.
    assert_eq!(builder.edge_tail_node(7).unwrap().incoming.len(), 2);
    assert!(builder.edge_tail_node(7).unwrap().incoming.contains(&2));
    assert!(builder.edge_tail_node(7).unwrap().incoming.contains(&8));
    assert_eq!(builder.edge_tail_node(7).unwrap().outgoing.len(), 1);
    assert!(builder.edge_tail_node(7).unwrap().outgoing.contains(&7));
    assert!(builder.edge(7).unwrap().content.is_dummy());
    assert_eq!(builder.edge_head_node(7).unwrap().incoming.len(), 2);
    assert!(builder.edge_head_node(7).unwrap().incoming.contains(&1));
    assert!(builder.edge_head_node(7).unwrap().incoming.contains(&7));
    assert_eq!(builder.edge_head_node(7).unwrap().outgoing.len(), 2);
    assert!(builder.edge_head_node(7).unwrap().outgoing.contains(&6));
    assert!(builder.edge_head_node(7).unwrap().outgoing.contains(&9));
    assert_eq!(
        builder.edge_tail_node(6).unwrap().id(),
        builder.edge_head_node(7).unwrap().id()
    );
    // Third dummy activity.
    assert_eq!(builder.edge_tail_node(8).unwrap().incoming.len(), 1);
    assert!(builder.edge_tail_node(8).unwrap().incoming.contains(&3));
    assert_eq!(builder.edge_tail_node(8).unwrap().outgoing.len(), 1);
    assert!(builder.edge_tail_node(8).unwrap().outgoing.contains(&8));
    assert!(builder.edge(7).unwrap().content.is_dummy());
    assert_eq!(builder.edge_head_node(8).unwrap().incoming.len(), 2);
    assert!(builder.edge_head_node(8).unwrap().incoming.contains(&2));
    assert!(builder.edge_head_node(8).unwrap().incoming.contains(&8));
    assert_eq!(builder.edge_head_node(8).unwrap().outgoing.len(), 1);
    assert!(builder.edge_head_node(8).unwrap().outgoing.contains(&7));
    assert_eq!(
        builder.edge_tail_node(7).unwrap().id(),
        builder.edge_head_node(8).unwrap().id()
    );
    // Fourth dummy activity.
    assert_eq!(builder.edge_tail_node(9).unwrap().incoming.len(), 2);
    assert!(builder.edge_tail_node(9).unwrap().incoming.contains(&1));
    assert!(builder.edge_tail_node(9).unwrap().incoming.contains(&7));
    assert_eq!(builder.edge_tail_node(9).unwrap().outgoing.len(), 2);
    assert!(builder.edge_tail_node(9).unwrap().outgoing.contains(&6));
    assert!(builder.edge_tail_node(9).unwrap().outgoing.contains(&9));
    assert!(builder.edge(9).unwrap().content.is_dummy());
    assert_eq!(builder.edge_head_node(9).unwrap().incoming.len(), 1);
    assert!(builder.edge_head_node(9).unwrap().incoming.contains(&9));
    assert_eq!(builder.edge_head_node(9).unwrap().outgoing.len(), 1);
    assert!(builder.edge_head_node(9).unwrap().outgoing.contains(&4));
    assert_eq!(
        builder.edge_tail_node(4).unwrap().id(),
        builder.edge_head_node(9).unwrap().id()
    );
    // Fifth dummy activity.
    assert!(!builder.edge_ids().contains(&10));
    // Sixth dummy activity.
    assert!(!builder.edge_ids().contains(&11));
    // Seventh dummy activity.
    assert_eq!(builder.end_node().incoming.len(), 1);
    assert!(builder.end_node().incoming.contains(&12));
    assert_eq!(builder.edge_tail_node(12).unwrap().incoming.len(), 2);
    assert!(builder.edge_tail_node(12).unwrap().incoming.contains(&4));
    assert!(builder.edge_tail_node(12).unwrap().incoming.contains(&6));
    assert_eq!(builder.edge_tail_node(12).unwrap().outgoing.len(), 1);
    assert!(builder.edge_tail_node(12).unwrap().outgoing.contains(&12));
    assert!(builder.edge(12).unwrap().content.is_dummy());
    assert_eq!(builder.edge_head_node(12).unwrap().incoming.len(), 1);
    assert!(builder.edge_head_node(12).unwrap().incoming.contains(&12));
    assert_eq!(
        builder.edge_head_node(12).unwrap().id(),
        builder.end_node().id()
    );
    assert_ne!(
        builder.edge_tail_node(12).unwrap().id(),
        builder.start_node().id()
    );
}

#[test]
fn given_three_activities_one_dependent_on_other_two_with_two_unnecessary_dummies_then_transitive_reduction_as_expected(
) {
    let mut builder = new_builder(7, 0);
    builder.shuffle_processing_order = true;
    assert!(builder.add_activity(Act::new(1, 0)));
    assert!(builder.add_activity(Act::new(2, 0)));
    assert!(builder.add_activity_with_dependencies(Act::new(3, 0), IndexSet::from([1, 2, 6])));
    assert!(builder.add_activity(Act::new_removable(4, 0, true)));
    assert!(
        builder.add_activity_with_dependencies(Act::new_removable(5, 0, true), IndexSet::from([1]))
    );
    assert!(builder.add_activity(Act::new_removable(6, 0, true)));
    assert_eq!(builder.edge_ids().len(), 15);
    assert_eq!(builder.node_ids().len(), 10);
    // First activity.
    assert_eq!(
        builder.edge_tail_node(1).unwrap().id(),
        builder.start_node().id()
    );
    assert_ne!(
        builder.edge_head_node(1).unwrap().id(),
        builder.end_node().id()
    );
    assert_eq!(builder.start_node().outgoing.len(), 4);
    assert!(builder.start_node().outgoing.contains(&1));
    assert!(builder.start_node().outgoing.contains(&2));
    assert!(builder.start_node().outgoing.contains(&4));
    assert!(builder.start_node().outgoing.contains(&6));
    assert_eq!(builder.edge_tail_node(1).unwrap().outgoing.len(), 4);
    assert!(builder.edge_tail_node(1).unwrap().outgoing.contains(&1));
    assert!(builder.edge_tail_node(1).unwrap().outgoing.contains(&2));
    assert!(builder.edge_tail_node(1).unwrap().outgoing.contains(&4));
    assert!(builder.edge_tail_node(1).unwrap().outgoing.contains(&6));
    assert_eq!(builder.edge_head_node(1).unwrap().incoming.len(), 1);
    assert!(builder.edge_head_node(1).unwrap().incoming.contains(&1));
    assert_eq!(builder.edge_head_node(1).unwrap().outgoing.len(), 3);
    assert!(builder.edge_head_node(1).unwrap().outgoing.contains(&8));
    assert!(builder.edge_head_node(1).unwrap().outgoing.contains(&10));
    assert!(builder.edge_head_node(1).unwrap().outgoing.contains(&14));
    // Second activity.
    assert_eq!(
        builder.edge_tail_node(2).unwrap().id(),
        builder.start_node().id()
    );
    assert_ne!(
        builder.edge_head_node(2).unwrap().id(),
        builder.end_node().id()
    );
    assert_eq!(builder.start_node().outgoing.len(), 4);
    assert!(builder.start_node().outgoing.contains(&1));
    assert!(builder.start_node().outgoing.contains(&2));
    assert!(builder.start_node().outgoing.contains(&4));
    assert!(builder.start_node().outgoing.contains(&6));
    assert_eq!(builder.edge_tail_node(2).unwrap().outgoing.len(), 4);
    assert!(builder.edge_tail_node(2).unwrap().outgoing.contains(&1));
    assert!(builder.edge_tail_node(2).unwrap().outgoing.contains(&2));
    assert!(builder.edge_tail_node(2).unwrap().outgoing.contains(&4));
    assert!(builder.edge_tail_node(2).unwrap().outgoing.contains(&6));
    assert_eq!(builder.edge_head_node(2).unwrap().incoming.len(), 1);
    assert!(builder.edge_head_node(2).unwrap().incoming.contains(&2));
    assert_eq!(builder.edge_head_node(2).unwrap().outgoing.len(), 2);
    assert!(builder.edge_head_node(2).unwrap().outgoing.contains(&9));
    assert!(builder.edge_head_node(2).unwrap().outgoing.contains(&11));
    // Third activity.
    assert_ne!(
        builder.edge_tail_node(3).unwrap().id(),
        builder.start_node().id()
    );
    assert_ne!(
        builder.edge_head_node(3).unwrap().id(),
        builder.end_node().id()
    );
    assert_eq!(builder.edge_tail_node(3).unwrap().incoming.len(), 3);
    assert!(builder.edge_tail_node(3).unwrap().incoming.contains(&10));
    assert!(builder.edge_tail_node(3).unwrap().incoming.contains(&11));
    assert!(builder.edge_tail_node(3).unwrap().incoming.contains(&16));
    assert_eq!(builder.edge_tail_node(3).unwrap().outgoing.len(), 1);
    assert!(builder.edge_tail_node(3).unwrap().outgoing.contains(&3));
    assert_eq!(builder.edge_head_node(3).unwrap().incoming.len(), 1);
    assert!(builder.edge_head_node(3).unwrap().incoming.contains(&3));
    assert_eq!(builder.edge_head_node(3).unwrap().outgoing.len(), 1);
    assert!(builder.edge_head_node(3).unwrap().outgoing.contains(&12));
    // First dummy activity.
    assert_ne!(
        builder.edge_tail_node(8).unwrap().id(),
        builder.start_node().id()
    );
    assert_eq!(
        builder.edge_head_node(8).unwrap().id(),
        builder.end_node().id()
    );
    assert_eq!(builder.edge_tail_node(8).unwrap().incoming.len(), 1);
    assert!(builder.edge_tail_node(8).unwrap().incoming.contains(&1));
    assert_eq!(builder.edge_tail_node(8).unwrap().outgoing.len(), 3);
    assert!(builder.edge_tail_node(8).unwrap().outgoing.contains(&8));
    assert!(builder.edge_tail_node(8).unwrap().outgoing.contains(&10));
    assert!(builder.edge_tail_node(8).unwrap().outgoing.contains(&14));
    assert_eq!(builder.edge_head_node(8).unwrap().incoming.len(), 5);
    assert!(builder.edge_head_node(8).unwrap().incoming.contains(&8));
    assert!(builder.edge_head_node(8).unwrap().incoming.contains(&9));
    assert!(builder.edge_head_node(8).unwrap().incoming.contains(&12));
    assert!(builder.edge_head_node(8).unwrap().incoming.contains(&13));
    assert!(builder.edge_head_node(8).unwrap().incoming.contains(&15));
    // Second dummy activity.
    assert_ne!(
        builder.edge_tail_node(9).unwrap().id(),
        builder.start_node().id()
    );
    assert_eq!(
        builder.edge_head_node(9).unwrap().id(),
        builder.end_node().id()
    );
    assert_eq!(builder.edge_tail_node(9).unwrap().incoming.len(), 1);
    assert!(builder.edge_tail_node(9).unwrap().incoming.contains(&2));
    assert_eq!(builder.edge_tail_node(9).unwrap().outgoing.len(), 2);
    assert!(builder.edge_tail_node(9).unwrap().outgoing.contains(&9));
    assert!(builder.edge_tail_node(9).unwrap().outgoing.contains(&11));
    assert_eq!(builder.edge_head_node(9).unwrap().incoming.len(), 5);
    assert!(builder.edge_head_node(9).unwrap().incoming.contains(&8));
    assert!(builder.edge_head_node(9).unwrap().incoming.contains(&9));
    assert!(builder.edge_head_node(9).unwrap().incoming.contains(&12));
    assert!(builder.edge_head_node(9).unwrap().incoming.contains(&13));
    assert!(builder.edge_head_node(9).unwrap().incoming.contains(&15));
    // Third dummy activity.
    assert_ne!(
        builder.edge_tail_node(10).unwrap().id(),
        builder.start_node().id()
    );
    assert_ne!(
        builder.edge_head_node(10).unwrap().id(),
        builder.end_node().id()
    );
    assert_eq!(builder.edge_tail_node(10).unwrap().incoming.len(), 1);
    assert!(builder.edge_tail_node(10).unwrap().incoming.contains(&1));
    assert_eq!(builder.edge_tail_node(10).unwrap().outgoing.len(), 3);
    assert!(builder.edge_tail_node(10).unwrap().outgoing.contains(&8));
    assert!(builder.edge_tail_node(10).unwrap().outgoing.contains(&10));
    assert!(builder.edge_tail_node(10).unwrap().outgoing.contains(&14));
    assert_eq!(builder.edge_head_node(10).unwrap().incoming.len(), 3);
    assert!(builder.edge_head_node(10).unwrap().incoming.contains(&10));
    assert!(builder.edge_head_node(10).unwrap().incoming.contains(&11));
    assert!(builder.edge_head_node(10).unwrap().incoming.contains(&16));
    assert_eq!(builder.edge_head_node(10).unwrap().outgoing.len(), 1);
    assert!(builder.edge_head_node(10).unwrap().outgoing.contains(&3));
    // Transitive Reduction.
    assert!(builder.transitive_reduction().unwrap());
    assert_eq!(builder.edge_ids().len(), 13);
    assert_eq!(builder.node_ids().len(), 10);
    assert!(builder.all_dependencies_satisfied());
    // First activity.
    assert_eq!(
        builder.edge_tail_node(1).unwrap().id(),
        builder.start_node().id()
    );
    assert_ne!(
        builder.edge_head_node(1).unwrap().id(),
        builder.end_node().id()
    );
    assert_eq!(builder.start_node().outgoing.len(), 4);
    assert!(builder.start_node().outgoing.contains(&1));
    assert!(builder.start_node().outgoing.contains(&2));
    assert!(builder.start_node().outgoing.contains(&4));
    assert!(builder.start_node().outgoing.contains(&6));
    assert_eq!(builder.edge_tail_node(1).unwrap().outgoing.len(), 4);
    assert!(builder.edge_tail_node(1).unwrap().outgoing.contains(&1));
    assert!(builder.edge_tail_node(1).unwrap().outgoing.contains(&2));
    assert!(builder.edge_tail_node(1).unwrap().outgoing.contains(&4));
    assert!(builder.edge_tail_node(1).unwrap().outgoing.contains(&6));
    assert_eq!(builder.edge_head_node(1).unwrap().incoming.len(), 1);
    assert!(builder.edge_head_node(1).unwrap().incoming.contains(&1));
    assert_eq!(builder.edge_head_node(1).unwrap().outgoing.len(), 2);
    assert!(builder.edge_head_node(1).unwrap().outgoing.contains(&10));
    assert!(builder.edge_head_node(1).unwrap().outgoing.contains(&14));
    // Second activity.
    assert_eq!(
        builder.edge_tail_node(2).unwrap().id(),
        builder.start_node().id()
    );
    assert_ne!(
        builder.edge_head_node(2).unwrap().id(),
        builder.end_node().id()
    );
    assert_eq!(builder.start_node().outgoing.len(), 4);
    assert!(builder.start_node().outgoing.contains(&1));
    assert!(builder.start_node().outgoing.contains(&2));
    assert!(builder.start_node().outgoing.contains(&4));
    assert!(builder.start_node().outgoing.contains(&6));
    assert_eq!(builder.edge_tail_node(2).unwrap().outgoing.len(), 4);
    assert!(builder.edge_tail_node(2).unwrap().outgoing.contains(&1));
    assert!(builder.edge_tail_node(2).unwrap().outgoing.contains(&2));
    assert!(builder.edge_tail_node(2).unwrap().outgoing.contains(&4));
    assert!(builder.edge_tail_node(2).unwrap().outgoing.contains(&6));
    assert_eq!(builder.edge_head_node(2).unwrap().incoming.len(), 1);
    assert!(builder.edge_head_node(2).unwrap().incoming.contains(&2));
    assert_eq!(builder.edge_head_node(2).unwrap().outgoing.len(), 1);
    assert!(builder.edge_head_node(2).unwrap().outgoing.contains(&11));
    // Third activity.
    assert_ne!(
        builder.edge_tail_node(3).unwrap().id(),
        builder.start_node().id()
    );
    assert_ne!(
        builder.edge_head_node(3).unwrap().id(),
        builder.end_node().id()
    );
    assert_eq!(builder.edge_tail_node(3).unwrap().incoming.len(), 3);
    assert!(builder.edge_tail_node(3).unwrap().incoming.contains(&10));
    assert!(builder.edge_tail_node(3).unwrap().incoming.contains(&11));
    assert!(builder.edge_tail_node(3).unwrap().incoming.contains(&16));
    assert_eq!(builder.edge_tail_node(3).unwrap().outgoing.len(), 1);
    assert!(builder.edge_tail_node(3).unwrap().outgoing.contains(&3));
    assert_eq!(builder.edge_head_node(3).unwrap().incoming.len(), 1);
    assert!(builder.edge_head_node(3).unwrap().incoming.contains(&3));
    assert_eq!(builder.edge_head_node(3).unwrap().outgoing.len(), 1);
    assert!(builder.edge_head_node(3).unwrap().outgoing.contains(&12));
    // First dummy activity.
    assert!(!builder.edge_ids().contains(&8));
    // Second dummy activity.
    assert!(!builder.edge_ids().contains(&9));
    // Third dummy activity.
    assert_ne!(
        builder.edge_tail_node(10).unwrap().id(),
        builder.start_node().id()
    );
    assert_ne!(
        builder.edge_head_node(10).unwrap().id(),
        builder.end_node().id()
    );
    assert_eq!(builder.edge_tail_node(10).unwrap().incoming.len(), 1);
    assert!(builder.edge_tail_node(10).unwrap().incoming.contains(&1));
    assert_eq!(builder.edge_tail_node(10).unwrap().outgoing.len(), 2);
    assert!(builder.edge_tail_node(10).unwrap().outgoing.contains(&10));
    assert!(builder.edge_tail_node(10).unwrap().outgoing.contains(&14));
    assert_eq!(builder.edge_head_node(10).unwrap().incoming.len(), 3);
    assert!(builder.edge_head_node(10).unwrap().incoming.contains(&10));
    assert!(builder.edge_head_node(10).unwrap().incoming.contains(&11));
    assert!(builder.edge_head_node(10).unwrap().incoming.contains(&16));
    assert_eq!(builder.edge_head_node(10).unwrap().outgoing.len(), 1);
    assert!(builder.edge_head_node(10).unwrap().outgoing.contains(&3));
}

#[test]
fn given_five_activities_with_three_unnecessary_dummies_then_remove_redundant_dummy_edges_as_expected(
) {
    let mut builder = new_builder(6, 0);
    builder.shuffle_processing_order = true;
    assert!(builder.add_activity(Act::new(1, 0)));
    assert!(builder.add_activity(Act::new(2, 0)));
    assert!(builder.add_activity_with_dependencies(Act::new(3, 0), IndexSet::from([1, 4])));
    assert!(builder.add_activity_with_dependencies(Act::new(4, 0), IndexSet::from([2])));
    assert!(builder.add_activity_with_dependencies(Act::new(5, 0), IndexSet::from([1])));
    assert_eq!(builder.edge_ids().len(), 13);
    assert_eq!(builder.node_ids().len(), 10);
    // RemoveRedundantEdges.
    assert!(builder.remove_redundant_edges().unwrap());
    assert_eq!(builder.edge_ids().len(), 9);
    assert_eq!(builder.node_ids().len(), 6);
    assert!(builder.all_dependencies_satisfied());
    // First activity.
    assert_eq!(
        builder.edge_tail_node(1).unwrap().id(),
        builder.start_node().id()
    );
    assert_ne!(
        builder.edge_head_node(1).unwrap().id(),
        builder.end_node().id()
    );
    assert_eq!(builder.start_node().outgoing.len(), 2);
    assert!(builder.start_node().outgoing.contains(&1));
    assert_eq!(builder.edge_tail_node(1).unwrap().outgoing.len(), 2);
    assert!(builder.edge_tail_node(1).unwrap().outgoing.contains(&1));
    assert_eq!(builder.edge_head_node(1).unwrap().incoming.len(), 1);
    assert!(builder.edge_head_node(1).unwrap().incoming.contains(&1));
    assert_eq!(builder.edge_head_node(1).unwrap().outgoing.len(), 3);
    assert!(builder.edge_head_node(1).unwrap().outgoing.contains(&7));
    assert!(builder.edge_head_node(1).unwrap().outgoing.contains(&9));
    assert!(builder.edge_head_node(1).unwrap().outgoing.contains(&5));
    assert_eq!(
        builder.edge_tail_node(7).unwrap().id(),
        builder.edge_head_node(1).unwrap().id()
    );
    // Second activity.
    assert_eq!(
        builder.edge_tail_node(2).unwrap().id(),
        builder.start_node().id()
    );
    assert_ne!(
        builder.edge_head_node(2).unwrap().id(),
        builder.end_node().id()
    );
    assert_eq!(builder.start_node().outgoing.len(), 2);
    assert!(builder.start_node().outgoing.contains(&2));
    assert_eq!(builder.edge_tail_node(2).unwrap().outgoing.len(), 2);
    assert!(builder.edge_tail_node(2).unwrap().outgoing.contains(&2));
    assert_eq!(builder.edge_head_node(2).unwrap().incoming.len(), 1);
    assert!(builder.edge_head_node(2).unwrap().incoming.contains(&2));
    assert_eq!(builder.edge_head_node(2).unwrap().outgoing.len(), 2);
    assert!(builder.edge_head_node(2).unwrap().outgoing.contains(&4));
    assert!(builder.edge_head_node(2).unwrap().outgoing.contains(&8));
    assert_eq!(
        builder.edge_tail_node(4).unwrap().id(),
        builder.edge_head_node(2).unwrap().id()
    );
    // Fourth activity.
    assert_eq!(builder.edge_tail_node(4).unwrap().incoming.len(), 1);
    assert!(builder.edge_tail_node(4).unwrap().incoming.contains(&2));
    assert_eq!(builder.edge_tail_node(4).unwrap().outgoing.len(), 2);
    assert!(builder.edge_tail_node(4).unwrap().outgoing.contains(&4));
    assert!(builder.edge_tail_node(4).unwrap().outgoing.contains(&8));
    assert_eq!(builder.edge_head_node(4).unwrap().incoming.len(), 2);
    assert!(builder.edge_head_node(4).unwrap().incoming.contains(&4));
    assert!(builder.edge_head_node(4).unwrap().incoming.contains(&9));
    assert_eq!(builder.edge_head_node(4).unwrap().outgoing.len(), 1);
    assert!(builder.edge_head_node(4).unwrap().outgoing.contains(&3));
    assert_eq!(
        builder.edge_tail_node(3).unwrap().id(),
        builder.edge_head_node(4).unwrap().id()
    );
    // First dummy activity.
    assert_eq!(builder.edge_tail_node(7).unwrap().incoming.len(), 1);
    assert!(builder.edge_tail_node(7).unwrap().incoming.contains(&1));
    assert_eq!(builder.edge_tail_node(7).unwrap().outgoing.len(), 3);
    assert!(builder.edge_tail_node(7).unwrap().outgoing.contains(&7));
    assert!(builder.edge_tail_node(7).unwrap().outgoing.contains(&9));
    assert!(builder.edge_tail_node(7).unwrap().outgoing.contains(&5));
    assert!(builder.edge(7).unwrap().content.is_dummy());
    assert_eq!(builder.edge_head_node(7).unwrap().incoming.len(), 4);
    assert!(builder.edge_head_node(7).unwrap().incoming.contains(&7));
    assert!(builder.edge_head_node(7).unwrap().incoming.contains(&8));
    assert!(builder.edge_head_node(7).unwrap().incoming.contains(&14));
    assert!(builder.edge_head_node(7).unwrap().incoming.contains(&3));
    assert_eq!(
        builder.edge_head_node(7).unwrap().id(),
        builder.end_node().id()
    );
    assert_ne!(
        builder.edge_tail_node(7).unwrap().id(),
        builder.start_node().id()
    );
    // Third activity.
    assert_eq!(builder.edge_tail_node(3).unwrap().incoming.len(), 2);
    assert!(builder.edge_tail_node(3).unwrap().incoming.contains(&9));
    assert!(builder.edge_tail_node(3).unwrap().incoming.contains(&4));
    assert_eq!(builder.edge_tail_node(3).unwrap().outgoing.len(), 1);
    assert!(builder.edge_tail_node(3).unwrap().outgoing.contains(&3));
    assert_eq!(builder.edge_head_node(3).unwrap().incoming.len(), 4);
    assert!(builder.edge_head_node(7).unwrap().incoming.contains(&7));
    assert!(builder.edge_head_node(7).unwrap().incoming.contains(&8));
    assert!(builder.edge_head_node(7).unwrap().incoming.contains(&14));
    assert!(builder.edge_head_node(3).unwrap().incoming.contains(&3));
    assert_eq!(builder.end_node().incoming.len(), 4);
    assert!(builder.end_node().incoming.contains(&7));
    assert!(builder.end_node().incoming.contains(&8));
    assert!(builder.end_node().incoming.contains(&14));
    assert!(builder.end_node().incoming.contains(&3));
    assert_eq!(
        builder.edge_head_node(3).unwrap().id(),
        builder.end_node().id()
    );
    assert_ne!(
        builder.edge_tail_node(3).unwrap().id(),
        builder.start_node().id()
    );
    // Fifth activity.
    assert_eq!(builder.edge_tail_node(5).unwrap().incoming.len(), 1);
    assert!(builder.edge_tail_node(5).unwrap().incoming.contains(&1));
    assert_eq!(builder.edge_tail_node(5).unwrap().outgoing.len(), 3);
    assert!(builder.edge_tail_node(5).unwrap().outgoing.contains(&5));
    assert!(builder.edge_tail_node(5).unwrap().outgoing.contains(&7));
    assert!(builder.edge_tail_node(5).unwrap().outgoing.contains(&9));
    assert_eq!(builder.edge_head_node(5).unwrap().incoming.len(), 1);
    assert!(builder.edge_head_node(5).unwrap().incoming.contains(&5));
    assert_eq!(builder.edge_head_node(5).unwrap().outgoing.len(), 1);
    assert!(builder.edge_head_node(5).unwrap().outgoing.contains(&14));
    assert_eq!(
        builder.edge_tail_node(14).unwrap().id(),
        builder.edge_head_node(5).unwrap().id()
    );
}

// -- Ancestor lookup ---------------------------------------------------------

#[test]
fn given_four_activities_one_dependent_on_other_three_get_ancestor_nodes_lookup_then_ancestors_as_expected(
) {
    let mut builder = new_builder(5, 0);
    builder.shuffle_processing_order = true;

    assert!(builder.add_activity(Act::new(1, 0)));
    assert!(builder.add_activity(Act::new(2, 0)));
    assert!(builder.add_activity(Act::new(3, 0)));
    assert!(builder.add_activity_with_dependencies(Act::new(4, 0), IndexSet::from([1, 2, 3])));

    let lookup = builder.get_ancestor_nodes_lookup().unwrap();

    // Start node (event 1).
    assert_eq!(lookup[&1].len(), 0);

    // End node (event 2).
    let end_node_ancestors = &lookup[&2];
    assert_eq!(end_node_ancestors.len(), 6);
    assert!(end_node_ancestors.contains(&1));
    assert!(end_node_ancestors.contains(&3));
    assert!(end_node_ancestors.contains(&4));
    assert!(end_node_ancestors.contains(&5));
    assert!(end_node_ancestors.contains(&6));
    assert!(end_node_ancestors.contains(&7));

    // Event 3.
    let event3_ancestors = &lookup[&3];
    assert_eq!(event3_ancestors.len(), 1);
    assert!(event3_ancestors.contains(&1));

    // Event 4.
    let event4_ancestors = &lookup[&4];
    assert_eq!(event4_ancestors.len(), 1);
    assert!(event4_ancestors.contains(&1));

    // Event 5.
    let event5_ancestors = &lookup[&5];
    assert_eq!(event5_ancestors.len(), 1);
    assert!(event5_ancestors.contains(&1));

    // Event 6.
    let event6_ancestors = &lookup[&6];
    assert_eq!(event6_ancestors.len(), 4);
    assert!(event6_ancestors.contains(&1));
    assert!(event6_ancestors.contains(&3));
    assert!(event6_ancestors.contains(&4));
    assert!(event6_ancestors.contains(&5));

    // Event 7.
    let event7_ancestors = &lookup[&7];
    assert_eq!(event7_ancestors.len(), 5);
    assert!(event7_ancestors.contains(&1));
    assert!(event7_ancestors.contains(&3));
    assert!(event7_ancestors.contains(&4));
    assert!(event7_ancestors.contains(&5));
    assert!(event7_ancestors.contains(&6));
}

// -- Constructor-from-graph tests --------------------------------------------

/// The 5-activity network shared by the constructor-from-graph tests: 1 and 2
/// are roots, 3 depends on 1 and 4, 4 on 2, 5 on 1.
fn build_standard_network() -> Builder {
    let mut builder = new_builder(6, 0);
    builder.shuffle_processing_order = true;
    builder.add_activity(Act::new(1, 0));
    builder.add_activity(Act::new(2, 0));
    builder.add_activity_with_dependencies(Act::new(3, 0), IndexSet::from([1, 4]));
    builder.add_activity_with_dependencies(Act::new(4, 0), IndexSet::from([2]));
    builder.add_activity_with_dependencies(Act::new(5, 0), IndexSet::from([1]));
    builder
}

fn assimilate(
    graph: zametek_maths_graphs_compilers::ArrowGraph<i32, i32, i32>,
) -> Result<Builder, zametek_maths_graphs_primitives::GraphError> {
    ArrowGraphBuilder::from_graph(graph, NextIdGenerator::new(6), NextIdGenerator::new(0))
}

#[test]
fn given_ctor_with_graph_then_graph_successfully_assimilated() {
    let mut builder = build_standard_network();
    let first_graph = builder.to_graph().unwrap();

    let mut builder2 = assimilate(first_graph.clone()).expect("assimilation should succeed");
    let second_graph = builder2.to_graph().unwrap();

    assert_eq!(second_graph, first_graph);
}

#[test]
fn given_ctor_with_graph_with_missing_edge_then_error() {
    let mut builder = build_standard_network();
    let mut graph = builder.to_graph().unwrap();

    graph.edges.remove(0);

    assert!(assimilate(graph).is_err());
}

#[test]
fn given_ctor_with_graph_with_too_many_edges_then_error() {
    let mut builder = build_standard_network();
    let mut graph = builder.to_graph().unwrap();

    // A fresh edge ID (no node references it). C# reuses `dummyActivityId.Next()`
    // which collides with an already-minted dummy edge; a non-colliding ID forces
    // the same "edges do not match" error without depending on how duplicate IDs
    // are folded.
    graph.edges.push(Edge::new(Act::new(100, 0)));

    assert!(assimilate(graph).is_err());
}

#[test]
fn given_ctor_with_graph_with_missing_node_then_error() {
    let mut builder = build_standard_network();
    let mut graph = builder.to_graph().unwrap();

    let normal_id = graph
        .nodes
        .iter()
        .find(|n| n.node_type() == NodeType::Normal)
        .unwrap()
        .id();
    graph.nodes.retain(|n| n.id() != normal_id);

    assert!(assimilate(graph).is_err());
}

#[test]
fn given_ctor_with_graph_with_too_many_nodes_then_error() {
    let mut builder = build_standard_network();
    let mut graph = builder.to_graph().unwrap();

    // A fresh Normal node with no edges: its ID appears among non-isolated nodes
    // but never among the edge endpoints, so assimilation rejects it.
    graph.nodes.push(Node::new(Event::new(100)));

    assert!(assimilate(graph).is_err());
}

#[test]
fn given_ctor_with_graph_with_no_start_node_then_error() {
    let mut builder = build_standard_network();
    let mut graph = builder.to_graph().unwrap();

    let node = graph
        .nodes
        .iter_mut()
        .find(|n| n.node_type() == NodeType::Start)
        .unwrap();
    node.set_node_type(NodeType::Normal);

    assert!(assimilate(graph).is_err());
}

#[test]
fn given_ctor_with_graph_with_no_end_node_then_error() {
    let mut builder = build_standard_network();
    let mut graph = builder.to_graph().unwrap();

    let node = graph
        .nodes
        .iter_mut()
        .find(|n| n.node_type() == NodeType::End)
        .unwrap();
    node.set_node_type(NodeType::Normal);

    assert!(assimilate(graph).is_err());
}

#[test]
fn given_ctor_with_graph_with_more_than_one_start_node_then_error() {
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

    assert!(assimilate(graph).is_err());
}

#[test]
fn given_ctor_with_graph_with_more_than_one_end_node_then_error() {
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

    assert!(assimilate(graph).is_err());
}

// -- Circular dependencies ---------------------------------------------------

/// Both circular-dependency tests expect the same two cycles, `{2, 4, 7}` and
/// `{5, 8, 9}`, order-independent within and between cycles.
fn assert_circular_dependencies(builder: &Builder) {
    let circular = builder.find_strong_circular_dependencies();
    assert_eq!(circular.len(), 2);

    let mut cycles: Vec<Vec<i32>> = circular
        .iter()
        .map(|c| {
            let mut ids = c.dependencies.to_vec();
            ids.sort();
            ids
        })
        .collect();
    cycles.sort();

    assert_eq!(cycles, vec![vec![2, 4, 7], vec![5, 8, 9]]);
}

#[test]
fn given_all_dummy_activities_find_circular_dependencies_then_finds_circular_dependency() {
    let mut builder = new_builder(100, 0);
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
fn given_find_circular_dependencies_then_finds_circular_dependency() {
    let mut builder = new_builder(100, 0);
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
