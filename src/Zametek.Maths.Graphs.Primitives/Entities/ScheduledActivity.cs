using System;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// Default implementation of <see cref="IScheduledActivity{T}"/>.
    /// </summary>
    public class ScheduledActivity<T>
        : IScheduledActivity<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        #region Ctors

        /// <summary>
        /// Creates a scheduled snapshot of an activity.
        /// </summary>
        public ScheduledActivity(T id, string? name, bool hasNoCost, bool hasNoBilling, bool hasNoEffort, int duration, int startTime, int finishTime)
        {
            Id = id;
            Name = name;
            HasNoCost = hasNoCost;
            HasNoBilling = hasNoBilling;
            HasNoEffort = hasNoEffort;
            Duration = duration;
            StartTime = startTime;
            FinishTime = finishTime;
        }

        #endregion

        #region IScheduledActivity<T> Members

        /// <inheritdoc/>
        public T Id
        {
            get;
        }

        /// <inheritdoc/>
        public string? Name
        {
            get;
        }

        /// <inheritdoc/>
        public bool HasNoCost
        {
            get;
        }

        /// <inheritdoc/>
        public bool HasNoBilling
        {
            get;
        }

        /// <inheritdoc/>
        public bool HasNoEffort
        {
            get;
        }

        /// <inheritdoc/>
        public int Duration
        {
            get;
        }

        /// <inheritdoc/>
        public int StartTime
        {
            get;
        }

        /// <inheritdoc/>
        public int FinishTime
        {
            get;
        }

        /// <inheritdoc/>
        public IScheduledActivity<T> Clone()
        {
            return (IScheduledActivity<T>)CloneObject();
        }

        /// <inheritdoc/>
        public object CloneObject()
        {
            return new ScheduledActivity<T>(Id, Name, HasNoCost, HasNoBilling, HasNoEffort, Duration, StartTime, FinishTime);
        }

        #endregion
    }
}
