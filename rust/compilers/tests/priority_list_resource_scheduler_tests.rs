//! Ports of `PriorityListResourceSchedulerTests.cs`. The C# null-argument
//! variants (null priority list, null filtered resources, null graph) have no
//! Rust counterpart - the arguments are slices and a `&mut dyn` trait object,
//! which cannot be null - and are omitted.

use indexmap::{IndexMap, IndexSet};
use zametek_maths_graphs_compilers::contracts::{
    IResourceSchedulingEngine, IResourceSchedulingGraph,
};
use zametek_maths_graphs_compilers::PriorityListResourceScheduler;
use zametek_maths_graphs_primitives::{
    Activity, DependentActivity, InterActivityAllocationType, LogicalOperator, Resource,
    ResourceSchedule,
};

/// Test double for the read-only graph view the scheduler operates on. The C#
/// fake is delegate-backed; the Rust trait hands out `&`/`&mut DependentActivity`,
/// so this stores the activities in a map and derives `clone_activities` from it.
struct FakeSchedulingGraph {
    activities: IndexMap<i32, DependentActivity<i32, i32, i32>>,
    strong_dependencies: IndexMap<i32, Vec<i32>>,
}

impl FakeSchedulingGraph {
    fn new() -> Self {
        Self {
            activities: IndexMap::new(),
            strong_dependencies: IndexMap::new(),
        }
    }

    fn with_activity(mut self, activity: DependentActivity<i32, i32, i32>) -> Self {
        self.activities.insert(activity.id(), activity);
        self
    }
}

impl IResourceSchedulingGraph<i32, i32, i32> for FakeSchedulingGraph {
    fn activity(&self, id: i32) -> &DependentActivity<i32, i32, i32> {
        &self.activities[&id]
    }

    fn activity_mut(&mut self, id: i32) -> &mut DependentActivity<i32, i32, i32> {
        self.activities.get_mut(&id).expect("activity must exist")
    }

    fn strong_activity_dependency_ids(&self, id: i32) -> Vec<i32> {
        self.strong_dependencies
            .get(&id)
            .cloned()
            .unwrap_or_default()
    }

    fn clone_activities(&self) -> Vec<DependentActivity<i32, i32, i32>> {
        self.activities.values().cloned().collect()
    }
}

// -- CalculateResourceSchedules ----------------------------------------------

#[test]
fn calculate_resource_schedules_with_empty_priority_list_and_no_resources_then_returns_empty() {
    let scheduler = PriorityListResourceScheduler;
    let mut graph = FakeSchedulingGraph::new();

    let output = scheduler
        .calculate_resource_schedules(&[], &[], false, &mut graph)
        .unwrap();

    assert!(output.is_empty());
}

#[test]
fn calculate_resource_schedules_with_single_activity_and_single_resource_then_schedules_activity() {
    let scheduler = PriorityListResourceScheduler;
    let mut activity = DependentActivity::new(1, 5);
    activity.earliest_start_time = Some(0);
    let resource = Resource::new(
        10,
        Some("R1".to_string()),
        false,
        false,
        InterActivityAllocationType::Direct,
        1.0,
        1.0,
        0,
        [],
    );
    let mut graph = FakeSchedulingGraph::new().with_activity(activity);

    let schedules = scheduler
        .calculate_resource_schedules(&[1], &[resource], false, &mut graph)
        .unwrap();

    assert_eq!(schedules.len(), 1);
    assert_eq!(schedules[0].resource.as_ref().unwrap().id, 10);
    assert!(schedules[0].scheduled_activities.iter().any(|x| x.id == 1));
}

#[test]
fn calculate_resource_schedules_with_infinite_resources_then_spawns_resources_for_all_activities() {
    let scheduler = PriorityListResourceScheduler;
    let mut a1 = DependentActivity::new(1, 5);
    a1.earliest_start_time = Some(0);
    let mut a2 = DependentActivity::new(2, 5);
    a2.earliest_start_time = Some(0);
    let mut graph = FakeSchedulingGraph::new()
        .with_activity(a1)
        .with_activity(a2);

    let schedules = scheduler
        .calculate_resource_schedules(&[1, 2], &[], true, &mut graph)
        .unwrap();

    assert!(!schedules.is_empty());
    let mut ids: Vec<i32> = schedules
        .iter()
        .flat_map(|x| x.scheduled_activities.iter().map(|y| y.id))
        .collect();
    ids.sort();
    assert_eq!(ids, vec![1, 2]);
}

// -- GatherUnavailableResources ----------------------------------------------

#[test]
fn gather_unavailable_resources_with_no_target_resources_then_returns_empty() {
    let scheduler = PriorityListResourceScheduler;
    let activity: Activity<i32, i32, i32> = Activity::new(1, 5);

    let output = scheduler.gather_unavailable_resources(&[&activity], &[]);

    assert!(output.is_empty());
}

#[test]
fn gather_unavailable_resources_with_and_operator_and_missing_resource_then_returns_activity_with_missing_ids(
) {
    let scheduler = PriorityListResourceScheduler;
    let mut activity: Activity<i32, i32, i32> = Activity::new(1, 5);
    activity.target_resource_operator = LogicalOperator::And;
    activity.target_resources.insert(10);
    activity.target_resources.insert(20);
    let resources = vec![Resource::new(
        10,
        Some("R10".to_string()),
        false,
        false,
        InterActivityAllocationType::Direct,
        1.0,
        1.0,
        0,
        [],
    )];

    let output = scheduler.gather_unavailable_resources(&[&activity], &resources);

    assert_eq!(output.len(), 1);
    assert_eq!(output[0].id, 1);
    assert!(output[0].resource_ids.contains(&20));
    assert!(!output[0].resource_ids.contains(&10));
}

#[test]
fn gather_unavailable_resources_with_or_operator_and_all_missing_then_returns_activity_with_all_ids(
) {
    let scheduler = PriorityListResourceScheduler;
    let mut activity: Activity<i32, i32, i32> = Activity::new(1, 5);
    activity.target_resource_operator = LogicalOperator::Or;
    activity.target_resources.insert(10);
    activity.target_resources.insert(20);
    let resources = vec![Resource::new(
        30,
        Some("R30".to_string()),
        false,
        false,
        InterActivityAllocationType::Direct,
        1.0,
        1.0,
        0,
        [],
    )];

    let output = scheduler.gather_unavailable_resources(&[&activity], &resources);

    assert_eq!(output.len(), 1);
    assert_eq!(output[0].id, 1);
    assert!(output[0].resource_ids.contains(&10));
    assert!(output[0].resource_ids.contains(&20));
}

#[test]
fn gather_unavailable_resources_with_or_operator_and_partial_match_then_returns_empty() {
    let scheduler = PriorityListResourceScheduler;
    let mut activity: Activity<i32, i32, i32> = Activity::new(1, 5);
    activity.target_resource_operator = LogicalOperator::Or;
    activity.target_resources.insert(10);
    activity.target_resources.insert(20);
    let resources = vec![Resource::new(
        10,
        Some("R10".to_string()),
        false,
        false,
        InterActivityAllocationType::Direct,
        1.0,
        1.0,
        0,
        [],
    )];

    let output = scheduler.gather_unavailable_resources(&[&activity], &resources);

    assert!(output.is_empty());
}

// -- ReplaceWithSyntheticResources -------------------------------------------

#[test]
fn replace_with_synthetic_resources_with_empty_input_then_returns_empty() {
    let scheduler = PriorityListResourceScheduler;

    let output: Vec<ResourceSchedule<i32, i32, i32>> =
        scheduler.replace_with_synthetic_resources(vec![]);

    assert!(output.is_empty());
}

#[test]
fn replace_with_synthetic_resources_with_schedules_then_assigns_synthetic_resource_ids() {
    let scheduler = PriorityListResourceScheduler;
    let resource = Resource::new(
        99,
        Some("Original".to_string()),
        false,
        false,
        InterActivityAllocationType::Direct,
        1.0,
        1.0,
        0,
        [],
    );
    let schedule: ResourceSchedule<i32, i32, i32> = ResourceSchedule {
        resource: Some(resource),
        scheduled_activities: vec![],
        start_time: 0,
        finish_time: 10,
        resource_allocation: vec![],
        cost_allocation: vec![],
        billing_allocation: vec![],
        effort_allocation: vec![],
        activity_allocation: vec![],
    };

    let output = scheduler.replace_with_synthetic_resources(vec![schedule]);

    assert_eq!(output.len(), 1);
    assert!(output[0].resource.is_some());
    assert_ne!(output[0].resource.as_ref().unwrap().id, 99);
}

// -- CollectIndirectResourceSchedules ----------------------------------------

#[test]
fn collect_indirect_resource_schedules_with_unscheduled_indirect_resource_then_includes_indirect_resource(
) {
    let scheduler = PriorityListResourceScheduler;
    let indirect = Resource::new(
        50,
        Some("I".to_string()),
        false,
        false,
        InterActivityAllocationType::Indirect,
        1.0,
        1.0,
        0,
        [],
    );
    let direct = Resource::new(
        60,
        Some("D".to_string()),
        false,
        false,
        InterActivityAllocationType::Direct,
        1.0,
        1.0,
        0,
        [],
    );
    let no_scheduled: [ResourceSchedule<i32, i32, i32>; 0] = [];

    let output = scheduler
        .collect_indirect_resource_schedules(&[indirect, direct], &no_scheduled, &[], 0, 10)
        .unwrap();

    assert_eq!(output.len(), 1);
    assert_eq!(output[0].resource.as_ref().unwrap().id, 50);
}

#[test]
fn collect_indirect_resource_schedules_with_all_indirect_already_scheduled_then_returns_empty() {
    let scheduler = PriorityListResourceScheduler;
    let indirect = Resource::new(
        50,
        Some("I".to_string()),
        false,
        false,
        InterActivityAllocationType::Indirect,
        1.0,
        1.0,
        0,
        [],
    );
    let scheduled: ResourceSchedule<i32, i32, i32> = ResourceSchedule {
        resource: Some(indirect.clone()),
        scheduled_activities: vec![],
        start_time: 0,
        finish_time: 10,
        resource_allocation: vec![],
        cost_allocation: vec![],
        billing_allocation: vec![],
        effort_allocation: vec![],
        activity_allocation: vec![],
    };

    let output = scheduler
        .collect_indirect_resource_schedules(&[indirect], &[scheduled], &[], 0, 10)
        .unwrap();

    assert!(output.is_empty());
}

// -- GetResourcePhasesUsed ---------------------------------------------------

#[test]
fn get_resource_phases_used_with_intersecting_phases_then_returns_intersection() {
    let scheduler = PriorityListResourceScheduler;
    let resource = Resource::new(
        50,
        Some("R".to_string()),
        false,
        false,
        InterActivityAllocationType::Indirect,
        1.0,
        1.0,
        0,
        [1, 2, 3],
    );
    let schedule: ResourceSchedule<i32, i32, i32> = ResourceSchedule {
        resource: Some(resource),
        scheduled_activities: vec![],
        start_time: 0,
        finish_time: 10,
        resource_allocation: vec![],
        cost_allocation: vec![],
        billing_allocation: vec![],
        effort_allocation: vec![],
        activity_allocation: vec![],
    };
    let workstreams_used: IndexSet<i32> = IndexSet::from([2, 3, 4]);

    let output = scheduler.get_resource_phases_used(&[schedule], &workstreams_used);

    assert!(output.contains(&2));
    assert!(output.contains(&3));
    assert!(!output.contains(&1));
    assert!(!output.contains(&4));
}

#[test]
fn get_resource_phases_used_with_no_schedules_having_resource_then_returns_empty() {
    let scheduler = PriorityListResourceScheduler;
    let schedule: ResourceSchedule<i32, i32, i32> = ResourceSchedule {
        resource: None,
        scheduled_activities: vec![],
        start_time: 0,
        finish_time: 10,
        resource_allocation: vec![],
        cost_allocation: vec![],
        billing_allocation: vec![],
        effort_allocation: vec![],
        activity_allocation: vec![],
    };
    let workstreams_used: IndexSet<i32> = IndexSet::from([1, 2]);

    let output = scheduler.get_resource_phases_used(&[schedule], &workstreams_used);

    assert!(output.is_empty());
}

// -- RebuildAlignedResourceSchedules -----------------------------------------

#[test]
fn rebuild_aligned_resource_schedules_with_empty_input_then_returns_empty() {
    let scheduler = PriorityListResourceScheduler;
    let graph = FakeSchedulingGraph::new();

    let output = scheduler
        .rebuild_aligned_resource_schedules(&[], false, &graph, &[], 0, 10)
        .unwrap();

    assert!(output.is_empty());
}
