using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    public interface IDependentActivity<T, TResourceId>
        : IActivity<T, TResourceId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
    {
        HashSet<T> Dependencies { get; }

        HashSet<T> ResourceDependencies { get; }
    }
}
