using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// Default implementation of <see cref="IActivity{T, TResourceId, TWorkStreamId}"/>.
    /// </summary>
    public class Activity<T, TResourceId, TWorkStreamId>
        : IActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        #region Ctors

        /// <summary>
        /// Creates an activity with the given ID and duration.
        /// </summary>
        public Activity(T id, int duration)
        {
            Id = id;
            Duration = duration;
            TargetWorkStreams = new HashSet<TWorkStreamId>();
            TargetResources = new HashSet<TResourceId>();
            AllocatedToResources = new HashSet<TResourceId>();
        }

        /// <summary>
        /// Creates an activity with the given ID, duration and removability flag.
        /// </summary>
        public Activity(T id, int duration, bool canBeRemoved)
            : this(id, duration)
        {
            CanBeRemoved = canBeRemoved;
        }

        /// <summary>
        /// Creates a fully-specified activity (used by cloning).
        /// </summary>
        public Activity(
            T id, string? name, string? notes, IEnumerable<TWorkStreamId> targetWorkStreams, IEnumerable<TResourceId> targetResources,
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

        /// <inheritdoc/>
        public T Id
        {
            get;
        }

        /// <inheritdoc/>
        public string? Name
        {
            get;
            set;
        }

        /// <inheritdoc/>
        public string? Notes
        {
            get;
            set;
        }

        /// <inheritdoc/>
        public HashSet<TWorkStreamId> TargetWorkStreams
        {
            get;
        }

        /// <inheritdoc/>
        public HashSet<TResourceId> TargetResources
        {
            get;
        }

        /// <inheritdoc/>
        public LogicalOperator TargetResourceOperator
        {
            get;
            set;
        }

        /// <inheritdoc/>
        public HashSet<TResourceId> AllocatedToResources
        {
            get;
        }

        /// <inheritdoc/>
        public bool CanBeRemoved
        {
            get;
            private set;
        }

        /// <inheritdoc/>
        public bool IsDummy => Duration <= 0;

        /// <inheritdoc/>
        public bool HasNoCost
        {
            get;
            set;
        }

        /// <inheritdoc/>
        public bool HasNoBilling
        {
            get;
            set;
        }

        /// <inheritdoc/>
        public bool HasNoEffort
        {
            get;
            set;
        }

        /// <inheritdoc/>
        public int Duration
        {
            get;
            set;
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public int? FreeSlack
        {
            get;
            set;
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public bool IsCritical
        {
            get
            {
                int? totalSlack = TotalSlack;
                return totalSlack.HasValue && totalSlack.Value <= 0;
            }
        }

        /// <inheritdoc/>
        public int? EarliestStartTime
        {
            get;
            set;
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public int? LatestFinishTime
        {
            get;
            set;
        }

        /// <inheritdoc/>
        public int? MinimumFreeSlack
        {
            get;
            set;
        }

        /// <inheritdoc/>
        public int? MinimumEarliestStartTime
        {
            get;
            set;
        }

        /// <inheritdoc/>
        public int? MaximumLatestFinishTime
        {
            get;
            set;
        }

        /// <inheritdoc/>
        public void SetAsReadOnly()
        {
            CanBeRemoved = false;
        }

        /// <inheritdoc/>
        public void SetAsRemovable()
        {
            CanBeRemoved = true;
        }

        /// <inheritdoc/>
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
