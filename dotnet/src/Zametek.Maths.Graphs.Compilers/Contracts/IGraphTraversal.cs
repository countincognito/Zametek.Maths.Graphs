using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    // Minimal read-only view of a graph that Tarjan's strongly connected components
    // algorithm needs, abstracting away whether the graph is traversed in edge-space
    // (arrow graphs) or node-space (vertex graphs). The arrow/vertex adapters wrap the
    // respective graph state and carry all of that variance, so the algorithm itself
    // can be written once.
    internal interface IGraphTraversal<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        // The keys (edge ids for arrow graphs, node ids for vertex graphs) the
        // algorithm iterates over.
        IEnumerable<T> Keys { get; }

        // The predecessor keys of the given key, in the same key-space as Keys.
        IEnumerable<T> PredecessorKeys(T referenceId);

        // Whether the element identified by the key is a removable dummy.
        bool IsRemovable(T referenceId);
    }
}
