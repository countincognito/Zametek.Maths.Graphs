using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// Computes ancestor lookups and performs transitive reduction on a directed
    /// graph whose nodes are keyed by ID. Shared by both arrow and vertex
    /// transitive reducers.
    /// </summary>
    /// <typeparam name="T">The node ID type.</typeparam>
    public interface ITransitiveReducer<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        /// <summary>
        /// Builds a lookup from each node ID to the full set of its ancestor node
        /// IDs. Returns null if the graph has unsatisfied dependencies or circular
        /// dependencies.
        /// </summary>
        Dictionary<T, HashSet<T>>? GetAncestorNodesLookup();

        /// <summary>
        /// Performs transitive reduction on the graph, removing all redundant
        /// edges. Returns false if the reduction cannot be performed (unsatisfied
        /// dependencies or circular dependencies).
        /// </summary>
        bool ReduceGraph();
    }
}
