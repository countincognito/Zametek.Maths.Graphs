using Shouldly;
using System;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class ScheduledActivityTests
    {
        [Fact]
        public void ScheduledActivity_GivenCtor_ThenPropertiesSet()
        {
            var scheduled = new ScheduledActivity<int>(1, "name", true, true, true, 5, 3, 8);

            scheduled.Id.ShouldBe(1);
            scheduled.Name.ShouldBe("name");
            scheduled.HasNoCost.ShouldBeTrue();
            scheduled.HasNoBilling.ShouldBeTrue();
            scheduled.HasNoEffort.ShouldBeTrue();
            scheduled.Duration.ShouldBe(5);
            scheduled.StartTime.ShouldBe(3);
            scheduled.FinishTime.ShouldBe(8);
        }

        [Fact]
        public void ScheduledActivity_GivenCloneObject_ThenAllPropertiesPreserved()
        {
            var scheduled = new ScheduledActivity<int>(1, "name", false, true, false, 5, 3, 8);

            var clone = (ScheduledActivity<int>)scheduled.CloneObject();

            clone.Id.ShouldBe(1);
            clone.Name.ShouldBe("name");
            clone.HasNoCost.ShouldBeFalse();
            clone.HasNoBilling.ShouldBeTrue();
            clone.HasNoEffort.ShouldBeFalse();
            clone.Duration.ShouldBe(5);
            clone.StartTime.ShouldBe(3);
            clone.FinishTime.ShouldBe(8);
        }
    }
}
