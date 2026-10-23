using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// The ID generator factory that builders have by default. It chooses the generator from the type of the ID,
    /// as <c>KeyExtensions</c> does: <see cref="int"/> IDs are descending (<see cref="PreviousIdGeneratorFactory{T}"/>),
    /// and so they are negative, and stay clear of the positive IDs of the activities. <see cref="Guid"/> IDs have no
    /// previous value, and so they are new GUIDs (<see cref="NextIdGeneratorFactory{T}"/>). The other types of ID have
    /// no generator at all: the generators of this factory throw an <see cref="InvalidOperationException"/> when they
    /// are asked for an ID. Use a factory of your own for such a type.
    /// </summary>
    public class StandardIdGeneratorFactory<T>
        : IIdGeneratorFactory<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        private readonly PreviousIdGeneratorFactory<T> m_Previous = new PreviousIdGeneratorFactory<T>();
        private readonly NextIdGeneratorFactory<T> m_Next = new NextIdGeneratorFactory<T>();

        /// <inheritdoc/>
        public IIdGenerator<T> Create(IEnumerable<T> usedIds) =>
            typeof(T) == typeof(Guid)
                ? m_Next.Create(usedIds)
                : m_Previous.Create(usedIds);
    }
}
