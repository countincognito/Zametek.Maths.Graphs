using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    // Transitive reducer for Activity-on-Arrow graphs.
    // Computes the ancestor-node lookup and delegates dummy-edge removal to the
    // IDummyEdgeOrchestrator — only dummy edges are reduced in arrow graphs.
    // Holds references (not ownership) to the graph-state dictionaries owned by ArrowGraphBuilder.
    internal sealed class ArrowTransitiveReducer<T, TResourceId, TWorkStreamId, TActivity>
        : ITransitiveReducer<T>
        where TActivity : class, IActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        #region Fields

        private readonly Func<bool> m_AllDependenciesSatisfied;
        private readonly Func<IList<ICircularDependency<T>>> m_FindStrongCircularDependencies;
        private readonly Func<IEnumerable<T>> m_GetEndNodeIds;
        private readonly IDummyEdgeOrchestrator<T, TResourceId, TWorkStreamId, TActivity> m_DummyEdgeOrchestrator;

        // References to builder-owned graph state.
        private readonly Dictionary<T, Node<T, IEvent<T>>> m_NodeLookup;
        private readonly Dictionary<T, Node<T, IEvent<T>>> m_EdgeTailNodeLookup;

        #endregion

        #region Ctor

        internal ArrowTransitiveReducer(
            Func<bool> allDependenciesSatisfied,
            Func<IList<ICircularDependency<T>>> findStrongCircularDependencies,
            Func<IEnumerable<T>> getEndNodeIds,
            IDummyEdgeOrchestrator<T, TResourceId, TWorkStreamId, TActivity> dummyEdgeOrchestrator,
            Dictionary<T, Node<T, IEvent<T>>> nodeLookup,
            Dictionary<T, Node<T, IEvent<T>>> edgeTailNodeLookup)
        {
            m_AllDependenciesSatisfied = allDependenciesSatisfied ?? throw new ArgumentNullException(nameof(allDependenciesSatisfied));
            m_FindStrongCircularDependencies = findStrongCircularDependencies ?? throw new ArgumentNullException(nameof(findStrongCircularDependencies));
            m_GetEndNodeIds = getEndNodeIds ?? throw new ArgumentNullException(nameof(getEndNodeIds));
            m_DummyEdgeOrchestrator = dummyEdgeOrchestrator ?? throw new ArgumentNullException(nameof(dummyEdgeOrchestrator));
            m_NodeLookup = nodeLookup ?? throw new ArgumentNullException(nameof(nodeLookup));
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
                m_DummyEdgeOrchestrator.RemoveRedundantIncomingDummyEdges(endNodeId, ancestorNodesLookup);
            return true;
        }

        #endregion

        #region Private Methods

        private HashSet<T> GetAncestorNodes(T nodeId, IDictionary<T, HashSet<T>> nodeIdAncestorLookup)
        {
            if (nodeIdAncestorLookup is null) throw new ArgumentNullException(nameof(nodeIdAncestorLookup));
            Node<T, IEvent<T>> node = m_NodeLookup[nodeId];
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

        #endregion
    }
}
