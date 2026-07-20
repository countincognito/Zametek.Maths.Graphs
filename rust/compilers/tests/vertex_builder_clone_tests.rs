//! Ports of `VertexGraphBuilderCloneTests.cs`. Regression cover for the clone
//! path: a cloned vertex builder must keep its edge events removable, so
//! transitive reduction still removes redundant edges on the clone.

use indexmap::IndexSet;
use zametek_maths_graphs_compilers::{NextIdGenerator, VertexGraphBuilder};
use zametek_maths_graphs_primitives::DependentActivity;

type Act = DependentActivity<i32, i32, i32>;

/// 1 -> 2 -> 3 plus the redundant direct dependency 1 -> 3.
fn build_redundant_triangle() -> VertexGraphBuilder<i32, i32, i32> {
    let mut builder = VertexGraphBuilder::<i32, i32, i32>::new(NextIdGenerator::default());
    builder.add_activity(Act::new(1, 5));
    builder.add_activity_with_dependencies(Act::new(2, 5), IndexSet::from([1]));
    builder.add_activity_with_dependencies(Act::new(3, 5), IndexSet::from([1, 2]));
    builder
}

#[test]
fn clone_object_then_edge_events_remain_removable() {
    let mut builder = build_redundant_triangle();
    assert!(builder.edges().all(|x| x.content.can_be_removed()));

    let clone = builder.clone_builder().unwrap();

    assert!(clone.edges().all(|x| x.content.can_be_removed()));
}

#[test]
fn clone_object_then_transitive_reduction_still_removes_redundant_edges() {
    let mut builder = build_redundant_triangle();
    assert_eq!(builder.edge_ids().len(), 3);

    let mut clone = builder.clone_builder().unwrap();

    assert!(clone.transitive_reduction());

    // The redundant 1 -> 3 edge must be removed on the clone, exactly as it
    // would be on the original.
    assert_eq!(clone.edge_ids().len(), 2);
}
