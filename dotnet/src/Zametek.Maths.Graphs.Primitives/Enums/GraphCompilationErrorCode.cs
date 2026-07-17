namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// The error codes a graph compilation can report. Codes prefixed P are found
    /// before compilation; codes prefixed C are found after compilation.
    /// </summary>
    public enum GraphCompilationErrorCode
    {
        /// <summary>
        /// Invalid dependencies - an activity depends on an ID that no activity in the graph has.
        /// </summary>
        P0010,

        /// <summary>
        /// Circular dependencies - the activity dependencies form a cycle.
        /// </summary>
        P0020,

        /// <summary>
        /// Invalid pre-compilation constraints - an activity's requested constraints are self-contradictory.
        /// </summary>
        P0030,

        /// <summary>
        /// All resources are marked as explicit targets, but not all activities have targeted resources.
        /// </summary>
        P0040,

        /// <summary>
        /// Unable to remove unnecessary edges during pre-compilation clean-up.
        /// </summary>
        P0050,

        /// <summary>
        /// Some necessary explicit target resources are unavailable.
        /// </summary>
        P0060,

        /// <summary>
        /// Invalid post-compilation constraints - the computed times violate an activity's constraints.
        /// </summary>
        C0010
    }
}
