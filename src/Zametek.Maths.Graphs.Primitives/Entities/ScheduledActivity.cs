﻿using System;

namespace Zametek.Maths.Graphs
{
    public class ScheduledActivity<T>
        : IScheduledActivity<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        #region Ctors

        public ScheduledActivity(T id, string name, bool hasNoCost, bool hasNoEffort, int duration, int startTime, int finishTime)
        {
            Id = id;
            Name = name;
            HasNoCost = hasNoCost;
            HasNoEffort = hasNoEffort;
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

        public bool HasNoCost
        {
            get;
        }

        public bool HasNoEffort
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
        }

        public int FinishTime
        {
            get;
        }

        public object CloneObject()
        {
            return new ScheduledActivity<T>(Id, Name, HasNoCost, HasNoEffort, Duration, StartTime, FinishTime);
        }

        #endregion
    }
}
