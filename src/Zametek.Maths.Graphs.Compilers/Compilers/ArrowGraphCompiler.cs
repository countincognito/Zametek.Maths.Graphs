using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    // Sealed compiler for Activity-on-Arrow graphs. Owns the ArrowGraphBuilder directly.
    // No inheritance from any compiler base class — all coordination logic is inlined.
    public sealed class ArrowGraphCompiler<T, TResourceId, TWorkStreamId, TDependentActivity>
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

        public ArrowGraphCompiler()
        {
            T edgeId = default;
            T nodeId = default;
            // Use DependentActivity as dummy so that the cast to TDependentActivity succeeds.
            m_ArrowGraphBuilder = new ArrowGraphBuilder<T, TResourceId, TWorkStreamId, TDependentActivity>(
                () => edgeId = edgeId.Previous(),
                () => nodeId = nodeId.Previous(),
                (id) => new DependentActivity<T, TResourceId, TWorkStreamId>(id, 0, canBeRemoved: true) as TDependentActivity);
            m_Lock = new object();
        }

        // Internal constructor for testability — accepts injected builder.
        internal ArrowGraphCompiler(ArrowGraphBuilder<T, TResourceId, TWorkStreamId, TDependentActivity> arrowGraphBuilder)
        {
            m_ArrowGraphBuilder = arrowGraphBuilder ?? throw new ArgumentNullException(nameof(arrowGraphBuilder));
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
                    return m_ArrowGraphBuilder.StartTime;
                }
            }
        }

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

        public T GetNextActivityId()
        {
            lock (m_Lock)
            {
                return m_ArrowGraphBuilder.ActivityIds.DefaultIfEmpty().Max().Next();
            }
        }

        public void Reset()
        {
            lock (m_Lock)
            {
                m_ArrowGraphBuilder.Reset();
            }
        }

        public bool AddActivity(TDependentActivity activity)
        {
            lock (m_Lock)
            {
                return m_ArrowGraphBuilder.AddActivity(
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
