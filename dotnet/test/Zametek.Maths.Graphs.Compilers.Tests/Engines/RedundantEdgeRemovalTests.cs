using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    // Regression tests for the walk that orders dummy edges for removal
    // (DummyEdgeOrchestrator.GetEdgesInDescendingOrder, reached through RemoveRedundantEdges,
    // which CalculateCriticalPath runs first). It carried two independent defects.
    //
    // It descended through every edge *occurrence* rather than every edge, so it enumerated
    // each distinct path from the start node instead of visiting each edge once. On the
    // branching graph below - 150 activities over 30 layers, 297 event nodes and 416 edges -
    // that is 3.5 x 10^8 steps per walk, and the three walks the removal performs took a
    // minute between them and allocated 158 GB.
    //
    // It was also recursive, so its depth was the length of the longest path, which a deep
    // chain overflows.
    //
    // The two tests pin those separately. The first measures allocation rather than elapsed
    // time, because allocation is deterministic and the two regimes are 158 GB against about
    // 1 MB - any threshold between them is unambiguous, and the one used leaves three orders
    // of magnitude of headroom on both sides.
    public class RedundantEdgeRemovalTests
    {
        // Enough depth for the path count to explode, while staying a graph the limits accept.
        private const int c_BranchingActivityCount = 150;
        private const int c_BranchingLayerCount = 30;

        // The scale the other deep-graph tests use; an arrow graph mints roughly two event
        // nodes per activity, so this is a walk about 20,000 deep.
        private const int c_ChainLength = 10000;

        private const long c_AllocationCeilingBytes = 100L * 1024 * 1024;

        [Fact]
        public void ArrowGraphBuilder_GivenBranchingGraph_WhenCalculatingCriticalPath_ThenDoesNotEnumerateEveryPath()
        {
            ArrowGraphBuilder<int, int, int, IActivity<int, int, int>> builder = BuildBranchingGraph();
            builder.TransitiveReduction().ShouldBeTrue();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long allocatedBefore = GC.GetTotalAllocatedBytes(precise: false);

            builder.CalculateCriticalPath();

            long allocated = GC.GetTotalAllocatedBytes(precise: false) - allocatedBefore;

            allocated.ShouldBeLessThan(
                c_AllocationCeilingBytes,
                $@"the redundant-edge walk allocated {allocated / (1024 * 1024)} MB, which means it is enumerating paths rather than edges again");
        }

        [Fact]
        public void ArrowGraphBuilder_GivenVeryDeepChain_WhenCalculatingCriticalPath_ThenDoesNotOverflowTheStack()
        {
            // The dummy-activity ID generator starts above the chain length so generated dummy
            // edge IDs never collide with the real activity IDs (1..c_ChainLength).
            var builder = new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(
                new NextIdGenerator<int>(c_ChainLength),
                new NextIdGenerator<int>(0));

            builder.AddActivity(new Activity<int, int, int>(1, 1));
            for (int id = 2; id <= c_ChainLength; id++)
            {
                builder.AddActivity(new Activity<int, int, int>(id, 1), [id - 1]);
            }

            // Reducing first is what the compiler does, and it is deliberate here rather
            // than incidental: calculating the critical path on a graph that was never
            // reduced is slow for an unrelated reason, recorded against RedirectDummyEdges.
            builder.TransitiveReduction().ShouldBeTrue();

            builder.CalculateCriticalPath();

            // A chain of unit durations, so the last activity starts once every earlier one
            // has finished - which also shows the walk reached the far end of the graph.
            builder.Activities.Single(x => x.Id == c_ChainLength).EarliestStartTime.ShouldBe(c_ChainLength - 1);
        }

        // Each activity depends on two in the previous layer, which is what gives the graph
        // more distinct start-to-end paths than it has edges.
        private static ArrowGraphBuilder<int, int, int, IActivity<int, int, int>> BuildBranchingGraph()
        {
            var builder = new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(
                new NextIdGenerator<int>(1_000_000),
                new NextIdGenerator<int>(0));

            int perLayer = c_BranchingActivityCount / c_BranchingLayerCount;
            for (int id = 1; id <= c_BranchingActivityCount; id++)
            {
                int layer = (id - 1) / perLayer;
                var dependencies = new HashSet<int>();
                if (layer > 0)
                {
                    int previousStart = ((layer - 1) * perLayer) + 1;
                    dependencies.Add(previousStart + ((id * 7) % perLayer));
                    dependencies.Add(previousStart + ((id * 13) % perLayer));
                }
                builder.AddActivity(new Activity<int, int, int>(id, 1 + (id % 9)), dependencies);
            }

            return builder;
        }
    }
}
