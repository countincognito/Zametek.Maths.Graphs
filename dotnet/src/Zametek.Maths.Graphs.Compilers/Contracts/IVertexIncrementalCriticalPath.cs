using System;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// An in-progress incremental critical-path calculation over a graph whose
    /// structure does not change. Created by
    /// <see cref="IVertexCriticalPathEngine{T, TResourceId, TWorkStreamId, TActivity}.BeginIncrementalCriticalPath"/>
    /// once the graph has been calculated in full, and then told about each
    /// subsequent change to a single activity's duration, so that only the values
    /// that actually move are recomputed.
    /// </summary>
    /// <remarks>
    /// The session caches a topological ordering of the graph, so it is only valid
    /// for as long as the graph structure is unchanged. Adding or removing an
    /// activity or a dependency invalidates it, and a new session must be begun.
    /// Durations may change freely - that is what it exists for.
    /// </remarks>
    /// <typeparam name="T">The activity/event ID type.</typeparam>
    public interface IVertexIncrementalCriticalPath<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        /// <summary>
        /// Recalculates the graph after the given activity's duration has been
        /// changed, propagating from that activity and stopping wherever a
        /// recomputed value matches the one already there.
        /// </summary>
        /// <remarks>
        /// Returns false when the change moved the project finish time, which
        /// shifts every latest finish time in the graph and is not worth
        /// propagating. The caller must then run a full calculation, after which
        /// the session may continue to be used.
        /// </remarks>
        /// <param name="activityId">The activity whose duration has just changed.</param>
        bool ApplyDurationChange(T activityId);
    }
}
