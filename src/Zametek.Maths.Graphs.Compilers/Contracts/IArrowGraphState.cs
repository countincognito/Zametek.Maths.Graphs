using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// Read-only contract for the Activity-on-Arrow graph state (activities on
    /// edges, events on nodes). Exposed publicly so that custom CPM engines and
    /// SCC finders can be implemented against it; the concrete state (and its
    /// mutation API) remains internal to the assembly.
    /// </summary>
    /// <typeparam name="T">The activity/event ID type.</typeparam>
    /// <typeparam name="TResourceId">The resource ID type.</typeparam>
    /// <typeparam name="TWorkStreamId">The work-stream ID type.</typeparam>
    /// <typeparam name="TActivity">The activity type.</typeparam>
    public interface IArrowGraphState<T, TResourceId, TWorkStreamId, TActivity>
        where TActivity : class, IActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        /// <summary>
        /// The single start node of the arrow graph.
        /// </summary>
        Node<T, IEvent<T>> StartNode { get; }

        /// <summary>
        /// The single end node of the arrow graph.
        /// </summary>
        Node<T, IEvent<T>> EndNode { get; }

        /// <summary>
        /// The IDs of all edges (activities).
        /// </summary>
        IEnumerable<T> EdgeIds { get; }

        /// <summary>
        /// The IDs of all nodes (events).
        /// </summary>
        IEnumerable<T> NodeIds { get; }

        /// <summary>
        /// All edges (activities).
        /// </summary>
        IEnumerable<Edge<T, TActivity>> Edges { get; }

        /// <summary>
        /// All nodes (events).
        /// </summary>
        IEnumerable<Node<T, IEvent<T>>> Nodes { get; }

        /// <summary>
        /// The IDs of dependencies that are referenced but not yet present in the graph.
        /// </summary>
        IEnumerable<T> InvalidDependencies { get; }

        /// <summary>
        /// Whether every referenced dependency is present in the graph.
        /// </summary>
        bool AllDependenciesSatisfied { get; }

        /// <summary>
        /// The number of edges.
        /// </summary>
        int EdgeCount { get; }

        /// <summary>
        /// The number of nodes.
        /// </summary>
        int NodeCount { get; }

        /// <summary>
        /// The activities carried on the edges.
        /// </summary>
        IEnumerable<TActivity> Activities { get; }

        /// <summary>
        /// The events carried on the nodes.
        /// </summary>
        IEnumerable<IEvent<T>> Events { get; }

        /// <summary>
        /// The nodes with only outgoing edges.
        /// </summary>
        IEnumerable<Node<T, IEvent<T>>> StartNodes { get; }

        /// <summary>
        /// The nodes with only incoming edges.
        /// </summary>
        IEnumerable<Node<T, IEvent<T>>> EndNodes { get; }

        /// <summary>
        /// The nodes with both incoming and outgoing edges.
        /// </summary>
        IEnumerable<Node<T, IEvent<T>>> NormalNodes { get; }

        /// <summary>
        /// The nodes with no edges at all.
        /// </summary>
        IEnumerable<Node<T, IEvent<T>>> IsolatedNodes { get; }

        /// <summary>
        /// Whether an edge with the given ID exists.
        /// </summary>
        bool ContainsEdge(T edgeId);

        /// <summary>
        /// Whether a node with the given ID exists.
        /// </summary>
        bool ContainsNode(T nodeId);

        /// <summary>
        /// Resolves the edge with the given ID.
        /// </summary>
        Edge<T, TActivity> Edge(T edgeId);

        /// <summary>
        /// Resolves the node with the given ID.
        /// </summary>
        Node<T, IEvent<T>> Node(T nodeId);

        /// <summary>
        /// Resolves the node the given edge points to.
        /// </summary>
        Node<T, IEvent<T>> EdgeHeadNode(T edgeId);

        /// <summary>
        /// Resolves the node the given edge starts from.
        /// </summary>
        Node<T, IEvent<T>> EdgeTailNode(T edgeId);

        /// <summary>
        /// Attempts to resolve the edge with the given ID.
        /// </summary>
        bool TryGetEdge(T edgeId, out Edge<T, TActivity> edge);

        /// <summary>
        /// Attempts to resolve the node with the given ID.
        /// </summary>
        bool TryGetNode(T nodeId, out Node<T, IEvent<T>> node);

        /// <summary>
        /// Attempts to resolve the node the given edge points to.
        /// </summary>
        bool TryGetEdgeHeadNode(T edgeId, out Node<T, IEvent<T>> node);

        /// <summary>
        /// Attempts to resolve the node the given edge starts from.
        /// </summary>
        bool TryGetEdgeTailNode(T edgeId, out Node<T, IEvent<T>> node);

        /// <summary>
        /// Attempts to resolve the nodes still waiting on the given dependency ID.
        /// </summary>
        bool TryGetUnsatisfiedSuccessors(T dependencyId, out HashSet<Node<T, IEvent<T>>> successors);
    }
}
