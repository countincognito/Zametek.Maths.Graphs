using Shouldly;
using System;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class ResourceScheduleTests
    {
        private static ResourceSchedule<int, int, int> BuildSchedule(IResource<int, int> resource)
        {
            return new ResourceSchedule<int, int, int>(
                resource,
                [new ScheduledActivity<int>(1, "a", false, false, false, 5, 0, 5)],
                startTime: 0,
                finishTime: 5,
                resourceAllocation: [true, true, true, true, true],
                costAllocation: [true, true, true, false, false],
                billingAllocation: [false, true, true, true, false],
                effortAllocation: [true, false, true, false, true],
                activityAllocation: [true, true, false, false, true]);
        }

        [Fact]
        public void ResourceSchedule_GivenCtor_WithNullScheduledActivities_ThenThrowsArgumentNullException()
        {
            Action act = () => new ResourceSchedule<int, int, int>(
                null, 0, 5,
                [], [], [],
                [], []);
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void ResourceSchedule_GivenCtor_WithoutResource_ThenResourceIsNull()
        {
            var schedule = BuildSchedule(null);

            schedule.Resource.ShouldBeNull();
            schedule.StartTime.ShouldBe(0);
            schedule.FinishTime.ShouldBe(5);
        }

        [Fact]
        public void ResourceSchedule_GivenCloneObject_ThenAllPropertiesPreserved()
        {
            var resource = new Resource<int, int>(
                10, "R1", false, false, InterActivityAllocationType.Direct, 1.0, 1.0, 0, []);
            var schedule = BuildSchedule(resource);

            var clone = (ResourceSchedule<int, int, int>)schedule.CloneObject();

            clone.Resource.Id.ShouldBe(10);
            clone.Resource.ShouldNotBeSameAs(resource);
            clone.ScheduledActivities.Single().Id.ShouldBe(1);
            clone.StartTime.ShouldBe(0);
            clone.FinishTime.ShouldBe(5);
            clone.ResourceAllocation.ShouldBe(schedule.ResourceAllocation);
            clone.CostAllocation.ShouldBe(schedule.CostAllocation);
            clone.BillingAllocation.ShouldBe(schedule.BillingAllocation);
            clone.EffortAllocation.ShouldBe(schedule.EffortAllocation);
            clone.ActivityAllocation.ShouldBe(schedule.ActivityAllocation);
        }

        [Fact]
        public void ResourceSchedule_GivenCloneObject_WhenNoResource_ThenCloneHasNullResource()
        {
            var schedule = BuildSchedule(null);

            var clone = (ResourceSchedule<int, int, int>)schedule.CloneObject();

            clone.Resource.ShouldBeNull();
        }
    }
}
