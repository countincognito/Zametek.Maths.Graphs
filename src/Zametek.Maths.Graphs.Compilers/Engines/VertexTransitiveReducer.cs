using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    // Transitive reducer for Activity-on-Vertex graphs.
    // Holds references (not ownership) to the graph-state dictionaries owned by VertexGraphBuilder.
    internal sealed class VertexTransitiveReducer<T, TResourceId, TWorkStreamId, TActivity>
        : ITransitiveReducer<T>
        where TActivity : IActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        #region Fields

        private readonly Func<bool> m_AllDependenciesSatisfied;
        private readonly Func<IList<ICircularDependency<T>>> m_FindStrongCircularDependencies;
        private readonly Func<IEnumerable<T>> m_GetEndNodeIds;

        // References to builder-owned graph state.
        private readonly Dictionary<T, Edge<T, IEvent<T>>> m_EdgeLookup;
        private readonly Dictionary<T, Node<T, TActivity>> m_NodeLookup;
        private readonly Dictionary<T, Node<T, TActivity>> m_EdgeHeadNodeLookup;
        private readonly Dictionary<T, Node<T, TActivity>> m_EdgeTailNodeLookup;

        #endregion

        #region Ctor

        internal VertexTransitiveReducer(
            Func<bool> allDependenciesSatisfied,
            Func<IList<ICircularDependency<T>>> findStrongCircularDependencies,
            Func<IEnumerable<T>> getEndNodeIds,
            Dictionary<T, Edge<T, IEvent<T>>> edgeLookup,
            Dictionary<T, Node<T, TActivity>> nodeLookup,
            Dictionary<T, Node<T, TActivity>> edgeHeadNodeLookup,
            Dictionary<T, Node<T, TActivity>> edgeTailNodeLookup)
        {
            m_AllDependenciesSatisfied = allDependenciesSatisfied ?? throw new ArgumentNullException(nameof(allDependenciesSatisfied));
            m_FindStrongCircularDependencies = findStrongCircularDependencies ?? throw new ArgumentNullException(nameof(findStrongCircularDependencies));
            m_GetEndNodeIds = getEndNodeIds ?? throw new ArgumentNullException(nameof(getEndNodeIds));
            m_EdgeLookup = edgeLookup ?? throw new ArgumentNullException(nameof(edgeLookup));
            m_NodeLookup = nodeLookup ?? throw new ArgumentNullException(nameof(nodeLookup));
            m_EdgeHeadNodeLookup = edgeHeadNodeLookup ?? throw new ArgumentNullException(nameof(edgeHeadNodeLookup));
            m_EdgeTailNodeLookup = edgeTailNodeLookup ?? throw new ArgumentNullException(nameof(edgeTailNodeLookup));
        }

        #endregion

        #region ITransitiveReducer

        public IDictionary<T, HashSet<T>> GetAncestorNodesLookup()
        {
            if (!m_AllDependenciesSatisfied()) return null;
            IList<ICircularDependency<T>> circularDependencies = m_FindStrongCircularDependencies();
            if (circularDependencies.Any()) return null;
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
            if (ancestorNodesLookup is null) return false;
            foreach (T endNodeId in m_GetEndNodeIds())
                RemoveRedundantIncomingEdges(endNodeId, ancestorNodesLookup);
            return true;
        }

        #endregion

        #region Private Methods

        private HashSet<T> GetAncestorNodes(T nodeId, IDictionary<T, HashSet<T>> nodeIdAncestorLookup)
        {
            if (nodeIdAncestorLookup is null) throw new ArgumentNullException(nameof(nodeIdAncestorLookup));
            Node<T, TActivity> node = m_NodeLookup[nodeId];
            var totalAncestorNodes = new HashSet<T>();
            if (node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated)
                return totalAncestorNodes;

            foreach (T tailNodeId in node.IncomingEdges.Select(x => m_EdgeTailNodeLookup[x].Id).ToList())
            {
                if (!totalAncestorNodes.Contains(tailNodeId)) totalAncestorNodes.Add(tailNodeId);
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
            if (nodeIdAncestorLookup is null) throw new ArgumentNullException(nameof(nodeIdAncestorLookup));
            Node<T, TActivity> node = m_NodeLookup[nodeId];
            if (node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated) return;

            var tailNodeAncestors = new HashSet<T>(node.IncomingEdges
                .Select(x => m_EdgeTailNodeLookup[x].Id)
                .SelectMany(x => nodeIdAncestorLookup[x]));

            // In a vertex graph, all edges are removable.
            foreach (T edgeId in node.IncomingEdges
                .Select(x => m_EdgeLookup[x])
                .Where(x => x.Content.CanBeRemoved)
                .Select(x => x.Id)
                .ToList())
            {
                Node<T, TActivity> tailNode = m_EdgeTailNodeLookup[edgeId];
                if (tailNodeAncestors.Contains(tailNode.Id))
                {
                    tailNode.OutgoingEdges.Remove(edgeId);
                    m_EdgeTailNodeLookup.Remove(edgeId);
                    node.IncomingEdges.Remove(edgeId);
                    m_EdgeHeadNodeLookup.Remove(edgeId);
                    m_EdgeLookup.Remove(edgeId);
                }
            }

            foreach (T tailNodeId in node.IncomingEdges.Select(x => m_EdgeTailNodeLookup[x].Id).ToList())
                RemoveRedundantIncomingEdges(tailNodeId, nodeIdAncestorLookup);
        }

        #endregion
    }
}
