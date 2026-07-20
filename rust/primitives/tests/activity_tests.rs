//! Ports of `Zametek.Maths.Graphs.Primitives.Tests/Entities/ActivityTests.cs`.

use indexmap::IndexSet;
use zametek_maths_graphs_primitives::{Activity, LogicalOperator};

type Act = Activity<i32, i32, i32>;

#[test]
fn activity_given_ctor_then_properties_set_and_collections_empty() {
    let activity = Act::new(1, 5);
    assert_eq!(activity.id(), 1);
    assert_eq!(activity.duration, 5);
    assert!(activity.target_work_streams.is_empty());
    assert!(activity.target_resources.is_empty());
    assert!(activity.allocated_to_resources.is_empty());
    assert!(!activity.can_be_removed());
}

#[test]
fn activity_given_earliest_finish_time_when_no_earliest_start_time_then_none() {
    let activity = Act::new(1, 5);
    assert_eq!(activity.earliest_finish_time(), None);
}

#[test]
fn activity_given_earliest_finish_time_when_earliest_start_time_set_then_start_plus_duration() {
    let mut activity = Act::new(1, 5);
    activity.earliest_start_time = Some(3);
    assert_eq!(activity.earliest_finish_time(), Some(8));
}

#[test]
fn activity_given_latest_start_time_when_latest_finish_time_set_then_finish_minus_duration() {
    let mut activity = Act::new(1, 5);
    activity.latest_finish_time = Some(12);
    assert_eq!(activity.latest_start_time(), Some(7));
}

#[test]
fn activity_given_total_slack_when_both_finish_times_available_then_latest_minus_earliest() {
    let mut activity = Act::new(1, 5);
    activity.earliest_start_time = Some(0); // EF = 5
    activity.latest_finish_time = Some(8);
    assert_eq!(activity.total_slack(), Some(3));
}

#[test]
fn activity_given_total_slack_when_times_missing_then_none() {
    let activity = Act::new(1, 5);
    assert_eq!(activity.total_slack(), None);
}

#[test]
fn activity_given_interfering_slack_then_total_slack_minus_free_slack() {
    let mut activity = Act::new(1, 5);
    activity.earliest_start_time = Some(0); // EF = 5
    activity.latest_finish_time = Some(8); // total slack = 3
    activity.free_slack = Some(1);
    assert_eq!(activity.interfering_slack(), Some(2));
}

#[test]
fn activity_given_interfering_slack_when_free_slack_missing_then_none() {
    let mut activity = Act::new(1, 5);
    activity.earliest_start_time = Some(0);
    activity.latest_finish_time = Some(8);
    assert_eq!(activity.interfering_slack(), None);
}

#[test]
fn activity_given_is_critical_when_zero_total_slack_then_true() {
    let mut activity = Act::new(1, 5);
    activity.earliest_start_time = Some(0);
    activity.latest_finish_time = Some(5);
    assert!(activity.is_critical());
}

#[test]
fn activity_given_is_critical_when_negative_total_slack_then_true() {
    // Over-constrained: latest finish before earliest finish.
    let mut activity = Act::new(1, 5);
    activity.earliest_start_time = Some(0);
    activity.latest_finish_time = Some(3);
    assert_eq!(activity.total_slack(), Some(-2));
    assert!(activity.is_critical());
}

#[test]
fn activity_given_is_critical_when_positive_total_slack_then_false() {
    let mut activity = Act::new(1, 5);
    activity.earliest_start_time = Some(0);
    activity.latest_finish_time = Some(8);
    assert!(!activity.is_critical());
}

#[test]
fn activity_given_is_critical_when_no_times_then_false() {
    let activity = Act::new(1, 5);
    assert!(!activity.is_critical());
}

#[test]
fn activity_given_is_dummy_when_zero_duration_then_true() {
    assert!(Act::new(1, 0).is_dummy());
    assert!(!Act::new(1, 1).is_dummy());
}

#[test]
fn activity_given_removable_ctor_then_can_be_removed() {
    assert!(Act::new_removable(1, 5, true).can_be_removed());
    assert!(!Act::new_removable(1, 5, false).can_be_removed());
}

#[test]
fn activity_given_set_as_removable_and_read_only_then_can_be_removed_toggles() {
    let mut activity = Act::new(1, 5);
    activity.set_as_removable();
    assert!(activity.can_be_removed());
    activity.set_as_read_only();
    assert!(!activity.can_be_removed());
}

#[test]
fn activity_given_clone_then_all_properties_preserved() {
    let mut activity = Act::new(1, 5);
    activity.name = Some("name".to_string());
    activity.notes = Some("notes".to_string());
    activity.target_resource_operator = LogicalOperator::Or;
    activity.has_no_cost = true;
    activity.has_no_billing = true;
    activity.has_no_effort = true;
    activity.earliest_start_time = Some(2);
    activity.latest_finish_time = Some(10);
    activity.free_slack = Some(1);
    activity.minimum_free_slack = Some(1);
    activity.minimum_earliest_start_time = Some(2);
    activity.maximum_latest_finish_time = Some(11);
    activity.target_work_streams.insert(21);
    activity.target_resources.insert(31);
    activity.allocated_to_resources.insert(41);
    activity.set_as_removable();

    let clone = activity.clone();

    assert_eq!(clone.id(), 1);
    assert_eq!(clone.duration, 5);
    assert_eq!(clone.name.as_deref(), Some("name"));
    assert_eq!(clone.notes.as_deref(), Some("notes"));
    assert_eq!(clone.target_resource_operator, LogicalOperator::Or);
    assert!(clone.has_no_cost);
    assert!(clone.has_no_billing);
    assert!(clone.has_no_effort);
    assert_eq!(clone.earliest_start_time, Some(2));
    assert_eq!(clone.latest_finish_time, Some(10));
    assert_eq!(clone.free_slack, Some(1));
    assert_eq!(clone.minimum_free_slack, Some(1));
    assert_eq!(clone.minimum_earliest_start_time, Some(2));
    assert_eq!(clone.maximum_latest_finish_time, Some(11));
    assert_eq!(clone.target_work_streams, IndexSet::from([21]));
    assert_eq!(clone.target_resources, IndexSet::from([31]));
    assert_eq!(clone.allocated_to_resources, IndexSet::from([41]));
    assert!(clone.can_be_removed());
}

#[test]
fn activity_given_clone_then_collections_are_independent_copies() {
    let mut activity = Act::new(1, 5);
    activity.target_resources.insert(31);

    let mut clone = activity.clone();
    clone.target_resources.insert(32);

    assert_eq!(activity.target_resources.len(), 1);
    assert_eq!(clone.target_resources.len(), 2);
}
