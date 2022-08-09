using System;

namespace Zametek.Maths.Graphs
{
    public interface IInvalidConstraint<T>
        : IHaveId<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        string Message { get; }
    }
}
