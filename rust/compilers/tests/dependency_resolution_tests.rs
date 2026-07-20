//! Ports of `DependencyResolutionTests.cs`: the transitive reducer's shared-
//! ancestor handling and the strong-dependency walk's dummy-chain resolution,
//! exercised through the public builder API.

use indexmap::IndexSet;
use zametek_maths_graphs_compilers::{ArrowGraphBuilder, NextIdGenerator, VertexGraphBuilder};
use zametek_maths_graphs_primitives::DependentActivity;

type Act = DependentActivity<i32, i32, i32>;

#[test]
fn vertex_shared_ancestor_across_two_sinks_then_reduction_removes_redundant_edges_from_both() {
    // A -> B, and two sinks each depending on BOTH A (redundantly) and B.
    let mut builder = VertexGraphBuilder::<i32, i32, i32>::new(NextIdGenerator::new(0));
    assert!(builder.add_activity(Act::new(1, 1))); // A
    builder.add_activity_with_dependencies(Act::new(2, 1), IndexSet::from([1])); // B dep A
    builder.add_activity_with_dependencies(Act::new(3, 1), IndexSet::from([1, 2])); // D1 dep A, B
    builder.add_activity_with_dependencies(Act::new(4, 1), IndexSet::from([1, 2])); // D2 dep A, B

    assert!(builder.transitive_reduction());

    assert_eq!(builder.activity_dependency_ids(3), vec![2]);
    assert_eq!(builder.activity_dependency_ids(4), vec![2]);
    assert_eq!(builder.activity_dependency_ids(2), vec![1]);
}

#[test]
fn vertex_real_root_behind_dummy_diamond_then_strong_dependency_resolves_real_root() {
    // A (real) behind two zero-duration links B and C, which D depends on.
    let mut builder = VertexGraphBuilder::<i32, i32, i32>::new(NextIdGenerator::new(0));
    assert!(builder.add_activity(Act::new(1, 1)));
    builder.add_activity_with_dependencies(Act::new(2, 0), IndexSet::from([1]));
    builder.add_activity_with_dependencies(Act::new(3, 0), IndexSet::from([1]));
    builder.add_activity_with_dependencies(Act::new(4, 1), IndexSet::from([2, 3]));

    let mut strong = builder.strong_activity_dependency_ids(4);
    strong.sort();
    strong.dedup();
    assert_eq!(strong, vec![1]);
}

#[test]
fn arrow_real_root_behind_dummy_diamond_then_strong_dependency_resolves_real_root() {
    let mut builder =
        ArrowGraphBuilder::<i32, i32, i32>::new(NextIdGenerator::new(100), NextIdGenerator::new(0));
    assert!(builder.add_activity(Act::new(1, 1)));
    builder.add_activity_with_dependencies(Act::new(2, 0), IndexSet::from([1]));
    builder.add_activity_with_dependencies(Act::new(3, 0), IndexSet::from([1]));
    builder.add_activity_with_dependencies(Act::new(4, 1), IndexSet::from([2, 3]));

    let mut strong = builder.strong_activity_dependency_ids(4);
    strong.sort();
    strong.dedup();
    assert_eq!(strong, vec![1]);
}
