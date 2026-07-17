using Shouldly;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    // Regression tests for deep-graph transitive reduction. These were previously
    // impossible: the ancestor lookup materialised a HashSet per node (O(N^2) entries,
    // tens of bytes each - out of memory long before 20k nodes). The reducers now
    // compute ancestors as compact bitsets (O(N^2) *bits*), so a 20k-deep chain reduces
    // comfortably - and the redundant shortcut edge proves the reduction actually ran.
    public class DeepTransitiveReductionTests
    {
        private const int c_ChainLength = 20000;

        // Arrow graphs mint roughly two event nodes per activity, so a 10k activity chain
        // already exercises ~20k node depth (the same scale as the other deep tests) at a
        // quarter of the bitset cost of a 20k chain.
        private const int c_ArrowChainLength = 10000;

        [Fact]
        public void VertexGraphBuilder_GivenVeryDeepChainWithRedundantShortcut_ThenTransitiveReductionRemovesShortcut()
        {
            var builder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(new NextIdGenerator<int>(0));

            builder.AddActivity(new Activity<int, int, int>(1, 1));
            for (int id = 2; id < c_ChainLength; id++)
            {
                builder.AddActivity(new Activity<int, int, int>(id, 1), [id - 1]);
            }
            // The final activity depends on its predecessor AND (redundantly) on the chain root.
            builder.AddActivity(new Activity<int, int, int>(c_ChainLength, 1), [c_ChainLength - 1, 1]);

            builder.TransitiveReduction().ShouldBeTrue();

            builder.ActivityDependencyIds(c_ChainLength).ShouldBe([c_ChainLength - 1]);
        }

        [Fact]
        public void ArrowGraphBuilder_GivenVeryDeepChainWithRedundantShortcut_ThenTransitiveReductionRemovesShortcut()
        {
            // The dummy-activity ID generator starts above the chain length so generated dummy
            // edge IDs never collide with the real activity IDs (1..c_ArrowChainLength).
            var builder = new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(
                new NextIdGenerator<int>(c_ArrowChainLength),
                new NextIdGenerator<int>(0));

            builder.AddActivity(new Activity<int, int, int>(1, 1));
            for (int id = 2; id < c_ArrowChainLength; id++)
            {
                builder.AddActivity(new Activity<int, int, int>(id, 1), [id - 1]);
            }
            // The final activity depends on its predecessor AND (redundantly) on the chain root.
            builder.AddActivity(new Activity<int, int, int>(c_ArrowChainLength, 1), [c_ArrowChainLength - 1, 1]);

            builder.TransitiveReduction().ShouldBeTrue();

            builder.StrongActivityDependencyIds(c_ArrowChainLength).ShouldBe([c_ArrowChainLength - 1]);
        }
    }
}
