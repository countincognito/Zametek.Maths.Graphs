//! Ports of `GraphLimitTests.cs`.
//!
//! The domain limits in `graph_limits` exist so that absurd or corrupted input
//! is rejected with a clear error rather than consuming unbounded time and
//! memory. Declared values and counts are checked before compilation (`P0080`);
//! a schedule whose computed finish time runs past the horizon - which
//! per-value limits cannot prevent, because durations sum - is reported after
//! scheduling (`C0020`).

use zametek_maths_graphs_compilers::VertexGraphCompiler;
use zametek_maths_graphs_primitives::{
    graph_limits, DependentActivity, GraphCompilation, GraphCompilationErrorCode,
    InterActivityAllocationType, Resource, WorkStream,
};

type Compiler = VertexGraphCompiler<i32, i32, i32>;

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

fn create_work_stream(id: i32) -> WorkStream<i32> {
    WorkStream::new(id, format!("W{id}"), false)
}

fn single_error(
    compilation: &GraphCompilation<i32, i32, i32>,
) -> (GraphCompilationErrorCode, &str) {
    assert_eq!(
        compilation.compilation_errors.len(),
        1,
        "expected exactly one compilation error, got {:?}",
        compilation.compilation_errors
    );
    let error = &compilation.compilation_errors[0];
    (error.error_code, error.error_message.as_str())
}

fn has_limit_error_containing(compilation: &GraphCompilation<i32, i32, i32>, text: &str) -> bool {
    compilation
        .compilation_errors
        .iter()
        .any(|x| x.error_code == GraphCompilationErrorCode::P0080 && x.error_message.contains(text))
}

fn has_limit_error(compilation: &GraphCompilation<i32, i32, i32>) -> bool {
    compilation
        .compilation_errors
        .iter()
        .any(|x| x.error_code == GraphCompilationErrorCode::P0080)
}

#[test]
fn vertex_graph_compiler_given_duration_above_maximum_then_reports_p0080() {
    let mut compiler: Compiler = VertexGraphCompiler::new();
    compiler.add_activity(DependentActivity::new(
        1,
        graph_limits::MAXIMUM_TIME_VALUE + 1,
    ));

    let compilation = compiler
        .compile_with_resources(&[create_resource(10)])
        .unwrap();

    let (code, message) = single_error(&compilation);
    assert_eq!(code, GraphCompilationErrorCode::P0080);
    // Asserted in full rather than by substring, so the rendered text stays
    // identical to what the C# resource strings produce.
    assert_eq!(
        message,
        "Values or counts outside the supported limits:\n\
         Activity 1 -> Duration is 100001, which is outside the supported range of 0 to 100000\n"
    );
    assert!(compilation.resource_schedules.is_empty());
}

#[test]
fn vertex_graph_compiler_given_duration_at_maximum_then_compiles_without_limit_error() {
    let mut compiler: Compiler = VertexGraphCompiler::new();
    compiler.add_activity(DependentActivity::new(1, graph_limits::MAXIMUM_TIME_VALUE));

    let compilation = compiler
        .compile_with_resources(&[create_resource(10)])
        .unwrap();

    assert!(!has_limit_error(&compilation));
}

#[test]
fn vertex_graph_compiler_given_negative_minimum_earliest_start_time_then_reports_p0080() {
    let mut compiler: Compiler = VertexGraphCompiler::new();
    let mut activity = DependentActivity::new(1, 5);
    activity.minimum_earliest_start_time = Some(-1);
    compiler.add_activity(activity);

    let compilation = compiler
        .compile_with_resources(&[create_resource(10)])
        .unwrap();

    let (code, message) = single_error(&compilation);
    assert_eq!(code, GraphCompilationErrorCode::P0080);
    assert!(
        message.contains("MinimumEarliestStartTime"),
        "message was: {message}"
    );
}

#[test]
fn vertex_graph_compiler_given_maximum_latest_finish_time_above_maximum_then_reports_p0080() {
    let mut compiler: Compiler = VertexGraphCompiler::new();
    let mut activity = DependentActivity::new(1, 5);
    activity.maximum_latest_finish_time = Some(graph_limits::MAXIMUM_TIME_VALUE + 1);
    compiler.add_activity(activity);

    let compilation = compiler
        .compile_with_resources(&[create_resource(10)])
        .unwrap();

    assert!(has_limit_error_containing(
        &compilation,
        "MaximumLatestFinishTime"
    ));
}

#[test]
fn vertex_graph_compiler_given_negative_minimum_free_slack_then_reports_p0080() {
    let mut compiler: Compiler = VertexGraphCompiler::new();
    let mut activity = DependentActivity::new(1, 5);
    activity.minimum_free_slack = Some(-5);
    compiler.add_activity(activity);

    let compilation = compiler
        .compile_with_resources(&[create_resource(10)])
        .unwrap();

    assert!(has_limit_error_containing(&compilation, "MinimumFreeSlack"));
}

#[test]
fn vertex_graph_compiler_given_too_many_activities_then_reports_p0080() {
    let mut compiler: Compiler = VertexGraphCompiler::new();
    for id in 1..=(graph_limits::MAXIMUM_ACTIVITY_COUNT as i32 + 1) {
        compiler.add_activity(DependentActivity::new(id, 1));
    }

    let compilation = compiler
        .compile_with_resources(&[create_resource(10)])
        .unwrap();

    let (code, message) = single_error(&compilation);
    assert_eq!(code, GraphCompilationErrorCode::P0080);
    assert_eq!(
        message,
        "Values or counts outside the supported limits:\n\
         The number of activities is 2001, which exceeds the maximum supported count of 2000\n"
    );
}

#[test]
fn vertex_graph_compiler_given_too_many_resources_then_reports_p0080() {
    let mut compiler: Compiler = VertexGraphCompiler::new();
    compiler.add_activity(DependentActivity::new(1, 5));

    let resources: Vec<Resource<i32, i32>> = (1..=(graph_limits::MAXIMUM_RESOURCE_COUNT as i32
        + 1))
        .map(create_resource)
        .collect();

    let compilation = compiler.compile_with_resources(&resources).unwrap();

    assert!(has_limit_error_containing(&compilation, "resources"));
}

#[test]
fn vertex_graph_compiler_given_too_many_work_streams_then_reports_p0080() {
    let mut compiler: Compiler = VertexGraphCompiler::new();
    compiler.add_activity(DependentActivity::new(1, 5));

    let work_streams: Vec<WorkStream<i32>> = (1..=(graph_limits::MAXIMUM_WORK_STREAM_COUNT as i32
        + 1))
        .map(create_work_stream)
        .collect();

    let compilation = compiler
        .compile_with_resources_and_work_streams(&[create_resource(10)], &work_streams)
        .unwrap();

    assert!(has_limit_error_containing(&compilation, "work streams"));
}

#[test]
fn vertex_graph_compiler_given_counts_at_their_maximums_then_reports_no_limit_error() {
    let mut compiler: Compiler = VertexGraphCompiler::new();
    compiler.add_activity(DependentActivity::new(1, 5));

    let resources: Vec<Resource<i32, i32>> = (1..=(graph_limits::MAXIMUM_RESOURCE_COUNT as i32))
        .map(create_resource)
        .collect();
    let work_streams: Vec<WorkStream<i32>> = (1..=(graph_limits::MAXIMUM_WORK_STREAM_COUNT as i32))
        .map(create_work_stream)
        .collect();

    let compilation = compiler
        .compile_with_resources_and_work_streams(&resources, &work_streams)
        .unwrap();

    assert!(!has_limit_error(&compilation));
}

#[test]
fn vertex_graph_compiler_given_durations_that_sum_beyond_the_horizon_then_reports_c0020() {
    // Every individual duration is legal, but chained together they push the
    // computed schedule past the horizon - the case per-value limits cannot catch.
    let mut compiler: Compiler = VertexGraphCompiler::new();
    let activity_count = 5;
    let duration = (graph_limits::MAXIMUM_TIME_VALUE / activity_count) + 1;
    for id in 1..=activity_count {
        compiler.add_activity(if id == 1 {
            DependentActivity::new(id, duration)
        } else {
            DependentActivity::with_dependencies(id, duration, [id - 1])
        });
    }

    let compilation = compiler
        .compile_with_resources(&[create_resource(10)])
        .unwrap();

    assert!(compilation
        .compilation_errors
        .iter()
        .any(|x| x.error_code == GraphCompilationErrorCode::C0020));
    assert!(compilation.resource_schedules.is_empty());
}

#[test]
fn vertex_graph_compiler_given_schedule_ending_exactly_at_the_horizon_then_compiles_successfully() {
    let mut compiler: Compiler = VertexGraphCompiler::new();
    compiler.add_activity(DependentActivity::new(1, graph_limits::MAXIMUM_TIME_VALUE));

    let compilation = compiler
        .compile_with_resources(&[create_resource(10)])
        .unwrap();

    assert!(
        compilation.compilation_errors.is_empty(),
        "errors were: {:?}",
        compilation.compilation_errors
    );
    assert!(!compilation.resource_schedules.is_empty());
}
