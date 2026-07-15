using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    // Shared ancestor-node calculation for both arrow and vertex transitive reducers.
    // The reducers handle the (graph-flavour specific) dependency-satisfied and circular
    // dependency checks and supply this calculation with a node-space view of the graph.
    //
    // The traversal is iterative (an explicit post-order stack) rather than recursive, so a
    // deep dependency chain cannot overflow the call stack. This does not change the O(N^2)
    // memory of the lookup itself - each node stores the set of all of its ancestors, which
    // is inherent to this reduction algorithm and remains a separate scaling limit.
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

            foreach (T endNodeId in view.EndNodeIds.ToList())
            {
                FillAncestorNodes(view, endNodeId, nodeIdAncestorLookup);
            }

            return nodeIdAncestorLookup;
        }

        // Iterative post-order traversal: a node's ancestor set is the union of its parents'
        // ancestor sets plus the parents themselves, so every parent must be resolved before
        // the node itself. A node is (re)visited via the stack until all its parents are in the
        // lookup, then computed and popped. The graph is known to be acyclic here (circular
        // dependencies are rejected above), so this always terminates.
        private static void FillAncestorNodes<T>(
            IAncestorGraphView<T> view,
            T rootNodeId,
            Dictionary<T, HashSet<T>> nodeIdAncestorLookup)
            where T : struct, IComparable<T>, IEquatable<T>
        {
            var stack = new Stack<T>();
            stack.Push(rootNodeId);

            while (stack.Count != 0)
            {
                T nodeId = stack.Peek();

                if (nodeIdAncestorLookup.ContainsKey(nodeId))
                {
                    stack.Pop();
                    continue;
                }

                if (view.IsRootNode(nodeId))
                {
                    nodeIdAncestorLookup[nodeId] = new HashSet<T>();
                    stack.Pop();
                    continue;
                }

                // Push any parents that are not yet resolved; the node stays on the stack and is
                // only computed on a later visit, once every parent is present in the lookup.
                List<T> parentNodeIds = view.ParentNodeIds(nodeId).ToList();
                bool allParentsResolved = true;
                foreach (T parentNodeId in parentNodeIds)
                {
                    if (!nodeIdAncestorLookup.ContainsKey(parentNodeId))
                    {
                        stack.Push(parentNodeId);
                        allParentsResolved = false;
                    }
                }

                if (!allParentsResolved)
                {
                    continue;
                }

                var totalAncestorNodes = new HashSet<T>();
                foreach (T parentNodeId in parentNodeIds)
                {
                    totalAncestorNodes.Add(parentNodeId);
                    totalAncestorNodes.UnionWith(nodeIdAncestorLookup[parentNodeId]);
                }

                nodeIdAncestorLookup[nodeId] = totalAncestorNodes;
                stack.Pop();
            }
        }
    }
}
