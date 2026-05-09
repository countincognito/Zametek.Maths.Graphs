using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    // Sealed compiler for Activity-on-Vertex graphs.
    // Thin coordinator: owns the builder + a lock. Every public method delegates to
    // m_VertexGraphBuilder under the lock. No algorithm logic lives here.
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
                lock (m_Lock) { return m_VertexGraphBuilder.StartTime; }
            }
        }

        public int FinishTime
        {
            get
            {
                lock (m_Lock) { return m_VertexGraphBuilder.FinishTime; }
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
                    int extraNodes = 2;
                    int extraEdges = m_VertexGraphBuilder.StartNodes.Count() + m_VertexGraphBuilder.EndNodes.Count();
                    int isolatedNodeCount = m_VertexGraphBuilder.IsolatedNodes.Count();
                    return (edgeCount + extraEdges) - (nodeCount + extraNodes) + 2 * (1 + isolatedNodeCount);
                }
            }
        }

        internal VertexGraphBuilder<T, TResourceId, TWorkStreamId, TDependentActivity> Builder
        {
            get
            {
                lock (m_Lock) { return m_VertexGraphBuilder; }
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
            lock (m_Lock) { m_VertexGraphBuilder.Reset(); }
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
                    IEnumerable<T> dependentActivityIds = m_VertexGraphBuilder
                        .Activities.Where(x => x.Dependencies.Contains(activityId)).Select(x => x.Id);
                    foreach (T dependentActivityId in dependentActivityIds)
                        m_VertexGraphBuilder.Activity(dependentActivityId).Dependencies.Remove(activityId);
                }
                {
                    IEnumerable<T> dependentActivityIds = m_VertexGraphBuilder
                        .Activities.Where(x => x.PlanningDependencies.Contains(activityId)).Select(x => x.Id);
                    foreach (T dependentActivityId in dependentActivityIds)
                        m_VertexGraphBuilder.Activity(dependentActivityId).PlanningDependencies.Remove(activityId);
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
                    throw new InvalidOperationException(Properties.Resources.Message_CannotPerformTransitiveReduction);

                foreach (T activityId in m_VertexGraphBuilder.ActivityIds)
                {
                    TDependentActivity activity = m_VertexGraphBuilder.Activity(activityId);
                    IList<T> actualDependencyIds = m_VertexGraphBuilder.ActivityDependencyIds(activityId);
                    var remainingCompiledDependencies = new HashSet<T>(activity.Dependencies.Intersect(actualDependencyIds));
                    var remainingPlanningDependencies = new HashSet<T>(activity.PlanningDependencies.Intersect(actualDependencyIds));
                    m_VertexGraphBuilder.SetActivityDependencies(activityId, remainingCompiledDependencies, remainingPlanningDependencies);
                }
            }
        }

        public Graph<T, IEvent<T>, TDependentActivity> ToGraph()
        {
            lock (m_Lock) { return m_VertexGraphBuilder.ToGraph(); }
        }

        public bool SetActivityDependencies(T activityId, HashSet<T> dependencies, HashSet<T> planningDependencies)
        {
            lock (m_Lock)
            {
                return m_VertexGraphBuilder.SetActivityDependencies(activityId, dependencies, planningDependencies);
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
            if (resources is null) throw new ArgumentNullException(nameof(resources));
            if (workStreams is null) throw new ArgumentNullException(nameof(workStreams));

            lock (m_Lock)
            {
                IEnumerable<TDependentActivity> activities = m_VertexGraphBuilder.Activities;
                m_VertexGraphBuilder.ResetResourceState(activities);

                IEnumerable<T> invalidDependencies = m_VertexGraphBuilder.InvalidDependencies;
                IEnumerable<ICircularDependency<T>> circularDependencies = m_VertexGraphBuilder.FindStrongCircularDependencies();
                IEnumerable<IInvalidConstraint<T>> invalidPrecompilationConstraints = m_VertexGraphBuilder.FindInvalidPreCompilationConstraints();

                bool infiniteResources = resources.Count == 0;
                IList<IResource<TResourceId, TWorkStreamId>> filteredResources = resources.Where(x => !x.IsInactive).ToList();

                bool allResourcesExplicitTargetsButNotAllActivitiesTargeted =
                    !infiniteResources
                    && filteredResources.All(x => x.IsExplicitTarget)
                    && m_VertexGraphBuilder.Activities.Any(x => !x.IsDummy && x.TargetResources.Count == 0);

                IList<IUnavailableResources<T, TResourceId>> unavailableResourcesSet =
                    infiniteResources
                    ? new List<IUnavailableResources<T, TResourceId>>()
                    : GatherUnavailableResources(activities, filteredResources);

                var compilationErrors = new List<GraphCompilationError>();
                m_VertexGraphBuilder.AddPreCompilationErrors(compilationErrors, invalidDependencies, activities, circularDependencies,
                    invalidPrecompilationConstraints, allResourcesExplicitTargetsButNotAllActivitiesTargeted,
                    unavailableResourcesSet);

                if (compilationErrors.Count != 0)
                {
                    return new GraphCompilation<T, TResourceId, TWorkStreamId, TDependentActivity>(
                        m_VertexGraphBuilder.Activities.Select(x => (TDependentActivity)x.CloneObject()),
                        Enumerable.Empty<IResourceSchedule<T, TResourceId, TWorkStreamId>>(),
                        Enumerable.Empty<IWorkStream<TWorkStreamId>>(),
                        compilationErrors);
                }

                m_VertexGraphBuilder.CalculateCriticalPath();
                List<IResourceSchedule<T, TResourceId, TWorkStreamId>> resourceSchedules = m_VertexGraphBuilder
                    .CalculateResourceSchedulesByPriorityList(filteredResources)
                    .ToList();

                if (infiniteResources)
                    resourceSchedules = ReplaceWithSyntheticResources(resourceSchedules);

                m_VertexGraphBuilder.AssignResourceDependencies(resourceSchedules);
                m_VertexGraphBuilder.CalculateCriticalPath();

                if (!m_VertexGraphBuilder.BackFillIsolatedNodes())
                    throw new InvalidOperationException(Properties.Resources.Message_CannotBackFillIsolatedNodes);

                m_VertexGraphBuilder.RemoveResourceOnlyDependencies(activities);

                IEnumerable<IInvalidConstraint<T>> invalidPostcompilationConstraints =
                    m_VertexGraphBuilder.FindInvalidPostCompilationConstraints();

                if (invalidPostcompilationConstraints.Any())
                {
                    compilationErrors.Add(new GraphCompilationError(
                        GraphCompilationErrorCode.C0010,
                        BuildInvalidConstraintsErrorMessage(invalidPostcompilationConstraints)));
                }

                m_VertexGraphBuilder.UpdateActivitySuccessors(activities);

                IEnumerable<IActivity<T, TResourceId, TWorkStreamId>> finalActivities =
                    m_VertexGraphBuilder.Activities.Select(x => (IActivity<T, TResourceId, TWorkStreamId>)x.CloneObject());

                int startTime = m_VertexGraphBuilder.StartTime;
                int finishTime = m_VertexGraphBuilder.FinishTime;

                IEnumerable<IResourceSchedule<T, TResourceId, TWorkStreamId>> newResourceSchedules =
                    RebuildAlignedResourceSchedules(resourceSchedules, infiniteResources, finalActivities, startTime, finishTime);

                IEnumerable<IResourceSchedule<T, TResourceId, TWorkStreamId>> indirectResourceSchedules =
                    CollectIndirectResourceSchedules(filteredResources, newResourceSchedules, finalActivities, startTime, finishTime);

                List<IResourceSchedule<T, TResourceId, TWorkStreamId>> totalResourceSchedules =
                    newResourceSchedules.Union(indirectResourceSchedules).ToList();

                HashSet<TWorkStreamId> workstreamsUsed = finalActivities.SelectMany(x => x.TargetWorkStreams).Distinct().ToHashSet();
                HashSet<TWorkStreamId> resourcePhasesUsed = GetResourcePhasesUsed(totalResourceSchedules, workstreamsUsed);

                return new GraphCompilation<T, TResourceId, TWorkStreamId, TDependentActivity>(
                    m_VertexGraphBuilder.Activities.Select(x => (TDependentActivity)x.CloneObject()),
                    totalResourceSchedules,
                    workStreams.Where(x => resourcePhasesUsed.Contains(x.Id)),
                    compilationErrors);
            }
        }

        #endregion

        #region Private Static Scheduling Helpers

        private static IList<IUnavailableResources<T, TResourceId>> GatherUnavailableResources(
            IEnumerable<TDependentActivity> activities,
            IList<IResource<TResourceId, TWorkStreamId>> filteredResources)
        {
            var output = new List<IUnavailableResources<T, TResourceId>>();
            foreach (TDependentActivity activity in activities)
            {
                if (activity.TargetResources.Count == 0) continue;
                if (activity.TargetResourceOperator == LogicalOperator.AND)
                {
                    IEnumerable<TResourceId> unavailable = activity.TargetResources.Except(filteredResources.Select(x => x.Id));
                    if (unavailable.Any()) output.Add(new UnavailableResources<T, TResourceId>(activity.Id, unavailable));
                }
                else if (activity.TargetResourceOperator == LogicalOperator.OR
                         || activity.TargetResourceOperator == LogicalOperator.ACTIVE_AND)
                {
                    IEnumerable<TResourceId> intersection = activity.TargetResources.Intersect(filteredResources.Select(x => x.Id));
                    if (!intersection.Any()) output.Add(new UnavailableResources<T, TResourceId>(activity.Id, activity.TargetResources));
                }
            }
            return output;
        }

        private static List<IResourceSchedule<T, TResourceId, TWorkStreamId>> ReplaceWithSyntheticResources(
            List<IResourceSchedule<T, TResourceId, TWorkStreamId>> resourceSchedules)
        {
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
                    schedule.ActivityAllocation,
                    schedule.CostAllocation,
                    schedule.BillingAllocation,
                    schedule.EffortAllocation));
            }
            return replacements;
        }

        private IEnumerable<IResourceSchedule<T, TResourceId, TWorkStreamId>> RebuildAlignedResourceSchedules(
            IList<IResourceSchedule<T, TResourceId, TWorkStreamId>> resourceSchedules,
            bool infiniteResources,
            IEnumerable<IActivity<T, TResourceId, TWorkStreamId>> finalActivities,
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
                    TDependentActivity activity = m_VertexGraphBuilder.Activity(scheduledActivity.Id);
                    builder.AppendActivityWithoutChecks(activity, activity.EarliestStartTime.GetValueOrDefault());
                }
                builders.Add(builder);
            }
            return builders
                .Select(x => x.ToResourceSchedule(finalActivities, startTime, finishTime))
                .Where(x => x.ScheduledActivities.Any())
                .ToList();
        }

        private static IEnumerable<IResourceSchedule<T, TResourceId, TWorkStreamId>> CollectIndirectResourceSchedules(
            IList<IResource<TResourceId, TWorkStreamId>> filteredResources,
            IEnumerable<IResourceSchedule<T, TResourceId, TWorkStreamId>> scheduledResources,
            IEnumerable<IActivity<T, TResourceId, TWorkStreamId>> finalActivities,
            int startTime,
            int finishTime)
        {
            HashSet<TResourceId> scheduledIds = scheduledResources
                .Where(x => x.Resource != null).Select(x => x.Resource.Id).ToHashSet();
            return filteredResources
                .Where(x => x.InterActivityAllocationType == InterActivityAllocationType.Indirect
                            && !scheduledIds.Contains(x.Id))
                .Select(x => new ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>(x)
                    .ToResourceSchedule(finalActivities, startTime, finishTime))
                .ToList();
        }

        private static HashSet<TWorkStreamId> GetResourcePhasesUsed(
            IEnumerable<IResourceSchedule<T, TResourceId, TWorkStreamId>> totalSchedules,
            HashSet<TWorkStreamId> workstreamsUsed)
        {
            HashSet<TWorkStreamId> resourcePhases = totalSchedules
                .Where(x => x.Resource != null).SelectMany(x => x.Resource.InterActivityPhases).Distinct().ToHashSet();
            return resourcePhases.Intersect(workstreamsUsed).ToHashSet();
        }

        private static string BuildInvalidConstraintsErrorMessage(IEnumerable<IInvalidConstraint<T>> invalidConstraints)
        {
            if (invalidConstraints == null || !invalidConstraints.Any()) return string.Empty;
            var output = new System.Text.StringBuilder();
            output.AppendLine($@"{Properties.Resources.Message_InvalidConstraints}");
            foreach (IInvalidConstraint<T> invalidConstraint in invalidConstraints)
                output.AppendLine($@"{invalidConstraint.Id} -> {invalidConstraint.Message}");
            return output.ToString();
        }

        #endregion
    }
}
