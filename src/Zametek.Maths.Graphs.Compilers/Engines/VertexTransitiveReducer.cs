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

        private readonly Func<IList<ICircularDependency<T>>> m_FindStrongCircularDependencies;
        private readonly Func<IEnumerable<T>> m_GetEndNodeIds;
        private readonly VertexGraphState<T, TResourceId, TWorkStreamId, TActivity> m_State;

        #endregion

        #region Ctor

        internal VertexTransitiveReducer(
            Func<IList<ICircularDependency<T>>> findStrongCircularDependencies,
            Func<IEnumerable<T>> getEndNodeIds,
            VertexGraphState<T, TResourceId, TWorkStreamId, TActivity> state)
        {
            m_FindStrongCircularDependencies = findStrongCircularDependencies ?? throw new ArgumentNullException(nameof(findStrongCircularDependencies));
            m_GetEndNodeIds = getEndNodeIds ?? throw new ArgumentNullException(nameof(getEndNodeIds));
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
            IList<ICircularDependency<T>> circularDependencies = m_FindStrongCircularDependencies();
            if (circularDependencies.Any())
            {
                return null;
            }
            var nodeIdAncestorLookup = new Dictionary<T, HashSet<T>>();
            foreach (T endNodeId in m_GetEndNodeIds())
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
            foreach (T endNodeId in m_GetEndNodeIds())
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
