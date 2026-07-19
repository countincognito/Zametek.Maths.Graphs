using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// Calculates critical path variables for Activity-on-Vertex graphs. The
    /// engine operates on the graph state supplied to its methods, plus the
    /// in-flight constraint list for the current pass.
    /// </summary>
    /// <typeparam name="T">The activity/event ID type.</typeparam>
    /// <typeparam name="TResourceId">The resource ID type.</typeparam>
    /// <typeparam name="TWorkStreamId">The work-stream ID type.</typeparam>
    /// <typeparam name="TActivity">The activity type.</typeparam>
    public interface IVertexCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
        where TActivity : IActivity<T, TResourceId, TWorkStreamId>
    {
        /// <summary>
        /// The forward pass: computes earliest start/finish times. When
        /// <paramref name="shuffle"/> is true the remaining elements are processed
        /// in a random order on each iteration (used to prove the calculation is
        /// order-independent). Returns false when the pass cannot complete;
        /// violations are appended to <paramref name="invalidConstraints"/>.
        /// </summary>
        bool CalculateCriticalPathForwardFlow(
            IVertexGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            List<IInvalidConstraint<T>> invalidConstraints,
            bool shuffle);

        /// <summary>
        /// The backward pass: computes latest start/finish times and slack. When
        /// <paramref name="shuffle"/> is true the remaining elements are processed
        /// in a random order on each iteration. Returns false when the pass cannot
        /// complete; violations are appended to <paramref name="invalidConstraints"/>.
        /// </summary>
        bool CalculateCriticalPathBackwardFlow(
            IVertexGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            List<IInvalidConstraint<T>> invalidConstraints,
            bool shuffle);

        /// <summary>
        /// Fills in critical-path values for isolated nodes (activities with no
        /// dependencies or dependents), which the flow passes do not reach.
        /// </summary>
        bool BackFillIsolatedNodes(
            IVertexGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            List<IInvalidConstraint<T>> invalidConstraints);
    }
}
