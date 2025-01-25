using System.Collections.Generic;

namespace Zametek.Maths.Graphs.Tests
{
    public class DummyResourceSchedule
        : ResourceSchedule<int, int, int>
    {
        public DummyResourceSchedule(Resource<int, int> resource, IEnumerable<ScheduledActivity<int>> scheduledActivities, int finishTime, IEnumerable<bool> activityAllocation, IEnumerable<bool> costAllocation, IEnumerable<bool> effortAllocation)
            : base(resource, scheduledActivities, finishTime, activityAllocation, costAllocation, effortAllocation)
        {
        }

        public ResourceSchedule<int, int, int> AsBase()
        {
            return CloneObject() as ResourceSchedule<int, int, int>;
        }
    }
}
