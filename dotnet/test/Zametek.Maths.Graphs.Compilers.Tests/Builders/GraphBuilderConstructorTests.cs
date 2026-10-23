using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    // The builders have convenience constructors that take only the ID generator factories. They get their standard
    // engines from the engines bundle. These tests show that they use the factories that they are given, also when
    // they load a graph. The builder asks a factory for a generator for the IDs that the graph holds, so that the
    // events (vertex), and the dummy activities and the nodes (arrow), that the builder makes later get IDs from the
    // generator that are clear of those IDs.
    public class GraphBuilderConstructorTests
    {
        #region Helpers

        // A factory that records the IDs that it is asked about, and gives generators that step up from 1,000.
        private sealed class RecordingIdGeneratorFactory
            : IIdGeneratorFactory<int>
        {
            public List<int[]> Requests { get; } = [];

            public IIdGenerator<int> Create(IEnumerable<int> usedIds)
            {
                Requests.Add([.. usedIds]);
                return new NextIdGenerator<int>(1_000);
            }
        }

        // A factory that gives no generator, which it must not do.
        private sealed class NullIdGeneratorFactory
            : IIdGeneratorFactory<int>
        {
            public IIdGenerator<int> Create(IEnumerable<int> usedIds) => null;
        }

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

        private static VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> NewVertexBuilder(IIdGeneratorFactory<int> factory) =>
            new(factory);

        private static ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>> NewArrowBuilder(
            IIdGeneratorFactory<int> edgeFactory,
            IIdGeneratorFactory<int> nodeFactory) =>
            new(edgeFactory, nodeFactory);

        #endregion

        #region Vertex

        [Fact]
        public void VertexGraphBuilder_GivenAnIdGeneratorFactory_ThenTheEventsHaveIdsFromIt()
        {
            VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> builder = NewVertexBuilder(new NextIdGeneratorFactory<int>(1000));

            AddVertexPlan(builder);

            builder.EventIds.ShouldNotBeEmpty();
            builder.EventIds.ShouldAllBe(x => x > 1000);
        }

        [Fact]
        public void VertexGraphBuilder_GivenAGraphAndAnIdGeneratorFactory_ThenTheEventsAddedLaterHaveIdsFromIt()
        {
            VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> first = NewVertexBuilder(new NextIdGeneratorFactory<int>(0));
            AddVertexPlan(first);

            var second = new VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(first.ToGraph(), new NextIdGeneratorFactory<int>(1000));
            second.AddActivity(new DependentActivity<int, int, int>(3, 2), [2]).ShouldBeTrue();

            // The events of the graph keep their IDs, and the new ones come from the generator.
            first.EventIds.ShouldBeSubsetOf(second.EventIds);
            List<int> added = second.EventIds.Except(first.EventIds).ToList();
            added.ShouldNotBeEmpty();
            added.ShouldAllBe(x => x > 1000);
        }

        [Fact]
        public void VertexGraphBuilder_GivenAGraphAndAFactoryThatStartsInsideIt_ThenTheEventsAddedLaterDoNotReuseItsIds()
        {
            // The graph has the event 1,001. A generator that stepped up from 1,000 would give 1,001 again.
            VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> first = NewVertexBuilder(new NextIdGeneratorFactory<int>(1000));
            AddVertexPlan(first);
            first.EventIds.ShouldBe([1001]);

            var second = new VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(first.ToGraph(), new NextIdGeneratorFactory<int>(1000));
            second.AddActivity(new DependentActivity<int, int, int>(3, 2), [2]).ShouldBeTrue();

            second.EventIds.Order().ShouldBe([1001, 1002]);
        }

        [Fact]
        public void VertexGraphBuilder_GivenAGraphAndTheStandardFactory_ThenTheEventsAddedLaterStepDownFromTheLowestOne()
        {
            VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> first = NewVertexBuilder(new StandardIdGeneratorFactory<int>());
            AddVertexPlan(first);
            first.EventIds.ShouldBe([-1]);

            var second = new VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(first.ToGraph(), new StandardIdGeneratorFactory<int>());
            second.AddActivity(new DependentActivity<int, int, int>(3, 2), [2]).ShouldBeTrue();

            second.EventIds.Order().ShouldBe([-2, -1]);
        }

        [Fact]
        public void VertexGraphBuilder_GivenANewBuilder_ThenTheFactoryIsAskedForAGeneratorForNoIds()
        {
            var factory = new RecordingIdGeneratorFactory();

            NewVertexBuilder(factory);

            factory.Requests.Count.ShouldBe(1);
            factory.Requests[0].ShouldBeEmpty();
        }

        [Fact]
        public void VertexGraphBuilder_GivenAGraph_ThenTheFactoryIsAskedForAGeneratorForTheIdsOfItsEdges()
        {
            VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> first = NewVertexBuilder(new NextIdGeneratorFactory<int>(0));
            AddVertexPlan(first);
            Graph<int, IEvent<int>, IDependentActivity<int, int, int>> graph = first.ToGraph();
            var factory = new RecordingIdGeneratorFactory();

            _ = new VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(graph, factory);

            factory.Requests.Count.ShouldBe(1);
            factory.Requests[0].Order().ShouldBe(graph.Edges.Select(x => x.Id).Order());
            factory.Requests[0].ShouldNotBeEmpty();
        }

        [Fact]
        public void VertexGraphBuilder_GivenAFactoryThatGivesNoGenerator_ThenThrows()
        {
            VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> first = NewVertexBuilder(new NextIdGeneratorFactory<int>(0));
            AddVertexPlan(first);

            var onNew = Should.Throw<InvalidOperationException>(() => NewVertexBuilder(new NullIdGeneratorFactory()));
            var onGraph = Should.Throw<InvalidOperationException>(
                () => new VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(first.ToGraph(), new NullIdGeneratorFactory()));

            onNew.Message.ShouldContain("null generator");
            onGraph.Message.ShouldContain("null generator");
        }

        [Fact]
        public void VertexGraphBuilder_GivenABundleWithoutAnIdGeneratorFactory_ThenThrowsAndNamesTheFactory()
        {
            var engines = new VertexGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>> { EdgeIdGeneratorFactory = null };

            var thrown = Should.Throw<ArgumentNullException>(() => new VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(engines));

            thrown.ParamName.ShouldBe("edgeIdGeneratorFactory");
        }

        #endregion

        #region Arrow

        [Fact]
        public void ArrowGraphBuilder_GivenIdGeneratorFactories_ThenTheDummyActivitiesAndTheNodesHaveIdsFromThem()
        {
            ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>> builder = NewArrowBuilder(
                new NextIdGeneratorFactory<int>(1000),
                new NextIdGeneratorFactory<int>(2000));

            AddArrowPlan(builder);

            builder.NodeIds.ShouldNotBeEmpty();
            builder.NodeIds.ShouldAllBe(x => x > 2000);
            List<int> dummies = builder.EdgeIds.Except([1, 2, 3, 4]).ToList();
            dummies.ShouldNotBeEmpty();
            dummies.ShouldAllBe(x => x > 1000);
        }

        [Fact]
        public void ArrowGraphBuilder_GivenAGraphAndIdGeneratorFactories_ThenTheDummyActivitiesAndTheNodesAddedLaterHaveIdsFromThem()
        {
            ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>> first = NewArrowBuilder(
                new NextIdGeneratorFactory<int>(100),
                new NextIdGeneratorFactory<int>(200));
            AddArrowPlan(first);

            var second = new ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(
                first.ToGraph(),
                new NextIdGeneratorFactory<int>(1000),
                new NextIdGeneratorFactory<int>(2000));
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

        [Fact]
        public void ArrowGraphBuilder_GivenAGraphAndFactoriesThatStartInsideIt_ThenWhatIsMadeLaterDoesNotReuseItsIds()
        {
            // The factories start at the IDs that the graph already holds: a generator that stepped up from there
            // would give them again.
            ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>> first = NewArrowBuilder(
                new NextIdGeneratorFactory<int>(100),
                new NextIdGeneratorFactory<int>(200));
            AddArrowPlan(first);
            int highestNode = first.NodeIds.Max();
            int highestEdge = first.EdgeIds.Max();

            var second = new ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(
                first.ToGraph(),
                new NextIdGeneratorFactory<int>(100),
                new NextIdGeneratorFactory<int>(200));
            second.AddActivity(new DependentActivity<int, int, int>(5, 1), [3, 4]).ShouldBeTrue();

            List<int> nodes = second.NodeIds.Except(first.NodeIds).ToList();
            nodes.ShouldNotBeEmpty();
            nodes.ShouldAllBe(x => x > highestNode);
            List<int> dummies = second.EdgeIds.Except(first.EdgeIds).Except([5]).ToList();
            dummies.ShouldNotBeEmpty();
            dummies.ShouldAllBe(x => x > highestEdge);
        }

        [Fact]
        public void ArrowGraphBuilder_GivenAGraphAndTheStandardFactories_ThenWhatIsMadeLaterStepsDownFromTheLowestId()
        {
            ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>> first = NewArrowBuilder(
                new StandardIdGeneratorFactory<int>(),
                new StandardIdGeneratorFactory<int>());
            AddArrowPlan(first);
            int lowestNode = first.NodeIds.Min();
            int lowestEdge = first.EdgeIds.Min();

            var second = new ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(
                first.ToGraph(),
                new StandardIdGeneratorFactory<int>(),
                new StandardIdGeneratorFactory<int>());
            second.AddActivity(new DependentActivity<int, int, int>(5, 1), [3, 4]).ShouldBeTrue();

            List<int> nodes = second.NodeIds.Except(first.NodeIds).ToList();
            nodes.ShouldNotBeEmpty();
            nodes.ShouldAllBe(x => x < lowestNode);
            List<int> dummies = second.EdgeIds.Except(first.EdgeIds).Except([5]).ToList();
            dummies.ShouldNotBeEmpty();
            dummies.ShouldAllBe(x => x < lowestEdge);
        }

        [Fact]
        public void ArrowGraphBuilder_GivenANewBuilder_ThenTheFactoriesAreAskedForGeneratorsForNoIds()
        {
            var edgeFactory = new RecordingIdGeneratorFactory();
            var nodeFactory = new RecordingIdGeneratorFactory();

            NewArrowBuilder(edgeFactory, nodeFactory);

            edgeFactory.Requests.Count.ShouldBe(1);
            edgeFactory.Requests[0].ShouldBeEmpty();
            nodeFactory.Requests.Count.ShouldBe(1);
            nodeFactory.Requests[0].ShouldBeEmpty();
        }

        [Fact]
        public void ArrowGraphBuilder_GivenAGraph_ThenTheFactoriesAreAskedForGeneratorsForTheIdsOfItsEdgesAndNodes()
        {
            ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>> first = NewArrowBuilder(
                new NextIdGeneratorFactory<int>(100),
                new NextIdGeneratorFactory<int>(200));
            AddArrowPlan(first);
            Graph<int, IDependentActivity<int, int, int>, IEvent<int>> graph = first.ToGraph();
            var edgeFactory = new RecordingIdGeneratorFactory();
            var nodeFactory = new RecordingIdGeneratorFactory();

            _ = new ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(graph, edgeFactory, nodeFactory);

            edgeFactory.Requests.Count.ShouldBe(1);
            edgeFactory.Requests[0].Order().ShouldBe(graph.Edges.Select(x => x.Id).Order());
            edgeFactory.Requests[0].ShouldNotBeEmpty();
            nodeFactory.Requests.Count.ShouldBe(1);
            nodeFactory.Requests[0].Order().ShouldBe(graph.Nodes.Select(x => x.Id).Order());
            nodeFactory.Requests[0].ShouldNotBeEmpty();
        }

        [Fact]
        public void ArrowGraphBuilder_GivenAFactoryThatGivesNoGenerator_ThenThrows()
        {
            ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>> first = NewArrowBuilder(
                new NextIdGeneratorFactory<int>(100),
                new NextIdGeneratorFactory<int>(200));
            AddArrowPlan(first);
            Graph<int, IDependentActivity<int, int, int>, IEvent<int>> graph = first.ToGraph();

            var onNewEdge = Should.Throw<InvalidOperationException>(() => NewArrowBuilder(new NullIdGeneratorFactory(), new NextIdGeneratorFactory<int>()));
            var onNewNode = Should.Throw<InvalidOperationException>(() => NewArrowBuilder(new NextIdGeneratorFactory<int>(), new NullIdGeneratorFactory()));
            var onGraphEdge = Should.Throw<InvalidOperationException>(
                () => new ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(graph, new NullIdGeneratorFactory(), new NextIdGeneratorFactory<int>()));
            var onGraphNode = Should.Throw<InvalidOperationException>(
                () => new ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(graph, new NextIdGeneratorFactory<int>(), new NullIdGeneratorFactory()));

            onNewEdge.Message.ShouldContain("null generator");
            onNewNode.Message.ShouldContain("null generator");
            onGraphEdge.Message.ShouldContain("null generator");
            onGraphNode.Message.ShouldContain("null generator");
        }

        [Theory]
        [InlineData("EdgeIdGeneratorFactory", "edgeIdGeneratorFactory")]
        [InlineData("NodeIdGeneratorFactory", "nodeIdGeneratorFactory")]
        public void ArrowGraphBuilder_GivenABundleWithoutAnIdGeneratorFactory_ThenThrowsAndNamesTheFactory(string missing, string parameter)
        {
            var engines = new ArrowGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>>();
            if (missing == nameof(engines.EdgeIdGeneratorFactory))
            {
                engines.EdgeIdGeneratorFactory = null;
            }
            else
            {
                engines.NodeIdGeneratorFactory = null;
            }

            var thrown = Should.Throw<ArgumentNullException>(() => new ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(engines));

            thrown.ParamName.ShouldBe(parameter);
        }

        #endregion
    }
}
