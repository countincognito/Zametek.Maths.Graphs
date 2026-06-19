using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    // Computes the ancestor-node lookup for any directed graph whose nodes are keyed by T.
    // Shared by both arrow and vertex transitive reducers.
    public interface ITransitiveReducer<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        // Builds a lookup from each node ID to the full set of its ancestor node IDs.
        // Returns null if the graph has unsatisfied dependencies or circular dependencies.
        Dictionary<T, HashSet<T>> GetAncestorNodesLookup();

        // Performs transitive reduction on the graph, removing all redundant edges.
        // Returns false if the reduction cannot be performed (unsatisfied deps, circulars).
        bool ReduceGraph();
    }
}
