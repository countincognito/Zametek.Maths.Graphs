using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    public class ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        #region Fields

        private readonly IResource<TResourceId, TWorkStreamId> m_Resource;
        private readonly LinkedList<IScheduledActivity<T>> m_ScheduledActivities;

        #endregion

        #region Ctors

        public ResourceScheduleBuilder(IResource<TResourceId, TWorkStreamId> resource)
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

        public bool IsExplicitTarget => m_Resource != null && m_Resource.IsExplicitTarget;

        public bool IsInactive => m_Resource != null && m_Resource.IsInactive;

        public IEnumerable<IScheduledActivity<T>> ScheduledActivities => m_ScheduledActivities.ToList();

        public int LastActivityFinishTime
        {
            get
            {
                if (m_ScheduledActivities.Count == 0)
                {
                    return 0;
                }
                return m_ScheduledActivities.Last.Value.FinishTime;
            }
        }

        public int EarliestAvailableStartTimeForNextActivity => LastActivityFinishTime;

        #endregion

        #region Private Methods

        private static (IList<bool> activityAllocation, IList<bool> costAllocation, IList<bool> effortAllocation) ExtractAllocations(
            IResource<TResourceId, TWorkStreamId> resource,
            IEnumerable<IScheduledActivity<T>> scheduledActivities,
            IEnumerable<IActivity<T, TResourceId, TWorkStreamId>> activities,
            int finishTime)
        {
            if (scheduledActivities is null)
            {
                throw new ArgumentNullException(nameof(scheduledActivities));
            }
            if (activities is null)
            {
                throw new ArgumentNullException(nameof(activities));
            }
            int resourceFinishTime = scheduledActivities.Select(x => x.FinishTime).DefaultIfEmpty().Max();
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
            if (interActivityAllocationType == InterActivityAllocationType.Indirect)
            {
                AllocationForIndirectType(resource, activities, scheduledActivities, distribution);
                AllocationForScheduledActivitiesType(scheduledActivities, distribution);
            }
            // None.
            else if (interActivityAllocationType == InterActivityAllocationType.None)
            {
                AllocationForNoneType(scheduledActivities, distribution);
                AllocationForNoCostOrEffortActivities(scheduledActivities, distribution);
            }
            // Direct.
            else if (interActivityAllocationType == InterActivityAllocationType.Direct)
            {
                AllocationForScheduledActivitiesType(scheduledActivities, distribution);
                AllocationForNoCostOrEffortActivities(scheduledActivities, distribution);
            }
            else
            {
                throw new InvalidOperationException($@"Unknown InterActivityAllocationType value ({interActivityAllocationType})");
            }

            var activityAllocation = distribution.Select(
                x => x != TimeType.None)
                .ToList();
            var costAllocation = distribution.Select(
                x => x != TimeType.None && !x.HasFlag(TimeType.CostIgnored))
                .ToList();
            var effortAllocation = distribution.Select(
                x => x != TimeType.None && !x.HasFlag(TimeType.EffortIgnored))
                .ToList();

            return (activityAllocation, costAllocation, effortAllocation);
        }

        private static void AllocationForIndirectType(
            IResource<TResourceId, TWorkStreamId> resource,
            IEnumerable<IActivity<T, TResourceId, TWorkStreamId>> activities,
            IEnumerable<IScheduledActivity<T>> scheduledActivities,
            IList<TimeType> distribution)
        {
            if (resource is null)
            {
                throw new ArgumentNullException(nameof(resource));
            }
            if (activities is null)
            {
                throw new ArgumentNullException(nameof(activities));
            }
            if (scheduledActivities is null)
            {
                throw new ArgumentNullException(nameof(scheduledActivities));
            }
            if (distribution is null)
            {
                throw new ArgumentNullException(nameof(distribution));
            }

            if (distribution.Count == 0)
            {
                return;
            }

            int latestActivityFinishTime = scheduledActivities.Select(x => x.FinishTime).DefaultIfEmpty().Max();
            if (distribution.Count < latestActivityFinishTime)
            {
                throw new InvalidOperationException($@"Distribution length ({distribution.Count}) cannot be less than latest activity finish time ({latestActivityFinishTime})");
            }

            // If the type is Indirect, then the resource must exist.

            HashSet<TWorkStreamId> resourcePhases = resource.InterActivityPhases.Distinct().ToHashSet();

            // If the resource has no phases then assume the default and mark the
            // entire time span as costed, from start to finish
            if (resourcePhases.Count == 0)
            {
                for (int i = 0; i < distribution.Count; i++)
                {
                    distribution[i] |= TimeType.Middle;
                }
                distribution[0] |= TimeType.Start;
                distribution[^1] |= TimeType.Finish;
            }
            // Otherwise, we have to go through each activity and find where the
            // associated phases start and end.
            else
            {
                // Find the range for each resource phase (phased work stream).
                HashSet<TWorkStreamId> workstreamsUsed = activities.SelectMany(x => x.TargetWorkStreams).Distinct().ToHashSet();

                HashSet<TWorkStreamId> resourcePhasesUsed = resourcePhases.Intersect(workstreamsUsed).ToHashSet();

                List<IActivity<T, TResourceId, TWorkStreamId>> orderedActivities =
                    activities.OrderBy(x => x.EarliestStartTime).ThenBy(x => x.LatestStartTime).ToList();

                var resourcePhaseStarts = new Dictionary<TWorkStreamId, int>();
                var resourcePhaseEnds = new Dictionary<TWorkStreamId, int>();

                foreach (IActivity<T, TResourceId, TWorkStreamId> activity in orderedActivities)
                {
                    foreach (TWorkStreamId workStream in activity.TargetWorkStreams.Where(resourcePhasesUsed.Contains))
                    {
                        int earliestStartTime = activity.EarliestStartTime.GetValueOrDefault();
                        int earliestEndTime = activity.EarliestFinishTime.GetValueOrDefault();

                        // Gather the start times.
                        if (resourcePhaseStarts.ContainsKey(workStream))
                        {
                            // We do nothing here, since the activities are ordered
                            // then we won't be interested in any later start times.
                        }
                        else
                        {
                            resourcePhaseStarts.Add(workStream, earliestStartTime);
                        }

                        // Gather the end times.
                        if (resourcePhaseEnds.ContainsKey(workStream))
                        {
                            int currentEndTime = resourcePhaseEnds[workStream];
                            if (earliestEndTime > currentEndTime)
                            {
                                resourcePhaseEnds[workStream] = earliestEndTime;
                            }
                        }
                        else
                        {
                            resourcePhaseEnds.Add(workStream, earliestEndTime);
                        }
                    }
                }

                // Check to make sure the key collections are the same.
                if (!resourcePhaseStarts.Keys.SequenceEqual(resourcePhaseEnds.Keys))
                {
                    throw new InvalidOperationException($@"Keys for phase starting points does not match the keys for phase ending points for resouce {resource.Id}.");
                }

                // Now we find the earliest start and the latest end and use those
                // to mark out the full range.

                int startTime = resourcePhaseStarts.Values.DefaultIfEmpty().Min();
                int endTime = resourcePhaseEnds.Values.DefaultIfEmpty().Max();

                // If start and end times are both 0 then that means the
                // specific phase was never used, so just leave the allocations
                // as 'ignore'.
                if (startTime != 0 || endTime != 0)
                {
                    for (int timeIndex = startTime; timeIndex < endTime; timeIndex++)
                    {
                        distribution[timeIndex] |= TimeType.Middle;
                    }

                    int startIndex = startTime;
                    int finishIndex = endTime - 1;

                    if (startIndex == finishIndex)
                    {
                        distribution[startIndex] |= TimeType.Start | TimeType.Finish;
                    }
                    else
                    {
                        distribution[startIndex] |= TimeType.Start;
                        distribution[finishIndex] |= TimeType.Finish;
                    }
                }
            }
        }

        private static void AllocationForScheduledActivitiesType(
            IEnumerable<IScheduledActivity<T>> scheduledActivities,
            IList<TimeType> distribution)
        {
            if (scheduledActivities is null)
            {
                throw new ArgumentNullException(nameof(scheduledActivities));
            }
            if (distribution is null)
            {
                throw new ArgumentNullException(nameof(distribution));
            }

            int latestActivityFinishTime = scheduledActivities.Select(x => x.FinishTime).DefaultIfEmpty().Max();
            if (distribution.Count < latestActivityFinishTime)
            {
                throw new InvalidOperationException($@"Distribution length ({distribution.Count}) cannot be less than latest activity finish time ({latestActivityFinishTime})");
            }

            // Mark schedules as normal.
            foreach (IScheduledActivity<T> scheduledActivity in scheduledActivities)
            {
                for (int timeIndex = scheduledActivity.StartTime; timeIndex < scheduledActivity.FinishTime; timeIndex++)
                {
                    distribution[timeIndex] |= TimeType.Middle;
                }

                int startIndex = scheduledActivity.StartTime;
                int finishIndex = scheduledActivity.FinishTime - 1;

                if (startIndex == finishIndex)
                {
                    distribution[startIndex] |= TimeType.Start | TimeType.Finish;
                }
                else
                {
                    distribution[startIndex] |= TimeType.Start;
                    distribution[finishIndex] |= TimeType.Finish;
                }
            }

            // Find the first Start and the last Finish, then fill in the gaps between them.
            int firstStartIndex = 0;
            int lastFinishIndex = distribution.Count - 1;

            bool startFound = false;
            for (int i = 0; i < distribution.Count; i++)
            {
                if (distribution[i].HasFlag(TimeType.Start)
                    || distribution[i].HasFlag(TimeType.Finish))
                {
                    firstStartIndex = i;
                    startFound = true;
                    break;
                }
            }

            bool endFound = false;
            for (int i = lastFinishIndex; i >= 0; i--)
            {
                if (distribution[i].HasFlag(TimeType.Start)
                    || distribution[i].HasFlag(TimeType.Finish))
                {
                    lastFinishIndex = i;
                    endFound = true;
                    break;
                }
            }

            if (startFound
                || endFound)
            {
                for (int i = firstStartIndex + 1; i < lastFinishIndex; i++)
                {
                    distribution[i] |= TimeType.Middle;
                }
            }
        }

        private static void AllocationForNoneType(
            IEnumerable<IScheduledActivity<T>> scheduledActivities,
            IList<TimeType> distribution)
        {
            if (scheduledActivities is null)
            {
                throw new ArgumentNullException(nameof(scheduledActivities));
            }
            if (distribution is null)
            {
                throw new ArgumentNullException(nameof(distribution));
            }

            int latestActivityFinishTime = scheduledActivities.Select(x => x.FinishTime).DefaultIfEmpty().Max();
            if (distribution.Count < latestActivityFinishTime)
            {
                throw new InvalidOperationException($@"Distribution length ({distribution.Count}) cannot be less than latest activity finish time ({latestActivityFinishTime})");
            }

            // Mark schedules as normal.
            foreach (IScheduledActivity<T> scheduledActivity in scheduledActivities)
            {
                for (int timeIndex = scheduledActivity.StartTime; timeIndex < scheduledActivity.FinishTime; timeIndex++)
                {
                    distribution[timeIndex] |= TimeType.Middle;
                }

                int startIndex = scheduledActivity.StartTime;
                int finishIndex = scheduledActivity.FinishTime - 1;

                if (startIndex == finishIndex)
                {
                    distribution[startIndex] |= TimeType.Start | TimeType.Finish;
                }
                else
                {
                    distribution[startIndex] |= TimeType.Start;
                    distribution[finishIndex] |= TimeType.Finish;
                }
            }
        }

        private static void AllocationForNoCostOrEffortActivities(
            IEnumerable<IScheduledActivity<T>> scheduledActivities,
            IList<TimeType> distribution)
        {
            if (scheduledActivities is null)
            {
                throw new ArgumentNullException(nameof(scheduledActivities));
            }
            if (distribution is null)
            {
                throw new ArgumentNullException(nameof(distribution));
            }

            int latestActivityFinishTime = scheduledActivities.Select(x => x.FinishTime).DefaultIfEmpty().Max();
            if (distribution.Count < latestActivityFinishTime)
            {
                throw new InvalidOperationException($@"Distribution length ({distribution.Count}) cannot be less than latest activity finish time ({latestActivityFinishTime})");
            }

            // Now mark the uncosted areas
            foreach (IScheduledActivity<T> scheduledActivity in scheduledActivities)
            {
                if (scheduledActivity.HasNoCost)
                {
                    for (int timeIndex = scheduledActivity.StartTime; timeIndex < scheduledActivity.FinishTime; timeIndex++)
                    {
                        distribution[timeIndex] |= TimeType.CostIgnored;
                    }
                }
                if (scheduledActivity.HasNoEffort)
                {
                    for (int timeIndex = scheduledActivity.StartTime; timeIndex < scheduledActivity.FinishTime; timeIndex++)
                    {
                        distribution[timeIndex] |= TimeType.EffortIgnored;
                    }
                }
            }
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

        public void AppendActivity(IActivity<T, TResourceId, TWorkStreamId> activity, int startTime)
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

        public void AppendActivityWithoutChecks(IActivity<T, TResourceId, TWorkStreamId> activity, int startTime)
        {
            if (activity is null)
            {
                throw new ArgumentNullException(nameof(activity));
            }
            var scheduledActivity = new ScheduledActivity<T>(
                activity.Id, activity.Name, activity.HasNoCost, activity.HasNoEffort,
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

        public IResourceSchedule<T, TResourceId, TWorkStreamId> ToResourceSchedule(
            IEnumerable<IActivity<T, TResourceId, TWorkStreamId>> activities,
            int finishTime)
        {
            if (activities == null)
            {
                throw new ArgumentNullException(nameof(activities));
            }

            (IList<bool> activityAllocation, IList<bool> costAllocation, IList<bool> effortAllocation) =
                ExtractAllocations(m_Resource, m_ScheduledActivities, activities, finishTime);

            return new ResourceSchedule<T, TResourceId, TWorkStreamId>(
                m_Resource,
                m_ScheduledActivities,
                finishTime,
                activityAllocation,
                costAllocation,
                effortAllocation);
        }

        #endregion

        #region Private Types

        [Flags]
        private enum TimeType
        {
            None = 0,
            CostIgnored = 1 << 0,
            EffortIgnored = 1 << 1,
            Start = 1 << 2,
            Middle = 1 << 3,
            Finish = 1 << 4,
        }

        #endregion
    }
}
