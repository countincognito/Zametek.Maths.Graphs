using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// The raw result of a simulation: for each iteration, the finish time of the schedule, the
    /// activities that were critical, and the critical paths. It holds no statistics. The caller
    /// makes the mean, the percentiles, the histogram and the criticality index from it.
    /// </summary>
    /// <remarks>
    /// An iteration is numbered from 0, and an activity is numbered by its place in
    /// <see cref="ActivityIds"/>, which is the order in which the plan gave its activities.
    /// A simulation produces this result, and the constructor is public only so that the type can
    /// be built in tests and rebuilt from a stored result.
    /// </remarks>
    /// <typeparam name="T">The activity ID type.</typeparam>
    public sealed class SimulationResult<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        /// <summary>
        /// The finish time that an iteration has when it failed: when the resource scheduler could
        /// not place every activity, or when the schedule ran past
        /// <see cref="GraphLimits.MaximumTimeValue"/>. A failed iteration has no critical
        /// activities and no critical paths.
        /// </summary>
        public const int FailedFinishTime = -1;

        private readonly ulong[]? m_CriticalBits;
        private readonly int m_BitsPerIteration;
        private readonly int[]? m_PathOffsets;
        private readonly int[]? m_PathIndexes;
        private readonly bool[]? m_PathsComplete;

        /// <summary>
        /// Creates a result.
        /// </summary>
        /// <param name="activityIds">The activities, in the order of their columns.</param>
        /// <param name="finishTimes">The finish time of each iteration, or <see cref="FailedFinishTime"/>.</param>
        /// <param name="criticalActivityBits">
        /// One bit for each activity and iteration, or null when they were not recorded. The bits of
        /// an iteration start at a word boundary and take (activities + 63) / 64 words: bit n of word
        /// w is the activity w * 64 + n.
        /// </param>
        /// <param name="criticalPaths">The distinct critical paths, in the order in which they first appear.</param>
        /// <param name="pathOffsets">For each iteration and one more, where its paths start in <paramref name="pathIndexes"/>, or null when they were not recorded.</param>
        /// <param name="pathIndexes">The paths of each iteration, as indexes into <paramref name="criticalPaths"/>.</param>
        /// <param name="pathsComplete">For each iteration, whether the list of its paths is complete.</param>
        public SimulationResult(
            IReadOnlyList<T> activityIds,
            int[] finishTimes,
            ulong[]? criticalActivityBits,
            IReadOnlyList<CriticalPath<T>> criticalPaths,
            int[]? pathOffsets,
            int[]? pathIndexes,
            bool[]? pathsComplete)
        {
            ActivityIds = activityIds ?? throw new ArgumentNullException(nameof(activityIds));
            FinishTimes = finishTimes ?? throw new ArgumentNullException(nameof(finishTimes));
            CriticalPaths = criticalPaths ?? throw new ArgumentNullException(nameof(criticalPaths));

            m_BitsPerIteration = (activityIds.Count + 63) / 64;
            m_CriticalBits = criticalActivityBits;
            if (m_CriticalBits != null && m_CriticalBits.Length != finishTimes.Length * m_BitsPerIteration)
            {
                throw new ArgumentException(@"The bits of the critical activities do not match the iterations.", nameof(criticalActivityBits));
            }

            m_PathOffsets = pathOffsets;
            m_PathIndexes = pathIndexes;
            m_PathsComplete = pathsComplete;
            if (m_PathOffsets != null)
            {
                if (m_PathIndexes is null || m_PathsComplete is null)
                {
                    throw new ArgumentException(@"The paths of the iterations are not all given.", nameof(pathIndexes));
                }
                if (m_PathOffsets.Length != finishTimes.Length + 1 || m_PathsComplete.Length != finishTimes.Length)
                {
                    throw new ArgumentException(@"The paths do not match the iterations.", nameof(pathOffsets));
                }
            }

            int failed = 0;
            foreach (int finishTime in finishTimes)
            {
                if (finishTime == FailedFinishTime)
                {
                    failed++;
                }
            }
            FailedIterationCount = failed;
        }

        /// <summary>
        /// The activities, in the order in which the plan gave them.
        /// </summary>
        public IReadOnlyList<T> ActivityIds { get; }

        /// <summary>
        /// The number of iterations, including the ones that failed.
        /// </summary>
        public int IterationCount => FinishTimes.Count;

        /// <summary>
        /// The finish time of each iteration, or <see cref="FailedFinishTime"/> for one that failed.
        /// </summary>
        public IReadOnlyList<int> FinishTimes { get; }

        /// <summary>
        /// The number of iterations that failed.
        /// </summary>
        public int FailedIterationCount { get; }

        /// <summary>
        /// Whether the result says which activities were critical in each iteration.
        /// </summary>
        public bool HasCriticalActivities => m_CriticalBits != null;

        /// <summary>
        /// Whether an activity was critical in an iteration.
        /// </summary>
        /// <param name="iteration">The iteration, from 0.</param>
        /// <param name="activityIndex">The place of the activity in <see cref="ActivityIds"/>.</param>
        public bool IsCritical(int iteration, int activityIndex)
        {
            if (m_CriticalBits is null)
            {
                throw new InvalidOperationException(@"The critical activities were not recorded.");
            }
            if (iteration < 0 || iteration >= IterationCount)
            {
                throw new ArgumentOutOfRangeException(nameof(iteration));
            }
            if (activityIndex < 0 || activityIndex >= ActivityIds.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(activityIndex));
            }
            ulong word = m_CriticalBits[(iteration * m_BitsPerIteration) + (activityIndex >> 6)];
            return ((word >> (activityIndex & 63)) & 1UL) != 0UL;
        }

        /// <summary>
        /// Whether the result holds the critical paths of each iteration.
        /// </summary>
        public bool HasCriticalPaths => m_PathOffsets != null;

        /// <summary>
        /// The distinct critical paths of all the iterations, in the order in which they first appear.
        /// </summary>
        public IReadOnlyList<CriticalPath<T>> CriticalPaths { get; }

        /// <summary>
        /// The critical paths of an iteration, as indexes into <see cref="CriticalPaths"/>. An
        /// iteration has several when paths are tied: the schedule has more than one chain of
        /// activities that sets its finish time. A failed iteration, and an iteration with no
        /// activity that takes time, has none.
        /// </summary>
        /// <param name="iteration">The iteration, from 0.</param>
        public ReadOnlySpan<int> CriticalPathIndexes(int iteration)
        {
            if (m_PathOffsets is null || m_PathIndexes is null)
            {
                throw new InvalidOperationException(@"The critical paths were not recorded.");
            }
            if (iteration < 0 || iteration >= IterationCount)
            {
                throw new ArgumentOutOfRangeException(nameof(iteration));
            }
            return new ReadOnlySpan<int>(m_PathIndexes, m_PathOffsets[iteration], m_PathOffsets[iteration + 1] - m_PathOffsets[iteration]);
        }

        /// <summary>
        /// Whether the paths that <see cref="CriticalPathIndexes(int)"/> gives for an iteration are all of its
        /// critical paths. It is false when the iteration had more than
        /// <see cref="SimulationSettings.MaximumCriticalPathsPerIteration"/> of them.
        /// </summary>
        /// <param name="iteration">The iteration, from 0.</param>
        public bool IsCriticalPathListComplete(int iteration)
        {
            if (m_PathsComplete is null)
            {
                throw new InvalidOperationException(@"The critical paths were not recorded.");
            }
            if (iteration < 0 || iteration >= IterationCount)
            {
                throw new ArgumentOutOfRangeException(nameof(iteration));
            }
            return m_PathsComplete[iteration];
        }
    }
}
