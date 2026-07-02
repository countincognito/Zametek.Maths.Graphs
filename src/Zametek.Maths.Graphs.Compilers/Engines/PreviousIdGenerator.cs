using System;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// Generates sequential IDs in descending order.
    /// </summary>
    public class PreviousIdGenerator<T>
        : IIdGenerator<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        private readonly object m_Lock;
        private T m_Value;

        /// <summary>
        /// Creates a generator that starts stepping downwards from the given initial value.
        /// </summary>
        public PreviousIdGenerator(T initial = default)
        {
            m_Lock = new object();
            m_Value = initial;
        }

        /// <inheritdoc/>
        public T Generate()
        {
            lock (m_Lock)
            {
                m_Value = m_Value.Previous();
                return m_Value;
            }
        }
    }
}
