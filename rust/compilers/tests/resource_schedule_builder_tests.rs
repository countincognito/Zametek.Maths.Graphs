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
use zametek_maths_graphs_primitives::{InterActivityAllocationType, Resource, ScheduledActivity};

type Rsb = ResourceScheduleBuilder<i32, i32, i32>;

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

    assert!(rs.resource_allocation[..start].iter().all(|&x| !x));
    assert!(rs.resource_allocation[start..finish].iter().all(|&x| x));
    assert!(rs.resource_allocation[finish..].iter().all(|&x| !x));
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

    assert!(rs.resource_allocation[..start].iter().all(|&x| !x));
    assert!(rs.resource_allocation[start..finish].iter().all(|&x| x));
    assert!(rs.resource_allocation[finish..].iter().all(|&x| !x));
}
