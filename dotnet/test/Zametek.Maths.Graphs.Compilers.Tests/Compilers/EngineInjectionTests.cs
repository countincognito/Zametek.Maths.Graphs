using Shouldly;
using System;
using System.Collections.Generic;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    // Demonstrates that the now-public engine seams let an external-style consumer
    // inject custom engines through the public builder/compiler constructors,
    // programming only against the public interfaces (including the read-only
    // IVertexGraphState contract) without any access to internal types.
    public class EngineInjectionTests
    {
        // Wraps the real CPM engine and records how often each pass is invoked.
        private sealed class SpyVertexCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity>
            : IVertexCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity>
            where T : struct, IComparable<T>, IEquatable<T>
            where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
            where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
            where TActivity : IActivity<T, TResourceId, TWorkStreamId>
        {
            private readonly IVertexCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity> m_Inner;

            public SpyVertexCriticalPathEngine(IVertexCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity> inner)
            {
                m_Inner = inner;
            }

            public int ForwardFlowCallCount { get; private set; }

            public int BackwardFlowCallCount { get; private set; }

            public int BackFillCallCount { get; private set; }

            public bool CalculateCriticalPathForwardFlow(
                IVertexGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
                List<IInvalidConstraint<T>> invalidConstraints,
                bool shuffle)
            {
                ForwardFlowCallCount++;
                return m_Inner.CalculateCriticalPathForwardFlow(state, invalidConstraints, shuffle);
            }

            public bool CalculateCriticalPathBackwardFlow(
                IVertexGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
                List<IInvalidConstraint<T>> invalidConstraints,
                bool shuffle)
            {
                BackwardFlowCallCount++;
                return m_Inner.CalculateCriticalPathBackwardFlow(state, invalidConstraints, shuffle);
            }

            public bool BackFillIsolatedNodes(
                IVertexGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
                List<IInvalidConstraint<T>> invalidConstraints)
            {
                BackFillCallCount++;
                return m_Inner.BackFillIsolatedNodes(state, invalidConstraints);
            }
        }

        // Wraps the real event generator and counts how often it is asked for an event.
        private sealed class CountingEventGenerator<T>
            : IEventGenerator<T>
            where T : struct, IComparable<T>, IEquatable<T>
        {
            private readonly IEventGenerator<T> m_Inner;

            public CountingEventGenerator(IEventGenerator<T> inner)
            {
                m_Inner = inner;
            }

            public int GenerateCallCount { get; private set; }

            public IEvent<T> Generate(T id)
            {
                GenerateCallCount++;
                return m_Inner.Generate(id);
            }

            public IEvent<T> Generate(T id, int? earliestFinishTime, int? latestFinishTime)
            {
                GenerateCallCount++;
                return m_Inner.Generate(id, earliestFinishTime, latestFinishTime);
            }
        }

        [Fact]
        public void VertexGraphCompiler_GivenInjectedCriticalPathEngine_ThenCustomEngineIsUsedDuringCompile()
        {
            var spy = new SpyVertexCriticalPathEngine<int, int, int, IDependentActivity<int, int, int>>(
                new VertexCriticalPathEngine<int, int, int, IDependentActivity<int, int, int>>());

            var builder = new VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(
                new PreviousIdGenerator<int>(),
                new RemovableEventGenerator<int>(),
                new VertexTarjanStronglyConnectedComponentsFinder<int, int, int, IDependentActivity<int, int, int>>(),
                spy,
                new PriorityListResourceScheduler<int, int, int>());

            var compiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>(builder);

            compiler.AddActivity(new DependentActivity<int, int, int>(1, 3));
            var activity2 = new DependentActivity<int, int, int>(2, 5);
            activity2.Dependencies.Add(1);
            compiler.AddActivity(activity2);

            compiler.Compile();

            spy.ForwardFlowCallCount.ShouldBeGreaterThan(0);
            spy.BackwardFlowCallCount.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void VertexGraphCompiler_GivenInjectedEventGenerator_ThenCustomGeneratorIsUsed()
        {
            var counting = new CountingEventGenerator<int>(new RemovableEventGenerator<int>());

            var builder = new VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(
                new PreviousIdGenerator<int>(),
                counting,
                new VertexTarjanStronglyConnectedComponentsFinder<int, int, int, IDependentActivity<int, int, int>>(),
                new VertexCriticalPathEngine<int, int, int, IDependentActivity<int, int, int>>(),
                new PriorityListResourceScheduler<int, int, int>());

            var compiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>(builder);

            // Activity 2 depends on activity 1, so linking them creates an edge whose
            // event is produced by the injected generator.
            compiler.AddActivity(new DependentActivity<int, int, int>(1, 3));
            var activity2 = new DependentActivity<int, int, int>(2, 5);
            activity2.Dependencies.Add(1);
            compiler.AddActivity(activity2);

            counting.GenerateCallCount.ShouldBeGreaterThan(0);
        }
    }
}
