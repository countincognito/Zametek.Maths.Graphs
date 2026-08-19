//! Ports of `SchedulerTimeGateTests.cs`.
//!
//! Locks in the time-gate semantics of the priority-list scheduler so the
//! skip-ahead optimisation cannot change observable behaviour: activities gated
//! by an earliest start time idle until their gate opens, a maximum latest
//! finish time schedules just-in-time, zero-duration activities complete on the
//! tick after they start, and activities queue behind busy resources.

use indexmap::IndexMap;
use zametek_maths_graphs_compilers::contracts::{
    IResourceSchedulingEngine, IResourceSchedulingGraph,
};
use zametek_maths_graphs_compilers::{PriorityListResourceScheduler, VertexGraphCompiler};
use zametek_maths_graphs_primitives::{
    DependentActivity, InterActivityAllocationType, Resource, ScheduledActivity,
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

    fn with_dependencies(mut self, id: i32, dependencies: impl IntoIterator<Item = i32>) -> Self {
        self.strong_dependencies
            .insert(id, dependencies.into_iter().collect());
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

fn create_resource(id: i32) -> Resource<i32, i32> {
    Resource::new(
        id,
        Some(format!("R{id}")),
        false,
        false,
        InterActivityAllocationType::None,
        0.0,
        0.0,
        0,
        [],
    )
}

fn activity_with_earliest_start_time(
    id: i32,
    duration: i32,
    earliest_start_time: i32,
) -> DependentActivity<i32, i32, i32> {
    let mut activity = DependentActivity::new(id, duration);
    activity.earliest_start_time = Some(earliest_start_time);
    activity
}

fn scheduled_with_id(schedule: &[ScheduledActivity<i32>], id: i32) -> &ScheduledActivity<i32> {
    schedule
        .iter()
        .find(|x| x.id == id)
        .unwrap_or_else(|| panic!("activity {id} was not scheduled"))
}

#[test]
fn priority_list_resource_scheduler_given_activity_gated_by_earliest_start_time_then_schedules_at_the_gate(
) {
    let scheduler = PriorityListResourceScheduler;
    let mut graph =
        FakeSchedulingGraph::new().with_activity(activity_with_earliest_start_time(1, 5, 250));

    let schedules = scheduler
        .calculate_resource_schedules(&[1], &[create_resource(10)], false, &mut graph)
        .unwrap();

    assert_eq!(schedules.len(), 1);
    let scheduled = &schedules[0].scheduled_activities;
    assert_eq!(scheduled.len(), 1);
    assert_eq!(scheduled[0].start_time, 250);
    assert_eq!(scheduled[0].finish_time, 255);
}

#[test]
fn priority_list_resource_scheduler_given_activity_with_maximum_latest_finish_time_then_schedules_just_in_time(
) {
    let scheduler = PriorityListResourceScheduler;
    let mut activity = activity_with_earliest_start_time(1, 5, 0);
    activity.maximum_latest_finish_time = Some(100);
    let mut graph = FakeSchedulingGraph::new().with_activity(activity);

    let schedules = scheduler
        .calculate_resource_schedules(&[1], &[create_resource(10)], false, &mut graph)
        .unwrap();

    // The scheduler delays a deadline-constrained activity until starting any
    // later would overshoot the deadline, so it finishes exactly on it.
    assert_eq!(schedules.len(), 1);
    let scheduled = &schedules[0].scheduled_activities;
    assert_eq!(scheduled.len(), 1);
    assert_eq!(scheduled[0].start_time, 95);
    assert_eq!(scheduled[0].finish_time, 100);
}

#[test]
fn priority_list_resource_scheduler_given_deadline_constrained_and_unconstrained_activities_then_only_the_constrained_one_waits(
) {
    let scheduler = PriorityListResourceScheduler;
    let mut constrained = activity_with_earliest_start_time(1, 5, 0);
    constrained.maximum_latest_finish_time = Some(100);
    let mut graph = FakeSchedulingGraph::new()
        .with_activity(constrained)
        .with_activity(activity_with_earliest_start_time(2, 5, 0));

    let schedules = scheduler
        .calculate_resource_schedules(&[1, 2], &[create_resource(10)], false, &mut graph)
        .unwrap();

    assert_eq!(schedules.len(), 1);
    let scheduled = &schedules[0].scheduled_activities;
    let first = scheduled_with_id(scheduled, 2);
    let second = scheduled_with_id(scheduled, 1);
    assert_eq!(first.start_time, 0);
    assert_eq!(first.finish_time, 5);
    assert_eq!(second.start_time, 95);
    assert_eq!(second.finish_time, 100);
}

#[test]
fn priority_list_resource_scheduler_given_zero_duration_activity_gated_by_earliest_start_time_then_successor_starts_on_the_next_tick(
) {
    let scheduler = PriorityListResourceScheduler;
    let mut graph = FakeSchedulingGraph::new()
        .with_activity(activity_with_earliest_start_time(1, 0, 100))
        .with_activity(DependentActivity::new(2, 5))
        .with_dependencies(2, [1]);

    let schedules = scheduler
        .calculate_resource_schedules(&[1, 2], &[create_resource(10)], false, &mut graph)
        .unwrap();

    assert_eq!(schedules.len(), 1);
    let scheduled = &schedules[0].scheduled_activities;
    let milestone = scheduled_with_id(scheduled, 1);
    let successor = scheduled_with_id(scheduled, 2);
    assert_eq!(milestone.start_time, 100);
    assert_eq!(milestone.finish_time, 100);
    // A zero-duration activity is only detected as completed on the tick after
    // it starts, so its successor begins one tick later.
    assert_eq!(successor.start_time, 101);
    assert_eq!(successor.finish_time, 106);
}

#[test]
fn priority_list_resource_scheduler_given_activity_queued_behind_busy_resource_then_starts_when_the_resource_frees(
) {
    let scheduler = PriorityListResourceScheduler;
    let mut graph = FakeSchedulingGraph::new()
        .with_activity(activity_with_earliest_start_time(1, 7, 0))
        .with_activity(activity_with_earliest_start_time(2, 3, 0));

    let schedules = scheduler
        .calculate_resource_schedules(&[1, 2], &[create_resource(10)], false, &mut graph)
        .unwrap();

    assert_eq!(schedules.len(), 1);
    let scheduled = &schedules[0].scheduled_activities;
    assert_eq!(scheduled_with_id(scheduled, 1).start_time, 0);
    assert_eq!(scheduled_with_id(scheduled, 2).start_time, 7);
    assert_eq!(scheduled_with_id(scheduled, 2).finish_time, 10);
}

#[test]
fn vertex_graph_compiler_given_minimum_earliest_start_time_then_schedule_honours_the_delay() {
    let mut compiler: VertexGraphCompiler<i32, i32, i32> = VertexGraphCompiler::new();
    let mut first = DependentActivity::new(1, 5);
    first.minimum_earliest_start_time = Some(300);
    compiler.add_activity(first);
    compiler.add_activity(DependentActivity::with_dependencies(2, 5, [1]));

    let compilation = compiler
        .compile_with_resources(&[create_resource(10)])
        .unwrap();

    assert!(compilation.compilation_errors.is_empty());
    let first = compilation
        .dependent_activities
        .iter()
        .find(|x| x.id() == 1)
        .expect("activity 1");
    let second = compilation
        .dependent_activities
        .iter()
        .find(|x| x.id() == 2)
        .expect("activity 2");
    assert_eq!(first.earliest_start_time, Some(300));
    assert_eq!(second.earliest_start_time, Some(305));
}
