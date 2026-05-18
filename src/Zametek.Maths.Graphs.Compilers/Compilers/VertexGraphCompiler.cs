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
            get { lock (m_Lock) { return m_VertexGraphBuilder.StartTime; } }
        }

        public int FinishTime
        {
            get { lock (m_Lock) { return m_VertexGraphBuilder.FinishTime; } }
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
            get { lock (m_Lock) { return m_VertexGraphBuilder; } }
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
                    foreach (T id in dependentActivityIds)
                    {
                        m_VertexGraphBuilder.Activity(id).Dependencies.Remove(activityId);
                    }
                }
                {
                    IEnumerable<T> dependentActivityIds = m_VertexGraphBuilder
                        .Activities.Where(x => x.PlanningDependencies.Contains(activityId)).Select(x => x.Id);
                    foreach (T id in dependentActivityIds)
                    {
                        m_VertexGraphBuilder.Activity(id).PlanningDependencies.Remove(activityId);
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
                if (!m_VertexGraphBuilder.TransitiveReduction())
                    throw new InvalidOperationException(Properties.Resources.Message_CannotPerformTransitiveReduction);

                foreach (T activityId in m_VertexGraphBuilder.ActivityIds)
                {
                    TDependentActivity activity = m_VertexGraphBuilder.Activity(activityId);
                    IList<T> actualDependencyIds = m_VertexGraphBuilder.ActivityDependencyIds(activityId);
                    var remainingCompiled = new HashSet<T>(activity.Dependencies.Intersect(actualDependencyIds));
                    var remainingPlanning = new HashSet<T>(activity.PlanningDependencies.Intersect(actualDependencyIds));
                    m_VertexGraphBuilder.SetActivityDependencies(activityId, remainingCompiled, remainingPlanning);
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

        public IGraphCompilation<T, TResourceId, TWorkStreamId, TDependentActivity> Compile(
            IList<IResource<TResourceId, TWorkStreamId>> resources)
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
                bool infiniteResources = resources.Count == 0;
                IList<IResource<TResourceId, TWorkStreamId>> filteredResources = resources.Where(x => !x.IsInactive).ToList();

                m_VertexGraphBuilder.ResetResourceState(m_VertexGraphBuilder.Activities);

                var compilationErrors = new List<GraphCompilationError>();
                m_VertexGraphBuilder.AddPreCompilationErrors(compilationErrors, filteredResources, infiniteResources);

                if (compilationErrors.Count != 0)
                {
                    return new GraphCompilation<T, TResourceId, TWorkStreamId, TDependentActivity>(
                        m_VertexGraphBuilder.Activities.Select(x => (TDependentActivity)x.CloneObject()),
                        Enumerable.Empty<IResourceSchedule<T, TResourceId, TWorkStreamId>>(),
                        Enumerable.Empty<IWorkStream<TWorkStreamId>>(),
                        compilationErrors);
                }

                // First CPM pass → schedule → wire resource dependencies → second CPM pass.
                m_VertexGraphBuilder.CalculateCriticalPath();
                List<IResourceSchedule<T, TResourceId, TWorkStreamId>> resourceSchedules =
                    m_VertexGraphBuilder.CalculateResourceSchedulesByPriorityList(filteredResources).ToList();

                if (infiniteResources)
                {
                    resourceSchedules = PriorityListResourceScheduler<T, TResourceId, TWorkStreamId>.ReplaceWithSyntheticResources(resourceSchedules);
                }

                m_VertexGraphBuilder.AssignResourceDependencies(resourceSchedules);
                m_VertexGraphBuilder.CalculateCriticalPath();

                if (!m_VertexGraphBuilder.BackFillIsolatedNodes())
                {
                    throw new InvalidOperationException(Properties.Resources.Message_CannotBackFillIsolatedNodes);
                }

                m_VertexGraphBuilder.RemoveResourceOnlyDependencies(m_VertexGraphBuilder.Activities);
                m_VertexGraphBuilder.AddPostCompilationErrors(compilationErrors);
                m_VertexGraphBuilder.UpdateActivitySuccessors(m_VertexGraphBuilder.Activities);

                // Rebuild schedules aligned to final CPM times, collect indirect resource schedules.
                IEnumerable<IActivity<T, TResourceId, TWorkStreamId>> finalActivities =
                    m_VertexGraphBuilder.Activities.Select(x => (IActivity<T, TResourceId, TWorkStreamId>)x.CloneObject());
                int startTime = m_VertexGraphBuilder.StartTime;
                int finishTime = m_VertexGraphBuilder.FinishTime;

                IEnumerable<IResourceSchedule<T, TResourceId, TWorkStreamId>> newSchedules =
                    PriorityListResourceScheduler<T, TResourceId, TWorkStreamId>.RebuildAlignedResourceSchedules(
                        resourceSchedules, infiniteResources,
                        id => (IActivity<T, TResourceId, TWorkStreamId>)m_VertexGraphBuilder.Activity(id),
                        finalActivities, startTime, finishTime);
                IEnumerable<IResourceSchedule<T, TResourceId, TWorkStreamId>> indirectSchedules =
                    PriorityListResourceScheduler<T, TResourceId, TWorkStreamId>.CollectIndirectResourceSchedules(
                        filteredResources, newSchedules, finalActivities, startTime, finishTime);
                List<IResourceSchedule<T, TResourceId, TWorkStreamId>> totalSchedules =
                    newSchedules.Union(indirectSchedules).ToList();

                HashSet<TWorkStreamId> workstreamsUsed = finalActivities.SelectMany(x => x.TargetWorkStreams).Distinct().ToHashSet();
                HashSet<TWorkStreamId> resourcePhasesUsed =
                    PriorityListResourceScheduler<T, TResourceId, TWorkStreamId>.GetResourcePhasesUsed(totalSchedules, workstreamsUsed);

                return new GraphCompilation<T, TResourceId, TWorkStreamId, TDependentActivity>(
                    m_VertexGraphBuilder.Activities.Select(x => (TDependentActivity)x.CloneObject()),
                    totalSchedules,
                    workStreams.Where(x => resourcePhasesUsed.Contains(x.Id)),
                    compilationErrors);
            }
        }

        #endregion

    }
}
