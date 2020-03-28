using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    public interface ICircularDependency<T>
        where T : IComparable<T>, IEquatable<T>
    {
        HashSet<T> Dependencies { get; }
    }
}
