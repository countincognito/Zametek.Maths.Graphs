using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// Makes the <see cref="IIdGenerator{T}"/> of a builder. A generator holds the count of one graph, and so
    /// builders cannot share one. The factory holds no state: it is an engine like the others, and it is shared by
    /// the builders of an engines bundle, by their clones and by the builders of a standard graph factory. Each
    /// builder asks it for a generator of its own, when the builder is made.
    /// </summary>
    /// <remarks>
    /// A new builder asks for a generator for an empty graph. A builder that loads a graph, and the clone of a
    /// builder, ask for a generator for the IDs that the graph already holds. Thus, nobody has to work out where a
    /// generator must start, and the generator of a clone is of the same kind as the generator of its original.
    /// <see cref="StandardIdGeneratorFactory{T}"/> is the factory that builders have by default.
    /// </remarks>
    /// <typeparam name="T">The ID type.</typeparam>
    public interface IIdGeneratorFactory<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        /// <summary>
        /// Makes a new generator for a graph that holds the given IDs (no IDs, for a new graph). The generator must
        /// not give one of those IDs, and it shares no state with a generator that the factory made before.
        /// </summary>
        /// <param name="usedIds">
        /// The IDs that the graph holds. For an edge generator they are the IDs of the edges, and for a node
        /// generator they are the IDs of the nodes.
        /// </param>
        IIdGenerator<T> Create(IEnumerable<T> usedIds);
    }
}
