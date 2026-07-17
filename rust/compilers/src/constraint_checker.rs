use crate::messages;
use zametek_maths_graphs_primitives::{Activity, InvalidConstraint, Key};

/// Shared constraint validation logic used by both graph builders — the
/// counterpart of the C# `ConstraintChecker`.
pub(crate) fn find_invalid_pre_compilation_constraints<'a, K, R, W>(
    activities: impl IntoIterator<Item = &'a Activity<K, R, W>>,
) -> Vec<InvalidConstraint<K>>
where
    K: Key + 'a,
    R: Key + 'a,
    W: Key + 'a,
{
    let mut output = Vec::new();

    for activity in activities {
        if activity.minimum_free_slack.is_some() && activity.maximum_latest_finish_time.is_some() {
            output.push(InvalidConstraint::new(
                activity.id(),
                messages::MSG_CANNOT_SET_MINIMUM_FREE_SLACK_AND_MAXIMUM_LATEST_FINISH_TIME,
            ));
            continue;
        }

        if let (Some(min_est), Some(max_lft)) = (
            activity.minimum_earliest_start_time,
            activity.maximum_latest_finish_time,
        ) {
            if min_est + activity.duration > max_lft {
                output.push(InvalidConstraint::new(
                    activity.id(),
                    messages::MSG_MINIMUM_EARLIEST_START_TIME_PLUS_DURATION,
                ));
            }
        }
    }

    output
}

pub(crate) fn find_invalid_post_compilation_constraints<'a, K, R, W>(
    activities: impl IntoIterator<Item = &'a Activity<K, R, W>>,
) -> Vec<InvalidConstraint<K>>
where
    K: Key + 'a,
    R: Key + 'a,
    W: Key + 'a,
{
    let mut output = Vec::new();

    for activity in activities {
        check_early_times(activity, &mut output);
        check_late_times(activity, &mut output);
        check_start_time_order(activity, &mut output);
        check_finish_time_order(activity, &mut output);
        check_earliest_start_constraint(activity, &mut output);
        check_latest_finish_constraint(activity, &mut output);
        check_free_slack_constraint(activity, &mut output);
    }

    output
}

fn check_early_times<K: Key, R: Key, W: Key>(
    activity: &Activity<K, R, W>,
    output: &mut Vec<InvalidConstraint<K>>,
) {
    let (Some(est), Some(eft)) = (
        activity.earliest_start_time,
        activity.earliest_finish_time(),
    ) else {
        return;
    };

    if est < 0 {
        output.push(InvalidConstraint::new(
            activity.id(),
            messages::MSG_EARLIEST_START_TIME_LESS_THAN_ZERO,
        ));
    }

    if eft < 0 {
        output.push(InvalidConstraint::new(
            activity.id(),
            messages::MSG_EARLIEST_FINISH_TIME_LESS_THAN_ZERO,
        ));
    }
}

fn check_late_times<K: Key, R: Key, W: Key>(
    activity: &Activity<K, R, W>,
    output: &mut Vec<InvalidConstraint<K>>,
) {
    let (Some(lst), Some(lft)) = (activity.latest_start_time(), activity.latest_finish_time) else {
        return;
    };

    if lst < 0 {
        output.push(InvalidConstraint::new(
            activity.id(),
            messages::MSG_LATEST_START_TIME_LESS_THAN_ZERO,
        ));
    }

    if lft < 0 {
        output.push(InvalidConstraint::new(
            activity.id(),
            messages::MSG_LATEST_FINISH_TIME_LESS_THAN_ZERO,
        ));
    }
}

fn check_start_time_order<K: Key, R: Key, W: Key>(
    activity: &Activity<K, R, W>,
    output: &mut Vec<InvalidConstraint<K>>,
) {
    let (Some(est), Some(lst)) = (activity.earliest_start_time, activity.latest_start_time())
    else {
        return;
    };

    if lst < est {
        output.push(InvalidConstraint::new(
            activity.id(),
            messages::MSG_LATEST_START_TIME_LESS_THAN_EARLIEST_START_TIME,
        ));
    }
}

fn check_finish_time_order<K: Key, R: Key, W: Key>(
    activity: &Activity<K, R, W>,
    output: &mut Vec<InvalidConstraint<K>>,
) {
    let (Some(eft), Some(lft)) = (activity.earliest_finish_time(), activity.latest_finish_time)
    else {
        return;
    };

    if lft < eft {
        output.push(InvalidConstraint::new(
            activity.id(),
            messages::MSG_LATEST_FINISH_TIME_LESS_THAN_EARLIEST_FINISH_TIME,
        ));
    }
}

fn check_earliest_start_constraint<K: Key, R: Key, W: Key>(
    activity: &Activity<K, R, W>,
    output: &mut Vec<InvalidConstraint<K>>,
) {
    let (Some(est), Some(min_est)) = (
        activity.earliest_start_time,
        activity.minimum_earliest_start_time,
    ) else {
        return;
    };

    if est < min_est {
        output.push(InvalidConstraint::new(
            activity.id(),
            messages::MSG_EARLIEST_START_TIME_LESS_THAN_MINIMUM_EARLIEST_START_TIME,
        ));
    }
}

fn check_latest_finish_constraint<K: Key, R: Key, W: Key>(
    activity: &Activity<K, R, W>,
    output: &mut Vec<InvalidConstraint<K>>,
) {
    let (Some(lft), Some(max_lft)) = (
        activity.latest_finish_time,
        activity.maximum_latest_finish_time,
    ) else {
        return;
    };

    if lft > max_lft {
        output.push(InvalidConstraint::new(
            activity.id(),
            messages::MSG_LATEST_FINISH_TIME_MORE_THAN_MAXIMUM_LATEST_FINISH_TIME,
        ));
    }
}

fn check_free_slack_constraint<K: Key, R: Key, W: Key>(
    activity: &Activity<K, R, W>,
    output: &mut Vec<InvalidConstraint<K>>,
) {
    let (Some(free_slack), Some(min_free_slack)) =
        (activity.free_slack, activity.minimum_free_slack)
    else {
        return;
    };

    if free_slack < min_free_slack {
        output.push(InvalidConstraint::new(
            activity.id(),
            messages::MSG_FREE_SLACK_LESS_THAN_MINIMUM_FREE_SLACK,
        ));
    }
}
