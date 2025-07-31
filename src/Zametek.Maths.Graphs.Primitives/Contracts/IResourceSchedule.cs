using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    public interface IResourceSchedule<out T, out TResourceId, TWorkStreamId>
        : ICloneObject
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        IResource<TResourceId, TWorkStreamId> Resource { get; }

        IEnumerable<IScheduledActivity<T>> ScheduledActivities { get; }

        IEnumerable<bool> ActivityAllocation { get; }

        IEnumerable<bool> CostAllocation { get; }

        IEnumerable<bool> BillingAllocation { get; }

        IEnumerable<bool> EffortAllocation { get; }

        int StartTime { get; }

        int FinishTime { get; }
    }
}
