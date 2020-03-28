using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    public class GraphCompilationErrors<T>
        : IGraphCompilationErrors<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        #region Ctors

        public GraphCompilationErrors(
            bool allResourcesExplicitTargetsButNotAllActivitiesTargeted,
            IEnumerable<ICircularDependency<T>> circularDependencies,
            IEnumerable<T> missingDependencies)
        {
            AllResourcesExplicitTargetsButNotAllActivitiesTargeted = allResourcesExplicitTargetsButNotAllActivitiesTargeted;
            CircularDependencies = circularDependencies.ToList();
            MissingDependencies = missingDependencies.ToList();
        }

        #endregion

        #region IGraphCompilationErrors<T> Members

        public bool AllResourcesExplicitTargetsButNotAllActivitiesTargeted
        {
            get;
        }

        public IEnumerable<ICircularDependency<T>> CircularDependencies
        {
            get;
        }

        public IEnumerable<T> MissingDependencies
        {
            get;
        }

        #endregion
    }
}
