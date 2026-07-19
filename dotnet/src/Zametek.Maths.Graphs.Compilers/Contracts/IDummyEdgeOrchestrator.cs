using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// Orchestrates the dummy-edge operations for an Activity-on-Arrow graph. The
    /// engine is stateless: the graph state - and the ID/activity generators and
    /// SCC finder each operation needs - are supplied to its methods by the builder
    /// that owns them. It owns the dummy-edge mutation primitives; the reduction
    /// traversal that decides which dummy edges are redundant lives in
    /// <see cref="IArrowTransitiveReducer{T, TResourceId, TWorkStreamId, TActivity}"/>.
    /// </summary>
    /// <typeparam name="T">The activity/event ID type.</typeparam>
    /// <typeparam name="TResourceId">The resource ID type.</typeparam>
    /// <typeparam name="TWorkStreamId">The work-stream ID type.</typeparam>
    /// <typeparam name="TActivity">The activity type.</typeparam>
    public interface IDummyEdgeOrchestrator<T, TResourceId, TWorkStreamId, TActivity>
        where TActivity : class, IActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        /// <summary>
        /// Inserts a dummy edge from <paramref name="tailNode"/> to
        /// <paramref name="headNode"/>, minting its ID and activity from the given
        /// generators.
        /// </summary>
        void ConnectWithDummyEdge(
            ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            IIdGenerator<T> edgeIdGenerator,
            IActivityGenerator<T, TResourceId, TWorkStreamId, TActivity> dummyActivityGenerator,
            Node<T, IEvent<T>> tailNode,
            Node<T, IEvent<T>> headNode);

        /// <summary>
        /// Removes a dummy edge from the graph, merging adjacent nodes where possible.
        /// </summary>
        bool RemoveDummyActivity(
            ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            T activityId);

        /// <summary>
        /// Redirects redundant dummy edges (canonical arrow-graph normalisation step 1).
        /// </summary>
        bool RedirectDummyEdges(
            ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            IArrowStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity> sccFinder);

        /// <summary>
        /// Removes dummy edges that are transitively implied (transitive reduction
        /// for dummy edges only).
        /// </summary>
        bool RemoveRedundantDummyEdges(
            ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            IArrowStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity> sccFinder);

        /// <summary>
        /// Returns all dummy edges in depth-first descending order from the start node.
        /// </summary>
        List<Edge<T, TActivity>> GetDummyEdgesInDescendingOrder(
            ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state);
    }
}
