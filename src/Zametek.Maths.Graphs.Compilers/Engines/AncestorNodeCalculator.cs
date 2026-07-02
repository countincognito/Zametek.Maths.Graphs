using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    // Shared ancestor-node calculation for both arrow and vertex transitive reducers.
    // The reducers handle the (graph-flavour specific) dependency-satisfied and circular
    // dependency checks and supply this calculation with a node-space view of the graph.
    internal static class AncestorNodeCalculator
    {
        public static Dictionary<T, HashSet<T>>? GetAncestorNodesLookup<T>(
            IAncestorGraphView<T> view,
            IReadOnlyCollection<ICircularDependency<T>> circularDependencies)
            where T : struct, IComparable<T>, IEquatable<T>
        {
            if (view is null)
            {
                throw new ArgumentNullException(nameof(view));
            }
            if (circularDependencies is null)
            {
                throw new ArgumentNullException(nameof(circularDependencies));
            }

            if (circularDependencies.Count != 0)
            {
                return null;
            }

            var nodeIdAncestorLookup = new Dictionary<T, HashSet<T>>();
            List<T> endNodeIds = view.EndNodeIds.ToList();

            foreach (T endNodeId in endNodeIds)
            {
                HashSet<T> totalAncestorNodes = GetAncestorNodes(view, endNodeId, nodeIdAncestorLookup);
                nodeIdAncestorLookup.Add(endNodeId, totalAncestorNodes);
            }

            return nodeIdAncestorLookup;
        }

        private static HashSet<T> GetAncestorNodes<T>(
            IAncestorGraphView<T> view,
            T nodeId,
            Dictionary<T, HashSet<T>> nodeIdAncestorLookup)
            where T : struct, IComparable<T>, IEquatable<T>
        {
            if (nodeIdAncestorLookup is null)
            {
                throw new ArgumentNullException(nameof(nodeIdAncestorLookup));
            }

            var totalAncestorNodes = new HashSet<T>();

            if (view.IsRootNode(nodeId))
            {
                return totalAncestorNodes;
            }

            // Go through each incoming edge and find the nodes
            // to which they connect.
            foreach (T tailNodeId in view.ParentNodeIds(nodeId).ToList())
            {
                totalAncestorNodes.Add(tailNodeId);
                // If the lookup holds the ancestor nodes for the tail
                // node then add them to the ancestor nodes. Otherwise
                // calculate the ancestor nodes for the tail node too.
                if (!nodeIdAncestorLookup.TryGetValue(tailNodeId, out HashSet<T> tailNodeAncestorNodes))
                {
                    tailNodeAncestorNodes = GetAncestorNodes(view, tailNodeId, nodeIdAncestorLookup);
                    nodeIdAncestorLookup.Add(tailNodeId, tailNodeAncestorNodes);
                }
                totalAncestorNodes.UnionWith(tailNodeAncestorNodes);
            }

            return totalAncestorNodes;
        }
    }
}
