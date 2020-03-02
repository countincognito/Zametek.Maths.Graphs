using System;

namespace Zametek.Maths.Graphs
{
    public interface IScheduledActivity<T>
        : IHaveId<T>, ICloneObject
        where T : struct, IComparable<T>, IEquatable<T>
    {
        string Name
        {
            get;
        }

        int Duration
        {
            get;
        }

        int StartTime
        {
            get;
            set;
        }

        int FinishTime
        {
            get;
            set;
        }
    }
}
