using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    // Transitive reducer for Activity-on-Arrow graphs.
    // Computes the ancestor-node lookup and delegates dummy-edge removal to the
    // IDummyEdgeOrchestrator — only dummy edges are reduced in arrow graphs.
    // Operates on the shared ArrowGraphState.
    internal sealed class ArrowTransitiveReducer<T, TResourceId, TWorkStreamId, TActivity>
        : ITransitiveReducer<T>
        where TActivity : class, IActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        #region Fields

        private readonly Func<IList<ICircularDependency<T>>> m_FindStrongCircularDependencies;
        private readonly Func<IEnumerable<T>> m_GetEndNodeIds;
        private readonly IDummyEdgeOrchestrator<T, TResourceId, TWorkStreamId, TActivity> m_DummyEdgeOrchestrator;
        private readonly ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> m_State;

        #endregion

        #region Ctor

        internal ArrowTransitiveReducer(
            Func<List<ICircularDependency<T>>> findStrongCircularDependencies,
            Func<List<T>> getEndNodeIds,
            IDummyEdgeOrchestrator<T, TResourceId, TWorkStreamId, TActivity> dummyEdgeOrchestrator,
            ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state)
        {
            m_FindStrongCircularDependencies = findStrongCircularDependencies ?? throw new ArgumentNullException(nameof(findStrongCircularDependencies));
            m_GetEndNodeIds = getEndNodeIds ?? throw new ArgumentNullException(nameof(getEndNodeIds));
            m_DummyEdgeOrchestrator = dummyEdgeOrchestrator ?? throw new ArgumentNullException(nameof(dummyEdgeOrchestrator));
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
            Dictionary<T, HashSet<T>> ancestorNodesLookup = GetAncestorNodesLookup();
            if (ancestorNodesLookup is null)
            {
                return false;
            }
            foreach (T endNodeId in m_GetEndNodeIds())
            {
                m_DummyEdgeOrchestrator.RemoveRedundantIncomingDummyEdges(endNodeId, ancestorNodesLookup);
            }
            return true;
        }

        #endregion

        #region Private Methods

        private HashSet<T> GetAncestorNodes(T nodeId, Dictionary<T, HashSet<T>> nodeIdAncestorLookup)
        {
            if (nodeIdAncestorLookup is null)
            {
                throw new ArgumentNullException(nameof(nodeIdAncestorLookup));
            }
            Node<T, IEvent<T>> node = m_State.Node(nodeId);
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

        #endregion
    }
}
