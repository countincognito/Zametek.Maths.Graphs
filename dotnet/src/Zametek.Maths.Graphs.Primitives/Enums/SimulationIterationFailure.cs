namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// Why an iteration of a simulation failed. A failed iteration has no finish time, no critical
    /// activities and no critical paths, and so the statistics of a simulation leave it out.
    /// </summary>
    public enum SimulationIterationFailure
    {
        /// <summary>
        /// The iteration did not fail.
        /// </summary>
        None = 0,

        /// <summary>
        /// The resource scheduler could not place every activity.
        /// </summary>
        ResourceSchedulingStall = 1,

        /// <summary>
        /// The schedule ran past <see cref="GraphLimits.MaximumTimeValue"/>.
        /// </summary>
        MaximumTimeValueExceeded = 2,

        /// <summary>
        /// An activity could not finish by its maximum latest finish time with the durations of the
        /// iteration. The schedule is then not valid. A compile with the same durations reports
        /// <see cref="GraphCompilationErrorCode.C0010"/>, or
        /// <see cref="GraphCompilationErrorCode.P0030"/> when the duration of an activity is longer
        /// than the window from its minimum earliest start time to its maximum latest finish time.
        /// </summary>
        InvalidConstraints = 3,
    }
}
