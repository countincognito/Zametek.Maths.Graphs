using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    public interface IResourceSchedule<T>
        : IWorkingCopy
        where T : struct, IComparable<T>, IEquatable<T>
    {
        IResource<T> Resource
        {
            get;
        }

        IList<IScheduledActivity<T>> ScheduledActivities
        {
            get;
        }

        IList<bool> ActivityAllocation
        {
            get;
        }

        int FinishTime
        {
            get;
        }
    }
}
