using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    public class GraphCompilation<T, TResourceId, TDependentActivity>
        : IGraphCompilation<T, TResourceId, TDependentActivity>
        where TDependentActivity : IDependentActivity<T, TResourceId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
    {
        #region Ctors

        public GraphCompilation(
            IEnumerable<TDependentActivity> dependentActivities,
            IEnumerable<IResourceSchedule<T, TResourceId>> resourceSchedules)
            : this(dependentActivities, resourceSchedules, Enumerable.Empty<IGraphCompilationError>())
        {
        }

        public GraphCompilation(
            IEnumerable<TDependentActivity> dependentActivities,
            IEnumerable<IResourceSchedule<T, TResourceId>> resourceSchedules,
            IEnumerable<IGraphCompilationError> compilationErrors)
        {
            DependentActivities = dependentActivities.ToList();
            ResourceSchedules = resourceSchedules.ToList();
            CompilationErrors = compilationErrors.ToList();
        }

        #endregion

        #region GraphCompilation<T> Members

        public IEnumerable<TDependentActivity> DependentActivities
        {
            get;
        }

        public IEnumerable<IResourceSchedule<T, TResourceId>> ResourceSchedules
        {
            get;
        }

        public IEnumerable<IGraphCompilationError> CompilationErrors
        {
            get;
        }

        #endregion
    }
}
