using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    // Transitive reducer for Activity-on-Vertex graphs.
    // Operates on the shared VertexGraphState.
    internal sealed class VertexTransitiveReducer<T, TResourceId, TWorkStreamId, TActivity>
        : ITransitiveReducer<T>
        where TActivity : IActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        #region Fields

        private readonly IVertexStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity> m_StronglyConnectedComponentsFinder;
        private readonly VertexGraphState<T, TResourceId, TWorkStreamId, TActivity> m_State;
        private readonly IAncestorGraphView<T> m_AncestorGraphView;

        #endregion

        #region Ctor

        internal VertexTransitiveReducer(
            IVertexStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity> stronglyConnectedComponentsFinder,
            VertexGraphState<T, TResourceId, TWorkStreamId, TActivity> state)
        {
            m_StronglyConnectedComponentsFinder = stronglyConnectedComponentsFinder ?? throw new ArgumentNullException(nameof(stronglyConnectedComponentsFinder));
            m_State = state ?? throw new ArgumentNullException(nameof(state));
            m_AncestorGraphView = new VertexAncestorGraphView<T, TResourceId, TWorkStreamId, TActivity>(m_State);
        }

        #endregion

        #region ITransitiveReducer

        public Dictionary<T, HashSet<T>>? GetAncestorNodesLookup()
        {
            if (!m_State.AllDependenciesSatisfied)
            {
                return null;
            }

            List<ICircularDependency<T>> circularDependencies =
                m_StronglyConnectedComponentsFinder.FindStronglyCircularDependencies(m_State, ignoreDummies: false);

            return AncestorNodeCalculator.GetAncestorNodesLookup(m_AncestorGraphView, circularDependencies);
        }

        public bool ReduceGraph()
        {
            IDictionary<T, HashSet<T>>? ancestorNodesLookup = GetAncestorNodesLookup();

            if (ancestorNodesLookup is null)
            {
                return false;
            }

            RemoveRedundantIncomingEdges(m_State.EndNodes.Select(x => x.Id), ancestorNodesLookup);

            return true;
        }

        #endregion

        #region Private Methods

        // Iterative (was recursive) so a deep dependency chain cannot overflow the
        // stack. A single shared visited set means each node's incoming edges are
        // reduced exactly once: every node removes only its own incoming edges, using
        // the static ancestor lookup, so the operation is independent of visit order
        // and idempotent per node.
        private void RemoveRedundantIncomingEdges(IEnumerable<T> rootNodeIds, IDictionary<T, HashSet<T>> nodeIdAncestorLookup)
        {
            if (nodeIdAncestorLookup is null)
            {
                throw new ArgumentNullException(nameof(nodeIdAncestorLookup));
            }

            var visited = new HashSet<T>();
            var stack = new Stack<T>();
            foreach (T rootNodeId in rootNodeIds)
            {
                stack.Push(rootNodeId);
            }

            while (stack.Count != 0)
            {
                T nodeId = stack.Pop();
                if (!visited.Add(nodeId))
                {
                    continue;
                }

                Node<T, TActivity> node = m_State.Node(nodeId);

                if (node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated)
                {
                    continue;
                }

                // Go through all the incoming edges and collate the
                // ancestors of their tail nodes.
                var tailNodeAncestors = new HashSet<T>(node.IncomingEdges
                    .Select(x => m_State.EdgeTailNode(x).Id)
                    .SelectMany(x => nodeIdAncestorLookup[x]));

                // Go through the incoming edges and remove any that connect
                // directly to any ancestors of the edges' tail nodes.
                // In a vertex graph, all edges are removable.
                foreach (T edgeId in node.IncomingEdges
                    .Select(x => m_State.Edge(x))
                    .Where(x => x.Content.CanBeRemoved)
                    .Select(x => x.Id)
                    .ToList())
                {
                    Node<T, TActivity> tailNode = m_State.EdgeTailNode(edgeId);
                    if (tailNodeAncestors.Contains(tailNode.Id))
                    {
                        // Remove the edge from the tail node.
                        tailNode.OutgoingEdges.Remove(edgeId);
                        m_State.RemoveEdgeTailNode(edgeId);

                        // Remove the edge from the node itself.
                        node.IncomingEdges.Remove(edgeId);
                        m_State.RemoveEdgeHeadNode(edgeId);

                        // Remove the edge completely.
                        m_State.RemoveEdge(edgeId);
                    }
                }

                // Continue with all the remaining incoming edges' tail nodes.
                foreach (T tailNodeId in node.IncomingEdges.Select(x => m_State.EdgeTailNode(x).Id).ToList())
                {
                    stack.Push(tailNodeId);
                }
            }
        }

        #endregion
    }
}
