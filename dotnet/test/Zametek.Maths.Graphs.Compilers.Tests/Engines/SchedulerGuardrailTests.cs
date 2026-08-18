using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    // Guardrails against the scheduling livelocks diagnosed from a production hang:
    // a structurally corrupted HashSet (buckets zeroed by unsynchronized concurrent
    // modification) enumerates normally but fails every Contains lookup, which used
    // to trap CalculateResourceSchedules in an infinite tick loop. These tests prove
    // that such inputs - and every other provably-unschedulable state - now surface
    // as a P0070 pre-compilation error, a C0020 compilation error, or a
    // ResourceSchedulingStallException, instead of hanging. Each scheduling call runs
    // under a watchdog so a regression fails the test rather than wedging the run.
    public class SchedulerGuardrailTests
    {
        #region Helpers

        private sealed class FakeSchedulingGraph : IResourceSchedulingGraph<int, int, int>
        {
            private readonly Func<int, IActivity<int, int, int>> m_Activity;
            private readonly Func<int, List<int>> m_StrongDependencies;
            private readonly Func<List<IActivity<int, int, int>>> m_CloneActivities;

            public FakeSchedulingGraph(
                Func<int, IActivity<int, int, int>> activity,
                Func<int, List<int>> strongDependencies,
                Func<List<IActivity<int, int, int>>> cloneActivities)
            {
                m_Activity = activity;
                m_StrongDependencies = strongDependencies;
                m_CloneActivities = cloneActivities;
            }

            public IActivity<int, int, int> Activity(int id) => m_Activity(id);

            public List<int> StrongActivityDependencyIds(int id) => m_StrongDependencies(id);

            public List<IActivity<int, int, int>> CloneActivities() => m_CloneActivities();
        }

        private static Resource<int, int> CreateResource(int id, bool isExplicitTarget = false)
        {
            return new Resource<int, int>(id, $@"R{id}", isExplicitTarget, false, InterActivityAllocationType.None, 0.0, 0.0, 0, []);
        }

        // Reproduces the corruption observed in the production dump: the set's entries
        // survive intact (so enumeration and Count behave normally) but its bucket
        // index is zeroed, so every Contains lookup misses. In production this state
        // was captured by HashSet's copy-constructor cloning a set mid-Clear() on
        // another thread; reflection recreates it deterministically.
        private static void CorruptBuckets(HashSet<int> set)
        {
            FieldInfo bucketsField = typeof(HashSet<int>).GetField("_buckets", BindingFlags.NonPublic | BindingFlags.Instance);
            bucketsField.ShouldNotBeNull("HashSet<T> internals have changed; update this test's corruption technique.");
            var buckets = (Array)bucketsField.GetValue(set);
            buckets.ShouldNotBeNull();
            Array.Clear(buckets, 0, buckets.Length);

            // Prove the corruption took: values still enumerate but can no longer be found.
            set.Count.ShouldBeGreaterThan(0);
            foreach (int item in set)
            {
                set.Contains(item).ShouldBeFalse();
            }
        }

        // Runs the given operation on a worker so that a livelock regression fails the
        // test after a timeout instead of hanging the whole test run. Faults are
        // rethrown unwrapped so callers can assert on the original exception type.
        private static TResult RunWithWatchdog<TResult>(Func<TResult> func)
        {
            Task<TResult> task = Task.Run(func);
            bool completed;
            try
            {
                completed = task.Wait(TimeSpan.FromSeconds(30));
            }
            catch (AggregateException)
            {
                completed = true;
            }
            completed.ShouldBeTrue("The scheduling operation appears to be stuck in a loop - the stall guardrail failed.");
            return task.GetAwaiter().GetResult();
        }

        #endregion

        [Fact]
        public void VertexGraphCompiler_GivenCorruptedTargetResourceSet_ThenReportsP0070InsteadOfHanging()
        {
            var compiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            var activity = new DependentActivity<int, int, int>(2, 25)
            {
                TargetResourceOperator = LogicalOperator.AND,
            };
            activity.TargetResources.Add(12);
            activity.TargetResources.Add(14);
            CorruptBuckets(activity.TargetResources);
            compiler.AddActivity(activity);

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation =
                RunWithWatchdog(() => compiler.Compile([CreateResource(12), CreateResource(14)], TestContext.Current.CancellationToken));

            IGraphCompilationError error = compilation.CompilationErrors.ShouldHaveSingleItem();
            error.ErrorCode.ShouldBe(GraphCompilationErrorCode.P0070);
            error.ErrorMessage.ShouldContain(@"Activity 2 -> TargetResources");
            compilation.ResourceSchedules.ShouldBeEmpty();
        }

        [Fact]
        public void PriorityListResourceScheduler_GivenCorruptedTargetResourceSet_ThenStallsWithInconsistencyDiagnosisInsteadOfHanging()
        {
            var scheduler = new PriorityListResourceScheduler<int, int, int>();
            var activity = new Activity<int, int, int>(1, 5)
            {
                EarliestStartTime = 0,
                TargetResourceOperator = LogicalOperator.AND,
            };
            activity.TargetResources.Add(10);
            activity.TargetResources.Add(20);
            CorruptBuckets(activity.TargetResources);
            var graph = new FakeSchedulingGraph(id => activity, id => [], () => [activity]);

            ResourceSchedulingStallException ex = Should.Throw<ResourceSchedulingStallException>(() =>
                RunWithWatchdog(() => scheduler.CalculateResourceSchedules(
                    [1],
                    [CreateResource(10), CreateResource(20)],
                    infiniteResources: false,
                    graph, TestContext.Current.CancellationToken).ToList()));

            ex.Message.ShouldContain(@"internally inconsistent");
            ex.Message.ShouldContain(@"1 -> ");
        }

        [Fact]
        public void PriorityListResourceScheduler_GivenAndOperatorTargetingMissingResource_ThenStallsWithMissingResourceDiagnosis()
        {
            var scheduler = new PriorityListResourceScheduler<int, int, int>();
            var activity = new Activity<int, int, int>(1, 5)
            {
                EarliestStartTime = 0,
                TargetResourceOperator = LogicalOperator.AND,
            };
            activity.TargetResources.Add(99);
            var graph = new FakeSchedulingGraph(id => activity, id => [], () => [activity]);

            ResourceSchedulingStallException ex = Should.Throw<ResourceSchedulingStallException>(() =>
                RunWithWatchdog(() => scheduler.CalculateResourceSchedules(
                    [1],
                    [CreateResource(10)],
                    infiniteResources: false,
                    graph, TestContext.Current.CancellationToken).ToList()));

            ex.Message.ShouldContain(@"not available");
            ex.Message.ShouldContain(@"99");
        }

        [Fact]
        public void PriorityListResourceScheduler_GivenOnlyExplicitTargetResourcesAndUntargetedActivity_ThenStallsWithExplicitTargetDiagnosis()
        {
            var scheduler = new PriorityListResourceScheduler<int, int, int>();
            var activity = new Activity<int, int, int>(1, 5) { EarliestStartTime = 0 };
            var graph = new FakeSchedulingGraph(id => activity, id => [], () => [activity]);

            ResourceSchedulingStallException ex = Should.Throw<ResourceSchedulingStallException>(() =>
                RunWithWatchdog(() => scheduler.CalculateResourceSchedules(
                    [1],
                    [CreateResource(10, isExplicitTarget: true)],
                    infiniteResources: false,
                    graph, TestContext.Current.CancellationToken).ToList()));

            ex.Message.ShouldContain(@"explicit target");
        }

        [Fact]
        public void PriorityListResourceScheduler_GivenDependencyThatCanNeverComplete_ThenStallsWithDependencyDiagnosis()
        {
            var scheduler = new PriorityListResourceScheduler<int, int, int>();
            var activity = new Activity<int, int, int>(1, 5) { EarliestStartTime = 0 };
            var graph = new FakeSchedulingGraph(id => activity, id => [42], () => [activity]);

            ResourceSchedulingStallException ex = Should.Throw<ResourceSchedulingStallException>(() =>
                RunWithWatchdog(() => scheduler.CalculateResourceSchedules(
                    [1],
                    [CreateResource(10)],
                    infiniteResources: false,
                    graph, TestContext.Current.CancellationToken).ToList()));

            ex.Message.ShouldContain(@"can never complete");
            ex.Message.ShouldContain(@"42");
        }

        [Fact]
        public void PriorityListResourceScheduler_GivenEarliestStartTimeBeyondTheTimeHorizon_ThenStallsWithHorizonDiagnosis()
        {
            var scheduler = new PriorityListResourceScheduler<int, int, int>();
            var activity = new Activity<int, int, int>(1, 5) { EarliestStartTime = GraphLimits.MaximumTimeValue + 1 };
            var graph = new FakeSchedulingGraph(id => activity, id => [], () => [activity]);

            ResourceSchedulingStallException ex = Should.Throw<ResourceSchedulingStallException>(() =>
                RunWithWatchdog(() => scheduler.CalculateResourceSchedules(
                    [1],
                    [CreateResource(10)],
                    infiniteResources: false,
                    graph, TestContext.Current.CancellationToken).ToList()));

            ex.Message.ShouldContain(@"maximum supported time value");
        }

        [Fact]
        public void PriorityListResourceScheduler_GivenDurationThatWouldFinishBeyondTheTimeHorizon_ThenStallsWithHorizonDiagnosis()
        {
            var scheduler = new PriorityListResourceScheduler<int, int, int>();
            // Starts inside the horizon, but is too long to finish within it.
            var activity = new Activity<int, int, int>(1, GraphLimits.MaximumTimeValue)
            {
                EarliestStartTime = 1,
            };
            var graph = new FakeSchedulingGraph(id => activity, id => [], () => [activity]);

            ResourceSchedulingStallException ex = Should.Throw<ResourceSchedulingStallException>(() =>
                RunWithWatchdog(() => scheduler.CalculateResourceSchedules(
                    [1],
                    [CreateResource(10)],
                    infiniteResources: false,
                    graph, TestContext.Current.CancellationToken).ToList()));

            ex.Message.ShouldContain(@"maximum supported time value");
        }

        [Fact]
        public void PriorityListResourceScheduler_GivenActivityExactlyAtTheTimeHorizon_ThenSchedulesIt()
        {
            var scheduler = new PriorityListResourceScheduler<int, int, int>();
            // Finishing exactly on the horizon is within the limit, so it must schedule.
            var activity = new Activity<int, int, int>(1, 5)
            {
                EarliestStartTime = GraphLimits.MaximumTimeValue - 5,
            };
            var graph = new FakeSchedulingGraph(id => activity, id => [], () => [activity]);

            var schedules = RunWithWatchdog(() => scheduler.CalculateResourceSchedules(
                [1],
                [CreateResource(10)],
                infiniteResources: false,
                graph, TestContext.Current.CancellationToken).ToList());

            IScheduledActivity<int> scheduled = schedules.Single().ScheduledActivities.Single();
            scheduled.StartTime.ShouldBe(GraphLimits.MaximumTimeValue - 5);
            scheduled.FinishTime.ShouldBe(GraphLimits.MaximumTimeValue);
        }

        [Fact]
        public void PriorityListResourceScheduler_GivenCancelledToken_ThenThrowsOperationCanceledException()
        {
            var scheduler = new PriorityListResourceScheduler<int, int, int>();
            var activity = new Activity<int, int, int>(1, 5) { EarliestStartTime = 0 };
            var graph = new FakeSchedulingGraph(id => activity, id => [], () => [activity]);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Should.Throw<OperationCanceledException>(() =>
                scheduler.CalculateResourceSchedules(
                    [1],
                    [CreateResource(10)],
                    infiniteResources: false,
                    graph,
                    cts.Token).ToList());
        }

        [Fact]
        public void VertexGraphCompiler_GivenCancelledToken_ThenThrowsOperationCanceledException()
        {
            var compiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            compiler.AddActivity(new DependentActivity<int, int, int>(1, 5));
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Should.Throw<OperationCanceledException>(() =>
                compiler.Compile([CreateResource(10)], [], cts.Token));
        }

        [Fact]
        public void ArrowGraphBuilder_GivenCorruptedTargetResourceSet_ThenStallsWithInconsistencyDiagnosisInsteadOfHanging()
        {
            var graphBuilder = new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(
                new NextIdGenerator<int>(1000), new NextIdGenerator<int>(0));
            var activity = new Activity<int, int, int>(1, 5)
            {
                TargetResourceOperator = LogicalOperator.AND,
            };
            activity.TargetResources.Add(10);
            activity.TargetResources.Add(20);
            CorruptBuckets(activity.TargetResources);
            graphBuilder.AddActivity(activity).ShouldBeTrue();

            ResourceSchedulingStallException ex = Should.Throw<ResourceSchedulingStallException>(() =>
                RunWithWatchdog(() => graphBuilder.CalculateResourceSchedulesByPriorityList(
                    [CreateResource(10), CreateResource(20)], TestContext.Current.CancellationToken)));

            ex.Message.ShouldContain(@"internally inconsistent");
        }

        [Fact]
        public void ArrowGraphBuilder_GivenCancelledToken_ThenThrowsOperationCanceledException()
        {
            var graphBuilder = new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(
                new NextIdGenerator<int>(1000), new NextIdGenerator<int>(0));
            graphBuilder.AddActivity(new Activity<int, int, int>(1, 5)).ShouldBeTrue();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Should.Throw<OperationCanceledException>(() =>
                graphBuilder.CalculateResourceSchedulesByPriorityList([CreateResource(10)], cts.Token));
        }

        [Fact]
        public void PriorityListResourceScheduler_GivenTokenCancelledMidScheduling_ThenThrowsOperationCanceledException()
        {
            var scheduler = new PriorityListResourceScheduler<int, int, int>();
            var first = new Activity<int, int, int>(1, 5) { EarliestStartTime = 0 };
            var second = new Activity<int, int, int>(2, 3) { EarliestStartTime = 0 };
            Dictionary<int, IActivity<int, int, int>> lookup = new()
            {
                [1] = first,
                [2] = second,
            };
            using var cts = new CancellationTokenSource();
            // Cancel from inside the scheduling loop, deterministically: the graph is
            // first asked for activity 2 when it becomes ready (after activity 1
            // completes), so cancellation lands mid-run rather than up front.
            var graph = new FakeSchedulingGraph(
                id =>
                {
                    if (id == 2)
                    {
                        cts.Cancel();
                    }
                    return lookup[id];
                },
                id => id == 2 ? [1] : [],
                () => [first, second]);

            OperationCanceledException ex = Should.Throw<OperationCanceledException>(() =>
                RunWithWatchdog(() => scheduler.CalculateResourceSchedules(
                    [1, 2],
                    [CreateResource(10)],
                    infiniteResources: false,
                    graph,
                    cts.Token).ToList()));

            ex.CancellationToken.ShouldBe(cts.Token);
        }

        [Fact]
        public void VertexGraphBuilder_GivenCancelledToken_ThenThrowsOperationCanceledException()
        {
            var builder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(new NextIdGenerator<int>(0));
            builder.AddActivity(new Activity<int, int, int>(1, 5)).ShouldBeTrue();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Should.Throw<OperationCanceledException>(() =>
                builder.CalculateResourceSchedulesByPriorityList([CreateResource(10)], cts.Token));
        }

        [Fact]
        public void VertexGraphCompiler_GivenCancelledToken_WithConvenienceCompileForms_ThenThrowsOperationCanceledException()
        {
            var compiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            compiler.AddActivity(new DependentActivity<int, int, int>(1, 5));
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Should.Throw<OperationCanceledException>(() => compiler.Compile(cts.Token));
            Should.Throw<OperationCanceledException>(() => compiler.Compile([CreateResource(10)], cts.Token));
        }

        [Fact]
        public void VertexGraphCompiler_GivenLiveToken_WithConvenienceCompileForms_ThenCompilesNormally()
        {
            var compiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            compiler.AddActivity(new DependentActivity<int, int, int>(1, 5));
            compiler.AddActivity(new DependentActivity<int, int, int>(2, 5, [1]));
            using var cts = new CancellationTokenSource();

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> infinite =
                compiler.Compile(cts.Token);
            infinite.CompilationErrors.ShouldBeEmpty();
            infinite.ResourceSchedules.ShouldNotBeEmpty();

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> resourced =
                compiler.Compile([CreateResource(10)], cts.Token);
            resourced.CompilationErrors.ShouldBeEmpty();
            resourced.ResourceSchedules.ShouldNotBeEmpty();
        }

        [Fact]
        public void VertexGraphCompiler_GivenHealthyGraphAndLiveToken_ThenCompilesNormally()
        {
            var compiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            compiler.AddActivity(new DependentActivity<int, int, int>(1, 5));
            compiler.AddActivity(new DependentActivity<int, int, int>(2, 5, [1]));
            using var cts = new CancellationTokenSource();

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation =
                compiler.Compile([CreateResource(10)], [], cts.Token);

            compilation.CompilationErrors.ShouldBeEmpty();
            compilation.ResourceSchedules.ShouldNotBeEmpty();
        }
    }
}
