using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    // Implements all dummy-edge operations for Activity-on-Arrow graphs.
    // Holds references (not ownership) to the graph-state dictionaries owned by ArrowGraphBuilder.
    // This allows the orchestrator to be a proper engine without copying graph state.
    internal sealed class DummyEdgeOrchestrator<T, TResourceId, TWorkStreamId, TActivity>
        : IDummyEdgeOrchestrator<T, TResourceId, TWorkStreamId, TActivity>
        where TActivity : class, IActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        #region Fields

        private readonly Func<T> m_EdgeIdGenerator;
        private readonly Func<T, TActivity> m_DummyActivityGenerator;
        private readonly Func<bool> m_AllDependenciesSatisfied;
        private readonly Func<IList<ICircularDependency<T>>> m_FindStrongCircularDependencies;

        // References to builder-owned graph state.
        private readonly Dictionary<T, Edge<T, TActivity>> m_EdgeLookup;
        private readonly Dictionary<T, Node<T, IEvent<T>>> m_NodeLookup;
        private readonly Dictionary<T, Node<T, IEvent<T>>> m_EdgeHeadNodeLookup;
        private readonly Dictionary<T, Node<T, IEvent<T>>> m_EdgeTailNodeLookup;

        // Accessors for Start/End nodes (builder-owned properties).
        private readonly Func<Node<T, IEvent<T>>> m_GetStartNode;
        private readonly Func<Node<T, IEvent<T>>> m_GetEndNode;

        #endregion

        #region Ctor

        internal DummyEdgeOrchestrator(
            Func<T> edgeIdGenerator,
            Func<T, TActivity> dummyActivityGenerator,
            Func<bool> allDependenciesSatisfied,
            Func<IList<ICircularDependency<T>>> findStrongCircularDependencies,
            Dictionary<T, Edge<T, TActivity>> edgeLookup,
            Dictionary<T, Node<T, IEvent<T>>> nodeLookup,
            Dictionary<T, Node<T, IEvent<T>>> edgeHeadNodeLookup,
            Dictionary<T, Node<T, IEvent<T>>> edgeTailNodeLookup,
            Func<Node<T, IEvent<T>>> getStartNode,
            Func<Node<T, IEvent<T>>> getEndNode)
        {
            m_EdgeIdGenerator = edgeIdGenerator ?? throw new ArgumentNullException(nameof(edgeIdGenerator));
            m_DummyActivityGenerator = dummyActivityGenerator ?? throw new ArgumentNullException(nameof(dummyActivityGenerator));
            m_AllDependenciesSatisfied = allDependenciesSatisfied ?? throw new ArgumentNullException(nameof(allDependenciesSatisfied));
            m_FindStrongCircularDependencies = findStrongCircularDependencies ?? throw new ArgumentNullException(nameof(findStrongCircularDependencies));
            m_EdgeLookup = edgeLookup ?? throw new ArgumentNullException(nameof(edgeLookup));
            m_NodeLookup = nodeLookup ?? throw new ArgumentNullException(nameof(nodeLookup));
            m_EdgeHeadNodeLookup = edgeHeadNodeLookup ?? throw new ArgumentNullException(nameof(edgeHeadNodeLookup));
            m_EdgeTailNodeLookup = edgeTailNodeLookup ?? throw new ArgumentNullException(nameof(edgeTailNodeLookup));
            m_GetStartNode = getStartNode ?? throw new ArgumentNullException(nameof(getStartNode));
            m_GetEndNode = getEndNode ?? throw new ArgumentNullException(nameof(getEndNode));
        }

        #endregion

        #region IDummyEdgeOrchestrator

        public void ConnectWithDummyEdge(Node<T, IEvent<T>> tailNode, Node<T, IEvent<T>> headNode)
        {
            T dummyEdgeId = m_EdgeIdGenerator();
            var dummyEdge = new Edge<T, TActivity>(m_DummyActivityGenerator(dummyEdgeId));
            headNode.IncomingEdges.Add(dummyEdgeId);
            m_EdgeHeadNodeLookup.Add(dummyEdgeId, headNode);
            tailNode.OutgoingEdges.Add(dummyEdgeId);
            m_EdgeTailNodeLookup.Add(dummyEdgeId, tailNode);
            m_EdgeLookup.Add(dummyEdge.Id, dummyEdge);
        }

        public bool RemoveDummyActivity(T activityId)
        {
            if (!m_EdgeLookup.TryGetValue(activityId, out Edge<T, TActivity> edge)) return false;
            if (!edge.Content.IsDummy) return false;
            if (!edge.Content.CanBeRemoved) return false;

            Node<T, IEvent<T>> tailNode = m_EdgeTailNodeLookup[activityId];
            Node<T, IEvent<T>> headNode = m_EdgeHeadNodeLookup[activityId];

            if (HaveDescendantOrAncestorOverlap(tailNode, headNode) && !ShareMoreThanOneEdge(tailNode, headNode))
                return false;

            // Remove the edge from the tail node.
            tailNode.OutgoingEdges.Remove(activityId);
            m_EdgeTailNodeLookup.Remove(activityId);

            // Remove the edge from the head node.
            headNode.IncomingEdges.Remove(activityId);
            m_EdgeHeadNodeLookup.Remove(activityId);

            // Remove the edge completely.
            m_EdgeLookup.Remove(activityId);

            if (headNode.NodeType != NodeType.End
                && headNode.NodeType != NodeType.Isolated
                && !headNode.IncomingEdges.Any())
            {
                IList<T> headNodeOutgoingEdgeIds = headNode.OutgoingEdges.ToList();
                foreach (T headNodeOutgoingEdgeId in headNodeOutgoingEdgeIds)
                {
                    bool changeTailSuccess = ChangeEdgeTailNode(headNodeOutgoingEdgeId, tailNode.Id);
                    if (!changeTailSuccess)
                        throw new InvalidOperationException($@"Unable to change tail node of edge {headNodeOutgoingEdgeId} to node {tailNode.Id} when removing dummy activity {activityId}");
                }
            }
            else if (tailNode.NodeType != NodeType.Start
                && tailNode.NodeType != NodeType.Isolated
                && !tailNode.OutgoingEdges.Any())
            {
                IList<T> tailNodeIncomingEdgeIds = tailNode.IncomingEdges.ToList();
                foreach (T tailNodeIncomingEdgeId in tailNodeIncomingEdgeIds)
                {
                    bool changeHeadSuccess = ChangeEdgeHeadNode(tailNodeIncomingEdgeId, headNode.Id);
                    if (!changeHeadSuccess)
                        throw new InvalidOperationException($@"Unable to change head node of edge {tailNodeIncomingEdgeId} to node {headNode.Id} when removing dummy activity {activityId}");
                }
            }
            return true;
        }

        public bool RedirectDummyEdges()
        {
            if (!m_AllDependenciesSatisfied()) return false;
            IList<ICircularDependency<T>> circularDependencies = m_FindStrongCircularDependencies();
            if (circularDependencies.Any()) return false;

            List<Node<T, IEvent<T>>> nodes = m_NodeLookup.Values
                .Where(x => x.NodeType != NodeType.End && x.NodeType != NodeType.Isolated)
                .OrderByDescending(x => x.Content.EarliestFinishTime)
                .ToList();

            foreach (Node<T, IEvent<T>> node in nodes)
            {
                var outgoingDummyEdgeIdLookup = new HashSet<T>(
                    node.OutgoingEdges.Select(x => m_EdgeLookup[x])
                    .Where(x => x.Content.IsDummy && x.Content.CanBeRemoved).Select(x => x.Id));

                IList<Node<T, IEvent<T>>> dummyEdgeSuccessorNodes =
                    outgoingDummyEdgeIdLookup.Select(x => m_EdgeHeadNodeLookup[x]).ToList();

                IList<IEnumerable<T>> dummyEdgeIdsToSuccessorNodes =
                    dummyEdgeSuccessorNodes
                    .Select(x => x.IncomingEdges.Select(y => m_EdgeLookup[y])
                    .Where(y => y.Content.IsDummy && y.Content.CanBeRemoved)
                    .Select(y => y.Id))
                    .ToList();

                if (!dummyEdgeIdsToSuccessorNodes.Any()) continue;

                IList<T> commonDependencyNodes =
                    dummyEdgeIdsToSuccessorNodes.Select(x => x.Select(y => m_EdgeTailNodeLookup[y].Id))
                    .Aggregate((previous, next) => previous.Intersect(next)).ToList();

                var commonDependencyNodeLookup = new HashSet<T>(commonDependencyNodes);

                IList<T> commonDependencyEdgeIds =
                    dummyEdgeIdsToSuccessorNodes.SelectMany(x => x)
                    .Where(x => commonDependencyNodeLookup.Contains(m_EdgeTailNodeLookup[x].Id))
                    .ToList();

                var allSuccessorNodeLookup = new HashSet<T>(node.OutgoingEdges.Select(x => m_EdgeHeadNodeLookup[x].Id));
                var commonSuccessorNodeLookup = new HashSet<T>(commonDependencyEdgeIds.Select(x => m_EdgeHeadNodeLookup[x].Id));

                if (!allSuccessorNodeLookup.IsSubsetOf(commonSuccessorNodeLookup)) continue;

                List<T> commonDependencyEdgeIdsForOriginalNode = commonDependencyEdgeIds
                    .Where(x => !outgoingDummyEdgeIdLookup.Contains(x))
                    .OrderBy(x => x)
                    .ToList();

                foreach (T commonDependencyEdgeId in commonDependencyEdgeIdsForOriginalNode)
                {
                    bool changeHeadSuccess = ChangeEdgeHeadNode(commonDependencyEdgeId, node.Id);
                    if (!changeHeadSuccess)
                        throw new InvalidOperationException($@"Unable to change head node of edge {commonDependencyEdgeId} to node {node.Id} when redirecting dummy activities");
                }

                RemoveParallelIncomingDummyEdges(node);
            }
            return true;
        }

        public bool RemoveRedundantDummyEdges()
        {
            if (!m_AllDependenciesSatisfied()) return false;
            IList<ICircularDependency<T>> circularDependencies = m_FindStrongCircularDependencies();
            if (circularDependencies.Any()) return false;

            foreach (Edge<T, TActivity> edge in GetDummyEdgesInDescendingOrder().Where(x => x.Content.CanBeRemoved))
            {
                Node<T, IEvent<T>> tailNode = m_EdgeTailNodeLookup[edge.Id];
                Node<T, IEvent<T>> headNode = m_EdgeHeadNodeLookup[edge.Id];
                if (tailNode.OutgoingEdges.Count == 1 && headNode.IncomingEdges.Count == 1)
                    RemoveDummyActivity(edge.Id);
            }

            foreach (Edge<T, TActivity> edge in GetDummyEdgesInDescendingOrder().Where(x => x.Content.CanBeRemoved))
            {
                if (m_EdgeHeadNodeLookup[edge.Id].IncomingEdges.Count == 1)
                    RemoveDummyActivity(edge.Id);
            }

            foreach (Edge<T, TActivity> edge in GetDummyEdgesInDescendingOrder().Where(x => x.Content.CanBeRemoved))
            {
                if (m_EdgeTailNodeLookup[edge.Id].OutgoingEdges.Count == 1)
                    RemoveDummyActivity(edge.Id);
            }

            foreach (Node<T, IEvent<T>> node in m_NodeLookup.Values.ToList())
                RemoveParallelIncomingDummyEdges(node);

            return true;
        }

        public void RemoveRedundantIncomingDummyEdges(T nodeId, IDictionary<T, HashSet<T>> nodeIdAncestorLookup)
        {
            if (nodeIdAncestorLookup is null) throw new ArgumentNullException(nameof(nodeIdAncestorLookup));
            Node<T, IEvent<T>> node = m_NodeLookup[nodeId];
            if (node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated) return;

            var tailNodeAncestors = new HashSet<T>(node.IncomingEdges
                .Select(x => m_EdgeTailNodeLookup[x].Id)
                .SelectMany(x => nodeIdAncestorLookup[x]));

            List<T> incomingDummyEdges = node.IncomingEdges
                .Select(x => m_EdgeLookup[x])
                .Where(x => x.Content.IsDummy && x.Content.CanBeRemoved)
                .Select(x => x.Id)
                .ToList();

            foreach (T dummyEdgeId in incomingDummyEdges)
            {
                T dummyEdgeTailNodeId = m_EdgeTailNodeLookup[dummyEdgeId].Id;
                if (tailNodeAncestors.Contains(dummyEdgeTailNodeId))
                    RemoveDummyActivity(dummyEdgeId);
            }

            List<T> remainingIncomingEdges = node.IncomingEdges
                .Select(x => m_EdgeTailNodeLookup[x].Id)
                .ToList();

            foreach (T tailNodeId in remainingIncomingEdges)
                RemoveRedundantIncomingDummyEdges(tailNodeId, nodeIdAncestorLookup);
        }

        public IList<Edge<T, TActivity>> GetDummyEdgesInDescendingOrder()
        {
            var recordedEdges = new HashSet<T>();
            var edgesInDescendingOrder = new List<Edge<T, TActivity>>();
            GetEdgesInDescendingOrder(m_GetStartNode().Id, edgesInDescendingOrder, recordedEdges);
            return edgesInDescendingOrder.Where(x => x.Content.IsDummy).ToList();
        }

        #endregion

        #region Private Methods

        private void GetEdgesInDescendingOrder(T nodeId, IList<Edge<T, TActivity>> edgesInDescendingOrder, HashSet<T> recordedEdges)
        {
            if (edgesInDescendingOrder is null) throw new ArgumentNullException(nameof(edgesInDescendingOrder));
            if (recordedEdges is null) throw new ArgumentNullException(nameof(recordedEdges));
            Node<T, IEvent<T>> node = m_NodeLookup[nodeId];
            if (node.NodeType == NodeType.End || node.NodeType == NodeType.Isolated) return;

            foreach (Edge<T, TActivity> outgoingEdge in node.OutgoingEdges.Select(x => m_EdgeLookup[x]))
            {
                if (!recordedEdges.Contains(outgoingEdge.Id))
                {
                    edgesInDescendingOrder.Add(outgoingEdge);
                    recordedEdges.Add(outgoingEdge.Id);
                }
                GetEdgesInDescendingOrder(m_EdgeHeadNodeLookup[outgoingEdge.Id].Id, edgesInDescendingOrder, recordedEdges);
            }
        }

        private bool HaveDescendantOrAncestorOverlap(Node<T, IEvent<T>> tailNode, Node<T, IEvent<T>> headNode)
        {
            if (tailNode is null) throw new ArgumentNullException(nameof(tailNode));
            if (headNode is null) throw new ArgumentNullException(nameof(headNode));

            var tailNeighbours = new HashSet<T>();
            if (tailNode.NodeType != NodeType.End && tailNode.NodeType != NodeType.Isolated)
                tailNeighbours.UnionWith(tailNode.OutgoingEdges.Select(x => m_EdgeHeadNodeLookup[x].Id).Except(new[] { headNode.Id }));
            if (tailNode.NodeType != NodeType.Start && tailNode.NodeType != NodeType.Isolated)
                tailNeighbours.UnionWith(tailNode.IncomingEdges.Select(x => m_EdgeTailNodeLookup[x].Id).Except(new[] { headNode.Id }));

            var headNeighbours = new HashSet<T>();
            if (headNode.NodeType != NodeType.Start && headNode.NodeType != NodeType.Isolated)
                headNeighbours.UnionWith(headNode.IncomingEdges.Select(x => m_EdgeTailNodeLookup[x].Id).Except(new[] { tailNode.Id }));
            if (headNode.NodeType != NodeType.End && headNode.NodeType != NodeType.Isolated)
                headNeighbours.UnionWith(headNode.OutgoingEdges.Select(x => m_EdgeHeadNodeLookup[x].Id).Except(new[] { tailNode.Id }));

            return tailNeighbours.Overlaps(headNeighbours);
        }

        private bool ShareMoreThanOneEdge(Node<T, IEvent<T>> tailNode, Node<T, IEvent<T>> headNode)
        {
            if (tailNode is null) throw new ArgumentNullException(nameof(tailNode));
            if (headNode is null) throw new ArgumentNullException(nameof(headNode));

            var tailOutgoing = tailNode.NodeType != NodeType.End && tailNode.NodeType != NodeType.Isolated
                ? new HashSet<T>(tailNode.OutgoingEdges) : new HashSet<T>();
            var headIncoming = headNode.NodeType != NodeType.Start && headNode.NodeType != NodeType.Isolated
                ? new HashSet<T>(headNode.IncomingEdges) : new HashSet<T>();

            return tailOutgoing.Intersect(headIncoming).Count() > 1;
        }

        private void RemoveParallelIncomingDummyEdges(Node<T, IEvent<T>> node)
        {
            if (node is null) throw new ArgumentNullException(nameof(node));
            if (node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated) return;

            var tailNodeParallelDummyEdgesLookup = new Dictionary<T, HashSet<T>>();
            IEnumerable<T> removableIncomingDummyEdgeIds = node.IncomingEdges
                .Select(x => m_EdgeLookup[x])
                .Where(x => x.Content.IsDummy && x.Content.CanBeRemoved)
                .Select(x => x.Id)
                .ToList();

            foreach (T incomingDummyEdgeId in removableIncomingDummyEdgeIds)
            {
                T tailNodeId = m_EdgeTailNodeLookup[incomingDummyEdgeId].Id;
                if (!tailNodeParallelDummyEdgesLookup.TryGetValue(tailNodeId, out HashSet<T> dummyEdgeIds))
                {
                    dummyEdgeIds = new HashSet<T>();
                    tailNodeParallelDummyEdgesLookup.Add(tailNodeId, dummyEdgeIds);
                }
                dummyEdgeIds.Add(incomingDummyEdgeId);
            }

            IList<T> setsOfMoreThanOneDummyEdge = tailNodeParallelDummyEdgesLookup
                .Where(x => x.Value.Count > 1).Select(x => x.Key).ToList();

            foreach (T tailNodeId in setsOfMoreThanOneDummyEdge)
            {
                IList<T> dummyEdgeIds = tailNodeParallelDummyEdgesLookup[tailNodeId].ToList();
                int length = dummyEdgeIds.Count;
                for (int i = 1; i < length; i++)
                    RemoveDummyActivity(dummyEdgeIds[i]);
            }
        }

        private bool ChangeEdgeTailNodeWithoutCleanup(T edgeId, T newTailNodeId)
        {
            if (!m_AllDependenciesSatisfied()) return false;
            if (!m_EdgeLookup.TryGetValue(edgeId, out Edge<T, TActivity> _)) return false;
            if (!m_NodeLookup.TryGetValue(newTailNodeId, out Node<T, IEvent<T>> newTailNode)) return false;

            Node<T, IEvent<T>> oldTailNode = m_EdgeTailNodeLookup[edgeId];
            oldTailNode.OutgoingEdges.Remove(edgeId);
            m_EdgeTailNodeLookup.Remove(edgeId);
            newTailNode.OutgoingEdges.Add(edgeId);
            m_EdgeTailNodeLookup.Add(edgeId, newTailNode);
            return true;
        }

        private bool ChangeEdgeHeadNodeWithoutCleanup(T edgeId, T newHeadNodeId)
        {
            if (!m_AllDependenciesSatisfied()) return false;
            if (!m_EdgeLookup.TryGetValue(edgeId, out Edge<T, TActivity> _)) return false;
            if (!m_NodeLookup.TryGetValue(newHeadNodeId, out Node<T, IEvent<T>> newHeadNode)) return false;

            Node<T, IEvent<T>> currentHeadNode = m_EdgeHeadNodeLookup[edgeId];
            currentHeadNode.IncomingEdges.Remove(edgeId);
            m_EdgeHeadNodeLookup.Remove(edgeId);
            newHeadNode.IncomingEdges.Add(edgeId);
            m_EdgeHeadNodeLookup.Add(edgeId, newHeadNode);
            return true;
        }

        private bool ChangeEdgeTailNode(T edgeId, T newTailNodeId)
        {
            if (!m_AllDependenciesSatisfied()) return false;

            Node<T, IEvent<T>> oldTailNode = m_EdgeTailNodeLookup[edgeId];
            bool changeTailSuccess = ChangeEdgeTailNodeWithoutCleanup(edgeId, newTailNodeId);
            if (!changeTailSuccess)
                throw new InvalidOperationException($@"Unable to change tail node of edge {edgeId} to node {newTailNodeId} without cleanup");

            IList<T> oldTailNodeOutgoingEdgeIds = oldTailNode.OutgoingEdges.ToList();
            if (!oldTailNodeOutgoingEdgeIds.Any())
            {
                Node<T, IEvent<T>> headNode = m_EdgeHeadNodeLookup[edgeId];
                IList<T> oldTailNodeIncomingEdgeIds = oldTailNode.IncomingEdges.ToList();
                foreach (T oldTailNodeIncomingEdgeId in oldTailNodeIncomingEdgeIds)
                {
                    bool changeHeadSuccess = ChangeEdgeHeadNodeWithoutCleanup(oldTailNodeIncomingEdgeId, headNode.Id);
                    if (!changeHeadSuccess)
                        throw new InvalidOperationException($@"Unable to change head node of edge {oldTailNodeIncomingEdgeId} to node {headNode.Id} without cleanup");
                }
            }

            if (oldTailNode.NodeType != NodeType.Start
                && oldTailNode.NodeType != NodeType.Isolated
                && !oldTailNode.IncomingEdges.Any()
                && !oldTailNode.OutgoingEdges.Any())
            {
                m_NodeLookup.Remove(oldTailNode.Id);
            }
            return true;
        }

        private bool ChangeEdgeHeadNode(T edgeId, T newHeadNodeId)
        {
            if (!m_AllDependenciesSatisfied()) return false;

            Node<T, IEvent<T>> oldHeadNode = m_EdgeHeadNodeLookup[edgeId];
            bool changeHeadSuccess = ChangeEdgeHeadNodeWithoutCleanup(edgeId, newHeadNodeId);
            if (!changeHeadSuccess)
                throw new InvalidOperationException($@"Unable to change head node of edge {edgeId} to node {newHeadNodeId} without cleanup");

            IList<T> oldHeadNodeIncomingEdgeIds = oldHeadNode.IncomingEdges.ToList();
            if (!oldHeadNodeIncomingEdgeIds.Any())
            {
                Node<T, IEvent<T>> tailNode = m_EdgeTailNodeLookup[edgeId];
                IList<T> oldHeadNodeOutgoingEdgeIds = oldHeadNode.OutgoingEdges.ToList();
                foreach (T oldHeadNodeOutgoingEdgeId in oldHeadNodeOutgoingEdgeIds)
                {
                    bool changeTailSuccess = ChangeEdgeTailNodeWithoutCleanup(oldHeadNodeOutgoingEdgeId, tailNode.Id);
                    if (!changeTailSuccess)
                        throw new InvalidOperationException($@"Unable to change tail node of edge {oldHeadNodeOutgoingEdgeId} to node {tailNode.Id} without cleanup");
                }
            }

            if (oldHeadNode.NodeType != NodeType.End
                && oldHeadNode.NodeType != NodeType.Isolated
                && !oldHeadNode.IncomingEdges.Any()
                && !oldHeadNode.OutgoingEdges.Any())
            {
                m_NodeLookup.Remove(oldHeadNode.Id);
            }
            return true;
        }

        #endregion
    }
}
