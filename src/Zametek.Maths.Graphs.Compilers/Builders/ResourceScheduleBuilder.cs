using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    public class ResourceScheduleBuilder<T, TResourceId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
    {
        #region Fields

        private readonly IResource<TResourceId> m_Resource;
        private readonly LinkedList<IScheduledActivity<T>> m_ScheduledActivities;

        #endregion

        #region Ctors

        public ResourceScheduleBuilder(IResource<TResourceId> resource)
            : this()
        {
            m_Resource = resource ?? throw new ArgumentNullException(nameof(resource));
        }

        public ResourceScheduleBuilder()
        {
            m_ScheduledActivities = new LinkedList<IScheduledActivity<T>>();
        }

        #endregion

        #region Properties

        public TResourceId? ResourceId => m_Resource?.Id;

        public bool IsExplicitTarget => m_Resource != null ? m_Resource.IsExplicitTarget : false;

        public IEnumerable<IScheduledActivity<T>> ScheduledActivities => m_ScheduledActivities.ToList();

        public int LastActivityFinishTime
        {
            get
            {
                if (!m_ScheduledActivities.Any())
                {
                    return 0;
                }
                return m_ScheduledActivities.Last.Value.FinishTime;
            }
        }

        public int EarliestAvailableStartTimeForNextActivity => LastActivityFinishTime;

        #endregion

        #region Private Methods

        private static IList<bool> ExtractActivityAllocation(
            IResource<TResourceId> resource,
            IEnumerable<IScheduledActivity<T>> scheduledActivities,
            int finishTime)
        {
            if (scheduledActivities is null)
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

            List<TimeType> distribution = Enumerable.Repeat(TimeType.None, finishTime).ToList();

            // Indirect.
            // For indirect we basically mark the entire time span as costed, from start to finish.
            if (interActivityAllocationType == InterActivityAllocationType.Indirect)
            {
                for (int i = 0; i < distribution.Count; i++)
                {
                    distribution[i] = TimeType.Middle;
                }
                distribution[0] = TimeType.Start;
                distribution[distribution.Count - 1] = TimeType.Finish;
            }
            else if (interActivityAllocationType == InterActivityAllocationType.None)
            {
                // None.
                // Mark schedules as normal, unless they need to be ignored.
                foreach (IScheduledActivity<T> scheduledActivity in scheduledActivities)
                {
                    if (scheduledActivity.HasNoCost)
                    {
                        for (int timeIndex = scheduledActivity.StartTime; timeIndex < scheduledActivity.FinishTime; timeIndex++)
                        {
                            distribution[timeIndex] = TimeType.Ignored;
                        }
                    }
                    else
                    {
                        for (int timeIndex = scheduledActivity.StartTime; timeIndex < scheduledActivity.FinishTime; timeIndex++)
                        {
                            distribution[timeIndex] = TimeType.Middle;
                        }
                        distribution[scheduledActivity.StartTime] = TimeType.Start;
                        distribution[scheduledActivity.FinishTime - 1] = TimeType.Finish;
                    }
                }
            }
            else if (interActivityAllocationType == InterActivityAllocationType.Direct)
            {
                // Direct.
                // Mark schedules as normal.
                foreach (IScheduledActivity<T> scheduledActivity in scheduledActivities)
                {
                    for (int timeIndex = scheduledActivity.StartTime; timeIndex < scheduledActivity.FinishTime; timeIndex++)
                    {
                        distribution[timeIndex] = TimeType.Middle;
                    }
                    distribution[scheduledActivity.StartTime] = TimeType.Start;
                    distribution[scheduledActivity.FinishTime - 1] = TimeType.Finish;
                }

                // Find the first Start and the last Finish, then fill in the gaps between them.
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

                // Now mark the uncosted areas
                foreach (IScheduledActivity<T> scheduledActivity in scheduledActivities)
                {
                    if (scheduledActivity.HasNoCost)
                    {
                        for (int timeIndex = scheduledActivity.StartTime; timeIndex < scheduledActivity.FinishTime; timeIndex++)
                        {
                            distribution[timeIndex] = TimeType.Ignored;
                        }
                    }
                }
            }
            else
            {
                throw new InvalidOperationException($@"Unknown InterActivityAllocationType value ({interActivityAllocationType})");
            }

            return distribution.Select(x => x == TimeType.None || x == TimeType.Ignored ? false : true).ToList();
        }

        private void AddActivity(IScheduledActivity<T> scheduledActivity)
        {
            if (scheduledActivity is null)
            {
                throw new ArgumentNullException(nameof(scheduledActivity));
            }

            m_ScheduledActivities.AddLast(scheduledActivity);
        }

        #endregion

        #region Public Methods

        public void AppendActivity(IScheduledActivity<T> scheduledActivity)
        {
            if (scheduledActivity is null)
            {
                throw new ArgumentNullException(nameof(scheduledActivity));
            }
            int earliestAvailableStartTimeForNextActivity = EarliestAvailableStartTimeForNextActivity;
            if (scheduledActivity.StartTime < earliestAvailableStartTimeForNextActivity)
            {
                throw new InvalidOperationException($@"Scheduled activity's start time {scheduledActivity.StartTime} is less than the earliest available start time for the next activity {earliestAvailableStartTimeForNextActivity}");
            }
            AppendActivityWithoutChecks(scheduledActivity);
        }

        public void AppendActivityWithoutChecks(IScheduledActivity<T> scheduledActivity)
        {
            if (scheduledActivity is null)
            {
                throw new ArgumentNullException(nameof(scheduledActivity));
            }
            AddActivity(scheduledActivity);
        }

        public void AppendActivity(IActivity<T, TResourceId> activity, int startTime)
        {
            if (activity is null)
            {
                throw new ArgumentNullException(nameof(activity));
            }
            if (startTime < EarliestAvailableStartTimeForNextActivity)
            {
                startTime = EarliestAvailableStartTimeForNextActivity;
            }
            AppendActivityWithoutChecks(activity, startTime);
        }

        public void AppendActivityWithoutChecks(IActivity<T, TResourceId> activity, int startTime)
        {
            if (activity is null)
            {
                throw new ArgumentNullException(nameof(activity));
            }
            var scheduledActivity = new ScheduledActivity<T>(
                activity.Id, activity.Name, activity.HasNoCost,
                activity.Duration, startTime, startTime + activity.Duration);
            AddActivity(scheduledActivity);
        }

        public void ClearActivities()
        {
            m_ScheduledActivities.Clear();
        }

        public T? ActivityAt(int time)
        {
            foreach (IScheduledActivity<T> scheduledActivity in m_ScheduledActivities)
            {
                if (time >= scheduledActivity.StartTime
                    && time < scheduledActivity.FinishTime)
                {
                    return scheduledActivity.Id;
                }
            }
            return null;
        }

        public IResourceSchedule<T, TResourceId> ToResourceSchedule(int finishTime)
        {
            return new ResourceSchedule<T, TResourceId>(
                m_Resource,
                m_ScheduledActivities,
                finishTime,
                ExtractActivityAllocation(m_Resource, m_ScheduledActivities, finishTime));
        }

        #endregion

        #region Private Types

        private enum TimeType
        {
            None,
            Ignored,
            Start,
            Middle,
            Finish
        }

        #endregion
    }
}
