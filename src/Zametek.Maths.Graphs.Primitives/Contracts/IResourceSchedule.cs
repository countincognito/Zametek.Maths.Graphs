using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    public interface IResourceSchedule<out T, out TResourceId>
        : ICloneObject
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
    {
        IResource<TResourceId> Resource { get; }

        IEnumerable<IScheduledActivity<T>> ScheduledActivities { get; }

        IEnumerable<bool> ActivityAllocation { get; }

        int FinishTime { get; }
    }
}
