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

        #endregion

        #region Ctor

        internal VertexTransitiveReducer(
            IVertexStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity> stronglyConnectedComponentsFinder,
            VertexGraphState<T, TResourceId, TWorkStreamId, TActivity> state)
        {
            m_StronglyConnectedComponentsFinder = stronglyConnectedComponentsFinder ?? throw new ArgumentNullException(nameof(stronglyConnectedComponentsFinder));
            m_State = state ?? throw new ArgumentNullException(nameof(state));
        }

        #endregion

        #region ITransitiveReducer

        public Dictionary<T, HashSet<T>> GetAncestorNodesLookup()
        {
            if (!m_State.AllDependenciesSatisfied)
            {
                return null;
            }

            List<ICircularDependency<T>> circularDependencies =
                m_StronglyConnectedComponentsFinder.FindStronglyCircularDependencies(m_State, ignoreDummies: false);

            if (circularDependencies.Count != 0)
            {
                return null;
            }

            var nodeIdAncestorLookup = new Dictionary<T, HashSet<T>>();
            List<T> endNodeIds = m_State.EndNodes.Select(x => x.Id).ToList();

            foreach (T endNodeId in endNodeIds)
            {
                HashSet<T> totalAncestorNodes = GetAncestorNodes(endNodeId, nodeIdAncestorLookup);
                nodeIdAncestorLookup.Add(endNodeId, totalAncestorNodes);
            }

            return nodeIdAncestorLookup;
        }

        public bool ReduceGraph()
        {
            IDictionary<T, HashSet<T>> ancestorNodesLookup = GetAncestorNodesLookup();

            if (ancestorNodesLookup is null)
            {
                return false;
            }

            List<T> endNodeIds = m_State.EndNodes.Select(x => x.Id).ToList();

            foreach (T endNodeId in endNodeIds)
            {
                RemoveRedundantIncomingEdges(endNodeId, ancestorNodesLookup);
            }

            return true;
        }

        #endregion

        #region Private Methods

        private HashSet<T> GetAncestorNodes(T nodeId, IDictionary<T, HashSet<T>> nodeIdAncestorLookup)
        {
            if (nodeIdAncestorLookup is null)
            {
                throw new ArgumentNullException(nameof(nodeIdAncestorLookup));
            }

            Node<T, TActivity> node = m_State.Node(nodeId);
            var totalAncestorNodes = new HashSet<T>();

            if (node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated)
            {
                return totalAncestorNodes;
            }

            foreach (T tailNodeId in node.IncomingEdges.Select(x => m_State.EdgeTailNode(x).Id).ToList())
            {
                totalAncestorNodes.Add(tailNodeId);
                if (!nodeIdAncestorLookup.TryGetValue(tailNodeId, out HashSet<T> tailNodeAncestorNodes))
                {
                    tailNodeAncestorNodes = GetAncestorNodes(tailNodeId, nodeIdAncestorLookup);
                    nodeIdAncestorLookup.Add(tailNodeId, tailNodeAncestorNodes);
                }
                totalAncestorNodes.UnionWith(tailNodeAncestorNodes);
            }

            return totalAncestorNodes;
        }

        private void RemoveRedundantIncomingEdges(T nodeId, IDictionary<T, HashSet<T>> nodeIdAncestorLookup)
        {
            if (nodeIdAncestorLookup is null)
            {
                throw new ArgumentNullException(nameof(nodeIdAncestorLookup));
            }

            Node<T, TActivity> node = m_State.Node(nodeId);

            if (node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated)
            {
                return;
            }

            var tailNodeAncestors = new HashSet<T>(node.IncomingEdges
                .Select(x => m_State.EdgeTailNode(x).Id)
                .SelectMany(x => nodeIdAncestorLookup[x]));

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
                    tailNode.OutgoingEdges.Remove(edgeId);
                    m_State.RemoveEdgeTailNode(edgeId);
                    node.IncomingEdges.Remove(edgeId);
                    m_State.RemoveEdgeHeadNode(edgeId);
                    m_State.RemoveEdge(edgeId);
                }
            }

            foreach (T tailNodeId in node.IncomingEdges.Select(x => m_State.EdgeTailNode(x).Id).ToList())
            {
                RemoveRedundantIncomingEdges(tailNodeId, nodeIdAncestorLookup);
            }
        }

        #endregion
    }
}
