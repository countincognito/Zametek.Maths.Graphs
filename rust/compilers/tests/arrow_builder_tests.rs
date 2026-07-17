//! Ports of representative tests from
//! `Zametek.Maths.Graphs.Compilers.Tests/Builders/ArrowGraphBuilderTests.cs`.
//! ID expectations are copied verbatim from the C# tests (same generators,
//! same seeds), so the graph structure matches edge for edge.

use indexmap::IndexSet;
use zametek_maths_graphs_compilers::{ArrowGraphBuilder, ArrowGraphCompiler, IdGenerator};
use zametek_maths_graphs_primitives::{DependentActivity, NodeType};

type Builder = ArrowGraphBuilder<i32, i32, i32>;
type Act = DependentActivity<i32, i32, i32>;

fn new_builder(dummy_activity_id: i32, event_id: i32) -> Builder {
    Builder::new(
        IdGenerator::Next(dummy_activity_id),
        IdGenerator::Next(event_id),
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
