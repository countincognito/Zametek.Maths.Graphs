using Shouldly;
using System;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    // Regression test for the cost of redirecting dummy edges (DummyEdgeOrchestrator,
    // reached through RedirectEdges, which CalculateCriticalPath runs last).
    //
    // Redirecting is quadratic by construction, and that is not what this guards: a group
    // of nodes feeding a common set of successors through removable dummy edges is
    // collapsed into a chain one link at a time, so a fan-in of k costs k(k-1)/2
    // redirections. That is inherent to the canonical form the method produces.
    //
    // What this guards is the factor that sat on top of it. Each redirection copied a
    // node's whole edge set purely to ask whether the set was empty, and the node in
    // question is the one holding the fan-in, so the copy was O(k) and the pass was cubic.
    // On the chain below that was the difference between 834 MB and 169 MB; the ceiling
    // sits between the two with a factor of two either side. Allocation is the measure
    // rather than elapsed time because it is deterministic, and because it names the
    // defect exactly - the copies were the whole of it.
    //
    // A transitively reduced graph has no removable dummy fan-in left at all, so it
    // redirects nothing and none of this arises, which is why the chain here is
    // deliberately left un-reduced.
    public class RedirectDummyEdgesTests
    {
        private const int c_ChainLength = 1000;

        private const long c_AllocationCeilingBytes = 400L * 1024 * 1024;

        [Fact]
        public void ArrowGraphBuilder_GivenUnreducedChain_WhenCalculatingCriticalPath_ThenDoesNotCopyEdgeSetsToTestThem()
        {
            var builder = new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(
                new NextIdGenerator<int>(c_ChainLength),
                new NextIdGenerator<int>(0));

            builder.AddActivity(new Activity<int, int, int>(1, 1));
            for (int id = 2; id <= c_ChainLength; id++)
            {
                builder.AddActivity(new Activity<int, int, int>(id, 1), [id - 1]);
            }

            // Per thread rather than per process: the suite runs tests in parallel, and a
            // process-wide counter measures whatever else happens to be running too.
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

            builder.CalculateCriticalPath();

            long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

            allocated.ShouldBeLessThan(
                c_AllocationCeilingBytes,
                $@"redirecting allocated {allocated / (1024 * 1024)} MB, which means it is copying edge sets to test whether they are empty again");

            // The redirection still has to have happened: a chain of unit durations puts
            // the last activity's earliest start at one per predecessor.
            builder.Activities.Single(x => x.Id == c_ChainLength).EarliestStartTime.ShouldBe(c_ChainLength - 1);
        }
    }
}
