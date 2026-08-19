use super::schedule_builder::ResourceScheduleBuilder;
use crate::contracts::{IResourceSchedulingEngine, IResourceSchedulingGraph};
use crate::messages;
use indexmap::{IndexMap, IndexSet};
use std::fmt::Write as _;
use zametek_maths_graphs_primitives::{
    graph_limits, Activity, DependentActivity, GraphError, InterActivityAllocationType, Key,
    LogicalOperator, Resource, ResourceSchedule, UnavailableResources,
};

/// Default resource-scheduling engine: priority-list allocation plus the
/// surrounding scheduling pipeline. The counterpart of the C#
/// `PriorityListResourceScheduler`.
#[derive(Debug, Clone, Copy, Default)]
pub struct PriorityListResourceScheduler;

impl<K: Key, R: Key, W: Key> IResourceSchedulingEngine<K, R, W> for PriorityListResourceScheduler {
    fn calculate_resource_schedules(
        &self,
        priority_list: &[K],
        filtered_resources: &[Resource<R, W>],
        infinite_resources: bool,
        graph: &mut dyn IResourceSchedulingGraph<K, R, W>,
    ) -> Result<Vec<ResourceSchedule<K, R, W>>, GraphError> {
        calculate_resource_schedules(priority_list, filtered_resources, infinite_resources, graph)
    }

    fn gather_unavailable_resources(
        &self,
        activities: &[&Activity<K, R, W>],
        filtered_resources: &[Resource<R, W>],
    ) -> Vec<UnavailableResources<K, R>> {
        gather_unavailable_resources(activities.iter().copied(), filtered_resources)
    }

    fn replace_with_synthetic_resources(
        &self,
        resource_schedules: Vec<ResourceSchedule<K, R, W>>,
    ) -> Vec<ResourceSchedule<K, R, W>> {
        replace_with_synthetic_resources(resource_schedules)
    }

    fn rebuild_aligned_resource_schedules(
        &self,
        resource_schedules: &[ResourceSchedule<K, R, W>],
        infinite_resources: bool,
        graph: &dyn IResourceSchedulingGraph<K, R, W>,
        final_activities: &[Activity<K, R, W>],
        start_time: i32,
        finish_time: i32,
    ) -> Result<Vec<ResourceSchedule<K, R, W>>, GraphError> {
        rebuild_aligned_resource_schedules(
            resource_schedules,
            infinite_resources,
            graph,
            final_activities,
            start_time,
            finish_time,
        )
    }

    fn collect_indirect_resource_schedules(
        &self,
        filtered_resources: &[Resource<R, W>],
        scheduled_resources: &[ResourceSchedule<K, R, W>],
        final_activities: &[Activity<K, R, W>],
        start_time: i32,
        finish_time: i32,
    ) -> Result<Vec<ResourceSchedule<K, R, W>>, GraphError> {
        collect_indirect_resource_schedules(
            filtered_resources,
            scheduled_resources,
            final_activities,
            start_time,
            finish_time,
        )
    }

    fn get_resource_phases_used(
        &self,
        total_schedules: &[ResourceSchedule<K, R, W>],
        workstreams_used: &IndexSet<W>,
    ) -> IndexSet<W> {
        get_resource_phases_used(total_schedules, workstreams_used)
    }
}

/// Priority-list resource scheduling - the counterpart of the C#
/// `PriorityListResourceScheduler.CalculateResourceSchedules`.
pub(crate) fn calculate_resource_schedules<K, R, W>(
    priority_list: &[K],
    filtered_resources: &[Resource<R, W>],
    infinite_resources: bool,
    graph: &mut dyn IResourceSchedulingGraph<K, R, W>,
) -> Result<Vec<ResourceSchedule<K, R, W>>, GraphError>
where
    K: Key,
    R: Key,
    W: Key,
{
    let mut working_list: Vec<Option<K>> = priority_list.iter().copied().map(Some).collect();

    // Each activity's strong (resolved) dependency set is invariant for the
    // duration of scheduling, so resolve it once here rather than re-walking
    // the graph for every pending activity on every time tick.
    let mut strong_dependency_lookup: IndexMap<K, IndexSet<K>> = IndexMap::new();
    for activity_id in priority_list {
        if !strong_dependency_lookup.contains_key(activity_id) {
            strong_dependency_lookup.insert(
                *activity_id,
                graph
                    .strong_activity_dependency_ids(*activity_id)
                    .into_iter()
                    .collect(),
            );
        }
    }

    let mut resource_schedule_builders: Vec<ResourceScheduleBuilder<K, R, W>> = {
        let mut sorted: Vec<&Resource<R, W>> = filtered_resources.iter().collect();
        sorted.sort_by_key(|x| x.allocation_order);
        sorted
            .into_iter()
            .map(|x| ResourceScheduleBuilder::new(x.clone()))
            .collect()
    };

    let mut completed: IndexSet<K> = IndexSet::new();
    let mut started: IndexSet<K> = IndexSet::new();
    let mut ready: Vec<Option<K>> = vec![None; working_list.len()];
    let mut time_counter: i32 = 0;

    while working_list.iter().any(|x| x.is_some())
        || !started.is_empty()
        || ready.iter().any(|x| x.is_some())
    {
        advance_completed_activities(
            &resource_schedule_builders,
            time_counter,
            &mut started,
            &mut completed,
        );
        promote_ready_activities(
            &mut working_list,
            &mut ready,
            &completed,
            &started,
            &strong_dependency_lookup,
        );
        assign_ready_activities_to_resources(
            &mut ready,
            &mut resource_schedule_builders,
            &mut *graph,
            filtered_resources,
            infinite_resources,
            &mut started,
            time_counter,
        );
        time_counter = next_tick_of_interest(
            &working_list,
            &ready,
            &started,
            &completed,
            &resource_schedule_builders,
            &*graph,
            infinite_resources,
            &strong_dependency_lookup,
            time_counter,
        )?;
    }

    let final_activities: Vec<DependentActivity<K, R, W>> = graph.clone_activities();
    let final_plain: Vec<Activity<K, R, W>> = final_activities
        .iter()
        .map(|a| a.activity.clone())
        .collect();

    // `first_activity_start_time` rather than a minimum over each builder's whole
    // schedule: appends are in non-decreasing start-time order, so the first entry
    // already is the minimum. An empty builder still contributes zero, exactly as
    // the inner `unwrap_or(0)` did, which is what keeps the result identical.
    let start_time = resource_schedule_builders
        .iter()
        .map(|x| x.first_activity_start_time())
        .min()
        .unwrap_or(0);

    let finish_time = resource_schedule_builders
        .iter()
        .map(|x| x.last_activity_finish_time())
        .max()
        .unwrap_or(0);

    let mut output = Vec::new();
    for builder in &resource_schedule_builders {
        let schedule = builder.to_resource_schedule(&final_plain, start_time, finish_time)?;
        if !schedule.scheduled_activities.is_empty() {
            output.push(schedule);
        }
    }
    Ok(output)
}

fn advance_completed_activities<K: Key, R: Key, W: Key>(
    builders: &[ResourceScheduleBuilder<K, R, W>],
    time_counter: i32,
    started: &mut IndexSet<K>,
    completed: &mut IndexSet<K>,
) {
    // Any started activities that are currently not running must now be completed.
    let running: IndexSet<K> = builders
        .iter()
        .filter_map(|x| x.activity_at(time_counter))
        .collect();

    // Now work out which of the started jobs are now completed.
    let not_yet_completed: Vec<K> = started
        .iter()
        .filter(|x| running.contains(*x))
        .copied()
        .collect();
    for id in &not_yet_completed {
        started.shift_remove(id);
    }
    completed.extend(started.iter().copied());
    // Refresh the started set.
    started.clear();
    started.extend(not_yet_completed);
}

fn promote_ready_activities<K: Key>(
    working_list: &mut [Option<K>],
    ready: &mut [Option<K>],
    completed: &IndexSet<K>,
    started: &IndexSet<K>,
    strong_dependency_lookup: &IndexMap<K, IndexSet<K>>,
) {
    // Get the activities that have completed direct dependencies. Add these to
    // the ready queue since there is nothing preventing them from starting.
    for i in 0..working_list.len() {
        let Some(activity_id) = working_list[i] else {
            continue;
        };
        let direct_dependencies = &strong_dependency_lookup[&activity_id];
        if direct_dependencies.iter().all(|d| completed.contains(d))
            && !completed.contains(&activity_id)
            && !started.contains(&activity_id)
        {
            ready[i] = Some(activity_id);
            working_list[i] = None;
        }
    }
}

#[allow(clippy::too_many_arguments, clippy::needless_range_loop)]
fn assign_ready_activities_to_resources<K, R, W>(
    ready: &mut [Option<K>],
    builders: &mut Vec<ResourceScheduleBuilder<K, R, W>>,
    graph: &mut dyn IResourceSchedulingGraph<K, R, W>,
    filtered_resources: &[Resource<R, W>],
    infinite_resources: bool,
    started: &mut IndexSet<K>,
    time_counter: i32,
) where
    K: Key,
    R: Key,
    W: Key,
{
    // Cycle through each ready activity and find the first currently available
    // schedule builder.
    let mut keep_looking = true;
    while ready.iter().any(|x| x.is_some()) && keep_looking {
        keep_looking = false;
        let mut available_builder_exists = false;

        for i in 0..ready.len() {
            let Some(activity_id) = ready[i] else {
                continue;
            };
            graph
                .activity_mut(activity_id)
                .allocated_to_resources
                .clear();
            let activity = graph.activity(activity_id).activity.clone();

            // Check to see if the activity has to be targeted to specific
            // resources, and that this resource is one of those specific targets.
            let must_target_specific = !infinite_resources && !activity.target_resources.is_empty();

            let scheduled = if !must_target_specific {
                try_schedule_unrestricted(
                    builders,
                    &activity,
                    activity_id,
                    time_counter,
                    started,
                    &mut available_builder_exists,
                )
            } else {
                try_schedule_targeted(
                    builders,
                    &activity,
                    activity_id,
                    filtered_resources,
                    time_counter,
                    started,
                    &mut available_builder_exists,
                )
            };
            if scheduled {
                ready[i] = None;
                keep_looking = true;
            }
        }

        if infinite_resources && !available_builder_exists && !keep_looking {
            builders.push(ResourceScheduleBuilder::new_unmapped());
            keep_looking = true;
        }
    }
}

fn try_schedule_unrestricted<K: Key, R: Key, W: Key>(
    builders: &mut [ResourceScheduleBuilder<K, R, W>],
    activity: &Activity<K, R, W>,
    activity_id: K,
    time_counter: i32,
    started: &mut IndexSet<K>,
    available_builder_exists: &mut bool,
) -> bool {
    for builder in builders.iter_mut() {
        if builder.earliest_available_start_time_for_next_activity() > time_counter {
            continue;
        }
        *available_builder_exists = true;
        if builder.is_explicit_target() {
            continue;
        }
        if activity.earliest_start_time.unwrap_or(0) > time_counter {
            continue;
        }
        if let Some(max_lft) = activity.maximum_latest_finish_time {
            if i64::from(max_lft) > i64::from(time_counter) + i64::from(activity.duration) {
                continue;
            }
        }
        // Starting here would push the finish time past the supported time
        // horizon; the activity can never start, which the stall detector reports.
        if i64::from(time_counter) + i64::from(activity.duration)
            > i64::from(graph_limits::MAXIMUM_TIME_VALUE)
        {
            continue;
        }
        builder.append_activity(activity, time_counter);
        started.insert(activity_id);
        return true;
    }
    false
}

#[allow(clippy::too_many_arguments)]
fn try_schedule_targeted<K: Key, R: Key, W: Key>(
    builders: &mut [ResourceScheduleBuilder<K, R, W>],
    activity: &Activity<K, R, W>,
    activity_id: K,
    filtered_resources: &[Resource<R, W>],
    time_counter: i32,
    started: &mut IndexSet<K>,
    available_builder_exists: &mut bool,
) -> bool {
    let mut available: Vec<usize> = Vec::new();

    for builder_index in 0..builders.len() {
        let builder = &builders[builder_index];
        if builder.earliest_available_start_time_for_next_activity() > time_counter {
            continue;
        }
        *available_builder_exists = true;

        if let Some(resource_id) = builder.resource_id() {
            if !activity.target_resources.contains(&resource_id) {
                continue;
            }
        }
        if activity.earliest_start_time.unwrap_or(0) > time_counter {
            continue;
        }
        if let Some(max_lft) = activity.maximum_latest_finish_time {
            if i64::from(max_lft) > i64::from(time_counter) + i64::from(activity.duration) {
                continue;
            }
        }
        // Starting here would push the finish time past the supported time
        // horizon; the activity can never start, which the stall detector reports.
        if i64::from(time_counter) + i64::from(activity.duration)
            > i64::from(graph_limits::MAXIMUM_TIME_VALUE)
        {
            continue;
        }

        match activity.target_resource_operator {
            // Find just one resource that can accommodate the activity.
            LogicalOperator::Or => {
                builders[builder_index].append_activity(activity, time_counter);
                started.insert(activity_id);
                return true;
            }
            // Find all the resources that must accommodate the activity.
            LogicalOperator::And => {
                if !available.contains(&builder_index) {
                    available.push(builder_index);
                }
                let available_resource_ids: IndexSet<R> = available
                    .iter()
                    .map(|i| builders[*i].resource_id().unwrap_or_default())
                    .collect();
                let target_set: IndexSet<R> = activity.target_resources.iter().copied().collect();
                if sets_equal(&target_set, &available_resource_ids) {
                    for i in &available {
                        builders[*i].append_activity(activity, time_counter);
                        started.insert(activity_id);
                    }
                    return true;
                }
            }
            // Find all the active resources that must accommodate the activity.
            LogicalOperator::ActiveAnd => {
                if !available.contains(&builder_index) {
                    available.push(builder_index);
                }
                let available_resource_ids: IndexSet<R> = available
                    .iter()
                    .map(|i| builders[*i].resource_id().unwrap_or_default())
                    .collect();
                // Check intersection of TargetResources and filtered Resources.
                let intersection: IndexSet<R> = activity
                    .target_resources
                    .iter()
                    .filter(|r| filtered_resources.iter().any(|x| x.id == **r))
                    .copied()
                    .collect();
                if sets_equal(&intersection, &available_resource_ids) {
                    for i in &available {
                        builders[*i].append_activity(activity, time_counter);
                        started.insert(activity_id);
                    }
                    return true;
                }
            }
        }
    }
    false
}

fn sets_equal<T: Key>(a: &IndexSet<T>, b: &IndexSet<T>) -> bool {
    a.len() == b.len() && a.iter().all(|x| b.contains(x))
}

/// Decides which tick the scheduling loop should process next. Most of the time
/// that is simply the next tick, but when every remaining activity is waiting on
/// a time gate the loop can jump straight to the earliest gate opening - every
/// skipped tick is provably a no-op (no completion, promotion or assignment can
/// occur before the next running activity finishes or the next gate opens).
///
/// When there is nothing running, nothing pending completion and no future time
/// gate while activities still remain, no amount of further ticking can change
/// anything - so instead of looping forever the scheduler stops and reports
/// which activities are stuck and why.
#[allow(clippy::too_many_arguments)]
fn next_tick_of_interest<K: Key, R: Key, W: Key>(
    working_list: &[Option<K>],
    ready: &[Option<K>],
    started: &IndexSet<K>,
    completed: &IndexSet<K>,
    builders: &[ResourceScheduleBuilder<K, R, W>],
    graph: &dyn IResourceSchedulingGraph<K, R, W>,
    infinite_resources: bool,
    strong_dependency_lookup: &IndexMap<K, IndexSet<K>>,
    time_counter: i32,
) -> Result<i32, GraphError> {
    // Times are compared in i64 so that pathological input values cannot
    // overflow i32 arithmetic inside the loop.
    let mut next_event_time = i64::MAX;

    if !started.is_empty() {
        // Find the activities currently occupying resources, and when the
        // earliest of them will finish. A builder is busy when its last recorded
        // activity finishes after the current tick (activities are only ever
        // appended at the current tick, so nothing later exists).
        let mut running: IndexSet<K> = IndexSet::new();
        for builder in builders {
            let last_finish_time = i64::from(builder.last_activity_finish_time());
            if last_finish_time > i64::from(time_counter) {
                if let Some(running_activity_id) = builder.activity_at(time_counter) {
                    running.insert(running_activity_id);
                }
                if last_finish_time < next_event_time {
                    next_event_time = last_finish_time;
                }
            }
        }

        // A started activity that is no longer running (e.g. one with zero
        // duration) completes on the very next tick, which may unblock its
        // successors - so no jump is possible.
        for started_id in started {
            if !running.contains(started_id) {
                return Ok(time_counter + 1);
            }
        }
    }

    // Ready activities can also be unblocked purely by the passage of time: an
    // earliest start time that has not been reached yet, or a deadline whose
    // just-in-time start point is still ahead.
    for slot in ready {
        let Some(activity_id) = *slot else {
            continue;
        };
        let activity = graph.activity(activity_id);
        let earliest_start_time = i64::from(activity.earliest_start_time.unwrap_or(0));
        if earliest_start_time > i64::from(time_counter) && earliest_start_time < next_event_time {
            next_event_time = earliest_start_time;
        }
        if let Some(maximum_latest_finish_time) = activity.maximum_latest_finish_time {
            let maximum_latest_finish_time = i64::from(maximum_latest_finish_time);
            // The just-in-time gate holds the activity back while starting now
            // would still finish ahead of the deadline; it opens at the tick
            // where the finish would land exactly on the deadline.
            let gate_opens_at = maximum_latest_finish_time - i64::from(activity.duration);
            if maximum_latest_finish_time > i64::from(time_counter) + i64::from(activity.duration)
                && gate_opens_at < next_event_time
            {
                next_event_time = gate_opens_at;
            }
        }
    }

    if next_event_time != i64::MAX {
        // Jumping beyond the supported horizon means nothing schedulable remains
        // within it, so the state is dead however far the clock is wound forward.
        if next_event_time > i64::from(graph_limits::MAXIMUM_TIME_VALUE) {
            return Err(GraphError::resource_scheduling_stall(build_stall_message(
                working_list,
                ready,
                builders,
                graph,
                infinite_resources,
                strong_dependency_lookup,
                completed,
                time_counter,
            )));
        }
        // Guaranteed to be at least time_counter + 1, because every candidate
        // above is strictly greater than the current tick.
        return Ok(next_event_time as i32);
    }

    // No running activity, no pending completion, and no future time gate: if any
    // activities remain then they can never be scheduled.
    if ready.iter().any(|x| x.is_some())
        || working_list.iter().any(|x| x.is_some())
        || !started.is_empty()
    {
        return Err(GraphError::resource_scheduling_stall(build_stall_message(
            working_list,
            ready,
            builders,
            graph,
            infinite_resources,
            strong_dependency_lookup,
            completed,
            time_counter,
        )));
    }

    // Everything has drained; the loop condition is about to terminate the loop.
    Ok(time_counter + 1)
}

/// Builds the diagnostic message for a scheduling stall: one line per stuck
/// activity explaining, as precisely as possible, why it can never be scheduled.
#[allow(clippy::too_many_arguments)]
fn build_stall_message<K: Key, R: Key, W: Key>(
    working_list: &[Option<K>],
    ready: &[Option<K>],
    builders: &[ResourceScheduleBuilder<K, R, W>],
    graph: &dyn IResourceSchedulingGraph<K, R, W>,
    infinite_resources: bool,
    strong_dependency_lookup: &IndexMap<K, IndexSet<K>>,
    completed: &IndexSet<K>,
    time_counter: i32,
) -> String {
    let mut available_resource_ids: IndexSet<R> = IndexSet::new();
    let mut all_builders_explicit_target = !builders.is_empty();
    for builder in builders {
        if let Some(resource_id) = builder.resource_id() {
            available_resource_ids.insert(resource_id);
        }
        if !builder.is_explicit_target() {
            all_builders_explicit_target = false;
        }
    }

    let mut output = String::new();
    let _ = writeln!(output, "{}", messages::MSG_RESOURCE_SCHEDULING_STALLED);
    for slot in ready {
        let Some(activity_id) = *slot else {
            continue;
        };
        let activity = graph.activity(activity_id);
        let _ = writeln!(
            output,
            "{} -> {}",
            activity_id,
            describe_unschedulable_activity(
                activity,
                &available_resource_ids,
                all_builders_explicit_target,
                infinite_resources,
                time_counter,
            )
        );
    }
    for slot in working_list {
        let Some(activity_id) = *slot else {
            continue;
        };
        let mut outstanding: Vec<K> = strong_dependency_lookup[&activity_id]
            .iter()
            .filter(|x| !completed.contains(*x))
            .copied()
            .collect();
        outstanding.sort_unstable();
        let _ = writeln!(
            output,
            "{} -> {} {}",
            activity_id,
            messages::MSG_WAITING_ON_DEPENDENCIES_THAT_CAN_NEVER_COMPLETE,
            join_ids(&outstanding)
        );
    }
    output
}

/// Explains why a ready activity could not be assigned to any resource.
///
/// The C# original has a further branch here that probes whether the activity's
/// target resource set agrees with its own contents, because the incident this
/// guardrail was written for involved a structurally corrupted `HashSet` whose
/// lookups always returned false. That branch has no counterpart here: safe Rust
/// cannot produce such a collection, for the same reason the `P0070`
/// self-consistency check is not part of this port.
fn describe_unschedulable_activity<K: Key, R: Key, W: Key>(
    activity: &Activity<K, R, W>,
    available_resource_ids: &IndexSet<R>,
    all_builders_explicit_target: bool,
    infinite_resources: bool,
    time_counter: i32,
) -> String {
    let target_resources: Vec<R> = activity.target_resources.iter().copied().collect();
    let must_target_specific = !infinite_resources && !target_resources.is_empty();

    if must_target_specific {
        let mut missing: Vec<R> = target_resources
            .iter()
            .filter(|x| !available_resource_ids.contains(*x))
            .copied()
            .collect();
        missing.sort_unstable();
        if activity.target_resource_operator == LogicalOperator::And && !missing.is_empty() {
            return format!(
                "{} {}",
                messages::MSG_REQUIRES_ALL_TARGET_RESOURCES_BUT_SOME_NOT_AVAILABLE,
                join_ids(&missing)
            );
        }
        if matches!(
            activity.target_resource_operator,
            LogicalOperator::Or | LogicalOperator::ActiveAnd
        ) && missing.len() == target_resources.len()
        {
            let mut sorted = target_resources;
            sorted.sort_unstable();
            return format!(
                "{} {}",
                messages::MSG_NONE_OF_TARGET_RESOURCES_ARE_AVAILABLE,
                join_ids(&sorted)
            );
        }
    } else if all_builders_explicit_target {
        return messages::MSG_NO_TARGET_RESOURCES_BUT_ALL_RESOURCES_ARE_EXPLICIT_TARGETS
            .to_string();
    }

    // Either the activity cannot start until after the horizon, or starting it at
    // the earliest opportunity would still finish beyond it - in both cases no
    // amount of waiting can make it schedulable.
    let horizon = i64::from(graph_limits::MAXIMUM_TIME_VALUE);
    let earliest_start_time = i64::from(activity.earliest_start_time.unwrap_or(0));
    let duration = i64::from(activity.duration);
    if earliest_start_time > horizon
        || earliest_start_time + duration > horizon
        || i64::from(time_counter) + duration > horizon
    {
        return messages::format_message(
            messages::MSG_CANNOT_BE_SCHEDULED_WITHIN_MAXIMUM_TIME_VALUE,
            &[&graph_limits::MAXIMUM_TIME_VALUE],
        );
    }

    messages::MSG_COULD_NOT_BE_ASSIGNED_TO_ANY_RESOURCE.to_string()
}

/// Renders IDs the way the C# original does (`string.Join(", ", ids)`).
fn join_ids<T: Key>(ids: &[T]) -> String {
    ids.iter()
        .map(|x| x.to_string())
        .collect::<Vec<_>>()
        .join(", ")
}

// -- Scheduling pipeline helpers ---------------------------------------------

/// Gathers the set of activities that reference resources not present in
/// `filtered_resources`.
pub(crate) fn gather_unavailable_resources<'a, K, R, W>(
    activities: impl IntoIterator<Item = &'a Activity<K, R, W>>,
    filtered_resources: &[Resource<R, W>],
) -> Vec<UnavailableResources<K, R>>
where
    K: Key + 'a,
    R: Key + 'a,
    W: Key + 'a,
{
    let mut output = Vec::new();
    for activity in activities {
        if activity.target_resources.is_empty() {
            continue;
        }
        match activity.target_resource_operator {
            // When all explicit target resources must be available.
            LogicalOperator::And => {
                let unavailable: Vec<R> = activity
                    .target_resources
                    .iter()
                    .filter(|r| !filtered_resources.iter().any(|x| x.id == **r))
                    .copied()
                    .collect();
                if !unavailable.is_empty() {
                    output.push(UnavailableResources::new(activity.id(), unavailable));
                }
            }
            // When at least one explicit target resource must be available.
            LogicalOperator::Or | LogicalOperator::ActiveAnd => {
                let has_intersection = activity
                    .target_resources
                    .iter()
                    .any(|r| filtered_resources.iter().any(|x| x.id == *r));
                if !has_intersection {
                    output.push(UnavailableResources::new(
                        activity.id(),
                        activity.target_resources.iter().copied(),
                    ));
                }
            }
        }
    }
    output
}

/// Replaces infinite-resource schedules with synthetic resource IDs so that
/// resource-dependency chaining works in the second compile pass.
pub(crate) fn replace_with_synthetic_resources<K: Key, R: Key, W: Key>(
    resource_schedules: Vec<ResourceSchedule<K, R, W>>,
) -> Vec<ResourceSchedule<K, R, W>> {
    let mut resource_id = R::default();
    let mut replacements = Vec::with_capacity(resource_schedules.len());
    for schedule in resource_schedules {
        resource_id = resource_id.next();
        replacements.push(ResourceSchedule {
            resource: Some(Resource::new(
                resource_id,
                None,
                false,
                false,
                InterActivityAllocationType::None,
                0.0,
                0.0,
                0,
                Vec::new(),
            )),
            scheduled_activities: schedule.scheduled_activities,
            start_time: schedule.start_time,
            finish_time: schedule.finish_time,
            resource_allocation: schedule.resource_allocation,
            cost_allocation: schedule.cost_allocation,
            billing_allocation: schedule.billing_allocation,
            effort_allocation: schedule.effort_allocation,
            activity_allocation: schedule.activity_allocation,
        });
    }
    replacements
}

/// Rebuilds resource schedules aligned to CPM-computed earliest start times.
pub(crate) fn rebuild_aligned_resource_schedules<K, R, W>(
    resource_schedules: &[ResourceSchedule<K, R, W>],
    infinite_resources: bool,
    graph: &dyn IResourceSchedulingGraph<K, R, W>,
    final_activities: &[Activity<K, R, W>],
    start_time: i32,
    finish_time: i32,
) -> Result<Vec<ResourceSchedule<K, R, W>>, GraphError>
where
    K: Key,
    R: Key,
    W: Key,
{
    let mut builders: Vec<ResourceScheduleBuilder<K, R, W>> = Vec::new();
    for old_schedule in resource_schedules {
        let mut builder = match &old_schedule.resource {
            None => ResourceScheduleBuilder::new_unmapped(),
            Some(_) if infinite_resources => ResourceScheduleBuilder::new_unmapped(),
            Some(resource) => ResourceScheduleBuilder::new(resource.clone()),
        };

        for scheduled_activity in &old_schedule.scheduled_activities {
            let activity = graph.activity(scheduled_activity.id);
            // This add needs to be without checks because the alignment may not
            // be perfect.
            builder.append_activity_without_checks(
                &activity.activity,
                activity.earliest_start_time.unwrap_or(0),
            );
        }
        builders.push(builder);
    }

    let mut output = Vec::new();
    for builder in &builders {
        let schedule = builder.to_resource_schedule(final_activities, start_time, finish_time)?;
        if !schedule.scheduled_activities.is_empty() {
            output.push(schedule);
        }
    }
    Ok(output)
}

/// Returns schedules for Indirect resources that were not directly assigned
/// any activities.
pub(crate) fn collect_indirect_resource_schedules<K, R, W>(
    filtered_resources: &[Resource<R, W>],
    scheduled_resources: &[ResourceSchedule<K, R, W>],
    final_activities: &[Activity<K, R, W>],
    start_time: i32,
    finish_time: i32,
) -> Result<Vec<ResourceSchedule<K, R, W>>, GraphError>
where
    K: Key,
    R: Key,
    W: Key,
{
    let scheduled_ids: IndexSet<R> = scheduled_resources
        .iter()
        .filter_map(|x| x.resource.as_ref())
        .map(|r| r.id)
        .collect();

    let mut output = Vec::new();
    for resource in filtered_resources.iter().filter(|x| {
        x.inter_activity_allocation_type == InterActivityAllocationType::Indirect
            && !scheduled_ids.contains(&x.id)
    }) {
        let builder = ResourceScheduleBuilder::<K, R, W>::new(resource.clone());
        output.push(builder.to_resource_schedule(final_activities, start_time, finish_time)?);
    }
    Ok(output)
}

/// Returns the set of work-stream phase IDs that appear on at least one
/// resource schedule.
pub(crate) fn get_resource_phases_used<K, R, W>(
    total_schedules: &[ResourceSchedule<K, R, W>],
    workstreams_used: &IndexSet<W>,
) -> IndexSet<W>
where
    K: Key,
    R: Key,
    W: Key,
{
    let resource_phases: IndexSet<W> = total_schedules
        .iter()
        .filter_map(|x| x.resource.as_ref())
        .flat_map(|r| r.inter_activity_phases.iter().copied())
        .collect();
    resource_phases
        .into_iter()
        .filter(|p| workstreams_used.contains(p))
        .collect()
}
