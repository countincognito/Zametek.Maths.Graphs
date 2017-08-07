using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    public abstract class ArrowGraphCompilerBase<T, TDependentActivity, TActivity, TEvent>
        : GraphCompilerBase<T, TDependentActivity, TEvent, TDependentActivity, TEvent>
        where TDependentActivity : IDependentActivity<T>, TActivity
        where TActivity : IActivity<T>
        where TEvent : IEvent<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        #region Fields

        private readonly object m_Lock;
        private readonly ArrowGraphBuilderBase<T, TDependentActivity, TEvent> m_ArrowGraphBuilder;

        #endregion

        #region Ctors

        protected ArrowGraphCompilerBase(ArrowGraphBuilderBase<T, TDependentActivity, TEvent> arrowGraphBuilder)
            : base(arrowGraphBuilder)
        {
            m_ArrowGraphBuilder = arrowGraphBuilder ?? throw new ArgumentNullException(nameof(arrowGraphBuilder));
            m_Lock = new object();
        }

        #endregion

        #region Public Methods

        public void Compile()
        {
            lock (m_Lock)
            {
                // Sanity check the graph data.
                IEnumerable<T> missingDependencies = m_ArrowGraphBuilder.MissingDependencies;
                if (missingDependencies.Any())
                {
                    throw new InvalidOperationException("Cannot construct arrow graph");
                }

                m_ArrowGraphBuilder.CalculateCriticalPath();
            }
        }

        #endregion

        #region Overrides

        public override bool AddActivity(TDependentActivity activity)
        {
            lock (m_Lock)
            {
                return m_ArrowGraphBuilder.AddActivity(activity, activity.Dependencies);
            }
        }

        public override bool RemoveActivity(T activityId)
        {
            lock (m_Lock)
            {
                // Clear out the activity from compiled dependencies first.
                IEnumerable<T> dependentActivityIds = m_ArrowGraphBuilder
                    .Activities
                    .Where(x => x.Dependencies.Contains(activityId))
                    .Select(x => x.Id);

                foreach (T dependentActivityId in dependentActivityIds)
                {
                    var dependentActivity = m_ArrowGraphBuilder.Activity(dependentActivityId);
                    dependentActivity.Dependencies.Remove(activityId);
                }

                m_ArrowGraphBuilder.Activity(activityId)?.SetAsRemovable();
                return m_ArrowGraphBuilder.RemoveActivity(activityId);
            }
        }

        #endregion
    }
}
