//! Ports of representative tests from
//! `Zametek.Maths.Graphs.Compilers.Tests/Compilers/VertexGraphCompilerTests.cs`.
//! The expected values are copied verbatim from the C# test suite, so a green
//! run demonstrates input/output parity with the original library.

use indexmap::IndexSet;
use zametek_maths_graphs_compilers::VertexGraphCompiler;
use zametek_maths_graphs_primitives::{
    DependentActivity, GraphCompilationErrorCode, InterActivityAllocationType, LogicalOperator,
    NodeType, Resource, ScheduledActivity,
};

type Compiler = VertexGraphCompiler<i32, i32, i32>;
type Act = DependentActivity<i32, i32, i32>;
type Res = Resource<i32, i32>;

fn resource(id: i32) -> Res {
    Resource::new(
        id,
        Some(String::new()),
        false,
        false,
        InterActivityAllocationType::None,
        1.0,
        1.0,
        0,
        [],
    )
}

fn assert_scheduled(scheduled: &ScheduledActivity<i32>, id: i32, start: i32, finish: i32) {
    assert_eq!(scheduled.id, id, "scheduled activity id");
    assert_eq!(scheduled.start_time, start, "start time of activity {id}");
    assert_eq!(
        scheduled.finish_time, finish,
        "finish time of activity {id}"
    );
}

#[test]
fn given_constructor_then_no_exception() {
    let compiler = Compiler::new();
    let builder = compiler.builder();
    assert!(builder.edge_ids().is_empty());
    assert!(builder.node_ids().is_empty());
    assert!(builder.all_dependencies_satisfied());
    assert!(builder.start_nodes().is_empty());
    assert!(builder.end_nodes().is_empty());
}

#[test]
fn given_single_activity_no_dependencies_then_no_start_or_end_nodes() {
    let mut compiler = Compiler::new();
    assert!(compiler.add_activity(Act::new(1, 0)));

    let builder = compiler.builder();
    assert!(builder.edge_ids().is_empty());
    assert_eq!(builder.node_ids().len(), 1);
    assert!(builder.all_dependencies_satisfied());
    assert!(builder.start_nodes().is_empty());
    assert!(builder.end_nodes().is_empty());
    assert_eq!(builder.node(1).unwrap().id(), 1);
    assert_eq!(builder.node(1).unwrap().node_type(), NodeType::Isolated);
    assert_eq!(builder.activity(1).unwrap().id(), 1);
    assert_eq!(builder.activities().count(), 1);
    assert_eq!(builder.edges().count(), 0);
}

#[test]
fn given_two_activities_no_dependencies_then_nodes_are_isolated_with_same_finish_times() {
    let mut compiler = Compiler::new();
    assert!(compiler.add_activity(Act::new(1, 3)));
    assert!(compiler.add_activity(Act::new(2, 5)));

    compiler.compile().unwrap();

    let builder = compiler.builder();
    assert!(builder.edge_ids().is_empty());
    assert_eq!(builder.node_ids().len(), 2);
    assert!(builder.all_dependencies_satisfied());
    assert!(builder.start_nodes().is_empty());
    assert!(builder.end_nodes().is_empty());

    assert_eq!(builder.node(1).unwrap().node_type(), NodeType::Isolated);
    let activity1 = builder.activity(1).unwrap();
    assert_eq!(activity1.earliest_start_time, Some(0));
    assert_eq!(activity1.latest_start_time(), Some(2));
    assert_eq!(activity1.earliest_finish_time(), Some(3));
    assert_eq!(activity1.latest_finish_time, Some(5));
    assert_eq!(activity1.free_slack, Some(2));
    assert_eq!(activity1.total_slack(), Some(2));

    assert_eq!(builder.node(2).unwrap().node_type(), NodeType::Isolated);
    let activity2 = builder.activity(2).unwrap();
    assert_eq!(activity2.earliest_start_time, Some(0));
    assert_eq!(activity2.latest_start_time(), Some(0));
    assert_eq!(activity2.earliest_finish_time(), Some(5));
    assert_eq!(activity2.latest_finish_time, Some(5));
    assert_eq!(activity2.free_slack, Some(0));
    assert_eq!(activity2.total_slack(), Some(0));

    assert_eq!(builder.activities().count(), 2);
    assert_eq!(builder.edges().count(), 0);
}

#[test]
fn given_compile_with_invalid_constraints_then_finds_invalid_constraints() {
    let mut compiler = Compiler::new();
    compiler.add_activity(Act::new(1, 6));
    compiler.add_activity(Act::new(2, 7));
    compiler.add_activity(Act::new(3, 8));
    let mut a4 = Act::with_dependencies(4, 11, [2]);
    a4.minimum_earliest_start_time = Some(7);
    a4.maximum_latest_finish_time = Some(17);
    compiler.add_activity(a4);
    compiler.add_activity(Act::with_dependencies(5, 8, [1, 2, 3]));
    compiler.add_activity(Act::with_dependencies(6, 7, [3]));
    compiler.add_activity(Act::with_dependencies(7, 4, [4]));
    compiler.add_activity(Act::with_dependencies(8, 4, [4, 6]));
    let mut a9 = Act::with_dependencies(9, 10, [5]);
    a9.minimum_free_slack = Some(2);
    a9.maximum_latest_finish_time = Some(8);
    compiler.add_activity(a9);

    let compilation = compiler.compile().unwrap();

    assert!(compilation.resource_schedules.is_empty());
    assert_eq!(compilation.compilation_errors.len(), 1);
    assert_eq!(
        compilation.compilation_errors[0].error_code,
        GraphCompilationErrorCode::P0030
    );

    let expected = "Invalid activity constraints:\n\
        4 -> (MinimumEarliestStartTime + Duration) must be greater than MaximumLatestFinishTime\n\
        9 -> Cannot set MinimumFreeSlack and MaximumLatestFinishTime at the same time\n";
    assert_eq!(compilation.compilation_errors[0].error_message, expected);
}

#[test]
fn given_compile_with_circular_dependencies_then_finds_circular_dependencies() {
    let mut compiler = Compiler::new();
    compiler.add_activity(Act::new(1, 10));
    compiler.add_activity(Act::with_dependencies(2, 10, [7]));
    compiler.add_activity(Act::new(3, 10));
    compiler.add_activity(Act::with_dependencies(4, 10, [2]));
    compiler.add_activity(Act::with_dependencies(5, 10, [1, 2, 3, 8]));
    compiler.add_activity(Act::with_dependencies(6, 10, [3]));
    compiler.add_activity(Act::with_dependencies(7, 10, [4]));
    compiler.add_activity(Act::with_dependencies(8, 10, [9, 6]));
    compiler.add_activity(Act::with_dependencies(9, 10, [5]));

    let compilation = compiler.compile().unwrap();

    assert!(compilation.resource_schedules.is_empty());
    assert_eq!(compilation.compilation_errors.len(), 1);
    assert_eq!(
        compilation.compilation_errors[0].error_code,
        GraphCompilationErrorCode::P0020
    );

    let expected = "Circular activity dependencies:\n4 -> 7 -> 2\n9 -> 8 -> 5\n";
    assert_eq!(compilation.compilation_errors[0].error_message, expected);
}

#[test]
fn given_compile_with_invalid_dependencies_then_finds_invalid_dependencies() {
    let mut compiler = Compiler::new();
    compiler.add_activity(Act::new(1, 10));
    compiler.add_activity(Act::with_dependencies(2, 10, [7]));
    compiler.add_activity(Act::with_dependencies(3, 10, [21]));
    compiler.add_activity(Act::with_dependencies(4, 10, [2]));
    compiler.add_activity(Act::with_dependencies(5, 10, [1, 2, 3, 8]));
    compiler.add_activity(Act::with_dependencies(6, 10, [3]));
    compiler.add_activity(Act::with_dependencies(7, 10, [22]));
    compiler.add_activity(Act::with_dependencies(8, 10, [9, 6]));
    compiler.add_activity(Act::new(9, 10));

    let compilation = compiler.compile().unwrap();

    assert!(compilation.resource_schedules.is_empty());
    assert_eq!(compilation.compilation_errors.len(), 1);
    assert_eq!(
        compilation.compilation_errors[0].error_code,
        GraphCompilationErrorCode::P0010
    );

    let expected = "Invalid activity dependencies:\n\
        21 is invalid but referenced by: 3\n\
        22 is invalid but referenced by: 7\n";
    assert_eq!(compilation.compilation_errors[0].error_message, expected);
}

#[test]
fn given_compile_with_all_three_error_types_then_finds_all_three() {
    let mut compiler = Compiler::new();
    compiler.add_activity(Act::new(1, 10));
    compiler.add_activity(Act::with_dependencies(2, 10, [7]));
    compiler.add_activity(Act::with_dependencies(3, 10, [21]));
    let mut a4 = Act::with_dependencies(4, 10, [2]);
    a4.minimum_earliest_start_time = Some(7);
    a4.maximum_latest_finish_time = Some(16);
    compiler.add_activity(a4);
    compiler.add_activity(Act::with_dependencies(5, 10, [1, 2, 3, 8]));
    compiler.add_activity(Act::with_dependencies(6, 10, [3]));
    compiler.add_activity(Act::with_dependencies(7, 10, [4, 22]));
    compiler.add_activity(Act::with_dependencies(8, 10, [9, 6, 22]));
    compiler.add_activity(Act::with_dependencies(9, 10, [5]));

    let compilation = compiler.compile().unwrap();

    assert!(compilation.resource_schedules.is_empty());
    assert_eq!(compilation.compilation_errors.len(), 3);

    assert_eq!(
        compilation.compilation_errors[0].error_code,
        GraphCompilationErrorCode::P0010
    );
    let expected0 = "Invalid activity dependencies:\n\
        21 is invalid but referenced by: 3\n\
        22 is invalid but referenced by: 7, 8\n";
    assert_eq!(compilation.compilation_errors[0].error_message, expected0);

    assert_eq!(
        compilation.compilation_errors[1].error_code,
        GraphCompilationErrorCode::P0020
    );
    let expected1 = "Circular activity dependencies:\n4 -> 7 -> 2\n9 -> 8 -> 5\n";
    assert_eq!(compilation.compilation_errors[1].error_message, expected1);

    assert_eq!(
        compilation.compilation_errors[2].error_code,
        GraphCompilationErrorCode::P0030
    );
    let expected2 = "Invalid activity constraints:\n\
        4 -> (MinimumEarliestStartTime + Duration) must be greater than MaximumLatestFinishTime\n";
    assert_eq!(compilation.compilation_errors[2].error_message, expected2);
}

#[test]
fn given_compile_with_post_compilation_invalid_constraints_then_finds_invalid_constraints() {
    let mut compiler = Compiler::new();
    compiler.add_activity(Act::new(1, 6));
    compiler.add_activity(Act::new(2, 7));
    compiler.add_activity(Act::new(3, 8));
    let mut a4 = Act::with_dependencies(4, 11, [2]);
    a4.maximum_latest_finish_time = Some(5);
    compiler.add_activity(a4);
    compiler.add_activity(Act::with_dependencies(5, 8, [1, 2, 3]));
    compiler.add_activity(Act::with_dependencies(6, 7, [3]));
    compiler.add_activity(Act::with_dependencies(7, 4, [4]));
    compiler.add_activity(Act::with_dependencies(8, 4, [4, 6]));
    compiler.add_activity(Act::with_dependencies(9, 10, [5]));

    let compilation = compiler.compile().unwrap();

    assert!(!compilation.resource_schedules.is_empty());
    assert_eq!(compilation.compilation_errors.len(), 1);
    assert_eq!(
        compilation.compilation_errors[0].error_code,
        GraphCompilationErrorCode::C0010
    );

    let expected = "Invalid activity constraints:\n\
        2 -> LatestStartTime cannot be less than zero\n\
        2 -> LatestFinishTime cannot be less than zero\n\
        2 -> LatestStartTime cannot be less than EarliestStartTime\n\
        2 -> LatestFinishTime cannot be less than EarliestFinishTime\n\
        4 -> EarliestStartTime cannot be less than zero\n\
        4 -> LatestStartTime cannot be less than zero\n";
    assert_eq!(compilation.compilation_errors[0].error_message, expected);

    let schedules = &compilation.resource_schedules;
    assert_eq!(schedules.len(), 3);

    assert!(schedules[0].resource.is_none());
    assert_eq!(schedules[0].scheduled_activities.len(), 3);
    assert_scheduled(&schedules[0].scheduled_activities[0], 2, 0, 7);
    assert_scheduled(&schedules[0].scheduled_activities[1], 4, -6, 5);
    assert_scheduled(&schedules[0].scheduled_activities[2], 7, 5, 9);

    assert!(schedules[1].resource.is_none());
    assert_eq!(schedules[1].scheduled_activities.len(), 3);
    assert_scheduled(&schedules[1].scheduled_activities[0], 3, 0, 8);
    assert_scheduled(&schedules[1].scheduled_activities[1], 5, 8, 16);
    assert_scheduled(&schedules[1].scheduled_activities[2], 9, 16, 26);

    assert!(schedules[2].resource.is_none());
    assert_eq!(schedules[2].scheduled_activities.len(), 3);
    assert_scheduled(&schedules[2].scheduled_activities[0], 1, 0, 6);
    assert_scheduled(&schedules[2].scheduled_activities[1], 6, 8, 15);
    assert_scheduled(&schedules[2].scheduled_activities[2], 8, 15, 19);

    let builder = compiler.builder();

    let a2 = builder.activity(2).unwrap();
    assert_eq!(a2.earliest_start_time, Some(0));
    assert_eq!(a2.earliest_finish_time(), Some(7));
    assert_eq!(a2.free_slack, Some(-13));
    assert_eq!(a2.total_slack(), Some(-13));
    assert_eq!(a2.latest_start_time(), Some(-13));
    assert_eq!(a2.latest_finish_time, Some(-6));

    let a4 = builder.activity(4).unwrap();
    assert_eq!(a4.earliest_start_time, Some(-6));
    assert_eq!(a4.earliest_finish_time(), Some(5));
    assert_eq!(a4.free_slack, Some(0));
    assert_eq!(a4.total_slack(), Some(0));
    assert_eq!(a4.latest_start_time(), Some(-6));
    assert_eq!(a4.latest_finish_time, Some(5));
}

// The canonical 9-activity network compiled with unlimited resources - ports
// VertexGraphCompiler_GivenCompileWithCompiledDependenciesAndUnlimitedResources.
#[test]
fn given_compile_with_compiled_dependencies_and_unlimited_resources_then_resource_schedules_correct_order(
) {
    let mut compiler = Compiler::new();
    compiler.add_activity(Act::new(1, 6));
    compiler.add_activity(Act::new(2, 7));
    compiler.add_activity(Act::new(3, 8));
    compiler.add_activity(Act::with_dependencies(4, 11, [2]));
    compiler.add_activity(Act::with_dependencies(5, 8, [1, 2, 3]));
    compiler.add_activity(Act::with_dependencies(6, 7, [3]));
    compiler.add_activity(Act::with_dependencies(7, 4, [4]));
    compiler.add_activity(Act::with_dependencies(8, 4, [4, 6]));
    compiler.add_activity(Act::with_dependencies(9, 10, [5]));

    let compilation = compiler.compile().unwrap();

    assert!(compilation.compilation_errors.is_empty());

    let schedules = &compilation.resource_schedules;
    assert_eq!(schedules.len(), 3);

    assert!(schedules[0].resource.is_none());
    assert_eq!(schedules[0].scheduled_activities.len(), 3);
    assert_scheduled(&schedules[0].scheduled_activities[0], 3, 0, 8);
    assert_scheduled(&schedules[0].scheduled_activities[1], 5, 8, 16);
    assert_scheduled(&schedules[0].scheduled_activities[2], 9, 16, 26);

    assert!(schedules[1].resource.is_none());
    assert_eq!(schedules[1].scheduled_activities.len(), 3);
    assert_scheduled(&schedules[1].scheduled_activities[0], 2, 0, 7);
    assert_scheduled(&schedules[1].scheduled_activities[1], 4, 7, 18);
    assert_scheduled(&schedules[1].scheduled_activities[2], 7, 18, 22);

    assert!(schedules[2].resource.is_none());
    assert_eq!(schedules[2].scheduled_activities.len(), 3);
    assert_scheduled(&schedules[2].scheduled_activities[0], 1, 0, 6);
    assert_scheduled(&schedules[2].scheduled_activities[1], 6, 8, 15);
    assert_scheduled(&schedules[2].scheduled_activities[2], 8, 18, 22);

    let builder = compiler.builder();

    let a1 = builder.activity(1).unwrap();
    assert_eq!(a1.earliest_start_time, Some(0));
    assert_eq!(a1.earliest_finish_time(), Some(6));
    assert_eq!(a1.free_slack, Some(2));
    assert_eq!(a1.total_slack(), Some(2));
    assert_eq!(a1.latest_start_time(), Some(2));
    assert_eq!(a1.latest_finish_time, Some(8));
    assert_eq!(a1.dependencies.len(), 0);
    assert_eq!(a1.planning_dependencies.len(), 0);
    assert_eq!(a1.resource_dependencies.len(), 0);
    assert_eq!(a1.successors.len(), 1);
    assert!(a1.successors.contains(&5));

    let a2 = builder.activity(2).unwrap();
    assert_eq!(a2.earliest_start_time, Some(0));
    assert_eq!(a2.earliest_finish_time(), Some(7));
    assert_eq!(a2.free_slack, Some(0));
    assert_eq!(a2.total_slack(), Some(1));
    assert_eq!(a2.latest_start_time(), Some(1));
    assert_eq!(a2.latest_finish_time, Some(8));
    assert_eq!(a2.dependencies.len(), 0);
    assert_eq!(a2.planning_dependencies.len(), 0);
    assert_eq!(a2.resource_dependencies.len(), 0);
    assert_eq!(a2.successors.len(), 2);
    assert!(a2.successors.contains(&4));
    assert!(a2.successors.contains(&5));

    let a3 = builder.activity(3).unwrap();
    assert_eq!(a3.earliest_start_time, Some(0));
    assert_eq!(a3.earliest_finish_time(), Some(8));
    assert_eq!(a3.free_slack, Some(0));
    assert_eq!(a3.total_slack(), Some(0));
    assert_eq!(a3.latest_start_time(), Some(0));
    assert_eq!(a3.latest_finish_time, Some(8));
    assert_eq!(a3.dependencies.len(), 0);
    assert_eq!(a3.planning_dependencies.len(), 0);
    assert_eq!(a3.resource_dependencies.len(), 0);
    assert_eq!(a3.successors.len(), 2);
    assert!(a3.successors.contains(&5));
    assert!(a3.successors.contains(&6));

    let a4 = builder.activity(4).unwrap();
    assert_eq!(a4.earliest_start_time, Some(7));
    assert_eq!(a4.earliest_finish_time(), Some(18));
    assert_eq!(a4.free_slack, Some(0));
    assert_eq!(a4.total_slack(), Some(4));
    assert_eq!(a4.latest_start_time(), Some(11));
    assert_eq!(a4.latest_finish_time, Some(22));
    assert_eq!(a4.dependencies.len(), 1);
    assert!(a4.dependencies.contains(&2));
    assert_eq!(a4.planning_dependencies.len(), 0);
    assert_eq!(a4.resource_dependencies.len(), 1);
    assert!(a4.resource_dependencies.contains(&2));
    assert_eq!(a4.successors.len(), 2);
    assert!(a4.successors.contains(&7));
    assert!(a4.successors.contains(&8));

    let a5 = builder.activity(5).unwrap();
    assert_eq!(a5.earliest_start_time, Some(8));
    assert_eq!(a5.earliest_finish_time(), Some(16));
    assert_eq!(a5.free_slack, Some(0));
    assert_eq!(a5.total_slack(), Some(0));
    assert_eq!(a5.latest_start_time(), Some(8));
    assert_eq!(a5.latest_finish_time, Some(16));
    assert_eq!(a5.dependencies.len(), 3);
    assert!(a5.dependencies.contains(&1));
    assert!(a5.dependencies.contains(&2));
    assert!(a5.dependencies.contains(&3));
    assert_eq!(a5.planning_dependencies.len(), 0);
    assert_eq!(a5.resource_dependencies.len(), 1);
    assert!(a5.resource_dependencies.contains(&3));
    assert_eq!(a5.successors.len(), 1);
    assert!(a5.successors.contains(&9));

    let a6 = builder.activity(6).unwrap();
    assert_eq!(a6.earliest_start_time, Some(8));
    assert_eq!(a6.earliest_finish_time(), Some(15));
    assert_eq!(a6.free_slack, Some(3));
    assert_eq!(a6.total_slack(), Some(7));
    assert_eq!(a6.latest_start_time(), Some(15));
    assert_eq!(a6.latest_finish_time, Some(22));
    assert_eq!(a6.dependencies.len(), 1);
    assert!(a6.dependencies.contains(&3));
    assert_eq!(a6.planning_dependencies.len(), 0);
    assert_eq!(a6.resource_dependencies.len(), 1);
    assert!(a6.resource_dependencies.contains(&1));
    assert_eq!(a6.successors.len(), 1);
    assert!(a6.successors.contains(&8));

    let a7 = builder.activity(7).unwrap();
    assert_eq!(a7.earliest_start_time, Some(18));
    assert_eq!(a7.earliest_finish_time(), Some(22));
    assert_eq!(a7.free_slack, Some(4));
    assert_eq!(a7.total_slack(), Some(4));
    assert_eq!(a7.latest_start_time(), Some(22));
    assert_eq!(a7.latest_finish_time, Some(26));
    assert_eq!(a7.dependencies.len(), 1);
    assert!(a7.dependencies.contains(&4));
    assert_eq!(a7.planning_dependencies.len(), 0);
    assert_eq!(a7.resource_dependencies.len(), 1);
    assert!(a7.resource_dependencies.contains(&4));
    assert_eq!(a7.successors.len(), 0);

    let a8 = builder.activity(8).unwrap();
    assert_eq!(a8.earliest_start_time, Some(18));
    assert_eq!(a8.earliest_finish_time(), Some(22));
    assert_eq!(a8.free_slack, Some(4));
    assert_eq!(a8.total_slack(), Some(4));
    assert_eq!(a8.latest_start_time(), Some(22));
    assert_eq!(a8.latest_finish_time, Some(26));
    assert_eq!(a8.dependencies.len(), 2);
    assert!(a8.dependencies.contains(&4));
    assert!(a8.dependencies.contains(&6));
    assert_eq!(a8.planning_dependencies.len(), 0);
    assert_eq!(a8.resource_dependencies.len(), 1);
    assert!(a8.resource_dependencies.contains(&6));
    assert_eq!(a8.successors.len(), 0);

    let a9 = builder.activity(9).unwrap();
    assert_eq!(a9.earliest_start_time, Some(16));
    assert_eq!(a9.earliest_finish_time(), Some(26));
    assert_eq!(a9.free_slack, Some(0));
    assert_eq!(a9.total_slack(), Some(0));
    assert_eq!(a9.latest_start_time(), Some(16));
    assert_eq!(a9.latest_finish_time, Some(26));
    assert_eq!(a9.dependencies.len(), 1);
    assert!(a9.dependencies.contains(&5));
    assert_eq!(a9.planning_dependencies.len(), 0);
    assert_eq!(a9.resource_dependencies.len(), 1);
    assert!(a9.resource_dependencies.contains(&5));
    assert_eq!(a9.successors.len(), 0);
}

#[test]
fn given_compile_with_available_resources_then_resource_schedules_correct_order() {
    let mut compiler = Compiler::new();

    let mut activity1 = Act::new(1, 6);
    let mut activity2 = Act::with_dependencies(2, 7, [1]);
    let mut activity3 = Act::with_dependencies(3, 4, [2]);
    let mut activity4 = Act::with_dependencies(4, 4, [3]);

    activity1.target_resources.insert(1);
    activity1.target_resource_operator = LogicalOperator::And;

    activity2.target_resources.insert(1);
    activity2.target_resources.insert(2);
    activity2.target_resource_operator = LogicalOperator::Or;

    activity3.target_resources.insert(1);
    activity3.target_resources.insert(3);
    activity3.target_resource_operator = LogicalOperator::Or;

    activity4.target_resources.insert(1);
    activity4.target_resources.insert(3);
    activity4.target_resources.insert(4);
    activity4.target_resource_operator = LogicalOperator::ActiveAnd;

    compiler.add_activity(activity1);
    compiler.add_activity(activity2);
    compiler.add_activity(activity3);
    compiler.add_activity(activity4);

    let mut r2 = resource(2);
    r2.is_inactive = true;
    let mut r3 = resource(3);
    r3.is_inactive = true;
    let resources = vec![resource(1), r2, r3, resource(4)];

    let compilation = compiler.compile_with_resources(&resources).unwrap();

    assert!(compilation.compilation_errors.is_empty());

    let schedules = &compilation.resource_schedules;
    assert_eq!(schedules.len(), 2);

    assert_eq!(schedules[0].resource.as_ref().unwrap().id, 1);
    assert_eq!(schedules[0].scheduled_activities.len(), 4);
    assert_scheduled(&schedules[0].scheduled_activities[0], 1, 0, 6);
    assert_scheduled(&schedules[0].scheduled_activities[1], 2, 6, 13);
    assert_scheduled(&schedules[0].scheduled_activities[2], 3, 13, 17);
    assert_scheduled(&schedules[0].scheduled_activities[3], 4, 17, 21);

    assert_eq!(schedules[1].resource.as_ref().unwrap().id, 4);
    assert_eq!(schedules[1].scheduled_activities.len(), 1);
    assert_scheduled(&schedules[1].scheduled_activities[0], 4, 17, 21);
}

#[test]
fn given_compile_with_different_resource_operator_then_resource_schedules_correct_order() {
    let mut compiler = Compiler::new();

    let mut activity1 = Act::new(1, 6);
    let mut activity2 = Act::with_dependencies(2, 7, [1]);
    let mut activity3 = Act::with_dependencies(3, 4, [2]);
    let mut activity4 = Act::with_dependencies(4, 4, [3]);
    activity1.target_resources.insert(1);
    activity2.target_resources.insert(1);
    activity3.target_resources.insert(1);
    activity4.target_resources.insert(1);
    compiler.add_activity(activity1);
    compiler.add_activity(activity2);
    compiler.add_activity(activity3);
    compiler.add_activity(activity4);

    for operator in [
        LogicalOperator::And,
        LogicalOperator::Or,
        LogicalOperator::ActiveAnd,
    ] {
        for id in compiler.builder().activity_ids() {
            compiler
                .builder_mut()
                .activity_mut(id)
                .unwrap()
                .target_resource_operator = operator;
        }

        let compilation = compiler.compile_with_resources(&[resource(1)]).unwrap();

        assert!(compilation.compilation_errors.is_empty());

        let schedules = &compilation.resource_schedules;
        assert_eq!(schedules.len(), 1);

        assert_eq!(schedules[0].resource.as_ref().unwrap().id, 1);
        assert_eq!(schedules[0].scheduled_activities.len(), 4);
        assert_scheduled(&schedules[0].scheduled_activities[0], 1, 0, 6);
        assert_scheduled(&schedules[0].scheduled_activities[1], 2, 6, 13);
        assert_scheduled(&schedules[0].scheduled_activities[2], 3, 13, 17);
        assert_scheduled(&schedules[0].scheduled_activities[3], 4, 17, 21);
    }
}

#[test]
fn given_cyclomatic_complexity_with_no_nodes_then_finds_zero() {
    let mut compiler = Compiler::new();
    compiler.compile().unwrap();
    assert_eq!(compiler.cyclomatic_complexity(), 0);
}

#[test]
fn given_cyclomatic_complexity_in_one_network_then_as_expected() {
    let mut compiler = Compiler::new();
    compiler.add_activity(Act::new(1, 6));
    compiler.add_activity(Act::new(2, 7));
    compiler.add_activity(Act::new(3, 8));
    compiler.add_activity(Act::with_dependencies(4, 11, [2]));
    compiler.add_activity(Act::with_dependencies(5, 8, [1, 2, 3]));
    compiler.add_activity(Act::with_dependencies(6, 7, [3]));
    compiler.add_activity(Act::with_dependencies(7, 4, [4]));
    compiler.add_activity(Act::with_dependencies(8, 4, [4, 6]));
    compiler.add_activity(Act::with_dependencies(9, 10, [5]));

    compiler.compile().unwrap();

    assert_eq!(compiler.cyclomatic_complexity(), 6);
}

#[test]
fn given_cyclomatic_complexity_in_three_networks_then_as_expected() {
    let mut compiler = Compiler::new();
    compiler.add_activity(Act::new(1, 6));
    compiler.add_activity(Act::new(2, 7));
    compiler.add_activity(Act::new(3, 8));
    compiler.add_activity(Act::with_dependencies(4, 11, [1]));
    compiler.add_activity(Act::with_dependencies(5, 8, [2]));
    compiler.add_activity(Act::with_dependencies(6, 7, [3]));

    compiler.compile().unwrap();

    assert_eq!(compiler.cyclomatic_complexity(), 3);
}

#[test]
fn given_cyclomatic_complexity_with_two_lone_nodes_then_as_expected() {
    let mut compiler = Compiler::new();
    compiler.add_activity(Act::new(1, 6));
    compiler.add_activity(Act::new(2, 7));
    compiler.add_activity(Act::new(3, 8));
    compiler.add_activity(Act::with_dependencies(4, 11, [1]));

    compiler.compile().unwrap();

    assert_eq!(compiler.cyclomatic_complexity(), 3);
}

#[test]
fn given_transitive_reduction_when_redundant_dependencies_then_redundant_dependencies_removed() {
    let mut compiler = Compiler::new();
    compiler.add_activity(Act::new(1, 1));
    compiler.add_activity(Act::with_dependencies(2, 2, [1]));
    compiler.add_activity(Act::with_dependencies(3, 2, [1, 2]));

    compiler.transitive_reduction().unwrap();
    let compilation = compiler.compile().unwrap();

    assert!(compilation.compilation_errors.is_empty());

    let builder = compiler.builder();

    let a1 = builder.activity(1).unwrap();
    assert_eq!(a1.dependencies.len(), 0);
    assert_eq!(a1.planning_dependencies.len(), 0);
    assert_eq!(a1.resource_dependencies.len(), 0);
    assert_eq!(a1.successors.len(), 1);
    assert!(a1.successors.contains(&2));

    let a2 = builder.activity(2).unwrap();
    assert_eq!(a2.dependencies.len(), 1);
    assert!(a2.dependencies.contains(&1));
    assert_eq!(a2.planning_dependencies.len(), 0);
    assert_eq!(a2.resource_dependencies.len(), 1);
    assert!(a2.resource_dependencies.contains(&1));
    assert_eq!(a2.successors.len(), 1);
    assert!(a2.successors.contains(&3));

    let a3 = builder.activity(3).unwrap();
    assert_eq!(a3.dependencies.len(), 1);
    assert!(a3.dependencies.contains(&2));
    assert_eq!(a3.planning_dependencies.len(), 0);
    assert_eq!(a3.resource_dependencies.len(), 1);
    assert!(a3.resource_dependencies.contains(&2));
    assert_eq!(a3.successors.len(), 0);
}

#[test]
fn given_transitive_reduction_when_redundant_planning_dependencies_then_removed() {
    let mut compiler = Compiler::new();
    compiler.add_activity(Act::new(1, 1));
    compiler.add_activity(Act::with_planning_dependencies(2, 2, [], [1]));
    compiler.add_activity(Act::with_planning_dependencies(3, 2, [], [1, 2]));

    compiler.transitive_reduction().unwrap();
    let compilation = compiler.compile().unwrap();

    assert!(compilation.compilation_errors.is_empty());

    let builder = compiler.builder();

    let a2 = builder.activity(2).unwrap();
    assert_eq!(a2.dependencies.len(), 0);
    assert_eq!(a2.planning_dependencies.len(), 1);
    assert!(a2.planning_dependencies.contains(&1));
    assert_eq!(a2.resource_dependencies.len(), 1);
    assert!(a2.resource_dependencies.contains(&1));
    assert_eq!(a2.successors.len(), 1);
    assert!(a2.successors.contains(&3));

    let a3 = builder.activity(3).unwrap();
    assert_eq!(a3.dependencies.len(), 0);
    assert_eq!(a3.planning_dependencies.len(), 1);
    assert!(a3.planning_dependencies.contains(&2));
    assert_eq!(a3.resource_dependencies.len(), 1);
    assert!(a3.resource_dependencies.contains(&2));
    assert_eq!(a3.successors.len(), 0);
}

#[test]
fn given_transitive_reduction_when_dependencies_redundant_across_planning_dependencies_then_removed(
) {
    let mut compiler = Compiler::new();
    compiler.add_activity(Act::new(1, 1));
    compiler.add_activity(Act::with_planning_dependencies(2, 2, [], [1]));
    compiler.add_activity(Act::with_dependencies(3, 2, [1, 2]));

    compiler.transitive_reduction().unwrap();
    let compilation = compiler.compile().unwrap();

    assert!(compilation.compilation_errors.is_empty());

    let builder = compiler.builder();

    let a2 = builder.activity(2).unwrap();
    assert_eq!(a2.dependencies.len(), 0);
    assert_eq!(a2.planning_dependencies.len(), 1);
    assert!(a2.planning_dependencies.contains(&1));

    let a3 = builder.activity(3).unwrap();
    assert_eq!(a3.dependencies.len(), 1);
    assert!(a3.dependencies.contains(&2));
    assert_eq!(a3.planning_dependencies.len(), 0);
    assert_eq!(a3.resource_dependencies.len(), 1);
    assert!(a3.resource_dependencies.contains(&2));
    assert_eq!(a3.successors.len(), 0);
}

#[test]
fn given_transitive_reduction_when_planning_dependencies_redundant_across_dependencies_then_removed(
) {
    let mut compiler = Compiler::new();
    compiler.add_activity(Act::new(1, 1));
    compiler.add_activity(Act::with_dependencies(2, 2, [1]));
    compiler.add_activity(Act::with_planning_dependencies(3, 2, [], [1, 2]));

    compiler.transitive_reduction().unwrap();
    let compilation = compiler.compile().unwrap();

    assert!(compilation.compilation_errors.is_empty());

    let builder = compiler.builder();

    let a2 = builder.activity(2).unwrap();
    assert_eq!(a2.dependencies.len(), 1);
    assert!(a2.dependencies.contains(&1));
    assert_eq!(a2.planning_dependencies.len(), 0);

    let a3 = builder.activity(3).unwrap();
    assert_eq!(a3.dependencies.len(), 0);
    assert_eq!(a3.planning_dependencies.len(), 1);
    assert!(a3.planning_dependencies.contains(&2));
    assert_eq!(a3.resource_dependencies.len(), 1);
    assert!(a3.resource_dependencies.contains(&2));
    assert_eq!(a3.successors.len(), 0);
}

#[test]
fn given_set_activity_dependencies_then_graph_matches() {
    let mut compiler = Compiler::new();
    compiler.add_activity(Act::new(1, 1));
    compiler.add_activity(Act::new(2, 2));
    compiler.add_activity(Act::with_dependencies(3, 2, [1, 2]));

    // Replace {1,2} with just {2}.
    assert!(compiler.set_activity_dependencies(3, IndexSet::from([2]), IndexSet::new()));

    let builder = compiler.builder();
    let mut dependency_ids = builder.activity_dependency_ids(3);
    dependency_ids.sort();
    assert_eq!(dependency_ids, vec![2]);
    let a3 = builder.activity(3).unwrap();
    assert_eq!(a3.dependencies.len(), 1);
    assert!(a3.dependencies.contains(&2));
}

#[test]
fn given_get_next_activity_id_then_returns_max_plus_one() {
    let mut compiler = Compiler::new();
    assert_eq!(compiler.get_next_activity_id(), 1);
    compiler.add_activity(Act::new(1, 1));
    compiler.add_activity(Act::new(7, 1));
    assert_eq!(compiler.get_next_activity_id(), 8);
}
