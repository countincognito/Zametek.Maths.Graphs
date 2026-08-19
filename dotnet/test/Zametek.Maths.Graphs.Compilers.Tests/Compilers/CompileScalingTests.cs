using Shouldly;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    // Where a whole compile spends its time, now that the priority-list calculation no
    // longer dominates it.
    //
    // Every measurement taken during the performance investigation was of the
    // priority-list calculation alone, because that was the cost. It has since gone from
    // 6.9 s to a fraction of a second at the activity limit, roughly twenty-fold, which
    // means the shape of a compile has changed and nothing had re-measured it. In
    // particular the scheduler carries a quadratic nobody has had cause to look at:
    // ResourceScheduleBuilder.ActivityAt scans a resource's scheduled activities from
    // the front, and the tick loop calls it once per resource per tick.
    //
    // Everything is timed inside a single compile, through the public engine seams, so
    // the parts genuinely sum to the whole rather than being separate runs compared
    // against each other. Times accumulate as raw Stopwatch ticks: a call that takes
    // under a millisecond truncates to zero, and the incremental path makes thousands of
    // such calls per compile, so accumulating milliseconds would report nothing.
    public class CompileScalingTests
    {
        private readonly ITestOutputHelper m_Output;

        public CompileScalingTests(ITestOutputHelper output)
        {
            m_Output = output;
        }

        #region Timing decorators

        private sealed class TimingResourceSchedulingEngine
            : IResourceSchedulingEngine<int, int, int>
        {
            private readonly IResourceSchedulingEngine<int, int, int> m_Inner =
                new PriorityListResourceScheduler<int, int, int>();

            internal long ScheduleTicks { get; private set; }

            internal long RebuildTicks { get; private set; }

            internal long IndirectTicks { get; private set; }

            internal int TickLoopCallCount { get; private set; }

            public IEnumerable<IResourceSchedule<int, int, int>> CalculateResourceSchedules(
                List<int> priorityList,
                List<IResource<int, int>> filteredResources,
                bool infiniteResources,
                IResourceSchedulingGraph<int, int, int> graph,
                CancellationToken cancellationToken)
            {
                TickLoopCallCount++;
                long start = Stopwatch.GetTimestamp();
                // Materialised inside the timer: the engine returns a lazy sequence, and
                // timing an unenumerated enumerable would measure nothing.
                List<IResourceSchedule<int, int, int>> result = m_Inner.CalculateResourceSchedules(
                    priorityList, filteredResources, infiniteResources, graph, cancellationToken).ToList();
                ScheduleTicks += Stopwatch.GetTimestamp() - start;
                return result;
            }

            public IList<IUnavailableResources<int, int>> GatherUnavailableResources(
                List<IActivity<int, int, int>> activities,
                List<IResource<int, int>> filteredResources) =>
                m_Inner.GatherUnavailableResources(activities, filteredResources);

            public List<IResourceSchedule<int, int, int>> ReplaceWithSyntheticResources(
                List<IResourceSchedule<int, int, int>> resourceSchedules) =>
                m_Inner.ReplaceWithSyntheticResources(resourceSchedules);

            public IEnumerable<IResourceSchedule<int, int, int>> RebuildAlignedResourceSchedules(
                List<IResourceSchedule<int, int, int>> resourceSchedules,
                bool infiniteResources,
                IResourceSchedulingGraph<int, int, int> graph,
                List<IActivity<int, int, int>> finalActivities,
                int startTime,
                int finishTime)
            {
                long start = Stopwatch.GetTimestamp();
                List<IResourceSchedule<int, int, int>> result = m_Inner.RebuildAlignedResourceSchedules(
                    resourceSchedules, infiniteResources, graph, finalActivities, startTime, finishTime).ToList();
                RebuildTicks += Stopwatch.GetTimestamp() - start;
                return result;
            }

            public IEnumerable<IResourceSchedule<int, int, int>> CollectIndirectResourceSchedules(
                List<IResource<int, int>> filteredResources,
                List<IResourceSchedule<int, int, int>> scheduledResources,
                List<IActivity<int, int, int>> finalActivities,
                int startTime,
                int finishTime)
            {
                long start = Stopwatch.GetTimestamp();
                List<IResourceSchedule<int, int, int>> result = m_Inner.CollectIndirectResourceSchedules(
                    filteredResources, scheduledResources, finalActivities, startTime, finishTime).ToList();
                IndirectTicks += Stopwatch.GetTimestamp() - start;
                return result;
            }

            public HashSet<int> GetResourcePhasesUsed(
                List<IResourceSchedule<int, int, int>> totalSchedules,
                HashSet<int> workstreamsUsed) =>
                m_Inner.GetResourcePhasesUsed(totalSchedules, workstreamsUsed);
        }

        // Separates the two things a compile asks of the critical-path engine: the full
        // passes (three per compile - one opening the priority list, two in the compile
        // pipeline) and the incremental applications, one per activity placed. Together
        // they are the priority-list calculation plus the compile's own CPM work.
        private sealed class TimingVertexCriticalPathEngine
            : IVertexCriticalPathEngine<int, int, int, IDependentActivity<int, int, int>>
        {
            private readonly IVertexCriticalPathEngine<int, int, int, IDependentActivity<int, int, int>> m_Inner =
                new VertexCriticalPathEngine<int, int, int, IDependentActivity<int, int, int>>();

            internal long FullPassTicks { get; private set; }

            internal long IncrementalTicks { get; private set; }

            internal int FullPassCount { get; private set; }

            internal int IncrementalCount { get; private set; }

            public bool CalculateCriticalPathForwardFlow(
                IVertexGraphState<int, int, int, IDependentActivity<int, int, int>> state,
                List<IInvalidConstraint<int>> invalidConstraints,
                bool shuffle)
            {
                FullPassCount++;
                long start = Stopwatch.GetTimestamp();
                bool result = m_Inner.CalculateCriticalPathForwardFlow(state, invalidConstraints, shuffle);
                FullPassTicks += Stopwatch.GetTimestamp() - start;
                return result;
            }

            public bool CalculateCriticalPathBackwardFlow(
                IVertexGraphState<int, int, int, IDependentActivity<int, int, int>> state,
                List<IInvalidConstraint<int>> invalidConstraints,
                bool shuffle)
            {
                long start = Stopwatch.GetTimestamp();
                bool result = m_Inner.CalculateCriticalPathBackwardFlow(state, invalidConstraints, shuffle);
                FullPassTicks += Stopwatch.GetTimestamp() - start;
                return result;
            }

            public bool BackFillIsolatedNodes(
                IVertexGraphState<int, int, int, IDependentActivity<int, int, int>> state,
                List<IInvalidConstraint<int>> invalidConstraints) =>
                m_Inner.BackFillIsolatedNodes(state, invalidConstraints);

            public IVertexIncrementalCriticalPath<int> BeginIncrementalCriticalPath(
                IVertexGraphState<int, int, int, IDependentActivity<int, int, int>> state)
            {
                IVertexIncrementalCriticalPath<int> session = m_Inner.BeginIncrementalCriticalPath(state);
                return session is null ? null : new TimingSession(this, session);
            }

            private sealed class TimingSession
                : IVertexIncrementalCriticalPath<int>
            {
                private readonly TimingVertexCriticalPathEngine m_Owner;
                private readonly IVertexIncrementalCriticalPath<int> m_Inner;

                internal TimingSession(
                    TimingVertexCriticalPathEngine owner,
                    IVertexIncrementalCriticalPath<int> inner)
                {
                    m_Owner = owner;
                    m_Inner = inner;
                }

                public bool ApplyDurationChange(int activityId)
                {
                    m_Owner.IncrementalCount++;
                    long start = Stopwatch.GetTimestamp();
                    bool result = m_Inner.ApplyDurationChange(activityId);
                    m_Owner.IncrementalTicks += Stopwatch.GetTimestamp() - start;
                    return result;
                }
            }
        }

        private sealed class Split
        {
            internal long TotalMilliseconds { get; set; }

            internal long IncrementalMilliseconds { get; set; }

            internal long FullPassMilliseconds { get; set; }

            internal long ScheduleMilliseconds { get; set; }

            internal long RebuildMilliseconds { get; set; }

            internal long IndirectMilliseconds { get; set; }

            internal int FullPassCount { get; set; }

            internal int IncrementalCount { get; set; }

            internal long EverythingElseMilliseconds =>
                Math.Max(
                    0,
                    TotalMilliseconds - IncrementalMilliseconds - FullPassMilliseconds
                        - ScheduleMilliseconds - RebuildMilliseconds - IndirectMilliseconds);
        }

        #endregion

        [Fact(Explicit = true)]
        public void MeasureCompileSplit()
        {
            // Explicit, because it compiles graphs up to the activity limit. Build in
            // Release first, then:
            //   .\Zametek.Maths.Graphs.Compilers.Tests.exe -explicit only -method "*MeasureCompileSplit*" -showLiveOutput
            //
            // Rows are written as they complete so a long run visibly makes progress.
            m_Output.WriteLine(@"Full compile through VertexGraphCompiler, 20 resources, depth = size / 25.");
            m_Output.WriteLine(@"Best of five runs, chosen by total time so the parts belong to one compile.");
            m_Output.WriteLine(@"'incremental' is the priority-list loop; 'full CPM' is the three whole-graph passes; 'scheduling' is the tick loop.");
            m_Output.WriteLine(@"");
            m_Output.WriteLine(
                $@"{"activities",11} {"depth",6} {"compile",9} {"incremental",12} {"full CPM",9} {"scheduling",11} {"rebuild",8} {"indirect",9} {"else",7}");

            foreach (int size in new[] { 250, 500, 1_000, 1_500, 2_000 })
            {
                int layers = Math.Max(1, size / 25);
                Split split = BestOfFive(size, layers, resourceCount: 20);

                m_Output.WriteLine(
                    $@"{size,11} {layers,6} {split.TotalMilliseconds + "ms",9} {split.IncrementalMilliseconds + "ms",12} {split.FullPassMilliseconds + "ms",9} {split.ScheduleMilliseconds + "ms",11} {split.RebuildMilliseconds + "ms",8} {split.IndirectMilliseconds + "ms",9} {split.EverythingElseMilliseconds + "ms",7}");
            }

            m_Output.WriteLine(@"");
            Split atTheLimit = BestOfFive(2_000, layers: 80, resourceCount: 20);
            m_Output.WriteLine(
                $@"At the activity limit: {atTheLimit.FullPassCount} full passes, {atTheLimit.IncrementalCount} incremental applications.");
        }

        [Fact(Explicit = true)]
        public void MeasureSchedulerScalingWithResourceCount()
        {
            // ActivityAt scans one resource's own schedule, so its cost per tick falls as
            // the activities spread across more resources while the tick count stays
            // roughly the same. If the tick loop were dominated by that scan its total
            // would fall as resources are added; if it is dominated by anything else it
            // would not. That is what this second table is for.
            m_Output.WriteLine(@"Tick-loop time against resource count, at 2,000 activities and depth 80. Best of five, after a discarded warm-up.");
            m_Output.WriteLine($@"{"resources",11} {"scheduling",12} {"compile",10}");

            foreach (int resourceCount in new[] { 5, 10, 20, 50, 100, 200 })
            {
                Split split = BestOfFive(2_000, layers: 80, resourceCount);
                m_Output.WriteLine(
                    $@"{resourceCount,11} {split.ScheduleMilliseconds + "ms",12} {split.TotalMilliseconds + "ms",10}");
            }
        }

        [Fact]
        public void Compile_GivenLayeredGraph_ThenTheHarnessMeasuresACompileThatActuallyRan()
        {
            // Not a measurement - it is what stops the explicit harnesses above from
            // silently timing a compile that failed early and did no work.
            Split split = MeasureCompile(200, layers: 8, resourceCount: 20);

            split.IncrementalCount.ShouldBe(200);
            split.FullPassCount.ShouldBe(3);
        }

        // Best of five after a discarded warm-up. The minimum rather than the mean,
        // because the noise here is additive - garbage collection and scheduling
        // interference only ever make a run slower - so the fastest run is the closest
        // estimate of the work itself. Without the warm-up the first size measured comes
        // out several times slower than its neighbours, which is jitting rather than
        // anything about the graph.
        private static Split BestOfFive(int size, int layers, int resourceCount)
        {
            MeasureCompile(size, layers, resourceCount);

            Split best = null;
            for (int run = 0; run < 5; run++)
            {
                Split split = MeasureCompile(size, layers, resourceCount);
                if (best is null || split.TotalMilliseconds < best.TotalMilliseconds)
                {
                    best = split;
                }
            }
            return best;
        }

        private static Split MeasureCompile(int size, int layers, int resourceCount)
        {
            var scheduling = new TimingResourceSchedulingEngine();
            var criticalPath = new TimingVertexCriticalPathEngine();
            var graphBuilder = new VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(
                new VertexGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>>
                {
                    EdgeIdGenerator = new NextIdGenerator<int>(1_000_000),
                    ResourceSchedulingEngine = scheduling,
                    CriticalPathEngine = criticalPath,
                });
            AddLayeredGraph(graphBuilder, size, layers);

            var compiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>(graphBuilder);
            List<IResource<int, int>> resources = Enumerable.Range(1, resourceCount)
                .Select(id => (IResource<int, int>)new Resource<int, int>(
                    id, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 1.0, 0, Array.Empty<int>()))
                .ToList();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long start = Stopwatch.GetTimestamp();
            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation =
                compiler.Compile(resources, CancellationToken.None);
            long totalTicks = Stopwatch.GetTimestamp() - start;

            compilation.CompilationErrors.ShouldBeEmpty(
                $@"the compile at {size} activities reported errors, so this would be timing a compile that stopped early");

            return new Split
            {
                TotalMilliseconds = ToMilliseconds(totalTicks),
                IncrementalMilliseconds = ToMilliseconds(criticalPath.IncrementalTicks),
                FullPassMilliseconds = ToMilliseconds(criticalPath.FullPassTicks),
                ScheduleMilliseconds = ToMilliseconds(scheduling.ScheduleTicks),
                RebuildMilliseconds = ToMilliseconds(scheduling.RebuildTicks),
                IndirectMilliseconds = ToMilliseconds(scheduling.IndirectTicks),
                FullPassCount = criticalPath.FullPassCount,
                IncrementalCount = criticalPath.IncrementalCount,
            };
        }

        private static long ToMilliseconds(long ticks) => (ticks * 1000) / Stopwatch.Frequency;

        // The same shape the priority-list harness uses, so the two are comparable.
        private static void AddLayeredGraph(
            VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> graphBuilder,
            int size,
            int layers)
        {
            int perLayer = Math.Max(1, size / layers);
            for (int id = 1; id <= size; id++)
            {
                int layer = (id - 1) / perLayer;
                var dependencies = new HashSet<int>();
                if (layer > 0)
                {
                    int previousStart = ((layer - 1) * perLayer) + 1;
                    int previousEnd = Math.Min(layer * perLayer, size);
                    if (previousEnd >= previousStart)
                    {
                        dependencies.Add(previousStart + ((id * 7) % (previousEnd - previousStart + 1)));
                        dependencies.Add(previousStart + ((id * 13) % (previousEnd - previousStart + 1)));
                    }
                }
                graphBuilder.AddActivity(
                    new DependentActivity<int, int, int>(id, 1 + (id % 9)),
                    dependencies);
            }
        }
    }
}
