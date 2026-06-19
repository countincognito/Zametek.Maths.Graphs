using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    // Read-only contract for the Activity-on-Vertex graph state. Exposed publicly so that
    // custom CPM engines and SCC finders can be implemented against it; the concrete
    // VertexGraphState (and its mutation API) remains internal to the assembly.
    public interface IVertexGraphState<T, TResourceId, TWorkStreamId, TActivity>
        where TActivity : IActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        IEnumerable<T> EdgeIds { get; }

        IEnumerable<T> NodeIds { get; }

        IEnumerable<Edge<T, IEvent<T>>> Edges { get; }

        IEnumerable<Node<T, TActivity>> Nodes { get; }

        IEnumerable<T> InvalidDependencies { get; }

        bool AllDependenciesSatisfied { get; }

        int EdgeCount { get; }

        int NodeCount { get; }

        IEnumerable<TActivity> Activities { get; }

        IEnumerable<IEvent<T>> Events { get; }

        IEnumerable<Node<T, TActivity>> StartNodes { get; }

        IEnumerable<Node<T, TActivity>> EndNodes { get; }

        IEnumerable<Node<T, TActivity>> NormalNodes { get; }

        IEnumerable<Node<T, TActivity>> IsolatedNodes { get; }

        bool ContainsEdge(T edgeId);

        bool ContainsNode(T nodeId);

        Edge<T, IEvent<T>> Edge(T edgeId);

        Node<T, TActivity> Node(T nodeId);

        Node<T, TActivity> EdgeHeadNode(T edgeId);

        Node<T, TActivity> EdgeTailNode(T edgeId);

        bool TryGetEdge(T edgeId, out Edge<T, IEvent<T>> edge);

        bool TryGetNode(T nodeId, out Node<T, TActivity> node);

        bool TryGetEdgeHeadNode(T edgeId, out Node<T, TActivity> node);

        bool TryGetEdgeTailNode(T edgeId, out Node<T, TActivity> node);

        bool TryGetUnsatisfiedSuccessors(T dependencyId, out HashSet<Node<T, TActivity>> successors);
    }
}
