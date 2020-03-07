using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    public class GraphCompilationErrors<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        #region Ctors

        public GraphCompilationErrors(
            bool allResourcesExplicitTargetsButNotAllActivitiesTargeted,
            IEnumerable<CircularDependency<T>> circularDependencies,
            IEnumerable<T> missingDependencies)
        {
            AllResourcesExplicitTargetsButNotAllActivitiesTargeted = allResourcesExplicitTargetsButNotAllActivitiesTargeted;
            CircularDependencies = circularDependencies.ToList();
            MissingDependencies = missingDependencies.ToList();
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

        #endregion
    }
}
