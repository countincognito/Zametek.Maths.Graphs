namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// How one step of a critical path follows the step before it.
    /// </summary>
    public enum CriticalPathLink
    {
        /// <summary>
        /// The first step of a path, which does not follow another step.
        /// </summary>
        None = 0,

        /// <summary>
        /// The activity depends on the one before it, as the plan says.
        /// </summary>
        Dependency = 1,

        /// <summary>
        /// The activity does not depend on the one before it. It starts when it does because the
        /// two share a resource, and the resource finishes the one before it starts the other.
        /// </summary>
        Resource = 2,
    }
}
