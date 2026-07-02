using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// A set of activity IDs that form a circular dependency (a strongly
    /// connected component with more than one member).
    /// </summary>
    /// <typeparam name="T">The ID type.</typeparam>
    public interface ICircularDependency<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        /// <summary>
        /// The IDs participating in the cycle.
        /// </summary>
        HashSet<T> Dependencies { get; }
    }
}
