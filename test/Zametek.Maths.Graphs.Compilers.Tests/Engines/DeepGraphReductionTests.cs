using Shouldly;
using System.Collections.Generic;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    // Regression tests for the iterative transitive reducer and dependency walk: a
    // dependency chain deep enough to overflow the call stack under the old recursive
    // implementations must still reduce and resolve without a StackOverflowException.
    public class DeepGraphReductionTests
    {
        private const int c_ChainLength = 20000;

        [Fact]
        public void VertexGraphBuilder_GivenVeryDeepDependencyChain_ThenTransitiveReductionCompletesWithoutStackOverflow()
        {
            var builder = new VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(
                new VertexGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>>());

            builder.AddActivity(new DependentActivity<int, int, int>(1, 1), []);
            for (int id = 2; id <= c_ChainLength; id++)
            {
                builder.AddActivity(new DependentActivity<int, int, int>(id, 1), [id - 1]);
            }

            bool reduced = builder.TransitiveReduction();

            reduced.ShouldBeTrue();
            // A simple chain has no redundant edges, so each activity still depends on its predecessor.
            builder.ActivityDependencyIds(c_ChainLength).ShouldBe([c_ChainLength - 1]);
        }

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
    }
}
