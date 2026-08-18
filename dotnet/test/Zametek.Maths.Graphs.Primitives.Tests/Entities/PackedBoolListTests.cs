using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    // PackedBoolList stores one bit per flag instead of one byte, so these tests
    // concentrate on the boundaries where the packing arithmetic could go wrong:
    // lengths either side of a 64-bit word, flags in the highest bit of a word, and
    // round-tripping arbitrary patterns.
    public class PackedBoolListTests
    {
        [Fact]
        public void PackedBoolList_GivenFrom_WithNull_ThenThrowsArgumentNullException()
        {
            Action act = () => PackedBoolList.From(null);
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void PackedBoolList_GivenFrom_WithEmptySource_ThenIsEmpty()
        {
            PackedBoolList output = PackedBoolList.From([]);

            output.Count.ShouldBe(0);
            output.ShouldBeEmpty();
        }

        [Theory]
        [InlineData(1)]
        [InlineData(63)]
        [InlineData(64)]
        [InlineData(65)]
        [InlineData(127)]
        [InlineData(128)]
        [InlineData(129)]
        [InlineData(1000)]
        public void PackedBoolList_GivenFrom_WithAlternatingFlags_ThenRoundTripsExactly(int length)
        {
            List<bool> source = Enumerable.Range(0, length).Select(x => x % 2 == 0).ToList();

            PackedBoolList output = PackedBoolList.From(source);

            output.Count.ShouldBe(length);
            output.ToList().ShouldBe(source);
        }

        [Theory]
        [InlineData(63)]
        [InlineData(64)]
        [InlineData(65)]
        [InlineData(200)]
        public void PackedBoolList_GivenFrom_WithRandomFlags_ThenIndexerMatchesSource(int length)
        {
            var rng = new Random(length);
            List<bool> source = Enumerable.Range(0, length).Select(_ => rng.Next(2) == 0).ToList();

            PackedBoolList output = PackedBoolList.From(source);

            for (int i = 0; i < length; i++)
            {
                output[i].ShouldBe(source[i], $@"index {i}");
            }
        }

        [Fact]
        public void PackedBoolList_GivenFrom_WithFlagInHighestBitOfWord_ThenReadsBackTrue()
        {
            // Bit 63 is the top bit of the first word, and bit 64 the bottom bit of the
            // second - the exact boundary the shift and mask arithmetic has to get right.
            List<bool> source = Enumerable.Repeat(false, 130).ToList();
            source[63] = true;
            source[64] = true;
            source[129] = true;

            PackedBoolList output = PackedBoolList.From(source);

            output[63].ShouldBeTrue();
            output[64].ShouldBeTrue();
            output[129].ShouldBeTrue();
            output.Count(x => x).ShouldBe(3);
        }

        [Fact]
        public void PackedBoolList_GivenFrom_WithAllFlagsSet_ThenAllReadBackTrue()
        {
            PackedBoolList output = PackedBoolList.From(Enumerable.Repeat(true, 100));

            output.Count.ShouldBe(100);
            output.All(x => x).ShouldBeTrue();
        }

        [Fact]
        public void PackedBoolList_GivenFrom_WithLazySourceOfUnknownLength_ThenGrowsCorrectly()
        {
            // A source with no known count exercises the doubling growth path.
            IEnumerable<bool> lazySource = Enumerable.Range(0, 500).Where(x => x % 3 != 0).Select(x => x % 2 == 0);
            List<bool> expected = lazySource.ToList();

            PackedBoolList output = PackedBoolList.From(lazySource);

            output.Count.ShouldBe(expected.Count);
            output.ToList().ShouldBe(expected);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(10)]
        [InlineData(11)]
        public void PackedBoolList_GivenIndexer_WithIndexOutOfRange_ThenThrowsArgumentOutOfRangeException(int index)
        {
            PackedBoolList output = PackedBoolList.From(Enumerable.Repeat(true, 10));

            Action act = () => _ = output[index];

            act.ShouldThrow<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void ResourceSchedule_GivenAllocationStreams_ThenExposesThemUnchanged()
        {
            List<bool> resourceAllocation = [true, false, true, true];
            List<bool> costAllocation = [false, false, true, true];
            List<bool> billingAllocation = [true, true, false, false];
            List<bool> effortAllocation = [false, true, false, true];
            List<bool> activityAllocation = [true, true, true, false];

            var schedule = new ResourceSchedule<int, int, int>(
                [],
                0, 4,
                resourceAllocation,
                costAllocation,
                billingAllocation,
                effortAllocation,
                activityAllocation);

            // Packing is an internal storage detail; the streams must read back exactly
            // as supplied.
            schedule.ResourceAllocation.ShouldBe(resourceAllocation);
            schedule.CostAllocation.ShouldBe(costAllocation);
            schedule.BillingAllocation.ShouldBe(billingAllocation);
            schedule.EffortAllocation.ShouldBe(effortAllocation);
            schedule.ActivityAllocation.ShouldBe(activityAllocation);
        }

        [Fact]
        public void ResourceSchedule_GivenCloneObject_ThenAllocationStreamsSurviveTheRoundTrip()
        {
            var rng = new Random(7);
            List<bool> resourceAllocation = Enumerable.Range(0, 200).Select(_ => rng.Next(2) == 0).ToList();

            var schedule = new ResourceSchedule<int, int, int>(
                [],
                0, 200,
                resourceAllocation,
                resourceAllocation,
                resourceAllocation,
                resourceAllocation,
                resourceAllocation);

            var clone = (ResourceSchedule<int, int, int>)schedule.CloneObject();

            clone.ResourceAllocation.ShouldBe(resourceAllocation);
            clone.ActivityAllocation.ShouldBe(resourceAllocation);
        }
    }
}
