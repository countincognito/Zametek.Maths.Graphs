namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// How a resource's time (and therefore cost, billing and effort) is spread
    /// across the schedule between the activities it performs.
    /// </summary>
    public enum InterActivityAllocationType
    {
        /// <summary>
        /// No inter-activity spreading; only the scheduled activities themselves
        /// are allocated. Used for synthetic resources.
        /// </summary>
        None,

        /// <summary>
        /// The resource is allocated only while actively performing scheduled
        /// activities; idle gaps between activities are not counted.
        /// </summary>
        Direct,

        /// <summary>
        /// The resource is allocated continuously across the span of its
        /// involvement, filling the gaps between its activities (overhead-style
        /// commitment).
        /// </summary>
        Indirect
    }
}
