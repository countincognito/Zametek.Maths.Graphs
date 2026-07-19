using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    // Transitive reducer for Activity-on-Vertex graphs. Stateless: the graph state
    // and SCC finder are supplied to each method, so a single instance can reduce
    // any graph and is safely shared across builder clones.
    /// <summary>
    /// Default transitive reducer for Activity-on-Vertex graphs.
    /// </summary>
    public sealed class VertexTransitiveReducer<T, TResourceId, TWorkStreamId, TActivity>
        : IVertexTransitiveReducer<T, TResourceId, TWorkStreamId, TActivity>
        where TActivity : IActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        /// <inheritdoc/>
        public Dictionary<T, HashSet<T>>? GetAncestorNodesLookup(
            VertexGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            IVertexStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity> sccFinder)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }
            if (sccFinder is null)
            {
                throw new ArgumentNullException(nameof(sccFinder));
            }

            if (!state.AllDependenciesSatisfied)
            {
                return null;
            }

            List<ICircularDependency<T>> circularDependencies =
                sccFinder.FindStronglyCircularDependencies(state, ignoreDummies: false);

            var ancestorGraphView = new VertexAncestorGraphView<T, TResourceId, TWorkStreamId, TActivity>(state);
            return AncestorNodeCalculator.GetAncestorNodesLookup(ancestorGraphView, circularDependencies);
        }

        /// <inheritdoc/>
        public bool ReduceGraph(
            VertexGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            IVertexStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity> sccFinder)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }
            if (sccFinder is null)
            {
                throw new ArgumentNullException(nameof(sccFinder));
            }

            AncestorBitSets<T>? ancestorBitSets = GetAncestorBitSets(state, sccFinder);

            if (ancestorBitSets is null)
            {
                return false;
            }

            RemoveRedundantIncomingEdges(state, state.EndNodes.Select(x => x.Id), ancestorBitSets);

            return true;
        }

        // Same checks as GetAncestorNodesLookup, but the ancestors stay in their compact
        // bitset form - ReduceGraph never materialises the dictionary-of-hashsets.
        private static AncestorBitSets<T>? GetAncestorBitSets(
            VertexGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            IVertexStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity> sccFinder)
        {
            if (!state.AllDependenciesSatisfied)
            {
                return null;
            }

            List<ICircularDependency<T>> circularDependencies =
                sccFinder.FindStronglyCircularDependencies(state, ignoreDummies: false);

            var ancestorGraphView = new VertexAncestorGraphView<T, TResourceId, TWorkStreamId, TActivity>(state);
            return AncestorNodeCalculator.GetAncestorBitSets(ancestorGraphView, circularDependencies);
        }

        // Iterative (was recursive) so a deep dependency chain cannot overflow the
        // stack. A single shared visited set means each node's incoming edges are
        // reduced exactly once: every node removes only its own incoming edges, using
        // the static ancestor bitsets, so the operation is independent of visit order
        // and idempotent per node.
        private static void RemoveRedundantIncomingEdges(
            VertexGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            IEnumerable<T> rootNodeIds,
            AncestorBitSets<T> ancestorBitSets)
        {
            if (ancestorBitSets is null)
            {
                throw new ArgumentNullException(nameof(ancestorBitSets));
            }

            var visited = new HashSet<T>();
            var stack = new Stack<T>();
            foreach (T rootNodeId in rootNodeIds)
            {
                stack.Push(rootNodeId);
            }

            ulong[] scratch = ancestorBitSets.CreateScratch();

            while (stack.Count != 0)
            {
                T nodeId = stack.Pop();
                if (!visited.Add(nodeId))
                {
                    continue;
                }

                Node<T, TActivity> node = state.Node(nodeId);

                if (node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated)
                {
                    continue;
                }

                // Go through all the incoming edges and collate the
                // ancestors of their tail nodes into the scratch bitset.
                AncestorBitSets<T>.ClearScratch(scratch);
                foreach (T incomingEdgeId in node.IncomingEdges)
                {
                    ancestorBitSets.UnionAncestorsInto(scratch, state.EdgeTailNode(incomingEdgeId).Id);
                }

                // Go through the incoming edges and remove any that connect
                // directly to any ancestors of the edges' tail nodes.
                // In a vertex graph, all edges are removable.
                foreach (T edgeId in node.IncomingEdges
                    .Select(x => state.Edge(x))
                    .Where(x => x.Content.CanBeRemoved)
                    .Select(x => x.Id)
                    .ToList())
                {
                    Node<T, TActivity> tailNode = state.EdgeTailNode(edgeId);
                    if (ancestorBitSets.ScratchContains(scratch, tailNode.Id))
                    {
                        // Remove the edge from the tail node.
                        tailNode.OutgoingEdges.Remove(edgeId);
                        state.RemoveEdgeTailNode(edgeId);

                        // Remove the edge from the node itself.
                        node.IncomingEdges.Remove(edgeId);
                        state.RemoveEdgeHeadNode(edgeId);

                        // Remove the edge completely.
                        state.RemoveEdge(edgeId);
                    }
                }

                // Continue with all the remaining incoming edges' tail nodes.
                foreach (T tailNodeId in node.IncomingEdges.Select(x => state.EdgeTailNode(x).Id).ToList())
                {
                    stack.Push(tailNodeId);
                }
            }
        }
    }
}
