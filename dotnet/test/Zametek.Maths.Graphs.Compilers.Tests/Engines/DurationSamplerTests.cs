using Shouldly;
using System;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class DurationSamplerTests
    {
        private const int c_Draws = 200_000;

        private static (double Mean, double StandardDeviation, double Minimum, double Maximum) Draw(
            DurationDistribution distribution,
            DurationEstimate estimate,
            ulong seed = 11UL,
            double shape = DurationSampler.DefaultModifiedBetaShape)
        {
            var random = new SimulationRandom(seed);
            double sum = 0.0;
            double sumOfSquares = 0.0;
            double minimum = double.MaxValue;
            double maximum = double.MinValue;
            for (int i = 0; i < c_Draws; i++)
            {
                double value = DurationSampler.Sample(distribution, random, estimate, shape);
                sum += value;
                sumOfSquares += value * value;
                minimum = Math.Min(minimum, value);
                maximum = Math.Max(maximum, value);
            }
            double mean = sum / c_Draws;
            return (mean, Math.Sqrt((sumOfSquares / c_Draws) - (mean * mean)), minimum, maximum);
        }

        public static TheoryData<int, int, int> Estimates => new TheoryData<int, int, int>
        {
            { 2, 4, 6 },
            { 4, 5, 12 },
            { 1, 5, 9 },
            { 7, 9, 9 },
            { 5, 5, 9 },
            { 0, 3, 10 },
            { 0, 0, 5 },
        };

        [Theory]
        [MemberData(nameof(Estimates))]
        public void BetaPert_Given_Estimates_Then_HasThePertMeanAndThePertStandardDeviation(int optimistic, int mostLikely, int pessimistic)
        {
            var estimate = new DurationEstimate(optimistic, mostLikely, pessimistic);
            double mean = (optimistic + (4.0 * mostLikely) + pessimistic) / 6.0;

            var result = Draw(DurationDistribution.BetaPert, estimate);

            result.Mean.ShouldBe(mean, 0.01 * (pessimistic - optimistic));
            result.StandardDeviation.ShouldBe(estimate.StandardDeviation, 0.02 * (pessimistic - optimistic));
        }

        [Theory]
        [MemberData(nameof(Estimates))]
        public void ModifiedBetaPert_Given_Estimates_Then_HasThePertMeanAndTheVarianceOfItsShape(int optimistic, int mostLikely, int pessimistic)
        {
            var estimate = new DurationEstimate(optimistic, mostLikely, pessimistic);
            double mean = (optimistic + (4.0 * mostLikely) + pessimistic) / 6.0;

            var result = Draw(DurationDistribution.ModifiedBetaPert, estimate);

            // The variance of a modified PERT beta with a shape value of g is
            // (mean - O) (P - mean) / (g + 3).
            double variance = (mean - optimistic) * (pessimistic - mean) / (DurationSampler.DefaultModifiedBetaShape + 3.0);
            result.Mean.ShouldBe(mean, 0.01 * (pessimistic - optimistic));
            result.StandardDeviation.ShouldBe(Math.Sqrt(variance), 0.02 * (pessimistic - optimistic));
        }

        [Theory]
        [MemberData(nameof(Estimates))]
        public void Triangular_Given_Estimates_Then_HasTheMeanOfATriangle(int optimistic, int mostLikely, int pessimistic)
        {
            var estimate = new DurationEstimate(optimistic, mostLikely, pessimistic);

            var result = Draw(DurationDistribution.Triangular, estimate);

            result.Mean.ShouldBe((optimistic + mostLikely + pessimistic) / 3.0, 0.01 * (pessimistic - optimistic));
        }

        [Theory]
        [MemberData(nameof(Estimates))]
        public void Uniform_Given_Estimates_Then_HasTheMeanAndTheDeviationOfAUniformNumber(int optimistic, int mostLikely, int pessimistic)
        {
            var estimate = new DurationEstimate(optimistic, mostLikely, pessimistic);
            double range = pessimistic - optimistic;

            var result = Draw(DurationDistribution.Uniform, estimate);

            result.Mean.ShouldBe((optimistic + pessimistic) / 2.0, 0.01 * range);
            result.StandardDeviation.ShouldBe(range / Math.Sqrt(12.0), 0.02 * range);
        }

        [Theory]
        [MemberData(nameof(Estimates))]
        public void Normal_Given_Estimates_Then_StaysInsideTheRangeFromOptimisticToPessimistic(int optimistic, int mostLikely, int pessimistic)
        {
            var estimate = new DurationEstimate(optimistic, mostLikely, pessimistic);

            var result = Draw(DurationDistribution.Normal, estimate);

            result.Minimum.ShouldBeGreaterThanOrEqualTo(optimistic);
            result.Maximum.ShouldBeLessThanOrEqualTo(pessimistic);
            result.Mean.ShouldBe((optimistic + (4.0 * mostLikely) + pessimistic) / 6.0, 0.02 * (pessimistic - optimistic));
        }

        [Theory]
        [MemberData(nameof(Estimates))]
        public void Sample_Given_AnyDistribution_Then_StaysInsideTheRangeFromOptimisticToPessimistic(int optimistic, int mostLikely, int pessimistic)
        {
            var estimate = new DurationEstimate(optimistic, mostLikely, pessimistic);

            foreach (DurationDistribution distribution in Enum.GetValues(typeof(DurationDistribution)))
            {
                var result = Draw(distribution, estimate, seed: 99UL);
                result.Minimum.ShouldBeGreaterThanOrEqualTo(optimistic, distribution.ToString());
                result.Maximum.ShouldBeLessThanOrEqualTo(pessimistic, distribution.ToString());
            }
        }

        [Fact]
        public void BetaPert_Given_TheReferenceActivityB_Then_MatchesTheNumbersOfTheFormula()
        {
            // Activity B of the reference sample: O = 4, ML = 5, P = 12. The PERT mean is 6 and the
            // PERT standard deviation is 4 / 3.
            var result = Draw(DurationDistribution.BetaPert, new DurationEstimate(4, 5, 12));

            result.Mean.ShouldBe(6.0, 0.03);
            result.StandardDeviation.ShouldBe(8.0 / 6.0, 0.02);
        }

        [Fact]
        public void ModifiedBetaPert_Given_AShape_Then_ALargerShapeDrawsCloserToTheMode()
        {
            var estimate = new DurationEstimate(0, 2, 10);

            var narrow = Draw(DurationDistribution.ModifiedBetaPert, estimate, shape: 12.0);
            var wide = Draw(DurationDistribution.ModifiedBetaPert, estimate, shape: 2.0);

            narrow.StandardDeviation.ShouldBeLessThan(wide.StandardDeviation);
        }

        [Theory]
        [InlineData(DurationDistribution.BetaPert)]
        [InlineData(DurationDistribution.ModifiedBetaPert)]
        [InlineData(DurationDistribution.Triangular)]
        [InlineData(DurationDistribution.Normal)]
        [InlineData(DurationDistribution.Uniform)]
        public void Sample_Given_AnActivityWithNoSpread_Then_IsTheDurationAndTakesNothingFromTheGenerator(DurationDistribution distribution)
        {
            var random = new SimulationRandom(5UL);
            var untouched = new SimulationRandom(5UL);

            DurationSampler.Sample(distribution, random, DurationEstimate.Fixed(7), DurationSampler.DefaultModifiedBetaShape).ShouldBe(7.0);

            random.NextUInt64().ShouldBe(untouched.NextUInt64());
        }

        [Fact]
        public void Sample_Given_ADistributionThatDoesNotExist_Then_Throws()
        {
            Should.Throw<ArgumentOutOfRangeException>(() => DurationSampler.Sample(
                (DurationDistribution)99,
                new SimulationRandom(1UL),
                new DurationEstimate(1, 2, 3),
                DurationSampler.DefaultModifiedBetaShape));
        }

        [Theory]
        [InlineData(2.4999, 2)]
        [InlineData(2.5, 3)]
        [InlineData(2.5001, 3)]
        [InlineData(3.0, 3)]
        [InlineData(0.5, 1)]
        public void RoundToWholeUnits_Given_ADraw_Then_IsTheNearestWholeNumberWithHalvesUp(double draw, int expected)
        {
            DurationSampler.RoundToWholeUnits(draw, new DurationEstimate(0, 3, 10)).ShouldBe(expected);
        }

        [Fact]
        public void RoundToWholeUnits_Given_ADrawJustOutsideTheRange_Then_IsLimitedToTheRange()
        {
            var estimate = new DurationEstimate(4, 5, 12);

            DurationSampler.RoundToWholeUnits(3.4, estimate).ShouldBe(4);
            DurationSampler.RoundToWholeUnits(12.4, estimate).ShouldBe(12);
        }
    }
}
