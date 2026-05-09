using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Zametek.Maths.Graphs
{
    // Sealed compiler for Activity-on-Vertex graphs.
    // Owns the graph builder directly (no inheritance from compiler base classes).
    // The Compile() method performs the full two-pass CPM + resource scheduling pipeline.
    public sealed class VertexGraphCompiler<T, TResourceId, TWorkStreamId, TDependentActivity>
        where TDependentActivity : IDependentActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        #region Fields

        private readonly object m_Lock;
        private readonly VertexGraphBuilder<T, TResourceId, TWorkStreamId, TDependentActivity> m_VertexGraphBuilder;

        #endregion

        #region Ctors

        public VertexGraphCompiler()
        {
            T edgeId = default;
            T nodeId = default;
            m_VertexGraphBuilder = new VertexGraphBuilder<T, TResourceId, TWorkStreamId, TDependentActivity>(
                () => edgeId = edgeId.Previous(),
                () => nodeId = nodeId.Previous());
            m_Lock = new object();
        }

        // Internal constructor for engine injection (testability).
        internal VertexGraphCompiler(VertexGraphBuilder<T, TResourceId, TWorkStreamId, TDependentActivity> vertexGraphBuilder)
        {
            m_VertexGraphBuilder = vertexGraphBuilder ?? throw new ArgumentNullException(nameof(vertexGraphBuilder));
            m_Lock = new object();
        }

        #endregion

        #region Properties

        public int StartTime
        {
            get
            {
                lock (m_Lock)
                {
                    return m_VertexGraphBuilder.StartTime;
                }
            }
        }

        public int FinishTime
        {
            get
            {
                lock (m_Lock)
                {
                    return m_VertexGraphBuilder.FinishTime;
                }
            }
        }

        // https://en.wikipedia.org/wiki/Cyclomatic_complexity
        public int CyclomaticComplexity
        {
            get
            {
                lock (m_Lock)
                {
                    int edgeCount = m_VertexGraphBuilder.Edges.Count();
                    int nodeCount = m_VertexGraphBuilder.Nodes.Count();

                    // Correction factor for multiple entry and exit points.

                    // Artificial Start and End nodes.
                    int extraNodes = 2;
                    // Artificial edges to connect the artificial Start and End nodes.
                    int extraEdges = m_VertexGraphBuilder.StartNodes.Count() + m_VertexGraphBuilder.EndNodes.Count();

                    // Isolated nodes count as separate connected components.
                    int isolatedNodeCount = m_VertexGraphBuilder.IsolatedNodes.Count();

                    int cyclomaticComplexity = (edgeCount + extraEdges) - (nodeCount + extraNodes) + 2 * (1 + isolatedNodeCount);
                    return cyclomaticComplexity;
                }
            }
        }

        internal VertexGraphBuilder<T, TResourceId, TWorkStreamId, TDependentActivity> Builder
        {
            get
            {
                lock (m_Lock)
                {
                    return m_VertexGraphBuilder;
                }
            }
        }

        #endregion

        #region Public Methods

        public T GetNextActivityId()
        {
            lock (m_Lock)
            {
                return m_VertexGraphBuilder.ActivityIds.DefaultIfEmpty().Max().Next();
            }
        }

        public void Reset()
        {
            lock (m_Lock)
            {
                m_VertexGraphBuilder.Reset();
            }
        }

        public bool AddActivity(TDependentActivity activity)
        {
            lock (m_Lock)
            {
                return m_VertexGraphBuilder.AddActivity(
                    activity,
                    new HashSet<T>(activity.Dependencies.Union(activity.PlanningDependencies)));
            }
        }

        public bool RemoveActivity(T activityId)
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

        public void TransitiveReduction()
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

        public Graph<T, IEvent<T>, TDependentActivity> ToGraph()
        {
            lock (m_Lock)
            {
                return m_VertexGraphBuilder.ToGraph();
            }
        }

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

                // Resource: 1, Core: 1, New: 0
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

                // Resource: 1, Core: 0, New: 1
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

                // Resource: 0, Core: 1, New: 0
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

                // Resource: 0, Core: 0, New: X
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
                                IEnumerable<TResourceId> unavailableResourceIds =
                                    dependentActivity.TargetResources.Except(filteredResources.Select(x => x.Id));

                                if (unavailableResourceIds.Any())
                                {
                                    unavailableResourcesSet.Add(
                                        new UnavailableResources<T, TResourceId>(dependentActivity.Id, unavailableResourceIds));
                                }
                            }
                            // When at least one explicit target resource must be available.
                            else if (dependentActivity.TargetResourceOperator == LogicalOperator.OR
                                    || dependentActivity.TargetResourceOperator == LogicalOperator.ACTIVE_AND)
                            {
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

                // Go through each activity and update the upstream successors.

                foreach (TDependentActivity activity in activities)
                {
                    T activityId = activity.Id;
                    activity.Successors.Clear();

                    Node<T, TDependentActivity> node = m_VertexGraphBuilder.Node(activityId);

                    if (node.NodeType == NodeType.Start || node.NodeType == NodeType.Normal)
                    {
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
                    ResourceScheduleBuilder<T, TResourceId, TWorkStreamId> newResourceScheduleBuilder =
                        oldResourceSchedule.Resource == null || infiniteResources
                        ? new ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>()
                        : new ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>(oldResourceSchedule.Resource);

                    IEnumerable<IScheduledActivity<T>> oldScheduledActivities = oldResourceSchedule.ScheduledActivities;

                    foreach (IScheduledActivity<T> oldScheduledActivity in oldScheduledActivities)
                    {
                        T oldScheduledActivityId = oldScheduledActivity.Id;
                        TDependentActivity activity = m_VertexGraphBuilder.Activity(oldScheduledActivityId);

                        newResourceScheduleBuilder.AppendActivityWithoutChecks(activity, activity.EarliestStartTime.GetValueOrDefault());
                    }

                    newResourceScheduleBuilders.Add(newResourceScheduleBuilder);
                }

                IEnumerable<IActivity<T, TResourceId, TWorkStreamId>> finalActivities =
                    m_VertexGraphBuilder.Activities.Select(x => (IActivity<T, TResourceId, TWorkStreamId>)x.CloneObject());

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

                List<IResourceSchedule<T, TResourceId, TWorkStreamId>> totalResourceSchedules =
                    newResourceSchedules.Union(indirectResourceSchedules).ToList();

                HashSet<TWorkStreamId> workstreamsUsed = finalActivities.SelectMany(x => x.TargetWorkStreams).Distinct().ToHashSet();

                HashSet<TWorkStreamId> resourcePhases = totalResourceSchedules
                    .Where(x => x.Resource != null)
                    .Select(x => x.Resource)
                    .SelectMany(x => x.InterActivityPhases)
                    .Distinct().ToHashSet();

                HashSet<TWorkStreamId> resourcePhasesUsed = resourcePhases.Intersect(workstreamsUsed).ToHashSet();

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
    }
}
