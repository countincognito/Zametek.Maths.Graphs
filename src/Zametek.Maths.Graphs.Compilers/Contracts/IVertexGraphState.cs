using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// Read-only contract for the Activity-on-Vertex graph state (activities on
    /// nodes, events on edges). Exposed publicly so that custom CPM engines and
    /// SCC finders can be implemented against it; the concrete state (and its
    /// mutation API) remains internal to the assembly.
    /// </summary>
    /// <typeparam name="T">The activity/event ID type.</typeparam>
    /// <typeparam name="TResourceId">The resource ID type.</typeparam>
    /// <typeparam name="TWorkStreamId">The work-stream ID type.</typeparam>
    /// <typeparam name="TActivity">The activity type.</typeparam>
    public interface IVertexGraphState<T, TResourceId, TWorkStreamId, TActivity>
        where TActivity : IActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        /// <summary>
        /// The IDs of all edges (events).
        /// </summary>
        IEnumerable<T> EdgeIds { get; }

        /// <summary>
        /// The IDs of all nodes (activities).
        /// </summary>
        IEnumerable<T> NodeIds { get; }

        /// <summary>
        /// All edges (events).
        /// </summary>
        IEnumerable<Edge<T, IEvent<T>>> Edges { get; }

        /// <summary>
        /// All nodes (activities).
        /// </summary>
        IEnumerable<Node<T, TActivity>> Nodes { get; }

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
        /// The activities carried on the nodes.
        /// </summary>
        IEnumerable<TActivity> Activities { get; }

        /// <summary>
        /// The events carried on the edges.
        /// </summary>
        IEnumerable<IEvent<T>> Events { get; }

        /// <summary>
        /// The nodes with only outgoing edges.
        /// </summary>
        IEnumerable<Node<T, TActivity>> StartNodes { get; }

        /// <summary>
        /// The nodes with only incoming edges.
        /// </summary>
        IEnumerable<Node<T, TActivity>> EndNodes { get; }

        /// <summary>
        /// The nodes with both incoming and outgoing edges.
        /// </summary>
        IEnumerable<Node<T, TActivity>> NormalNodes { get; }

        /// <summary>
        /// The nodes with no edges at all.
        /// </summary>
        IEnumerable<Node<T, TActivity>> IsolatedNodes { get; }

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
        Edge<T, IEvent<T>> Edge(T edgeId);

        /// <summary>
        /// Resolves the node with the given ID.
        /// </summary>
        Node<T, TActivity> Node(T nodeId);

        /// <summary>
        /// Resolves the node the given edge points to.
        /// </summary>
        Node<T, TActivity> EdgeHeadNode(T edgeId);

        /// <summary>
        /// Resolves the node the given edge starts from.
        /// </summary>
        Node<T, TActivity> EdgeTailNode(T edgeId);

        /// <summary>
        /// Attempts to resolve the edge with the given ID.
        /// </summary>
        bool TryGetEdge(T edgeId, out Edge<T, IEvent<T>> edge);

        /// <summary>
        /// Attempts to resolve the node with the given ID.
        /// </summary>
        bool TryGetNode(T nodeId, out Node<T, TActivity> node);

        /// <summary>
        /// Attempts to resolve the node the given edge points to.
        /// </summary>
        bool TryGetEdgeHeadNode(T edgeId, out Node<T, TActivity> node);

        /// <summary>
        /// Attempts to resolve the node the given edge starts from.
        /// </summary>
        bool TryGetEdgeTailNode(T edgeId, out Node<T, TActivity> node);

        /// <summary>
        /// Attempts to resolve the nodes still waiting on the given dependency ID.
        /// </summary>
        bool TryGetUnsatisfiedSuccessors(T dependencyId, out HashSet<Node<T, TActivity>> successors);
    }
}
