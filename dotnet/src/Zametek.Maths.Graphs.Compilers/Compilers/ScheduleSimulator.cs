using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// A Monte Carlo simulation of a schedule. Each iteration draws a duration for every activity
    /// that has a spread, from the three estimates of the activity, and then schedules the plan
    /// again, onto its resources, as a compile does. The result holds, for each iteration, the
    /// finish time, the critical activities and the critical paths.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The simulation is repeatable. The same plan and the same <see cref="SimulationSettings"/> give
    /// the same result, for any number of threads, and the Rust port of the library gives the same
    /// result too. The iterations are split into chunks of 64. Each chunk has a generator of random
    /// numbers of its own, and its seed comes from the seed of the settings and the number of the
    /// chunk. A chunk does not depend on the thread that runs it, nor on the order in which chunks
    /// finish.
    /// </para>
    /// <para>
    /// The durations are drawn in the order in which the plan gave its activities, and an activity
    /// with no spread takes no random number. A draw is rounded to whole time units, with halves up,
    /// and it stays within the range from O to P. A draw of 0 stays 0: the activity is then as long
    /// as a milestone, and it takes no resource in that iteration.
    /// </para>
    /// <para>
    /// An iteration costs about as much as a compile, and the cost grows with the square of the
    /// number of activities. A simulation reports its progress, and it stops between iterations when
    /// its token is cancelled.
    /// </para>
    /// <para>
    /// The simulation schedules a plan with the builders that a factory of its caller gives it (each thread
    /// needs a builder of its own). The engines of a builder decide how the plan is scheduled: the
    /// critical-path engine, the resource scheduler and the others. Thus, a caller that compiles with engines
    /// of its own gets a simulation that schedules in the same way. The first check of the plan is a compile,
    /// by a compiler that the same factory gives. <see cref="ScheduleSimulator{T, TResourceId, TWorkStreamId}"/>
    /// is this class with the standard factory. The Rust port takes a factory too
    /// (<c>ScheduleSimulator::with_factory</c>).
    /// </para>
    /// <para>
    /// An instance is safe for use by many threads: it never changes after it is made, and each run
    /// has its own copies of the plan.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The activity ID type.</typeparam>
    /// <typeparam name="TResourceId">The resource ID type.</typeparam>
    /// <typeparam name="TWorkStreamId">The work-stream ID type.</typeparam>
    /// <typeparam name="TActivity">The activity type of the builder. <see cref="ICloneObject.CloneObject"/> of an activity must give an object of this type.</typeparam>
    public class ScheduleSimulator<T, TResourceId, TWorkStreamId, TActivity>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
        where TActivity : class, IDependentActivity<T, TResourceId, TWorkStreamId>
    {
        // The number of iterations that share a generator of random numbers. It is part of the
        // result: a different size gives different draws for the same seed.
        internal const int ChunkSize = 64;

        private readonly T[] m_ActivityIds;
        private readonly Dictionary<T, int> m_IndexById;
        private readonly List<TActivity> m_Activities;
        private readonly List<IResource<TResourceId, TWorkStreamId>> m_Resources;
        private readonly IVertexGraphFactory<T, TResourceId, TWorkStreamId, TActivity> m_Factory;
        private readonly DurationEstimate[]? m_Estimates;

        /// <summary>
        /// Prepares a simulation of a plan. The activities are the ones that a compile takes: their
        /// dependencies and constraints, and the duration that the plan schedules (the expected
        /// duration). The simulation copies everything, and it does not change what it is given.
        /// </summary>
        /// <param name="activities">The activities of the plan. The order is part of the contract: it is the order in which a simulation draws the durations, and the order of the columns of the result.</param>
        /// <param name="resources">The resources of the plan. An empty list is the same as in a compile: infinite resources.</param>
        /// <param name="workStreams">The work streams of the plan.</param>
        /// <param name="estimates">
        /// The estimates of the activities that have them. An activity that has none keeps its
        /// duration in every iteration. The key must be the ID of an activity of the plan.
        /// </param>
        /// <param name="factory">
        /// Makes the builders that schedule the plan, and the compiler for the first check of it. The
        /// simulation calls <see cref="IVertexGraphFactory{T, TResourceId, TWorkStreamId, TActivity}.CreateCompiler"/>
        /// once, when it makes the simulator, and never for an iteration. The compile of the check gives
        /// <see cref="CompilationErrors"/> and <see cref="PlannedFinishTime"/>. It calls
        /// <see cref="IVertexGraphFactory{T, TResourceId, TWorkStreamId, TActivity}.CreateBuilder"/> once for
        /// each thread that runs iterations, and it calls it from any thread. A builder holds the engines that
        /// decide how a plan is scheduled, and so the factory decides it. If the factory gives the same engines
        /// to all its builders, as <see cref="VertexGraphFactory{T, TResourceId, TWorkStreamId, TActivity}"/>
        /// does, the engines must hold no state or be safe for use by many threads. The factory must not give
        /// null, and each builder that it gives must be new and empty.
        /// </param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="ArgumentException">An estimate belongs to an activity that is not in the plan, two activities have the same ID, or an activity clones itself to an object that is not a <typeparamref name="TActivity"/>.</exception>
        /// <exception cref="InvalidOperationException">The factory gave a null compiler (or, in a run, a null builder).</exception>
        public ScheduleSimulator(
            IEnumerable<TActivity> activities,
            IEnumerable<IResource<TResourceId, TWorkStreamId>> resources,
            IEnumerable<IWorkStream<TWorkStreamId>> workStreams,
            IReadOnlyDictionary<T, DurationEstimate> estimates,
            IVertexGraphFactory<T, TResourceId, TWorkStreamId, TActivity> factory)
        {
            if (activities is null)
            {
                throw new ArgumentNullException(nameof(activities));
            }
            if (resources is null)
            {
                throw new ArgumentNullException(nameof(resources));
            }
            if (workStreams is null)
            {
                throw new ArgumentNullException(nameof(workStreams));
            }
            if (estimates is null)
            {
                throw new ArgumentNullException(nameof(estimates));
            }
            if (factory is null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            m_Factory = factory;
            m_Activities = activities.Select(CloneActivity).ToList();
            m_Resources = resources.Select(x => (IResource<TResourceId, TWorkStreamId>)x.CloneObject()).ToList();
            m_ActivityIds = m_Activities.Select(x => x.Id).ToArray();

            m_IndexById = new Dictionary<T, int>(m_ActivityIds.Length);
            for (int i = 0; i < m_ActivityIds.Length; i++)
            {
                if (!m_IndexById.TryAdd(m_ActivityIds[i], i))
                {
                    throw new ArgumentException($@"The plan has two activities with the ID {m_ActivityIds[i]}.", nameof(activities));
                }
            }
            foreach (T id in estimates.Keys)
            {
                if (!m_IndexById.ContainsKey(id))
                {
                    throw new ArgumentException($@"There is an estimate for the activity {id}, which is not in the plan.", nameof(estimates));
                }
            }

            // The plan is compiled once, as it stands, to find out whether it can be scheduled at
            // all. The iterations check nothing: they would find the same errors again and again.
            // The compile also gives the finish time that the plan itself has.
            var compiler = NewCompiler();
            foreach (TActivity activity in m_Activities)
            {
                compiler.AddActivity(CloneActivity(activity));
            }
            IGraphCompilation<T, TResourceId, TWorkStreamId, TActivity> compilation = compiler.Compile(
                m_Resources.Select(x => (IResource<TResourceId, TWorkStreamId>)x.CloneObject()).ToList(),
                workStreams.ToList(),
                CancellationToken.None);
            CompilationErrors = compilation.CompilationErrors.ToList();
            if (CompilationErrors.Count == 0)
            {
                PlannedFinishTime = compiler.FinishTime;
                m_Estimates = m_Activities
                    .Select(x => estimates.TryGetValue(x.Id, out DurationEstimate estimate) ? estimate : DurationEstimate.Fixed(x.Duration))
                    .ToArray();
            }
        }

        // A copy of an activity, as the type of the simulator. The builder holds activities of this type, and
        // so an activity that clones itself to another type is refused here and not in the middle of a run.
        private static TActivity CloneActivity(TActivity activity) =>
            activity.CloneObject() as TActivity
            ?? throw new ArgumentException(
                $@"The activity {activity.Id} cloned itself to an object that is not a {typeof(TActivity)}. CloneObject of an activity must give an object of the type of the simulator.",
                @"activities");

        // A new builder from the factory of the caller.
        private VertexGraphBuilder<T, TResourceId, TWorkStreamId, TActivity> NewBuilder() =>
            m_Factory.CreateBuilder()
            ?? throw new InvalidOperationException(@"The factory gave a null builder. CreateBuilder must give a new, empty builder each time.");

        // The compiler of the first check, from the factory of the caller. Only the first check is a compile.
        private VertexGraphCompiler<T, TResourceId, TWorkStreamId, TActivity> NewCompiler() =>
            m_Factory.CreateCompiler()
            ?? throw new InvalidOperationException(@"The factory gave a null compiler. CreateCompiler must give a compiler.");

        /// <summary>
        /// The activities, in the order in which the plan gave them: the order of the columns of a result.
        /// </summary>
        public IReadOnlyList<T> ActivityIds => m_ActivityIds;

        /// <summary>
        /// The errors that a compile of the plan reports. A plan with errors cannot be simulated:
        /// <see cref="Run(SimulationSettings, IProgress{int}?, CancellationToken)"/> throws.
        /// </summary>
        public IReadOnlyList<IGraphCompilationError> CompilationErrors { get; }

        /// <summary>
        /// The finish time of the plan as it stands, with the durations that it schedules (the
        /// expected durations). It is 0 when the plan has compilation errors.
        /// </summary>
        public int PlannedFinishTime { get; }

        /// <summary>
        /// Runs a simulation.
        /// </summary>
        /// <param name="settings">What to do.</param>
        public SimulationResult<T> Run(SimulationSettings settings) =>
            Run(settings, null, CancellationToken.None);

        /// <summary>
        /// Runs a simulation, which stops between iterations when the token is cancelled.
        /// </summary>
        /// <param name="settings">What to do.</param>
        /// <param name="cancellationToken">The token that stops the simulation.</param>
        /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
        public SimulationResult<T> Run(SimulationSettings settings, CancellationToken cancellationToken) =>
            Run(settings, null, cancellationToken);

        /// <summary>
        /// Runs a simulation, which stops between iterations when the token is cancelled.
        /// </summary>
        /// <param name="settings">What to do.</param>
        /// <param name="progress">Told the number of iterations that are done, from time to time and from any thread. Its calls need not come in order.</param>
        /// <param name="cancellationToken">The token that stops the simulation.</param>
        /// <exception cref="InvalidOperationException">The plan has compilation errors.</exception>
        /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
        public SimulationResult<T> Run(SimulationSettings settings, IProgress<int>? progress, CancellationToken cancellationToken)
        {
            if (settings is null)
            {
                throw new ArgumentNullException(nameof(settings));
            }
            if (m_Estimates is null)
            {
                throw new InvalidOperationException(@"The plan has compilation errors, and cannot be simulated.");
            }
            cancellationToken.ThrowIfCancellationRequested();

            int iterations = settings.Iterations;
            int chunkCount = (iterations + ChunkSize - 1) / ChunkSize;
            var chunks = new ChunkResult[chunkCount];
            int done = 0;

            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = settings.MaxDegreeOfParallelism,
                CancellationToken = cancellationToken,
            };
            Parallel.For(
                0,
                chunkCount,
                options,
                () => new Worker(this, settings),
                (chunk, state, worker) =>
                {
                    int first = chunk * ChunkSize;
                    int count = Math.Min(ChunkSize, iterations - first);
                    chunks[chunk] = worker.Run(chunk, count, cancellationToken);
                    progress?.Report(Interlocked.Add(ref done, count));
                    return worker;
                },
                worker => { });

            return Merge(settings, chunks);
        }

        #region Merge

        // Puts the chunks together in the order of their numbers. The paths get their numbers in
        // the order in which they first appear in an iteration, which does not depend on the order
        // in which the chunks were made.
        private SimulationResult<T> Merge(SimulationSettings settings, ChunkResult[] chunks)
        {
            int iterations = settings.Iterations;
            int wordsPerIteration = (m_ActivityIds.Length + 63) / 64;

            var finishTimes = new int[iterations];
            ulong[]? bits = settings.RecordCriticalActivities ? new ulong[iterations * wordsPerIteration] : null;
            int[]? offsets = settings.RecordCriticalPaths ? new int[iterations + 1] : null;
            bool[]? complete = settings.RecordCriticalPaths ? new bool[iterations] : null;
            var pathIndexes = new List<int>();
            var distinctPaths = new Dictionary<EncodedPath, int>();
            var paths = new List<CriticalPath<T>>();

            int iteration = 0;
            foreach (ChunkResult chunk in chunks)
            {
                Array.Copy(chunk.FinishTimes, 0, finishTimes, iteration, chunk.Count);
                if (bits != null && chunk.CriticalBits != null)
                {
                    Array.Copy(chunk.CriticalBits, 0, bits, iteration * wordsPerIteration, chunk.CriticalBits.Length);
                }

                if (offsets != null && complete != null && chunk.PathOffsets != null && chunk.PathIndexes != null && chunk.Paths != null && chunk.PathsComplete != null)
                {
                    // The number of each of the paths of the chunk among all the paths.
                    var globalIndex = new int[chunk.Paths.Count];
                    for (int i = 0; i < chunk.Paths.Count; i++)
                    {
                        var key = new EncodedPath(chunk.Paths[i]);
                        if (!distinctPaths.TryGetValue(key, out int index))
                        {
                            index = paths.Count;
                            distinctPaths.Add(key, index);
                            paths.Add(Decode(chunk.Paths[i]));
                        }
                        globalIndex[i] = index;
                    }

                    for (int k = 0; k < chunk.Count; k++)
                    {
                        offsets[iteration + k] = pathIndexes.Count;
                        complete[iteration + k] = chunk.PathsComplete[k];
                        for (int p = chunk.PathOffsets[k]; p < chunk.PathOffsets[k + 1]; p++)
                        {
                            pathIndexes.Add(globalIndex[chunk.PathIndexes[p]]);
                        }
                    }
                }
                iteration += chunk.Count;
            }
            if (offsets != null)
            {
                offsets[iterations] = pathIndexes.Count;
            }

            return new SimulationResult<T>(
                m_ActivityIds,
                finishTimes,
                bits,
                paths,
                offsets,
                offsets is null ? null : pathIndexes.ToArray(),
                complete);
        }

        private CriticalPath<T> Decode(int[] encoded) =>
            new CriticalPath<T>(encoded.Select(x => new CriticalPathStep<T>(m_ActivityIds[x >> 2], (CriticalPathLink)(x & 3))));

        #endregion

        #region Chunks

        // What one chunk of iterations gives. The paths are held as encoded lists, numbered in the
        // order in which they first appear in the chunk.
        private sealed class ChunkResult
        {
            internal ChunkResult(int count)
            {
                Count = count;
                FinishTimes = new int[count];
            }

            internal int Count { get; }

            internal int[] FinishTimes { get; }

            internal ulong[]? CriticalBits { get; set; }

            internal List<int[]>? Paths { get; set; }

            internal int[]? PathOffsets { get; set; }

            internal int[]? PathIndexes { get; set; }

            internal bool[]? PathsComplete { get; set; }
        }

        // The state that one thread keeps for the chunks that it runs: its own copy of the plan.
        private sealed class Worker
        {
            private readonly ScheduleSimulator<T, TResourceId, TWorkStreamId, TActivity> m_Simulator;
            private readonly SimulationSettings m_Settings;
            private readonly SimulationIterationRunner<T, TResourceId, TWorkStreamId, TActivity> m_Runner;
            private readonly CriticalPathTracer<T, TResourceId, TWorkStreamId> m_Tracer;
            private readonly int[] m_Durations;

            internal Worker(ScheduleSimulator<T, TResourceId, TWorkStreamId, TActivity> simulator, SimulationSettings settings)
            {
                m_Simulator = simulator;
                m_Settings = settings;
                m_Runner = new SimulationIterationRunner<T, TResourceId, TWorkStreamId, TActivity>(
                    simulator.NewBuilder(),
                    simulator.m_Activities.Select(CloneActivity).ToList(),
                    simulator.m_Resources.Select(x => (IResource<TResourceId, TWorkStreamId>)x.CloneObject()).ToList(),
                    settings.ScheduleOntoSyntheticResources);
                m_Tracer = new CriticalPathTracer<T, TResourceId, TWorkStreamId>(m_Runner.Activities, simulator.m_IndexById);
                m_Durations = new int[simulator.m_ActivityIds.Length];
            }

            internal ChunkResult Run(int chunk, int count, CancellationToken cancellationToken)
            {
                var random = new SimulationRandom(SimulationRandom.ChunkSeed(m_Settings.Seed, chunk));
                int wordsPerIteration = (m_Durations.Length + 63) / 64;

                var result = new ChunkResult(count);
                if (m_Settings.RecordCriticalActivities)
                {
                    result.CriticalBits = new ulong[count * wordsPerIteration];
                }
                var pathLists = new List<int[]>();
                var localPaths = new List<int[]>();
                var localIndex = new Dictionary<EncodedPath, int>();
                var pathIndexes = new List<int>();
                if (m_Settings.RecordCriticalPaths)
                {
                    result.Paths = localPaths;
                    result.PathOffsets = new int[count + 1];
                    result.PathsComplete = new bool[count];
                }

                for (int k = 0; k < count; k++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    DrawDurations(random);
                    int finish = m_Runner.Run(m_Durations, cancellationToken);
                    result.FinishTimes[k] = finish;

                    if (result.PathOffsets != null)
                    {
                        result.PathOffsets[k] = pathIndexes.Count;
                        result.PathsComplete![k] = true;
                    }
                    if (finish == SimulationResult<T>.FailedFinishTime)
                    {
                        continue;
                    }

                    if (result.CriticalBits != null)
                    {
                        m_Tracer.FillCriticalBits(result.CriticalBits, k * wordsPerIteration);
                    }
                    if (result.PathOffsets != null)
                    {
                        pathLists.Clear();
                        result.PathsComplete![k] = m_Tracer.Trace(finish, m_Durations, m_Settings.MaximumCriticalPathsPerIteration, pathLists);
                        foreach (int[] path in pathLists)
                        {
                            var key = new EncodedPath(path);
                            if (!localIndex.TryGetValue(key, out int index))
                            {
                                index = localPaths.Count;
                                localIndex.Add(key, index);
                                localPaths.Add(path);
                            }
                            pathIndexes.Add(index);
                        }
                    }
                }

                if (result.PathOffsets != null)
                {
                    result.PathOffsets[count] = pathIndexes.Count;
                    result.PathIndexes = pathIndexes.ToArray();
                }
                return result;
            }

            // The durations of one iteration, in the order of the plan.
            private void DrawDurations(SimulationRandom random)
            {
                DurationEstimate[] estimates = m_Simulator.m_Estimates!;
                for (int i = 0; i < estimates.Length; i++)
                {
                    DurationEstimate estimate = estimates[i];
                    m_Durations[i] = estimate.HasSpread
                        ? DurationSampler.RoundToWholeUnits(
                            DurationSampler.Sample(m_Settings.Distribution, random, estimate, m_Settings.ModifiedBetaShape),
                            estimate)
                        : estimate.MostLikely;
                }
            }
        }

        // A path as a key of a dictionary: two paths are the same when their steps are.
        private readonly struct EncodedPath
            : IEquatable<EncodedPath>
        {
            private readonly int[] m_Steps;
            private readonly int m_Hash;

            internal EncodedPath(int[] steps)
            {
                m_Steps = steps;
                var hash = new HashCode();
                foreach (int step in steps)
                {
                    hash.Add(step);
                }
                m_Hash = hash.ToHashCode();
            }

            public bool Equals(EncodedPath other) => m_Hash == other.m_Hash && m_Steps.AsSpan().SequenceEqual(other.m_Steps);

            public override bool Equals(object? obj) => obj is EncodedPath other && Equals(other);

            public override int GetHashCode() => m_Hash;
        }

        #endregion
    }
}
