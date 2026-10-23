using Shouldly;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    // The builders have convenience constructors that take only the ID generators. They get their standard engines
    // from the engines bundle. These tests show that they use the ID generators that they are given, also when they
    // load a graph: the events (vertex), and the dummy activities and the nodes (arrow), that they make get their
    // IDs from those generators.
    public class GraphBuilderConstructorTests
    {
        private static void AddVertexPlan(VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> builder)
        {
            builder.AddActivity(new DependentActivity<int, int, int>(1, 3), []).ShouldBeTrue();
            builder.AddActivity(new DependentActivity<int, int, int>(2, 5), [1]).ShouldBeTrue();
        }

        // 3 needs 1 and 2, and 4 needs only 2, which needs a dummy activity.
        private static void AddArrowPlan(ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>> builder)
        {
            builder.AddActivity(new DependentActivity<int, int, int>(1, 3), []).ShouldBeTrue();
            builder.AddActivity(new DependentActivity<int, int, int>(2, 5), []).ShouldBeTrue();
            builder.AddActivity(new DependentActivity<int, int, int>(3, 2), [1, 2]).ShouldBeTrue();
            builder.AddActivity(new DependentActivity<int, int, int>(4, 4), [2]).ShouldBeTrue();
        }

        [Fact]
        public void VertexGraphBuilder_GivenAnIdGenerator_ThenTheEventsHaveIdsFromIt()
        {
            var builder = new VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(new NextIdGenerator<int>(1000));

            AddVertexPlan(builder);

            builder.EventIds.ShouldNotBeEmpty();
            builder.EventIds.ShouldAllBe(x => x > 1000);
        }

        [Fact]
        public void VertexGraphBuilder_GivenAGraphAndAnIdGenerator_ThenTheEventsAddedLaterHaveIdsFromIt()
        {
            var first = new VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(new NextIdGenerator<int>(0));
            AddVertexPlan(first);

            var second = new VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(first.ToGraph(), new NextIdGenerator<int>(1000));
            second.AddActivity(new DependentActivity<int, int, int>(3, 2), [2]).ShouldBeTrue();

            // The events of the graph keep their IDs, and the new ones come from the generator.
            first.EventIds.ShouldBeSubsetOf(second.EventIds);
            List<int> added = second.EventIds.Except(first.EventIds).ToList();
            added.ShouldNotBeEmpty();
            added.ShouldAllBe(x => x > 1000);
        }

        [Fact]
        public void ArrowGraphBuilder_GivenIdGenerators_ThenTheDummyActivitiesAndTheNodesHaveIdsFromThem()
        {
            var builder = new ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(
                new NextIdGenerator<int>(1000),
                new NextIdGenerator<int>(2000));

            AddArrowPlan(builder);

            builder.NodeIds.ShouldNotBeEmpty();
            builder.NodeIds.ShouldAllBe(x => x > 2000);
            List<int> dummies = builder.EdgeIds.Except([1, 2, 3, 4]).ToList();
            dummies.ShouldNotBeEmpty();
            dummies.ShouldAllBe(x => x > 1000);
        }

        [Fact]
        public void ArrowGraphBuilder_GivenAGraphAndIdGenerators_ThenTheDummyActivitiesAndTheNodesAddedLaterHaveIdsFromThem()
        {
            var first = new ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(
                new NextIdGenerator<int>(100),
                new NextIdGenerator<int>(200));
            AddArrowPlan(first);

            var second = new ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(
                first.ToGraph(),
                new NextIdGenerator<int>(1000),
                new NextIdGenerator<int>(2000));
            second.AddActivity(new DependentActivity<int, int, int>(5, 1), [3, 4]).ShouldBeTrue();

            // What the graph held keeps its IDs, and what is made later comes from the generators.
            first.NodeIds.ShouldBeSubsetOf(second.NodeIds);
            List<int> nodes = second.NodeIds.Except(first.NodeIds).ToList();
            nodes.ShouldNotBeEmpty();
            nodes.ShouldAllBe(x => x > 2000);
            List<int> dummies = second.EdgeIds.Except(first.EdgeIds).Except([5]).ToList();
            dummies.ShouldNotBeEmpty();
            dummies.ShouldAllBe(x => x > 1000);
        }
    }
}
