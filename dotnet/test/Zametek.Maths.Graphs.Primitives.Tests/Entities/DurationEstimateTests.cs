using Shouldly;
using System;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class DurationEstimateTests
    {
        [Fact]
        public void DurationEstimate_GivenCtor_ThenPropertiesSet()
        {
            var estimate = new DurationEstimate(4, 5, 12);

            estimate.Optimistic.ShouldBe(4);
            estimate.MostLikely.ShouldBe(5);
            estimate.Pessimistic.ShouldBe(12);
            estimate.HasSpread.ShouldBeTrue();
        }

        [Theory]
        [InlineData(-1, 0, 0)]
        [InlineData(3, 2, 5)]
        [InlineData(1, 6, 5)]
        [InlineData(0, 0, 100_001)]
        public void DurationEstimate_GivenValuesThatBreakTheRule_ThenThrows(int optimistic, int mostLikely, int pessimistic)
        {
            Should.Throw<ArgumentOutOfRangeException>(() => new DurationEstimate(optimistic, mostLikely, pessimistic));
        }

        [Fact]
        public void DurationEstimate_GivenTheLimitsOfTheRule_ThenAccepts()
        {
            new DurationEstimate(0, 0, 0).HasSpread.ShouldBeFalse();
            new DurationEstimate(0, 100_000, 100_000).HasSpread.ShouldBeTrue();
            new DurationEstimate(100_000, 100_000, 100_000).ExpectedDuration.ShouldBe(100_000);
        }

        [Fact]
        public void Fixed_GivenADuration_ThenHasNoSpreadAndExpectsTheDuration()
        {
            DurationEstimate estimate = DurationEstimate.Fixed(7);

            estimate.HasSpread.ShouldBeFalse();
            estimate.ExpectedDuration.ShouldBe(7);
            estimate.StandardDeviation.ShouldBe(0.0);
        }

        [Theory]
        [InlineData(4, 10, 10, 9, 1.0)]    // (4 + 40 + 10) / 6 = 9
        [InlineData(4, 10, 22, 11, 3.0)]   // (4 + 40 + 22) / 6 = 11
        [InlineData(2, 4, 6, 4, 0.6666666666666666)]
        [InlineData(0, 0, 3, 1, 0.5)]      // 3 / 6 = 0.5, and a half goes up
        [InlineData(0, 0, 9, 2, 1.5)]      // 9 / 6 = 1.5, and a half goes up
        [InlineData(0, 0, 2, 0, 0.3333333333333333)]
        public void ExpectedDuration_GivenEstimates_ThenIsThePertMeanToTheNearestWholeUnitWithHalvesUp(
            int optimistic, int mostLikely, int pessimistic, int expected, double standardDeviation)
        {
            var estimate = new DurationEstimate(optimistic, mostLikely, pessimistic);

            estimate.ExpectedDuration.ShouldBe(expected);
            estimate.StandardDeviation.ShouldBe(standardDeviation, 1e-12);
        }

        [Fact]
        public void Equals_GivenTheSameEstimates_ThenAreEqual()
        {
            var a = new DurationEstimate(1, 2, 3);
            var b = new DurationEstimate(1, 2, 3);
            var c = new DurationEstimate(1, 2, 4);

            a.Equals(b).ShouldBeTrue();
            (a == b).ShouldBeTrue();
            a.GetHashCode().ShouldBe(b.GetHashCode());
            (a != c).ShouldBeTrue();
            a.Equals((object)c).ShouldBeFalse();
        }

        [Fact]
        public void ToString_GivenEstimates_ThenIsOptimisticMostLikelyPessimistic()
        {
            new DurationEstimate(1, 2, 3).ToString().ShouldBe(@"1/2/3");
        }
    }
}
