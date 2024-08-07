﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace Zametek.Maths.Graphs
{
    internal static class GraphBuilderExtensions
    {
        internal static void ClearCriticalPathVariables<T, TResourceId, TWorkStreamId, TEdgeContent, TNodeContent, TActivity, TEvent>
            (this GraphBuilderBase<T, TResourceId, TWorkStreamId, TEdgeContent, TNodeContent, TActivity, TEvent> graphBuilder)
            where T : struct, IComparable<T>, IEquatable<T>
            where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
            where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
            where TEdgeContent : IHaveId<T>, ICloneObject
            where TNodeContent : IHaveId<T>, ICloneObject
            where TActivity : IActivity<T, TResourceId, TWorkStreamId>
            where TEvent : IEvent<T>
        {
            if (graphBuilder is null)
            {
                throw new ArgumentNullException(nameof(graphBuilder));
            }
            foreach (TActivity tmpActivity in graphBuilder.Activities)
            {
                tmpActivity.FreeSlack = null;
                tmpActivity.EarliestStartTime = null;
                tmpActivity.LatestFinishTime = null;
            }
            foreach (TEvent tmpEvent in graphBuilder.Events)
            {
                tmpEvent.EarliestFinishTime = null;
                tmpEvent.LatestFinishTime = null;
            }
        }

        internal static IList<T> CalculateCriticalPathPriorityList<T, TResourceId, TWorkStreamId, TEdgeContent, TNodeContent, TActivity, TEvent>
            (this GraphBuilderBase<T, TResourceId, TWorkStreamId, TEdgeContent, TNodeContent, TActivity, TEvent> graphBuilder)
            where T : struct, IComparable<T>, IEquatable<T>
            where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
            where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
            where TEdgeContent : IHaveId<T>, ICloneObject
            where TNodeContent : IHaveId<T>, ICloneObject
            where TActivity : IActivity<T, TResourceId, TWorkStreamId>
            where TEvent : IEvent<T>
        {
            if (graphBuilder is null)
            {
                throw new ArgumentNullException(nameof(graphBuilder));
            }
            var tmpGraphBuilder = (GraphBuilderBase<T, TResourceId, TWorkStreamId, TEdgeContent, TNodeContent, TActivity, TEvent>)graphBuilder.CloneObject();
            var priorityList = new List<T>();
            bool cont = true;
            while (cont)
            {
                tmpGraphBuilder.CalculateCriticalPath();

                // Get the critical path in order of earliest start time.
                int minFloat = tmpGraphBuilder.Activities
                    .Where(x => !x.IsDummy && x.TotalSlack.HasValue)
                    .Select(x => x.TotalSlack.Value)
                    .DefaultIfEmpty()
                    .Min();

                IList<T> criticalActivityIds =
                    tmpGraphBuilder.Activities
                    .Where(x => x.TotalSlack == minFloat && !x.IsDummy)
                    .OrderBy(x => x.EarliestStartTime)
                    .Select(x => x.Id)
                    .ToList();

                if (criticalActivityIds.Any())
                {
                    T criticalActivityId = criticalActivityIds.First();
                    priorityList.Add(criticalActivityId);

                    // Set the processed activity to dummy.
                    tmpGraphBuilder.Activity(criticalActivityId).Duration = 0;
                }
                else
                {
                    cont = false;
                }
            }
            if (tmpGraphBuilder.Activities.Any(x => !x.IsDummy))
            {
                throw new InvalidOperationException(Properties.Resources.Message_CannotCalculateCriticalPathPriorityList);
            }
            return priorityList;
        }

        internal static IEnumerable<IResourceSchedule<T, TResourceId, TWorkStreamId>> CalculateResourceSchedulesByPriorityList<T, TResourceId, TWorkStreamId, TEdgeContent, TNodeContent, TActivity, TEvent>
            (this GraphBuilderBase<T, TResourceId, TWorkStreamId, TEdgeContent, TNodeContent, TActivity, TEvent> graphBuilder, IList<IResource<TResourceId, TWorkStreamId>> resources)
            where T : struct, IComparable<T>, IEquatable<T>
            where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
            where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
            where TEdgeContent : IHaveId<T>, ICloneObject
            where TNodeContent : IHaveId<T>, ICloneObject
            where TActivity : IActivity<T, TResourceId, TWorkStreamId>
            where TEvent : IEvent<T>
        {
            if (graphBuilder is null)
            {
                throw new ArgumentNullException(nameof(graphBuilder));
            }
            if (resources is null)
            {
                throw new ArgumentNullException(nameof(resources));
            }
            if (resources.Count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(resources), Properties.Resources.Message_ValueCannotBeNegative);
            }
            if (!graphBuilder.Activities.Any())
            {
                return Enumerable.Empty<IResourceSchedule<T, TResourceId, TWorkStreamId>>();
            }

            // If resources are 0, assume infinite.
            bool infiniteResources = !resources.Any();

            // Filter out inactive resources.
            IList<IResource<TResourceId, TWorkStreamId>> filteredResources = resources.Where(x => !x.IsInactive).ToList();

            // If resources are limited, check to make sure all activities can be accepted.
            if (!infiniteResources)
            {
                // Check if any activities are obliged to use only explicit target resources
                // that are unavailable.
                var unavailableResourcesSet = new List<IUnavailableResources<T, TResourceId>>();

                foreach (TActivity activity in graphBuilder.Activities)
                {
                    if (activity.TargetResources.Any())
                    {
                        // When all explicit target resources must be available.
                        if (activity.TargetResourceOperator == LogicalOperator.AND)
                        {
                            // Ids in TargetResources that are not in filtered Resources.
                            IEnumerable<TResourceId> unavailableResourceIds =
                                activity.TargetResources.Except(filteredResources.Select(x => x.Id));

                            if (unavailableResourceIds.Any())
                            {
                                unavailableResourcesSet.Add(
                                    new UnavailableResources<T, TResourceId>(activity.Id, unavailableResourceIds));
                            }
                        }
                        // When at least one explicit target resource must be available.
                        else if (activity.TargetResourceOperator == LogicalOperator.OR)
                        {
                            // Check intersection of TargetResources and filtered Resources.
                            IEnumerable<TResourceId> intersection =
                                activity.TargetResources.Intersect(filteredResources.Select(x => x.Id));

                            if (!intersection.Any())
                            {
                                unavailableResourcesSet.Add(
                                    new UnavailableResources<T, TResourceId>(activity.Id, activity.TargetResources));
                            }
                        }
                    }
                }

                if (unavailableResourcesSet.Any())
                {
                    throw new InvalidOperationException(Properties.Resources.Message_AtLeastOneOfSpecifiedTargetResourcesAreNotAvailableInResourcesProvided);
                }

                // If all resources are explicit targets, check to make sure all activities
                // targeted to at least one.
                bool allResourcesAreExplicitTargets = filteredResources.All(x => x.IsExplicitTarget);
                bool atLeastOneActivityRequiresNonExplicitTargetResource = graphBuilder.Activities.Any(x => !x.IsDummy && !x.TargetResources.Any());
                if (allResourcesAreExplicitTargets
                    && atLeastOneActivityRequiresNonExplicitTargetResource)
                {
                    throw new InvalidOperationException(Properties.Resources.Message_AtLeastOneActivityRequiresNonExplicitTargetResourceButAllProvidedResourcesAreExplicitTargets);
                }
            }

            var tmpGraphBuilder = (GraphBuilderBase<T, TResourceId, TWorkStreamId, TEdgeContent, TNodeContent, TActivity, TEvent>)graphBuilder.CloneObject();

            IList<T?> priorityList = tmpGraphBuilder
                .CalculateCriticalPathPriorityList()
                .Select(x => new T?(x))
                .ToList();

            IList<ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>> resourceScheduleBuilders = filteredResources
                .OrderBy(x => x.AllocationOrder)
                .Select(x => new ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>(x))
                .ToList();

            var completed = new HashSet<T>();
            var started = new HashSet<T>();
            var ready = new List<T?>(Enumerable.Repeat((T?)null, priorityList.Count));
            int timeCounter = 0;
            while (priorityList.Any(x => x.HasValue) || started.Any() || ready.Any(x => x.HasValue))
            {
                // Any started activities that are currently not running must now be completed.
                var running = new HashSet<T>(resourceScheduleBuilders
                    .Select(x => x.ActivityAt(timeCounter))
                    .Where(x => x.HasValue)
                    .Select(x => x.GetValueOrDefault()));
                IList<T> notYetCompleted = started.Intersect(running).ToList();

                // Now work out which of the started jobs are now completed.
                started.ExceptWith(notYetCompleted);
                completed.UnionWith(started);

                // Refresh the started set.
                started.Clear();
                started.UnionWith(notYetCompleted);

                // Get the activities that have completed direct dependencies.
                // Add these to the ready queue since there is nothing preventing them from starting.
                var indicesToRemove = new HashSet<int>();
                for (int activityIndex = 0; activityIndex < priorityList.Count; activityIndex++)
                {
                    T? nullableActivityId = priorityList[activityIndex];
                    if (!nullableActivityId.HasValue)
                    {
                        continue;
                    }
                    T activityId = nullableActivityId.GetValueOrDefault();

                    var directDependencies =
                        new HashSet<T>(tmpGraphBuilder.StrongActivityDependencyIds(activityId));

                    if (directDependencies.IsSubsetOf(completed)
                        && !completed.Contains(activityId)
                        && !started.Contains(activityId))
                    {
                        ready[activityIndex] = activityId;
                        indicesToRemove.Add(activityIndex);
                    }
                }

                // Now clear the activities that have been added to the ready queue.
                foreach (int indexToRemove in indicesToRemove)
                {
                    priorityList[indexToRemove] = null;
                }

                // Cycle through each ready activity and find the first currently available schedule builder.
                bool keepLooking = true;
                while (ready.Any(x => x.HasValue) && keepLooking)
                {
                    keepLooking = false;
                    bool availableResourceScheduleBuilderExists = false;

                    for (int activityIndex = 0; activityIndex < ready.Count; activityIndex++)
                    {
                        T? nullableActivityId = ready[activityIndex];
                        if (!nullableActivityId.HasValue)
                        {
                            continue;
                        }
                        T activityId = nullableActivityId.GetValueOrDefault();
                        IActivity<T, TResourceId, TWorkStreamId> activity = tmpGraphBuilder.Activity(activityId);
                        activity.AllocatedToResources.Clear();

                        // Check to see if the activity has to be targeted to specific resources,
                        // and that this resource is one of those specific targets.
                        bool activityMustBeTargetedToSpecificResource = !infiniteResources && activity.TargetResources.Any();

                        if (!activityMustBeTargetedToSpecificResource)
                        {
                            foreach (ResourceScheduleBuilder<T, TResourceId, TWorkStreamId> resourceScheduleBuilder in resourceScheduleBuilders)
                            {
                                if (resourceScheduleBuilder.EarliestAvailableStartTimeForNextActivity <= timeCounter)
                                {
                                    availableResourceScheduleBuilderExists = true;
                                    if (resourceScheduleBuilder.IsExplicitTarget)
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

                                    resourceScheduleBuilder.AppendActivity(activity, timeCounter);
                                    started.Add(activityId);
                                    keepLooking = true;
                                    ready[activityIndex] = null;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            var availableResourceSchedulers = new HashSet<ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>>();

                            foreach (ResourceScheduleBuilder<T, TResourceId, TWorkStreamId> resourceScheduleBuilder in resourceScheduleBuilders)
                            {
                                if (resourceScheduleBuilder.EarliestAvailableStartTimeForNextActivity <= timeCounter)
                                {
                                    availableResourceScheduleBuilderExists = true;
                                    bool resourceCannotAcceptThisActivity = resourceScheduleBuilder.ResourceId != null
                                        && !activity.TargetResources.Contains(resourceScheduleBuilder.ResourceId.GetValueOrDefault());

                                    if (resourceCannotAcceptThisActivity)
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
                                        resourceScheduleBuilder.AppendActivity(activity, timeCounter);
                                        started.Add(activityId);
                                        keepLooking = true;
                                        ready[activityIndex] = null;
                                        break;
                                    }
                                    // Find all the resources that must accommodate the activity.
                                    else if (activity.TargetResourceOperator == LogicalOperator.AND)
                                    {
                                        availableResourceSchedulers.Add(resourceScheduleBuilder);

                                        var targetResources = new HashSet<TResourceId>(activity.TargetResources);
                                        if (targetResources.SetEquals(availableResourceSchedulers.Select(x => x.ResourceId.GetValueOrDefault())))
                                        {
                                            foreach (ResourceScheduleBuilder<T, TResourceId, TWorkStreamId> availableResourceScheduler in availableResourceSchedulers)
                                            {
                                                availableResourceScheduler.AppendActivity(activity, timeCounter);
                                                started.Add(activityId);
                                            }
                                            keepLooking = true;
                                            ready[activityIndex] = null;
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        throw new NotImplementedException($@"Unknown TargetResourceOperator value ({activity.TargetResourceOperator})");
                                    }
                                }
                            }
                        }
                    }

                    if (infiniteResources
                        && !availableResourceScheduleBuilderExists
                        && !keepLooking)
                    {
                        resourceScheduleBuilders.Add(new ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>());
                        keepLooking = true;
                    }
                }
                timeCounter++;
            }

            // We will need a copy of the final activities for the final part.
            IEnumerable<IActivity<T, TResourceId, TWorkStreamId>> finalActivities =
                graphBuilder.Activities.Select(x => (IActivity<T, TResourceId, TWorkStreamId>)x.CloneObject())
                .ToList();

            int finishTime = resourceScheduleBuilders.Select(x => x.LastActivityFinishTime).DefaultIfEmpty().Max();

            return resourceScheduleBuilders
                .Select(x => x.ToResourceSchedule(finalActivities, finishTime))
                .Where(x => x.ScheduledActivities.Any())
                .ToList();
        }
    }
}
