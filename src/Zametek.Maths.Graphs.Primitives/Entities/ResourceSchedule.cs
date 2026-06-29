using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    public class ResourceSchedule<T, TResourceId, TWorkStreamId>
        : IResourceSchedule<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        #region Ctors

        public ResourceSchedule(
            IResource<TResourceId, TWorkStreamId> resource,
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

        public IResource<TResourceId, TWorkStreamId> Resource
        {
            get;
        }

        public IEnumerable<IScheduledActivity<T>> ScheduledActivities
        {
            get;
        }

        public IEnumerable<bool> ResourceAllocation
        {
            get;
        }

        public IEnumerable<bool> CostAllocation
        {
            get;
        }

        public IEnumerable<bool> BillingAllocation
        {
            get;
        }

        public IEnumerable<bool> EffortAllocation
        {
            get;
        }

        public IEnumerable<bool> ActivityAllocation
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
            IResource<TResourceId, TWorkStreamId> resource = null;
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
