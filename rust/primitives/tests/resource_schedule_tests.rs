//! Ports of `.../Entities/ResourceScheduleTests.cs`. The null-scheduled-
//! activities constructor guard has no Rust counterpart and is omitted;
//! `ResourceSchedule` is a plain struct built with a struct literal.

use zametek_maths_graphs_primitives::{
    InterActivityAllocationType, Resource, ResourceSchedule, ScheduledActivity,
};

fn build_schedule(resource: Option<Resource<i32, i32>>) -> ResourceSchedule<i32, i32, i32> {
    ResourceSchedule {
        resource,
        scheduled_activities: vec![ScheduledActivity::new(
            1,
            Some("a".to_string()),
            false,
            false,
            false,
            5,
            0,
            5,
        )],
        start_time: 0,
        finish_time: 5,
        resource_allocation: vec![true, true, true, true, true],
        cost_allocation: vec![true, true, true, false, false],
        billing_allocation: vec![false, true, true, true, false],
        effort_allocation: vec![true, false, true, false, true],
        activity_allocation: vec![true, true, false, false, true],
    }
}

#[test]
fn resource_schedule_given_without_resource_then_resource_is_none() {
    let schedule = build_schedule(None);
    assert!(schedule.resource.is_none());
    assert_eq!(schedule.start_time, 0);
    assert_eq!(schedule.finish_time, 5);
}

#[test]
fn resource_schedule_given_clone_then_all_properties_preserved() {
    let resource = Resource::new(
        10,
        Some("R1".to_string()),
        false,
        false,
        InterActivityAllocationType::Direct,
        1.0,
        1.0,
        0,
        Vec::<i32>::new(),
    );
    let schedule = build_schedule(Some(resource));

    let clone = schedule.clone();

    assert_eq!(clone.resource.as_ref().unwrap().id, 10);
    assert_eq!(clone.scheduled_activities.len(), 1);
    assert_eq!(clone.scheduled_activities[0].id, 1);
    assert_eq!(clone.start_time, 0);
    assert_eq!(clone.finish_time, 5);
    assert_eq!(clone.resource_allocation, schedule.resource_allocation);
    assert_eq!(clone.cost_allocation, schedule.cost_allocation);
    assert_eq!(clone.billing_allocation, schedule.billing_allocation);
    assert_eq!(clone.effort_allocation, schedule.effort_allocation);
    assert_eq!(clone.activity_allocation, schedule.activity_allocation);
}

#[test]
fn resource_schedule_given_clone_when_no_resource_then_clone_has_none() {
    let schedule = build_schedule(None);
    let clone = schedule.clone();
    assert!(clone.resource.is_none());
}
