using System.Collections.Generic;

namespace Zametek.Maths.Graphs.Tests
{
    public class DummyResourceSchedule
        : ResourceSchedule<int, int, int>
    {
        public DummyResourceSchedule(Resource<int, int> resource, IEnumerable<ScheduledActivity<int>> scheduledActivities, int startTime, int finishTime, IEnumerable<bool> resourceAllocation, IEnumerable<bool> costAllocation, IEnumerable<bool> billingAllocation, IEnumerable<bool> effortAllocation, IEnumerable<bool> activityAllocation)
            : base(resource, scheduledActivities, startTime, finishTime, resourceAllocation, costAllocation, billingAllocation, effortAllocation, activityAllocation)
        {
        }

        public ResourceSchedule<int, int, int> AsBase()
        {
            return CloneObject() as ResourceSchedule<int, int, int>;
        }
    }
}
