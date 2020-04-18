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
            IEnumerable<T> missingDependencies,
            IEnumerable<T> invalidConstraints)
        {
            AllResourcesExplicitTargetsButNotAllActivitiesTargeted = allResourcesExplicitTargetsButNotAllActivitiesTargeted;
            CircularDependencies = circularDependencies.ToList();
            MissingDependencies = missingDependencies.ToList();
            InvalidConstraints = invalidConstraints.ToList();
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

        public IEnumerable<T> InvalidConstraints
        {
            get;
        }

        #endregion
    }
}
