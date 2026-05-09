using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    // Stateless priority-list resource scheduler.
    // Receives the priority-ordered activity list and callbacks for graph state —
    // works identically for both Arrow and Vertex builders.
    internal sealed class PriorityListResourceScheduler<T, TResourceId, TWorkStreamId>
        : IResourceSchedulingEngine<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        public IEnumerable<IResourceSchedule<T, TResourceId, TWorkStreamId>> CalculateResourceSchedules(
            IList<T> priorityList,
            IList<IResource<TResourceId, TWorkStreamId>> filteredResources,
            bool infiniteResources,
            Func<T, IActivity<T, TResourceId, TWorkStreamId>> activityLookup,
            Func<T, IList<T>> strongDependencyLookup,
            Func<IEnumerable<IActivity<T, TResourceId, TWorkStreamId>>> finalActivitiesFactory)
        {
            if (priorityList is null) throw new ArgumentNullException(nameof(priorityList));
            if (filteredResources is null) throw new ArgumentNullException(nameof(filteredResources));
            if (activityLookup is null) throw new ArgumentNullException(nameof(activityLookup));
            if (strongDependencyLookup is null) throw new ArgumentNullException(nameof(strongDependencyLookup));
            if (finalActivitiesFactory is null) throw new ArgumentNullException(nameof(finalActivitiesFactory));

            IList<T?> workingList = priorityList.Select(x => new T?(x)).ToList();

            IList<ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>> resourceScheduleBuilders = filteredResources
                .OrderBy(x => x.AllocationOrder)
                .Select(x => new ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>(x))
                .ToList();

            var completed = new HashSet<T>();
            var started = new HashSet<T>();
            var ready = new List<T?>(Enumerable.Repeat((T?)null, workingList.Count));
            int timeCounter = 0;

            while (workingList.Any(x => x.HasValue) || started.Any() || ready.Any(x => x.HasValue))
            {
                AdvanceCompletedActivities(resourceScheduleBuilders, timeCounter, started, completed);
                PromoteReadyActivities(workingList, ready, completed, started, strongDependencyLookup);
                AssignReadyActivitiesToResources(ready, resourceScheduleBuilders, activityLookup, filteredResources,
                    infiniteResources, started, timeCounter);
                timeCounter++;
            }

            IEnumerable<IActivity<T, TResourceId, TWorkStreamId>> finalActivities = finalActivitiesFactory();

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
            IList<ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>> builders,
            int timeCounter,
            HashSet<T> started,
            HashSet<T> completed)
        {
            var running = new HashSet<T>(builders
                .Select(x => x.ActivityAt(timeCounter))
                .Where(x => x.HasValue)
                .Select(x => x.GetValueOrDefault()));

            IList<T> notYetCompleted = started.Intersect(running).ToList();
            started.ExceptWith(notYetCompleted);
            completed.UnionWith(started);
            started.Clear();
            started.UnionWith(notYetCompleted);
        }

        private static void PromoteReadyActivities(
            IList<T?> workingList,
            IList<T?> ready,
            HashSet<T> completed,
            HashSet<T> started,
            Func<T, IList<T>> strongDependencyLookup)
        {
            var indicesToRemove = new HashSet<int>();
            for (int i = 0; i < workingList.Count; i++)
            {
                if (!workingList[i].HasValue) continue;
                T activityId = workingList[i].GetValueOrDefault();
                var directDependencies = new HashSet<T>(strongDependencyLookup(activityId));
                if (directDependencies.IsSubsetOf(completed)
                    && !completed.Contains(activityId)
                    && !started.Contains(activityId))
                {
                    ready[i] = activityId;
                    indicesToRemove.Add(i);
                }
            }
            foreach (int idx in indicesToRemove)
            {
                workingList[idx] = null;
            }
        }

        private static void AssignReadyActivitiesToResources(
            IList<T?> ready,
            IList<ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>> builders,
            Func<T, IActivity<T, TResourceId, TWorkStreamId>> activityLookup,
            IList<IResource<TResourceId, TWorkStreamId>> filteredResources,
            bool infiniteResources,
            HashSet<T> started,
            int timeCounter)
        {
            bool keepLooking = true;
            while (ready.Any(x => x.HasValue) && keepLooking)
            {
                keepLooking = false;
                bool availableBuilderExists = false;

                for (int i = 0; i < ready.Count; i++)
                {
                    if (!ready[i].HasValue) continue;
                    T activityId = ready[i].GetValueOrDefault();
                    IActivity<T, TResourceId, TWorkStreamId> activity = activityLookup(activityId);
                    activity.AllocatedToResources.Clear();

                    bool mustTargetSpecific = !infiniteResources && activity.TargetResources.Any();

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
            IList<ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>> builders,
            IActivity<T, TResourceId, TWorkStreamId> activity,
            T activityId,
            int timeCounter,
            HashSet<T> started,
            ref bool availableBuilderExists)
        {
            foreach (ResourceScheduleBuilder<T, TResourceId, TWorkStreamId> builder in builders)
            {
                if (builder.EarliestAvailableStartTimeForNextActivity > timeCounter) continue;
                availableBuilderExists = true;
                if (builder.IsExplicitTarget) continue;
                if (activity.EarliestStartTime.GetValueOrDefault() > timeCounter) continue;
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
            IList<ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>> builders,
            IActivity<T, TResourceId, TWorkStreamId> activity,
            T activityId,
            IList<IResource<TResourceId, TWorkStreamId>> filteredResources,
            int timeCounter,
            HashSet<T> started,
            ref bool availableBuilderExists)
        {
            var available = new HashSet<ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>>();

            foreach (ResourceScheduleBuilder<T, TResourceId, TWorkStreamId> builder in builders)
            {
                if (builder.EarliestAvailableStartTimeForNextActivity > timeCounter) continue;
                availableBuilderExists = true;

                if (builder.ResourceId != null
                    && !activity.TargetResources.Contains(builder.ResourceId.GetValueOrDefault()))
                {
                    continue;
                }
                if (activity.EarliestStartTime.GetValueOrDefault() > timeCounter) continue;
                if (activity.MaximumLatestFinishTime.HasValue
                    && activity.MaximumLatestFinishTime.GetValueOrDefault() > (timeCounter + activity.Duration))
                {
                    continue;
                }

                if (activity.TargetResourceOperator == LogicalOperator.OR)
                {
                    builder.AppendActivity(activity, timeCounter);
                    started.Add(activityId);
                    return true;
                }
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
                else if (activity.TargetResourceOperator == LogicalOperator.ACTIVE_AND)
                {
                    available.Add(builder);
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
    }
}
