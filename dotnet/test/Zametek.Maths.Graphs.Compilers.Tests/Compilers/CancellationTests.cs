using Shouldly;
using System;
using System.Collections.Generic;
using System.Threading;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    // The safety net for cancellation.
    //
    // A cancellation token that is only checked on entry is worse than none at all: it
    // tells a caller the operation is interruptible when in fact it is not, and the
    // caller has no way to find out short of trying. So the tests that matter here are
    // the mid-flight ones, which cancel from inside the calculation and then assert that
    // it stopped where it was rather than running to completion.
    //
    // "Stopped where it was" is measured by counting the loop iterations that actually
    // happened. Throwing is not enough on its own - a loop with no check in it still
    // throws, just later, once control returns to the caller.
    public class CancellationTests
    {
        #region Spies

        // Cancels the token once the arrow critical-path engine has been asked for a
        // forward pass a given number of times. The priority-list loop performs exactly
        // one such pass per iteration, so the count is the iteration number.
        private sealed class CancellingArrowCriticalPathEngine
            : IArrowCriticalPathEngine<int, int, int, IDependentActivity<int, int, int>>
        {
            private readonly IArrowCriticalPathEngine<int, int, int, IDependentActivity<int, int, int>> m_Inner =
                new ArrowCriticalPathEngine<int, int, int, IDependentActivity<int, int, int>>();
            private readonly CancellationTokenSource m_CancellationTokenSource;
            private readonly int m_CancelAfter;

            internal CancellingArrowCriticalPathEngine(CancellationTokenSource cancellationTokenSource, int cancelAfter)
            {
                m_CancellationTokenSource = cancellationTokenSource;
                m_CancelAfter = cancelAfter;
            }

            internal int ForwardPassCount { get; private set; }

            public bool CalculateEventEarliestFinishTimes(
                IArrowGraphState<int, int, int, IDependentActivity<int, int, int>> state,
                List<IInvalidConstraint<int>> invalidConstraints,
                bool shuffle)
            {
                ForwardPassCount++;
                if (ForwardPassCount == m_CancelAfter)
                {
                    m_CancellationTokenSource.Cancel();
                }
                return m_Inner.CalculateEventEarliestFinishTimes(state, invalidConstraints, shuffle);
            }

            public bool CalculateEventLatestFinishTimes(
                IArrowGraphState<int, int, int, IDependentActivity<int, int, int>> state,
                List<IInvalidConstraint<int>> invalidConstraints,
                bool shuffle) =>
                m_Inner.CalculateEventLatestFinishTimes(state, invalidConstraints, shuffle);

            public bool CalculateCriticalPathVariables(
                IArrowGraphState<int, int, int, IDependentActivity<int, int, int>> state,
                List<IInvalidConstraint<int>> invalidConstraints) =>
                m_Inner.CalculateCriticalPathVariables(state, invalidConstraints);
        }

        // The vertex equivalent. Since Phase 3 the vertex priority-list loop calls the
        // incremental session once per iteration rather than the engine, so the count
        // lives on the session the engine hands out.
        private sealed class CancellingVertexCriticalPathEngine
            : IVertexCriticalPathEngine<int, int, int, IDependentActivity<int, int, int>>
        {
            private readonly IVertexCriticalPathEngine<int, int, int, IDependentActivity<int, int, int>> m_Inner =
                new VertexCriticalPathEngine<int, int, int, IDependentActivity<int, int, int>>();
            private readonly CancellationTokenSource m_CancellationTokenSource;
            private readonly int m_CancelAfter;

            internal CancellingVertexCriticalPathEngine(CancellationTokenSource cancellationTokenSource, int cancelAfter)
            {
                m_CancellationTokenSource = cancellationTokenSource;
                m_CancelAfter = cancelAfter;
            }

            internal int DurationChangeCount { get; private set; }

            public bool CalculateCriticalPathForwardFlow(
                IVertexGraphState<int, int, int, IDependentActivity<int, int, int>> state,
                List<IInvalidConstraint<int>> invalidConstraints,
                bool shuffle) =>
                m_Inner.CalculateCriticalPathForwardFlow(state, invalidConstraints, shuffle);

            public bool CalculateCriticalPathBackwardFlow(
                IVertexGraphState<int, int, int, IDependentActivity<int, int, int>> state,
                List<IInvalidConstraint<int>> invalidConstraints,
                bool shuffle) =>
                m_Inner.CalculateCriticalPathBackwardFlow(state, invalidConstraints, shuffle);

            public bool BackFillIsolatedNodes(
                IVertexGraphState<int, int, int, IDependentActivity<int, int, int>> state,
                List<IInvalidConstraint<int>> invalidConstraints) =>
                m_Inner.BackFillIsolatedNodes(state, invalidConstraints);

            public IVertexIncrementalCriticalPath<int> BeginIncrementalCriticalPath(
                IVertexGraphState<int, int, int, IDependentActivity<int, int, int>> state)
            {
                IVertexIncrementalCriticalPath<int> session = m_Inner.BeginIncrementalCriticalPath(state);
                return session is null ? null : new CountingSession(this, session);
            }

            private sealed class CountingSession
                : IVertexIncrementalCriticalPath<int>
            {
                private readonly CancellingVertexCriticalPathEngine m_Owner;
                private readonly IVertexIncrementalCriticalPath<int> m_Inner;

                internal CountingSession(
                    CancellingVertexCriticalPathEngine owner,
                    IVertexIncrementalCriticalPath<int> inner)
                {
                    m_Owner = owner;
                    m_Inner = inner;
                }

                public bool ApplyDurationChange(int activityId)
                {
                    m_Owner.DurationChangeCount++;
                    if (m_Owner.DurationChangeCount == m_Owner.m_CancelAfter)
                    {
                        m_Owner.m_CancellationTokenSource.Cancel();
                    }
                    return m_Inner.ApplyDurationChange(activityId);
                }
            }
        }

        #endregion

        #region Helpers

        // A chain, so that the priority-list loop is guaranteed to need one iteration
        // per activity and the iteration count is unambiguous.
        private static void AddChain(
            ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>> graphBuilder,
            int length)
        {
            for (int id = 1; id <= length; id++)
            {
                graphBuilder.AddActivity(
                    new DependentActivity<int, int, int>(id, 5),
                    id == 1 ? new HashSet<int>() : new HashSet<int> { id - 1 });
            }
        }

        private static void AddChain(
            VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> graphBuilder,
            int length)
        {
            for (int id = 1; id <= length; id++)
            {
                graphBuilder.AddActivity(
                    new DependentActivity<int, int, int>(id, 5),
                    id == 1 ? new HashSet<int>() : new HashSet<int> { id - 1 });
            }
        }

        #endregion

        [Fact]
        public void ArrowGraphCompiler_GivenAlreadyCancelledToken_ThenCompileThrows()
        {
            var graphCompiler = new ArrowGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(1, 3));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(2, 5, [1]));

            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            Should.Throw<OperationCanceledException>(() => graphCompiler.Compile(cancellationTokenSource.Token));
        }

        [Fact]
        public void VertexGraphCompiler_GivenAlreadyCancelledToken_ThenCompileThrows()
        {
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(1, 3));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(2, 5, [1]));

            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            Should.Throw<OperationCanceledException>(() =>
                graphCompiler.Compile(cancellationTokenSource.Token));
        }

        [Fact]
        public void ArrowGraphBuilder_GivenTokenCancelledMidCalculation_ThenPriorityListStopsWhereItWas()
        {
            const int activityCount = 200;
            const int cancelAfter = 5;

            using var cancellationTokenSource = new CancellationTokenSource();
            var engine = new CancellingArrowCriticalPathEngine(cancellationTokenSource, cancelAfter);
            var graphBuilder = new ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(
                new ArrowGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>>
                {
                    CriticalPathEngine = engine,
                });
            AddChain(graphBuilder, activityCount);

            Should.Throw<OperationCanceledException>(() =>
                graphBuilder.CalculateCriticalPathPriorityList(cancellationTokenSource.Token));

            // The point of the test. Without a check inside the loop this would run all
            // 200 iterations and only throw afterwards - if at all.
            engine.ForwardPassCount.ShouldBeLessThan(activityCount);
            engine.ForwardPassCount.ShouldBeLessThanOrEqualTo(cancelAfter + 1);
        }

        [Fact]
        public void VertexGraphBuilder_GivenTokenCancelledMidCalculation_ThenPriorityListStopsWhereItWas()
        {
            const int activityCount = 200;
            const int cancelAfter = 5;

            using var cancellationTokenSource = new CancellationTokenSource();
            var engine = new CancellingVertexCriticalPathEngine(cancellationTokenSource, cancelAfter);
            var graphBuilder = new VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(
                new VertexGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>>
                {
                    EdgeIdGenerator = new NextIdGenerator<int>(1_000_000),
                    CriticalPathEngine = engine,
                });
            AddChain(graphBuilder, activityCount);

            Should.Throw<OperationCanceledException>(() =>
                graphBuilder.CalculateResourceSchedulesByPriorityList(
                    new List<IResource<int, int>>(),
                    cancellationTokenSource.Token));

            // CalculateResourceSchedulesByPriorityList has always accepted a token, but
            // until now it ran the whole priority list - one pass per activity - before
            // reaching the scheduler that honours it. This is what pins that shut.
            engine.DurationChangeCount.ShouldBeLessThan(activityCount);
            engine.DurationChangeCount.ShouldBeLessThanOrEqualTo(cancelAfter + 1);
        }

        [Fact]
        public void ArrowGraphBuilder_GivenTokenCancelledMidCalculation_ThenResourceSchedulingStopsWhereItWas()
        {
            const int activityCount = 200;
            const int cancelAfter = 5;

            using var cancellationTokenSource = new CancellationTokenSource();
            var engine = new CancellingArrowCriticalPathEngine(cancellationTokenSource, cancelAfter);
            var graphBuilder = new ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(
                new ArrowGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>>
                {
                    CriticalPathEngine = engine,
                });
            AddChain(graphBuilder, activityCount);

            Should.Throw<OperationCanceledException>(() =>
                graphBuilder.CalculateResourceSchedulesByPriorityList(
                    new List<IResource<int, int>>(),
                    cancellationTokenSource.Token));

            engine.ForwardPassCount.ShouldBeLessThan(activityCount);
        }

        [Fact]
        public void ArrowGraphBuilder_GivenUncancelledToken_ThenPriorityListIsUnchanged()
        {
            // Threading a token through must not change what the calculation produces.
            const int activityCount = 40;

            var withToken = new ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(
                new ArrowGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>>());
            AddChain(withToken, activityCount);

            List<int> priorityList = withToken.CalculateCriticalPathPriorityList(CancellationToken.None);

            priorityList.Count.ShouldBe(activityCount);
            priorityList.ShouldBe(withToken.CalculateCriticalPathPriorityList(TestContext.Current.CancellationToken));
        }
    }
}
