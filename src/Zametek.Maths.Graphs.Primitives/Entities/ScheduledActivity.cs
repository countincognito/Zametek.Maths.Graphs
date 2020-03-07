using System;

namespace Zametek.Maths.Graphs
{
    public class ScheduledActivity<T>
        : IScheduledActivity<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        #region Ctors

        public ScheduledActivity(T id, string name, int duration, int startTime, int finishTime)
        {
            Id = id;
            Name = name;
            Duration = duration;
            StartTime = startTime;
            FinishTime = finishTime;
        }

        #endregion

        #region IScheduledActivity<T> Members

        public T Id
        {
            get;
        }

        public string Name
        {
            get;
        }

        public int Duration
        {
            get;
        }

        public int StartTime
        {
            get;
            set;
        }

        public int FinishTime
        {
            get;
            set;
        }

        public object CloneObject()
        {
            return new ScheduledActivity<T>(Id, Name, Duration, StartTime, FinishTime);
        }

        #endregion
    }
}
