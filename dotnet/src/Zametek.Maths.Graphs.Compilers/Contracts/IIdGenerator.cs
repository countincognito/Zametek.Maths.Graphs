using System;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// Generates a sequence of unique IDs for graph elements (edges, nodes,
    /// dummy activities).
    /// </summary>
    /// <typeparam name="T">The ID type.</typeparam>
    public interface IIdGenerator<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        /// <summary>
        /// Returns the next unique ID in the sequence.
        /// </summary>
        T Generate();
    }
}
