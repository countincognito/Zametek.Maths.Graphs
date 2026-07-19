using Shouldly;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    // Demonstrates the engines-bundle constructors and the injectable stateless
    // engines (the transitive reducer and the dummy-edge orchestrator). Because the
    // engines are stateless - the builder passes them the graph state and any
    // collaborators per call - they are injected directly, with no factory seam.
    public class EngineBundleInjectionTests
    {
        // Wraps the default reducer and counts how often it reduces a graph.
        private sealed class CountingVertexTransitiveReducer
            : IVertexTransitiveReducer<int, int, int, IDependentActivity<int, int, int>>
        {
            private readonly VertexTransitiveReducer<int, int, int, IDependentActivity<int, int, int>> m_Inner = new();

            public int ReduceGraphCallCount { get; private set; }

            public Dictionary<int, HashSet<int>> GetAncestorNodesLookup(
                VertexGraphState<int, int, int, IDependentActivity<int, int, int>> state,
                IVertexStronglyConnectedComponentsFinder<int, int, int, IDependentActivity<int, int, int>> sccFinder)
                => m_Inner.GetAncestorNodesLookup(state, sccFinder);

            public bool ReduceGraph(
                VertexGraphState<int, int, int, IDependentActivity<int, int, int>> state,
                IVertexStronglyConnectedComponentsFinder<int, int, int, IDependentActivity<int, int, int>> sccFinder)
            {
                ReduceGraphCallCount++;
                return m_Inner.ReduceGraph(state, sccFinder);
            }
        }

        // Wraps the default orchestrator and counts how often it wires a dummy edge.
        private sealed class CountingDummyEdgeOrchestrator
            : IDummyEdgeOrchestrator<int, int, int, IDependentActivity<int, int, int>>
        {
            private readonly DummyEdgeOrchestrator<int, int, int, IDependentActivity<int, int, int>> m_Inner = new();

            public int ConnectCallCount { get; private set; }

            public void ConnectWithDummyEdge(
                ArrowGraphState<int, int, int, IDependentActivity<int, int, int>> state,
                IIdGenerator<int> edgeIdGenerator,
                IActivityGenerator<int, int, int, IDependentActivity<int, int, int>> dummyActivityGenerator,
                Node<int, IEvent<int>> tailNode,
                Node<int, IEvent<int>> headNode)
            {
                ConnectCallCount++;
                m_Inner.ConnectWithDummyEdge(state, edgeIdGenerator, dummyActivityGenerator, tailNode, headNode);
            }

            public bool RemoveDummyActivity(
                ArrowGraphState<int, int, int, IDependentActivity<int, int, int>> state,
                int activityId)
                => m_Inner.RemoveDummyActivity(state, activityId);

            public bool RedirectDummyEdges(
                ArrowGraphState<int, int, int, IDependentActivity<int, int, int>> state,
                IArrowStronglyConnectedComponentsFinder<int, int, int, IDependentActivity<int, int, int>> sccFinder)
                => m_Inner.RedirectDummyEdges(state, sccFinder);

            public bool RemoveRedundantDummyEdges(
                ArrowGraphState<int, int, int, IDependentActivity<int, int, int>> state,
                IArrowStronglyConnectedComponentsFinder<int, int, int, IDependentActivity<int, int, int>> sccFinder)
                => m_Inner.RemoveRedundantDummyEdges(state, sccFinder);

            public List<Edge<int, IDependentActivity<int, int, int>>> GetDummyEdgesInDescendingOrder(
                ArrowGraphState<int, int, int, IDependentActivity<int, int, int>> state)
                => m_Inner.GetDummyEdgesInDescendingOrder(state);
        }

        [Fact]
        public void VertexGraphBuilder_GivenDefaultEnginesBundle_ThenCompilesSuccessfully()
        {
            var builder = new VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(
                new VertexGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>>());
            var compiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>(builder);

            compiler.AddActivity(new DependentActivity<int, int, int>(1, 3));
            compiler.AddActivity(new DependentActivity<int, int, int>(2, 5, [1]));

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> output = compiler.Compile();

            output.CompilationErrors.ShouldBeEmpty();
            compiler.FinishTime.ShouldBe(8);
        }

        [Fact]
        public void VertexGraphBuilder_GivenInjectedTransitiveReducer_ThenReducerIsUsed()
        {
            var reducer = new CountingVertexTransitiveReducer();

            var builder = new VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(
                new VertexGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>>
                {
                    TransitiveReducer = reducer,
                });

            builder.AddActivity(new DependentActivity<int, int, int>(1, 1), []);
            builder.AddActivity(new DependentActivity<int, int, int>(2, 1), [1]);
            builder.AddActivity(new DependentActivity<int, int, int>(3, 1), [1, 2]);

            builder.TransitiveReduction().ShouldBeTrue();

            // The injected (stateless) reducer performed the reduction.
            reducer.ReduceGraphCallCount.ShouldBeGreaterThan(0);

            // The direct 1 -> 3 dependency is redundant (implied via 2) and is removed.
            builder.ActivityDependencyIds(3).ShouldBe([2]);
        }

        [Fact]
        public void VertexGraphBuilder_GivenInjectedTransitiveReducer_ThenReducerSurvivesClone()
        {
            var reducer = new CountingVertexTransitiveReducer();

            var builder = new VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(
                new VertexGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>>
                {
                    TransitiveReducer = reducer,
                });

            builder.AddActivity(new DependentActivity<int, int, int>(1, 1), []);
            builder.AddActivity(new DependentActivity<int, int, int>(2, 1), [1]);
            builder.AddActivity(new DependentActivity<int, int, int>(3, 1), [1, 2]);

            var clone = (VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>)builder.CloneObject();

            clone.ShouldNotBeSameAs(builder);

            // The same stateless reducer instance is preserved on the clone and used.
            clone.TransitiveReduction().ShouldBeTrue();
            reducer.ReduceGraphCallCount.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void ArrowGraphBuilder_GivenDefaultEnginesBundle_ThenBuildsGraph()
        {
            var builder = new ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(
                new ArrowGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>>());

            builder.AddActivity(new DependentActivity<int, int, int>(1, 3), []);
            builder.AddActivity(new DependentActivity<int, int, int>(2, 5), [1]);

            // The arrow builder also mints dummy activities, so check containment.
            builder.ActivityIds.ShouldContain(1);
            builder.ActivityIds.ShouldContain(2);
        }

        [Fact]
        public void ArrowGraphBuilder_GivenInjectedOrchestrator_ThenOrchestratorIsUsed()
        {
            var orchestrator = new CountingDummyEdgeOrchestrator();

            var builder = new ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(
                new ArrowGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>>
                {
                    DummyEdgeOrchestrator = orchestrator,
                });

            // The orchestrator produced by the injected engine wires up the dummy edges.
            builder.AddActivity(new DependentActivity<int, int, int>(1, 3), []);
            builder.AddActivity(new DependentActivity<int, int, int>(2, 5), [1]);

            orchestrator.ConnectCallCount.ShouldBeGreaterThan(0);
            builder.EdgeIds.Count().ShouldBeGreaterThan(2);
        }
    }
}
