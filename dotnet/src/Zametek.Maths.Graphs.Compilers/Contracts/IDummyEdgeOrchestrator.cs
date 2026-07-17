using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// Orchestrates all dummy-edge operations for an Activity-on-Arrow graph.
    /// The orchestrator holds no graph state of its own - it operates on the state
    /// owned by the arrow graph builder, which is supplied at construction time.
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
        /// Inserts a dummy edge from <paramref name="tailNode"/> to <paramref name="headNode"/>.
        /// </summary>
        void ConnectWithDummyEdge(Node<T, IEvent<T>> tailNode, Node<T, IEvent<T>> headNode);

        /// <summary>
        /// Removes a dummy edge from the graph, merging adjacent nodes where possible.
        /// </summary>
        bool RemoveDummyActivity(T activityId);

        /// <summary>
        /// Redirects redundant dummy edges (canonical arrow-graph normalisation step 1).
        /// </summary>
        bool RedirectDummyEdges();

        /// <summary>
        /// Removes dummy edges that are transitively implied (transitive reduction
        /// for dummy edges only).
        /// </summary>
        bool RemoveRedundantDummyEdges();

        /// <summary>
        /// Removes dummy edges made redundant by transitivity, starting from
        /// <paramref name="nodeId"/>.
        /// </summary>
        void RemoveRedundantIncomingDummyEdges(T nodeId, Dictionary<T, HashSet<T>> nodeIdAncestorLookup);

        /// <summary>
        /// Returns all dummy edges in depth-first descending order from the start node.
        /// </summary>
        List<Edge<T, TActivity>> GetDummyEdgesInDescendingOrder();
    }
}
