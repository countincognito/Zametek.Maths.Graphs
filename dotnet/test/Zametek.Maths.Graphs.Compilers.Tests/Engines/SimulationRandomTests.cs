using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class SimulationRandomTests
    {
        [Fact]
        public void SplitMix_Given_SeedZero_Then_GivesThePublishedValues()
        {
            // The first values of splitmix64 from a seed of 0, as published with the algorithm.
            ulong x = 0;
            SimulationRandom.SplitMix(ref x).ShouldBe(0xE220A8397B1DCDAFUL);
            SimulationRandom.SplitMix(ref x).ShouldBe(0x6E789E6AA1B965F4UL);
            SimulationRandom.SplitMix(ref x).ShouldBe(0x06C45D188009454FUL);
            SimulationRandom.SplitMix(ref x).ShouldBe(0xF88BB8A8724C81ECUL);
        }

        [Fact]
        public void NextUInt64_Given_TheStateOneTwoThreeFour_Then_GivesThePublishedValues()
        {
            // The first values of xoshiro256** from the state 1, 2, 3, 4, as published with the
            // algorithm.
            var random = new SimulationRandom(1UL, 2UL, 3UL, 4UL);
            random.NextUInt64().ShouldBe(11520UL);
            random.NextUInt64().ShouldBe(0UL);
            random.NextUInt64().ShouldBe(1509978240UL);
            random.NextUInt64().ShouldBe(1215971899390074240UL);
        }

        [Fact]
        public void Ctor_Given_ASeed_Then_StartsFromTheStateThatSplitMixGives()
        {
            ulong x = 12345;
            var expected = new SimulationRandom(
                SimulationRandom.SplitMix(ref x),
                SimulationRandom.SplitMix(ref x),
                SimulationRandom.SplitMix(ref x),
                SimulationRandom.SplitMix(ref x));
            var actual = new SimulationRandom(12345UL);

            for (int i = 0; i < 20; i++)
            {
                actual.NextUInt64().ShouldBe(expected.NextUInt64());
            }
        }

        [Fact]
        public void NextDouble_Given_ManyDraws_Then_StaysInTheHalfOpenRangeAndHasTheMeanOfAUniformNumber()
        {
            var random = new SimulationRandom(1UL);
            double sum = 0.0;
            const int count = 100_000;
            for (int i = 0; i < count; i++)
            {
                double value = random.NextDouble();
                value.ShouldBeGreaterThanOrEqualTo(0.0);
                value.ShouldBeLessThan(1.0);
                sum += value;
            }
            (sum / count).ShouldBe(0.5, 0.005);
        }

        [Fact]
        public void NextDoubleOpen_Given_ManyDraws_Then_StaysInTheRangeThatTheLogarithmNeeds()
        {
            var random = new SimulationRandom(2UL);
            for (int i = 0; i < 100_000; i++)
            {
                double value = random.NextDoubleOpen();
                value.ShouldBeGreaterThan(0.0);
                value.ShouldBeLessThanOrEqualTo(1.0);
            }
        }

        [Fact]
        public void NextGaussian_Given_ManyDraws_Then_HasMeanZeroAndVarianceOne()
        {
            (double mean, double variance) = Moments(new SimulationRandom(3UL), random => random.NextGaussian(), 200_000);
            mean.ShouldBe(0.0, 0.01);
            variance.ShouldBe(1.0, 0.02);
        }

        [Theory]
        [InlineData(0.5)]
        [InlineData(0.667)]
        [InlineData(1.0)]
        [InlineData(2.5)]
        [InlineData(9.0)]
        public void NextGamma_Given_AShape_Then_HasMeanAndVarianceEqualToTheShape(double shape)
        {
            (double mean, double variance) = Moments(new SimulationRandom(4UL), random => random.NextGamma(shape), 200_000);
            mean.ShouldBe(shape, shape * 0.02);
            variance.ShouldBe(shape, shape * 0.05);
        }

        [Theory]
        [InlineData(0.667, 2.0)]
        [InlineData(1.0, 1.0)]
        [InlineData(3.0, 5.0)]
        public void NextBeta_Given_TwoShapes_Then_HasTheMeanAndTheVarianceOfABeta(double alpha, double beta)
        {
            (double mean, double variance) = Moments(new SimulationRandom(5UL), random => random.NextBeta(alpha, beta), 200_000);
            double expectedMean = alpha / (alpha + beta);
            double expectedVariance = alpha * beta / ((alpha + beta) * (alpha + beta) * (alpha + beta + 1.0));
            mean.ShouldBe(expectedMean, 0.005);
            variance.ShouldBe(expectedVariance, expectedVariance * 0.05);
        }

        [Fact]
        public void NextBeta_Given_ManyDraws_Then_StaysInTheOpenUnitRange()
        {
            var random = new SimulationRandom(6UL);
            for (int i = 0; i < 100_000; i++)
            {
                double value = random.NextBeta(0.667, 4.0);
                value.ShouldBeGreaterThanOrEqualTo(0.0);
                value.ShouldBeLessThanOrEqualTo(1.0);
            }
        }

        [Fact]
        public void ChunkSeed_Given_ManyChunks_Then_GivesADifferentSeedToEach()
        {
            List<ulong> seeds = Enumerable.Range(0, 10_000).Select(x => SimulationRandom.ChunkSeed(42UL, x)).ToList();
            seeds.Distinct().Count().ShouldBe(seeds.Count);
        }

        [Fact]
        public void ChunkSeed_Given_TheSameSeedAndChunk_Then_IsTheSame()
        {
            SimulationRandom.ChunkSeed(42UL, 7).ShouldBe(SimulationRandom.ChunkSeed(42UL, 7));
            SimulationRandom.ChunkSeed(42UL, 7).ShouldNotBe(SimulationRandom.ChunkSeed(43UL, 7));
        }

        [Fact]
        public void ChunkSeed_Given_TwoChunks_Then_TheirGeneratorsDoNotShareAnyOfTheirFirstOutputs()
        {
            // The seeds of two chunks are far apart, and so the first words of two generators
            // are not the same words in a different place.
            var first = new HashSet<ulong>(Outputs(new SimulationRandom(SimulationRandom.ChunkSeed(1UL, 0)), 1000));
            var second = Outputs(new SimulationRandom(SimulationRandom.ChunkSeed(1UL, 1)), 1000);
            second.Count(first.Contains).ShouldBe(0);
        }

        private static IEnumerable<ulong> Outputs(SimulationRandom random, int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return random.NextUInt64();
            }
        }

        private static (double Mean, double Variance) Moments(SimulationRandom random, Func<SimulationRandom, double> draw, int count)
        {
            double sum = 0.0;
            double sumOfSquares = 0.0;
            for (int i = 0; i < count; i++)
            {
                double value = draw(random);
                sum += value;
                sumOfSquares += value * value;
            }
            double mean = sum / count;
            return (mean, (sumOfSquares / count) - (mean * mean));
        }
    }
}
