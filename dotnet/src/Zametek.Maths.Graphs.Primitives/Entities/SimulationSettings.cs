using System;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// What a simulation does: how many iterations, from which seed, and from which distribution
    /// it draws the durations.
    /// </summary>
    /// <remarks>
    /// The same settings, on the same plan, give the same result: the same finish time and the
    /// same critical activities in each iteration. That holds for any value of
    /// <see cref="MaxDegreeOfParallelism"/>, and the Rust port of the library gives it too.
    /// </remarks>
    public sealed class SimulationSettings
    {
        /// <summary>
        /// The default number of iterations.
        /// </summary>
        public const int DefaultIterations = 5_000;

        /// <summary>
        /// The default number of critical paths that one iteration records.
        /// </summary>
        public const int DefaultMaximumCriticalPathsPerIteration = 32;

        private int m_Iterations = DefaultIterations;
        private double m_ModifiedBetaShape = 4.0;
        private int m_MaxDegreeOfParallelism = -1;
        private int m_MaximumCriticalPathsPerIteration = DefaultMaximumCriticalPathsPerIteration;

        /// <summary>
        /// The number of iterations: from 1 to <see cref="GraphLimits.MaximumSimulationIterations"/>.
        /// Each iteration schedules the whole plan again.
        /// </summary>
        public int Iterations
        {
            get => m_Iterations;
            set
            {
                if (value < 1 || value > GraphLimits.MaximumSimulationIterations)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        value,
                        $@"The number of iterations must be from 1 to {GraphLimits.MaximumSimulationIterations}.");
                }
                m_Iterations = value;
            }
        }

        /// <summary>
        /// The seed of the random numbers. The same seed gives the same result.
        /// </summary>
        public ulong Seed { get; set; }

        /// <summary>
        /// The distribution of the duration of an activity that has a spread. The default is the
        /// beta of the reference PERT tools, <see cref="DurationDistribution.BetaPert"/>.
        /// </summary>
        public DurationDistribution Distribution { get; set; } = DurationDistribution.BetaPert;

        /// <summary>
        /// The shape value of <see cref="DurationDistribution.ModifiedBetaPert"/>: a larger value
        /// draws closer to the most likely duration. It is 4 by default, the usual value, and it
        /// must be a number above 0.
        /// </summary>
        public double ModifiedBetaShape
        {
            get => m_ModifiedBetaShape;
            set
            {
                if (!(value > 0.0) || double.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, @"The shape value must be a number above 0.");
                }
                m_ModifiedBetaShape = value;
            }
        }

        /// <summary>
        /// The most threads that the simulation uses, or -1 for as many as the machine has
        /// processors (the default). The result does not depend on the value.
        /// </summary>
        public int MaxDegreeOfParallelism
        {
            get => m_MaxDegreeOfParallelism;
            set
            {
                if (value < -1 || value == 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, @"The number of threads must be -1, or 1 or more.");
                }
                m_MaxDegreeOfParallelism = value;
            }
        }

        /// <summary>
        /// Whether the result holds, for each iteration, which activities were critical. This takes
        /// one bit for each activity and iteration. It is true by default.
        /// </summary>
        public bool RecordCriticalActivities { get; set; } = true;

        /// <summary>
        /// Whether the result holds, for each iteration, its critical paths. It is true by default.
        /// </summary>
        public bool RecordCriticalPaths { get; set; } = true;

        /// <summary>
        /// The most critical paths that one iteration records. Equal finish times of whole days make
        /// ties common, and a plan can have a very large number of critical paths that are all
        /// tied. An iteration with more paths than this records the first ones, in a fixed order,
        /// and the result says that its list is not complete. The default is
        /// <see cref="DefaultMaximumCriticalPathsPerIteration"/>.
        /// </summary>
        public int MaximumCriticalPathsPerIteration
        {
            get => m_MaximumCriticalPathsPerIteration;
            set
            {
                if (value < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, @"The number of paths must be 1 or more.");
                }
                m_MaximumCriticalPathsPerIteration = value;
            }
        }

        /// <summary>
        /// With no resources, the compile schedules the plan onto synthetic resources, and
        /// two activities that meet at the same moment can then be linked by one of them, which can
        /// make more activities critical. When this is false (the default), a simulation of a plan
        /// with no resources stops after the critical-path calculation. The finish times are the
        /// same, and the critical activities are those of the plan alone, without the synthetic
        /// links. When this is true, the simulation does what the compile does. It has no effect
        /// on a plan with resources.
        /// </summary>
        public bool ScheduleOntoSyntheticResources { get; set; }
    }
}
