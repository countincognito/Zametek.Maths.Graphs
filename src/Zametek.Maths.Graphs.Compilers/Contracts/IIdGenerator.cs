using System;

namespace Zametek.Maths.Graphs
{
    public interface IIdGenerator<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        T Generate();
    }
}
