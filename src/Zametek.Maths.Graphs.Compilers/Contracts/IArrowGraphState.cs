using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    // Read-only contract for the Activity-on-Arrow graph state. Exposed publicly so that
    // custom CPM engines and SCC finders can be implemented against it; the concrete
    // ArrowGraphState (and its mutation API) remains internal to the assembly.
    public interface IArrowGraphState<T, TResourceId, TWorkStreamId, TActivity>
        where TActivity : class, IActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        Node<T, IEvent<T>> StartNode { get; }

        Node<T, IEvent<T>> EndNode { get; }

        IEnumerable<T> EdgeIds { get; }

        IEnumerable<T> NodeIds { get; }

        IEnumerable<Edge<T, TActivity>> Edges { get; }

        IEnumerable<Node<T, IEvent<T>>> Nodes { get; }

        IEnumerable<T> InvalidDependencies { get; }

        bool AllDependenciesSatisfied { get; }

        int EdgeCount { get; }

        int NodeCount { get; }

        IEnumerable<TActivity> Activities { get; }

        IEnumerable<IEvent<T>> Events { get; }

        IEnumerable<Node<T, IEvent<T>>> StartNodes { get; }

        IEnumerable<Node<T, IEvent<T>>> EndNodes { get; }

        IEnumerable<Node<T, IEvent<T>>> NormalNodes { get; }

        IEnumerable<Node<T, IEvent<T>>> IsolatedNodes { get; }

        bool ContainsEdge(T edgeId);

        bool ContainsNode(T nodeId);

        Edge<T, TActivity> Edge(T edgeId);

        Node<T, IEvent<T>> Node(T nodeId);

        Node<T, IEvent<T>> EdgeHeadNode(T edgeId);

        Node<T, IEvent<T>> EdgeTailNode(T edgeId);

        bool TryGetEdge(T edgeId, out Edge<T, TActivity> edge);

        bool TryGetNode(T nodeId, out Node<T, IEvent<T>> node);

        bool TryGetEdgeHeadNode(T edgeId, out Node<T, IEvent<T>> node);

        bool TryGetEdgeTailNode(T edgeId, out Node<T, IEvent<T>> node);

        bool TryGetUnsatisfiedSuccessors(T dependencyId, out HashSet<Node<T, IEvent<T>>> successors);
    }
}
