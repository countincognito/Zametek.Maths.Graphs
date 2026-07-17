using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    // Compiler for Activity-on-Vertex graphs.
    // Thin coordinator: owns the builder + a lock. Every public method delegates to
    // m_VertexGraphBuilder under the lock. No algorithm logic lives here.
    /// <summary>
    /// Compiler for Activity-on-Vertex graphs: a thread-safe coordinator around a <see cref="VertexGraphBuilder{T, TResourceId, TWorkStreamId, TActivity}"/>. This is the compiler to use for analysis - <see cref="Compile()"/> runs the full pipeline including resource scheduling.
    /// </summary>
    public class VertexGraphCompiler<T, TResourceId, TWorkStreamId, TDependentActivity>
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

        /// <summary>
        /// Creates a compiler wired with the default engines.
        /// </summary>
        public VertexGraphCompiler()
        {
            T edgeId = default;
            m_VertexGraphBuilder = new VertexGraphBuilder<T, TResourceId, TWorkStreamId, TDependentActivity>(new PreviousIdGenerator<T>(edgeId));
            m_Lock = new object();
        }

        // Builder-injecting constructor - accepts a builder configured with custom engines.
        /// <summary>
        /// Creates a compiler around the given (possibly custom-engined) builder.
        /// </summary>
        public VertexGraphCompiler(VertexGraphBuilder<T, TResourceId, TWorkStreamId, TDependentActivity> vertexGraphBuilder)
        {
            m_VertexGraphBuilder = vertexGraphBuilder ?? throw new ArgumentNullException(nameof(vertexGraphBuilder));
            m_Lock = new object();
        }

        #endregion

        #region Properties

        /// <summary>
        /// The earliest start time across all activities.
        /// </summary>
        public int StartTime
        {
            get { lock (m_Lock) { return m_VertexGraphBuilder.StartTime; } }
        }

        /// <summary>
        /// The latest finish time across all activities.
        /// </summary>
        public int FinishTime
        {
            get { lock (m_Lock) { return m_VertexGraphBuilder.FinishTime; } }
        }

        // https://en.wikipedia.org/wiki/Cyclomatic_complexity
        /// <summary>
        /// The cyclomatic complexity of the network (a measure of its parallelism).
        /// </summary>
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

        /// <summary>
        /// Returns an unused activity ID.
        /// </summary>
        public T GetNextActivityId()
        {
            lock (m_Lock)
            {
                return m_VertexGraphBuilder.ActivityIds.DefaultIfEmpty().Max().Next();
            }
        }

        /// <summary>
        /// Clears all activities and returns the compiler to its initial state.
        /// </summary>
        public void Reset()
        {
            lock (m_Lock) { m_VertexGraphBuilder.Reset(); }
        }

        /// <summary>
        /// Adds an activity, wiring its compiled and planning dependencies into the graph. Returns false if the ID already exists.
        /// </summary>
        public bool AddActivity(TDependentActivity activity)
        {
            lock (m_Lock)
            {
                return m_VertexGraphBuilder.AddActivity(
                    activity,
                    new HashSet<T>(activity.Dependencies.Union(activity.PlanningDependencies)));
            }
        }

        /// <summary>
        /// Removes an activity and detaches it from its dependents.
        /// </summary>
        public bool RemoveActivity(T activityId)
        {
            lock (m_Lock)
            {
                {
                    // Clear out the activity from compiled dependencies.
                    IEnumerable<T> dependentActivityIds = m_VertexGraphBuilder
                        .Activities.Where(x => x.Dependencies.Contains(activityId)).Select(x => x.Id);
                    foreach (T id in dependentActivityIds)
                    {
                        m_VertexGraphBuilder.Activity(id).Dependencies.Remove(activityId);
                    }
                }
                {
                    // Clear out the activity from planning dependencies.
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

        /// <summary>
        /// Strips redundant dependencies, keeping only the minimal edge set. Throws <see cref="InvalidOperationException"/> if the reduction cannot be performed.
        /// </summary>
        public void TransitiveReduction()
        {
            lock (m_Lock)
            {
                if (!m_VertexGraphBuilder.TransitiveReduction())
                {
                    throw new InvalidOperationException(Properties.Resources.Message_CannotPerformTransitiveReduction);
                }

                // Now set the compiled and planning dependencies to match the actual remaining dependencies.
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

        /// <summary>
        /// Exports the compiled Activity-on-Vertex structure.
        /// </summary>
        public Graph<T, IEvent<T>, TDependentActivity> ToGraph()
        {
            lock (m_Lock) { return m_VertexGraphBuilder.ToGraph(); }
        }

        /// <summary>
        /// Replaces an activity's compiled and planning dependencies.
        /// </summary>
        public bool SetActivityDependencies(T activityId, HashSet<T> dependencies, HashSet<T> planningDependencies)
        {
            lock (m_Lock)
            {
                return m_VertexGraphBuilder.SetActivityDependencies(activityId, dependencies, planningDependencies);
            }
        }

        /// <summary>
        /// Compiles with infinite resources - the pure critical-path schedule.
        /// </summary>
        public IGraphCompilation<T, TResourceId, TWorkStreamId, TDependentActivity> Compile()
        {
            return Compile(new List<IResource<TResourceId, TWorkStreamId>>());
        }

        /// <summary>
        /// Compiles, scheduling activities onto the given resources (an empty list means infinite resources).
        /// </summary>
        public IGraphCompilation<T, TResourceId, TWorkStreamId, TDependentActivity> Compile(
            List<IResource<TResourceId, TWorkStreamId>> resources)
        {
            return Compile(resources, new List<IWorkStream<TWorkStreamId>>());
        }

        /// <summary>
        /// Compiles with resources and reports which of the given work streams were used.
        /// </summary>
        public IGraphCompilation<T, TResourceId, TWorkStreamId, TDependentActivity> Compile(
            List<IResource<TResourceId, TWorkStreamId>> resources,
            List<IWorkStream<TWorkStreamId>> workStreams)
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
                // If resources are 0, assume infinite resources.
                bool infiniteResources = resources.Count == 0;
                // Filter out disabled resources.
                List<IResource<TResourceId, TWorkStreamId>> filteredResources = resources.Where(x => !x.IsInactive).ToList();

                m_VertexGraphBuilder.ResetResourceState(m_VertexGraphBuilder.Activities.ToList());

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

                // If the previous calculation was performed with infinite resources, then it will not be possible
                // to handle resource dependencies. So here we need to create fake resources for resource dependencies
                // to work in the next step.
                if (infiniteResources)
                {
                    resourceSchedules = m_VertexGraphBuilder.ResourceSchedulingEngine.ReplaceWithSyntheticResources(resourceSchedules);
                }

                // Determine the resource dependencies and add them to the compiled dependencies.
                m_VertexGraphBuilder.AssignResourceDependencies(resourceSchedules);
                m_VertexGraphBuilder.CalculateCriticalPath();

                if (!m_VertexGraphBuilder.BackFillIsolatedNodes())
                {
                    throw new InvalidOperationException(Properties.Resources.Message_CannotBackFillIsolatedNodes);
                }

                m_VertexGraphBuilder.RemoveResourceOnlyDependencies(m_VertexGraphBuilder.Activities.ToList());
                m_VertexGraphBuilder.AddPostCompilationErrors(compilationErrors);
                m_VertexGraphBuilder.UpdateActivitySuccessors(m_VertexGraphBuilder.Activities.ToList());

                // Rebuild schedules aligned to final CPM times, collect indirect resource schedules.
                List<IActivity<T, TResourceId, TWorkStreamId>> finalActivities = m_VertexGraphBuilder.Activities
                    .Select(x => (IActivity<T, TResourceId, TWorkStreamId>)x.CloneObject())
                    .ToList();
                int startTime = m_VertexGraphBuilder.StartTime;
                int finishTime = m_VertexGraphBuilder.FinishTime;

                List<IResourceSchedule<T, TResourceId, TWorkStreamId>> newSchedules =
                    m_VertexGraphBuilder.ResourceSchedulingEngine.RebuildAlignedResourceSchedules(
                        resourceSchedules, infiniteResources,
                        m_VertexGraphBuilder,
                        finalActivities, startTime, finishTime)
                    .ToList();
                List<IResourceSchedule<T, TResourceId, TWorkStreamId>> indirectSchedules =
                    m_VertexGraphBuilder.ResourceSchedulingEngine.CollectIndirectResourceSchedules(
                        filteredResources, newSchedules, finalActivities, startTime, finishTime)
                    .ToList();
                List<IResourceSchedule<T, TResourceId, TWorkStreamId>> totalSchedules =
                    newSchedules.Union(indirectSchedules).ToList();

                // Now calculate the used work streams.
                HashSet<TWorkStreamId> workstreamsUsed = finalActivities.SelectMany(x => x.TargetWorkStreams).Distinct().ToHashSet();
                HashSet<TWorkStreamId> resourcePhasesUsed =
                    m_VertexGraphBuilder.ResourceSchedulingEngine.GetResourcePhasesUsed(totalSchedules, workstreamsUsed);

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
