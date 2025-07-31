using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    public class Activity<T, TResourceId, TWorkStreamId>
        : IActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        #region Ctors

        public Activity(T id, int duration)
        {
            Id = id;
            Duration = duration;
            TargetWorkStreams = new HashSet<TWorkStreamId>();
            TargetResources = new HashSet<TResourceId>();
            AllocatedToResources = new HashSet<TResourceId>();
        }

        public Activity(T id, int duration, bool canBeRemoved)
            : this(id, duration)
        {
            CanBeRemoved = canBeRemoved;
        }

        public Activity(
            T id, string name, string notes, IEnumerable<TWorkStreamId> targetWorkStreams, IEnumerable<TResourceId> targetResources,
            LogicalOperator targetLogicalOperator, IEnumerable<TResourceId> allocatedToResources, bool canBeRemoved, bool hasNoCost, bool hasNoBilling,
            bool hasNoEffort, int duration, int? freeSlack, int? earliestStartTime, int? latestFinishTime, int? minimumFreeSlack, int? minimumEarliestStartTime,
            int? maximumLatestFinishTime)
        {
            if (targetWorkStreams is null)
            {
                throw new ArgumentNullException(nameof(targetWorkStreams));
            }
            if (targetResources is null)
            {
                throw new ArgumentNullException(nameof(targetResources));
            }
            Id = id;
            Name = name;
            Notes = notes;
            TargetWorkStreams = new HashSet<TWorkStreamId>(targetWorkStreams);
            TargetResources = new HashSet<TResourceId>(targetResources);
            TargetResourceOperator = targetLogicalOperator;
            AllocatedToResources = new HashSet<TResourceId>(allocatedToResources);
            CanBeRemoved = canBeRemoved;
            HasNoCost = hasNoCost;
            HasNoBilling = hasNoBilling;
            HasNoEffort = hasNoEffort;
            Duration = duration;
            FreeSlack = freeSlack;
            EarliestStartTime = earliestStartTime;
            LatestFinishTime = latestFinishTime;
            MinimumFreeSlack = minimumFreeSlack;
            MinimumEarliestStartTime = minimumEarliestStartTime;
            MaximumLatestFinishTime = maximumLatestFinishTime;
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

        public string Notes
        {
            get;
            set;
        }

        public HashSet<TWorkStreamId> TargetWorkStreams
        {
            get;
        }

        public HashSet<TResourceId> TargetResources
        {
            get;
        }

        public LogicalOperator TargetResourceOperator
        {
            get;
            set;
        }

        public HashSet<TResourceId> AllocatedToResources
        {
            get;
        }

        public bool CanBeRemoved
        {
            get;
            private set;
        }

        public bool IsDummy => Duration <= 0;

        public bool HasNoCost
        {
            get;
            set;
        }

        public bool HasNoBilling
        {
            get;
            set;
        }

        public bool HasNoEffort
        {
            get;
            set;
        }

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
                return totalSlack <= 0;
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

        public int? MaximumLatestFinishTime
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
            return new Activity<T, TResourceId, TWorkStreamId>(
                Id, Name, Notes, TargetWorkStreams, TargetResources, TargetResourceOperator, AllocatedToResources,
                CanBeRemoved, HasNoCost, HasNoBilling, HasNoEffort, Duration, FreeSlack, EarliestStartTime, LatestFinishTime,
                MinimumFreeSlack, MinimumEarliestStartTime, MaximumLatestFinishTime);
        }

        #endregion
    }
}
