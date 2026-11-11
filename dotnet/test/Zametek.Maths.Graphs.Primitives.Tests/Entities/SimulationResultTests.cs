using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class SimulationResultTests
    {
        private static CriticalPath<int> Path(params (int Id, CriticalPathLink Link)[] steps) =>
            new CriticalPath<int>(steps.Select(x => new CriticalPathStep<int>(x.Id, x.Link)));

        [Fact]
        public void SimulationResult_GivenAWholeResult_ThenGivesBackEachPart()
        {
            // Three iterations of a plan of 70 activities (two words of bits for each iteration).
            int[] ids = Enumerable.Range(1, 70).ToArray();
            var bits = new ulong[6];
            bits[0] = 0b101UL;                 // iteration 0: activities 0 and 2
            bits[2] = 1UL << 5;                // iteration 1: activity 5
            bits[5] = 1UL << 5;                // iteration 2: activity 69 (the second word)
            CriticalPath<int> first = Path((1, CriticalPathLink.None), (3, CriticalPathLink.Dependency));
            CriticalPath<int> second = Path((6, CriticalPathLink.None));

            var result = new SimulationResult<int>(
                ids,
                new[] { 10, -1, 12 },
                bits,
                new[] { first, second },
                new[] { 0, 2, 2, 3 },
                new[] { 0, 1, 1 },
                new[] { true, true, false });

            result.IterationCount.ShouldBe(3);
            result.FailedIterationCount.ShouldBe(1);
            result.FinishTimes.ShouldBe(new[] { 10, SimulationResult<int>.FailedFinishTime, 12 });
            result.HasCriticalActivities.ShouldBeTrue();
            result.IsCritical(0, 0).ShouldBeTrue();
            result.IsCritical(0, 1).ShouldBeFalse();
            result.IsCritical(0, 2).ShouldBeTrue();
            result.IsCritical(1, 5).ShouldBeTrue();
            result.IsCritical(2, 69).ShouldBeTrue();
            result.IsCritical(2, 5).ShouldBeFalse();
            result.HasCriticalPaths.ShouldBeTrue();
            result.CriticalPathIndexes(0).ToArray().ShouldBe(new[] { 0, 1 });
            result.CriticalPathIndexes(1).ToArray().ShouldBeEmpty();
            result.CriticalPathIndexes(2).ToArray().ShouldBe(new[] { 1 });
            result.IsCriticalPathListComplete(0).ShouldBeTrue();
            result.IsCriticalPathListComplete(2).ShouldBeFalse();
            result.CriticalPaths[0].ToString().ShouldBe(@"1 > 3");
        }

        [Fact]
        public void SimulationResult_GivenOnlyTheFinishTimes_ThenTheOtherPartsAreNotThere()
        {
            var result = new SimulationResult<int>(new[] { 1, 2 }, new[] { 5, 6 }, null, new List<CriticalPath<int>>(), null, null, null);

            result.HasCriticalActivities.ShouldBeFalse();
            result.HasCriticalPaths.ShouldBeFalse();
            Should.Throw<InvalidOperationException>(() => result.IsCritical(0, 0));
            Should.Throw<InvalidOperationException>(() => result.CriticalPathIndexes(0).ToArray());
            Should.Throw<InvalidOperationException>(() => result.IsCriticalPathListComplete(0));
        }

        [Fact]
        public void SimulationResult_GivenAnIterationOrActivityThatDoesNotExist_ThenThrows()
        {
            var result = new SimulationResult<int>(new[] { 1, 2 }, new[] { 5, 6 }, new ulong[2], new List<CriticalPath<int>>(), null, null, null);

            Should.Throw<ArgumentOutOfRangeException>(() => result.IsCritical(2, 0));
            Should.Throw<ArgumentOutOfRangeException>(() => result.IsCritical(-1, 0));
            Should.Throw<ArgumentOutOfRangeException>(() => result.IsCritical(0, 2));
        }

        [Fact]
        public void SimulationResult_GivenPartsThatDoNotMatch_ThenThrows()
        {
            Should.Throw<ArgumentException>(() => new SimulationResult<int>(
                new[] { 1, 2 }, new[] { 5, 6 }, new ulong[3], new List<CriticalPath<int>>(), null, null, null));
            Should.Throw<ArgumentException>(() => new SimulationResult<int>(
                new[] { 1, 2 }, new[] { 5, 6 }, null, new List<CriticalPath<int>>(), new[] { 0, 0 }, new int[0], new[] { true, true }));
            Should.Throw<ArgumentException>(() => new SimulationResult<int>(
                new[] { 1, 2 }, new[] { 5, 6 }, null, new List<CriticalPath<int>>(), new[] { 0, 0, 0 }, null, new[] { true, true }));
        }

        [Fact]
        public void SimulationResult_GivenTheReasonsOfTheFailures_ThenGivesThemBack()
        {
            var result = new SimulationResult<int>(
                new[] { 1, 2 },
                new[] { 5, -1, -1, 6 },
                null,
                new List<CriticalPath<int>>(),
                null,
                null,
                null,
                new[] { SimulationIterationFailure.None, SimulationIterationFailure.InvalidConstraints, SimulationIterationFailure.ResourceSchedulingStall, SimulationIterationFailure.None },
                new[] { 0, 1 });

            result.FailedIterationCount.ShouldBe(2);
            result.HasFailureReasons.ShouldBeTrue();
            result.FailureReason(0).ShouldBe(SimulationIterationFailure.None);
            result.FailureReason(1).ShouldBe(SimulationIterationFailure.InvalidConstraints);
            result.FailureReason(2).ShouldBe(SimulationIterationFailure.ResourceSchedulingStall);
            result.MaximumLatestFinishTimeMissCount(0).ShouldBe(0);
            result.MaximumLatestFinishTimeMissCount(1).ShouldBe(1);
            Should.Throw<ArgumentOutOfRangeException>(() => result.FailureReason(4));
            Should.Throw<ArgumentOutOfRangeException>(() => result.MaximumLatestFinishTimeMissCount(2));
        }

        [Fact]
        public void SimulationResult_GivenNoReasonsOfTheFailures_ThenTheyAreNotThere()
        {
            var result = new SimulationResult<int>(new[] { 1, 2 }, new[] { 5, -1 }, null, new List<CriticalPath<int>>(), null, null, null);

            result.HasFailureReasons.ShouldBeFalse();
            Should.Throw<InvalidOperationException>(() => result.FailureReason(0));
            Should.Throw<InvalidOperationException>(() => result.MaximumLatestFinishTimeMissCount(0));
        }

        [Fact]
        public void SimulationResult_GivenReasonsOfTheFailuresThatDoNotMatch_ThenThrows()
        {
            var empty = new List<CriticalPath<int>>();

            // Reasons without misses, and misses without reasons.
            Should.Throw<ArgumentException>(() => new SimulationResult<int>(
                new[] { 1, 2 }, new[] { 5, 6 }, null, empty, null, null, null, new SimulationIterationFailure[2], null));
            Should.Throw<ArgumentException>(() => new SimulationResult<int>(
                new[] { 1, 2 }, new[] { 5, 6 }, null, empty, null, null, null, null, new int[2]));

            // A reason for each iteration, and a count for each activity.
            Should.Throw<ArgumentException>(() => new SimulationResult<int>(
                new[] { 1, 2 }, new[] { 5, 6 }, null, empty, null, null, null, new SimulationIterationFailure[3], new int[2]));
            Should.Throw<ArgumentException>(() => new SimulationResult<int>(
                new[] { 1, 2 }, new[] { 5, 6 }, null, empty, null, null, null, new SimulationIterationFailure[2], new int[3]));

            // A failed iteration has a reason, and an iteration with a finish time has none.
            Should.Throw<ArgumentException>(() => new SimulationResult<int>(
                new[] { 1, 2 }, new[] { 5, -1 }, null, empty, null, null, null, new SimulationIterationFailure[2], new int[2]));
            Should.Throw<ArgumentException>(() => new SimulationResult<int>(
                new[] { 1, 2 }, new[] { 5, 6 }, null, empty, null, null, null,
                new[] { SimulationIterationFailure.None, SimulationIterationFailure.InvalidConstraints }, new int[2]));
        }

        [Fact]
        public void CriticalPath_GivenTheSameSteps_ThenIsEqualAndHasTheSameHash()
        {
            CriticalPath<int> a = Path((1, CriticalPathLink.None), (2, CriticalPathLink.Resource));
            CriticalPath<int> b = Path((1, CriticalPathLink.None), (2, CriticalPathLink.Resource));
            CriticalPath<int> c = Path((1, CriticalPathLink.None), (2, CriticalPathLink.Dependency));

            a.Equals(b).ShouldBeTrue();
            a.GetHashCode().ShouldBe(b.GetHashCode());
            a.Equals(c).ShouldBeFalse();
            a.Equals((object)null).ShouldBeFalse();
        }

        [Fact]
        public void CriticalPathStep_GivenTheSameValues_ThenIsEqual()
        {
            var a = new CriticalPathStep<int>(4, CriticalPathLink.Resource);
            var b = new CriticalPathStep<int>(4, CriticalPathLink.Resource);

            a.Equals(b).ShouldBeTrue();
            a.GetHashCode().ShouldBe(b.GetHashCode());
            a.Equals(new CriticalPathStep<int>(4, CriticalPathLink.Dependency)).ShouldBeFalse();
            a.ToString().ShouldBe(@"4 (Resource)");
        }

        [Fact]
        public void CriticalPath_GivenNoSteps_ThenThrows()
        {
            Should.Throw<ArgumentNullException>(() => new CriticalPath<int>(null));
        }
    }
}
