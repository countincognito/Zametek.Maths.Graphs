using System;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// Generates a sequence of unique IDs for graph elements (edges, nodes,
    /// dummy activities). A generator holds the count of one graph, and so each
    /// builder has one of its own, which an <see cref="IIdGeneratorFactory{T}"/> made.
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
