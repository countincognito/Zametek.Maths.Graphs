using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    public class GraphCompilation<T, TDependentActivity, TResourceId>
        : IGraphCompilation<T, TDependentActivity, TResourceId>
        where TDependentActivity : IDependentActivity<T, TResourceId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
    {
        #region Ctors

        public GraphCompilation(
            IEnumerable<TDependentActivity> dependentActivities,
            IEnumerable<IResourceSchedule<TResourceId>> resourceSchedules)
        {
            DependentActivities = dependentActivities.ToList();
            ResourceSchedules = resourceSchedules.ToList();
        }

        public GraphCompilation(
            IEnumerable<TDependentActivity> dependentActivities,
            IEnumerable<IResourceSchedule<TResourceId>> resourceSchedules,
            IGraphCompilationErrors<T> graphCompilationErrors)
            : this(dependentActivities, resourceSchedules)
        {
            Errors = graphCompilationErrors ?? throw new ArgumentNullException(nameof(graphCompilationErrors));
        }

        #endregion

        #region GraphCompilation<T> Members

        public IGraphCompilationErrors<T> Errors
        {
            get;
        }

        public IEnumerable<TDependentActivity> DependentActivities
        {
            get;
        }

        public IEnumerable<IResourceSchedule<TResourceId>> ResourceSchedules
        {
            get;
        }

        #endregion
    }
}
