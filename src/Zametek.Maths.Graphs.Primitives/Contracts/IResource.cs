using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// Something that performs activities (a person, a machine, an overhead).
    /// Supplied to the compiler to schedule activities onto finite capacity.
    /// </summary>
    /// <typeparam name="T">The resource ID type.</typeparam>
    /// <typeparam name="TWorkStreamId">The work-stream ID type.</typeparam>
    public interface IResource<out T, TWorkStreamId>
        : IHaveId<T>, ICloneObject
        where T : struct, IComparable<T>, IEquatable<T>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        /// <summary>
        /// The display name of the resource.
        /// </summary>
        string? Name { get; }

        /// <summary>
        /// Whether the resource is opt-in only: it picks up an activity only if
        /// that activity names it as a target resource.
        /// </summary>
        bool IsExplicitTarget { get; }

        /// <summary>
        /// Whether the resource is disabled. Inactive resources are filtered out
        /// before scheduling.
        /// </summary>
        bool IsInactive { get; }

        /// <summary>
        /// How the resource's time is spread across the schedule between the
        /// activities it performs.
        /// </summary>
        InterActivityAllocationType InterActivityAllocationType { get; }

        /// <summary>
        /// The cost per time unit of the resource.
        /// </summary>
        double UnitCost { get; }

        /// <summary>
        /// The billing per time unit of the resource.
        /// </summary>
        double UnitBilling { get; }

        /// <summary>
        /// The priority in which resources are offered work; lower values are
        /// filled first.
        /// </summary>
        int AllocationOrder { get; }

        /// <summary>
        /// The IDs of the work-stream phases the resource is associated with
        /// (relevant mainly to indirect resources).
        /// </summary>
        HashSet<TWorkStreamId> InterActivityPhases { get; }
    }
}
