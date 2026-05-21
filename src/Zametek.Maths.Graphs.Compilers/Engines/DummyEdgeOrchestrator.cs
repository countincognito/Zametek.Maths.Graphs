using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    // Implements all dummy-edge operations for Activity-on-Arrow graphs.
    // Operates on the shared ArrowGraphState supplied at construction time — the
    // orchestrator owns no graph state of its own.
    internal sealed class DummyEdgeOrchestrator<T, TResourceId, TWorkStreamId, TActivity>
        : IDummyEdgeOrchestrator<T, TResourceId, TWorkStreamId, TActivity>
        where TActivity : class, IActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        #region Fields

        private readonly IIdGenerator<T> m_EdgeIdGenerator;
        private readonly IActivityGenerator<T, TResourceId, TWorkStreamId, TActivity> m_DummyActivityGenerator;
        private readonly Func<IList<ICircularDependency<T>>> m_FindStrongCircularDependencies;
        private readonly ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> m_State;

        #endregion

        #region Ctor

        internal DummyEdgeOrchestrator(
            IIdGenerator<T> edgeIdGenerator,
            IActivityGenerator<T, TResourceId, TWorkStreamId, TActivity> dummyActivityGenerator,
            Func<List<ICircularDependency<T>>> findStrongCircularDependencies,
            ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state)
        {
            m_EdgeIdGenerator = edgeIdGenerator ?? throw new ArgumentNullException(nameof(edgeIdGenerator));
            m_DummyActivityGenerator = dummyActivityGenerator ?? throw new ArgumentNullException(nameof(dummyActivityGenerator));
            m_FindStrongCircularDependencies = findStrongCircularDependencies ?? throw new ArgumentNullException(nameof(findStrongCircularDependencies));
            m_State = state ?? throw new ArgumentNullException(nameof(state));
        }

        #endregion

        #region IDummyEdgeOrchestrator

        public void ConnectWithDummyEdge(Node<T, IEvent<T>> tailNode, Node<T, IEvent<T>> headNode)
        {
            T dummyEdgeId = m_EdgeIdGenerator.Generate();
            var dummyEdge = new Edge<T, TActivity>(m_DummyActivityGenerator.Generate(dummyEdgeId));
            headNode.IncomingEdges.Add(dummyEdgeId);
            m_State.SetEdgeHeadNode(dummyEdgeId, headNode);
            tailNode.OutgoingEdges.Add(dummyEdgeId);
            m_State.SetEdgeTailNode(dummyEdgeId, tailNode);
            m_State.AddEdge(dummyEdge);
        }

        public bool RemoveDummyActivity(T activityId)
        {
            if (!m_State.TryGetEdge(activityId, out Edge<T, TActivity> edge))
            {
                return false;
            }
            if (!edge.Content.IsDummy)
            {
                return false;
            }
            if (!edge.Content.CanBeRemoved)
            {
                return false;
            }

            Node<T, IEvent<T>> tailNode = m_State.EdgeTailNode(activityId);
            Node<T, IEvent<T>> headNode = m_State.EdgeHeadNode(activityId);

            if (HaveDescendantOrAncestorOverlap(tailNode, headNode) && !ShareMoreThanOneEdge(tailNode, headNode))
            {
                return false;
            }

            // Remove the edge from the tail node.
            tailNode.OutgoingEdges.Remove(activityId);
            m_State.RemoveEdgeTailNode(activityId);

            // Remove the edge from the head node.
            headNode.IncomingEdges.Remove(activityId);
            m_State.RemoveEdgeHeadNode(activityId);

            // Remove the edge completely.
            m_State.RemoveEdge(activityId);

            if (headNode.NodeType != NodeType.End
                && headNode.NodeType != NodeType.Isolated
                && headNode.IncomingEdges.Count == 0)
            {
                IList<T> headNodeOutgoingEdgeIds = headNode.OutgoingEdges.ToList();
                foreach (T headNodeOutgoingEdgeId in headNodeOutgoingEdgeIds)
                {
                    bool changeTailSuccess = ChangeEdgeTailNode(headNodeOutgoingEdgeId, tailNode.Id);
                    if (!changeTailSuccess)
                    {
                        throw new InvalidOperationException($@"Unable to change tail node of edge {headNodeOutgoingEdgeId} to node {tailNode.Id} when removing dummy activity {activityId}");
                    }
                }
            }
            else if (tailNode.NodeType != NodeType.Start
                && tailNode.NodeType != NodeType.Isolated
                && tailNode.OutgoingEdges.Count == 0)
            {
                IList<T> tailNodeIncomingEdgeIds = tailNode.IncomingEdges.ToList();
                foreach (T tailNodeIncomingEdgeId in tailNodeIncomingEdgeIds)
                {
                    bool changeHeadSuccess = ChangeEdgeHeadNode(tailNodeIncomingEdgeId, headNode.Id);
                    if (!changeHeadSuccess)
                    {
                        throw new InvalidOperationException($@"Unable to change head node of edge {tailNodeIncomingEdgeId} to node {headNode.Id} when removing dummy activity {activityId}");
                    }
                }
            }
            return true;
        }

        public bool RedirectDummyEdges()
        {
            if (!m_State.AllDependenciesSatisfied)
            {
                return false;
            }
            IList<ICircularDependency<T>> circularDependencies = m_FindStrongCircularDependencies();
            if (circularDependencies.Any())
            {
                return false;
            }

            List<Node<T, IEvent<T>>> nodes = m_State.Nodes
                .Where(x => x.NodeType != NodeType.End && x.NodeType != NodeType.Isolated)
                .OrderByDescending(x => x.Content.EarliestFinishTime)
                .ToList();

            foreach (Node<T, IEvent<T>> node in nodes)
            {
                var outgoingDummyEdgeIdLookup = new HashSet<T>(
                    node.OutgoingEdges.Select(x => m_State.Edge(x))
                    .Where(x => x.Content.IsDummy && x.Content.CanBeRemoved).Select(x => x.Id));

                IList<Node<T, IEvent<T>>> dummyEdgeSuccessorNodes =
                    outgoingDummyEdgeIdLookup.Select(x => m_State.EdgeHeadNode(x)).ToList();

                IList<IEnumerable<T>> dummyEdgeIdsToSuccessorNodes =
                    dummyEdgeSuccessorNodes
                    .Select(x => x.IncomingEdges.Select(y => m_State.Edge(y))
                    .Where(y => y.Content.IsDummy && y.Content.CanBeRemoved)
                    .Select(y => y.Id))
                    .ToList();

                if (!dummyEdgeIdsToSuccessorNodes.Any())
                {
                    continue;
                }

                IList<T> commonDependencyNodes =
                    dummyEdgeIdsToSuccessorNodes.Select(x => x.Select(y => m_State.EdgeTailNode(y).Id))
                    .Aggregate((previous, next) => previous.Intersect(next)).ToList();

                var commonDependencyNodeLookup = new HashSet<T>(commonDependencyNodes);

                IList<T> commonDependencyEdgeIds =
                    dummyEdgeIdsToSuccessorNodes.SelectMany(x => x)
                    .Where(x => commonDependencyNodeLookup.Contains(m_State.EdgeTailNode(x).Id))
                    .ToList();

                var allSuccessorNodeLookup = new HashSet<T>(node.OutgoingEdges.Select(x => m_State.EdgeHeadNode(x).Id));
                var commonSuccessorNodeLookup = new HashSet<T>(commonDependencyEdgeIds.Select(x => m_State.EdgeHeadNode(x).Id));

                if (!allSuccessorNodeLookup.IsSubsetOf(commonSuccessorNodeLookup))
                {
                    continue;
                }

                List<T> commonDependencyEdgeIdsForOriginalNode = commonDependencyEdgeIds
                    .Where(x => !outgoingDummyEdgeIdLookup.Contains(x))
                    .OrderBy(x => x)
                    .ToList();

                foreach (T commonDependencyEdgeId in commonDependencyEdgeIdsForOriginalNode)
                {
                    bool changeHeadSuccess = ChangeEdgeHeadNode(commonDependencyEdgeId, node.Id);
                    if (!changeHeadSuccess)
                    {
                        throw new InvalidOperationException($@"Unable to change head node of edge {commonDependencyEdgeId} to node {node.Id} when redirecting dummy activities");
                    }
                }

                RemoveParallelIncomingDummyEdges(node);
            }
            return true;
        }

        public bool RemoveRedundantDummyEdges()
        {
            if (!m_State.AllDependenciesSatisfied)
            {
                return false;
            }
            IList<ICircularDependency<T>> circularDependencies = m_FindStrongCircularDependencies();
            if (circularDependencies.Any())
            {
                return false;
            }

            foreach (Edge<T, TActivity> edge in GetDummyEdgesInDescendingOrder().Where(x => x.Content.CanBeRemoved))
            {
                Node<T, IEvent<T>> tailNode = m_State.EdgeTailNode(edge.Id);
                Node<T, IEvent<T>> headNode = m_State.EdgeHeadNode(edge.Id);
                if (tailNode.OutgoingEdges.Count == 1 && headNode.IncomingEdges.Count == 1)
                {
                    RemoveDummyActivity(edge.Id);
                }
            }

            foreach (Edge<T, TActivity> edge in GetDummyEdgesInDescendingOrder().Where(x => x.Content.CanBeRemoved))
            {
                if (m_State.EdgeHeadNode(edge.Id).IncomingEdges.Count == 1)
                {
                    RemoveDummyActivity(edge.Id);
                }
            }

            foreach (Edge<T, TActivity> edge in GetDummyEdgesInDescendingOrder().Where(x => x.Content.CanBeRemoved))
            {
                if (m_State.EdgeTailNode(edge.Id).OutgoingEdges.Count == 1)
                {
                    RemoveDummyActivity(edge.Id);
                }
            }

            foreach (Node<T, IEvent<T>> node in m_State.Nodes.ToList())
            {
                RemoveParallelIncomingDummyEdges(node);
            }

            return true;
        }

        public void RemoveRedundantIncomingDummyEdges(T nodeId, Dictionary<T, HashSet<T>> nodeIdAncestorLookup)
        {
            if (nodeIdAncestorLookup is null)
            {
                throw new ArgumentNullException(nameof(nodeIdAncestorLookup));
            }
            Node<T, IEvent<T>> node = m_State.Node(nodeId);
            if (node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated)
            {
                return;
            }

            var tailNodeAncestors = new HashSet<T>(node.IncomingEdges
                .Select(x => m_State.EdgeTailNode(x).Id)
                .SelectMany(x => nodeIdAncestorLookup[x]));

            List<T> incomingDummyEdges = node.IncomingEdges
                .Select(x => m_State.Edge(x))
                .Where(x => x.Content.IsDummy && x.Content.CanBeRemoved)
                .Select(x => x.Id)
                .ToList();

            foreach (T dummyEdgeId in incomingDummyEdges)
            {
                T dummyEdgeTailNodeId = m_State.EdgeTailNode(dummyEdgeId).Id;
                if (tailNodeAncestors.Contains(dummyEdgeTailNodeId))
                {
                    RemoveDummyActivity(dummyEdgeId);
                }
            }

            List<T> remainingIncomingEdges = node.IncomingEdges
                .Select(x => m_State.EdgeTailNode(x).Id)
                .ToList();

            foreach (T tailNodeId in remainingIncomingEdges)
            {
                RemoveRedundantIncomingDummyEdges(tailNodeId, nodeIdAncestorLookup);
            }
        }

        public List<Edge<T, TActivity>> GetDummyEdgesInDescendingOrder()
        {
            var recordedEdges = new HashSet<T>();
            var edgesInDescendingOrder = new List<Edge<T, TActivity>>();
            GetEdgesInDescendingOrder(m_State.StartNode.Id, edgesInDescendingOrder, recordedEdges);
            return edgesInDescendingOrder.Where(x => x.Content.IsDummy).ToList();
        }

        #endregion

        #region Private Methods

        private void GetEdgesInDescendingOrder(T nodeId, List<Edge<T, TActivity>> edgesInDescendingOrder, HashSet<T> recordedEdges)
        {
            if (edgesInDescendingOrder is null)
            {
                throw new ArgumentNullException(nameof(edgesInDescendingOrder));
            }
            if (recordedEdges is null)
            {
                throw new ArgumentNullException(nameof(recordedEdges));
            }
            Node<T, IEvent<T>> node = m_State.Node(nodeId);
            if (node.NodeType == NodeType.End || node.NodeType == NodeType.Isolated)
            {
                return;
            }

            foreach (Edge<T, TActivity> outgoingEdge in node.OutgoingEdges.Select(x => m_State.Edge(x)))
            {
                if (!recordedEdges.Contains(outgoingEdge.Id))
                {
                    edgesInDescendingOrder.Add(outgoingEdge);
                    recordedEdges.Add(outgoingEdge.Id);
                }
                GetEdgesInDescendingOrder(m_State.EdgeHeadNode(outgoingEdge.Id).Id, edgesInDescendingOrder, recordedEdges);
            }
        }

        private bool HaveDescendantOrAncestorOverlap(Node<T, IEvent<T>> tailNode, Node<T, IEvent<T>> headNode)
        {
            if (tailNode is null)
            {
                throw new ArgumentNullException(nameof(tailNode));
            }
            if (headNode is null)
            {
                throw new ArgumentNullException(nameof(headNode));
            }

            var tailNeighbours = new HashSet<T>();
            if (tailNode.NodeType != NodeType.End && tailNode.NodeType != NodeType.Isolated)
            {
                tailNeighbours.UnionWith(tailNode.OutgoingEdges.Select(x => m_State.EdgeHeadNode(x).Id).Except(new[] { headNode.Id }));
            }
            if (tailNode.NodeType != NodeType.Start && tailNode.NodeType != NodeType.Isolated)
            {
                tailNeighbours.UnionWith(tailNode.IncomingEdges.Select(x => m_State.EdgeTailNode(x).Id).Except(new[] { headNode.Id }));
            }

            var headNeighbours = new HashSet<T>();
            if (headNode.NodeType != NodeType.Start && headNode.NodeType != NodeType.Isolated)
            {
                headNeighbours.UnionWith(headNode.IncomingEdges.Select(x => m_State.EdgeTailNode(x).Id).Except(new[] { tailNode.Id }));
            }
            if (headNode.NodeType != NodeType.End && headNode.NodeType != NodeType.Isolated)
            {
                headNeighbours.UnionWith(headNode.OutgoingEdges.Select(x => m_State.EdgeHeadNode(x).Id).Except(new[] { tailNode.Id }));
            }

            return tailNeighbours.Overlaps(headNeighbours);
        }

        private static bool ShareMoreThanOneEdge(Node<T, IEvent<T>> tailNode, Node<T, IEvent<T>> headNode)
        {
            if (tailNode is null)
            {
                throw new ArgumentNullException(nameof(tailNode));
            }
            if (headNode is null)
            {
                throw new ArgumentNullException(nameof(headNode));
            }

            var tailOutgoing = tailNode.NodeType != NodeType.End && tailNode.NodeType != NodeType.Isolated
                ? new HashSet<T>(tailNode.OutgoingEdges) : new HashSet<T>();
            var headIncoming = headNode.NodeType != NodeType.Start && headNode.NodeType != NodeType.Isolated
                ? new HashSet<T>(headNode.IncomingEdges) : new HashSet<T>();

            return tailOutgoing.Intersect(headIncoming).Count() > 1;
        }

        private void RemoveParallelIncomingDummyEdges(Node<T, IEvent<T>> node)
        {
            if (node is null)
            {
                throw new ArgumentNullException(nameof(node));
            }
            if (node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated)
            {
                return;
            }

            var tailNodeParallelDummyEdgesLookup = new Dictionary<T, HashSet<T>>();
            IEnumerable<T> removableIncomingDummyEdgeIds = node.IncomingEdges
                .Select(x => m_State.Edge(x))
                .Where(x => x.Content.IsDummy && x.Content.CanBeRemoved)
                .Select(x => x.Id)
                .ToList();

            foreach (T incomingDummyEdgeId in removableIncomingDummyEdgeIds)
            {
                T tailNodeId = m_State.EdgeTailNode(incomingDummyEdgeId).Id;
                if (!tailNodeParallelDummyEdgesLookup.TryGetValue(tailNodeId, out HashSet<T> dummyEdgeIds))
                {
                    dummyEdgeIds = new HashSet<T>();
                    tailNodeParallelDummyEdgesLookup.Add(tailNodeId, dummyEdgeIds);
                }
                dummyEdgeIds.Add(incomingDummyEdgeId);
            }

            List<T> setsOfMoreThanOneDummyEdge = tailNodeParallelDummyEdgesLookup
                .Where(x => x.Value.Count > 1).Select(x => x.Key).ToList();

            foreach (T tailNodeId in setsOfMoreThanOneDummyEdge)
            {
                List<T> dummyEdgeIds = tailNodeParallelDummyEdgesLookup[tailNodeId].ToList();
                int length = dummyEdgeIds.Count;
                for (int i = 1; i < length; i++)
                {
                    RemoveDummyActivity(dummyEdgeIds[i]);
                }
            }
        }

        private bool ChangeEdgeTailNodeWithoutCleanup(T edgeId, T newTailNodeId)
        {
            if (!m_State.AllDependenciesSatisfied)
            {
                return false;
            }
            if (!m_State.ContainsEdge(edgeId))
            {
                return false;
            }
            if (!m_State.TryGetNode(newTailNodeId, out Node<T, IEvent<T>> newTailNode))
            {
                return false;
            }

            Node<T, IEvent<T>> oldTailNode = m_State.EdgeTailNode(edgeId);
            oldTailNode.OutgoingEdges.Remove(edgeId);
            m_State.RemoveEdgeTailNode(edgeId);
            newTailNode.OutgoingEdges.Add(edgeId);
            m_State.SetEdgeTailNode(edgeId, newTailNode);
            return true;
        }

        private bool ChangeEdgeHeadNodeWithoutCleanup(T edgeId, T newHeadNodeId)
        {
            if (!m_State.AllDependenciesSatisfied)
            {
                return false;
            }
            if (!m_State.ContainsEdge(edgeId))
            {
                return false;
            }
            if (!m_State.TryGetNode(newHeadNodeId, out Node<T, IEvent<T>> newHeadNode))
            {
                return false;
            }

            Node<T, IEvent<T>> currentHeadNode = m_State.EdgeHeadNode(edgeId);
            currentHeadNode.IncomingEdges.Remove(edgeId);
            m_State.RemoveEdgeHeadNode(edgeId);
            newHeadNode.IncomingEdges.Add(edgeId);
            m_State.SetEdgeHeadNode(edgeId, newHeadNode);
            return true;
        }

        private bool ChangeEdgeTailNode(T edgeId, T newTailNodeId)
        {
            if (!m_State.AllDependenciesSatisfied)
            {
                return false;
            }

            Node<T, IEvent<T>> oldTailNode = m_State.EdgeTailNode(edgeId);
            bool changeTailSuccess = ChangeEdgeTailNodeWithoutCleanup(edgeId, newTailNodeId);
            if (!changeTailSuccess)
            {
                throw new InvalidOperationException($@"Unable to change tail node of edge {edgeId} to node {newTailNodeId} without cleanup");
            }

            IList<T> oldTailNodeOutgoingEdgeIds = oldTailNode.OutgoingEdges.ToList();
            if (!oldTailNodeOutgoingEdgeIds.Any())
            {
                Node<T, IEvent<T>> headNode = m_State.EdgeHeadNode(edgeId);
                IList<T> oldTailNodeIncomingEdgeIds = oldTailNode.IncomingEdges.ToList();
                foreach (T oldTailNodeIncomingEdgeId in oldTailNodeIncomingEdgeIds)
                {
                    bool changeHeadSuccess = ChangeEdgeHeadNodeWithoutCleanup(oldTailNodeIncomingEdgeId, headNode.Id);
                    if (!changeHeadSuccess)
                    {
                        throw new InvalidOperationException($@"Unable to change head node of edge {oldTailNodeIncomingEdgeId} to node {headNode.Id} without cleanup");
                    }
                }
            }

            if (oldTailNode.NodeType != NodeType.Start
                && oldTailNode.NodeType != NodeType.Isolated
                && oldTailNode.IncomingEdges.Count == 0
                && oldTailNode.OutgoingEdges.Count == 0)
            {
                m_State.RemoveNode(oldTailNode.Id);
            }
            return true;
        }

        private bool ChangeEdgeHeadNode(T edgeId, T newHeadNodeId)
        {
            if (!m_State.AllDependenciesSatisfied)
            {
                return false;
            }

            Node<T, IEvent<T>> oldHeadNode = m_State.EdgeHeadNode(edgeId);
            bool changeHeadSuccess = ChangeEdgeHeadNodeWithoutCleanup(edgeId, newHeadNodeId);
            if (!changeHeadSuccess)
            {
                throw new InvalidOperationException($@"Unable to change head node of edge {edgeId} to node {newHeadNodeId} without cleanup");
            }

            IList<T> oldHeadNodeIncomingEdgeIds = oldHeadNode.IncomingEdges.ToList();
            if (!oldHeadNodeIncomingEdgeIds.Any())
            {
                Node<T, IEvent<T>> tailNode = m_State.EdgeTailNode(edgeId);
                IList<T> oldHeadNodeOutgoingEdgeIds = oldHeadNode.OutgoingEdges.ToList();
                foreach (T oldHeadNodeOutgoingEdgeId in oldHeadNodeOutgoingEdgeIds)
                {
                    bool changeTailSuccess = ChangeEdgeTailNodeWithoutCleanup(oldHeadNodeOutgoingEdgeId, tailNode.Id);
                    if (!changeTailSuccess)
                    {
                        throw new InvalidOperationException($@"Unable to change tail node of edge {oldHeadNodeOutgoingEdgeId} to node {tailNode.Id} without cleanup");
                    }
                }
            }

            if (oldHeadNode.NodeType != NodeType.End
                && oldHeadNode.NodeType != NodeType.Isolated
                && oldHeadNode.IncomingEdges.Count == 0
                && oldHeadNode.OutgoingEdges.Count == 0)
            {
                m_State.RemoveNode(oldHeadNode.Id);
            }
            return true;
        }

        #endregion
    }
}
