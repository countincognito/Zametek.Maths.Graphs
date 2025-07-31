using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Zametek.Maths.Graphs
{
    public abstract class VertexGraphCompilerBase<T, TResourceId, TWorkStreamId, TDependentActivity, TActivity, TEvent>
        : GraphCompilerBase<T, TResourceId, TWorkStreamId, TEvent, TDependentActivity, TDependentActivity, TEvent>
        where TDependentActivity : IDependentActivity<T, TResourceId, TWorkStreamId>
        where TActivity : IActivity<T, TResourceId, TWorkStreamId>
        where TEvent : IEvent<T>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        #region Fields

        private readonly object m_Lock;
        private readonly VertexGraphBuilderBase<T, TResourceId, TWorkStreamId, TDependentActivity, TEvent> m_VertexGraphBuilder;

        #endregion

        #region Ctors

        protected VertexGraphCompilerBase(VertexGraphBuilderBase<T, TResourceId, TWorkStreamId, TDependentActivity, TEvent> vertexGraphBuilder)
            : base(vertexGraphBuilder)
        {
            m_VertexGraphBuilder = vertexGraphBuilder ?? throw new ArgumentNullException(nameof(vertexGraphBuilder));
            m_Lock = new object();
        }

        #endregion

        #region Public Methods

        //public bool AddActivityDependencies(T activityId, HashSet<T> dependencies)
        //{
        //    lock (m_Lock)
        //    {
        //        if (dependencies is null)
        //        {
        //            throw new ArgumentNullException(nameof(dependencies));
        //        }
        //        if (!m_VertexGraphBuilder.ActivityIds.Contains(activityId))
        //        {
        //            return false;
        //        }
        //        if (!dependencies.Any())
        //        {
        //            return true;
        //        }

        //        var activity = (TDependentActivity)m_VertexGraphBuilder.Activity(activityId);
        //        var resourceAndCompiledDependencies = new HashSet<T>(activity.ResourceDependencies.Intersect(activity.Dependencies));
        //        var resourceOrCompiledDependencies = new HashSet<T>(activity.ResourceDependencies.Union(activity.Dependencies));
        //        var onlyResourceDependencies = new HashSet<T>(activity.ResourceDependencies.Except(resourceAndCompiledDependencies));

        //        // If a dependency is already a compiled dependency, then do nothing.

        //        // If a dependency is already a resource dependency, but not a compiled dependency,
        //        // then just add it to the the compiled dependencies.
        //        var toBeAddedToCompiledDependencies = new HashSet<T>(dependencies.Intersect(onlyResourceDependencies));

        //        foreach (T dependencyId in toBeAddedToCompiledDependencies)
        //        {
        //            activity.Dependencies.Add(dependencyId);
        //        }

        //        // If a dependency is neither a compiled dependency, nor a resource dependency,
        //        // then add it to everything.
        //        var toBeAddedToEverything = new HashSet<T>(dependencies.Except(resourceOrCompiledDependencies));

        //        foreach (T dependencyId in toBeAddedToEverything)
        //        {
        //            activity.Dependencies.Add(dependencyId);
        //        }

        //        return m_VertexGraphBuilder.AddActivityDependencies(activityId, toBeAddedToEverything);
        //    }
        //}

        //public bool RemoveActivityDependencies(T activityId, HashSet<T> dependencies)
        //{
        //    lock (m_Lock)
        //    {
        //        if (dependencies is null)
        //        {
        //            throw new ArgumentNullException(nameof(dependencies));
        //        }
        //        if (!m_VertexGraphBuilder.ActivityIds.Contains(activityId))
        //        {
        //            return false;
        //        }
        //        if (!dependencies.Any())
        //        {
        //            return true;
        //        }

        //        var activity = (TDependentActivity)m_VertexGraphBuilder.Activity(activityId);
        //        var resourceAndCompiledDependencies = new HashSet<T>(activity.ResourceDependencies.Intersect(activity.Dependencies));
        //        var onlyCompiledDependencies = new HashSet<T>(activity.Dependencies.Except(resourceAndCompiledDependencies));

        //        // If a dependency is a resource dependency, but not a compiled dependency,
        //        // then do nothing.

        //        // If a dependency is a resource dependency, and also a compiled dependency,
        //        // then just remove it from the compiled dependencies.
        //        var toBeRemovedFromCompiledDependencies = new HashSet<T>(dependencies.Intersect(resourceAndCompiledDependencies));

        //        foreach (T dependencyId in toBeRemovedFromCompiledDependencies)
        //        {
        //            activity.Dependencies.Remove(dependencyId);
        //        }

        //        // If a dependency is only a compiled dependency, but not a resource dependency,
        //        // then remove it from the compiled dependencies and the graph builder.
        //        var toBeRemovedFromEverything = new HashSet<T>(dependencies.Intersect(onlyCompiledDependencies));

        //        foreach (T dependencyId in toBeRemovedFromEverything)
        //        {
        //            activity.Dependencies.Remove(dependencyId);
        //        }

        //        return m_VertexGraphBuilder.RemoveActivityDependencies(activityId, toBeRemovedFromEverything);
        //    }
        //}

        public bool SetActivityDependencies(T activityId, HashSet<T> dependencies, HashSet<T> planningDependencies)
        {
            lock (m_Lock)
            {
                if (dependencies is null)
                {
                    throw new ArgumentNullException(nameof(dependencies));
                }
                if (planningDependencies is null)
                {
                    throw new ArgumentNullException(nameof(planningDependencies));
                }
                if (!m_VertexGraphBuilder.ActivityIds.Contains(activityId))
                {
                    return false;
                }

                TDependentActivity activity = m_VertexGraphBuilder.Activity(activityId);
                var coreDependencies = new HashSet<T>(activity.Dependencies.Union(activity.PlanningDependencies));

                var resourceAndCompiledDependencies = new HashSet<T>(activity.ResourceDependencies.Intersect(activity.Dependencies));
                var resourceAndPlanningDependencies = new HashSet<T>(activity.ResourceDependencies.Intersect(activity.PlanningDependencies));

                var resourceOrCompiledDependencies = new HashSet<T>(activity.ResourceDependencies.Union(activity.Dependencies));
                var resourceOrPlanningDependencies = new HashSet<T>(activity.ResourceDependencies.Union(activity.PlanningDependencies));

                var compiledNotResourceDependencies = new HashSet<T>(activity.Dependencies.Except(activity.ResourceDependencies));
                var planningNotResourceDependencies = new HashSet<T>(activity.PlanningDependencies.Except(activity.ResourceDependencies));

                var resourceNotCompiledDependencies = new HashSet<T>(activity.ResourceDependencies.Except(activity.Dependencies));
                var resourceNotPlanningDependencies = new HashSet<T>(activity.ResourceDependencies.Except(activity.PlanningDependencies));

                bool successfullyRemoved = true;
                bool successfullyAdded = true;

                // Resource: 1
                //     Core: 0
                //      New: 0
                // If an existing dependency is a resource dependency, but not a core
                // dependency, and is not in the new dependencies, then do nothing.

                // Resource: 0
                //     Core: 1
                //      New: 1
                // If an existing dependency is not a resource dependency, but is a core
                // dependency, and is in the new dependencies, then do nothing.

                // Resource: 1
                //     Core: 1
                //      New: 1
                // If an existing dependency is a resource dependency, and also a core
                // dependency, and is in the new dependencies, then do nothing.

                // Resource: 1
                //     Core: 1
                //      New: 0
                // If an existing dependency is a resource dependency, and also a core
                // dependency, and is not in the new dependencies, then remove it from the
                // core dependencies.

                {
                    IList<T> toBeRemovedFromCompiledDependencies = resourceAndCompiledDependencies.Except(dependencies).ToList();

                    foreach (T dependencyId in toBeRemovedFromCompiledDependencies)
                    {
                        activity.Dependencies.Remove(dependencyId);
                    }

                    IList<T> toBeRemovedFromPlanningDependencies = resourceAndPlanningDependencies.Except(planningDependencies).ToList();

                    foreach (T dependencyId in toBeRemovedFromPlanningDependencies)
                    {
                        activity.PlanningDependencies.Remove(dependencyId);
                    }

                    List<T> updatedDependencies = activity.Dependencies
                        .Union(activity.PlanningDependencies)
                        .Union(activity.ResourceDependencies)
                        .ToList();
                    IList<T> currentDependencies = m_VertexGraphBuilder.ActivityDependencyIds(activityId);

                    var toBeRemoved = new HashSet<T>(currentDependencies
                        .Except(updatedDependencies)
                        .Union(toBeRemovedFromCompiledDependencies)
                        .Union(toBeRemovedFromPlanningDependencies));

                    successfullyRemoved &= m_VertexGraphBuilder.RemoveActivityDependencies(activityId, toBeRemoved);
                }

                // Resource: 1
                //     Core: 0
                //      New: 1
                // If an existing dependency is a resource dependency, but not a core
                // dependency, and is in the new dependencies, then add it to the core
                // dependencies.

                {
                    var toBeAddedToCompiledDependencies = resourceNotCompiledDependencies.Intersect(dependencies);

                    foreach (T dependencyId in toBeAddedToCompiledDependencies)
                    {
                        activity.Dependencies.Add(dependencyId);
                    }

                    var toBeAddedToPlanningDependencies = resourceNotPlanningDependencies.Intersect(planningDependencies);

                    foreach (T dependencyId in toBeAddedToPlanningDependencies)
                    {
                        activity.PlanningDependencies.Add(dependencyId);
                    }

                    List<T> updatedDependencies = activity.Dependencies
                        .Union(activity.PlanningDependencies)
                        .Union(activity.ResourceDependencies)
                        .ToList();
                    IList<T> currentDependencies = m_VertexGraphBuilder.ActivityDependencyIds(activityId);

                    var toBeAdded = new HashSet<T>(updatedDependencies
                        .Except(currentDependencies)
                        .Union(toBeAddedToCompiledDependencies)
                        .Union(toBeAddedToPlanningDependencies));

                    successfullyAdded &= m_VertexGraphBuilder.AddActivityDependencies(activityId, toBeAdded);
                }

                // Resource: 0
                //     Core: 1
                //      New: 0
                // If an existing dependency is not a resource dependency, but is a core
                // dependency, and is not in the new dependencies, then remove it from the
                // core dependencies.

                {
                    var toBeRemovedFromCompiledDependencies = compiledNotResourceDependencies.Except(dependencies);

                    foreach (T dependencyId in toBeRemovedFromCompiledDependencies)
                    {
                        activity.Dependencies.Remove(dependencyId);
                    }

                    var toBeRemovedFromPlanningDependencies = planningNotResourceDependencies.Except(planningDependencies);

                    foreach (T dependencyId in toBeRemovedFromPlanningDependencies)
                    {
                        activity.PlanningDependencies.Remove(dependencyId);
                    }

                    List<T> updatedDependencies = activity.Dependencies
                        .Union(activity.PlanningDependencies)
                        .Union(activity.ResourceDependencies)
                        .ToList();
                    IList<T> currentDependencies = m_VertexGraphBuilder.ActivityDependencyIds(activityId);

                    var toBeRemoved = new HashSet<T>(currentDependencies
                        .Except(updatedDependencies)
                        .Union(toBeRemovedFromCompiledDependencies)
                        .Union(toBeRemovedFromPlanningDependencies));

                    successfullyRemoved &= m_VertexGraphBuilder.RemoveActivityDependencies(activityId, toBeRemoved);
                }

                // Resource: 0
                //     Core: 0
                //      New: X
                // If a new dependency is neither a resource dependency, nor a core
                // dependency, then add it to the core dependencies.

                {
                    var toBeAddedToCompiledDependencies = dependencies.Except(resourceOrCompiledDependencies);

                    foreach (T dependencyId in toBeAddedToCompiledDependencies)
                    {
                        activity.Dependencies.Add(dependencyId);
                    }

                    var toBeAddedToPlanningDependencies = planningDependencies.Except(resourceOrPlanningDependencies);

                    foreach (T dependencyId in toBeAddedToPlanningDependencies)
                    {
                        activity.PlanningDependencies.Add(dependencyId);
                    }

                    List<T> updatedDependencies = activity.Dependencies
                        .Union(activity.PlanningDependencies)
                        .Union(activity.ResourceDependencies)
                        .ToList();
                    IList<T> currentDependencies = m_VertexGraphBuilder.ActivityDependencyIds(activityId);

                    var toBeAdded = new HashSet<T>(updatedDependencies
                        .Except(currentDependencies)
                        .Union(toBeAddedToCompiledDependencies)
                        .Union(toBeAddedToPlanningDependencies));

                    successfullyAdded &= m_VertexGraphBuilder.AddActivityDependencies(activityId, toBeAdded);
                }

                // Final return.
                return successfullyRemoved && successfullyAdded;
            }
        }

        public IGraphCompilation<T, TResourceId, TWorkStreamId, TDependentActivity> Compile()
        {
            return Compile(new List<IResource<TResourceId, TWorkStreamId>>());
        }

        public IGraphCompilation<T, TResourceId, TWorkStreamId, TDependentActivity> Compile(IList<IResource<TResourceId, TWorkStreamId>> resources)
        {
            return Compile(resources, new List<IWorkStream<TWorkStreamId>>());
        }

        public IGraphCompilation<T, TResourceId, TWorkStreamId, TDependentActivity> Compile(
            IList<IResource<TResourceId, TWorkStreamId>> resources,
            IList<IWorkStream<TWorkStreamId>> workStreams)
        {
            if (resources is null)
            {
                throw new ArgumentNullException(nameof(resources));
            }
            if (workStreams is null)
            {
                throw new ArgumentNullException(nameof(workStreams));
            }

            lock (m_Lock)
            {
                IEnumerable<TDependentActivity> activities = m_VertexGraphBuilder.Activities;

                // Reset activity dependencies in the graph to match only the compiled and planning dependencies
                // (i.e. remove any that are *only* resource dependencies).
                foreach (TDependentActivity activity in activities)
                {
                    IEnumerable<T> coreDependencies = activity.Dependencies.Union(activity.PlanningDependencies);
                    m_VertexGraphBuilder.RemoveActivityDependencies(
                        activity.Id,
                        new HashSet<T>(activity.ResourceDependencies.Except(coreDependencies)));
                    activity.ResourceDependencies.Clear();
                    activity.AllocatedToResources.Clear();
                }

                // Sanity check the graph data.
                IEnumerable<T> invalidDependencies = m_VertexGraphBuilder.InvalidDependencies;
                IEnumerable<ICircularDependency<T>> circularDependencies = m_VertexGraphBuilder.FindStrongCircularDependencies();
                IEnumerable<IInvalidConstraint<T>> invalidPrecompilationConstraints = m_VertexGraphBuilder.FindInvalidPreCompilationConstraints();

                // If resources are 0, assume infinite resources.
                bool infiniteResources = resources.Count == 0;

                // Filter out disabled resources.
                IList<IResource<TResourceId, TWorkStreamId>> filteredResources = resources.Where(x => !x.IsInactive).ToList();

                // Sanity check the resources.
                bool allResourcesExplicitTargetsButNotAllActivitiesTargeted =
                    !infiniteResources
                    && filteredResources.All(x => x.IsExplicitTarget)
                    && m_VertexGraphBuilder.Activities.Any(x => !x.IsDummy && x.TargetResources.Count == 0);

                // Check if any activities are obliged to use only explicit target resources
                // that are unavailable.
                var unavailableResourcesSet = new List<IUnavailableResources<T, TResourceId>>();

                if (!infiniteResources)
                {
                    foreach (TDependentActivity dependentActivity in activities)
                    {
                        if (dependentActivity.TargetResources.Count != 0)
                        {
                            // When all explicit target resources must be available.
                            if (dependentActivity.TargetResourceOperator == LogicalOperator.AND)
                            {
                                // Ids in TargetResources that are not in filtered Resources.
                                IEnumerable<TResourceId> unavailableResourceIds =
                                    dependentActivity.TargetResources.Except(filteredResources.Select(x => x.Id));

                                if (unavailableResourceIds.Any())
                                {
                                    unavailableResourcesSet.Add(
                                        new UnavailableResources<T, TResourceId>(dependentActivity.Id, unavailableResourceIds));
                                }
                            }
                            // When at least one explicit target resource must be available.
                            else if (dependentActivity.TargetResourceOperator == LogicalOperator.OR)
                            {
                                // Check intersection of TargetResources and filtered Resources.
                                IEnumerable<TResourceId> intersection =
                                    dependentActivity.TargetResources.Intersect(filteredResources.Select(x => x.Id));

                                if (!intersection.Any())
                                {
                                    unavailableResourcesSet.Add(
                                        new UnavailableResources<T, TResourceId>(dependentActivity.Id, dependentActivity.TargetResources));
                                }
                            }
                        }
                    }
                }

                // Collate pre-compilation errors, if any exist.

                var compilationErrors = new List<GraphCompilationError>();

                // P0010
                if (invalidDependencies.Any())
                {
                    compilationErrors.Add(
                        new GraphCompilationError(
                            GraphCompilationErrorCode.P0010,
                            BuildInvalidDependenciesErrorMessage(invalidDependencies, activities)));
                }

                // P0020
                if (circularDependencies.Any())
                {
                    compilationErrors.Add(
                        new GraphCompilationError(
                            GraphCompilationErrorCode.P0020,
                            BuildCircularDependenciesErrorMessage(circularDependencies)));
                }

                // P0030
                if (invalidPrecompilationConstraints.Any())
                {
                    compilationErrors.Add(
                        new GraphCompilationError(
                            GraphCompilationErrorCode.P0030,
                            BuildInvalidConstraintsErrorMessage(invalidPrecompilationConstraints)));
                }

                // P0040
                if (allResourcesExplicitTargetsButNotAllActivitiesTargeted)
                {
                    compilationErrors.Add(
                        new GraphCompilationError(
                            GraphCompilationErrorCode.P0040,
                            $@"{Properties.Resources.Message_AllResourcesExplicitTargetsNotAllActivitiesTargeted}{Environment.NewLine}"));
                }

                // P0050
                if (!m_VertexGraphBuilder.CleanUpEdges())
                {
                    compilationErrors.Add(
                        new GraphCompilationError(
                            GraphCompilationErrorCode.P0050,
                            $@"{Properties.Resources.Message_UnableToRemoveUnnecessaryEdges}{Environment.NewLine}"));
                }

                // P0060
                if (unavailableResourcesSet.Count != 0)
                {
                    compilationErrors.Add(
                        new GraphCompilationError(
                            GraphCompilationErrorCode.P0060,
                            BuildUnavailableResourcesErrorMessage(unavailableResourcesSet)));
                }

                if (compilationErrors.Count != 0)
                {
                    return new GraphCompilation<T, TResourceId, TWorkStreamId, TDependentActivity>(
                        m_VertexGraphBuilder.Activities.Select(x => (TDependentActivity)x.CloneObject()),
                        Enumerable.Empty<IResourceSchedule<T, TResourceId, TWorkStreamId>>(),
                        Enumerable.Empty<IWorkStream<TWorkStreamId>>(),
                        compilationErrors);
                }

                // Perform first compilation and calculate resource schedules.
                m_VertexGraphBuilder.CalculateCriticalPath();
                List<IResourceSchedule<T, TResourceId, TWorkStreamId>> resourceSchedules = m_VertexGraphBuilder
                    .CalculateResourceSchedulesByPriorityList(filteredResources)
                    .ToList();

                // If the previous calculation was performed with infinite resources, then it will not be possible
                // to handle resource dependencies. So here we need to create fake resources for resource dependencies
                // to work in the next step.
                if (infiniteResources)
                {
                    TResourceId resourceId = default;

                    var replacementResourceSchedules = new List<IResourceSchedule<T, TResourceId, TWorkStreamId>>();

                    foreach (IResourceSchedule<T, TResourceId, TWorkStreamId> resourceSchedule in resourceSchedules)
                    {
                        resourceId = resourceId.Next();

                        replacementResourceSchedules.Add(
                            new ResourceSchedule<T, TResourceId, TWorkStreamId>(
                                new Resource<TResourceId, TWorkStreamId>(
                                    resourceId,
                                    null,
                                    false,
                                    false,
                                    InterActivityAllocationType.None,
                                    0.0,
                                    0.0,
                                    0,
                                    Enumerable.Empty<TWorkStreamId>()),
                                resourceSchedule.ScheduledActivities,
                                resourceSchedule.StartTime,
                                resourceSchedule.FinishTime,
                                resourceSchedule.ActivityAllocation,
                                resourceSchedule.CostAllocation,
                                resourceSchedule.BillingAllocation,
                                resourceSchedule.EffortAllocation)
                            );
                    }

                    resourceSchedules.Clear();
                    resourceSchedules.AddRange(replacementResourceSchedules);
                }

                {
                    // Determine the resource dependencies and add them to the compiled dependencies.
                    foreach (IResourceSchedule<T, TResourceId, TWorkStreamId> resourceSchedule in resourceSchedules)
                    {
                        T previousId = default;
                        bool first = true;
                        IResource<TResourceId, TWorkStreamId> resource = resourceSchedule.Resource;

                        foreach (IScheduledActivity<T> scheduledActivity in resourceSchedule.ScheduledActivities.OrderBy(x => x.StartTime))
                        {
                            T currentId = scheduledActivity.Id;
                            TDependentActivity activity = m_VertexGraphBuilder.Activity(currentId);

                            if (resource != null)
                            {
                                activity.AllocatedToResources.Add(resource.Id);
                            }

                            if (!first)
                            {
                                // Here we add the previous activity ID to the set of resource
                                // dependencies attached to the activity itself. However, we do
                                // not add it to the compiled and planning dependencies sets.
                                // Instead we make the change directly to graph data (which we
                                // reverse below - see just before post-compilation error collation).

                                activity.ResourceDependencies.Add(previousId);
                                IEnumerable<T> coreDependencies = activity.Dependencies.Union(activity.PlanningDependencies);
                                m_VertexGraphBuilder.AddActivityDependencies(
                                    currentId,
                                    new HashSet<T>(activity.ResourceDependencies.Except(coreDependencies)));
                            }

                            first = false;
                            previousId = scheduledActivity.Id;
                        }
                    }

                    // Rerun the compilation with the new dependencies.
                    m_VertexGraphBuilder.CalculateCriticalPath();
                }

                // At this point, all nodes should have finish times, except the
                // Isolated nodes. So we need to fix that.

                if (!m_VertexGraphBuilder.BackFillIsolatedNodes())
                {
                    throw new InvalidOperationException(Properties.Resources.Message_CannotBackFillIsolatedNodes);
                }

                // Clear up activity dependencies in the graph to match only the compiled and planning dependencies
                // (i.e. remove any that are *only* resource dependencies).
                foreach (TDependentActivity activity in activities)
                {
                    IEnumerable<T> coreDependencies = activity.Dependencies.Union(activity.PlanningDependencies);
                    m_VertexGraphBuilder.RemoveActivityDependencies(
                        activity.Id,
                        new HashSet<T>(activity.ResourceDependencies.Except(coreDependencies)));
                }

                // Collate post-compilation errors, if any exist.

                IEnumerable<IInvalidConstraint<T>> invalidPostcompilationConstraints = m_VertexGraphBuilder.FindInvalidPostCompilationConstraints();

                // C0010
                if (invalidPostcompilationConstraints.Any())
                {
                    compilationErrors.Add(
                        new GraphCompilationError(
                            GraphCompilationErrorCode.C0010,
                            BuildInvalidConstraintsErrorMessage(invalidPostcompilationConstraints)));
                }

                //if (compilationErrors.Count != 0)
                //{
                //    return new GraphCompilation<T, TResourceId, TWorkStreamId, TDependentActivity>(
                //        m_VertexGraphBuilder.Activities.Select(x => (TDependentActivity)x.CloneObject()),
                //        Enumerable.Empty<IResourceSchedule<T, TResourceId, TWorkStreamId>>(),
                //        Enumerable.Empty<IWorkStream<TWorkStreamId>>(),
                //        compilationErrors);
                //}

                // Go through each activity and update the upstream successors.

                foreach (TDependentActivity activity in activities)
                {
                    T activityId = activity.Id;
                    activity.Successors.Clear();

                    Node<T, TDependentActivity> node = m_VertexGraphBuilder.Node(activityId);

                    if (node.NodeType == NodeType.Start || node.NodeType == NodeType.Normal)
                    {
                        // Get the outgoing edges and the successor nodes IDs.
                        IEnumerable<T> successorNodeIds = node.OutgoingEdges
                            .Select(m_VertexGraphBuilder.EdgeHeadNode)
                            .Select(x => x.Id)
                            .ToList();

                        activity.Successors.UnionWith(successorNodeIds);
                    }
                }

                // Go through each resource schedule and ensure the scheduled activities
                // align with the compiled graph.

                int startTime = m_VertexGraphBuilder.StartTime;
                int finishTime = m_VertexGraphBuilder.FinishTime;
                var newResourceScheduleBuilders = new List<ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>>();

                foreach (IResourceSchedule<T, TResourceId, TWorkStreamId> oldResourceSchedule in resourceSchedules)
                {
                    // Remember to wipe the resources if we assume infinite resources.
                    ResourceScheduleBuilder<T, TResourceId, TWorkStreamId> newResourceScheduleBuilder =
                        oldResourceSchedule.Resource == null || infiniteResources
                        ? new ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>()
                        : new ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>(oldResourceSchedule.Resource);

                    IEnumerable<IScheduledActivity<T>> oldScheduledActivities = oldResourceSchedule.ScheduledActivities;

                    foreach (IScheduledActivity<T> oldScheduledActivity in oldScheduledActivities)
                    {
                        T oldScheduledActivityId = oldScheduledActivity.Id;
                        TDependentActivity activity = m_VertexGraphBuilder.Activity(oldScheduledActivityId);

                        // This add needs to be without checks because the alignment may not be perfect.
                        newResourceScheduleBuilder.AppendActivityWithoutChecks(activity, activity.EarliestStartTime.GetValueOrDefault());
                    }

                    newResourceScheduleBuilders.Add(newResourceScheduleBuilder);
                }

                // We will need a copy of the final activities for the final part.
                IEnumerable<IActivity<T, TResourceId, TWorkStreamId>> finalActivities =
                    m_VertexGraphBuilder.Activities.Select(x => (IActivity<T, TResourceId, TWorkStreamId>)x.CloneObject());

                // Build the resource schedules for any resources with scheduled activities.

                IEnumerable<IResourceSchedule<T, TResourceId, TWorkStreamId>> newResourceSchedules = newResourceScheduleBuilders
                    .Select(x => x.ToResourceSchedule(finalActivities, startTime, finishTime))
                    .Where(x => x.ScheduledActivities.Any())
                    .ToList();

                // Now find any remaining resources that were indirect and create schedules for them.

                HashSet<TResourceId> scheduledResourceIds = newResourceSchedules.Where(x => x.Resource != null).Select(x => x.Resource.Id).ToHashSet();

                IEnumerable<IResource<TResourceId, TWorkStreamId>> remainingIndirectResources = filteredResources
                    .Where(x => x.InterActivityAllocationType == InterActivityAllocationType.Indirect && !scheduledResourceIds.Contains(x.Id));

                var indirectResourceScheduleBuilders = new List<ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>>();

                foreach (IResource<TResourceId, TWorkStreamId> indirectResource in remainingIndirectResources)
                {
                    indirectResourceScheduleBuilders.Add(new ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>(indirectResource));
                }

                IEnumerable<IResourceSchedule<T, TResourceId, TWorkStreamId>> indirectResourceSchedules = indirectResourceScheduleBuilders
                    .Select(x => x.ToResourceSchedule(finalActivities, startTime, finishTime))
                    .ToList();

                // Now calculate the used work streams.

                List<IResourceSchedule<T, TResourceId, TWorkStreamId>> totalResourceSchedules =
                    newResourceSchedules.Union(indirectResourceSchedules).ToList();

                HashSet<TWorkStreamId> workstreamsUsed = finalActivities.SelectMany(x => x.TargetWorkStreams).Distinct().ToHashSet();

                HashSet<TWorkStreamId> resourcePhases = totalResourceSchedules
                    .Where(x => x.Resource != null)
                    .Select(x => x.Resource)
                    .SelectMany(x => x.InterActivityPhases)
                    .Distinct().ToHashSet();

                HashSet<TWorkStreamId> resourcePhasesUsed = resourcePhases.Intersect(workstreamsUsed).ToHashSet();

                // Return the final values.

                return new GraphCompilation<T, TResourceId, TWorkStreamId, TDependentActivity>(
                    m_VertexGraphBuilder.Activities.Select(x => (TDependentActivity)x.CloneObject()),
                    totalResourceSchedules,
                    workStreams.Where(x => resourcePhasesUsed.Contains(x.Id)),
                    compilationErrors);
            }
        }

        #endregion

        #region Private Methods

        private static string BuildInvalidDependenciesErrorMessage(
            IEnumerable<T> invalidDependencies,
            IEnumerable<TDependentActivity> activities)
        {
            if (invalidDependencies == null || !invalidDependencies.Any()
                || activities == null || !activities.Any())
            {
                return string.Empty;
            }
            var output = new StringBuilder();
            output.AppendLine($@"{Properties.Resources.Message_InvalidDependencies}");
            foreach (T invalidDependency in invalidDependencies)
            {
                IList<T> actsWithInvalidDeps = activities
                    .Where(x => x.Dependencies.Union(x.PlanningDependencies).Contains(invalidDependency))
                    .Select(x => x.Id)
                    .OrderBy(x => x)
                    .ToList();
                output.AppendLine($@"{invalidDependency} {Properties.Resources.Message_IsInvalidButReferencedBy} {string.Join(@", ", actsWithInvalidDeps)}");
            }
            return output.ToString();
        }

        private static string BuildCircularDependenciesErrorMessage(IEnumerable<ICircularDependency<T>> circularDependencies)
        {
            if (circularDependencies == null || !circularDependencies.Any())
            {
                return string.Empty;
            }
            var output = new StringBuilder();
            output.AppendLine($@"{Properties.Resources.Message_CircularDependencies}");
            foreach (ICircularDependency<T> circularDependency in circularDependencies)
            {
                output.AppendLine(string.Join(@" -> ", circularDependency.Dependencies));
            }
            return output.ToString();
        }

        private static string BuildInvalidConstraintsErrorMessage(IEnumerable<IInvalidConstraint<T>> invalidConstraints)
        {
            if (invalidConstraints == null || !invalidConstraints.Any())
            {
                return string.Empty;
            }
            var output = new StringBuilder();
            output.AppendLine($@"{Properties.Resources.Message_InvalidConstraints}");
            foreach (IInvalidConstraint<T> invalidConstraint in invalidConstraints)
            {
                output.AppendLine($@"{invalidConstraint.Id} -> {invalidConstraint.Message}");
            }
            return output.ToString();
        }

        private static string BuildUnavailableResourcesErrorMessage(IEnumerable<IUnavailableResources<T, TResourceId>> unavailableResourceSet)
        {
            if (unavailableResourceSet == null || !unavailableResourceSet.Any())
            {
                return string.Empty;
            }
            var output = new StringBuilder();
            output.AppendLine($@"{Properties.Resources.Message_UnavailableResources}");
            foreach (IUnavailableResources<T, TResourceId> unavailableResources in unavailableResourceSet)
            {
                output.AppendLine($@"{unavailableResources.Id} -> {string.Join(@", ", unavailableResources.ResourceIds.OrderBy(x => x))}");
            }
            return output.ToString();
        }

        #endregion

        #region Overrides

        public override bool AddActivity(TDependentActivity activity)
        {
            lock (m_Lock)
            {
                return m_VertexGraphBuilder.AddActivity(
                    activity,
                    new HashSet<T>(activity.Dependencies.Union(activity.PlanningDependencies)));
            }
        }

        public override bool RemoveActivity(T activityId)
        {
            lock (m_Lock)
            {
                {
                    // Clear out the activity from compiled dependencies.
                    IEnumerable<T> dependentActivityIds = m_VertexGraphBuilder
                        .Activities
                        .Where(x => x.Dependencies.Contains(activityId))
                        .Select(x => x.Id);

                    foreach (T dependentActivityId in dependentActivityIds)
                    {
                        var dependentActivity = m_VertexGraphBuilder.Activity(dependentActivityId);
                        dependentActivity.Dependencies.Remove(activityId);
                    }
                }
                {
                    // Clear out the activity from planning dependencies.
                    IEnumerable<T> dependentActivityIds = m_VertexGraphBuilder
                        .Activities
                        .Where(x => x.PlanningDependencies.Contains(activityId))
                        .Select(x => x.Id);

                    foreach (T dependentActivityId in dependentActivityIds)
                    {
                        var dependentActivity = m_VertexGraphBuilder.Activity(dependentActivityId);
                        dependentActivity.PlanningDependencies.Remove(activityId);
                    }
                }

                m_VertexGraphBuilder.Activity(activityId)?.SetAsRemovable();
                return m_VertexGraphBuilder.RemoveActivity(activityId);
            }
        }

        public override void TransitiveReduction()
        {
            lock (m_Lock)
            {
                bool transitivelyReduced = m_VertexGraphBuilder.TransitiveReduction();
                if (!transitivelyReduced)
                {
                    throw new InvalidOperationException(Properties.Resources.Message_CannotPerformTransitiveReduction);
                }

                // Now set the compiled and planning dependencies to match the actual remaining dependencies.
                foreach (T activityId in m_VertexGraphBuilder.ActivityIds)
                {
                    TDependentActivity activity = m_VertexGraphBuilder.Activity(activityId);
                    IList<T> actualDependencyIds = m_VertexGraphBuilder.ActivityDependencyIds(activityId);
                    var remainingCompiledDependencies = new HashSet<T>(activity.Dependencies.Intersect(actualDependencyIds));
                    var remainingPlanningDependencies = new HashSet<T>(activity.PlanningDependencies.Intersect(actualDependencyIds));
                    SetActivityDependencies(activityId, remainingCompiledDependencies, remainingPlanningDependencies);
                }
            }
        }

        #endregion
    }
}
