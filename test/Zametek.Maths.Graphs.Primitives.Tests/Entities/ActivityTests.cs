using Shouldly;
using System;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class ActivityTests
    {
        [Fact]
        public void Activity_GivenCtor_ThenPropertiesSetAndCollectionsEmpty()
        {
            var activity = new Activity<int, int, int>(1, 5);

            activity.Id.ShouldBe(1);
            activity.Duration.ShouldBe(5);
            activity.TargetWorkStreams.ShouldBeEmpty();
            activity.TargetResources.ShouldBeEmpty();
            activity.AllocatedToResources.ShouldBeEmpty();
            activity.CanBeRemoved.ShouldBeFalse();
        }

        [Fact]
        public void Activity_GivenEarliestFinishTime_WhenNoEarliestStartTime_ThenNull()
        {
            var activity = new Activity<int, int, int>(1, 5);

            activity.EarliestFinishTime.ShouldBeNull();
        }

        [Fact]
        public void Activity_GivenEarliestFinishTime_WhenEarliestStartTimeSet_ThenStartPlusDuration()
        {
            var activity = new Activity<int, int, int>(1, 5)
            {
                EarliestStartTime = 3,
            };

            activity.EarliestFinishTime.ShouldBe(8);
        }

        [Fact]
        public void Activity_GivenLatestStartTime_WhenLatestFinishTimeSet_ThenFinishMinusDuration()
        {
            var activity = new Activity<int, int, int>(1, 5)
            {
                LatestFinishTime = 12,
            };

            activity.LatestStartTime.ShouldBe(7);
        }

        [Fact]
        public void Activity_GivenTotalSlack_WhenBothFinishTimesAvailable_ThenLatestMinusEarliest()
        {
            var activity = new Activity<int, int, int>(1, 5)
            {
                EarliestStartTime = 0,   // EF = 5
                LatestFinishTime = 8,
            };

            activity.TotalSlack.ShouldBe(3);
        }

        [Fact]
        public void Activity_GivenTotalSlack_WhenTimesMissing_ThenNull()
        {
            var activity = new Activity<int, int, int>(1, 5);

            activity.TotalSlack.ShouldBeNull();
        }

        [Fact]
        public void Activity_GivenInterferingSlack_ThenTotalSlackMinusFreeSlack()
        {
            var activity = new Activity<int, int, int>(1, 5)
            {
                EarliestStartTime = 0,   // EF = 5
                LatestFinishTime = 8,    // total slack = 3
                FreeSlack = 1,
            };

            activity.InterferingSlack.ShouldBe(2);
        }

        [Fact]
        public void Activity_GivenInterferingSlack_WhenFreeSlackMissing_ThenNull()
        {
            var activity = new Activity<int, int, int>(1, 5)
            {
                EarliestStartTime = 0,
                LatestFinishTime = 8,
            };

            activity.InterferingSlack.ShouldBeNull();
        }

        [Fact]
        public void Activity_GivenIsCritical_WhenZeroTotalSlack_ThenTrue()
        {
            var activity = new Activity<int, int, int>(1, 5)
            {
                EarliestStartTime = 0,
                LatestFinishTime = 5,
            };

            activity.IsCritical.ShouldBeTrue();
        }

        [Fact]
        public void Activity_GivenIsCritical_WhenNegativeTotalSlack_ThenTrue()
        {
            // Over-constrained: latest finish before earliest finish.
            var activity = new Activity<int, int, int>(1, 5)
            {
                EarliestStartTime = 0,
                LatestFinishTime = 3,
            };

            activity.TotalSlack.ShouldBe(-2);
            activity.IsCritical.ShouldBeTrue();
        }

        [Fact]
        public void Activity_GivenIsCritical_WhenPositiveTotalSlack_ThenFalse()
        {
            var activity = new Activity<int, int, int>(1, 5)
            {
                EarliestStartTime = 0,
                LatestFinishTime = 8,
            };

            activity.IsCritical.ShouldBeFalse();
        }

        [Fact]
        public void Activity_GivenIsCritical_WhenNoTimes_ThenFalse()
        {
            var activity = new Activity<int, int, int>(1, 5);

            activity.IsCritical.ShouldBeFalse();
        }

        [Fact]
        public void Activity_GivenIsDummy_WhenZeroDuration_ThenTrue()
        {
            new Activity<int, int, int>(1, 0).IsDummy.ShouldBeTrue();
            new Activity<int, int, int>(1, 1).IsDummy.ShouldBeFalse();
        }

        [Fact]
        public void Activity_GivenSetAsRemovableAndReadOnly_ThenCanBeRemovedToggles()
        {
            var activity = new Activity<int, int, int>(1, 5);

            activity.SetAsRemovable();
            activity.CanBeRemoved.ShouldBeTrue();

            activity.SetAsReadOnly();
            activity.CanBeRemoved.ShouldBeFalse();
        }

        [Fact]
        public void Activity_GivenCloneObject_ThenAllPropertiesPreserved()
        {
            var activity = new Activity<int, int, int>(1, 5)
            {
                Name = "name",
                Notes = "notes",
                TargetResourceOperator = LogicalOperator.OR,
                HasNoCost = true,
                HasNoBilling = true,
                HasNoEffort = true,
                EarliestStartTime = 2,
                LatestFinishTime = 10,
                FreeSlack = 1,
                MinimumFreeSlack = 1,
                MinimumEarliestStartTime = 2,
                MaximumLatestFinishTime = 11,
            };
            activity.TargetWorkStreams.Add(21);
            activity.TargetResources.Add(31);
            activity.AllocatedToResources.Add(41);
            activity.SetAsRemovable();

            var clone = (Activity<int, int, int>)activity.CloneObject();

            clone.Id.ShouldBe(1);
            clone.Duration.ShouldBe(5);
            clone.Name.ShouldBe("name");
            clone.Notes.ShouldBe("notes");
            clone.TargetResourceOperator.ShouldBe(LogicalOperator.OR);
            clone.HasNoCost.ShouldBeTrue();
            clone.HasNoBilling.ShouldBeTrue();
            clone.HasNoEffort.ShouldBeTrue();
            clone.EarliestStartTime.ShouldBe(2);
            clone.LatestFinishTime.ShouldBe(10);
            clone.FreeSlack.ShouldBe(1);
            clone.MinimumFreeSlack.ShouldBe(1);
            clone.MinimumEarliestStartTime.ShouldBe(2);
            clone.MaximumLatestFinishTime.ShouldBe(11);
            clone.TargetWorkStreams.ShouldBe([21]);
            clone.TargetResources.ShouldBe([31]);
            clone.AllocatedToResources.ShouldBe([41]);
            clone.CanBeRemoved.ShouldBeTrue();
        }

        [Fact]
        public void Activity_GivenCloneObject_ThenCollectionsAreIndependentCopies()
        {
            var activity = new Activity<int, int, int>(1, 5);
            activity.TargetResources.Add(31);

            var clone = (Activity<int, int, int>)activity.CloneObject();
            clone.TargetResources.Add(32);

            activity.TargetResources.Count.ShouldBe(1);
            clone.TargetResources.Count.ShouldBe(2);
        }
    }
}
