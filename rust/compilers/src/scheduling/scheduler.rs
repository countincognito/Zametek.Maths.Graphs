use super::schedule_builder::ResourceScheduleBuilder;
use crate::contracts::{IResourceSchedulingEngine, IResourceSchedulingGraph};
use crate::messages;
use indexmap::{IndexMap, IndexSet};
use std::collections::VecDeque;
use std::fmt::Write as _;
use std::rc::Rc;
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

    // The allocation streams run from time 0 to this finish time, and an indirect
    // resource marks its phases in them from the first to the last activity of
    // each phase. An activity of zero duration is on no resource, but it can be in
    // a phase and finish after each activity that takes time. So the finish time
    // covers each activity, and not only the activities on the resources.
    let finish_time = resource_schedule_builders
        .iter()
        .map(|x| x.last_activity_finish_time())
        .max()
        .unwrap_or(0)
        .max(
            final_plain
                .iter()
                .map(|x| x.earliest_finish_time().unwrap_or(0))
                .max()
                .unwrap_or(0),
        );

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

    // With infinite resources each ready activity gets a builder, and so none
    // waits for one.
    if !infinite_resources {
        give_way_to_waiting_activities(ready, builders, &*graph, started, time_counter);
    }
}

// Each ready activity has now taken the first free builder in the allocation
// order that it can use. A ready activity can still wait while a builder is free:
// an activity that started at this tick took the builder that it needs, and that
// activity can also use the free one. Then the activity that started moves to the
// free builder, and the activity that waits starts on the builder that it leaves.
// Neither starts later than it did, and a resource does not stay idle for an
// activity that could not use it.
//
// A move can need a chain of moves: A leaves R1 for R2, and B leaves R2 for the
// free R3. The search is breadth first, and so it makes the fewest moves, and it
// tries the builders in the allocation order. Only an activity that can use any
// one of several builders moves. An activity that needs all of its targets stays,
// but it can be the one that waits: then each activity on one of its builders
// must move to a builder that it does not need.
//
// Nothing happens when no builder is free, or when no activity waits that could
// start now. Thus, the schedule is the one of the first-free rule alone, except
// at a tick where that rule leaves a builder idle that a chain of moves can put
// to use.
fn give_way_to_waiting_activities<K: Key, R: Key, W: Key>(
    ready: &mut [Option<K>],
    builders: &mut [ResourceScheduleBuilder<K, R, W>],
    graph: &dyn IResourceSchedulingGraph<K, R, W>,
    started: &mut IndexSet<K>,
    time_counter: i32,
) {
    let any_free = builders
        .iter()
        .any(|x| x.earliest_available_start_time_for_next_activity() <= time_counter);
    if !any_free {
        return;
    }

    // The activities that wait and whose time gates are open. An activity of zero
    // duration takes no time on a builder, and the first-free rule alone decides
    // where it goes.
    let mut waiting: Vec<usize> = Vec::new();
    for (i, slot) in ready.iter().enumerate() {
        let Some(activity_id) = *slot else {
            continue;
        };
        let activity = graph.activity(activity_id);
        if !activity.is_dummy() && can_start_at(activity, time_counter) {
            waiting.push(i);
        }
    }
    if waiting.is_empty() {
        return;
    }

    let mut seats = TickSeats::new(builders, graph, time_counter);
    let mut newly_seated: Vec<(usize, Vec<usize>)> = Vec::new();

    for ready_index in waiting {
        let Some(activity_id) = ready[ready_index] else {
            continue;
        };
        let activity = graph.activity(activity_id);

        let taken = if can_use_any_one_of_its_builders(activity) {
            seats.try_seat_on_one(activity_id)
        } else {
            seats.try_seat_on_all(activity_id, activity)
        };

        if let Some(taken) = taken {
            newly_seated.push((ready_index, taken));
        }
    }

    if newly_seated.is_empty() {
        return;
    }

    // First take each activity that moves off its builder. It is the last one
    // there, because it started at this tick and it takes time. Then put each on
    // its new builder, and start the activities that waited.
    let movers = seats.movers();
    for mover in &movers {
        builders[seats.original_seat_of(*mover)].remove_last_activity();
    }
    for mover in &movers {
        builders[seats.seat_of(*mover)].append_activity(graph.activity(*mover), time_counter);
    }
    for (ready_index, taken) in newly_seated {
        let Some(activity_id) = ready[ready_index] else {
            continue;
        };
        let activity = graph.activity(activity_id);

        // A later move can have moved an activity that waited to another builder.
        let final_seats = if can_use_any_one_of_its_builders(activity) {
            vec![seats.seat_of(activity_id)]
        } else {
            taken
        };
        for seat in final_seats {
            builders[seat].append_activity(activity, time_counter);
        }
        started.insert(activity_id);
        ready[ready_index] = None;
    }
}

// Whether the time gates of the activity are open at this tick: its earliest
// start time is reached, a maximum latest finish time does not hold it back to a
// later start, and it finishes within the supported time horizon.
fn can_start_at<K: Key, R: Key, W: Key>(activity: &Activity<K, R, W>, time_counter: i32) -> bool {
    if activity.earliest_start_time.unwrap_or(0) > time_counter {
        return false;
    }
    if let Some(max_lft) = activity.maximum_latest_finish_time {
        if i64::from(max_lft) > i64::from(time_counter) + i64::from(activity.duration) {
            return false;
        }
    }
    // Starting here would push the finish time past the supported time
    // horizon; the activity can never start, which the stall detector reports.
    if i64::from(time_counter) + i64::from(activity.duration)
        > i64::from(graph_limits::MAXIMUM_TIME_VALUE)
    {
        return false;
    }
    true
}

// An activity with no targets can use any one builder that is not an explicit
// target, and an activity with the OR operator can use any one of its targets. An
// activity with AND or ACTIVE_AND needs all of its targets at once.
fn can_use_any_one_of_its_builders<K: Key, R: Key, W: Key>(activity: &Activity<K, R, W>) -> bool {
    activity.target_resources.is_empty() || activity.target_resource_operator == LogicalOperator::Or
}

// The builders of one tick, for `give_way_to_waiting_activities`: which are free,
// and which hold an activity that started at this tick and can move. The moves
// are made here first, and on the builders themselves only when the search is
// over.
//
// The C# class keeps the list of the builders. Here that would hold a borrow of
// the builders until the search is over, and the moves must change them while the
// seats are still read. So `new` copies the two values of each builder that the
// search needs: the ID of its resource, and whether it is an explicit target. No
// move changes them.
struct TickSeats<'a, K: Key, R: Key, W: Key> {
    builders: Vec<(Option<R>, bool)>,
    graph: &'a dyn IResourceSchedulingGraph<K, R, W>,
    is_free: Vec<bool>,
    holder_of: Vec<Option<K>>,
    seat_of: IndexMap<K, usize>,
    original_seat_of: IndexMap<K, usize>,
    usable_builders: IndexMap<K, Rc<[usize]>>,

    // The builders that a search must not use. A search that fails leaves its
    // marks: the builders that it reached lead to no free builder, also for the
    // next activity that waits. A search that succeeds clears them, because its
    // moves change what can be reached.
    closed: Vec<bool>,
    previous: Vec<Option<usize>>,
    queue: VecDeque<usize>,
}

impl<'a, K: Key, R: Key, W: Key> TickSeats<'a, K, R, W> {
    fn new(
        builders: &[ResourceScheduleBuilder<K, R, W>],
        graph: &'a dyn IResourceSchedulingGraph<K, R, W>,
        time_counter: i32,
    ) -> Self {
        let mut seats = Self {
            builders: builders
                .iter()
                .map(|x| (x.resource_id(), x.is_explicit_target()))
                .collect(),
            graph,
            is_free: vec![false; builders.len()],
            holder_of: vec![None; builders.len()],
            seat_of: IndexMap::new(),
            original_seat_of: IndexMap::new(),
            usable_builders: IndexMap::new(),
            closed: vec![false; builders.len()],
            previous: vec![None; builders.len()],
            queue: VecDeque::new(),
        };

        for (index, builder) in builders.iter().enumerate() {
            seats.is_free[index] =
                builder.earliest_available_start_time_for_next_activity() <= time_counter;
            if seats.is_free[index] {
                continue;
            }
            // A builder that is not free holds an activity that takes time. It
            // started at this tick when its start time is this tick, and then it
            // can move when it can use any one of several builders.
            if let Some(last) = builder.last_scheduled_activity() {
                if last.start_time == time_counter
                    && can_use_any_one_of_its_builders(graph.activity(last.id))
                {
                    seats.holder_of[index] = Some(last.id);
                    seats.seat_of.insert(last.id, index);
                    seats.original_seat_of.insert(last.id, index);
                }
            }
        }
        seats
    }

    fn seat_of(&self, activity_id: K) -> usize {
        self.seat_of[&activity_id]
    }

    fn original_seat_of(&self, activity_id: K) -> usize {
        self.original_seat_of[&activity_id]
    }

    // The activities that started at this tick and are now on another builder.
    fn movers(&self) -> Vec<K> {
        let mut movers = Vec::new();
        for (activity_id, original) in &self.original_seat_of {
            if self.seat_of[activity_id] != *original {
                movers.push(*activity_id);
            }
        }
        movers
    }

    // Seats an activity that can use any one of its builders. Returns its
    // builder, or `None`.
    fn try_seat_on_one(&mut self, activity_id: K) -> Option<Vec<usize>> {
        let seat = self.free_one_for(activity_id)?;
        self.occupy(seat, activity_id);
        self.closed.fill(false);
        Some(vec![seat])
    }

    // Seats an activity that needs all of its builders: each activity on one of
    // them moves to a builder that is not one of them. Returns the builders, or
    // `None` when one of them cannot be made free. Then nothing has moved.
    fn try_seat_on_all(
        &mut self,
        activity_id: K,
        activity: &Activity<K, R, W>,
    ) -> Option<Vec<usize>> {
        let needed = self.usable_builders(activity_id);

        // With AND, each target must be a resource of the plan. With ACTIVE_AND,
        // the targets that are resources of the plan are enough, and there must
        // be one.
        if needed.is_empty()
            || (activity.target_resource_operator == LogicalOperator::And
                && needed.len() != activity.target_resources.len())
        {
            return None;
        }

        let is_free_before = self.is_free.clone();
        let holder_of_before = self.holder_of.clone();
        let seat_of_before = self.seat_of.clone();
        let closed_before = self.closed.clone();

        let mut all_free = true;
        for &index in needed.iter() {
            if self.is_free[index] {
                continue;
            }
            let Some(holder) = self.holder_of[index] else {
                all_free = false;
                break;
            };
            // The holder must not move to a builder that the activity needs.
            self.closed.fill(false);
            for &reserved in needed.iter() {
                self.closed[reserved] = true;
            }
            let Some(seat) = self.free_one_for(holder) else {
                all_free = false;
                break;
            };
            self.occupy(seat, holder);
            self.holder_of[index] = None;
            self.is_free[index] = true;
        }

        if !all_free {
            self.is_free = is_free_before;
            self.holder_of = holder_of_before;
            self.seat_of = seat_of_before;
            self.closed = closed_before;
            return None;
        }

        // An activity that needs all of its builders does not move, and so it is
        // no holder.
        for &index in needed.iter() {
            self.is_free[index] = false;
            self.holder_of[index] = None;
        }
        self.closed.fill(false);
        Some(needed.to_vec())
    }

    fn occupy(&mut self, index: usize, activity_id: K) {
        self.is_free[index] = false;
        self.holder_of[index] = Some(activity_id);
        self.seat_of.insert(activity_id, index);
    }

    // The builders that the activity can use, in the allocation order: its
    // targets, or each builder that is not an explicit target when it has no
    // targets.
    fn usable_builders(&mut self, activity_id: K) -> Rc<[usize]> {
        if let Some(usable) = self.usable_builders.get(&activity_id) {
            return Rc::clone(usable);
        }
        let activity = self.graph.activity(activity_id);
        let targeted = !activity.target_resources.is_empty();
        let mut usable: Vec<usize> = Vec::new();
        for (index, (resource_id, is_explicit_target)) in self.builders.iter().enumerate() {
            let can_use = if targeted {
                resource_id.is_some_and(|x| activity.target_resources.contains(&x))
            } else {
                !*is_explicit_target
            };
            if can_use {
                usable.push(index);
            }
        }
        let usable: Rc<[usize]> = usable.into();
        self.usable_builders.insert(activity_id, Rc::clone(&usable));
        usable
    }

    // Makes one builder free that the activity can use, and returns it, or
    // `None`. The search goes from the builders of the activity, through the
    // builders that the holders of those can use, to a builder that is free. Then
    // each holder on the way moves one step along it, and the first builder of
    // the way is free.
    fn free_one_for(&mut self, activity_id: K) -> Option<usize> {
        self.queue.clear();
        for &index in self.usable_builders(activity_id).iter() {
            if !self.closed[index] {
                self.closed[index] = true;
                self.previous[index] = None;
                self.queue.push_back(index);
            }
        }

        while let Some(index) = self.queue.pop_front() {
            if self.is_free[index] {
                let mut current = index;
                while let Some(previous) = self.previous[current] {
                    // A builder leads to the next one only through its holder.
                    let holder = self.holder_of[previous]
                        .expect("a builder on the way to a free builder has a holder");
                    self.occupy(current, holder);
                    current = previous;
                }
                self.holder_of[current] = None;
                self.is_free[current] = true;
                return Some(current);
            }

            let Some(holder) = self.holder_of[index] else {
                continue;
            };
            for &next in self.usable_builders(holder).iter() {
                if !self.closed[next] {
                    self.closed[next] = true;
                    self.previous[next] = Some(index);
                    self.queue.push_back(next);
                }
            }
        }
        None
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
        if !can_start_at(activity, time_counter) {
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
        if !can_start_at(activity, time_counter) {
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

/// Replaces infinite-resource schedules with synthetic resource IDs. Up to
/// 4.1.1 the compile called this, to chain the activities of each schedule with
/// resource dependencies. It no longer does, and this stays for other callers.
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
    if infinite_resources {
        return rebuild_lanes(
            resource_schedules,
            graph,
            final_activities,
            start_time,
            finish_time,
        );
    }

    let mut builders: Vec<ResourceScheduleBuilder<K, R, W>> = Vec::new();
    for old_schedule in resource_schedules {
        let mut builder = match &old_schedule.resource {
            None => ResourceScheduleBuilder::new_unmapped(),
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

// The activities of one lane, each with its start time on the lane.
type Lane<'a, K, R, W> = Vec<(i32, &'a Activity<K, R, W>)>;

// With infinite resources, the schedules are lanes with no resource: they show
// how many activities run at the same time. Each activity goes at its earliest
// start time, because the lanes add no dependency to the plan.
//
// An activity that the scheduler started at its earliest start time stays on its
// lane. Those activities do not overlap, because the scheduler put no two of them
// on one lane at once. The scheduler holds an activity with a maximum LFT back to
// the latest start that meets it, and the critical path can pull an activity back
// to before its predecessors. Such an activity is at another time here than in
// the scheduler. It goes on its own lane when that is free for the whole of its
// new time, and else on the first lane that is, or on a new lane. Thus, no lane
// has two activities at once, and the lanes are those of the scheduler for a plan
// with no maximum LFT.
fn rebuild_lanes<K, R, W>(
    resource_schedules: &[ResourceSchedule<K, R, W>],
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
    let mut lanes: Vec<Lane<K, R, W>> = Vec::new();
    let mut moved: Vec<(usize, &Activity<K, R, W>)> = Vec::new();

    for (lane_index, old_schedule) in resource_schedules.iter().enumerate() {
        let mut lane: Lane<K, R, W> = Vec::new();
        for scheduled_activity in &old_schedule.scheduled_activities {
            let activity = &graph.activity(scheduled_activity.id).activity;
            if scheduled_activity.start_time == activity.earliest_start_time.unwrap_or(0) {
                lane.push((scheduled_activity.start_time, activity));
            } else {
                moved.push((lane_index, activity));
            }
        }
        lanes.push(lane);
    }

    for (lane_index, activity) in moved {
        let earliest_start_time = activity.earliest_start_time.unwrap_or(0);
        let mut target: Option<usize> = None;
        if is_lane_free(&lanes[lane_index], earliest_start_time, activity.duration) {
            target = Some(lane_index);
        }
        if target.is_none() {
            target = lanes
                .iter()
                .position(|lane| is_lane_free(lane, earliest_start_time, activity.duration));
        }
        let target = target.unwrap_or_else(|| {
            lanes.push(Vec::new());
            lanes.len() - 1
        });
        lanes[target].push((earliest_start_time, activity));
    }

    let mut output = Vec::new();
    for mut lane in lanes {
        if lane.is_empty() {
            continue;
        }
        let mut builder = ResourceScheduleBuilder::new_unmapped();
        // The sort is stable (as the C# `OrderBy` is): it keeps the order of two
        // activities that start at the same time.
        lane.sort_by_key(|(lane_start_time, _)| *lane_start_time);
        for (lane_start_time, activity) in lane {
            builder.append_activity_without_checks(activity, lane_start_time);
        }
        output.push(builder.to_resource_schedule(final_activities, start_time, finish_time)?);
    }
    Ok(output)
}

// Whether no activity of the lane runs at any time from the start time to the
// start time plus the duration.
fn is_lane_free<K: Key, R: Key, W: Key>(
    lane: &[(i32, &Activity<K, R, W>)],
    start_time: i32,
    duration: i32,
) -> bool {
    let finish_time = i64::from(start_time) + i64::from(duration);
    for (lane_start_time, activity) in lane {
        if i64::from(*lane_start_time) < finish_time
            && i64::from(start_time) < i64::from(*lane_start_time) + i64::from(activity.duration)
        {
            return false;
        }
    }
    true
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
