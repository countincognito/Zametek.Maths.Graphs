using System.Collections.Generic;

namespace Zametek.Maths.Graphs.Tests
{
    public class DummyResourceSchedule
        : ResourceSchedule<int, int, int>
    {
        public DummyResourceSchedule(Resource<int, int> resource, IEnumerable<ScheduledActivity<int>> scheduledActivities, int finishTime, IEnumerable<bool> activityAllocation)
            : base(resource, scheduledActivities, finishTime, activityAllocation)
        {
        }
    }
}
