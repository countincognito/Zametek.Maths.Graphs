using System;

namespace Zametek.Maths.Graphs
{
    public interface IWorkStream<out T>
        : IHaveId<T>, ICloneObject
        where T : struct, IComparable<T>, IEquatable<T>
    {
        string Name { get; }

        bool IsPhase { get; }
    }
}
