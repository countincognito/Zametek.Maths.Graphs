use zametek_maths_graphs_primitives::{
    Activity, GraphError, InterActivityAllocationType, Key, Resource, ResourceSchedule,
    ScheduledActivity,
};

// Per-time-unit allocation flags - the counterpart of the C# private
// `TimeType` flags enum.
mod time_type {
    pub const NONE: u16 = 0;
    pub const RESOURCE_START: u16 = 1 << 0;
    pub const RESOURCE_MIDDLE: u16 = 1 << 1;
    pub const RESOURCE_BETWEEN: u16 = 1 << 2;
    pub const RESOURCE_FINISH: u16 = 1 << 3;

    pub const PHASE_START: u16 = 1 << 4;
    pub const PHASE_MIDDLE: u16 = 1 << 5;
    pub const PHASE_BETWEEN: u16 = 1 << 6;
    pub const PHASE_FINISH: u16 = 1 << 7;

    pub const ACTIVITY_ALLOCATED: u16 = 1 << 8;

    pub const COST_IGNORED: u16 = 1 << 9;
    pub const BILLING_IGNORED: u16 = 1 << 10;
    pub const EFFORT_IGNORED: u16 = 1 << 11;

    pub const RESOURCE_ALLOCATED: u16 = RESOURCE_START
        | RESOURCE_MIDDLE
        | RESOURCE_BETWEEN
        | RESOURCE_FINISH
        | PHASE_START
        | PHASE_MIDDLE
        | PHASE_BETWEEN
        | PHASE_FINISH;
}

/// Accumulates the activities scheduled onto a single resource and produces
/// the finished [`ResourceSchedule`] with its allocation streams - the
/// counterpart of the C# `ResourceScheduleBuilder`.
pub struct ResourceScheduleBuilder<K: Key, R: Key, W: Key> {
    resource: Option<Resource<R, W>>,
    scheduled_activities: Vec<ScheduledActivity<K>>,
}

impl<K: Key, R: Key, W: Key> ResourceScheduleBuilder<K, R, W> {
    /// Creates a builder for the given resource.
    pub fn new(resource: Resource<R, W>) -> Self {
        Self {
            resource: Some(resource),
            scheduled_activities: Vec::new(),
        }
    }

    /// Creates a builder for an unmapped (infinite-resources) schedule with no resource.
    pub fn new_unmapped() -> Self {
        Self {
            resource: None,
            scheduled_activities: Vec::new(),
        }
    }

    /// The ID of the resource, or `None` for an unmapped schedule.
    pub fn resource_id(&self) -> Option<R> {
        self.resource.as_ref().map(|r| r.id)
    }

    /// Whether the resource is opt-in only (false for unmapped schedules).
    pub fn is_explicit_target(&self) -> bool {
        self.resource
            .as_ref()
            .map(|r| r.is_explicit_target)
            .unwrap_or(false)
    }

    /// Whether the resource is disabled (false for unmapped schedules).
    pub fn is_inactive(&self) -> bool {
        self.resource
            .as_ref()
            .map(|r| r.is_inactive)
            .unwrap_or(false)
    }

    /// The activities scheduled so far.
    pub fn scheduled_activities(&self) -> &[ScheduledActivity<K>] {
        &self.scheduled_activities
    }

    /// The finish time of the last scheduled activity, or zero when empty.
    pub fn last_activity_finish_time(&self) -> i32 {
        self.scheduled_activities
            .last()
            .map(|a| a.finish_time)
            .unwrap_or(0)
    }

    /// The earliest time the next activity could start on this resource.
    pub fn earliest_available_start_time_for_next_activity(&self) -> i32 {
        self.last_activity_finish_time()
    }

    /// Appends an already-scheduled activity, validating that it starts no
    /// earlier than the resource is available.
    pub fn append_scheduled_activity(
        &mut self,
        scheduled_activity: ScheduledActivity<K>,
    ) -> Result<(), GraphError> {
        let earliest_available = self.earliest_available_start_time_for_next_activity();
        if scheduled_activity.start_time < earliest_available {
            return Err(GraphError::new(format!(
                "Scheduled activity's start time {} is less than the earliest available start time for the next activity {}",
                scheduled_activity.start_time, earliest_available
            )));
        }
        self.append_scheduled_activity_without_checks(scheduled_activity);
        Ok(())
    }

    /// Appends an already-scheduled activity without validation.
    pub fn append_scheduled_activity_without_checks(
        &mut self,
        scheduled_activity: ScheduledActivity<K>,
    ) {
        self.scheduled_activities.push(scheduled_activity);
    }

    /// Schedules the activity at the given start time, validating availability
    /// (the start is clamped forward to the resource's earliest availability).
    pub fn append_activity(&mut self, activity: &Activity<K, R, W>, start_time: i32) {
        let mut start_time = start_time;
        let earliest_available = self.earliest_available_start_time_for_next_activity();
        if start_time < earliest_available {
            start_time = earliest_available;
        }
        self.append_activity_without_checks(activity, start_time);
    }

    /// Schedules the activity at the given start time without validation.
    pub fn append_activity_without_checks(
        &mut self,
        activity: &Activity<K, R, W>,
        start_time: i32,
    ) {
        let scheduled_activity = ScheduledActivity::new(
            activity.id(),
            activity.name.clone(),
            activity.has_no_cost,
            activity.has_no_billing,
            activity.has_no_effort,
            activity.duration,
            start_time,
            start_time + activity.duration,
        );
        self.scheduled_activities.push(scheduled_activity);
    }

    /// Removes all scheduled activities.
    pub fn clear_activities(&mut self) {
        self.scheduled_activities.clear();
    }

    /// Returns the ID of the activity occupying the given time, or `None` if
    /// the resource is idle.
    pub fn activity_at(&self, time: i32) -> Option<K> {
        self.scheduled_activities
            .iter()
            .find(|a| time >= a.start_time && time < a.finish_time)
            .map(|a| a.id)
    }

    /// Produces the finished resource schedule, deriving the per-time-unit
    /// allocation streams.
    pub fn to_resource_schedule(
        &self,
        activities: &[Activity<K, R, W>],
        start_time: i32,
        finish_time: i32,
    ) -> Result<ResourceSchedule<K, R, W>, GraphError> {
        let allocations = extract_allocations(
            self.resource.as_ref(),
            &self.scheduled_activities,
            activities,
            finish_time,
        )?;

        Ok(ResourceSchedule {
            resource: self.resource.clone(),
            scheduled_activities: self.scheduled_activities.clone(),
            start_time,
            finish_time,
            resource_allocation: allocations.0,
            cost_allocation: allocations.1,
            billing_allocation: allocations.2,
            effort_allocation: allocations.3,
            activity_allocation: allocations.4,
        })
    }
}

type Allocations = (Vec<bool>, Vec<bool>, Vec<bool>, Vec<bool>, Vec<bool>);

fn extract_allocations<K: Key, R: Key, W: Key>(
    resource: Option<&Resource<R, W>>,
    scheduled_activities: &[ScheduledActivity<K>],
    activities: &[Activity<K, R, W>],
    finish_time: i32,
) -> Result<Allocations, GraphError> {
    let resource_finish_time = scheduled_activities
        .iter()
        .map(|x| x.finish_time)
        .max()
        .unwrap_or(0);
    if resource_finish_time > finish_time {
        return Err(GraphError::new(format!(
            "Requested finish time ({finish_time}) cannot be less than the actual finish time ({resource_finish_time})"
        )));
    }
    let inter_activity_allocation_type = resource
        .map(|r| r.inter_activity_allocation_type)
        .unwrap_or(InterActivityAllocationType::None);

    let mut distribution: Vec<u16> = vec![time_type::NONE; finish_time.max(0) as usize];

    match inter_activity_allocation_type {
        InterActivityAllocationType::Indirect => {
            // The Indirect allocation type can only come from a non-null resource.
            allocation_for_unscheduled_activity_types(
                resource.expect("Indirect allocation requires a resource"),
                activities,
                scheduled_activities,
                &mut distribution,
            )?;
            allocation_for_scheduled_activities_types(scheduled_activities, &mut distribution)?;
        }
        InterActivityAllocationType::None => {
            allocation_for_none_type(scheduled_activities, &mut distribution)?;
            allocation_for_no_cost_or_billing_or_effort_activities(
                scheduled_activities,
                &mut distribution,
            )?;
        }
        InterActivityAllocationType::Direct => {
            allocation_for_scheduled_activities_types(scheduled_activities, &mut distribution)?;
            allocation_for_no_cost_or_billing_or_effort_activities(
                scheduled_activities,
                &mut distribution,
            )?;
        }
    }

    let resource_allocation = distribution
        .iter()
        .map(|x| (x & time_type::RESOURCE_ALLOCATED) != 0)
        .collect();
    let cost_allocation = distribution
        .iter()
        .map(|x| (x & time_type::RESOURCE_ALLOCATED) != 0 && (x & time_type::COST_IGNORED) == 0)
        .collect();
    let billing_allocation = distribution
        .iter()
        .map(|x| (x & time_type::RESOURCE_ALLOCATED) != 0 && (x & time_type::BILLING_IGNORED) == 0)
        .collect();
    let effort_allocation = distribution
        .iter()
        .map(|x| (x & time_type::RESOURCE_ALLOCATED) != 0 && (x & time_type::EFFORT_IGNORED) == 0)
        .collect();
    let activity_allocation = distribution
        .iter()
        .map(|x| (x & time_type::ACTIVITY_ALLOCATED) != 0)
        .collect();

    Ok((
        resource_allocation,
        cost_allocation,
        billing_allocation,
        effort_allocation,
        activity_allocation,
    ))
}

fn check_distribution_length<K: Key>(
    scheduled_activities: &[ScheduledActivity<K>],
    distribution: &[u16],
) -> Result<(), GraphError> {
    let latest_activity_finish_time = scheduled_activities
        .iter()
        .map(|x| x.finish_time)
        .max()
        .unwrap_or(0);
    if (distribution.len() as i32) < latest_activity_finish_time {
        return Err(GraphError::new(format!(
            "Distribution length ({}) cannot be less than latest activity finish time ({})",
            distribution.len(),
            latest_activity_finish_time
        )));
    }
    Ok(())
}

// Clamps a scheduled activity to non-negative distribution indices.
fn clamped_range<K: Key>(scheduled_activity: &ScheduledActivity<K>) -> (usize, usize) {
    let start_index = scheduled_activity.start_time.max(0) as usize;
    let finish_index = (scheduled_activity.finish_time - 1).max(0) as usize;
    (start_index, finish_index)
}

fn allocation_for_unscheduled_activity_types<K: Key, R: Key, W: Key>(
    resource: &Resource<R, W>,
    activities: &[Activity<K, R, W>],
    scheduled_activities: &[ScheduledActivity<K>],
    distribution: &mut [u16],
) -> Result<(), GraphError> {
    if distribution.is_empty() {
        return Ok(());
    }

    check_distribution_length(scheduled_activities, distribution)?;

    // If the type is Indirect, then the resource must exist.
    let resource_phases = &resource.inter_activity_phases;

    // If the resource has no phases then assume the default and mark the
    // entire time span as costed, from start to finish.
    if resource_phases.is_empty() {
        distribution[0] |= time_type::PHASE_START;
        let last = distribution.len() - 1;
        distribution[last] |= time_type::PHASE_FINISH;

        for slot in distribution.iter_mut() {
            *slot |= time_type::PHASE_MIDDLE;
        }
        return Ok(());
    }

    // Otherwise, we have to go through each activity and find where the
    // associated phases start and end.

    // Find the range for each resource phase (phased work stream).
    let workstreams_used: indexmap::IndexSet<W> = activities
        .iter()
        .flat_map(|x| x.target_work_streams.iter().copied())
        .collect();

    let resource_phases_used: indexmap::IndexSet<W> = resource_phases
        .iter()
        .filter(|p| workstreams_used.contains(*p))
        .copied()
        .collect();

    let mut ordered_activities: Vec<&Activity<K, R, W>> = activities.iter().collect();
    ordered_activities.sort_by_key(|x| (x.earliest_start_time, x.latest_start_time()));

    let mut resource_phase_starts: indexmap::IndexMap<W, i32> = indexmap::IndexMap::new();
    let mut resource_phase_ends: indexmap::IndexMap<W, i32> = indexmap::IndexMap::new();

    for activity in ordered_activities {
        for work_stream in activity
            .target_work_streams
            .iter()
            .filter(|w| resource_phases_used.contains(*w))
        {
            let earliest_start_time = activity.earliest_start_time.unwrap_or(0);
            let earliest_end_time = activity.earliest_finish_time().unwrap_or(0);

            // Gather the start times. Since the activities are ordered, we are
            // not interested in any later start times.
            resource_phase_starts
                .entry(*work_stream)
                .or_insert(earliest_start_time);

            // Gather the end times.
            match resource_phase_ends.get_mut(work_stream) {
                Some(current_end_time) => {
                    if earliest_end_time > *current_end_time {
                        *current_end_time = earliest_end_time;
                    }
                }
                None => {
                    resource_phase_ends.insert(*work_stream, earliest_end_time);
                }
            }
        }
    }

    // Check to make sure the key collections are the same.
    let start_keys: Vec<W> = resource_phase_starts.keys().copied().collect();
    let end_keys: Vec<W> = resource_phase_ends.keys().copied().collect();
    if start_keys != end_keys {
        // Note: "resouce" reproduces the typo in the original C# message.
        return Err(GraphError::new(format!(
            "Keys for phase starting points does not match the keys for phase ending points for resouce {}.",
            resource.id
        )));
    }

    // Now we find the earliest start and the latest end and use those to mark
    // out the full range.
    let start_time = resource_phase_starts.values().copied().min().unwrap_or(0);
    let end_time = resource_phase_ends.values().copied().max().unwrap_or(0);

    // If start and end times are both 0 then that means the specific phase was
    // never used, so just leave the allocations as 'ignore'.
    if start_time != 0 || end_time != 0 {
        let start_index = start_time.max(0) as usize;
        let finish_index = (end_time - 1).max(0) as usize;

        distribution[start_index] |= time_type::PHASE_START;
        distribution[finish_index] |= time_type::PHASE_FINISH;

        for slot in distribution
            .iter_mut()
            .take(finish_index + 1)
            .skip(start_index)
        {
            *slot |= time_type::PHASE_MIDDLE;
        }
    }
    Ok(())
}

fn allocation_for_scheduled_activities_types<K: Key>(
    scheduled_activities: &[ScheduledActivity<K>],
    distribution: &mut [u16],
) -> Result<(), GraphError> {
    check_distribution_length(scheduled_activities, distribution)?;

    // Mark schedules as normal.
    for scheduled_activity in scheduled_activities {
        let (start_index, finish_index) = clamped_range(scheduled_activity);

        distribution[start_index] |= time_type::RESOURCE_START | time_type::PHASE_START;
        distribution[finish_index] |= time_type::RESOURCE_FINISH | time_type::PHASE_FINISH;

        for slot in distribution
            .iter_mut()
            .take(finish_index + 1)
            .skip(start_index)
        {
            *slot |= time_type::RESOURCE_MIDDLE
                | time_type::PHASE_MIDDLE
                | time_type::ACTIVITY_ALLOCATED;
        }
    }

    // Find the first Start and the last Finish, then fill in the gaps between
    // them. But just for scheduled activities.
    fill_between(
        distribution,
        time_type::RESOURCE_START,
        time_type::RESOURCE_FINISH,
        time_type::RESOURCE_BETWEEN,
    );

    // Now do the same for phases.
    fill_between(
        distribution,
        time_type::PHASE_START,
        time_type::PHASE_FINISH,
        time_type::PHASE_BETWEEN,
    );

    Ok(())
}

fn fill_between(distribution: &mut [u16], start_flag: u16, finish_flag: u16, between_flag: u16) {
    if distribution.is_empty() {
        return;
    }

    let mut first_start_index = 0usize;
    let mut last_finish_index = distribution.len() - 1;

    let mut start_found = false;
    for (i, slot) in distribution.iter().enumerate() {
        if (slot & start_flag) != 0 || (slot & finish_flag) != 0 {
            first_start_index = i;
            start_found = true;
            break;
        }
    }

    let mut end_found = false;
    for i in (0..distribution.len()).rev() {
        if (distribution[i] & start_flag) != 0 || (distribution[i] & finish_flag) != 0 {
            last_finish_index = i;
            end_found = true;
            break;
        }
    }

    if start_found || end_found {
        for slot in distribution
            .iter_mut()
            .take(last_finish_index)
            .skip(first_start_index + 1)
        {
            *slot |= between_flag;
        }
    }
}

fn allocation_for_none_type<K: Key>(
    scheduled_activities: &[ScheduledActivity<K>],
    distribution: &mut [u16],
) -> Result<(), GraphError> {
    check_distribution_length(scheduled_activities, distribution)?;

    // Mark schedules as normal.
    for scheduled_activity in scheduled_activities {
        let (start_index, finish_index) = clamped_range(scheduled_activity);

        distribution[start_index] |= time_type::RESOURCE_START;
        distribution[finish_index] |= time_type::RESOURCE_FINISH;

        for slot in distribution
            .iter_mut()
            .take(finish_index + 1)
            .skip(start_index)
        {
            *slot |= time_type::RESOURCE_MIDDLE | time_type::ACTIVITY_ALLOCATED;
        }
    }
    Ok(())
}

fn allocation_for_no_cost_or_billing_or_effort_activities<K: Key>(
    scheduled_activities: &[ScheduledActivity<K>],
    distribution: &mut [u16],
) -> Result<(), GraphError> {
    check_distribution_length(scheduled_activities, distribution)?;

    // Now mark the uncosted areas.
    for scheduled_activity in scheduled_activities {
        let (start_index, finish_index) = clamped_range(scheduled_activity);

        if scheduled_activity.has_no_cost {
            for slot in distribution
                .iter_mut()
                .take(finish_index + 1)
                .skip(start_index)
            {
                *slot |= time_type::COST_IGNORED;
            }
        }
        if scheduled_activity.has_no_billing {
            for slot in distribution
                .iter_mut()
                .take(finish_index + 1)
                .skip(start_index)
            {
                *slot |= time_type::BILLING_IGNORED;
            }
        }
        if scheduled_activity.has_no_effort {
            for slot in distribution
                .iter_mut()
                .take(finish_index + 1)
                .skip(start_index)
            {
                *slot |= time_type::EFFORT_IGNORED;
            }
        }
    }
    Ok(())
}
