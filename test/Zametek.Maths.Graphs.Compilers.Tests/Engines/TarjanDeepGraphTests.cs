using Shouldly;
using System;
using System.Collections.Generic;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    // Regression tests for the iterative Tarjan implementation: a dependency chain
    // deep enough to overflow the call stack under the old recursive depth-first
    // search must still be searchable.
    public class TarjanDeepGraphTests
    {
        private const int ChainLength = 20000;

        [Fact]
        public void VertexGraphBuilder_GivenVeryDeepDependencyChain_ThenFindStrongCircularDependenciesCompletesWithoutStackOverflow()
        {
            var builder = new VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(
                new VertexGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>>());

            builder.AddActivity(new DependentActivity<int, int, int>(1, 1), new HashSet<int>());
            for (int id = 2; id <= ChainLength; id++)
            {
                builder.AddActivity(new DependentActivity<int, int, int>(id, 1), new HashSet<int> { id - 1 });
            }

            List<ICircularDependency<int>> output = builder.FindStrongCircularDependencies();

            output.ShouldBeEmpty();
        }

        [Fact]
        public void VertexGraphBuilder_GivenVeryDeepDependencyChainClosedIntoCycle_ThenCycleIsFound()
        {
            var builder = new VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(
                new VertexGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>>());

            // Close the chain into one giant cycle: 1 depends on the final activity.
            builder.AddActivity(new DependentActivity<int, int, int>(1, 1), new HashSet<int> { ChainLength });
            for (int id = 2; id <= ChainLength; id++)
            {
                builder.AddActivity(new DependentActivity<int, int, int>(id, 1), new HashSet<int> { id - 1 });
            }

            List<ICircularDependency<int>> output = builder.FindStrongCircularDependencies();

            output.Count.ShouldBe(1);
            output[0].Dependencies.Count.ShouldBe(ChainLength);
        }
    }
}
