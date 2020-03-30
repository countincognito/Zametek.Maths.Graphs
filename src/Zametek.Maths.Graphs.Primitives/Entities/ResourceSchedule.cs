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
            int finishTime)
        {
            if (scheduledActivities == null)
            {
                throw new ArgumentNullException(nameof(scheduledActivities));
            }
            Resource = resource;
            ScheduledActivities = scheduledActivities.ToList();
            FinishTime = finishTime;
            ActivityAllocation = ExtractActivityAllocation(Resource, ScheduledActivities, FinishTime);
        }

        public ResourceSchedule(
            IEnumerable<IScheduledActivity<T>> scheduledActivities,
            int finishTime)
            : this(null, scheduledActivities, finishTime)
        {
        }

        #endregion

        #region Private Methods

        private static IList<bool> ExtractActivityAllocation(
            IResource<TResourceId> resource,
            IEnumerable<IScheduledActivity<T>> scheduledActivities,
            int finishTime)
        {
            if (scheduledActivities == null)
            {
                throw new ArgumentNullException(nameof(scheduledActivities));
            }
            if (!scheduledActivities.Any())
            {
                return Enumerable.Repeat(false, finishTime).ToList();
            }
            int resourceFinishTime = scheduledActivities.Max(x => x.FinishTime);
            if (resourceFinishTime > finishTime)
            {
                throw new InvalidOperationException($@"Requested finish time ({finishTime}) cannot be less than the actual finish time ({resourceFinishTime})");
            }
            var interActivityAllocationType = InterActivityAllocationType.None;
            if (resource != null)
            {
                interActivityAllocationType = resource.InterActivityAllocationType;
            }

            var distribution = Enumerable.Repeat(TimeType.None, finishTime).ToList();

            // Indirect.
            if (interActivityAllocationType == InterActivityAllocationType.Indirect)
            {
                for (int i = 0; i < distribution.Count; i++)
                {
                    distribution[i] = TimeType.Middle;
                }
                distribution[0] = TimeType.Start;
                distribution[distribution.Count - 1] = TimeType.Finish;
            }
            else if (interActivityAllocationType == InterActivityAllocationType.None
                || interActivityAllocationType == InterActivityAllocationType.Direct)
            {
                // Default (None).
                foreach (IScheduledActivity<T> scheduledActivity in scheduledActivities)
                {
                    for (int timeIndex = scheduledActivity.StartTime; timeIndex < scheduledActivity.FinishTime; timeIndex++)
                    {
                        distribution[timeIndex] = TimeType.Middle;
                    }
                    distribution[scheduledActivity.StartTime] = TimeType.Start;
                    distribution[scheduledActivity.FinishTime - 1] = TimeType.Finish;
                }

                // Direct.
                if (interActivityAllocationType == InterActivityAllocationType.Direct)
                {
                    int firstStartIndex = 0;
                    int lastFinishIndex = distribution.Count - 1;
                    for (int i = 0; i < distribution.Count; i++)
                    {
                        if (distribution[i] == TimeType.Start)
                        {
                            firstStartIndex = i;
                            break;
                        }
                    }
                    for (int i = lastFinishIndex; i >= 0; i--)
                    {
                        if (distribution[i] == TimeType.Finish)
                        {
                            lastFinishIndex = i;
                            break;
                        }
                    }
                    for (int i = firstStartIndex + 1; i < lastFinishIndex; i++)
                    {
                        distribution[i] = TimeType.Middle;
                    }
                }
            }
            else
            {
                throw new InvalidOperationException($@"Unknown InterActivityAllocationType value ({interActivityAllocationType})");
            }

            return distribution.Select(x => x == TimeType.None ? false : true).ToList();
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
                FinishTime);
        }

        #endregion

        #region Private Types

        private enum TimeType
        {
            None,
            Start,
            Middle,
            Finish
        }

        #endregion
    }
}
