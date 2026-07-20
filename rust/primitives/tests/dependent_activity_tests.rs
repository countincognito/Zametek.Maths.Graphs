//! Ports of `.../Entities/DependentActivityTests.cs`. The null-dependencies
//! constructor guard has no Rust counterpart (the argument is non-nullable by
//! type) and is omitted.

use indexmap::IndexSet;
use zametek_maths_graphs_primitives::DependentActivity;

type Dep = DependentActivity<i32, i32, i32>;

#[test]
fn dependent_activity_given_ctor_then_dependency_sets_empty() {
    let activity = Dep::new(1, 5);
    assert!(activity.dependencies.is_empty());
    assert!(activity.planning_dependencies.is_empty());
    assert!(activity.resource_dependencies.is_empty());
    assert!(activity.successors.is_empty());
}

#[test]
fn dependent_activity_given_ctor_with_dependencies_then_dependencies_copied() {
    let activity = Dep::with_dependencies(3, 8, [1, 2]);
    assert_eq!(activity.dependencies, IndexSet::from([1, 2]));
}

#[test]
fn dependent_activity_given_clone_then_dependency_sets_preserved() {
    let mut activity = Dep::with_dependencies(3, 8, [1, 2]);
    activity.planning_dependencies.insert(4);
    activity.resource_dependencies.insert(5);
    activity.successors.insert(6);

    let clone = activity.clone();

    assert_eq!(clone.dependencies, IndexSet::from([1, 2]));
    assert_eq!(clone.planning_dependencies, IndexSet::from([4]));
    assert_eq!(clone.resource_dependencies, IndexSet::from([5]));
    assert_eq!(clone.successors, IndexSet::from([6]));
}

#[test]
fn dependent_activity_given_clone_then_dependency_sets_are_independent_copies() {
    let activity = Dep::with_dependencies(3, 8, [1]);

    let mut clone = activity.clone();
    clone.dependencies.insert(2);

    assert_eq!(activity.dependencies.len(), 1);
    assert_eq!(clone.dependencies.len(), 2);
}

#[test]
fn dependent_activity_given_clone_then_base_activity_properties_preserved() {
    let mut activity = Dep::new(3, 8);
    activity.name = Some("name".to_string());
    activity.earliest_start_time = Some(1);
    activity.latest_finish_time = Some(12);
    activity.free_slack = Some(2);
    activity.set_as_removable();

    let clone = activity.clone();

    assert_eq!(clone.name.as_deref(), Some("name"));
    assert_eq!(clone.earliest_start_time, Some(1));
    assert_eq!(clone.latest_finish_time, Some(12));
    assert_eq!(clone.free_slack, Some(2));
    assert!(clone.can_be_removed());
}
