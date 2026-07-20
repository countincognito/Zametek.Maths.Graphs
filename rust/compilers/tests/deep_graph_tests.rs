//! Ports of `TarjanDeepGraphTests.cs`, `DeepDependencyWalkTests.cs` and
//! `DeepTransitiveReductionTests.cs`. The Rust traversals are iterative and the
//! ancestor sets are compact bitsets, so a chain deep enough to overflow a
//! recursive implementation (or exhaust memory with a hashset-per-node ancestor
//! lookup) is handled.

use indexmap::IndexSet;
use zametek_maths_graphs_compilers::{ArrowGraphBuilder, NextIdGenerator, VertexGraphBuilder};
use zametek_maths_graphs_primitives::DependentActivity;

type Act = DependentActivity<i32, i32, i32>;

const CHAIN_LENGTH: i32 = 20_000;
// Arrow graphs mint ~2 event nodes per activity, so a 10k activity chain reaches
// the same ~20k node depth at a quarter of the bitset cost.
const ARROW_CHAIN_LENGTH: i32 = 10_000;

// -- TarjanDeepGraphTests ----------------------------------------------------

#[test]
fn vertex_very_deep_dependency_chain_then_find_strong_circular_dependencies_completes() {
    let mut builder = VertexGraphBuilder::<i32, i32, i32>::new(NextIdGenerator::new(0));
    builder.add_activity(Act::new(1, 1));
    for id in 2..=CHAIN_LENGTH {
        builder.add_activity_with_dependencies(Act::new(id, 1), IndexSet::from([id - 1]));
    }
    assert!(builder.find_strong_circular_dependencies().is_empty());
}

#[test]
fn vertex_very_deep_chain_closed_into_cycle_then_cycle_is_found() {
    let mut builder = VertexGraphBuilder::<i32, i32, i32>::new(NextIdGenerator::new(0));
    // Close the chain into one giant cycle: 1 depends on the final activity.
    builder.add_activity_with_dependencies(Act::new(1, 1), IndexSet::from([CHAIN_LENGTH]));
    for id in 2..=CHAIN_LENGTH {
        builder.add_activity_with_dependencies(Act::new(id, 1), IndexSet::from([id - 1]));
    }
    let output = builder.find_strong_circular_dependencies();
    assert_eq!(output.len(), 1);
    assert_eq!(output[0].dependencies.len(), CHAIN_LENGTH as usize);
}

// -- DeepDependencyWalkTests -------------------------------------------------

#[test]
fn vertex_very_deep_dummy_chain_then_strong_dependency_resolves_real_root() {
    let mut builder = VertexGraphBuilder::<i32, i32, i32>::new(NextIdGenerator::new(0));
    builder.add_activity(Act::new(1, 1));
    for id in 2..=CHAIN_LENGTH {
        builder.add_activity_with_dependencies(Act::new(id, 0), IndexSet::from([id - 1]));
    }
    assert_eq!(
        builder.strong_activity_dependency_ids(CHAIN_LENGTH),
        vec![1]
    );
}

#[test]
fn arrow_very_deep_dependency_chain_then_builds_and_resolves() {
    let mut builder = ArrowGraphBuilder::<i32, i32, i32>::new(
        NextIdGenerator::new(CHAIN_LENGTH),
        NextIdGenerator::new(0),
    );
    builder.add_activity(Act::new(1, 1));
    for id in 2..=CHAIN_LENGTH {
        builder.add_activity_with_dependencies(Act::new(id, 1), IndexSet::from([id - 1]));
    }
    assert_eq!(
        builder.strong_activity_dependency_ids(CHAIN_LENGTH),
        vec![CHAIN_LENGTH - 1]
    );
}

// -- DeepTransitiveReductionTests --------------------------------------------

#[test]
fn vertex_very_deep_chain_with_redundant_shortcut_then_reduction_removes_shortcut() {
    let mut builder = VertexGraphBuilder::<i32, i32, i32>::new(NextIdGenerator::new(0));
    builder.add_activity(Act::new(1, 1));
    for id in 2..CHAIN_LENGTH {
        builder.add_activity_with_dependencies(Act::new(id, 1), IndexSet::from([id - 1]));
    }
    // The final activity depends on its predecessor AND (redundantly) on the root.
    builder.add_activity_with_dependencies(
        Act::new(CHAIN_LENGTH, 1),
        IndexSet::from([CHAIN_LENGTH - 1, 1]),
    );

    assert!(builder.transitive_reduction());

    assert_eq!(
        builder.activity_dependency_ids(CHAIN_LENGTH),
        vec![CHAIN_LENGTH - 1]
    );
}

#[test]
fn arrow_very_deep_chain_with_redundant_shortcut_then_reduction_removes_shortcut() {
    let mut builder = ArrowGraphBuilder::<i32, i32, i32>::new(
        NextIdGenerator::new(ARROW_CHAIN_LENGTH),
        NextIdGenerator::new(0),
    );
    builder.add_activity(Act::new(1, 1));
    for id in 2..ARROW_CHAIN_LENGTH {
        builder.add_activity_with_dependencies(Act::new(id, 1), IndexSet::from([id - 1]));
    }
    builder.add_activity_with_dependencies(
        Act::new(ARROW_CHAIN_LENGTH, 1),
        IndexSet::from([ARROW_CHAIN_LENGTH - 1, 1]),
    );

    assert!(builder.transitive_reduction().unwrap());

    assert_eq!(
        builder.strong_activity_dependency_ids(ARROW_CHAIN_LENGTH),
        vec![ARROW_CHAIN_LENGTH - 1]
    );
}
