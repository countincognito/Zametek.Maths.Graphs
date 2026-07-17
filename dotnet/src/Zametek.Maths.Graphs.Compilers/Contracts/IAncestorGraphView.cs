using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    // Minimal node-space view of a graph that the shared ancestor-node calculation needs.
    // Both arrow and vertex reducers compute ancestors over node-space, so the arrow/vertex
    // adapters wrap the respective graph state behind this view and the recursion can be
    // written once.
    internal interface IAncestorGraphView<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        // The ids of the end nodes the ancestor lookup is seeded from.
        IEnumerable<T> EndNodeIds { get; }

        // Whether the node has no ancestors (a start or isolated node).
        bool IsRootNode(T nodeId);

        // The tail-node ids of the node's incoming edges, i.e. its direct parents.
        IEnumerable<T> ParentNodeIds(T nodeId);
    }
}
