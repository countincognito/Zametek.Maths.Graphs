use super::schedule_builder::ResourceScheduleBuilder;
use crate::contracts::{IResourceSchedulingEngine, IResourceSchedulingGraph};
use indexmap::{IndexMap, IndexSet};
use zametek_maths_graphs_primitives::{
    Activity, DependentActivity, GraphError, InterActivityAllocationType, Key, LogicalOperator,
    Resource, ResourceSchedule, UnavailableResources,
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

/// Priority-list resource scheduling — the counterpart of the C#
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
        time_counter += 1;
    }

    let final_activities: Vec<DependentActivity<K, R, W>> = graph.clone_activities();
    let final_plain: Vec<Activity<K, R, W>> = final_activities
        .iter()
        .map(|a| a.activity.clone())
        .collect();

    let start_time = resource_schedule_builders
        .iter()
        .map(|x| {
            x.scheduled_activities()
                .iter()
                .map(|y| y.start_time)
                .min()
                .unwrap_or(0)
        })
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
            if max_lft > (time_counter + activity.duration) {
                continue;
            }
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
            if max_lft > (time_counter + activity.duration) {
                continue;
            }
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
