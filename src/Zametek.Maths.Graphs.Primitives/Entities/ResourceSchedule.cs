using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    public class ResourceSchedule<T, TResourceId>
        : IResourceSchedule<T, TResourceId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
    {
        #region Ctors

        public ResourceSchedule(
            IResource<TResourceId> resource,
            IEnumerable<IScheduledActivity<T>> scheduledActivities,
            int finishTime,
            IEnumerable<bool> activityAllocation)
        {
            if (scheduledActivities is null)
            {
                throw new ArgumentNullException(nameof(scheduledActivities));
            }
            Resource = resource;
            ScheduledActivities = scheduledActivities.ToList();
            FinishTime = finishTime;
            ActivityAllocation = activityAllocation.ToList();
        }

        public ResourceSchedule(
            IEnumerable<IScheduledActivity<T>> scheduledActivities,
            int finishTime,
            IEnumerable<bool> activityAllocation)
            : this(null, scheduledActivities, finishTime, activityAllocation)
        {
        }

        #endregion

        #region IResourceSchedule<T> Members

        public IResource<TResourceId> Resource
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

        public int FinishTime
        {
            get;
        }

        public object CloneObject()
        {
            IResource<TResourceId> resource = null;
            if (Resource != null)
            {
                resource = (IResource<TResourceId>)Resource.CloneObject();
            }
            return new ResourceSchedule<T, TResourceId>(
                resource,
                ScheduledActivities.Select(x => (IScheduledActivity<T>)x.CloneObject()),
                FinishTime,
                ActivityAllocation);
        }

        #endregion
    }
}
