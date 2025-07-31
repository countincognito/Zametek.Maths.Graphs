using System.Collections.Generic;

namespace Zametek.Maths.Graphs.Tests
{
    public class DummyResourceSchedule
        : ResourceSchedule<int, int, int>
    {
        public DummyResourceSchedule(Resource<int, int> resource, IEnumerable<ScheduledActivity<int>> scheduledActivities, int startTime, int finishTime, IEnumerable<bool> activityAllocation, IEnumerable<bool> costAllocation, IEnumerable<bool> billingAllocation, IEnumerable<bool> effortAllocation)
            : base(resource, scheduledActivities, startTime, finishTime, activityAllocation, costAllocation, billingAllocation, effortAllocation)
        {
        }

        public ResourceSchedule<int, int, int> AsBase()
        {
            return CloneObject() as ResourceSchedule<int, int, int>;
        }
    }
}
