namespace Zametek.Maths.Graphs
{
    // Domain sanity limits. These are not algorithmic requirements - they are bounds
    // chosen so that absurd or corrupted input is rejected with a clear compilation
    // error instead of consuming unbounded time and memory. Values within these
    // limits are accepted; a compilation that violates one reports P0080 (declared
    // values and counts) or C0020 (a computed schedule that runs past the horizon).
    //
    // Why a horizon limit exists at all: each resource schedule retains five
    // per-time-unit allocation streams (resource, cost, billing, effort, activity),
    // so the memory held by a compilation grows as
    // (horizon x resources), independently of how many activities there are.
    /// <summary>
    /// The sanity limits applied to graph compilation inputs. Consumers can use these to
    /// validate user input before compiling, so that out-of-range values are rejected at
    /// the point of entry rather than at compile time.
    /// </summary>
    public static class GraphLimits
    {
        /// <summary>
        /// The largest accepted value for any time-valued input or computed schedule time
        /// (durations, <c>MinimumEarliestStartTime</c>, <c>MaximumLatestFinishTime</c>,
        /// <c>MinimumFreeSlack</c>, and the compiled schedule's finish time). At one time
        /// unit per day this is roughly 270 years, far beyond any real plan.
        /// </summary>
        public const int MaximumTimeValue = 100_000;

        /// <summary>
        /// The smallest accepted value for any time-valued input. Negative times are never
        /// meaningful, so they are rejected up front rather than after compilation.
        /// </summary>
        public const int MinimumTimeValue = 0;

        /// <summary>
        /// The largest accepted number of activities in a graph. Compilation cost grows
        /// steeply with activity count (the priority-list calculation dominates), so this
        /// bounds a compile to a workable size rather than describing an algorithmic ceiling.
        /// </summary>
        public const int MaximumActivityCount = 2_000;

        /// <summary>
        /// The largest accepted number of resources supplied to a compilation. Resources
        /// multiply the memory held by the per-time-unit allocation streams, so this bound
        /// works together with <see cref="MaximumTimeValue"/>.
        /// </summary>
        public const int MaximumResourceCount = 1_000;

        /// <summary>
        /// The largest accepted number of work streams supplied to a compilation.
        /// </summary>
        public const int MaximumWorkStreamCount = 100;
    }
}
