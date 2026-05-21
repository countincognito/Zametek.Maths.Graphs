using System;

namespace Zametek.Maths.Graphs
{
    public class NextIdGenerator<T>
        : IIdGenerator<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        private readonly object m_Lock;
        private T m_Value;

        public NextIdGenerator(T initial = default)
        {
            m_Lock = new object();
            m_Value = initial;
        }

        public T Generate()
        {
            lock (m_Lock)
            {
                m_Value = m_Value.Next();
                return m_Value;
            }
        }
    }
}
