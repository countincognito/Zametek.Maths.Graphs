using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    // Compiler for Activity-on-Arrow graphs. Owns the ArrowGraphBuilder directly.
    // No inheritance from any compiler base class - all coordination logic is inlined.
    /// <summary>
    /// Compiler for Activity-on-Arrow graphs: a thread-safe coordinator around an <see cref="ArrowGraphBuilder{T, TResourceId, TWorkStreamId, TActivity}"/>. Intended for rendering - it prepares the network for <see cref="ToGraph"/> and performs no resource scheduling (use <see cref="VertexGraphCompiler{T, TResourceId, TWorkStreamId, TDependentActivity}"/> for analysis).
    /// </summary>
    public class ArrowGraphCompiler<T, TResourceId, TWorkStreamId, TDependentActivity>
        where TDependentActivity : class, IDependentActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        #region Fields

        private readonly object m_Lock;
        private readonly ArrowGraphBuilder<T, TResourceId, TWorkStreamId, TDependentActivity> m_ArrowGraphBuilder;

        #endregion

        #region Ctors

        /// <summary>
        /// Creates a compiler wired with the default engines.
        /// </summary>
        public ArrowGraphCompiler()
        {
            T edgeId = default;
            T nodeId = default;
            // Use DependentActivity as dummy so that the cast to TDependentActivity succeeds.
            m_ArrowGraphBuilder = new ArrowGraphBuilder<T, TResourceId, TWorkStreamId, TDependentActivity>(
                new PreviousIdGenerator<T>(edgeId),
                new PreviousIdGenerator<T>(nodeId),
                new DummyActivityGenerator<T, TResourceId, TWorkStreamId, TDependentActivity>(),
                new EventGenerator<T>(),
                new ArrowTarjanStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TDependentActivity>(),
                new ArrowCriticalPathEngine<T, TResourceId, TWorkStreamId, TDependentActivity>(),
                new PriorityListResourceScheduler<T, TResourceId, TWorkStreamId>());
            m_Lock = new object();
        }

        // Builder-injecting constructor - accepts a builder configured with custom engines.
        /// <summary>
        /// Creates a compiler around the given (possibly custom-engined) builder.
        /// </summary>
        public ArrowGraphCompiler(ArrowGraphBuilder<T, TResourceId, TWorkStreamId, TDependentActivity> arrowGraphBuilder)
        {
            m_ArrowGraphBuilder = arrowGraphBuilder ?? throw new ArgumentNullException(nameof(arrowGraphBuilder));
            m_Lock = new object();
        }

        #endregion

        #region Properties

        /// <summary>
        /// The earliest start time across all activities.
        /// </summary>
        public int StartTime
        {
            get
            {
                lock (m_Lock)
                {
                    return m_ArrowGraphBuilder.StartTime;
                }
            }
        }

        /// <summary>
        /// The latest finish time across all activities.
        /// </summary>
        public int FinishTime
        {
            get
            {
                lock (m_Lock)
                {
                    return m_ArrowGraphBuilder.FinishTime;
                }
            }
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
                    int edgeCount = m_ArrowGraphBuilder.Edges.Count();
                    int nodeCount = m_ArrowGraphBuilder.Nodes.Count();

                    // Correction factor for multiple entry and exit points.

                    // Artificial Start and End nodes (there is only one Start and one End in an arrow graph).
                    int extraNodes = 2;
                    // Artificial edges to connect the artificial Start and End nodes.
                    int extraEdges = m_ArrowGraphBuilder.StartNodes.Count() + m_ArrowGraphBuilder.EndNodes.Count();

                    // Isolated nodes count as separate connected components.
                    int isolatedNodeCount = m_ArrowGraphBuilder.IsolatedNodes.Count();

                    int cyclomaticComplexity = (edgeCount + extraEdges) - (nodeCount + extraNodes) + 2 * (1 + isolatedNodeCount);
                    return cyclomaticComplexity;
                }
            }
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
                return m_ArrowGraphBuilder.ActivityIds.DefaultIfEmpty().Max().Next();
            }
        }

        /// <summary>
        /// Clears all activities and returns the compiler to its initial state.
        /// </summary>
        public void Reset()
        {
            lock (m_Lock)
            {
                m_ArrowGraphBuilder.Reset();
            }
        }

        /// <summary>
        /// Adds an activity, wiring its compiled and planning dependencies into the graph. Returns false if the ID already exists.
        /// </summary>
        public bool AddActivity(TDependentActivity activity)
        {
            lock (m_Lock)
            {
                return m_ArrowGraphBuilder.AddActivity(
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
                    IEnumerable<T> dependentActivityIds = m_ArrowGraphBuilder
                        .Activities
                        .Where(x => x.Dependencies.Contains(activityId))
                        .Select(x => x.Id);

                    foreach (T dependentActivityId in dependentActivityIds)
                    {
                        var dependentActivity = m_ArrowGraphBuilder.Activity(dependentActivityId);
                        dependentActivity.Dependencies.Remove(activityId);
                    }
                }
                {
                    // Clear out the activity from planning dependencies.
                    IEnumerable<T> dependentActivityIds = m_ArrowGraphBuilder
                        .Activities
                        .Where(x => x.PlanningDependencies.Contains(activityId))
                        .Select(x => x.Id);

                    foreach (T dependentActivityId in dependentActivityIds)
                    {
                        var dependentActivity = m_ArrowGraphBuilder.Activity(dependentActivityId);
                        dependentActivity.PlanningDependencies.Remove(activityId);
                    }
                }

                m_ArrowGraphBuilder.Activity(activityId)?.SetAsRemovable();
                return m_ArrowGraphBuilder.RemoveActivity(activityId);
            }
        }

        /// <summary>
        /// Strips redundant dependencies, keeping only the minimal edge set. Throws <see cref="InvalidOperationException"/> if the reduction cannot be performed.
        /// </summary>
        public void TransitiveReduction()
        {
            lock (m_Lock)
            {
                bool transitivelyReduced = m_ArrowGraphBuilder.TransitiveReduction();
                if (!transitivelyReduced)
                {
                    throw new InvalidOperationException(Properties.Resources.Message_CannotPerformTransitiveReduction);
                }
            }
        }

        /// <summary>
        /// Validates the graph, applies transitive reduction and runs the critical-path calculation so the network can be laid out. Performs no resource scheduling.
        /// </summary>
        public void Compile()
        {
            lock (m_Lock)
            {
                // Sanity check the graph data.
                IEnumerable<T> invalidDependencies = m_ArrowGraphBuilder.InvalidDependencies;
                if (invalidDependencies.Any())
                {
                    throw new InvalidOperationException(Properties.Resources.Message_CannotConstructArrowGraphDueToInvalidDependencies);
                }
                TransitiveReduction();
                m_ArrowGraphBuilder.CalculateCriticalPath();
            }
        }

        /// <summary>
        /// Exports the compiled Activity-on-Arrow structure for rendering.
        /// </summary>
        public Graph<T, TDependentActivity, IEvent<T>> ToGraph()
        {
            lock (m_Lock)
            {
                return m_ArrowGraphBuilder.ToGraph();
            }
        }

        #endregion
    }
}
