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

        #region Public Methods

        public void AddActivity(IActivity<T, TResourceId> activity, int startTime)
        {
            if (activity == null)
            {
                throw new ArgumentNullException(nameof(activity));
            }
            if (startTime < EarliestAvailableStartTimeForNextActivity)
            {
                startTime = EarliestAvailableStartTimeForNextActivity;
            }
            var scheduledActivity = new ScheduledActivity<T>(
                activity.Id, activity.Name, activity.HasNoCost,
                activity.Duration, startTime, startTime + activity.Duration);
            m_ScheduledActivities.AddLast(scheduledActivity);
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
            return new ResourceSchedule<T, TResourceId>(m_Resource, m_ScheduledActivities, finishTime);
        }

        #endregion
    }
}
