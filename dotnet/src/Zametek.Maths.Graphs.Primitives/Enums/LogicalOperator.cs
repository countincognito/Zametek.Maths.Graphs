namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// How an activity's target resources combine when the scheduler decides
    /// which resources may perform it.
    /// </summary>
    public enum LogicalOperator
    {
        /// <summary>
        /// The activity requires all of its target resources; if any are missing
        /// from the supplied list it cannot be scheduled.
        /// </summary>
        AND,

        /// <summary>
        /// Any one of the activity's target resources will do.
        /// </summary>
        OR,

        /// <summary>
        /// Like <see cref="AND"/>, but evaluated only over the target resources
        /// actually present in the supplied list.
        /// </summary>
        ACTIVE_AND,
    }
}
