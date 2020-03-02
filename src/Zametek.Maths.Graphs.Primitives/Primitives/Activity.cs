using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    public class Activity<T>
        : IActivity<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        #region Ctors

        public Activity(T id, int duration)
        {
            Id = id;
            Duration = duration;
            TargetResources = new HashSet<T>();
        }

        public Activity(T id, int duration, bool canBeRemoved)
            : this(id, duration)
        {
            CanBeRemoved = canBeRemoved;
        }

        public Activity(
            T id, string name, IEnumerable<T> targetResources, LogicalOperator targetLogicalOperator,
            bool canBeRemoved, int duration, int? freeSlack, int? earliestStartTime, int? latestFinishTime,
            int? minimumFreeSlack, int? minimumEarliestStartTime, DateTime? minimumEarliestStartDateTime)
        {
            if (targetResources == null)
            {
                throw new ArgumentNullException(nameof(targetResources));
            }
            Id = id;
            Name = name;
            TargetResources = new HashSet<T>(targetResources);
            TargetResourceOperator = targetLogicalOperator;
            CanBeRemoved = canBeRemoved;
            Duration = duration;
            FreeSlack = freeSlack;
            EarliestStartTime = earliestStartTime;
            LatestFinishTime = latestFinishTime;
            MinimumFreeSlack = minimumFreeSlack;
            MinimumEarliestStartTime = minimumEarliestStartTime;
            MinimumEarliestStartDateTime = minimumEarliestStartDateTime;
        }

        #endregion

        #region IActivity<T> Members

        public T Id
        {
            get;
        }

        public string Name
        {
            get;
            set;
        }

        public HashSet<T> TargetResources
        {
            get;
        }

        public LogicalOperator TargetResourceOperator
        {
            get;
            set;
        }

        public bool CanBeRemoved
        {
            get;
            private set;
        }

        public bool IsDummy => Duration == 0;

        public int Duration
        {
            get;
            set;
        }

        public int? TotalSlack
        {
            get
            {
                int? latestFinishTime = LatestFinishTime;
                int? earliestFinishTime = EarliestFinishTime;
                if (latestFinishTime.HasValue
                    && earliestFinishTime.HasValue)
                {
                    return latestFinishTime.Value - earliestFinishTime.Value;
                }
                return null;
            }
        }

        public int? FreeSlack
        {
            get;
            set;
        }

        public int? InterferingSlack
        {
            get
            {
                int? totalSlack = TotalSlack;
                int? freeSlack = FreeSlack;
                if (totalSlack.HasValue
                    && freeSlack.HasValue)
                {
                    return totalSlack.Value - freeSlack.Value;
                }
                return null;
            }
        }

        public bool IsCritical
        {
            get
            {
                int? totalSlack = TotalSlack;
                return totalSlack == 0;
            }
        }

        public int? EarliestStartTime
        {
            get;
            set;
        }

        public int? LatestStartTime
        {
            get
            {
                int? latestFinishTime = LatestFinishTime;
                int duration = Duration;
                if (latestFinishTime.HasValue)
                {
                    return latestFinishTime.Value - duration;
                }
                return null;
            }
        }

        public int? EarliestFinishTime
        {
            get
            {
                int? earliestStartTime = EarliestStartTime;
                int duration = Duration;
                if (earliestStartTime.HasValue)
                {
                    return earliestStartTime.Value + duration;
                }
                return null;
            }
        }

        public int? LatestFinishTime
        {
            get;
            set;
        }

        public int? MinimumFreeSlack
        {
            get;
            set;
        }

        public int? MinimumEarliestStartTime
        {
            get;
            set;
        }

        public DateTime? MinimumEarliestStartDateTime
        {
            get;
            set;
        }

        public void SetAsReadOnly()
        {
            CanBeRemoved = false;
        }

        public void SetAsRemovable()
        {
            CanBeRemoved = true;
        }

        public virtual object CloneObject()
        {
            return new Activity<T>(
                Id, Name, TargetResources, TargetResourceOperator, CanBeRemoved, Duration, FreeSlack,
                EarliestStartTime, LatestFinishTime, MinimumFreeSlack, MinimumEarliestStartTime,
                MinimumEarliestStartDateTime);
        }

        #endregion
    }
}
