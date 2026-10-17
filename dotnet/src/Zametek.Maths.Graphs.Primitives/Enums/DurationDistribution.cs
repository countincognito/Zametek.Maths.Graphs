namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// The distribution from which a simulation draws the duration of an activity that has
    /// a spread, from its optimistic (O), most likely (ML) and pessimistic (P) durations.
    /// </summary>
    /// <remarks>
    /// Every distribution lies within the range from O to P. The values of the members are
    /// part of the contract: a stored simulation request can name one of them.
    /// </remarks>
    public enum DurationDistribution
    {
        /// <summary>
        /// A beta distribution on the range from O to P, with the PERT mean,
        /// (O + 4 ML + P) / 6, and the PERT variance, ((P - O) / 6) squared. This is the beta of
        /// the reference PERT tools, and it matches the expected duration and the standard
        /// deviation that a plan shows for an activity.
        /// </summary>
        BetaPert = 0,

        /// <summary>
        /// The modified PERT beta distribution on the range from O to P: its mode is ML, and its
        /// shape value (4 by default) sets how tightly it is drawn around the mode. Its mean is
        /// the PERT mean, but its variance is not the PERT variance: it is
        /// (mean - O) (P - mean) / (shape + 3), which is larger than that of <see cref="BetaPert"/>
        /// when ML is near the middle of the range, and smaller when ML is near O or P.
        /// </summary>
        ModifiedBetaPert = 1,

        /// <summary>
        /// A triangular distribution with its minimum at O, its mode at ML and its maximum at P.
        /// </summary>
        Triangular = 2,

        /// <summary>
        /// A normal distribution with the PERT mean and the PERT standard deviation, (P - O) / 6,
        /// held to the range from O to P: a draw outside the range takes the nearest end of it.
        /// </summary>
        Normal = 3,

        /// <summary>
        /// A uniform distribution on the range from O to P.
        /// </summary>
        Uniform = 4,
    }
}
