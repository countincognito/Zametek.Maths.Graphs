using System;

namespace Zametek.Maths.Graphs
{
    public interface IHaveId<out T>
        where T : IComparable<T>, IEquatable<T>
    {
        T Id
        {
            get;
        }
    }
}
