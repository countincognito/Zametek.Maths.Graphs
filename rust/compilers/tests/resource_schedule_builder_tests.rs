//! Ports of `ResourceScheduleBuilderTests.cs`.
//!
//! The last two C# tests (`ResourceSchedule2_ForIndirectResource_WithPhase` and
//! `ResourceSchedule3_ForDirectAndIndirectResources_WithPhases`) are driven by
//! the ~1200-line `ResourceSchedule3.json` fixture deserialized through
//! Newtonsoft. This port has no JSON/serde infrastructure, so those two are
//! omitted; the `ResourceSchedule1`/`ResourceSchedule2` fixtures (single Direct
//! resource) are small enough to hand-code, and the two pure-logic tests need no
//! fixture at all.

use zametek_maths_graphs_compilers::ResourceScheduleBuilder;
use zametek_maths_graphs_primitives::{
    Activity, InterActivityAllocationType, PackedBoolList, Resource, ScheduledActivity,
};

type Rsb = ResourceScheduleBuilder<i32, i32, i32>;

/// Asserts a stream is unallocated before `start`, allocated from `start` up to
/// `finish`, and unallocated thereafter. Expressed over the iterator because a
/// bit-packed stream cannot hand out `&[bool]` slices.
fn assert_allocated_between(allocation: &PackedBoolList, start: usize, finish: usize) {
    assert!(
        allocation.iter().take(start).all(|x| !x),
        "expected nothing allocated before {start}"
    );
    assert!(
        allocation
            .iter()
            .skip(start)
            .take(finish - start)
            .all(|x| x),
        "expected everything allocated between {start} and {finish}"
    );
    assert!(
        allocation.iter().skip(finish).all(|x| !x),
        "expected nothing allocated after {finish}"
    );
}

fn sched(id: i32, name: &str, duration: i32, start: i32, finish: i32) -> ScheduledActivity<i32> {
    ScheduledActivity::new(
        id,
        Some(name.to_string()),
        false,
        false,
        false,
        duration,
        start,
        finish,
    )
}

#[test]
fn resource_schedule1_for_indirect_resource_zero_finish_time_then_activity_allocation_empty() {
    let start_time = 0;
    let finish_time = 0;
    let resource = Resource::new(
        1,
        Some(String::new()),
        false,
        false,
        InterActivityAllocationType::Indirect,
        1.0,
        1.0,
        0,
        [],
    );

    let rsb: Rsb = ResourceScheduleBuilder::new(resource.clone());
    let rs = rsb
        .to_resource_schedule(&[], start_time, finish_time)
        .unwrap();

    assert!(rs.resource_allocation.is_empty());
    assert_eq!(rs.finish_time, finish_time);
    assert_eq!(rs.resource.as_ref(), Some(&resource));
    assert!(rs.scheduled_activities.is_empty());
}

#[test]
fn resource_schedule1_for_indirect_resource_large_finish_time_then_activity_allocation_full() {
    let start_time = 0;
    let finish_time = 10;
    let resource = Resource::new(
        1,
        Some(String::new()),
        false,
        false,
        InterActivityAllocationType::Indirect,
        1.0,
        1.0,
        0,
        [],
    );

    let rsb: Rsb = ResourceScheduleBuilder::new(resource.clone());
    let rs = rsb
        .to_resource_schedule(&[], start_time, finish_time)
        .unwrap();

    assert_eq!(rs.resource_allocation.len(), 10);
    assert_eq!(rs.finish_time, finish_time);
    assert_eq!(rs.resource.as_ref(), Some(&resource));
    assert!(rs.scheduled_activities.is_empty());
}

#[test]
fn resource_schedule1_for_direct_resource_then_start_73_and_finish_127() {
    let start = 73;
    let finish = 127;
    let start_time = 0;
    let finish_time = 150;

    // The `ResourceSchedule1.json` fixture: resource "Tom" (Direct) with the
    // activities scheduled between times 73 and 127.
    let resource = Resource::new(
        4,
        Some("Tom".to_string()),
        true,
        false,
        InterActivityAllocationType::Direct,
        1.0,
        1.0,
        0,
        [],
    );
    let scheduled = [
        sched(131, "Migration Prep", 1, 73, 74),
        sched(30, "DB backup", 1, 74, 75),
        sched(34, "Install APP", 1, 75, 76),
        sched(72, "DB backup", 2, 76, 78),
        sched(73, "Transfer DB", 2, 78, 80),
        sched(74, "Import data to server", 1, 80, 81),
        sched(75, "Validate DBs after import", 1, 81, 82),
        sched(31, "Transfer DB", 1, 82, 83),
        sched(35, "Validate Install", 1, 83, 84),
        sched(32, "Import data to server", 1, 84, 85),
        sched(36, "Document installation", 1, 85, 86),
        sched(33, "Validate DBs after import", 1, 86, 87),
        sched(37, "Login to application", 1, 87, 88),
        sched(38, "Smoke tests", 1, 88, 89),
        sched(147, "Test application", 1, 103, 104),
        sched(39, "Failover", 1, 111, 112),
        sched(46, "Change DNS", 1, 126, 127),
    ];

    let mut rsb: Rsb = ResourceScheduleBuilder::new(resource);
    for scheduled_activity in scheduled {
        rsb.append_scheduled_activity(scheduled_activity).unwrap();
    }
    let rs = rsb
        .to_resource_schedule(&[], start_time, finish_time)
        .unwrap();

    assert_allocated_between(&rs.resource_allocation, start, finish);
}

#[test]
fn resource_schedule2_for_direct_resource_then_start_73_and_finish_101() {
    let start = 73;
    let finish = 101;
    let start_time = 0;
    let finish_time = 150;

    // The `ResourceSchedule2.json` fixture: resource "Steve" (Direct) with a gap
    // between time 79 and 100 that `fill_between` bridges as allocated.
    let resource = Resource::new(
        5,
        Some("Steve".to_string()),
        true,
        false,
        InterActivityAllocationType::Direct,
        1.0,
        1.0,
        0,
        [],
    );
    let scheduled = [
        sched(50, "Migration Prep", 1, 73, 74),
        sched(51, "DB backup", 1, 74, 75),
        sched(52, "Transfer DB", 1, 75, 76),
        sched(53, "Import data to server", 1, 76, 77),
        sched(54, "Validate DBs after import", 1, 77, 78),
        sched(59, "Smoke tests", 1, 78, 79),
        sched(66, "Test application", 1, 100, 101),
    ];

    let mut rsb: Rsb = ResourceScheduleBuilder::new(resource);
    for scheduled_activity in scheduled {
        rsb.append_scheduled_activity(scheduled_activity).unwrap();
    }
    let rs = rsb
        .to_resource_schedule(&[], start_time, finish_time)
        .unwrap();

    assert_allocated_between(&rs.resource_allocation, start, finish);
}

// The scheduler reads `first_activity_start_time` instead of taking a minimum
// over each builder's whole schedule. That substitution is only valid because
// activities are appended in non-decreasing start-time order, which
// `append_activity` enforces by clamping a start time up to the resource's
// earliest availability. These pin both halves of it: the clamp, and the
// equivalence it licenses.
//
// Ports of the C# `ResourceScheduleBuilderTests` cases of the same names.

#[test]
fn given_no_scheduled_activities_then_first_activity_start_time_is_zero() {
    let rsb: Rsb = ResourceScheduleBuilder::new_unmapped();

    assert_eq!(rsb.first_activity_start_time(), 0);
    assert_eq!(rsb.last_activity_finish_time(), 0);
}

#[test]
fn given_activities_appended_out_of_order_then_first_activity_start_time_equals_the_minimum() {
    let resource = Resource::new(
        1,
        None,
        false,
        false,
        InterActivityAllocationType::None,
        1.0,
        1.0,
        0,
        [],
    );
    let mut rsb: Rsb = ResourceScheduleBuilder::new(resource);

    // The second and third ask to start before the resource is free, so the
    // builder clamps them forward. Nothing can therefore land ahead of the first.
    rsb.append_activity(&Activity::new(1, 5), 7);
    rsb.append_activity(&Activity::new(2, 3), 0);
    rsb.append_activity(&Activity::new(3, 4), 2);

    let minimum_start_time = rsb
        .scheduled_activities()
        .iter()
        .map(|x| x.start_time)
        .min()
        .expect("the schedule is not empty");
    let maximum_finish_time = rsb
        .scheduled_activities()
        .iter()
        .map(|x| x.finish_time)
        .max()
        .expect("the schedule is not empty");

    assert_eq!(rsb.first_activity_start_time(), minimum_start_time);
    assert_eq!(rsb.first_activity_start_time(), 7);
    assert_eq!(rsb.last_activity_finish_time(), maximum_finish_time);
}
