using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    // Stateless priority-list resource scheduler.
    // Receives the priority-ordered activity list and callbacks for graph state -
    // works identically for both Arrow and Vertex builders.
    // Also owns the scheduling pipeline helpers used before and after the core loop:
    //   GatherUnavailableResources, ReplaceWithSyntheticResources,
    //   RebuildAlignedResourceSchedules, CollectIndirectResourceSchedules, GetResourcePhasesUsed.
    /// <summary>
    /// Default resource scheduling engine: priority-list allocation plus the surrounding scheduling pipeline.
    /// </summary>
    public sealed class PriorityListResourceScheduler<T, TResourceId, TWorkStreamId>
        : IResourceSchedulingEngine<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        /// <inheritdoc/>
        public IEnumerable<IResourceSchedule<T, TResourceId, TWorkStreamId>> CalculateResourceSchedules(
            List<T> priorityList,
            List<IResource<TResourceId, TWorkStreamId>> filteredResources,
            bool infiniteResources,
            IResourceSchedulingGraph<T, TResourceId, TWorkStreamId> graph)
        {
            if (priorityList is null)
            {
                throw new ArgumentNullException(nameof(priorityList));
            }
            if (filteredResources is null)
            {
                throw new ArgumentNullException(nameof(filteredResources));
            }
            if (graph is null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            List<T?> workingList = priorityList.Select(x => new T?(x)).ToList();

            List<ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>> resourceScheduleBuilders = filteredResources
                .OrderBy(x => x.AllocationOrder)
                .Select(x => new ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>(x))
                .ToList();

            var completed = new HashSet<T>();
            var started = new HashSet<T>();
            var ready = new List<T?>(Enumerable.Repeat((T?)null, workingList.Count));
            int timeCounter = 0;

            while (workingList.Any(x => x.HasValue) || started.Count != 0 || ready.Any(x => x.HasValue))
            {
                AdvanceCompletedActivities(resourceScheduleBuilders, timeCounter, started, completed);
                PromoteReadyActivities(workingList, ready, completed, started, graph);
                AssignReadyActivitiesToResources(ready, resourceScheduleBuilders, graph, filteredResources,
                    infiniteResources, started, timeCounter);
                timeCounter++;
            }

            List<IActivity<T, TResourceId, TWorkStreamId>> finalActivities = graph.CloneActivities();

            int startTime = resourceScheduleBuilders
                .Select(x => x.ScheduledActivities.Select(y => y.StartTime).DefaultIfEmpty().Min())
                .DefaultIfEmpty().Min();

            int finishTime = resourceScheduleBuilders
                .Select(x => x.LastActivityFinishTime)
                .DefaultIfEmpty().Max();

            return resourceScheduleBuilders
                .Select(x => x.ToResourceSchedule(finalActivities, startTime, finishTime))
                .Where(x => x.ScheduledActivities.Any())
                .ToList();
        }

        private static void AdvanceCompletedActivities(
            List<ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>> builders,
            int timeCounter,
            HashSet<T> started,
            HashSet<T> completed)
        {
            // Any started activities that are currently not running must now be completed.
            HashSet<T> running = new HashSet<T>(builders
                .Select(x => x.ActivityAt(timeCounter))
                .Where(x => x.HasValue)
                .Select(x => x.GetValueOrDefault()));

            // Now work out which of the started jobs are now completed.
            IList<T> notYetCompleted = started.Intersect(running).ToList();
            started.ExceptWith(notYetCompleted);
            completed.UnionWith(started);
            // Refresh the started set.
            started.Clear();
            started.UnionWith(notYetCompleted);
        }

        private static void PromoteReadyActivities(
            List<T?> workingList,
            List<T?> ready,
            HashSet<T> completed,
            HashSet<T> started,
            IResourceSchedulingGraph<T, TResourceId, TWorkStreamId> graph)
        {
            // Get the activities that have completed direct dependencies.
            // Add these to the ready queue since there is nothing preventing them from starting.
            var indicesToRemove = new HashSet<int>();
            for (int i = 0; i < workingList.Count; i++)
            {
                if (!workingList[i].HasValue)
                {
                    continue;
                }
                T activityId = workingList[i].GetValueOrDefault();
                var directDependencies = new HashSet<T>(graph.StrongActivityDependencyIds(activityId));
                if (directDependencies.IsSubsetOf(completed)
                    && !completed.Contains(activityId)
                    && !started.Contains(activityId))
                {
                    ready[i] = activityId;
                    indicesToRemove.Add(i);
                }
            }
            // Now clear the activities that have been added to the ready queue.
            foreach (int idx in indicesToRemove)
            {
                workingList[idx] = null;
            }
        }

        private static void AssignReadyActivitiesToResources(
            List<T?> ready,
            List<ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>> builders,
            IResourceSchedulingGraph<T, TResourceId, TWorkStreamId> graph,
            List<IResource<TResourceId, TWorkStreamId>> filteredResources,
            bool infiniteResources,
            HashSet<T> started,
            int timeCounter)
        {
            // Cycle through each ready activity and find the first currently available schedule builder.
            bool keepLooking = true;
            while (ready.Any(x => x.HasValue) && keepLooking)
            {
                keepLooking = false;
                bool availableBuilderExists = false;

                for (int i = 0; i < ready.Count; i++)
                {
                    if (!ready[i].HasValue)
                    {
                        continue;
                    }
                    T activityId = ready[i].GetValueOrDefault();
                    IActivity<T, TResourceId, TWorkStreamId> activity = graph.Activity(activityId);
                    activity.AllocatedToResources.Clear();

                    // Check to see if the activity has to be targeted to specific resources,
                    // and that this resource is one of those specific targets.
                    bool mustTargetSpecific = !infiniteResources && activity.TargetResources.Count != 0;

                    if (!mustTargetSpecific)
                    {
                        bool scheduled = TryScheduleUnrestricted(
                            builders, activity, activityId, timeCounter, started, ref availableBuilderExists);
                        if (scheduled)
                        {
                            ready[i] = null;
                            keepLooking = true;
                        }
                    }
                    else
                    {
                        bool scheduled = TryScheduleTargeted(
                            builders, activity, activityId, filteredResources, timeCounter, started,
                            ref availableBuilderExists);
                        if (scheduled)
                        {
                            ready[i] = null;
                            keepLooking = true;
                        }
                    }
                }

                if (infiniteResources && !availableBuilderExists && !keepLooking)
                {
                    builders.Add(new ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>());
                    keepLooking = true;
                }
            }
        }

        private static bool TryScheduleUnrestricted(
            List<ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>> builders,
            IActivity<T, TResourceId, TWorkStreamId> activity,
            T activityId,
            int timeCounter,
            HashSet<T> started,
            ref bool availableBuilderExists)
        {
            foreach (ResourceScheduleBuilder<T, TResourceId, TWorkStreamId> builder in builders)
            {
                if (builder.EarliestAvailableStartTimeForNextActivity > timeCounter)
                {
                    continue;
                }
                availableBuilderExists = true;
                if (builder.IsExplicitTarget)
                {
                    continue;
                }
                if (activity.EarliestStartTime.GetValueOrDefault() > timeCounter)
                {
                    continue;
                }
                if (activity.MaximumLatestFinishTime.HasValue
                    && activity.MaximumLatestFinishTime.GetValueOrDefault() > (timeCounter + activity.Duration))
                {
                    continue;
                }
                builder.AppendActivity(activity, timeCounter);
                started.Add(activityId);
                return true;
            }
            return false;
        }

        private static bool TryScheduleTargeted(
            List<ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>> builders,
            IActivity<T, TResourceId, TWorkStreamId> activity,
            T activityId,
            List<IResource<TResourceId, TWorkStreamId>> filteredResources,
            int timeCounter,
            HashSet<T> started,
            ref bool availableBuilderExists)
        {
            var available = new HashSet<ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>>();

            foreach (ResourceScheduleBuilder<T, TResourceId, TWorkStreamId> builder in builders)
            {
                if (builder.EarliestAvailableStartTimeForNextActivity > timeCounter)
                {
                    continue;
                }
                availableBuilderExists = true;

                if (builder.ResourceId != null
                    && !activity.TargetResources.Contains(builder.ResourceId.GetValueOrDefault()))
                {
                    continue;
                }
                if (activity.EarliestStartTime.GetValueOrDefault() > timeCounter)
                {
                    continue;
                }
                if (activity.MaximumLatestFinishTime.HasValue
                    && activity.MaximumLatestFinishTime.GetValueOrDefault() > (timeCounter + activity.Duration))
                {
                    continue;
                }

                // Find just one resource that can accommodate the activity.
                if (activity.TargetResourceOperator == LogicalOperator.OR)
                {
                    builder.AppendActivity(activity, timeCounter);
                    started.Add(activityId);
                    return true;
                }
                // Find all the resources that must accommodate the activity.
                else if (activity.TargetResourceOperator == LogicalOperator.AND)
                {
                    available.Add(builder);
                    var targetSet = new HashSet<TResourceId>(activity.TargetResources);
                    if (targetSet.SetEquals(available.Select(x => x.ResourceId.GetValueOrDefault())))
                    {
                        foreach (var r in available) { r.AppendActivity(activity, timeCounter); started.Add(activityId); }
                        return true;
                    }
                }
                // Find all the active resources that must accommodate the activity.
                else if (activity.TargetResourceOperator == LogicalOperator.ACTIVE_AND)
                {
                    available.Add(builder);
                    // Check intersection of TargetResources and filtered Resources.
                    var intersection = new HashSet<TResourceId>(
                        activity.TargetResources.Intersect(filteredResources.Select(x => x.Id)));
                    if (intersection.SetEquals(available.Select(x => x.ResourceId.GetValueOrDefault())))
                    {
                        foreach (var r in available) { r.AppendActivity(activity, timeCounter); started.Add(activityId); }
                        return true;
                    }
                }
                else
                {
                    throw new NotImplementedException($@"Unknown TargetResourceOperator value ({activity.TargetResourceOperator})");
                }
            }
            return false;
        }

        #region Scheduling Pipeline Helpers

        // Gathers the set of activities that reference resources not present in filteredResources.
        /// <inheritdoc/>
        public IList<IUnavailableResources<T, TResourceId>> GatherUnavailableResources(
            List<IActivity<T, TResourceId, TWorkStreamId>> activities,
            List<IResource<TResourceId, TWorkStreamId>> filteredResources)
        {
            var output = new List<IUnavailableResources<T, TResourceId>>();
            foreach (IActivity<T, TResourceId, TWorkStreamId> activity in activities)
            {
                if (activity.TargetResources.Count == 0)
                {
                    continue;
                }
                // When all explicit target resources must be available.
                if (activity.TargetResourceOperator == LogicalOperator.AND)
                {
                    // Ids in TargetResources that are not in filtered Resources.
                    IEnumerable<TResourceId> unavailable = activity.TargetResources.Except(filteredResources.Select(x => x.Id));
                    if (unavailable.Any())
                    {
                        output.Add(new UnavailableResources<T, TResourceId>(activity.Id, unavailable));
                    }
                }
                // When at least one explicit target resource must be available.
                else if (activity.TargetResourceOperator == LogicalOperator.OR
                         || activity.TargetResourceOperator == LogicalOperator.ACTIVE_AND)
                {
                    // Check intersection of TargetResources and filtered Resources.
                    IEnumerable<TResourceId> intersection = activity.TargetResources.Intersect(filteredResources.Select(x => x.Id));
                    if (!intersection.Any())
                    {
                        output.Add(new UnavailableResources<T, TResourceId>(activity.Id, activity.TargetResources));
                    }
                }
            }
            return output;
        }

        // Replaces infinite-resource schedules with synthetic resource IDs so that resource-dependency
        // chaining works in the second compile pass.
        /// <inheritdoc/>
        public List<IResourceSchedule<T, TResourceId, TWorkStreamId>> ReplaceWithSyntheticResources(
            List<IResourceSchedule<T, TResourceId, TWorkStreamId>> resourceSchedules)
        {
            // Remember to wipe the resources if we assume infinite resources.
            TResourceId resourceId = default;
            var replacements = new List<IResourceSchedule<T, TResourceId, TWorkStreamId>>();
            foreach (IResourceSchedule<T, TResourceId, TWorkStreamId> schedule in resourceSchedules)
            {
                resourceId = resourceId.Next();
                replacements.Add(new ResourceSchedule<T, TResourceId, TWorkStreamId>(
                    new Resource<TResourceId, TWorkStreamId>(
                        resourceId, null, false, false, InterActivityAllocationType.None, 0.0, 0.0, 0,
                        Enumerable.Empty<TWorkStreamId>()),
                    schedule.ScheduledActivities,
                    schedule.StartTime,
                    schedule.FinishTime,
                    schedule.ResourceAllocation,
                    schedule.CostAllocation,
                    schedule.BillingAllocation,
                    schedule.EffortAllocation,
                    schedule.ActivityAllocation));
            }
            return replacements;
        }

        // Rebuilds resource schedules aligned to CPM-computed EarliestStartTime values.
        // The graph view resolves each scheduled activity by ID.
        /// <inheritdoc/>
        public IEnumerable<IResourceSchedule<T, TResourceId, TWorkStreamId>> RebuildAlignedResourceSchedules(
            List<IResourceSchedule<T, TResourceId, TWorkStreamId>> resourceSchedules,
            bool infiniteResources,
            IResourceSchedulingGraph<T, TResourceId, TWorkStreamId> graph,
            List<IActivity<T, TResourceId, TWorkStreamId>> finalActivities,
            int startTime,
            int finishTime)
        {
            var builders = new List<ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>>();
            foreach (IResourceSchedule<T, TResourceId, TWorkStreamId> oldSchedule in resourceSchedules)
            {
                ResourceScheduleBuilder<T, TResourceId, TWorkStreamId> builder =
                    oldSchedule.Resource == null || infiniteResources
                    ? new ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>()
                    : new ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>(oldSchedule.Resource);

                foreach (IScheduledActivity<T> scheduledActivity in oldSchedule.ScheduledActivities)
                {
                    IActivity<T, TResourceId, TWorkStreamId> activityObj = graph.Activity(scheduledActivity.Id);
                    // This add needs to be without checks because the alignment may not be perfect.
                    builder.AppendActivityWithoutChecks(activityObj, activityObj.EarliestStartTime.GetValueOrDefault());
                }
                builders.Add(builder);
            }
            return builders
                .Select(x => x.ToResourceSchedule(finalActivities, startTime, finishTime))
                .Where(x => x.ScheduledActivities.Any())
                .ToList();
        }

        // Returns schedules for Indirect resources that were not directly assigned any activities.
        /// <inheritdoc/>
        public IEnumerable<IResourceSchedule<T, TResourceId, TWorkStreamId>> CollectIndirectResourceSchedules(
            List<IResource<TResourceId, TWorkStreamId>> filteredResources,
            List<IResourceSchedule<T, TResourceId, TWorkStreamId>> scheduledResources,
            List<IActivity<T, TResourceId, TWorkStreamId>> finalActivities,
            int startTime,
            int finishTime)
        {
            HashSet<TResourceId> scheduledIds = scheduledResources
                .Where(x => x.Resource != null).Select(x => x.Resource!.Id).ToHashSet();
            return filteredResources
                .Where(x => x.InterActivityAllocationType == InterActivityAllocationType.Indirect
                            && !scheduledIds.Contains(x.Id))
                .Select(x => new ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>(x)
                    .ToResourceSchedule(finalActivities, startTime, finishTime))
                .ToList();
        }

        // Returns the set of work-stream phase IDs that appear on at least one resource schedule.
        /// <inheritdoc/>
        public HashSet<TWorkStreamId> GetResourcePhasesUsed(
            List<IResourceSchedule<T, TResourceId, TWorkStreamId>> totalSchedules,
            HashSet<TWorkStreamId> workstreamsUsed)
        {
            HashSet<TWorkStreamId> resourcePhases = totalSchedules
                .Where(x => x.Resource != null).SelectMany(x => x.Resource!.InterActivityPhases).Distinct().ToHashSet();
            return resourcePhases.Intersect(workstreamsUsed).ToHashSet();
        }

        #endregion
    }
}
