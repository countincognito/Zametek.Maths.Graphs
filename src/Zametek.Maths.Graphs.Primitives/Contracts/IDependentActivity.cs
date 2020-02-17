using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    public interface IDependentActivity<T>
        : IActivity<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        HashSet<T> Dependencies
        {
            get;
        }

        HashSet<T> ResourceDependencies
        {
            get;
        }
    }
}
