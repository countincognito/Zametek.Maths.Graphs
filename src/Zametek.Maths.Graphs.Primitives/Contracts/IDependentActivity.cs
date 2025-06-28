using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    public interface IDependentActivity<T, TResourceId, TWorkStreamId>
        : IActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        HashSet<T> Dependencies { get; }

        HashSet<T> PlanningDependencies { get; }

        HashSet<T> ResourceDependencies { get; }

        HashSet<T> Successors { get; }
    }
}
