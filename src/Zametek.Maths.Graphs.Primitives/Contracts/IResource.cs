using System;

namespace Zametek.Maths.Graphs
{
    public interface IResource<out T>
        : IHaveId<T>, ICloneObject
        where T : struct, IComparable<T>, IEquatable<T>
    {
        string Name { get; }

        bool IsExplicitTarget { get; }

        bool IsDisabled { get; }

        InterActivityAllocationType InterActivityAllocationType { get; }

        double UnitCost { get; }

        int AllocationOrder { get; }
    }
}
