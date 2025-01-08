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
            int finishTime,
            IEnumerable<bool> activityAllocation,
            IEnumerable<bool> costAllocation)
        {
            if (scheduledActivities is null)
            {
                throw new ArgumentNullException(nameof(scheduledActivities));
            }
            Resource = resource;
            ScheduledActivities = scheduledActivities.ToList();
            FinishTime = finishTime;
            ActivityAllocation = activityAllocation.ToList();
            CostAllocation = costAllocation.ToList();
        }

        public ResourceSchedule(
            IEnumerable<IScheduledActivity<T>> scheduledActivities,
            int finishTime,
            IEnumerable<bool> activityAllocation,
            IEnumerable<bool> costAllocation)
            : this(null, scheduledActivities, finishTime, activityAllocation, costAllocation)
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

        public IEnumerable<bool> ActivityAllocation
        {
            get;
        }

        public IEnumerable<bool> CostAllocation
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
                FinishTime,
                ActivityAllocation,
                CostAllocation);
        }

        #endregion
    }
}
