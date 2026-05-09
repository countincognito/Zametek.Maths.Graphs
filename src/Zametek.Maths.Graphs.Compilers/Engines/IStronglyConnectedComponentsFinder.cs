using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    // Finds strongly connected components (Tarjan's algorithm) for Arrow graphs.
    // In an Arrow graph, activities are edges — the algorithm traverses edge-space.
    internal interface IArrowStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity, TEvent>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
        where TActivity : IActivity<T, TResourceId, TWorkStreamId>
        where TEvent : IEvent<T>
    {
        IList<ICircularDependency<T>> FindStronglyConnectedComponents(
            IEnumerable<T> edgeIds,
            IDictionary<T, Edge<T, TActivity>> edgeLookup,
            IDictionary<T, Node<T, TEvent>> edgeHeadNodeLookup,
            IDictionary<T, Node<T, TEvent>> edgeTailNodeLookup);
    }

    // Finds strongly connected components (Tarjan's algorithm) for Vertex graphs.
    // In a Vertex graph, activities are nodes — the algorithm traverses node-space.
    internal interface IVertexStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity, TEvent>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
        where TActivity : IActivity<T, TResourceId, TWorkStreamId>
        where TEvent : IEvent<T>
    {
        IList<ICircularDependency<T>> FindStronglyConnectedComponents(
            IEnumerable<T> nodeIds,
            IDictionary<T, Node<T, TActivity>> nodeLookup,
            IDictionary<T, Node<T, TActivity>> edgeHeadNodeLookup,
            IDictionary<T, Node<T, TActivity>> edgeTailNodeLookup);
    }
}
