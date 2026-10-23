using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// Makes <see cref="NextIdGenerator{T}"/> objects: generators of IDs in ascending order, that start over the
    /// highest ID that the graph holds.
    /// </summary>
    public class NextIdGeneratorFactory<T>
        : IIdGeneratorFactory<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        private readonly T m_Initial;

        /// <summary>
        /// Creates a factory whose generators start stepping upwards from the given initial value, or from the
        /// highest ID of the graph if that ID is higher.
        /// </summary>
        public NextIdGeneratorFactory(T initial = default)
        {
            m_Initial = initial;
        }

        /// <inheritdoc/>
        public IIdGenerator<T> Create(IEnumerable<T> usedIds)
        {
            if (usedIds is null)
            {
                throw new ArgumentNullException(nameof(usedIds));
            }

            T start = m_Initial;
            foreach (T id in usedIds)
            {
                if (id.CompareTo(start) > 0)
                {
                    start = id;
                }
            }
            return new NextIdGenerator<T>(start);
        }
    }
}
