using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// The compiled timeline of a single resource: the activities scheduled onto
    /// it, and the per-time-unit allocation streams derived from them.
    /// </summary>
    /// <typeparam name="T">The activity ID type.</typeparam>
    /// <typeparam name="TResourceId">The resource ID type.</typeparam>
    /// <typeparam name="TWorkStreamId">The work-stream ID type.</typeparam>
    public interface IResourceSchedule<out T, out TResourceId, TWorkStreamId>
        : ICloneObject<IResourceSchedule<T, TResourceId, TWorkStreamId>>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        /// <summary>
        /// The resource the schedule belongs to, or null for an unmapped
        /// (synthetic) schedule created under infinite resources.
        /// </summary>
        IResource<TResourceId, TWorkStreamId>? Resource { get; }

        /// <summary>
        /// The activities scheduled onto the resource, with their scheduled start
        /// and finish times.
        /// </summary>
        IEnumerable<IScheduledActivity<T>> ScheduledActivities { get; }

        /// <summary>
        /// Per-time-unit flags indicating when the resource is allocated to the
        /// project (includes inter-activity spreading for indirect resources).
        /// </summary>
        IEnumerable<bool> ResourceAllocation { get; }

        /// <summary>
        /// Per-time-unit flags indicating when the resource incurs cost.
        /// </summary>
        IEnumerable<bool> CostAllocation { get; }

        /// <summary>
        /// Per-time-unit flags indicating when the resource incurs billing.
        /// </summary>
        IEnumerable<bool> BillingAllocation { get; }

        /// <summary>
        /// Per-time-unit flags indicating when the resource expends effort.
        /// </summary>
        IEnumerable<bool> EffortAllocation { get; }

        /// <summary>
        /// Per-time-unit flags indicating when the resource is actively working
        /// on scheduled activities.
        /// </summary>
        IEnumerable<bool> ActivityAllocation { get; }

        /// <summary>
        /// The time the schedule starts.
        /// </summary>
        int StartTime { get; }

        /// <summary>
        /// The time the schedule finishes.
        /// </summary>
        int FinishTime { get; }
    }
}
