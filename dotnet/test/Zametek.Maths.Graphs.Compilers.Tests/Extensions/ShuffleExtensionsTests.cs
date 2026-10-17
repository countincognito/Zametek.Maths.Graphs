using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    // The shuffle that the builders use when ShuffleProcessingOrder is on. The tests of the builders show that a result
    // does not depend on the order, and they would pass if the shuffle did nothing, so the shuffle is tested here.
    public class ShuffleExtensionsTests
    {
        [Fact]
        public void Shuffle_Given_AList_Then_KeepsTheSameItems()
        {
            List<int> items = Enumerable.Range(1, 50).ToList();

            items.Shuffle();

            items.OrderBy(x => x).ShouldBe(Enumerable.Range(1, 50));
        }

        [Fact]
        public void Shuffle_Given_AListShuffledManyTimes_Then_GivesMoreThanOneOrder()
        {
            var orders = new HashSet<string>();

            for (int i = 0; i < 50; i++)
            {
                List<int> items = Enumerable.Range(1, 10).ToList();
                items.Shuffle();
                orders.Add(string.Join(",", items));
            }

            // Ten items have 3,628,800 orders, so 50 shuffles that gave one order would mean that nothing is shuffled.
            orders.Count.ShouldBeGreaterThan(40);
        }

        [Fact]
        public void Shuffle_Given_ThreeItems_Then_EveryOrderComesUp()
        {
            var orders = new HashSet<string>();

            for (int i = 0; i < 500; i++)
            {
                var items = new List<int> { 1, 2, 3 };
                items.Shuffle();
                orders.Add(string.Join(",", items));
            }

            orders.Count.ShouldBe(6);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void Shuffle_Given_AListWithNothingToMove_Then_LeavesItAlone(int count)
        {
            List<int> items = Enumerable.Range(1, count).ToList();

            items.Shuffle();

            items.ShouldBe(Enumerable.Range(1, count));
        }

        [Fact]
        public void Shuffle_Given_NoList_Then_Throws()
        {
            Should.Throw<ArgumentNullException>(() => ((IList<int>)null).Shuffle());
        }

        [Fact]
        public async Task Shuffle_Given_ManyThreads_Then_EveryListKeepsItsItems()
        {
            await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < 500; i++)
                {
                    List<int> items = Enumerable.Range(1, 20).ToList();
                    items.Shuffle();
                    items.OrderBy(x => x).ShouldBe(Enumerable.Range(1, 20));
                }
            })));
        }
    }
}
