using System;

namespace Zametek.Maths.Graphs
{
    public interface IEvent<T>
        : IHaveId<T>, ICanBeRemoved, IWorkingCopy
        where T : IComparable<T>, IEquatable<T>
    {
        int? EarliestFinishTime
        {
            get;
            set;
        }

        int? LatestFinishTime
        {
            get;
            set;
        }
    }
}
