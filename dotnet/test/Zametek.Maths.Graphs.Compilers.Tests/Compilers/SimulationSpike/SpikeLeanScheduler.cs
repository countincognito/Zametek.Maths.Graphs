using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Zametek.Maths.Graphs.Tests
{
    // One scheduling pass of the simulation, on a graph that is built once and then given
    // new durations: the "lean path".
    //
    // A compile does a good deal that an iteration of the simulation does not need. It
    // checks the plan for errors before and after, copies every activity to give the caller
    // its own results, rebuilds the schedules to align them with the final times, and works
    // out the schedules of the indirect resources. None of that changes the finish time or
    // the critical activities. This class keeps the four steps that do:
    //
    //   1. the first critical-path calculation,
    //   2. the priority-list scheduling onto the resources,
    //   3. the resource dependencies that the schedule implies,
    //   4. the second critical-path calculation, which includes them.
    //
    // It reads the finish time and the critical activities from the builder after step 4.
    // The compile is the oracle: the tests compare every result of this class with the
    // result of a full compile of the same plan with the same durations.
    internal sealed class SpikeLeanScheduler
    {
        // Ticks of the stopwatch for each part of one iteration, added up over many
        // iterations. A part can take far less than a millisecond, and so this keeps the
        // raw ticks.
        internal sealed class Split
        {
            internal const int Reset = 0;
            internal const int FirstPass = 1;
            internal const int Schedule = 2;
            internal const int Assign = 3;
            internal const int SecondPass = 4;
            internal const int Rest = 5;
            internal const int Clone = 6;
            internal const int PriorityListWithClone = 7;

            internal readonly long[] Ticks = new long[8];
            internal int Iterations;

            internal double PerIterationMilliseconds(int stage) =>
                Iterations == 0 ? 0.0 : Ticks[stage] * 1000.0 / Stopwatch.Frequency / Iterations;
        }

        private readonly VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>> m_Compiler;
        private readonly VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> m_Builder;
        private readonly List<IDependentActivity<int, int, int>> m_ActivityList;
        private readonly IDependentActivity<int, int, int>[] m_ByIndex;
        private readonly List<IResource<int, int>> m_Resources;
        private readonly List<IWorkStream<int>> m_WorkStreams;
        private readonly bool m_Infinite;

        internal SpikeLeanScheduler(SpikePlan plan, bool useResources)
        {
            m_Compiler = plan.NewCompiler(plan.MostLikelyDurations());
            m_Builder = m_Compiler.Builder;
            m_Resources = plan.NewResources(useResources);
            m_WorkStreams = plan.NewWorkStreams();
            m_Infinite = m_Resources.Count == 0;
            m_ActivityList = m_Builder.Activities.ToList();
            m_ByIndex = plan.Activities.Select(x => m_Builder.Activity(x.Id)).ToArray();
        }

        // With no resources, the schedule is the plain critical-path schedule, and steps 2
        // to 4 add nothing to it. Whether that is so is a claim, and a test holds it to the
        // compile.
        internal bool CriticalPathOnlyWithoutResources { get; set; } = true;

        // The two faults that the test of the oracle seeds, to show that the oracle fails
        // when the lean path is wrong.
        internal bool FaultSkipReset { get; set; }

        internal bool FaultSkipSecondPass { get; set; }

        internal Split Timing { get; set; }

        // Also times the pieces inside step 2, which costs extra work in each iteration.
        internal bool ProfileInsideSchedule { get; set; }

        internal IDependentActivity<int, int, int> Activity(int index) => m_ByIndex[index];

        // Runs the iteration and returns the finish time. critical receives, for each
        // activity of the plan in its order, whether the activity is critical.
        internal int Run(int[] durations, bool[] critical, CancellationToken cancellationToken)
        {
            Split timing = Timing;
            long last = timing is null ? 0 : Stopwatch.GetTimestamp();

            for (int i = 0; i < m_ByIndex.Length; i++)
            {
                m_ByIndex[i].Duration = durations[i];
            }
            if (!FaultSkipReset)
            {
                m_Builder.ResetResourceState(m_ActivityList);
            }
            last = Mark(timing, last, Split.Reset);

            m_Builder.CalculateCriticalPath();
            last = Mark(timing, last, Split.FirstPass);

            if (!(m_Infinite && CriticalPathOnlyWithoutResources))
            {
                if (timing is not null && ProfileInsideSchedule)
                {
                    // Costs one clone and one extra priority-list calculation in each
                    // iteration, so it is a profile and not a measurement of the lean path.
                    // The extra work is kept out of every bucket.
                    long c0 = Stopwatch.GetTimestamp();
                    m_Builder.CloneObject();
                    long c1 = Stopwatch.GetTimestamp();
                    m_Builder.CalculateCriticalPathPriorityList(cancellationToken);
                    long c2 = Stopwatch.GetTimestamp();
                    timing.Ticks[Split.Clone] += c1 - c0;
                    timing.Ticks[Split.PriorityListWithClone] += c2 - c1;
                    last = Stopwatch.GetTimestamp();
                }

                List<IResourceSchedule<int, int, int>> schedules =
                    m_Builder.CalculateResourceSchedulesByPriorityList(m_Resources, cancellationToken);
                if (m_Infinite)
                {
                    schedules = m_Builder.ResourceSchedulingEngine.ReplaceWithSyntheticResources(schedules);
                }
                last = Mark(timing, last, Split.Schedule);

                m_Builder.AssignResourceDependencies(schedules);
                last = Mark(timing, last, Split.Assign);

                if (!FaultSkipSecondPass)
                {
                    m_Builder.CalculateCriticalPath();
                }
                last = Mark(timing, last, Split.SecondPass);
            }

            if (!m_Builder.BackFillIsolatedNodes())
            {
                throw new InvalidOperationException("The isolated activities could not be filled in.");
            }
            int finish = m_Builder.FinishTime;
            for (int i = 0; i < m_ByIndex.Length; i++)
            {
                critical[i] = m_ByIndex[i].IsCritical;
            }
            if (timing is not null)
            {
                Mark(timing, last, Split.Rest);
                timing.Iterations++;
            }
            return finish;
        }

        // What a plain Compile costs on this scheduler's own compiler when the caller only
        // changes the durations: the way the application compiles after an edit.
        internal int RunFullCompileOnTheSameCompiler(int[] durations, CancellationToken cancellationToken)
        {
            for (int i = 0; i < m_ByIndex.Length; i++)
            {
                m_ByIndex[i].Duration = durations[i];
            }
            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation =
                m_Compiler.Compile(m_Resources, m_WorkStreams, cancellationToken);
            if (compilation.CompilationErrors.Any())
            {
                throw new InvalidOperationException("The compile reported errors.");
            }
            return m_Compiler.FinishTime;
        }

        // The predecessors of each activity, as indexes into the plan, from what the builder
        // holds after the iteration. They include the dependencies that resource
        // allocation added, which is why a critical path can pass through a resource.
        internal int[][] PredecessorIndexes(Dictionary<int, int> indexById)
        {
            var result = new int[m_ByIndex.Length][];
            for (int i = 0; i < m_ByIndex.Length; i++)
            {
                IDependentActivity<int, int, int> activity = m_ByIndex[i];
                result[i] = activity.Dependencies
                    .Union(activity.PlanningDependencies)
                    .Union(activity.ResourceDependencies)
                    .Select(x => indexById[x])
                    .ToArray();
            }
            return result;
        }

        private static long Mark(Split timing, long previous, int stage)
        {
            if (timing is null)
            {
                return 0;
            }
            long now = Stopwatch.GetTimestamp();
            timing.Ticks[stage] += now - previous;
            return now;
        }
    }

    // The whole simulation: many iterations, in chunks, on as many threads as the caller
    // allows, with the same result for any number of threads.
    //
    // Each chunk of 64 iterations has a generator of its own. Its seed comes from the main
    // seed and the number of the chunk. An iteration does not depend on the thread that
    // runs it, nor on the order in which the chunks finish, and thus the arrays of results
    // are the same for one thread and for many.
    internal static class SpikeSimulation
    {
        internal const int ChunkSize = 64;

        internal sealed class Result
        {
            internal int[] Finishes;

            internal string[] Labels;

            internal int Failed;

            internal double Seconds;
        }

        private sealed class Worker
        {
            internal SpikeLeanScheduler Scheduler;
            internal int[] Durations;
            internal bool[] Critical;
        }

        internal static int RoundToWholeDays(double value) =>
            Math.Max(0, (int)Math.Round(value, MidpointRounding.AwayFromZero));

        // The durations of one iteration, in the order of the plan.
        internal static void DrawDurations(SpikePlan plan, SpikeDistribution distribution, SpikeRandom random, int[] durations)
        {
            for (int i = 0; i < durations.Length; i++)
            {
                SpikePlan.ActivityRecord activity = plan.Activities[i];
                if (activity.Optimistic == activity.Pessimistic
                    && activity.Optimistic == activity.Duration)
                {
                    durations[i] = activity.Duration;
                    continue;
                }
                durations[i] = RoundToWholeDays(
                    SpikeSamplers.Sample(distribution, random, activity.Optimistic, activity.Duration, activity.Pessimistic));
            }
        }

        internal static SpikeRandom ChunkRandom(ulong seed, int chunk) =>
            new SpikeRandom(seed ^ ((ulong)(chunk + 1) * 0x9E3779B97F4A7C15UL));

        internal static Result Run(
            SpikePlan plan,
            SpikeDistribution distribution,
            int iterations,
            ulong seed,
            int threads,
            bool useResources,
            bool withLabels,
            bool criticalPathOnlyWithoutResources = true)
        {
            var result = new Result
            {
                Finishes = new int[iterations],
                Labels = withLabels ? new string[iterations] : null,
            };
            int chunks = (iterations + ChunkSize - 1) / ChunkSize;
            Dictionary<int, int> indexById = plan.Activities
                .Select((x, i) => (x.Id, i))
                .ToDictionary(x => x.Id, x => x.i);
            string[] names = plan.Activities.Select(x => x.Name).ToArray();
            int failed = 0;

            var stopwatch = Stopwatch.StartNew();
            Parallel.For(
                0,
                chunks,
                new ParallelOptions { MaxDegreeOfParallelism = threads },
                () => new Worker
                {
                    Scheduler = new SpikeLeanScheduler(plan, useResources)
                    {
                        CriticalPathOnlyWithoutResources = criticalPathOnlyWithoutResources,
                    },
                    Durations = new int[plan.Activities.Count],
                    Critical = new bool[plan.Activities.Count],
                },
                (chunk, state, worker) =>
                {
                    SpikeRandom random = ChunkRandom(seed, chunk);
                    int first = chunk * ChunkSize;
                    int last = Math.Min(iterations, first + ChunkSize);
                    for (int i = first; i < last; i++)
                    {
                        DrawDurations(plan, distribution, random, worker.Durations);
                        try
                        {
                            result.Finishes[i] = worker.Scheduler.Run(worker.Durations, worker.Critical, CancellationToken.None);
                            if (withLabels)
                            {
                                result.Labels[i] = SpikeCriticalPaths.Label(
                                    worker.Scheduler.PredecessorIndexes(indexById),
                                    worker.Critical,
                                    names,
                                    worker.Durations);
                            }
                        }
                        catch (ResourceSchedulingStallException)
                        {
                            result.Finishes[i] = -1;
                            Interlocked.Increment(ref failed);
                        }
                    }
                    return worker;
                },
                worker => { });
            stopwatch.Stop();

            result.Failed = failed;
            result.Seconds = stopwatch.Elapsed.TotalSeconds;
            return result;
        }
    }
}
