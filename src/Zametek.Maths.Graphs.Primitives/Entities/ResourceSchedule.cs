using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// Default implementation of <see cref="IResourceSchedule{T, TResourceId, TWorkStreamId}"/>.
    /// </summary>
    public class ResourceSchedule<T, TResourceId, TWorkStreamId>
        : IResourceSchedule<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        #region Ctors

        /// <summary>
        /// Creates a schedule for the given resource (null for an unmapped schedule).
        /// </summary>
        public ResourceSchedule(
            IResource<TResourceId, TWorkStreamId>? resource,
            IEnumerable<IScheduledActivity<T>> scheduledActivities,
            int startTime,
            int finishTime,
            IEnumerable<bool> resourceAllocation,
            IEnumerable<bool> costAllocation,
            IEnumerable<bool> billingAllocation,
            IEnumerable<bool> effortAllocation,
            IEnumerable<bool> activityAllocation)
        {
            if (scheduledActivities is null)
            {
                throw new ArgumentNullException(nameof(scheduledActivities));
            }
            Resource = resource;
            ScheduledActivities = scheduledActivities.ToList();
            StartTime = startTime;
            FinishTime = finishTime;
            ResourceAllocation = resourceAllocation.ToList();
            CostAllocation = costAllocation.ToList();
            BillingAllocation = billingAllocation.ToList();
            EffortAllocation = effortAllocation.ToList();
            ActivityAllocation = activityAllocation.ToList();
        }

        /// <summary>
        /// Creates an unmapped schedule with no resource.
        /// </summary>
        public ResourceSchedule(
            IEnumerable<IScheduledActivity<T>> scheduledActivities,
            int startTime,
            int finishTime,
            IEnumerable<bool> resourceAllocation,
            IEnumerable<bool> costAllocation,
            IEnumerable<bool> billingAllocation,
            IEnumerable<bool> effortAllocation,
            IEnumerable<bool> activityAllocation)
            : this(null, scheduledActivities, startTime, finishTime, resourceAllocation, costAllocation, billingAllocation, effortAllocation, activityAllocation)
        {
        }

        #endregion

        #region IResourceSchedule<T> Members

        /// <inheritdoc/>
        public IResource<TResourceId, TWorkStreamId>? Resource
        {
            get;
        }

        /// <inheritdoc/>
        public IEnumerable<IScheduledActivity<T>> ScheduledActivities
        {
            get;
        }

        /// <inheritdoc/>
        public IEnumerable<bool> ResourceAllocation
        {
            get;
        }

        /// <inheritdoc/>
        public IEnumerable<bool> CostAllocation
        {
            get;
        }

        /// <inheritdoc/>
        public IEnumerable<bool> BillingAllocation
        {
            get;
        }

        /// <inheritdoc/>
        public IEnumerable<bool> EffortAllocation
        {
            get;
        }

        /// <inheritdoc/>
        public IEnumerable<bool> ActivityAllocation
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
        public IResourceSchedule<T, TResourceId, TWorkStreamId> Clone()
        {
            return (IResourceSchedule<T, TResourceId, TWorkStreamId>)CloneObject();
        }

        /// <inheritdoc/>
        public object CloneObject()
        {
            IResource<TResourceId, TWorkStreamId>? resource = null;
            if (Resource != null)
            {
                resource = (IResource<TResourceId, TWorkStreamId>)Resource.CloneObject();
            }
            return new ResourceSchedule<T, TResourceId, TWorkStreamId>(
                resource,
                ScheduledActivities.Select(x => (IScheduledActivity<T>)x.CloneObject()),
                StartTime,
                FinishTime,
                ResourceAllocation,
                CostAllocation,
                BillingAllocation,
                EffortAllocation,
                ActivityAllocation);
        }

        #endregion
    }
}
