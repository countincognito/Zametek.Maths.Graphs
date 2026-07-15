using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    // Implements all dummy-edge operations for Activity-on-Arrow graphs.
    // Operates on the shared ArrowGraphState supplied at construction time - the
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
        private readonly IArrowStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity> m_StronglyConnectedComponentsFinder;
        private readonly ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> m_State;

        #endregion

        #region Ctor

        internal DummyEdgeOrchestrator(
            IIdGenerator<T> edgeIdGenerator,
            IActivityGenerator<T, TResourceId, TWorkStreamId, TActivity> dummyActivityGenerator,
            IArrowStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity> stronglyConnectedComponentsFinder,
            ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state)
        {
            m_EdgeIdGenerator = edgeIdGenerator ?? throw new ArgumentNullException(nameof(edgeIdGenerator));
            m_DummyActivityGenerator = dummyActivityGenerator ?? throw new ArgumentNullException(nameof(dummyActivityGenerator));
            m_StronglyConnectedComponentsFinder = stronglyConnectedComponentsFinder ?? throw new ArgumentNullException(nameof(stronglyConnectedComponentsFinder));
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
            // Retrieve the activity's edge.
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

            // Check to make sure that no other edges will be made parallel
            // by removing this edge.
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

            // If the head node is not the End node, and it has no more incoming
            // edges, then transfer the head node's outgoing edges to the tail node.
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
                // If the tail node is not the Start node, and it has no more outgoing
                // edges, then transfer the tail node's incoming edges to the head node.
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

            List<ICircularDependency<T>> circularDependencies =
                m_StronglyConnectedComponentsFinder.FindStronglyCircularDependencies(m_State, ignoreDummies: false);

            if (circularDependencies.Count != 0)
            {
                return false;
            }

            // Go through each node that is not an End or Isolated node.
            List<Node<T, IEvent<T>>> nodes = m_State.Nodes
                .Where(x => x.NodeType != NodeType.End && x.NodeType != NodeType.Isolated)
                .OrderByDescending(x => x.Content.EarliestFinishTime)
                .ToList();

            foreach (Node<T, IEvent<T>> node in nodes)
            {
                // Get the outgoing dummy edges and their head nodes.
                var outgoingDummyEdgeIdLookup = new HashSet<T>(
                    node.OutgoingEdges.Select(x => m_State.Edge(x))
                    .Where(x => x.Content.IsDummy && x.Content.CanBeRemoved).Select(x => x.Id));

                IList<Node<T, IEvent<T>>> dummyEdgeSuccessorNodes =
                    outgoingDummyEdgeIdLookup.Select(x => m_State.EdgeHeadNode(x)).ToList();

                // Now from the successor nodes, work backwards to find
                // all the dependency nodes that share the same successor
                // nodes via dummy edges.

                // First find all the removable dummy edges that have the
                // successor nodes as head nodes.
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

                // Now find the subset of dependency nodes that are common
                // to all the successor nodes via removable dummy edges.
                IList<T> commonDependencyNodes =
                    dummyEdgeIdsToSuccessorNodes.Select(x => x.Select(y => m_State.EdgeTailNode(y).Id))
                    .Aggregate((previous, next) => previous.Intersect(next)).ToList();

                var commonDependencyNodeLookup = new HashSet<T>(commonDependencyNodes);

                // Now filter the dummy edges by whether they originate from
                // the common dependency nodes.
                IList<T> commonDependencyEdgeIds =
                    dummyEdgeIdsToSuccessorNodes.SelectMany(x => x)
                    .Where(x => commonDependencyNodeLookup.Contains(m_State.EdgeTailNode(x).Id))
                    .ToList();

                // In order to redirect any common dependencies to the original
                // node, it cannot have any successor nodes other than the common
                // successor nodes (i.e. its successor nodes must be a subset of
                // the common successor nodes).
                var allSuccessorNodeLookup = new HashSet<T>(node.OutgoingEdges.Select(x => m_State.EdgeHeadNode(x).Id));
                var commonSuccessorNodeLookup = new HashSet<T>(commonDependencyEdgeIds.Select(x => m_State.EdgeHeadNode(x).Id));

                if (!allSuccessorNodeLookup.IsSubsetOf(commonSuccessorNodeLookup))
                {
                    continue;
                }

                // Redirect all common dependencies towards the original node.
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

            List<ICircularDependency<T>> circularDependencies =
                m_StronglyConnectedComponentsFinder.FindStronglyCircularDependencies(m_State, ignoreDummies: false);

            if (circularDependencies.Count != 0)
            {
                return false;
            }

            // Go through and remove all the dummy edges that are
            // the only outgoing edge of their tail node, and also
            // the only incoming edge of their head node.
            foreach (Edge<T, TActivity> edge in GetDummyEdgesInDescendingOrder().Where(x => x.Content.CanBeRemoved))
            {
                Node<T, IEvent<T>> tailNode = m_State.EdgeTailNode(edge.Id);
                Node<T, IEvent<T>> headNode = m_State.EdgeHeadNode(edge.Id);
                if (tailNode.OutgoingEdges.Count == 1 && headNode.IncomingEdges.Count == 1)
                {
                    RemoveDummyActivity(edge.Id);
                }
            }

            // Next, go through and remove all the dummy edges that
            // are the only incoming edge of their head node.
            foreach (Edge<T, TActivity> edge in GetDummyEdgesInDescendingOrder().Where(x => x.Content.CanBeRemoved))
            {
                if (m_State.EdgeHeadNode(edge.Id).IncomingEdges.Count == 1)
                {
                    RemoveDummyActivity(edge.Id);
                }
            }

            // Next, go through and remove all the dummy edges that
            // are the only outgoing edge of their tail node.
            foreach (Edge<T, TActivity> edge in GetDummyEdgesInDescendingOrder().Where(x => x.Content.CanBeRemoved))
            {
                if (m_State.EdgeTailNode(edge.Id).OutgoingEdges.Count == 1)
                {
                    RemoveDummyActivity(edge.Id);
                }
            }

            // Remove parallel dummy edges (if they exist).
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

            // Iterative (was recursive) so a deep dependency chain cannot overflow the
            // stack. A visited set means each node's incoming edges are processed once:
            // every node removes only its own incoming dummy edges, using the static
            // ancestor lookup, so the operation is independent of visit order and
            // idempotent per node.
            var visited = new HashSet<T>();
            var stack = new Stack<T>();
            stack.Push(nodeId);

            while (stack.Count != 0)
            {
                T currentNodeId = stack.Pop();
                if (!visited.Add(currentNodeId))
                {
                    continue;
                }

                Node<T, IEvent<T>> node = m_State.Node(currentNodeId);

                if (node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated)
                {
                    continue;
                }

                // Go through all the incoming edges and collate the
                // ancestors of their tail nodes.
                var tailNodeAncestors = new HashSet<T>(node.IncomingEdges
                    .Select(x => m_State.EdgeTailNode(x).Id)
                    .SelectMany(x => nodeIdAncestorLookup[x]));

                // Go through the incoming dummy edges and remove any that
                // connect directly to any ancestors of the non-dummy edges'
                // tail nodes.
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

                // Continue with all the remaining incoming edges' tail nodes.
                List<T> remainingIncomingEdges = node.IncomingEdges
                    .Select(x => m_State.EdgeTailNode(x).Id)
                    .ToList();

                foreach (T tailNodeId in remainingIncomingEdges)
                {
                    stack.Push(tailNodeId);
                }
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

            // Go through each of the node's outgoing edges, record them,
            // then do the same to their head nodes.
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

        /// <summary>
        /// Check to make sure that no other edges will be made parallel
        /// by removing this edge. If there is an intersection between
        /// the ancestor/descendant nodes of the edge's tail node, and the
        /// ancestor/descendant nodes of the head node, then do not remove it.
        /// </summary>
        /// <param name="tailNode"></param>
        /// <param name="headNode"></param>
        /// <returns></returns>
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
            // First the descendants of the tail node.
            if (tailNode.NodeType != NodeType.End && tailNode.NodeType != NodeType.Isolated)
            {
                tailNeighbours.UnionWith(tailNode.OutgoingEdges.Select(x => m_State.EdgeHeadNode(x).Id).Except(new[] { headNode.Id }));
            }
            // Then the ancestors of the tail node.
            if (tailNode.NodeType != NodeType.Start && tailNode.NodeType != NodeType.Isolated)
            {
                tailNeighbours.UnionWith(tailNode.IncomingEdges.Select(x => m_State.EdgeTailNode(x).Id).Except(new[] { headNode.Id }));
            }

            var headNeighbours = new HashSet<T>();
            // Next the ancestors of the head node.
            if (headNode.NodeType != NodeType.Start && headNode.NodeType != NodeType.Isolated)
            {
                headNeighbours.UnionWith(headNode.IncomingEdges.Select(x => m_State.EdgeTailNode(x).Id).Except(new[] { tailNode.Id }));
            }
            // Then the descendants of the head node.
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
            // Clean up any dummy edges that are parallel coming into the head node.
            if (node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated)
            {
                return;
            }

            // First, find the tail nodes that connect to this node via dummy edges.
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

            // Now find the tail nodes that connect to this node via multiple dummy edges.
            List<T> setsOfMoreThanOneDummyEdge = tailNodeParallelDummyEdgesLookup
                .Where(x => x.Value.Count > 1).Select(x => x.Key).ToList();

            foreach (T tailNodeId in setsOfMoreThanOneDummyEdge)
            {
                List<T> dummyEdgeIds = tailNodeParallelDummyEdgesLookup[tailNodeId].ToList();
                int length = dummyEdgeIds.Count;
                // Leave one dummy edge behind.
                for (int i = 1; i < length; i++)
                {
                    RemoveDummyActivity(dummyEdgeIds[i]);
                }
            }
        }

        private bool ChangeEdgeTailNodeWithoutCleanup(T edgeId, T newTailNodeId)
        {
            // Do not attend this unless all dependencies are satisfied.
            if (!m_State.AllDependenciesSatisfied)
            {
                return false;
            }
            // Retrieve the activity edge.
            if (!m_State.ContainsEdge(edgeId))
            {
                return false;
            }
            // Retrieve the new tail event node.
            if (!m_State.TryGetNode(newTailNodeId, out Node<T, IEvent<T>> newTailNode))
            {
                return false;
            }

            // Remove the connection from the current tail node.
            Node<T, IEvent<T>> oldTailNode = m_State.EdgeTailNode(edgeId);
            oldTailNode.OutgoingEdges.Remove(edgeId);
            m_State.RemoveEdgeTailNode(edgeId);

            // Attach to the new tail node.
            newTailNode.OutgoingEdges.Add(edgeId);
            m_State.SetEdgeTailNode(edgeId, newTailNode);
            return true;
        }

        private bool ChangeEdgeHeadNodeWithoutCleanup(T edgeId, T newHeadNodeId)
        {
            // Do not attend this unless all dependencies are satisfied.
            if (!m_State.AllDependenciesSatisfied)
            {
                return false;
            }
            // Retrieve the activity edge.
            if (!m_State.ContainsEdge(edgeId))
            {
                return false;
            }
            // Retrieve the new head event node.
            if (!m_State.TryGetNode(newHeadNodeId, out Node<T, IEvent<T>> newHeadNode))
            {
                return false;
            }

            // Remove the connection from the current head node.
            Node<T, IEvent<T>> currentHeadNode = m_State.EdgeHeadNode(edgeId);
            currentHeadNode.IncomingEdges.Remove(edgeId);
            m_State.RemoveEdgeHeadNode(edgeId);

            // Attach to the new head node.
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

            // If the old tail node has no other outgoing edges, then
            // connect its incoming edges to the current head node.
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

            // Final check to see if the tail node has no incoming or outgoing edges.
            // If it does not then remove it.
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
            // Do not attend this unless all dependencies are satisfied.
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

            // If the old head node has no other incoming edges, then
            // connect its outgoing edges to the current tail node.
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

            // Final check to see if the head node has no incoming or outgoing edges.
            // If it does not then remove it.
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
