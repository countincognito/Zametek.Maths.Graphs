use crate::messages;
use zametek_maths_graphs_primitives::{graph_limits, Activity, Key, Resource, WorkStream};

/// Domain-limit validation - the counterpart of the C# `LimitChecker`.
///
/// Deliberately kept separate from [`crate::constraint_checker`]: those checks
/// describe constraints that contradict each other and also gate the
/// critical-path passes, whereas these describe values that are simply out of
/// range. Keeping them apart means an out-of-range value is reported (as
/// `P0080`) without changing how the critical-path calculation behaves.
///
/// Returns one line per violation, in the same order the C# original produces
/// them: the three counts first, then each activity's values in graph order.
pub(crate) fn find_limit_violations<K, R, W>(
    activities: &[&Activity<K, R, W>],
    filtered_resources: &[Resource<R, W>],
    work_streams: &[WorkStream<W>],
) -> Vec<String>
where
    K: Key,
    R: Key,
    W: Key,
{
    let mut output = Vec::new();

    add_count_violation(
        &mut output,
        messages::MSG_ACTIVITIES,
        activities.len(),
        graph_limits::MAXIMUM_ACTIVITY_COUNT,
    );
    add_count_violation(
        &mut output,
        messages::MSG_RESOURCES,
        filtered_resources.len(),
        graph_limits::MAXIMUM_RESOURCE_COUNT,
    );
    add_count_violation(
        &mut output,
        messages::MSG_WORK_STREAMS,
        work_streams.len(),
        graph_limits::MAXIMUM_WORK_STREAM_COUNT,
    );

    for activity in activities {
        // The value names are the C# property names rather than the Rust field
        // names, so that the rendered message matches the original exactly.
        add_range_violation(
            &mut output,
            activity.id(),
            "Duration",
            Some(activity.duration),
        );
        add_range_violation(
            &mut output,
            activity.id(),
            "MinimumEarliestStartTime",
            activity.minimum_earliest_start_time,
        );
        add_range_violation(
            &mut output,
            activity.id(),
            "MaximumLatestFinishTime",
            activity.maximum_latest_finish_time,
        );
        add_range_violation(
            &mut output,
            activity.id(),
            "MinimumFreeSlack",
            activity.minimum_free_slack,
        );
    }

    output
}

fn add_count_violation(
    output: &mut Vec<String>,
    item_description: &str,
    count: usize,
    maximum: usize,
) {
    if count > maximum {
        output.push(messages::format_message(
            messages::MSG_COUNT_EXCEEDS_MAXIMUM,
            &[&item_description, &count, &maximum],
        ));
    }
}

fn add_range_violation<K: Key>(
    output: &mut Vec<String>,
    activity_id: K,
    value_name: &str,
    value: Option<i32>,
) {
    let Some(actual) = value else {
        return;
    };
    if (graph_limits::MINIMUM_TIME_VALUE..=graph_limits::MAXIMUM_TIME_VALUE).contains(&actual) {
        return;
    }

    output.push(format!(
        "{} {} -> {}",
        messages::MSG_ACTIVITY,
        activity_id,
        messages::format_message(
            messages::MSG_VALUE_OUTSIDE_SUPPORTED_RANGE,
            &[
                &value_name,
                &actual,
                &graph_limits::MINIMUM_TIME_VALUE,
                &graph_limits::MAXIMUM_TIME_VALUE,
            ],
        )
    ));
}
