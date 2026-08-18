namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// The error codes a graph compilation can report. Codes prefixed P are found
    /// before compilation; codes prefixed C are found after compilation.
    /// </summary>
    /// <remarks>
    /// The numeric values are serialized by consumers (e.g. saved project files), so
    /// they must remain stable - new codes are appended with the next unused value
    /// rather than inserted in code order.
    /// </remarks>
    public enum GraphCompilationErrorCode
    {
        /// <summary>
        /// Invalid dependencies - an activity depends on an ID that no activity in the graph has.
        /// </summary>
        P0010 = 0,

        /// <summary>
        /// Circular dependencies - the activity dependencies form a cycle.
        /// </summary>
        P0020 = 1,

        /// <summary>
        /// Invalid pre-compilation constraints - an activity's requested constraints are self-contradictory.
        /// </summary>
        P0030 = 2,

        /// <summary>
        /// All resources are marked as explicit targets, but not all activities have targeted resources.
        /// </summary>
        P0040 = 3,

        /// <summary>
        /// Unable to remove unnecessary edges during pre-compilation clean-up.
        /// </summary>
        P0050 = 4,

        /// <summary>
        /// Some necessary explicit target resources are unavailable.
        /// </summary>
        P0060 = 5,

        /// <summary>
        /// Internally inconsistent input collections - an activity or resource holds a set whose
        /// lookups disagree with its contents, typically the result of unsynchronized concurrent
        /// modification while compilation inputs were being prepared.
        /// </summary>
        P0070 = 7,

        /// <summary>
        /// Declared values or counts fall outside the limits in <see cref="GraphLimits"/> - for
        /// example a negative or absurdly large duration or time constraint, or more activities,
        /// resources or work streams than a compilation accepts.
        /// </summary>
        P0080 = 9,

        /// <summary>
        /// Invalid post-compilation constraints - the computed times violate an activity's constraints.
        /// </summary>
        C0010 = 6,

        /// <summary>
        /// Resource scheduling could not produce a usable schedule - either one or more activities
        /// could never be scheduled onto the supplied resources (so the scheduler stopped instead of
        /// looping forever), or the computed schedule ran past <see cref="GraphLimits.MaximumTimeValue"/>.
        /// </summary>
        C0020 = 8
    }
}
