using Shouldly;
using System.Collections.Generic;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    // Regression test for the iterative strong-dependency walk: a dummy chain deep enough
    // to overflow the call stack under the old recursive implementation must still resolve.
    //
    // Note: there is deliberately no deep-chain *transitive reduction* test here. Reduction
    // builds an ancestor-node lookup that is O(N^2) in the number of nodes for a linear
    // chain (each node stores the set of all its ancestors), so a chain long enough to
    // stress recursion depth exhausts memory first - that is a property of the reduction
    // algorithm, not of the (now iterative) traversal, and cannot be guarded cheaply.
    public class DeepDependencyWalkTests
    {
        private const int c_ChainLength = 20000;

        [Fact]
        public void VertexGraphBuilder_GivenVeryDeepDummyChain_ThenStrongActivityDependencyIdsResolvesRealRootWithoutStackOverflow()
        {
            var builder = new VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(
                new VertexGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>>());

            // Activity 1 is real; every later activity is a zero-duration (dummy) link, so the
            // strong-dependency walk must pass transparently through the whole dummy chain to 1.
            builder.AddActivity(new DependentActivity<int, int, int>(1, 1), []);
            for (int id = 2; id <= c_ChainLength; id++)
            {
                builder.AddActivity(new DependentActivity<int, int, int>(id, 0), [id - 1]);
            }

            List<int> strongDependencies = builder.StrongActivityDependencyIds(c_ChainLength);

            strongDependencies.ShouldBe([1]);
        }

        [Fact]
        public void ArrowGraphBuilder_GivenVeryDeepDependencyChain_ThenBuildsAndResolvesInLinearTime()
        {
            // Building a chain used to be O(N^2) (each AddActivity intersected the whole edge
            // set); this exercises the linear path. The dummy-activity ID generator starts above
            // the chain length so dummy edge IDs never collide with the real activity IDs.
            var builder = new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(
                new NextIdGenerator<int>(c_ChainLength),
                new NextIdGenerator<int>(0));

            builder.AddActivity(new Activity<int, int, int>(1, 1));
            for (int id = 2; id <= c_ChainLength; id++)
            {
                builder.AddActivity(new Activity<int, int, int>(id, 1), [id - 1]);
            }

            builder.StrongActivityDependencyIds(c_ChainLength).ShouldBe([c_ChainLength - 1]);
        }
    }
}
