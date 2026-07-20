//! Ports of `.../Entities/ScheduledActivityTests.cs`.

use zametek_maths_graphs_primitives::ScheduledActivity;

#[test]
fn scheduled_activity_given_ctor_then_properties_set() {
    let scheduled =
        ScheduledActivity::<i32>::new(1, Some("name".to_string()), true, true, true, 5, 3, 8);
    assert_eq!(scheduled.id, 1);
    assert_eq!(scheduled.name.as_deref(), Some("name"));
    assert!(scheduled.has_no_cost);
    assert!(scheduled.has_no_billing);
    assert!(scheduled.has_no_effort);
    assert_eq!(scheduled.duration, 5);
    assert_eq!(scheduled.start_time, 3);
    assert_eq!(scheduled.finish_time, 8);
}

#[test]
fn scheduled_activity_given_clone_then_all_properties_preserved() {
    let scheduled =
        ScheduledActivity::<i32>::new(1, Some("name".to_string()), false, true, false, 5, 3, 8);
    let clone = scheduled.clone();
    assert_eq!(clone.id, 1);
    assert_eq!(clone.name.as_deref(), Some("name"));
    assert!(!clone.has_no_cost);
    assert!(clone.has_no_billing);
    assert!(!clone.has_no_effort);
    assert_eq!(clone.duration, 5);
    assert_eq!(clone.start_time, 3);
    assert_eq!(clone.finish_time, 8);
}
