using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    public interface IGraphCompilationErrors<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        bool AllResourcesExplicitTargetsButNotAllActivitiesTargeted { get; }

        IEnumerable<ICircularDependency<T>> CircularDependencies { get; }

        IEnumerable<T> MissingDependencies { get; }

        IEnumerable<T> InvalidConstraints { get; }
    }
}
