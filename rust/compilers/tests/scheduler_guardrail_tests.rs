//! Ports of `SchedulerGuardrailTests.cs`.
//!
//! Guardrails against the scheduling livelocks diagnosed from a production hang:
//! any provably-unschedulable state must surface as a stall error - and, through
//! the compiler, as a `C0020` compilation error - instead of spinning the tick
//! loop for ever. Each scheduling call runs under a watchdog so a regression
//! fails the test rather than wedging the run.
//!
//! Three groups of C# tests have no counterpart here:
//!
//! * The corrupted-`HashSet` repros (and the `P0070` self-consistency check they
//!   drive). The production incident involved a set whose bucket index had been
//!   zeroed by an unsynchronised concurrent modification, so it enumerated
//!   normally but failed every lookup. Safe Rust cannot produce that state - the
//!   borrow checker and `Send`/`Sync` prevent the racing mutation that caused it -
//!   so there is nothing to reproduce and nothing to detect.
//! * The cancellation tests. Cancellation tokens are a .NET-specific concern and
//!   are deliberately not part of this port.
//! * The arrow-builder variants, since the arrow path shares this scheduler.

use indexmap::IndexMap;
use std::sync::mpsc;
use std::thread;
use std::time::Duration;
use zametek_maths_graphs_compilers::contracts::{
    IResourceSchedulingEngine, IResourceSchedulingGraph,
};
use zametek_maths_graphs_compilers::{PriorityListResourceScheduler, VertexGraphCompiler};
use zametek_maths_graphs_primitives::{
    graph_limits, DependentActivity, GraphCompilationErrorCode, InterActivityAllocationType,
    LogicalOperator, Resource, ResourceSchedule,
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

fn create_resource(id: i32, is_explicit_target: bool) -> Resource<i32, i32> {
    Resource::new(
        id,
        Some(format!("R{id}")),
        is_explicit_target,
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

/// Runs the given operation on a worker so that a livelock regression fails the
/// test after a timeout instead of hanging the whole test run.
///
/// The worker is deliberately detached rather than joined: if the guardrail
/// regresses it never returns, and the test must fail on the timeout instead of
/// blocking for ever waiting for it.
fn run_with_watchdog<T, F>(func: F) -> T
where
    F: FnOnce() -> T + Send + 'static,
    T: Send + 'static,
{
    let (sender, receiver) = mpsc::channel();
    thread::spawn(move || {
        let _ = sender.send(func());
    });
    match receiver.recv_timeout(Duration::from_secs(30)) {
        Ok(value) => value,
        Err(mpsc::RecvTimeoutError::Timeout) => {
            panic!("the scheduling operation appears to be stuck in a loop - the stall guardrail failed")
        }
        Err(mpsc::RecvTimeoutError::Disconnected) => {
            panic!("the scheduling operation panicked")
        }
    }
}

/// Schedules under the watchdog and returns the stall message, failing the test
/// if scheduling unexpectedly succeeded or failed for some other reason.
fn expect_stall(
    priority_list: Vec<i32>,
    resources: Vec<Resource<i32, i32>>,
    graph: FakeSchedulingGraph,
) -> String {
    let result: Result<Vec<ResourceSchedule<i32, i32, i32>>, _> = run_with_watchdog(move || {
        let mut graph = graph;
        PriorityListResourceScheduler.calculate_resource_schedules(
            &priority_list,
            &resources,
            false,
            &mut graph,
        )
    });

    let error = result.expect_err("scheduling should have stalled");
    assert!(
        error.is_resource_scheduling_stall(),
        "expected a resource scheduling stall, got: {}",
        error.message()
    );
    error.message().to_string()
}

#[test]
fn priority_list_resource_scheduler_given_and_operator_targeting_missing_resource_then_stalls_with_missing_resource_diagnosis(
) {
    let mut activity = activity_with_earliest_start_time(1, 5, 0);
    activity.target_resource_operator = LogicalOperator::And;
    activity.target_resources.insert(99);
    let graph = FakeSchedulingGraph::new().with_activity(activity);

    let message = expect_stall(vec![1], vec![create_resource(10, false)], graph);

    assert!(message.contains("not available"), "message was: {message}");
    assert!(message.contains("99"), "message was: {message}");
}

#[test]
fn priority_list_resource_scheduler_given_or_operator_with_no_available_target_resources_then_stalls_with_no_targets_diagnosis(
) {
    let mut activity = activity_with_earliest_start_time(1, 5, 0);
    activity.target_resource_operator = LogicalOperator::Or;
    activity.target_resources.insert(98);
    activity.target_resources.insert(99);
    let graph = FakeSchedulingGraph::new().with_activity(activity);

    let message = expect_stall(vec![1], vec![create_resource(10, false)], graph);

    assert!(
        message.contains("none of its target resources are available"),
        "message was: {message}"
    );
    assert!(message.contains("98, 99"), "message was: {message}");
}

#[test]
fn priority_list_resource_scheduler_given_only_explicit_target_resources_and_untargeted_activity_then_stalls_with_explicit_target_diagnosis(
) {
    let graph =
        FakeSchedulingGraph::new().with_activity(activity_with_earliest_start_time(1, 5, 0));

    let message = expect_stall(vec![1], vec![create_resource(10, true)], graph);

    assert!(
        message.contains("explicit target"),
        "message was: {message}"
    );
}

#[test]
fn priority_list_resource_scheduler_given_dependency_that_can_never_complete_then_stalls_with_dependency_diagnosis(
) {
    let graph = FakeSchedulingGraph::new()
        .with_activity(activity_with_earliest_start_time(1, 5, 0))
        .with_dependencies(1, [42]);

    let message = expect_stall(vec![1], vec![create_resource(10, false)], graph);

    assert!(
        message.contains("can never complete"),
        "message was: {message}"
    );
    assert!(message.contains("42"), "message was: {message}");
}

#[test]
fn priority_list_resource_scheduler_given_earliest_start_time_beyond_the_time_horizon_then_stalls_with_horizon_diagnosis(
) {
    let graph = FakeSchedulingGraph::new().with_activity(activity_with_earliest_start_time(
        1,
        5,
        graph_limits::MAXIMUM_TIME_VALUE + 1,
    ));

    let message = expect_stall(vec![1], vec![create_resource(10, false)], graph);

    assert!(
        message.contains("maximum supported time value"),
        "message was: {message}"
    );
}

#[test]
fn priority_list_resource_scheduler_given_duration_that_would_finish_beyond_the_time_horizon_then_stalls_with_horizon_diagnosis(
) {
    // Starts inside the horizon, but is too long to finish within it.
    let graph = FakeSchedulingGraph::new().with_activity(activity_with_earliest_start_time(
        1,
        graph_limits::MAXIMUM_TIME_VALUE,
        1,
    ));

    let message = expect_stall(vec![1], vec![create_resource(10, false)], graph);

    assert!(
        message.contains("maximum supported time value"),
        "message was: {message}"
    );
}

#[test]
fn priority_list_resource_scheduler_given_activity_exactly_at_the_time_horizon_then_schedules_it() {
    // Finishing exactly on the horizon is within the limit, so it must schedule.
    let graph = FakeSchedulingGraph::new().with_activity(activity_with_earliest_start_time(
        1,
        5,
        graph_limits::MAXIMUM_TIME_VALUE - 5,
    ));

    let schedules = run_with_watchdog(move || {
        let mut graph = graph;
        PriorityListResourceScheduler
            .calculate_resource_schedules(&[1], &[create_resource(10, false)], false, &mut graph)
            .unwrap()
    });

    assert_eq!(schedules.len(), 1);
    let scheduled = &schedules[0].scheduled_activities;
    assert_eq!(scheduled.len(), 1);
    assert_eq!(
        scheduled[0].start_time,
        graph_limits::MAXIMUM_TIME_VALUE - 5
    );
    assert_eq!(scheduled[0].finish_time, graph_limits::MAXIMUM_TIME_VALUE);
}

#[test]
fn vertex_graph_compiler_given_unschedulable_activity_then_reports_c0020_instead_of_hanging() {
    // Every declared value is inside its own limit, but the activity cannot start
    // early enough to finish within the horizon, so scheduling can never progress.
    let compilation = run_with_watchdog(|| {
        let mut compiler: VertexGraphCompiler<i32, i32, i32> = VertexGraphCompiler::new();
        let mut activity = DependentActivity::new(1, 5);
        activity.minimum_earliest_start_time = Some(graph_limits::MAXIMUM_TIME_VALUE);
        compiler.add_activity(activity);
        compiler
            .compile_with_resources(&[create_resource(10, false)])
            .unwrap()
    });

    assert_eq!(compilation.compilation_errors.len(), 1);
    let error = &compilation.compilation_errors[0];
    assert_eq!(error.error_code, GraphCompilationErrorCode::C0020);
    assert!(
        error.error_message.contains("maximum supported time value"),
        "message was: {}",
        error.error_message
    );
    assert!(compilation.resource_schedules.is_empty());
}

#[test]
fn vertex_graph_compiler_given_healthy_graph_then_compiles_normally() {
    let mut compiler: VertexGraphCompiler<i32, i32, i32> = VertexGraphCompiler::new();
    compiler.add_activity(DependentActivity::new(1, 5));
    compiler.add_activity(DependentActivity::with_dependencies(2, 5, [1]));

    let compilation = compiler
        .compile_with_resources(&[create_resource(10, false)])
        .unwrap();

    assert!(compilation.compilation_errors.is_empty());
    assert!(!compilation.resource_schedules.is_empty());
}
