using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    public interface IResource<out T, TWorkStreamId>
        : IHaveId<T>, ICloneObject
        where T : struct, IComparable<T>, IEquatable<T>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        string Name { get; }

        bool IsExplicitTarget { get; }

        bool IsInactive { get; }

        InterActivityAllocationType InterActivityAllocationType { get; }

        double UnitCost { get; }

        double UnitBilling { get; }

        int AllocationOrder { get; }

        HashSet<TWorkStreamId> InterActivityPhases { get; }
    }
}
