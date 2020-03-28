using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    public interface IResourceSchedule<out T>
        : ICloneObject
        where T : struct, IComparable<T>, IEquatable<T>
    {
        IResource<T> Resource { get; }

        IEnumerable<IScheduledActivity<T>> ScheduledActivities { get; }

        IEnumerable<bool> ActivityAllocation { get; }

        int FinishTime { get; }
    }
}
