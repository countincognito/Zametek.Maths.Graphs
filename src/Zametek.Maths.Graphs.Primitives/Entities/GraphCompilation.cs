using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    public class GraphCompilation<T, TDependentActivity>
        where TDependentActivity : IDependentActivity<T>, IActivity<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        #region Ctors

        public GraphCompilation(
            IEnumerable<TDependentActivity> dependentActivities,
            IEnumerable<IResourceSchedule<T>> resourceSchedules)
        {
            DependentActivities = dependentActivities.ToList();
            ResourceSchedules = resourceSchedules.ToList();
        }

        public GraphCompilation(
            IEnumerable<TDependentActivity> dependentActivities,
            IEnumerable<IResourceSchedule<T>> resourceSchedules,
            GraphCompilationErrors<T> graphCompilationErrors)
            : this(dependentActivities, resourceSchedules)
        {
            Errors = graphCompilationErrors ?? throw new ArgumentNullException(nameof(graphCompilationErrors));
        }

        #endregion

        #region Properties

        public GraphCompilationErrors<T> Errors
        {
            get;
        }

        public IList<TDependentActivity> DependentActivities
        {
            get;
        }

        public IList<IResourceSchedule<T>> ResourceSchedules
        {
            get;
        }

        #endregion
    }
}
