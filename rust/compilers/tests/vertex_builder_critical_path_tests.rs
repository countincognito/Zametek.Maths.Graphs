//! Ports of `VertexGraphBuilderCriticalPathTests.cs`. The critical-path golden
//! values (earliest/latest start and finish, free and total slack) are copied
//! verbatim from the C# tests. `ShuffleProcessingOrder` is set to mirror the C#
//! tests; it only shuffles the CPM processing order and leaves the computed
//! critical-path values invariant.

use indexmap::IndexSet;
use zametek_maths_graphs_compilers::{NextIdGenerator, VertexGraphBuilder};
use zametek_maths_graphs_primitives::{DependentActivity, InterActivityAllocationType, Resource};

type Builder = VertexGraphBuilder<i32, i32, i32>;
type Act = DependentActivity<i32, i32, i32>;

fn new_builder() -> Builder {
    let mut builder = VertexGraphBuilder::<i32, i32, i32>::new(NextIdGenerator::new(0));
    builder.shuffle_processing_order = true;
    builder
}

/// Asserts an activity's forward-flow values (earliest start and finish).
fn assert_forward(builder: &Builder, id: i32, est: i32, eft: i32) {
    let activity = builder.activity(id).unwrap();
    assert_eq!(activity.earliest_start_time, Some(est), "EST of {id}");
    assert_eq!(activity.earliest_finish_time(), Some(eft), "EFT of {id}");
}

/// Asserts an activity's backward-flow values (latest start and finish).
fn assert_backward(builder: &Builder, id: i32, lst: i32, lft: i32) {
    let activity = builder.activity(id).unwrap();
    assert_eq!(activity.latest_start_time(), Some(lst), "LST of {id}");
    assert_eq!(activity.latest_finish_time, Some(lft), "LFT of {id}");
}

/// Asserts the full critical-path tuple for an activity.
#[allow(clippy::too_many_arguments)]
fn assert_cpm(
    builder: &Builder,
    id: i32,
    est: i32,
    eft: i32,
    free_slack: i32,
    total_slack: i32,
    lst: i32,
    lft: i32,
) {
    let activity = builder.activity(id).unwrap();
    assert_eq!(activity.earliest_start_time, Some(est), "EST of {id}");
    assert_eq!(activity.earliest_finish_time(), Some(eft), "EFT of {id}");
    assert_eq!(activity.free_slack, Some(free_slack), "FreeSlack of {id}");
    assert_eq!(
        activity.total_slack(),
        Some(total_slack),
        "TotalSlack of {id}"
    );
    assert_eq!(activity.latest_start_time(), Some(lst), "LST of {id}");
    assert_eq!(activity.latest_finish_time, Some(lft), "LFT of {id}");
}

#[test]
fn calculate_critical_path_forward_flow_then_as_expected() {
    let mut builder = new_builder();
    builder.add_activity(Act::new(1, 6));
    builder.add_activity(Act::new(2, 7));
    builder.add_activity(Act::new(3, 8));
    builder.add_activity_with_dependencies(Act::new(4, 11), IndexSet::from([2]));
    builder.add_activity_with_dependencies(Act::new(5, 8), IndexSet::from([1, 2, 3]));
    builder.add_activity_with_dependencies(Act::new(6, 7), IndexSet::from([3]));
    builder.add_activity_with_dependencies(Act::new(7, 4), IndexSet::from([4]));
    builder.add_activity_with_dependencies(Act::new(8, 4), IndexSet::from([4, 6]));
    builder.add_activity_with_dependencies(Act::new(9, 10), IndexSet::from([5]));

    builder.calculate_critical_path_forward_flow().unwrap();

    assert_forward(&builder, 1, 0, 6);
    assert_forward(&builder, 2, 0, 7);
    assert_forward(&builder, 3, 0, 8);
    assert_forward(&builder, 4, 7, 18);
    assert_forward(&builder, 5, 8, 16);
    assert_forward(&builder, 6, 8, 15);
    assert_forward(&builder, 7, 18, 22);
    assert_forward(&builder, 8, 18, 22);
    assert_forward(&builder, 9, 16, 26);
}

#[test]
fn calculate_critical_path_backward_flow_then_as_expected() {
    let mut builder = new_builder();
    builder.add_activity(Act::new(1, 6));
    builder.add_activity(Act::new(2, 7));
    builder.add_activity(Act::new(3, 8));
    builder.add_activity_with_dependencies(Act::new(4, 11), IndexSet::from([2]));
    builder.add_activity_with_dependencies(Act::new(5, 8), IndexSet::from([1, 2, 3]));
    builder.add_activity_with_dependencies(Act::new(6, 7), IndexSet::from([3]));
    builder.add_activity_with_dependencies(Act::new(7, 4), IndexSet::from([4]));
    builder.add_activity_with_dependencies(Act::new(8, 4), IndexSet::from([4, 6]));
    builder.add_activity_with_dependencies(Act::new(9, 10), IndexSet::from([5]));

    builder.calculate_critical_path_forward_flow().unwrap();
    builder.calculate_critical_path_backward_flow().unwrap();

    assert_backward(&builder, 1, 2, 8);
    assert_backward(&builder, 2, 1, 8);
    assert_backward(&builder, 3, 0, 8);
    assert_backward(&builder, 4, 11, 22);
    assert_backward(&builder, 5, 8, 16);
    assert_backward(&builder, 6, 15, 22);
    assert_backward(&builder, 7, 22, 26);
    assert_backward(&builder, 8, 22, 26);
    assert_backward(&builder, 9, 16, 26);
}

#[test]
fn calculate_critical_path_then_as_expected() {
    let mut builder = new_builder();
    builder.add_activity(Act::new(1, 6));
    builder.add_activity(Act::new(2, 7));
    builder.add_activity(Act::new(3, 8));
    builder.add_activity_with_dependencies(Act::new(4, 11), IndexSet::from([2]));
    builder.add_activity_with_dependencies(Act::new(5, 8), IndexSet::from([1, 2, 3]));
    builder.add_activity_with_dependencies(Act::new(6, 7), IndexSet::from([3]));
    builder.add_activity_with_dependencies(Act::new(7, 4), IndexSet::from([4]));
    builder.add_activity_with_dependencies(Act::new(8, 4), IndexSet::from([4, 6]));
    builder.add_activity_with_dependencies(Act::new(9, 10), IndexSet::from([5]));

    builder.calculate_critical_path().unwrap();

    assert_cpm(&builder, 1, 0, 6, 2, 2, 2, 8);
    assert_cpm(&builder, 2, 0, 7, 0, 1, 1, 8);
    assert_cpm(&builder, 3, 0, 8, 0, 0, 0, 8);
    assert_cpm(&builder, 4, 7, 18, 0, 4, 11, 22);
    assert_cpm(&builder, 5, 8, 16, 0, 0, 8, 16);
    assert_cpm(&builder, 6, 8, 15, 3, 7, 15, 22);
    assert_cpm(&builder, 7, 18, 22, 4, 4, 22, 26);
    assert_cpm(&builder, 8, 18, 22, 4, 4, 22, 26);
    assert_cpm(&builder, 9, 16, 26, 0, 0, 16, 26);
}

#[test]
fn calculate_critical_path_when_minimum_free_slack_in_start_activity_then_as_expected() {
    let mut builder = new_builder();
    builder.add_activity({
        let mut a = Act::new(1, 6);
        a.minimum_free_slack = Some(10);
        a
    });
    builder.add_activity(Act::new(2, 7));
    builder.add_activity(Act::new(3, 8));
    builder.add_activity_with_dependencies(Act::new(4, 11), IndexSet::from([2]));
    builder.add_activity_with_dependencies(Act::new(5, 8), IndexSet::from([1, 2, 3]));
    builder.add_activity_with_dependencies(Act::new(6, 7), IndexSet::from([3]));
    builder.add_activity_with_dependencies(Act::new(7, 4), IndexSet::from([4]));
    builder.add_activity_with_dependencies(Act::new(8, 4), IndexSet::from([4, 6]));
    builder.add_activity_with_dependencies(Act::new(9, 10), IndexSet::from([5]));

    builder.calculate_critical_path().unwrap();

    assert_cpm(&builder, 1, 0, 6, 10, 10, 10, 16);
    assert_cpm(&builder, 2, 0, 7, 0, 9, 9, 16);
    assert_cpm(&builder, 3, 0, 8, 0, 8, 8, 16);
    assert_cpm(&builder, 4, 7, 18, 0, 12, 19, 30);
    assert_cpm(&builder, 5, 16, 24, 0, 0, 16, 24);
    assert_cpm(&builder, 6, 8, 15, 3, 15, 23, 30);
    assert_cpm(&builder, 7, 18, 22, 12, 12, 30, 34);
    assert_cpm(&builder, 8, 18, 22, 12, 12, 30, 34);
    assert_cpm(&builder, 9, 24, 34, 0, 0, 24, 34);
}

#[test]
fn calculate_critical_path_when_minimum_free_slack_in_normal_activity_then_as_expected() {
    let mut builder = new_builder();
    builder.add_activity(Act::new(1, 6));
    builder.add_activity(Act::new(2, 7));
    builder.add_activity(Act::new(3, 8));
    builder.add_activity_with_dependencies(Act::new(4, 11), IndexSet::from([2]));
    builder.add_activity_with_dependencies(
        {
            let mut a = Act::new(5, 8);
            a.minimum_free_slack = Some(15);
            a
        },
        IndexSet::from([1, 2, 3]),
    );
    builder.add_activity_with_dependencies(Act::new(6, 7), IndexSet::from([3]));
    builder.add_activity_with_dependencies(Act::new(7, 4), IndexSet::from([4]));
    builder.add_activity_with_dependencies(Act::new(8, 4), IndexSet::from([4, 6]));
    builder.add_activity_with_dependencies(Act::new(9, 10), IndexSet::from([5]));

    builder.calculate_critical_path().unwrap();

    assert_cpm(&builder, 1, 0, 6, 2, 17, 17, 23);
    assert_cpm(&builder, 2, 0, 7, 0, 16, 16, 23);
    assert_cpm(&builder, 3, 0, 8, 0, 15, 15, 23);
    assert_cpm(&builder, 4, 7, 18, 0, 19, 26, 37);
    assert_cpm(&builder, 5, 8, 16, 15, 15, 23, 31);
    assert_cpm(&builder, 6, 8, 15, 3, 22, 30, 37);
    assert_cpm(&builder, 7, 18, 22, 19, 19, 37, 41);
    assert_cpm(&builder, 8, 18, 22, 19, 19, 37, 41);
    assert_cpm(&builder, 9, 31, 41, 0, 0, 31, 41);
}

#[test]
fn calculate_critical_path_when_minimum_free_slack_in_end_activity_then_as_expected() {
    let mut builder = new_builder();
    builder.add_activity(Act::new(1, 6));
    builder.add_activity(Act::new(2, 7));
    builder.add_activity(Act::new(3, 8));
    builder.add_activity_with_dependencies(Act::new(4, 11), IndexSet::from([2]));
    builder.add_activity_with_dependencies(Act::new(5, 8), IndexSet::from([1, 2, 3]));
    builder.add_activity_with_dependencies(Act::new(6, 7), IndexSet::from([3]));
    builder.add_activity_with_dependencies(Act::new(7, 4), IndexSet::from([4]));
    builder.add_activity_with_dependencies(Act::new(8, 4), IndexSet::from([4, 6]));
    builder.add_activity_with_dependencies(
        {
            let mut a = Act::new(9, 10);
            a.minimum_free_slack = Some(15);
            a
        },
        IndexSet::from([5]),
    );

    builder.calculate_critical_path().unwrap();

    assert_cpm(&builder, 1, 0, 6, 2, 17, 17, 23);
    assert_cpm(&builder, 2, 0, 7, 0, 16, 16, 23);
    assert_cpm(&builder, 3, 0, 8, 0, 15, 15, 23);
    assert_cpm(&builder, 4, 7, 18, 0, 19, 26, 37);
    assert_cpm(&builder, 5, 8, 16, 0, 15, 23, 31);
    assert_cpm(&builder, 6, 8, 15, 3, 22, 30, 37);
    assert_cpm(&builder, 7, 18, 22, 19, 19, 37, 41);
    assert_cpm(&builder, 8, 18, 22, 19, 19, 37, 41);
    assert_cpm(&builder, 9, 16, 26, 15, 15, 31, 41);
}

#[test]
fn calculate_critical_path_when_minimum_earliest_start_time_in_start_activity_then_as_expected() {
    let mut builder = new_builder();
    builder.add_activity({
        let mut a = Act::new(1, 6);
        a.minimum_earliest_start_time = Some(10);
        a
    });
    builder.add_activity(Act::new(2, 7));
    builder.add_activity(Act::new(3, 8));
    builder.add_activity_with_dependencies(Act::new(4, 11), IndexSet::from([2]));
    builder.add_activity_with_dependencies(Act::new(5, 8), IndexSet::from([1, 2, 3]));
    builder.add_activity_with_dependencies(Act::new(6, 7), IndexSet::from([3]));
    builder.add_activity_with_dependencies(Act::new(7, 4), IndexSet::from([4]));
    builder.add_activity_with_dependencies(Act::new(8, 4), IndexSet::from([4, 6]));
    builder.add_activity_with_dependencies(Act::new(9, 10), IndexSet::from([5]));

    builder.calculate_critical_path().unwrap();

    assert_cpm(&builder, 1, 10, 16, 0, 0, 10, 16);
    assert_cpm(&builder, 2, 0, 7, 0, 9, 9, 16);
    assert_cpm(&builder, 3, 0, 8, 0, 8, 8, 16);
    assert_cpm(&builder, 4, 7, 18, 0, 12, 19, 30);
    assert_cpm(&builder, 5, 16, 24, 0, 0, 16, 24);
    assert_cpm(&builder, 6, 8, 15, 3, 15, 23, 30);
    assert_cpm(&builder, 7, 18, 22, 12, 12, 30, 34);
    assert_cpm(&builder, 8, 18, 22, 12, 12, 30, 34);
    assert_cpm(&builder, 9, 24, 34, 0, 0, 24, 34);
}

#[test]
fn calculate_critical_path_when_minimum_earliest_start_time_in_normal_activity_then_as_expected() {
    let mut builder = new_builder();
    builder.add_activity(Act::new(1, 6));
    builder.add_activity(Act::new(2, 7));
    builder.add_activity(Act::new(3, 8));
    builder.add_activity_with_dependencies(Act::new(4, 11), IndexSet::from([2]));
    builder.add_activity_with_dependencies(
        {
            let mut a = Act::new(5, 8);
            a.minimum_earliest_start_time = Some(10);
            a
        },
        IndexSet::from([1, 2, 3]),
    );
    builder.add_activity_with_dependencies(Act::new(6, 7), IndexSet::from([3]));
    builder.add_activity_with_dependencies(Act::new(7, 4), IndexSet::from([4]));
    builder.add_activity_with_dependencies(Act::new(8, 4), IndexSet::from([4, 6]));
    builder.add_activity_with_dependencies(Act::new(9, 10), IndexSet::from([5]));

    builder.calculate_critical_path().unwrap();

    assert_cpm(&builder, 1, 0, 6, 4, 4, 4, 10);
    assert_cpm(&builder, 2, 0, 7, 0, 3, 3, 10);
    assert_cpm(&builder, 3, 0, 8, 0, 2, 2, 10);
    assert_cpm(&builder, 4, 7, 18, 0, 6, 13, 24);
    assert_cpm(&builder, 5, 10, 18, 0, 0, 10, 18);
    assert_cpm(&builder, 6, 8, 15, 3, 9, 17, 24);
    assert_cpm(&builder, 7, 18, 22, 6, 6, 24, 28);
    assert_cpm(&builder, 8, 18, 22, 6, 6, 24, 28);
    assert_cpm(&builder, 9, 18, 28, 0, 0, 18, 28);
}

#[test]
fn calculate_critical_path_when_minimum_earliest_start_time_in_end_activity_then_as_expected() {
    let mut builder = new_builder();
    builder.add_activity(Act::new(1, 6));
    builder.add_activity(Act::new(2, 7));
    builder.add_activity(Act::new(3, 8));
    builder.add_activity_with_dependencies(Act::new(4, 11), IndexSet::from([2]));
    builder.add_activity_with_dependencies(Act::new(5, 8), IndexSet::from([1, 2, 3]));
    builder.add_activity_with_dependencies(Act::new(6, 7), IndexSet::from([3]));
    builder.add_activity_with_dependencies(Act::new(7, 4), IndexSet::from([4]));
    builder.add_activity_with_dependencies(Act::new(8, 4), IndexSet::from([4, 6]));
    builder.add_activity_with_dependencies(
        {
            let mut a = Act::new(9, 10);
            a.minimum_earliest_start_time = Some(20);
            a
        },
        IndexSet::from([5]),
    );

    builder.calculate_critical_path().unwrap();

    assert_cpm(&builder, 1, 0, 6, 2, 6, 6, 12);
    assert_cpm(&builder, 2, 0, 7, 0, 5, 5, 12);
    assert_cpm(&builder, 3, 0, 8, 0, 4, 4, 12);
    assert_cpm(&builder, 4, 7, 18, 0, 8, 15, 26);
    assert_cpm(&builder, 5, 8, 16, 4, 4, 12, 20);
    assert_cpm(&builder, 6, 8, 15, 3, 11, 19, 26);
    assert_cpm(&builder, 7, 18, 22, 8, 8, 26, 30);
    assert_cpm(&builder, 8, 18, 22, 8, 8, 26, 30);
    assert_cpm(&builder, 9, 20, 30, 0, 0, 20, 30);
}

#[test]
fn calculate_critical_path_when_maximum_latest_finish_time_in_start_activity_then_as_expected() {
    let mut builder = new_builder();
    builder.add_activity({
        let mut a = Act::new(1, 6);
        a.maximum_latest_finish_time = Some(7);
        a
    });
    builder.add_activity(Act::new(2, 7));
    builder.add_activity(Act::new(3, 8));
    builder.add_activity_with_dependencies(Act::new(4, 11), IndexSet::from([2]));
    builder.add_activity_with_dependencies(Act::new(5, 8), IndexSet::from([1, 2, 3]));
    builder.add_activity_with_dependencies(Act::new(6, 7), IndexSet::from([3]));
    builder.add_activity_with_dependencies(Act::new(7, 4), IndexSet::from([4]));
    builder.add_activity_with_dependencies(Act::new(8, 4), IndexSet::from([4, 6]));
    builder.add_activity_with_dependencies(Act::new(9, 10), IndexSet::from([5]));

    builder.calculate_critical_path().unwrap();

    assert_cpm(&builder, 1, 0, 6, 1, 1, 1, 7);
    assert_cpm(&builder, 2, 0, 7, 0, 1, 1, 8);
    assert_cpm(&builder, 3, 0, 8, 0, 0, 0, 8);
    assert_cpm(&builder, 4, 7, 18, 0, 4, 11, 22);
    assert_cpm(&builder, 5, 8, 16, 0, 0, 8, 16);
    assert_cpm(&builder, 6, 8, 15, 3, 7, 15, 22);
    assert_cpm(&builder, 7, 18, 22, 4, 4, 22, 26);
    assert_cpm(&builder, 8, 18, 22, 4, 4, 22, 26);
    assert_cpm(&builder, 9, 16, 26, 0, 0, 16, 26);
}

#[test]
fn calculate_critical_path_when_extreme_maximum_latest_finish_time_in_start_activity_then_as_expected(
) {
    let mut builder = new_builder();
    builder.add_activity({
        let mut a = Act::new(1, 6);
        a.maximum_latest_finish_time = Some(5);
        a
    });
    builder.add_activity(Act::new(2, 7));
    builder.add_activity(Act::new(3, 8));
    builder.add_activity_with_dependencies(Act::new(4, 11), IndexSet::from([2]));
    builder.add_activity_with_dependencies(Act::new(5, 8), IndexSet::from([1, 2, 3]));
    builder.add_activity_with_dependencies(Act::new(6, 7), IndexSet::from([3]));
    builder.add_activity_with_dependencies(Act::new(7, 4), IndexSet::from([4]));
    builder.add_activity_with_dependencies(Act::new(8, 4), IndexSet::from([4, 6]));
    builder.add_activity_with_dependencies(Act::new(9, 10), IndexSet::from([5]));

    builder.calculate_critical_path().unwrap();

    assert_cpm(&builder, 1, -1, 5, 0, 0, -1, 5);
    assert_cpm(&builder, 2, 0, 7, 0, 1, 1, 8);
    assert_cpm(&builder, 3, 0, 8, 0, 0, 0, 8);
    assert_cpm(&builder, 4, 7, 18, 0, 4, 11, 22);
    assert_cpm(&builder, 5, 8, 16, 0, 0, 8, 16);
    assert_cpm(&builder, 6, 8, 15, 3, 7, 15, 22);
    assert_cpm(&builder, 7, 18, 22, 4, 4, 22, 26);
    assert_cpm(&builder, 8, 18, 22, 4, 4, 22, 26);
    assert_cpm(&builder, 9, 16, 26, 0, 0, 16, 26);
}

#[test]
fn calculate_critical_path_when_maximum_latest_finish_time_in_normal_activity_then_as_expected() {
    let mut builder = new_builder();
    builder.add_activity(Act::new(1, 6));
    builder.add_activity(Act::new(2, 7));
    builder.add_activity(Act::new(3, 8));
    builder.add_activity_with_dependencies(
        {
            let mut a = Act::new(4, 11);
            a.maximum_latest_finish_time = Some(18);
            a
        },
        IndexSet::from([2]),
    );
    builder.add_activity_with_dependencies(Act::new(5, 8), IndexSet::from([1, 2, 3]));
    builder.add_activity_with_dependencies(Act::new(6, 7), IndexSet::from([3]));
    builder.add_activity_with_dependencies(Act::new(7, 4), IndexSet::from([4]));
    builder.add_activity_with_dependencies(Act::new(8, 4), IndexSet::from([4, 6]));
    builder.add_activity_with_dependencies(Act::new(9, 10), IndexSet::from([5]));

    builder.calculate_critical_path().unwrap();

    assert_cpm(&builder, 1, 0, 6, 2, 2, 2, 8);
    assert_cpm(&builder, 2, 0, 7, 0, 0, 0, 7);
    assert_cpm(&builder, 3, 0, 8, 0, 0, 0, 8);
    assert_cpm(&builder, 4, 7, 18, 0, 0, 7, 18);
    assert_cpm(&builder, 5, 8, 16, 0, 0, 8, 16);
    assert_cpm(&builder, 6, 8, 15, 3, 7, 15, 22);
    assert_cpm(&builder, 7, 18, 22, 4, 4, 22, 26);
    assert_cpm(&builder, 8, 18, 22, 4, 4, 22, 26);
    assert_cpm(&builder, 9, 16, 26, 0, 0, 16, 26);
}

#[test]
fn calculate_critical_path_when_extreme_maximum_latest_finish_time_in_normal_activity_then_as_expected(
) {
    let mut builder = new_builder();
    builder.add_activity(Act::new(1, 6));
    builder.add_activity(Act::new(2, 7));
    builder.add_activity(Act::new(3, 8));
    builder.add_activity_with_dependencies(
        {
            let mut a = Act::new(4, 11);
            a.maximum_latest_finish_time = Some(16);
            a
        },
        IndexSet::from([2]),
    );
    builder.add_activity_with_dependencies(Act::new(5, 8), IndexSet::from([1, 2, 3]));
    builder.add_activity_with_dependencies(Act::new(6, 7), IndexSet::from([3]));
    builder.add_activity_with_dependencies(Act::new(7, 4), IndexSet::from([4]));
    builder.add_activity_with_dependencies(Act::new(8, 4), IndexSet::from([4, 6]));
    builder.add_activity_with_dependencies(Act::new(9, 10), IndexSet::from([5]));

    builder.calculate_critical_path().unwrap();

    assert_cpm(&builder, 1, 0, 6, 2, 2, 2, 8);
    assert_cpm(&builder, 2, 0, 7, -2, -2, -2, 5);
    assert_cpm(&builder, 3, 0, 8, 0, 0, 0, 8);
    assert_cpm(&builder, 4, 5, 16, 0, 0, 5, 16);
    assert_cpm(&builder, 5, 8, 16, 0, 0, 8, 16);
    assert_cpm(&builder, 6, 8, 15, 1, 7, 15, 22);
    assert_cpm(&builder, 7, 16, 20, 6, 6, 22, 26);
    assert_cpm(&builder, 8, 16, 20, 6, 6, 22, 26);
    assert_cpm(&builder, 9, 16, 26, 0, 0, 16, 26);
}

#[test]
fn calculate_critical_path_when_maximum_latest_finish_time_in_end_activity_then_as_expected() {
    let mut builder = new_builder();
    builder.add_activity(Act::new(1, 6));
    builder.add_activity(Act::new(2, 7));
    builder.add_activity(Act::new(3, 8));
    builder.add_activity_with_dependencies(Act::new(4, 11), IndexSet::from([2]));
    builder.add_activity_with_dependencies(Act::new(5, 8), IndexSet::from([1, 2, 3]));
    builder.add_activity_with_dependencies(Act::new(6, 7), IndexSet::from([3]));
    builder.add_activity_with_dependencies(Act::new(7, 4), IndexSet::from([4]));
    builder.add_activity_with_dependencies(
        {
            let mut a = Act::new(8, 4);
            a.maximum_latest_finish_time = Some(22);
            a
        },
        IndexSet::from([4, 6]),
    );
    builder.add_activity_with_dependencies(Act::new(9, 10), IndexSet::from([5]));

    builder.calculate_critical_path().unwrap();

    assert_cpm(&builder, 1, 0, 6, 2, 2, 2, 8);
    assert_cpm(&builder, 2, 0, 7, 0, 0, 0, 7);
    assert_cpm(&builder, 3, 0, 8, 0, 0, 0, 8);
    assert_cpm(&builder, 4, 7, 18, 0, 0, 7, 18);
    assert_cpm(&builder, 5, 8, 16, 0, 0, 8, 16);
    assert_cpm(&builder, 6, 8, 15, 3, 3, 11, 18);
    assert_cpm(&builder, 7, 18, 22, 4, 4, 22, 26);
    assert_cpm(&builder, 8, 18, 22, 0, 0, 18, 22);
    assert_cpm(&builder, 9, 16, 26, 0, 0, 16, 26);
}

#[test]
fn calculate_critical_path_when_extreme_maximum_latest_finish_time_in_end_activity_then_as_expected(
) {
    let mut builder = new_builder();
    builder.add_activity(Act::new(1, 6));
    builder.add_activity(Act::new(2, 7));
    builder.add_activity(Act::new(3, 8));
    builder.add_activity_with_dependencies(Act::new(4, 11), IndexSet::from([2]));
    builder.add_activity_with_dependencies(Act::new(5, 8), IndexSet::from([1, 2, 3]));
    builder.add_activity_with_dependencies(Act::new(6, 7), IndexSet::from([3]));
    builder.add_activity_with_dependencies(Act::new(7, 4), IndexSet::from([4]));
    builder.add_activity_with_dependencies(
        {
            let mut a = Act::new(8, 4);
            a.maximum_latest_finish_time = Some(21);
            a
        },
        IndexSet::from([4, 6]),
    );
    builder.add_activity_with_dependencies(Act::new(9, 10), IndexSet::from([5]));

    builder.calculate_critical_path().unwrap();

    assert_cpm(&builder, 1, 0, 6, 2, 2, 2, 8);
    assert_cpm(&builder, 2, 0, 7, -1, -1, -1, 6);
    assert_cpm(&builder, 3, 0, 8, 0, 0, 0, 8);
    assert_cpm(&builder, 4, 7, 18, -1, -1, 6, 17);
    assert_cpm(&builder, 5, 8, 16, 0, 0, 8, 16);
    assert_cpm(&builder, 6, 8, 15, 2, 2, 10, 17);
    assert_cpm(&builder, 7, 18, 22, 4, 4, 22, 26);
    assert_cpm(&builder, 8, 17, 21, 0, 0, 17, 21);
    assert_cpm(&builder, 9, 16, 26, 0, 0, 16, 26);
}

#[test]
fn calculate_critical_path_when_minimum_earliest_start_time_and_maximum_latest_finish_time_are_invalid_then_error(
) {
    let mut builder = new_builder();
    builder.add_activity(Act::new(1, 6));
    builder.add_activity(Act::new(2, 7));
    builder.add_activity(Act::new(3, 8));
    // Duration 11 with earliest start >= 7 finishes at 18, past the latest
    // finish of 17: an impossible window that the calculation must reject.
    builder.add_activity_with_dependencies(
        {
            let mut a = Act::new(4, 11);
            a.minimum_earliest_start_time = Some(7);
            a.maximum_latest_finish_time = Some(17);
            a
        },
        IndexSet::from([2]),
    );
    builder.add_activity_with_dependencies(Act::new(5, 8), IndexSet::from([1, 2, 3]));
    builder.add_activity_with_dependencies(Act::new(6, 7), IndexSet::from([3]));
    builder.add_activity_with_dependencies(Act::new(7, 4), IndexSet::from([4]));
    builder.add_activity_with_dependencies(Act::new(8, 4), IndexSet::from([4, 6]));
    builder.add_activity_with_dependencies(Act::new(9, 10), IndexSet::from([5]));

    assert!(builder.calculate_critical_path().is_err());
}

#[test]
fn calculate_critical_path_forward_flow_when_adding_and_removing_dependencies_then_as_expected() {
    let mut builder = new_builder();
    builder.add_activity(Act::new(1, 6));
    builder.add_activity(Act::new(2, 7));
    builder.add_activity(Act::new(3, 8));
    builder.add_activity_with_dependencies(Act::new(4, 11), IndexSet::from([2]));
    builder.add_activity_with_dependencies(Act::new(5, 8), IndexSet::from([1, 2, 3]));
    builder.add_activity_with_dependencies(Act::new(6, 7), IndexSet::from([3]));
    builder.add_activity_with_dependencies(Act::new(7, 4), IndexSet::from([4]));
    builder.add_activity_with_dependencies(Act::new(8, 4), IndexSet::from([4, 6]));
    builder.add_activity_with_dependencies(Act::new(9, 10), IndexSet::from([5]));
    assert_eq!(builder.nodes().count(), 9);
    assert_eq!(builder.edges().count(), 9);
    assert!(builder.add_activity_dependencies(6, IndexSet::from([5])));
    assert_eq!(builder.nodes().count(), 9);
    assert_eq!(builder.edges().count(), 10);
    builder.clear_critical_path_variables();
    builder.calculate_critical_path_forward_flow().unwrap();
    assert_eq!(
        builder.activity(1).unwrap().earliest_start_time,
        Some(0),
        "EarliestStartTime of 1"
    );
    assert_eq!(
        builder.activity(1).unwrap().earliest_finish_time(),
        Some(6),
        "EarliestFinishTime of 1"
    );
    assert_eq!(
        builder.activity(2).unwrap().earliest_start_time,
        Some(0),
        "EarliestStartTime of 2"
    );
    assert_eq!(
        builder.activity(2).unwrap().earliest_finish_time(),
        Some(7),
        "EarliestFinishTime of 2"
    );
    assert_eq!(
        builder.activity(3).unwrap().earliest_start_time,
        Some(0),
        "EarliestStartTime of 3"
    );
    assert_eq!(
        builder.activity(3).unwrap().earliest_finish_time(),
        Some(8),
        "EarliestFinishTime of 3"
    );
    assert_eq!(
        builder.activity(4).unwrap().earliest_start_time,
        Some(7),
        "EarliestStartTime of 4"
    );
    assert_eq!(
        builder.activity(4).unwrap().earliest_finish_time(),
        Some(18),
        "EarliestFinishTime of 4"
    );
    assert_eq!(
        builder.activity(5).unwrap().earliest_start_time,
        Some(8),
        "EarliestStartTime of 5"
    );
    assert_eq!(
        builder.activity(5).unwrap().earliest_finish_time(),
        Some(16),
        "EarliestFinishTime of 5"
    );
    assert_eq!(
        builder.activity(6).unwrap().earliest_start_time,
        Some(16),
        "EarliestStartTime of 6"
    );
    assert_eq!(
        builder.activity(6).unwrap().earliest_finish_time(),
        Some(23),
        "EarliestFinishTime of 6"
    );
    assert_eq!(
        builder.activity(7).unwrap().earliest_start_time,
        Some(18),
        "EarliestStartTime of 7"
    );
    assert_eq!(
        builder.activity(7).unwrap().earliest_finish_time(),
        Some(22),
        "EarliestFinishTime of 7"
    );
    assert_eq!(
        builder.activity(8).unwrap().earliest_start_time,
        Some(23),
        "EarliestStartTime of 8"
    );
    assert_eq!(
        builder.activity(8).unwrap().earliest_finish_time(),
        Some(27),
        "EarliestFinishTime of 8"
    );
    assert_eq!(
        builder.activity(9).unwrap().earliest_start_time,
        Some(16),
        "EarliestStartTime of 9"
    );
    assert_eq!(
        builder.activity(9).unwrap().earliest_finish_time(),
        Some(26),
        "EarliestFinishTime of 9"
    );
    assert!(builder.add_activity_dependencies(6, IndexSet::from([4])));
    assert_eq!(builder.nodes().count(), 9);
    assert_eq!(builder.edges().count(), 11);
    builder.clear_critical_path_variables();
    builder.calculate_critical_path_forward_flow().unwrap();
    assert_eq!(
        builder.activity(1).unwrap().earliest_start_time,
        Some(0),
        "EarliestStartTime of 1"
    );
    assert_eq!(
        builder.activity(1).unwrap().earliest_finish_time(),
        Some(6),
        "EarliestFinishTime of 1"
    );
    assert_eq!(
        builder.activity(2).unwrap().earliest_start_time,
        Some(0),
        "EarliestStartTime of 2"
    );
    assert_eq!(
        builder.activity(2).unwrap().earliest_finish_time(),
        Some(7),
        "EarliestFinishTime of 2"
    );
    assert_eq!(
        builder.activity(3).unwrap().earliest_start_time,
        Some(0),
        "EarliestStartTime of 3"
    );
    assert_eq!(
        builder.activity(3).unwrap().earliest_finish_time(),
        Some(8),
        "EarliestFinishTime of 3"
    );
    assert_eq!(
        builder.activity(4).unwrap().earliest_start_time,
        Some(7),
        "EarliestStartTime of 4"
    );
    assert_eq!(
        builder.activity(4).unwrap().earliest_finish_time(),
        Some(18),
        "EarliestFinishTime of 4"
    );
    assert_eq!(
        builder.activity(5).unwrap().earliest_start_time,
        Some(8),
        "EarliestStartTime of 5"
    );
    assert_eq!(
        builder.activity(5).unwrap().earliest_finish_time(),
        Some(16),
        "EarliestFinishTime of 5"
    );
    assert_eq!(
        builder.activity(6).unwrap().earliest_start_time,
        Some(18),
        "EarliestStartTime of 6"
    );
    assert_eq!(
        builder.activity(6).unwrap().earliest_finish_time(),
        Some(25),
        "EarliestFinishTime of 6"
    );
    assert_eq!(
        builder.activity(7).unwrap().earliest_start_time,
        Some(18),
        "EarliestStartTime of 7"
    );
    assert_eq!(
        builder.activity(7).unwrap().earliest_finish_time(),
        Some(22),
        "EarliestFinishTime of 7"
    );
    assert_eq!(
        builder.activity(8).unwrap().earliest_start_time,
        Some(25),
        "EarliestStartTime of 8"
    );
    assert_eq!(
        builder.activity(8).unwrap().earliest_finish_time(),
        Some(29),
        "EarliestFinishTime of 8"
    );
    assert_eq!(
        builder.activity(9).unwrap().earliest_start_time,
        Some(16),
        "EarliestStartTime of 9"
    );
    assert_eq!(
        builder.activity(9).unwrap().earliest_finish_time(),
        Some(26),
        "EarliestFinishTime of 9"
    );
    assert!(builder.remove_activity_dependencies(6, IndexSet::from([5])));
    assert_eq!(builder.nodes().count(), 9);
    assert_eq!(builder.edges().count(), 10);
    builder.clear_critical_path_variables();
    builder.calculate_critical_path_forward_flow().unwrap();
    assert_eq!(
        builder.activity(1).unwrap().earliest_start_time,
        Some(0),
        "EarliestStartTime of 1"
    );
    assert_eq!(
        builder.activity(1).unwrap().earliest_finish_time(),
        Some(6),
        "EarliestFinishTime of 1"
    );
    assert_eq!(
        builder.activity(2).unwrap().earliest_start_time,
        Some(0),
        "EarliestStartTime of 2"
    );
    assert_eq!(
        builder.activity(2).unwrap().earliest_finish_time(),
        Some(7),
        "EarliestFinishTime of 2"
    );
    assert_eq!(
        builder.activity(3).unwrap().earliest_start_time,
        Some(0),
        "EarliestStartTime of 3"
    );
    assert_eq!(
        builder.activity(3).unwrap().earliest_finish_time(),
        Some(8),
        "EarliestFinishTime of 3"
    );
    assert_eq!(
        builder.activity(4).unwrap().earliest_start_time,
        Some(7),
        "EarliestStartTime of 4"
    );
    assert_eq!(
        builder.activity(4).unwrap().earliest_finish_time(),
        Some(18),
        "EarliestFinishTime of 4"
    );
    assert_eq!(
        builder.activity(5).unwrap().earliest_start_time,
        Some(8),
        "EarliestStartTime of 5"
    );
    assert_eq!(
        builder.activity(5).unwrap().earliest_finish_time(),
        Some(16),
        "EarliestFinishTime of 5"
    );
    assert_eq!(
        builder.activity(6).unwrap().earliest_start_time,
        Some(18),
        "EarliestStartTime of 6"
    );
    assert_eq!(
        builder.activity(6).unwrap().earliest_finish_time(),
        Some(25),
        "EarliestFinishTime of 6"
    );
    assert_eq!(
        builder.activity(7).unwrap().earliest_start_time,
        Some(18),
        "EarliestStartTime of 7"
    );
    assert_eq!(
        builder.activity(7).unwrap().earliest_finish_time(),
        Some(22),
        "EarliestFinishTime of 7"
    );
    assert_eq!(
        builder.activity(8).unwrap().earliest_start_time,
        Some(25),
        "EarliestStartTime of 8"
    );
    assert_eq!(
        builder.activity(8).unwrap().earliest_finish_time(),
        Some(29),
        "EarliestFinishTime of 8"
    );
    assert_eq!(
        builder.activity(9).unwrap().earliest_start_time,
        Some(16),
        "EarliestStartTime of 9"
    );
    assert_eq!(
        builder.activity(9).unwrap().earliest_finish_time(),
        Some(26),
        "EarliestFinishTime of 9"
    );
    assert!(builder.remove_activity_dependencies(6, IndexSet::from([4])));
    assert_eq!(builder.nodes().count(), 9);
    assert_eq!(builder.edges().count(), 9);
    builder.clear_critical_path_variables();
    builder.calculate_critical_path_forward_flow().unwrap();
    assert_eq!(
        builder.activity(1).unwrap().earliest_start_time,
        Some(0),
        "EarliestStartTime of 1"
    );
    assert_eq!(
        builder.activity(1).unwrap().earliest_finish_time(),
        Some(6),
        "EarliestFinishTime of 1"
    );
    assert_eq!(
        builder.activity(2).unwrap().earliest_start_time,
        Some(0),
        "EarliestStartTime of 2"
    );
    assert_eq!(
        builder.activity(2).unwrap().earliest_finish_time(),
        Some(7),
        "EarliestFinishTime of 2"
    );
    assert_eq!(
        builder.activity(3).unwrap().earliest_start_time,
        Some(0),
        "EarliestStartTime of 3"
    );
    assert_eq!(
        builder.activity(3).unwrap().earliest_finish_time(),
        Some(8),
        "EarliestFinishTime of 3"
    );
    assert_eq!(
        builder.activity(4).unwrap().earliest_start_time,
        Some(7),
        "EarliestStartTime of 4"
    );
    assert_eq!(
        builder.activity(4).unwrap().earliest_finish_time(),
        Some(18),
        "EarliestFinishTime of 4"
    );
    assert_eq!(
        builder.activity(5).unwrap().earliest_start_time,
        Some(8),
        "EarliestStartTime of 5"
    );
    assert_eq!(
        builder.activity(5).unwrap().earliest_finish_time(),
        Some(16),
        "EarliestFinishTime of 5"
    );
    assert_eq!(
        builder.activity(6).unwrap().earliest_start_time,
        Some(8),
        "EarliestStartTime of 6"
    );
    assert_eq!(
        builder.activity(6).unwrap().earliest_finish_time(),
        Some(15),
        "EarliestFinishTime of 6"
    );
    assert_eq!(
        builder.activity(7).unwrap().earliest_start_time,
        Some(18),
        "EarliestStartTime of 7"
    );
    assert_eq!(
        builder.activity(7).unwrap().earliest_finish_time(),
        Some(22),
        "EarliestFinishTime of 7"
    );
    assert_eq!(
        builder.activity(8).unwrap().earliest_start_time,
        Some(18),
        "EarliestStartTime of 8"
    );
    assert_eq!(
        builder.activity(8).unwrap().earliest_finish_time(),
        Some(22),
        "EarliestFinishTime of 8"
    );
    assert_eq!(
        builder.activity(9).unwrap().earliest_start_time,
        Some(16),
        "EarliestStartTime of 9"
    );
    assert_eq!(
        builder.activity(9).unwrap().earliest_finish_time(),
        Some(26),
        "EarliestFinishTime of 9"
    );
}

#[test]
fn calculate_critical_path_backward_flow_when_adding_and_removing_dependencies_then_as_expected() {
    let mut builder = new_builder();
    builder.add_activity(Act::new(1, 6));
    builder.add_activity(Act::new(2, 7));
    builder.add_activity(Act::new(3, 8));
    builder.add_activity_with_dependencies(Act::new(4, 11), IndexSet::from([2]));
    builder.add_activity_with_dependencies(Act::new(5, 8), IndexSet::from([1, 2, 3]));
    builder.add_activity_with_dependencies(Act::new(6, 7), IndexSet::from([3]));
    builder.add_activity_with_dependencies(Act::new(7, 4), IndexSet::from([4]));
    builder.add_activity_with_dependencies(Act::new(8, 4), IndexSet::from([4, 6]));
    builder.add_activity_with_dependencies(Act::new(9, 10), IndexSet::from([5]));
    assert_eq!(builder.nodes().count(), 9);
    assert_eq!(builder.edges().count(), 9);
    assert!(builder.add_activity_dependencies(6, IndexSet::from([5])));
    assert_eq!(builder.nodes().count(), 9);
    assert_eq!(builder.edges().count(), 10);
    builder.clear_critical_path_variables();
    builder.calculate_critical_path_forward_flow().unwrap();
    builder.calculate_critical_path_backward_flow().unwrap();
    assert_eq!(
        builder.activity(1).unwrap().latest_start_time(),
        Some(2),
        "LatestStartTime of 1"
    );
    assert_eq!(
        builder.activity(1).unwrap().latest_finish_time,
        Some(8),
        "LatestFinishTime of 1"
    );
    assert_eq!(
        builder.activity(2).unwrap().latest_start_time(),
        Some(1),
        "LatestStartTime of 2"
    );
    assert_eq!(
        builder.activity(2).unwrap().latest_finish_time,
        Some(8),
        "LatestFinishTime of 2"
    );
    assert_eq!(
        builder.activity(3).unwrap().latest_start_time(),
        Some(0),
        "LatestStartTime of 3"
    );
    assert_eq!(
        builder.activity(3).unwrap().latest_finish_time,
        Some(8),
        "LatestFinishTime of 3"
    );
    assert_eq!(
        builder.activity(4).unwrap().latest_start_time(),
        Some(12),
        "LatestStartTime of 4"
    );
    assert_eq!(
        builder.activity(4).unwrap().latest_finish_time,
        Some(23),
        "LatestFinishTime of 4"
    );
    assert_eq!(
        builder.activity(5).unwrap().latest_start_time(),
        Some(8),
        "LatestStartTime of 5"
    );
    assert_eq!(
        builder.activity(5).unwrap().latest_finish_time,
        Some(16),
        "LatestFinishTime of 5"
    );
    assert_eq!(
        builder.activity(6).unwrap().latest_start_time(),
        Some(16),
        "LatestStartTime of 6"
    );
    assert_eq!(
        builder.activity(6).unwrap().latest_finish_time,
        Some(23),
        "LatestFinishTime of 6"
    );
    assert_eq!(
        builder.activity(7).unwrap().latest_start_time(),
        Some(23),
        "LatestStartTime of 7"
    );
    assert_eq!(
        builder.activity(7).unwrap().latest_finish_time,
        Some(27),
        "LatestFinishTime of 7"
    );
    assert_eq!(
        builder.activity(8).unwrap().latest_start_time(),
        Some(23),
        "LatestStartTime of 8"
    );
    assert_eq!(
        builder.activity(8).unwrap().latest_finish_time,
        Some(27),
        "LatestFinishTime of 8"
    );
    assert_eq!(
        builder.activity(9).unwrap().latest_start_time(),
        Some(17),
        "LatestStartTime of 9"
    );
    assert_eq!(
        builder.activity(9).unwrap().latest_finish_time,
        Some(27),
        "LatestFinishTime of 9"
    );
    assert!(builder.add_activity_dependencies(6, IndexSet::from([4])));
    assert_eq!(builder.nodes().count(), 9);
    assert_eq!(builder.edges().count(), 11);
    builder.clear_critical_path_variables();
    builder.calculate_critical_path_forward_flow().unwrap();
    builder.calculate_critical_path_backward_flow().unwrap();
    assert_eq!(
        builder.activity(1).unwrap().latest_start_time(),
        Some(4),
        "LatestStartTime of 1"
    );
    assert_eq!(
        builder.activity(1).unwrap().latest_finish_time,
        Some(10),
        "LatestFinishTime of 1"
    );
    assert_eq!(
        builder.activity(2).unwrap().latest_start_time(),
        Some(0),
        "LatestStartTime of 2"
    );
    assert_eq!(
        builder.activity(2).unwrap().latest_finish_time,
        Some(7),
        "LatestFinishTime of 2"
    );
    assert_eq!(
        builder.activity(3).unwrap().latest_start_time(),
        Some(2),
        "LatestStartTime of 3"
    );
    assert_eq!(
        builder.activity(3).unwrap().latest_finish_time,
        Some(10),
        "LatestFinishTime of 3"
    );
    assert_eq!(
        builder.activity(4).unwrap().latest_start_time(),
        Some(7),
        "LatestStartTime of 4"
    );
    assert_eq!(
        builder.activity(4).unwrap().latest_finish_time,
        Some(18),
        "LatestFinishTime of 4"
    );
    assert_eq!(
        builder.activity(5).unwrap().latest_start_time(),
        Some(10),
        "LatestStartTime of 5"
    );
    assert_eq!(
        builder.activity(5).unwrap().latest_finish_time,
        Some(18),
        "LatestFinishTime of 5"
    );
    assert_eq!(
        builder.activity(6).unwrap().latest_start_time(),
        Some(18),
        "LatestStartTime of 6"
    );
    assert_eq!(
        builder.activity(6).unwrap().latest_finish_time,
        Some(25),
        "LatestFinishTime of 6"
    );
    assert_eq!(
        builder.activity(7).unwrap().latest_start_time(),
        Some(25),
        "LatestStartTime of 7"
    );
    assert_eq!(
        builder.activity(7).unwrap().latest_finish_time,
        Some(29),
        "LatestFinishTime of 7"
    );
    assert_eq!(
        builder.activity(8).unwrap().latest_start_time(),
        Some(25),
        "LatestStartTime of 8"
    );
    assert_eq!(
        builder.activity(8).unwrap().latest_finish_time,
        Some(29),
        "LatestFinishTime of 8"
    );
    assert_eq!(
        builder.activity(9).unwrap().latest_start_time(),
        Some(19),
        "LatestStartTime of 9"
    );
    assert_eq!(
        builder.activity(9).unwrap().latest_finish_time,
        Some(29),
        "LatestFinishTime of 9"
    );
    assert!(builder.remove_activity_dependencies(6, IndexSet::from([5])));
    assert_eq!(builder.nodes().count(), 9);
    assert_eq!(builder.edges().count(), 10);
    builder.clear_critical_path_variables();
    builder.calculate_critical_path_forward_flow().unwrap();
    builder.calculate_critical_path_backward_flow().unwrap();
    assert_eq!(
        builder.activity(1).unwrap().latest_start_time(),
        Some(5),
        "LatestStartTime of 1"
    );
    assert_eq!(
        builder.activity(1).unwrap().latest_finish_time,
        Some(11),
        "LatestFinishTime of 1"
    );
    assert_eq!(
        builder.activity(2).unwrap().latest_start_time(),
        Some(0),
        "LatestStartTime of 2"
    );
    assert_eq!(
        builder.activity(2).unwrap().latest_finish_time,
        Some(7),
        "LatestFinishTime of 2"
    );
    assert_eq!(
        builder.activity(3).unwrap().latest_start_time(),
        Some(3),
        "LatestStartTime of 3"
    );
    assert_eq!(
        builder.activity(3).unwrap().latest_finish_time,
        Some(11),
        "LatestFinishTime of 3"
    );
    assert_eq!(
        builder.activity(4).unwrap().latest_start_time(),
        Some(7),
        "LatestStartTime of 4"
    );
    assert_eq!(
        builder.activity(4).unwrap().latest_finish_time,
        Some(18),
        "LatestFinishTime of 4"
    );
    assert_eq!(
        builder.activity(5).unwrap().latest_start_time(),
        Some(11),
        "LatestStartTime of 5"
    );
    assert_eq!(
        builder.activity(5).unwrap().latest_finish_time,
        Some(19),
        "LatestFinishTime of 5"
    );
    assert_eq!(
        builder.activity(6).unwrap().latest_start_time(),
        Some(18),
        "LatestStartTime of 6"
    );
    assert_eq!(
        builder.activity(6).unwrap().latest_finish_time,
        Some(25),
        "LatestFinishTime of 6"
    );
    assert_eq!(
        builder.activity(7).unwrap().latest_start_time(),
        Some(25),
        "LatestStartTime of 7"
    );
    assert_eq!(
        builder.activity(7).unwrap().latest_finish_time,
        Some(29),
        "LatestFinishTime of 7"
    );
    assert_eq!(
        builder.activity(8).unwrap().latest_start_time(),
        Some(25),
        "LatestStartTime of 8"
    );
    assert_eq!(
        builder.activity(8).unwrap().latest_finish_time,
        Some(29),
        "LatestFinishTime of 8"
    );
    assert_eq!(
        builder.activity(9).unwrap().latest_start_time(),
        Some(19),
        "LatestStartTime of 9"
    );
    assert_eq!(
        builder.activity(9).unwrap().latest_finish_time,
        Some(29),
        "LatestFinishTime of 9"
    );
    assert!(builder.remove_activity_dependencies(6, IndexSet::from([4])));
    assert_eq!(builder.nodes().count(), 9);
    assert_eq!(builder.edges().count(), 9);
    builder.clear_critical_path_variables();
    builder.calculate_critical_path_forward_flow().unwrap();
    builder.calculate_critical_path_backward_flow().unwrap();
    assert_eq!(
        builder.activity(1).unwrap().earliest_start_time,
        Some(0),
        "EarliestStartTime of 1"
    );
    assert_eq!(
        builder.activity(1).unwrap().earliest_finish_time(),
        Some(6),
        "EarliestFinishTime of 1"
    );
    assert_eq!(
        builder.activity(2).unwrap().earliest_start_time,
        Some(0),
        "EarliestStartTime of 2"
    );
    assert_eq!(
        builder.activity(2).unwrap().earliest_finish_time(),
        Some(7),
        "EarliestFinishTime of 2"
    );
    assert_eq!(
        builder.activity(3).unwrap().earliest_start_time,
        Some(0),
        "EarliestStartTime of 3"
    );
    assert_eq!(
        builder.activity(3).unwrap().earliest_finish_time(),
        Some(8),
        "EarliestFinishTime of 3"
    );
    assert_eq!(
        builder.activity(4).unwrap().earliest_start_time,
        Some(7),
        "EarliestStartTime of 4"
    );
    assert_eq!(
        builder.activity(4).unwrap().earliest_finish_time(),
        Some(18),
        "EarliestFinishTime of 4"
    );
    assert_eq!(
        builder.activity(5).unwrap().earliest_start_time,
        Some(8),
        "EarliestStartTime of 5"
    );
    assert_eq!(
        builder.activity(5).unwrap().earliest_finish_time(),
        Some(16),
        "EarliestFinishTime of 5"
    );
    assert_eq!(
        builder.activity(6).unwrap().earliest_start_time,
        Some(8),
        "EarliestStartTime of 6"
    );
    assert_eq!(
        builder.activity(6).unwrap().earliest_finish_time(),
        Some(15),
        "EarliestFinishTime of 6"
    );
    assert_eq!(
        builder.activity(7).unwrap().earliest_start_time,
        Some(18),
        "EarliestStartTime of 7"
    );
    assert_eq!(
        builder.activity(7).unwrap().earliest_finish_time(),
        Some(22),
        "EarliestFinishTime of 7"
    );
    assert_eq!(
        builder.activity(8).unwrap().earliest_start_time,
        Some(18),
        "EarliestStartTime of 8"
    );
    assert_eq!(
        builder.activity(8).unwrap().earliest_finish_time(),
        Some(22),
        "EarliestFinishTime of 8"
    );
    assert_eq!(
        builder.activity(9).unwrap().earliest_start_time,
        Some(16),
        "EarliestStartTime of 9"
    );
    assert_eq!(
        builder.activity(9).unwrap().earliest_finish_time(),
        Some(26),
        "EarliestFinishTime of 9"
    );
}

#[test]
fn calculate_critical_path_when_adding_and_removing_dependencies_then_as_expected() {
    let mut builder = new_builder();
    builder.add_activity(Act::new(1, 6));
    builder.add_activity(Act::new(2, 7));
    builder.add_activity(Act::new(3, 8));
    builder.add_activity_with_dependencies(Act::new(4, 11), IndexSet::from([2]));
    builder.add_activity_with_dependencies(Act::new(5, 8), IndexSet::from([1, 2, 3]));
    builder.add_activity_with_dependencies(Act::new(6, 7), IndexSet::from([3]));
    builder.add_activity_with_dependencies(Act::new(7, 4), IndexSet::from([4]));
    builder.add_activity_with_dependencies(Act::new(8, 4), IndexSet::from([4, 6]));
    builder.add_activity_with_dependencies(Act::new(9, 10), IndexSet::from([5]));
    assert_eq!(builder.nodes().count(), 9);
    assert_eq!(builder.edges().count(), 9);
    assert!(builder.add_activity_dependencies(6, IndexSet::from([5])));
    assert_eq!(builder.nodes().count(), 9);
    assert_eq!(builder.edges().count(), 10);
    builder.calculate_critical_path().unwrap();
    assert_eq!(
        builder.activity(1).unwrap().latest_start_time(),
        Some(2),
        "LatestStartTime of 1"
    );
    assert_eq!(
        builder.activity(1).unwrap().latest_finish_time,
        Some(8),
        "LatestFinishTime of 1"
    );
    assert_eq!(
        builder.activity(2).unwrap().latest_start_time(),
        Some(1),
        "LatestStartTime of 2"
    );
    assert_eq!(
        builder.activity(2).unwrap().latest_finish_time,
        Some(8),
        "LatestFinishTime of 2"
    );
    assert_eq!(
        builder.activity(3).unwrap().latest_start_time(),
        Some(0),
        "LatestStartTime of 3"
    );
    assert_eq!(
        builder.activity(3).unwrap().latest_finish_time,
        Some(8),
        "LatestFinishTime of 3"
    );
    assert_eq!(
        builder.activity(4).unwrap().latest_start_time(),
        Some(12),
        "LatestStartTime of 4"
    );
    assert_eq!(
        builder.activity(4).unwrap().latest_finish_time,
        Some(23),
        "LatestFinishTime of 4"
    );
    assert_eq!(
        builder.activity(5).unwrap().latest_start_time(),
        Some(8),
        "LatestStartTime of 5"
    );
    assert_eq!(
        builder.activity(5).unwrap().latest_finish_time,
        Some(16),
        "LatestFinishTime of 5"
    );
    assert_eq!(
        builder.activity(6).unwrap().latest_start_time(),
        Some(16),
        "LatestStartTime of 6"
    );
    assert_eq!(
        builder.activity(6).unwrap().latest_finish_time,
        Some(23),
        "LatestFinishTime of 6"
    );
    assert_eq!(
        builder.activity(7).unwrap().latest_start_time(),
        Some(23),
        "LatestStartTime of 7"
    );
    assert_eq!(
        builder.activity(7).unwrap().latest_finish_time,
        Some(27),
        "LatestFinishTime of 7"
    );
    assert_eq!(
        builder.activity(8).unwrap().latest_start_time(),
        Some(23),
        "LatestStartTime of 8"
    );
    assert_eq!(
        builder.activity(8).unwrap().latest_finish_time,
        Some(27),
        "LatestFinishTime of 8"
    );
    assert_eq!(
        builder.activity(9).unwrap().latest_start_time(),
        Some(17),
        "LatestStartTime of 9"
    );
    assert_eq!(
        builder.activity(9).unwrap().latest_finish_time,
        Some(27),
        "LatestFinishTime of 9"
    );
    assert!(builder.add_activity_dependencies(6, IndexSet::from([4])));
    assert_eq!(builder.nodes().count(), 9);
    assert_eq!(builder.edges().count(), 11);
    builder.calculate_critical_path().unwrap();
    assert_eq!(
        builder.activity(1).unwrap().latest_start_time(),
        Some(4),
        "LatestStartTime of 1"
    );
    assert_eq!(
        builder.activity(1).unwrap().latest_finish_time,
        Some(10),
        "LatestFinishTime of 1"
    );
    assert_eq!(
        builder.activity(2).unwrap().latest_start_time(),
        Some(0),
        "LatestStartTime of 2"
    );
    assert_eq!(
        builder.activity(2).unwrap().latest_finish_time,
        Some(7),
        "LatestFinishTime of 2"
    );
    assert_eq!(
        builder.activity(3).unwrap().latest_start_time(),
        Some(2),
        "LatestStartTime of 3"
    );
    assert_eq!(
        builder.activity(3).unwrap().latest_finish_time,
        Some(10),
        "LatestFinishTime of 3"
    );
    assert_eq!(
        builder.activity(4).unwrap().latest_start_time(),
        Some(7),
        "LatestStartTime of 4"
    );
    assert_eq!(
        builder.activity(4).unwrap().latest_finish_time,
        Some(18),
        "LatestFinishTime of 4"
    );
    assert_eq!(
        builder.activity(5).unwrap().latest_start_time(),
        Some(10),
        "LatestStartTime of 5"
    );
    assert_eq!(
        builder.activity(5).unwrap().latest_finish_time,
        Some(18),
        "LatestFinishTime of 5"
    );
    assert_eq!(
        builder.activity(6).unwrap().latest_start_time(),
        Some(18),
        "LatestStartTime of 6"
    );
    assert_eq!(
        builder.activity(6).unwrap().latest_finish_time,
        Some(25),
        "LatestFinishTime of 6"
    );
    assert_eq!(
        builder.activity(7).unwrap().latest_start_time(),
        Some(25),
        "LatestStartTime of 7"
    );
    assert_eq!(
        builder.activity(7).unwrap().latest_finish_time,
        Some(29),
        "LatestFinishTime of 7"
    );
    assert_eq!(
        builder.activity(8).unwrap().latest_start_time(),
        Some(25),
        "LatestStartTime of 8"
    );
    assert_eq!(
        builder.activity(8).unwrap().latest_finish_time,
        Some(29),
        "LatestFinishTime of 8"
    );
    assert_eq!(
        builder.activity(9).unwrap().latest_start_time(),
        Some(19),
        "LatestStartTime of 9"
    );
    assert_eq!(
        builder.activity(9).unwrap().latest_finish_time,
        Some(29),
        "LatestFinishTime of 9"
    );
    assert!(builder.remove_activity_dependencies(6, IndexSet::from([5])));
    assert_eq!(builder.nodes().count(), 9);
    assert_eq!(builder.edges().count(), 10);
    builder.calculate_critical_path().unwrap();
    assert_eq!(
        builder.activity(1).unwrap().latest_start_time(),
        Some(5),
        "LatestStartTime of 1"
    );
    assert_eq!(
        builder.activity(1).unwrap().latest_finish_time,
        Some(11),
        "LatestFinishTime of 1"
    );
    assert_eq!(
        builder.activity(2).unwrap().latest_start_time(),
        Some(0),
        "LatestStartTime of 2"
    );
    assert_eq!(
        builder.activity(2).unwrap().latest_finish_time,
        Some(7),
        "LatestFinishTime of 2"
    );
    assert_eq!(
        builder.activity(3).unwrap().latest_start_time(),
        Some(3),
        "LatestStartTime of 3"
    );
    assert_eq!(
        builder.activity(3).unwrap().latest_finish_time,
        Some(11),
        "LatestFinishTime of 3"
    );
    assert_eq!(
        builder.activity(4).unwrap().latest_start_time(),
        Some(7),
        "LatestStartTime of 4"
    );
    assert_eq!(
        builder.activity(4).unwrap().latest_finish_time,
        Some(18),
        "LatestFinishTime of 4"
    );
    assert_eq!(
        builder.activity(5).unwrap().latest_start_time(),
        Some(11),
        "LatestStartTime of 5"
    );
    assert_eq!(
        builder.activity(5).unwrap().latest_finish_time,
        Some(19),
        "LatestFinishTime of 5"
    );
    assert_eq!(
        builder.activity(6).unwrap().latest_start_time(),
        Some(18),
        "LatestStartTime of 6"
    );
    assert_eq!(
        builder.activity(6).unwrap().latest_finish_time,
        Some(25),
        "LatestFinishTime of 6"
    );
    assert_eq!(
        builder.activity(7).unwrap().latest_start_time(),
        Some(25),
        "LatestStartTime of 7"
    );
    assert_eq!(
        builder.activity(7).unwrap().latest_finish_time,
        Some(29),
        "LatestFinishTime of 7"
    );
    assert_eq!(
        builder.activity(8).unwrap().latest_start_time(),
        Some(25),
        "LatestStartTime of 8"
    );
    assert_eq!(
        builder.activity(8).unwrap().latest_finish_time,
        Some(29),
        "LatestFinishTime of 8"
    );
    assert_eq!(
        builder.activity(9).unwrap().latest_start_time(),
        Some(19),
        "LatestStartTime of 9"
    );
    assert_eq!(
        builder.activity(9).unwrap().latest_finish_time,
        Some(29),
        "LatestFinishTime of 9"
    );
    assert!(builder.remove_activity_dependencies(6, IndexSet::from([4])));
    assert_eq!(builder.nodes().count(), 9);
    assert_eq!(builder.edges().count(), 9);
    builder.calculate_critical_path().unwrap();
    assert_eq!(
        builder.activity(1).unwrap().earliest_start_time,
        Some(0),
        "EarliestStartTime of 1"
    );
    assert_eq!(
        builder.activity(1).unwrap().earliest_finish_time(),
        Some(6),
        "EarliestFinishTime of 1"
    );
    assert_eq!(
        builder.activity(2).unwrap().earliest_start_time,
        Some(0),
        "EarliestStartTime of 2"
    );
    assert_eq!(
        builder.activity(2).unwrap().earliest_finish_time(),
        Some(7),
        "EarliestFinishTime of 2"
    );
    assert_eq!(
        builder.activity(3).unwrap().earliest_start_time,
        Some(0),
        "EarliestStartTime of 3"
    );
    assert_eq!(
        builder.activity(3).unwrap().earliest_finish_time(),
        Some(8),
        "EarliestFinishTime of 3"
    );
    assert_eq!(
        builder.activity(4).unwrap().earliest_start_time,
        Some(7),
        "EarliestStartTime of 4"
    );
    assert_eq!(
        builder.activity(4).unwrap().earliest_finish_time(),
        Some(18),
        "EarliestFinishTime of 4"
    );
    assert_eq!(
        builder.activity(5).unwrap().earliest_start_time,
        Some(8),
        "EarliestStartTime of 5"
    );
    assert_eq!(
        builder.activity(5).unwrap().earliest_finish_time(),
        Some(16),
        "EarliestFinishTime of 5"
    );
    assert_eq!(
        builder.activity(6).unwrap().earliest_start_time,
        Some(8),
        "EarliestStartTime of 6"
    );
    assert_eq!(
        builder.activity(6).unwrap().earliest_finish_time(),
        Some(15),
        "EarliestFinishTime of 6"
    );
    assert_eq!(
        builder.activity(7).unwrap().earliest_start_time,
        Some(18),
        "EarliestStartTime of 7"
    );
    assert_eq!(
        builder.activity(7).unwrap().earliest_finish_time(),
        Some(22),
        "EarliestFinishTime of 7"
    );
    assert_eq!(
        builder.activity(8).unwrap().earliest_start_time,
        Some(18),
        "EarliestStartTime of 8"
    );
    assert_eq!(
        builder.activity(8).unwrap().earliest_finish_time(),
        Some(22),
        "EarliestFinishTime of 8"
    );
    assert_eq!(
        builder.activity(9).unwrap().earliest_start_time,
        Some(16),
        "EarliestStartTime of 9"
    );
    assert_eq!(
        builder.activity(9).unwrap().earliest_finish_time(),
        Some(26),
        "EarliestFinishTime of 9"
    );
}

#[test]
fn calculate_critical_path_priority_list_then_correct_order() {
    let mut builder = new_builder();
    builder.add_activity(Act::new(1, 6));
    builder.add_activity(Act::new(2, 7));
    builder.add_activity(Act::new(3, 8));
    builder.add_activity_with_dependencies(Act::new(4, 11), IndexSet::from([2]));
    builder.add_activity_with_dependencies(Act::new(5, 8), IndexSet::from([1, 2, 3]));
    builder.add_activity_with_dependencies(Act::new(6, 7), IndexSet::from([3]));
    builder.add_activity_with_dependencies(Act::new(7, 4), IndexSet::from([4]));
    builder.add_activity_with_dependencies(Act::new(8, 4), IndexSet::from([4, 6]));
    builder.add_activity_with_dependencies(Act::new(9, 10), IndexSet::from([5]));

    let priority_list = builder.calculate_critical_path_priority_list().unwrap();

    assert_eq!(priority_list, vec![3, 2, 1, 5, 4, 6, 9, 7, 8]);
}

#[test]
fn calculate_resource_schedules_by_priority_list_when_one_resource_then_correct_order() {
    let mut builder = new_builder();
    builder.add_activity(Act::new(1, 6));
    builder.add_activity(Act::new(2, 7));
    builder.add_activity(Act::new(3, 8));
    builder.add_activity_with_dependencies(Act::new(4, 11), IndexSet::from([2]));
    builder.add_activity_with_dependencies(Act::new(5, 8), IndexSet::from([1, 2, 3]));
    builder.add_activity_with_dependencies(Act::new(6, 7), IndexSet::from([3]));
    builder.add_activity_with_dependencies(Act::new(7, 4), IndexSet::from([4]));
    builder.add_activity_with_dependencies(Act::new(8, 4), IndexSet::from([4, 6]));
    builder.add_activity_with_dependencies(Act::new(9, 10), IndexSet::from([5]));

    let resources: Vec<Resource<i32, i32>> = vec![Resource::new(
        1,
        Some(String::new()),
        false,
        false,
        InterActivityAllocationType::None,
        1.0,
        1.0,
        0,
        [],
    )];
    let schedules = builder
        .calculate_resource_schedules_by_priority_list(&resources)
        .unwrap();

    assert_eq!(schedules.len(), 1);
    assert_eq!(schedules[0].resource.as_ref().unwrap().id, 1);
    assert_eq!(schedules[0].scheduled_activities.len(), 9);
    assert_eq!(schedules[0].scheduled_activities[0].id, 3);
    assert_eq!(schedules[0].scheduled_activities[0].start_time, 0);
    assert_eq!(schedules[0].scheduled_activities[0].finish_time, 8);
    assert_eq!(schedules[0].scheduled_activities[1].id, 2);
    assert_eq!(schedules[0].scheduled_activities[1].start_time, 8);
    assert_eq!(schedules[0].scheduled_activities[1].finish_time, 15);
    assert_eq!(schedules[0].scheduled_activities[2].id, 1);
    assert_eq!(schedules[0].scheduled_activities[2].start_time, 15);
    assert_eq!(schedules[0].scheduled_activities[2].finish_time, 21);
    assert_eq!(schedules[0].scheduled_activities[3].id, 5);
    assert_eq!(schedules[0].scheduled_activities[3].start_time, 21);
    assert_eq!(schedules[0].scheduled_activities[3].finish_time, 29);
    assert_eq!(schedules[0].scheduled_activities[4].id, 4);
    assert_eq!(schedules[0].scheduled_activities[4].start_time, 29);
    assert_eq!(schedules[0].scheduled_activities[4].finish_time, 40);
    assert_eq!(schedules[0].scheduled_activities[5].id, 6);
    assert_eq!(schedules[0].scheduled_activities[5].start_time, 40);
    assert_eq!(schedules[0].scheduled_activities[5].finish_time, 47);
    assert_eq!(schedules[0].scheduled_activities[6].id, 9);
    assert_eq!(schedules[0].scheduled_activities[6].start_time, 47);
    assert_eq!(schedules[0].scheduled_activities[6].finish_time, 57);
    assert_eq!(schedules[0].scheduled_activities[7].id, 7);
    assert_eq!(schedules[0].scheduled_activities[7].start_time, 57);
    assert_eq!(schedules[0].scheduled_activities[7].finish_time, 61);
    assert_eq!(schedules[0].scheduled_activities[8].id, 8);
    assert_eq!(schedules[0].scheduled_activities[8].start_time, 61);
    assert_eq!(schedules[0].scheduled_activities[8].finish_time, 65);
    assert_eq!(
        schedules[0]
            .scheduled_activities
            .last()
            .unwrap()
            .finish_time,
        65
    );
}

#[test]
fn calculate_resource_schedules_by_priority_when_list_two_resources_then_correct_order() {
    let mut builder = new_builder();
    builder.add_activity(Act::new(1, 6));
    builder.add_activity(Act::new(2, 7));
    builder.add_activity(Act::new(3, 8));
    builder.add_activity_with_dependencies(Act::new(4, 11), IndexSet::from([2]));
    builder.add_activity_with_dependencies(Act::new(5, 8), IndexSet::from([1, 2, 3]));
    builder.add_activity_with_dependencies(Act::new(6, 7), IndexSet::from([3]));
    builder.add_activity_with_dependencies(Act::new(7, 4), IndexSet::from([4]));
    builder.add_activity_with_dependencies(Act::new(8, 4), IndexSet::from([4, 6]));
    builder.add_activity_with_dependencies(Act::new(9, 10), IndexSet::from([5]));

    let resources: Vec<Resource<i32, i32>> = vec![
        Resource::new(
            1,
            Some(String::new()),
            false,
            false,
            InterActivityAllocationType::None,
            1.0,
            1.0,
            0,
            [],
        ),
        Resource::new(
            2,
            Some(String::new()),
            false,
            false,
            InterActivityAllocationType::None,
            1.0,
            1.0,
            0,
            [],
        ),
    ];
    let schedules = builder
        .calculate_resource_schedules_by_priority_list(&resources)
        .unwrap();

    assert_eq!(schedules.len(), 2);
    assert_eq!(schedules[0].resource.as_ref().unwrap().id, 1);
    assert_eq!(schedules[0].scheduled_activities.len(), 5);
    assert_eq!(schedules[0].scheduled_activities[0].id, 3);
    assert_eq!(schedules[0].scheduled_activities[0].start_time, 0);
    assert_eq!(schedules[0].scheduled_activities[0].finish_time, 8);
    assert_eq!(schedules[0].scheduled_activities[1].id, 4);
    assert_eq!(schedules[0].scheduled_activities[1].start_time, 8);
    assert_eq!(schedules[0].scheduled_activities[1].finish_time, 19);
    assert_eq!(schedules[0].scheduled_activities[2].id, 6);
    assert_eq!(schedules[0].scheduled_activities[2].start_time, 19);
    assert_eq!(schedules[0].scheduled_activities[2].finish_time, 26);
    assert_eq!(schedules[0].scheduled_activities[3].id, 7);
    assert_eq!(schedules[0].scheduled_activities[3].start_time, 26);
    assert_eq!(schedules[0].scheduled_activities[3].finish_time, 30);
    assert_eq!(schedules[0].scheduled_activities[4].id, 8);
    assert_eq!(schedules[0].scheduled_activities[4].start_time, 30);
    assert_eq!(schedules[0].scheduled_activities[4].finish_time, 34);
    assert_eq!(
        schedules[0]
            .scheduled_activities
            .last()
            .unwrap()
            .finish_time,
        34
    );
    assert_eq!(schedules[1].resource.as_ref().unwrap().id, 2);
    assert_eq!(schedules[1].scheduled_activities.len(), 4);
    assert_eq!(schedules[1].scheduled_activities[0].id, 2);
    assert_eq!(schedules[1].scheduled_activities[0].start_time, 0);
    assert_eq!(schedules[1].scheduled_activities[0].finish_time, 7);
    assert_eq!(schedules[1].scheduled_activities[1].id, 1);
    assert_eq!(schedules[1].scheduled_activities[1].start_time, 7);
    assert_eq!(schedules[1].scheduled_activities[1].finish_time, 13);
    assert_eq!(schedules[1].scheduled_activities[2].id, 5);
    assert_eq!(schedules[1].scheduled_activities[2].start_time, 13);
    assert_eq!(schedules[1].scheduled_activities[2].finish_time, 21);
    assert_eq!(schedules[1].scheduled_activities[3].id, 9);
    assert_eq!(schedules[1].scheduled_activities[3].start_time, 21);
    assert_eq!(schedules[1].scheduled_activities[3].finish_time, 31);
    assert_eq!(
        schedules[1]
            .scheduled_activities
            .last()
            .unwrap()
            .finish_time,
        31
    );
}

#[test]
fn calculate_resource_schedules_by_priority_list_when_three_resources_then_correct_order() {
    let mut builder = new_builder();
    builder.add_activity(Act::new(1, 6));
    builder.add_activity(Act::new(2, 7));
    builder.add_activity(Act::new(3, 8));
    builder.add_activity_with_dependencies(Act::new(4, 11), IndexSet::from([2]));
    builder.add_activity_with_dependencies(Act::new(5, 8), IndexSet::from([1, 2, 3]));
    builder.add_activity_with_dependencies(Act::new(6, 7), IndexSet::from([3]));
    builder.add_activity_with_dependencies(Act::new(7, 4), IndexSet::from([4]));
    builder.add_activity_with_dependencies(Act::new(8, 4), IndexSet::from([4, 6]));
    builder.add_activity_with_dependencies(Act::new(9, 10), IndexSet::from([5]));

    let resources: Vec<Resource<i32, i32>> = vec![
        Resource::new(
            1,
            Some(String::new()),
            false,
            false,
            InterActivityAllocationType::None,
            1.0,
            1.0,
            0,
            [],
        ),
        Resource::new(
            2,
            Some(String::new()),
            false,
            false,
            InterActivityAllocationType::None,
            1.0,
            1.0,
            0,
            [],
        ),
        Resource::new(
            3,
            Some(String::new()),
            false,
            false,
            InterActivityAllocationType::None,
            1.0,
            1.0,
            0,
            [],
        ),
    ];
    let schedules = builder
        .calculate_resource_schedules_by_priority_list(&resources)
        .unwrap();

    assert_eq!(schedules.len(), 3);
    assert_eq!(schedules[0].resource.as_ref().unwrap().id, 1);
    assert_eq!(schedules[0].scheduled_activities.len(), 3);
    assert_eq!(schedules[0].scheduled_activities[0].id, 3);
    assert_eq!(schedules[0].scheduled_activities[0].start_time, 0);
    assert_eq!(schedules[0].scheduled_activities[0].finish_time, 8);
    assert_eq!(schedules[0].scheduled_activities[1].id, 5);
    assert_eq!(schedules[0].scheduled_activities[1].start_time, 8);
    assert_eq!(schedules[0].scheduled_activities[1].finish_time, 16);
    assert_eq!(schedules[0].scheduled_activities[2].id, 9);
    assert_eq!(schedules[0].scheduled_activities[2].start_time, 16);
    assert_eq!(schedules[0].scheduled_activities[2].finish_time, 26);
    assert_eq!(
        schedules[0]
            .scheduled_activities
            .last()
            .unwrap()
            .finish_time,
        26
    );
    assert_eq!(schedules[1].resource.as_ref().unwrap().id, 2);
    assert_eq!(schedules[1].scheduled_activities.len(), 3);
    assert_eq!(schedules[1].scheduled_activities[0].id, 2);
    assert_eq!(schedules[1].scheduled_activities[0].start_time, 0);
    assert_eq!(schedules[1].scheduled_activities[0].finish_time, 7);
    assert_eq!(schedules[1].scheduled_activities[1].id, 4);
    assert_eq!(schedules[1].scheduled_activities[1].start_time, 7);
    assert_eq!(schedules[1].scheduled_activities[1].finish_time, 18);
    assert_eq!(schedules[1].scheduled_activities[2].id, 7);
    assert_eq!(schedules[1].scheduled_activities[2].start_time, 18);
    assert_eq!(schedules[1].scheduled_activities[2].finish_time, 22);
    assert_eq!(
        schedules[1]
            .scheduled_activities
            .last()
            .unwrap()
            .finish_time,
        22
    );
    assert_eq!(schedules[2].resource.as_ref().unwrap().id, 3);
    assert_eq!(schedules[2].scheduled_activities.len(), 3);
    assert_eq!(schedules[2].scheduled_activities[0].id, 1);
    assert_eq!(schedules[2].scheduled_activities[0].start_time, 0);
    assert_eq!(schedules[2].scheduled_activities[0].finish_time, 6);
    assert_eq!(schedules[2].scheduled_activities[1].id, 6);
    assert_eq!(schedules[2].scheduled_activities[1].start_time, 8);
    assert_eq!(schedules[2].scheduled_activities[1].finish_time, 15);
    assert_eq!(schedules[2].scheduled_activities[2].id, 8);
    assert_eq!(schedules[2].scheduled_activities[2].start_time, 18);
    assert_eq!(schedules[2].scheduled_activities[2].finish_time, 22);
    assert_eq!(
        schedules[2]
            .scheduled_activities
            .last()
            .unwrap()
            .finish_time,
        22
    );
}

#[test]
fn calculate_resource_schedules_by_priority_list_when_four_resources_then_correct_order() {
    let mut builder = new_builder();
    builder.add_activity(Act::new(1, 6));
    builder.add_activity(Act::new(2, 7));
    builder.add_activity(Act::new(3, 8));
    builder.add_activity_with_dependencies(Act::new(4, 11), IndexSet::from([2]));
    builder.add_activity_with_dependencies(Act::new(5, 8), IndexSet::from([1, 2, 3]));
    builder.add_activity_with_dependencies(Act::new(6, 7), IndexSet::from([3]));
    builder.add_activity_with_dependencies(Act::new(7, 4), IndexSet::from([4]));
    builder.add_activity_with_dependencies(Act::new(8, 4), IndexSet::from([4, 6]));
    builder.add_activity_with_dependencies(Act::new(9, 10), IndexSet::from([5]));

    let resources: Vec<Resource<i32, i32>> = vec![
        Resource::new(
            1,
            Some(String::new()),
            false,
            false,
            InterActivityAllocationType::None,
            1.0,
            1.0,
            0,
            [],
        ),
        Resource::new(
            2,
            Some(String::new()),
            false,
            false,
            InterActivityAllocationType::None,
            1.0,
            1.0,
            0,
            [],
        ),
        Resource::new(
            3,
            Some(String::new()),
            false,
            false,
            InterActivityAllocationType::None,
            1.0,
            1.0,
            0,
            [],
        ),
        Resource::new(
            4,
            Some(String::new()),
            false,
            false,
            InterActivityAllocationType::None,
            1.0,
            1.0,
            0,
            [],
        ),
    ];
    let schedules = builder
        .calculate_resource_schedules_by_priority_list(&resources)
        .unwrap();

    assert_eq!(schedules.len(), 3);
    assert_eq!(schedules[0].resource.as_ref().unwrap().id, 1);
    assert_eq!(schedules[0].scheduled_activities.len(), 3);
    assert_eq!(schedules[0].scheduled_activities[0].id, 3);
    assert_eq!(schedules[0].scheduled_activities[0].start_time, 0);
    assert_eq!(schedules[0].scheduled_activities[0].finish_time, 8);
    assert_eq!(schedules[0].scheduled_activities[1].id, 5);
    assert_eq!(schedules[0].scheduled_activities[1].start_time, 8);
    assert_eq!(schedules[0].scheduled_activities[1].finish_time, 16);
    assert_eq!(schedules[0].scheduled_activities[2].id, 9);
    assert_eq!(schedules[0].scheduled_activities[2].start_time, 16);
    assert_eq!(schedules[0].scheduled_activities[2].finish_time, 26);
    assert_eq!(
        schedules[0]
            .scheduled_activities
            .last()
            .unwrap()
            .finish_time,
        26
    );
    assert_eq!(schedules[1].resource.as_ref().unwrap().id, 2);
    assert_eq!(schedules[1].scheduled_activities.len(), 3);
    assert_eq!(schedules[1].scheduled_activities[0].id, 2);
    assert_eq!(schedules[1].scheduled_activities[0].start_time, 0);
    assert_eq!(schedules[1].scheduled_activities[0].finish_time, 7);
    assert_eq!(schedules[1].scheduled_activities[1].id, 4);
    assert_eq!(schedules[1].scheduled_activities[1].start_time, 7);
    assert_eq!(schedules[1].scheduled_activities[1].finish_time, 18);
    assert_eq!(schedules[1].scheduled_activities[2].id, 7);
    assert_eq!(schedules[1].scheduled_activities[2].start_time, 18);
    assert_eq!(schedules[1].scheduled_activities[2].finish_time, 22);
    assert_eq!(
        schedules[1]
            .scheduled_activities
            .last()
            .unwrap()
            .finish_time,
        22
    );
    assert_eq!(schedules[2].resource.as_ref().unwrap().id, 3);
    assert_eq!(schedules[2].scheduled_activities.len(), 3);
    assert_eq!(schedules[2].scheduled_activities[0].id, 1);
    assert_eq!(schedules[2].scheduled_activities[0].start_time, 0);
    assert_eq!(schedules[2].scheduled_activities[0].finish_time, 6);
    assert_eq!(schedules[2].scheduled_activities[1].id, 6);
    assert_eq!(schedules[2].scheduled_activities[1].start_time, 8);
    assert_eq!(schedules[2].scheduled_activities[1].finish_time, 15);
    assert_eq!(schedules[2].scheduled_activities[2].id, 8);
    assert_eq!(schedules[2].scheduled_activities[2].start_time, 18);
    assert_eq!(schedules[2].scheduled_activities[2].finish_time, 22);
    assert_eq!(
        schedules[2]
            .scheduled_activities
            .last()
            .unwrap()
            .finish_time,
        22
    );
}

#[test]
fn calculate_resource_schedules_by_priority_list_when_four_ordered_resources_then_correct_order() {
    let mut builder = new_builder();
    builder.add_activity(Act::new(1, 6));
    builder.add_activity(Act::new(2, 7));
    builder.add_activity(Act::new(3, 8));
    builder.add_activity_with_dependencies(Act::new(4, 11), IndexSet::from([2]));
    builder.add_activity_with_dependencies(Act::new(5, 8), IndexSet::from([1, 2, 3]));
    builder.add_activity_with_dependencies(Act::new(6, 7), IndexSet::from([3]));
    builder.add_activity_with_dependencies(Act::new(7, 4), IndexSet::from([4]));
    builder.add_activity_with_dependencies(Act::new(8, 4), IndexSet::from([4, 6]));
    builder.add_activity_with_dependencies(Act::new(9, 10), IndexSet::from([5]));

    let resources: Vec<Resource<i32, i32>> = vec![
        Resource::new(
            1,
            Some(String::new()),
            false,
            false,
            InterActivityAllocationType::None,
            1.0,
            1.0,
            4,
            [],
        ),
        Resource::new(
            2,
            Some(String::new()),
            false,
            false,
            InterActivityAllocationType::None,
            1.0,
            1.0,
            3,
            [],
        ),
        Resource::new(
            3,
            Some(String::new()),
            false,
            false,
            InterActivityAllocationType::None,
            1.0,
            1.0,
            2,
            [],
        ),
        Resource::new(
            4,
            Some(String::new()),
            false,
            false,
            InterActivityAllocationType::None,
            1.0,
            1.0,
            1,
            [],
        ),
    ];
    let schedules = builder
        .calculate_resource_schedules_by_priority_list(&resources)
        .unwrap();

    assert_eq!(schedules.len(), 3);
    assert_eq!(schedules[0].resource.as_ref().unwrap().id, 4);
    assert_eq!(schedules[0].scheduled_activities.len(), 3);
    assert_eq!(schedules[0].scheduled_activities[0].id, 3);
    assert_eq!(schedules[0].scheduled_activities[0].start_time, 0);
    assert_eq!(schedules[0].scheduled_activities[0].finish_time, 8);
    assert_eq!(schedules[0].scheduled_activities[1].id, 5);
    assert_eq!(schedules[0].scheduled_activities[1].start_time, 8);
    assert_eq!(schedules[0].scheduled_activities[1].finish_time, 16);
    assert_eq!(schedules[0].scheduled_activities[2].id, 9);
    assert_eq!(schedules[0].scheduled_activities[2].start_time, 16);
    assert_eq!(schedules[0].scheduled_activities[2].finish_time, 26);
    assert_eq!(
        schedules[0]
            .scheduled_activities
            .last()
            .unwrap()
            .finish_time,
        26
    );
    assert_eq!(schedules[1].resource.as_ref().unwrap().id, 3);
    assert_eq!(schedules[1].scheduled_activities.len(), 3);
    assert_eq!(schedules[1].scheduled_activities[0].id, 2);
    assert_eq!(schedules[1].scheduled_activities[0].start_time, 0);
    assert_eq!(schedules[1].scheduled_activities[0].finish_time, 7);
    assert_eq!(schedules[1].scheduled_activities[1].id, 4);
    assert_eq!(schedules[1].scheduled_activities[1].start_time, 7);
    assert_eq!(schedules[1].scheduled_activities[1].finish_time, 18);
    assert_eq!(schedules[1].scheduled_activities[2].id, 7);
    assert_eq!(schedules[1].scheduled_activities[2].start_time, 18);
    assert_eq!(schedules[1].scheduled_activities[2].finish_time, 22);
    assert_eq!(
        schedules[1]
            .scheduled_activities
            .last()
            .unwrap()
            .finish_time,
        22
    );
    assert_eq!(schedules[2].resource.as_ref().unwrap().id, 2);
    assert_eq!(schedules[2].scheduled_activities.len(), 3);
    assert_eq!(schedules[2].scheduled_activities[0].id, 1);
    assert_eq!(schedules[2].scheduled_activities[0].start_time, 0);
    assert_eq!(schedules[2].scheduled_activities[0].finish_time, 6);
    assert_eq!(schedules[2].scheduled_activities[1].id, 6);
    assert_eq!(schedules[2].scheduled_activities[1].start_time, 8);
    assert_eq!(schedules[2].scheduled_activities[1].finish_time, 15);
    assert_eq!(schedules[2].scheduled_activities[2].id, 8);
    assert_eq!(schedules[2].scheduled_activities[2].start_time, 18);
    assert_eq!(schedules[2].scheduled_activities[2].finish_time, 22);
    assert_eq!(
        schedules[2]
            .scheduled_activities
            .last()
            .unwrap()
            .finish_time,
        22
    );
}

#[test]
fn calculate_resource_schedules_by_priority_list_when_unlimited_resources_then_correct_order() {
    let mut builder = new_builder();
    builder.add_activity(Act::new(1, 6));
    builder.add_activity(Act::new(2, 7));
    builder.add_activity(Act::new(3, 8));
    builder.add_activity_with_dependencies(Act::new(4, 11), IndexSet::from([2]));
    builder.add_activity_with_dependencies(Act::new(5, 8), IndexSet::from([1, 2, 3]));
    builder.add_activity_with_dependencies(Act::new(6, 7), IndexSet::from([3]));
    builder.add_activity_with_dependencies(Act::new(7, 4), IndexSet::from([4]));
    builder.add_activity_with_dependencies(Act::new(8, 4), IndexSet::from([4, 6]));
    builder.add_activity_with_dependencies(Act::new(9, 10), IndexSet::from([5]));

    let resources: Vec<Resource<i32, i32>> = vec![];
    let schedules = builder
        .calculate_resource_schedules_by_priority_list(&resources)
        .unwrap();

    assert_eq!(schedules.len(), 3);
    assert!(schedules[0].resource.is_none());
    assert_eq!(schedules[0].scheduled_activities.len(), 3);
    assert_eq!(schedules[0].scheduled_activities[0].id, 3);
    assert_eq!(schedules[0].scheduled_activities[0].start_time, 0);
    assert_eq!(schedules[0].scheduled_activities[0].finish_time, 8);
    assert_eq!(schedules[0].scheduled_activities[1].id, 5);
    assert_eq!(schedules[0].scheduled_activities[1].start_time, 8);
    assert_eq!(schedules[0].scheduled_activities[1].finish_time, 16);
    assert_eq!(schedules[0].scheduled_activities[2].id, 9);
    assert_eq!(schedules[0].scheduled_activities[2].start_time, 16);
    assert_eq!(schedules[0].scheduled_activities[2].finish_time, 26);
    assert_eq!(
        schedules[0]
            .scheduled_activities
            .last()
            .unwrap()
            .finish_time,
        26
    );
    assert!(schedules[1].resource.is_none());
    assert_eq!(schedules[1].scheduled_activities.len(), 3);
    assert_eq!(schedules[1].scheduled_activities[0].id, 2);
    assert_eq!(schedules[1].scheduled_activities[0].start_time, 0);
    assert_eq!(schedules[1].scheduled_activities[0].finish_time, 7);
    assert_eq!(schedules[1].scheduled_activities[1].id, 4);
    assert_eq!(schedules[1].scheduled_activities[1].start_time, 7);
    assert_eq!(schedules[1].scheduled_activities[1].finish_time, 18);
    assert_eq!(schedules[1].scheduled_activities[2].id, 7);
    assert_eq!(schedules[1].scheduled_activities[2].start_time, 18);
    assert_eq!(schedules[1].scheduled_activities[2].finish_time, 22);
    assert_eq!(
        schedules[1]
            .scheduled_activities
            .last()
            .unwrap()
            .finish_time,
        22
    );
    assert!(schedules[2].resource.is_none());
    assert_eq!(schedules[2].scheduled_activities.len(), 3);
    assert_eq!(schedules[2].scheduled_activities[0].id, 1);
    assert_eq!(schedules[2].scheduled_activities[0].start_time, 0);
    assert_eq!(schedules[2].scheduled_activities[0].finish_time, 6);
    assert_eq!(schedules[2].scheduled_activities[1].id, 6);
    assert_eq!(schedules[2].scheduled_activities[1].start_time, 8);
    assert_eq!(schedules[2].scheduled_activities[1].finish_time, 15);
    assert_eq!(schedules[2].scheduled_activities[2].id, 8);
    assert_eq!(schedules[2].scheduled_activities[2].start_time, 18);
    assert_eq!(schedules[2].scheduled_activities[2].finish_time, 22);
    assert_eq!(
        schedules[2]
            .scheduled_activities
            .last()
            .unwrap()
            .finish_time,
        22
    );
}

#[test]
fn calculate_resource_schedules_by_priority_list_when_unlimited_resources_with_potential_overlap_then_correct_order(
) {
    let mut builder = new_builder();
    builder.add_activity(Act::new(1, 1));
    builder.add_activity_with_dependencies(
        {
            let mut a = Act::new(2, 2);
            a.minimum_earliest_start_time = Some(1);
            a.maximum_latest_finish_time = Some(4);
            a
        },
        IndexSet::from([1]),
    );
    builder.add_activity_with_dependencies(Act::new(3, 2), IndexSet::from([1]));
    builder.add_activity_with_dependencies(Act::new(4, 2), IndexSet::from([1]));
    builder.add_activity(Act::new(5, 2));

    let resources: Vec<Resource<i32, i32>> = vec![];
    let schedules = builder
        .calculate_resource_schedules_by_priority_list(&resources)
        .unwrap();

    assert_eq!(schedules.len(), 3);
    assert!(schedules[0].resource.is_none());
    assert_eq!(schedules[0].scheduled_activities.len(), 2);
    assert_eq!(schedules[0].scheduled_activities[0].id, 1);
    assert_eq!(schedules[0].scheduled_activities[0].start_time, 0);
    assert_eq!(schedules[0].scheduled_activities[0].finish_time, 1);
    assert_eq!(schedules[0].scheduled_activities[1].id, 3);
    assert_eq!(schedules[0].scheduled_activities[1].start_time, 1);
    assert_eq!(schedules[0].scheduled_activities[1].finish_time, 3);
    assert_eq!(
        schedules[0]
            .scheduled_activities
            .last()
            .unwrap()
            .finish_time,
        3
    );
    assert!(schedules[1].resource.is_none());
    assert_eq!(schedules[1].scheduled_activities.len(), 2);
    assert_eq!(schedules[1].scheduled_activities[0].id, 5);
    assert_eq!(schedules[1].scheduled_activities[0].start_time, 0);
    assert_eq!(schedules[1].scheduled_activities[0].finish_time, 2);
    assert_eq!(schedules[1].scheduled_activities[1].id, 2);
    assert_eq!(schedules[1].scheduled_activities[1].start_time, 2);
    assert_eq!(schedules[1].scheduled_activities[1].finish_time, 4);
    assert_eq!(
        schedules[1]
            .scheduled_activities
            .last()
            .unwrap()
            .finish_time,
        4
    );
    assert!(schedules[2].resource.is_none());
    assert_eq!(schedules[2].scheduled_activities.len(), 1);
    assert_eq!(schedules[2].scheduled_activities[0].id, 4);
    assert_eq!(schedules[2].scheduled_activities[0].start_time, 1);
    assert_eq!(schedules[2].scheduled_activities[0].finish_time, 3);
    assert_eq!(
        schedules[2]
            .scheduled_activities
            .last()
            .unwrap()
            .finish_time,
        3
    );
}
