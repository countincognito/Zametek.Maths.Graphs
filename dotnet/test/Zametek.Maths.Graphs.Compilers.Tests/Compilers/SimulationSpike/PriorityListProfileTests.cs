using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    // Where the priority-list calculation spends its own time, for the plans of the
    // simulation spike. It decides how much an exact optimisation of the list can win.
    //
    // The calculation repeats one step per activity: scan every activity for the least
    // slack, scan again for the earliest start among those, make the duration of the one
    // chosen zero, and ask the critical-path engine to bring the times up to date. The
    // engine does that incrementally, and gives up (returns false) when the change moves
    // the finish time of the project, because it then has to calculate again in full. So
    // the time of the list divides into the engine (incremental updates and full passes), a
    // copy of the graph that the public method takes, and the rest, which is the scans.
    public class PriorityListProfileTests
    {
        private readonly ITestOutputHelper m_Output;

        public PriorityListProfileTests(ITestOutputHelper output)
        {
            m_Output = output;
        }

        private sealed class TimingEngine
            : IVertexCriticalPathEngine<int, int, int, IDependentActivity<int, int, int>>
        {
            private readonly IVertexCriticalPathEngine<int, int, int, IDependentActivity<int, int, int>> m_Inner =
                new VertexCriticalPathEngine<int, int, int, IDependentActivity<int, int, int>>();

            internal long FullPassTicks;
            internal long IncrementalTicks;
            internal int FullPassCount;
            internal int IncrementalCount;
            internal int GaveUpCount;

            internal void Reset()
            {
                FullPassTicks = 0;
                IncrementalTicks = 0;
                FullPassCount = 0;
                IncrementalCount = 0;
                GaveUpCount = 0;
            }

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
                return session is null ? null : new Session(this, session);
            }

            private sealed class Session
                : IVertexIncrementalCriticalPath<int>
            {
                private readonly TimingEngine m_Owner;
                private readonly IVertexIncrementalCriticalPath<int> m_Inner;

                internal Session(TimingEngine owner, IVertexIncrementalCriticalPath<int> inner)
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
                    if (!result)
                    {
                        m_Owner.GaveUpCount++;
                    }
                    return result;
                }
            }
        }

        private static double Ms(long ticks, int iterations) =>
            ticks * 1000.0 / Stopwatch.Frequency / iterations;

        [Fact(Explicit = true)]
        public void MeasureWhereThePriorityListSpendsItsTime()
        {
            m_Output.WriteLine(@"The priority-list calculation alone (CalculateCriticalPathPriorityList), per iteration, on one thread, after a warm-up.");
            m_Output.WriteLine(@"'engine' = incremental updates plus full passes; 'gave up' = how often an incremental update returned false and a full calculation followed.");
            m_Output.WriteLine(@"");
            m_Output.WriteLine(
                $@"{"plan",-12} {"acts",5} {"total",9} {"graph copy",11} {"engine",9} {"of which full",14} {"scans+rest",11} {"updates",8} {"gave up",8} {"full passes",12}");

            foreach (SpikePlan plan in new[]
            {
                SpikePlan.LoadSample(),
                SpikePlan.Layered(100, layers: 4, resourceCount: 10),
                SpikePlan.Layered(250, layers: 10, resourceCount: 20),
                SpikePlan.Layered(500, layers: 20, resourceCount: 20),
            })
            {
                var engine = new TimingEngine();
                var builder = new VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(
                    new VertexGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>>
                    {
                        EdgeIdGeneratorFactory = new NextIdGeneratorFactory<int>(1_000_000),
                        CriticalPathEngine = engine,
                    });
                var compiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>(builder);
                foreach (SpikePlan.ActivityRecord record in plan.Activities)
                {
                    compiler.AddActivity(plan.NewActivity(record, record.Duration));
                }
                List<IDependentActivity<int, int, int>> activities = builder.Activities.ToList();
                IDependentActivity<int, int, int>[] byIndex = plan.Activities.Select(x => builder.Activity(x.Id)).ToArray();

                var random = new SpikeRandom(5);
                var vectors = new List<int[]>();
                for (int i = 0; i < 24; i++)
                {
                    var durations = new int[plan.Activities.Count];
                    SpikeSimulation.DrawDurations(plan, SpikeDistribution.BetaPertMomentMatched, random, durations);
                    vectors.Add(durations);
                }

                void Prepare(int[] durations)
                {
                    for (int i = 0; i < byIndex.Length; i++)
                    {
                        byIndex[i].Duration = durations[i];
                    }
                    builder.ResetResourceState(activities);
                    builder.CalculateCriticalPath();
                }

                // Warm up for about 1.5 seconds.
                long warmStart = Stopwatch.GetTimestamp();
                for (int i = 0; i < 10 || Stopwatch.GetTimestamp() - warmStart < 1.5 * Stopwatch.Frequency; i++)
                {
                    Prepare(vectors[i % vectors.Count]);
                    builder.CalculateCriticalPathPriorityList(CancellationToken.None);
                }

                // Measure. The graph copy is timed on its own, in the same state, because the
                // public method makes one before it starts.
                int iterations = plan.Activities.Count > 300 ? 30 : plan.Activities.Count > 90 ? 100 : 400;
                engine.Reset();
                long totalTicks = 0;
                long copyTicks = 0;
                for (int i = 0; i < iterations; i++)
                {
                    Prepare(vectors[i % vectors.Count]);

                    long c0 = Stopwatch.GetTimestamp();
                    builder.CloneObject();
                    copyTicks += Stopwatch.GetTimestamp() - c0;

                    long t0 = Stopwatch.GetTimestamp();
                    builder.CalculateCriticalPathPriorityList(CancellationToken.None);
                    totalTicks += Stopwatch.GetTimestamp() - t0;
                }

                // The engine's counters include the clone that the public method makes, which
                // calls nothing in the engine, so they are all the list's own.
                long engineTicks = engine.IncrementalTicks + engine.FullPassTicks;
                double total = Ms(totalTicks, iterations);
                double copy = Ms(copyTicks, iterations);
                double engineMs = Ms(engineTicks, iterations);
                double full = Ms(engine.FullPassTicks, iterations);
                double rest = Math.Max(0.0, total - copy - engineMs);

                m_Output.WriteLine(
                    $@"{plan.Name,-12} {plan.Activities.Count,5} {total.ToString("F2") + " ms",9} {copy.ToString("F2") + " ms",11} {engineMs.ToString("F2") + " ms",9} {full.ToString("F2") + " ms",14} {rest.ToString("F2") + " ms",11} {engine.IncrementalCount / (double)iterations,8:F0} {engine.GaveUpCount / (double)iterations,8:F1} {engine.FullPassCount / (double)iterations,12:F1}");
            }
        }
    }
}
