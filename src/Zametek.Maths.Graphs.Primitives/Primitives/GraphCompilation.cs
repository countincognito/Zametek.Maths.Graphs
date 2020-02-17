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
            bool allResourcesExplicitTargetsButNotAllActivitiesTargeted,
            IEnumerable<CircularDependency<T>> circularDependencies,
            IEnumerable<T> missingDependencies,
            IEnumerable<TDependentActivity> dependentActivities,
            IEnumerable<IResourceSchedule<T>> resourceSchedules)
        {
            AllResourcesExplicitTargetsButNotAllActivitiesTargeted = allResourcesExplicitTargetsButNotAllActivitiesTargeted;
            CircularDependencies = circularDependencies.ToList();
            MissingDependencies = missingDependencies.ToList();
            DependentActivities = dependentActivities.ToList();
            ResourceSchedules = resourceSchedules.ToList();
        }

        #endregion

        #region Properties

        public bool AllResourcesExplicitTargetsButNotAllActivitiesTargeted
        {
            get;
        }

        public IList<CircularDependency<T>> CircularDependencies
        {
            get;
        }

        public IList<T> MissingDependencies
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
