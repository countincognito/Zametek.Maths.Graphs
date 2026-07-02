using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    // Demonstrates the engines-bundle constructors and the factory seams for the
    // state-dependent engines (transitive reducers and the dummy-edge orchestrator).
    // Factories are the injection point because those engines are bound to the
    // builder's graph state at construction time.
    public class EngineBundleInjectionTests
    {
        // Wraps the default factory and counts how often a reducer is created.
        private sealed class CountingVertexTransitiveReducerFactory
            : IVertexTransitiveReducerFactory<int, int, int, IDependentActivity<int, int, int>>
        {
            private readonly VertexTransitiveReducerFactory<int, int, int, IDependentActivity<int, int, int>> m_Inner =
                new VertexTransitiveReducerFactory<int, int, int, IDependentActivity<int, int, int>>();

            public int CreateCallCount { get; private set; }

            public ITransitiveReducer<int> Create(
                IVertexStronglyConnectedComponentsFinder<int, int, int, IDependentActivity<int, int, int>> sccFinder,
                IVertexGraphState<int, int, int, IDependentActivity<int, int, int>> state)
            {
                CreateCallCount++;
                return m_Inner.Create(sccFinder, state);
            }
        }

        // Wraps the default factory and counts how often an orchestrator is created.
        private sealed class CountingDummyEdgeOrchestratorFactory
            : IDummyEdgeOrchestratorFactory<int, int, int, IDependentActivity<int, int, int>>
        {
            private readonly DummyEdgeOrchestratorFactory<int, int, int, IDependentActivity<int, int, int>> m_Inner =
                new DummyEdgeOrchestratorFactory<int, int, int, IDependentActivity<int, int, int>>();

            public int CreateCallCount { get; private set; }

            public IDummyEdgeOrchestrator<int, int, int, IDependentActivity<int, int, int>> Create(
                IIdGenerator<int> edgeIdGenerator,
                IActivityGenerator<int, int, int, IDependentActivity<int, int, int>> dummyActivityGenerator,
                IArrowStronglyConnectedComponentsFinder<int, int, int, IDependentActivity<int, int, int>> sccFinder,
                IArrowGraphState<int, int, int, IDependentActivity<int, int, int>> state)
            {
                CreateCallCount++;
                return m_Inner.Create(edgeIdGenerator, dummyActivityGenerator, sccFinder, state);
            }
        }

        [Fact]
        public void VertexGraphBuilder_GivenDefaultEnginesBundle_ThenCompilesSuccessfully()
        {
            var builder = new VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(
                new VertexGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>>());
            var compiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>(builder);

            compiler.AddActivity(new DependentActivity<int, int, int>(1, 3));
            compiler.AddActivity(new DependentActivity<int, int, int>(2, 5, new[] { 1 }));

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> output = compiler.Compile();

            output.CompilationErrors.ShouldBeEmpty();
            compiler.FinishTime.ShouldBe(8);
        }

        [Fact]
        public void VertexGraphBuilder_GivenInjectedTransitiveReducerFactory_ThenFactoryIsUsed()
        {
            var factory = new CountingVertexTransitiveReducerFactory();

            var builder = new VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(
                new VertexGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>>
                {
                    TransitiveReducerFactory = factory,
                });

            factory.CreateCallCount.ShouldBeGreaterThan(0);

            // The reducer produced by the injected factory performs the reduction.
            builder.AddActivity((IDependentActivity<int, int, int>)new DependentActivity<int, int, int>(1, 1), new HashSet<int>());
            builder.AddActivity((IDependentActivity<int, int, int>)new DependentActivity<int, int, int>(2, 1), new HashSet<int> { 1 });
            builder.AddActivity((IDependentActivity<int, int, int>)new DependentActivity<int, int, int>(3, 1), new HashSet<int> { 1, 2 });

            builder.TransitiveReduction().ShouldBeTrue();

            // The direct 1 -> 3 dependency is redundant (implied via 2) and is removed.
            builder.ActivityDependencyIds(3).ShouldBe(new[] { 2 });
        }

        [Fact]
        public void VertexGraphBuilder_GivenInjectedTransitiveReducerFactory_ThenFactorySurvivesClone()
        {
            var factory = new CountingVertexTransitiveReducerFactory();

            var builder = new VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(
                new VertexGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>>
                {
                    TransitiveReducerFactory = factory,
                });

            int countBeforeClone = factory.CreateCallCount;

            var clone = (VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>)builder.CloneObject();

            clone.ShouldNotBeSameAs(builder);
            factory.CreateCallCount.ShouldBeGreaterThan(countBeforeClone);
        }

        [Fact]
        public void ArrowGraphBuilder_GivenDefaultEnginesBundle_ThenBuildsGraph()
        {
            var builder = new ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(
                new ArrowGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>>());

            builder.AddActivity((IDependentActivity<int, int, int>)new DependentActivity<int, int, int>(1, 3), new HashSet<int>());
            builder.AddActivity((IDependentActivity<int, int, int>)new DependentActivity<int, int, int>(2, 5), new HashSet<int> { 1 });

            // The arrow builder also mints dummy activities, so check containment.
            builder.ActivityIds.ShouldContain(1);
            builder.ActivityIds.ShouldContain(2);
        }

        [Fact]
        public void ArrowGraphBuilder_GivenInjectedOrchestratorFactory_ThenFactoryIsUsed()
        {
            var factory = new CountingDummyEdgeOrchestratorFactory();

            var builder = new ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(
                new ArrowGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>>
                {
                    DummyEdgeOrchestratorFactory = factory,
                });

            factory.CreateCallCount.ShouldBeGreaterThan(0);

            // The orchestrator produced by the injected factory wires up the dummy edges.
            builder.AddActivity((IDependentActivity<int, int, int>)new DependentActivity<int, int, int>(1, 3), new HashSet<int>());
            builder.AddActivity((IDependentActivity<int, int, int>)new DependentActivity<int, int, int>(2, 5), new HashSet<int> { 1 });

            builder.EdgeIds.Count().ShouldBeGreaterThan(2);
        }
    }
}
