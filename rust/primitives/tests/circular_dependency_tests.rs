//! Ports of `.../Entities/CircularDependencyTests.cs`. Equality is
//! order-insensitive, as in C#.

use zametek_maths_graphs_primitives::CircularDependency;

#[test]
fn circular_dependency_given_ctor_then_dependencies_copied() {
    let circular = CircularDependency::new([1, 2, 3]);
    assert_eq!(circular.dependencies.len(), 3);
    assert!(circular.dependencies.contains(&1));
    assert!(circular.dependencies.contains(&2));
    assert!(circular.dependencies.contains(&3));
}

#[test]
fn circular_dependency_given_equals_when_same_dependencies_then_equal_regardless_of_order() {
    let circular1 = CircularDependency::new([1, 2, 3]);
    let circular2 = CircularDependency::new([3, 2, 1]);
    assert_eq!(circular1, circular2);
}

#[test]
fn circular_dependency_given_equals_when_different_dependencies_then_not_equal() {
    let circular1 = CircularDependency::new([1, 2]);
    let circular2 = CircularDependency::new([1, 3]);
    assert_ne!(circular1, circular2);
}
