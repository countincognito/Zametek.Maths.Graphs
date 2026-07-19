using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    // Implements the dummy-edge operations for Activity-on-Arrow graphs. Stateless:
    // the graph state - and the ID/activity generators and SCC finder each operation
    // needs - are supplied to its methods by the builder that owns them, so a single
    // instance can serve any graph and is safely shared across builder clones.
    /// <summary>
    /// Default dummy-edge orchestrator for Activity-on-Arrow graphs.
    /// </summary>
    public sealed class DummyEdgeOrchestrator<T, TResourceId, TWorkStreamId, TActivity>
        : IDummyEdgeOrchestrator<T, TResourceId, TWorkStreamId, TActivity>
        where TActivity : class, IActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        #region IDummyEdgeOrchestrator

        /// <inheritdoc/>
        public void ConnectWithDummyEdge(
            ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            IIdGenerator<T> edgeIdGenerator,
            IActivityGenerator<T, TResourceId, TWorkStreamId, TActivity> dummyActivityGenerator,
            Node<T, IEvent<T>> tailNode,
            Node<T, IEvent<T>> headNode)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }
            if (edgeIdGenerator is null)
            {
                throw new ArgumentNullException(nameof(edgeIdGenerator));
            }
            if (dummyActivityGenerator is null)
            {
                throw new ArgumentNullException(nameof(dummyActivityGenerator));
            }

            T dummyEdgeId = edgeIdGenerator.Generate();
            var dummyEdge = new Edge<T, TActivity>(dummyActivityGenerator.Generate(dummyEdgeId));
            headNode.IncomingEdges.Add(dummyEdgeId);
            state.SetEdgeHeadNode(dummyEdgeId, headNode);
            tailNode.OutgoingEdges.Add(dummyEdgeId);
            state.SetEdgeTailNode(dummyEdgeId, tailNode);
            state.AddEdge(dummyEdge);
        }

        /// <inheritdoc/>
        public bool RemoveDummyActivity(
            ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            T activityId)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            // Retrieve the activity's edge.
            if (!state.TryGetEdge(activityId, out Edge<T, TActivity> edge))
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

            Node<T, IEvent<T>> tailNode = state.EdgeTailNode(activityId);
            Node<T, IEvent<T>> headNode = state.EdgeHeadNode(activityId);

            // Check to make sure that no other edges will be made parallel
            // by removing this edge.
            if (HaveDescendantOrAncestorOverlap(state, tailNode, headNode) && !ShareMoreThanOneEdge(tailNode, headNode))
            {
                return false;
            }

            // Remove the edge from the tail node.
            tailNode.OutgoingEdges.Remove(activityId);
            state.RemoveEdgeTailNode(activityId);

            // Remove the edge from the head node.
            headNode.IncomingEdges.Remove(activityId);
            state.RemoveEdgeHeadNode(activityId);

            // Remove the edge completely.
            state.RemoveEdge(activityId);

            // If the head node is not the End node, and it has no more incoming
            // edges, then transfer the head node's outgoing edges to the tail node.
            if (headNode.NodeType != NodeType.End
                && headNode.NodeType != NodeType.Isolated
                && headNode.IncomingEdges.Count == 0)
            {
                IList<T> headNodeOutgoingEdgeIds = headNode.OutgoingEdges.ToList();
                foreach (T headNodeOutgoingEdgeId in headNodeOutgoingEdgeIds)
                {
                    bool changeTailSuccess = ChangeEdgeTailNode(state, headNodeOutgoingEdgeId, tailNode.Id);
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
                    bool changeHeadSuccess = ChangeEdgeHeadNode(state, tailNodeIncomingEdgeId, headNode.Id);
                    if (!changeHeadSuccess)
                    {
                        throw new InvalidOperationException($@"Unable to change head node of edge {tailNodeIncomingEdgeId} to node {headNode.Id} when removing dummy activity {activityId}");
                    }
                }
            }
            return true;
        }

        /// <inheritdoc/>
        public bool RedirectDummyEdges(
            ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            IArrowStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity> sccFinder)
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
                return false;
            }

            List<ICircularDependency<T>> circularDependencies =
                sccFinder.FindStronglyCircularDependencies(state, ignoreDummies: false);

            if (circularDependencies.Count != 0)
            {
                return false;
            }

            // Go through each node that is not an End or Isolated node.
            List<Node<T, IEvent<T>>> nodes = state.Nodes
                .Where(x => x.NodeType != NodeType.End && x.NodeType != NodeType.Isolated)
                .OrderByDescending(x => x.Content.EarliestFinishTime)
                .ToList();

            foreach (Node<T, IEvent<T>> node in nodes)
            {
                // Get the outgoing dummy edges and their head nodes.
                var outgoingDummyEdgeIdLookup = new HashSet<T>(
                    node.OutgoingEdges.Select(x => state.Edge(x))
                    .Where(x => x.Content.IsDummy && x.Content.CanBeRemoved).Select(x => x.Id));

                IList<Node<T, IEvent<T>>> dummyEdgeSuccessorNodes =
                    outgoingDummyEdgeIdLookup.Select(x => state.EdgeHeadNode(x)).ToList();

                // Now from the successor nodes, work backwards to find
                // all the dependency nodes that share the same successor
                // nodes via dummy edges.

                // First find all the removable dummy edges that have the
                // successor nodes as head nodes.
                IList<IEnumerable<T>> dummyEdgeIdsToSuccessorNodes =
                    dummyEdgeSuccessorNodes
                    .Select(x => x.IncomingEdges.Select(y => state.Edge(y))
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
                    dummyEdgeIdsToSuccessorNodes.Select(x => x.Select(y => state.EdgeTailNode(y).Id))
                    .Aggregate((previous, next) => previous.Intersect(next)).ToList();

                var commonDependencyNodeLookup = new HashSet<T>(commonDependencyNodes);

                // Now filter the dummy edges by whether they originate from
                // the common dependency nodes.
                IList<T> commonDependencyEdgeIds =
                    dummyEdgeIdsToSuccessorNodes.SelectMany(x => x)
                    .Where(x => commonDependencyNodeLookup.Contains(state.EdgeTailNode(x).Id))
                    .ToList();

                // In order to redirect any common dependencies to the original
                // node, it cannot have any successor nodes other than the common
                // successor nodes (i.e. its successor nodes must be a subset of
                // the common successor nodes).
                var allSuccessorNodeLookup = new HashSet<T>(node.OutgoingEdges.Select(x => state.EdgeHeadNode(x).Id));
                var commonSuccessorNodeLookup = new HashSet<T>(commonDependencyEdgeIds.Select(x => state.EdgeHeadNode(x).Id));

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
                    bool changeHeadSuccess = ChangeEdgeHeadNode(state, commonDependencyEdgeId, node.Id);
                    if (!changeHeadSuccess)
                    {
                        throw new InvalidOperationException($@"Unable to change head node of edge {commonDependencyEdgeId} to node {node.Id} when redirecting dummy activities");
                    }
                }

                RemoveParallelIncomingDummyEdges(state, node);
            }
            return true;
        }

        /// <inheritdoc/>
        public bool RemoveRedundantDummyEdges(
            ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            IArrowStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity> sccFinder)
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
                return false;
            }

            List<ICircularDependency<T>> circularDependencies =
                sccFinder.FindStronglyCircularDependencies(state, ignoreDummies: false);

            if (circularDependencies.Count != 0)
            {
                return false;
            }

            // Go through and remove all the dummy edges that are
            // the only outgoing edge of their tail node, and also
            // the only incoming edge of their head node.
            foreach (Edge<T, TActivity> edge in GetDummyEdgesInDescendingOrder(state).Where(x => x.Content.CanBeRemoved))
            {
                Node<T, IEvent<T>> tailNode = state.EdgeTailNode(edge.Id);
                Node<T, IEvent<T>> headNode = state.EdgeHeadNode(edge.Id);
                if (tailNode.OutgoingEdges.Count == 1 && headNode.IncomingEdges.Count == 1)
                {
                    RemoveDummyActivity(state, edge.Id);
                }
            }

            // Next, go through and remove all the dummy edges that
            // are the only incoming edge of their head node.
            foreach (Edge<T, TActivity> edge in GetDummyEdgesInDescendingOrder(state).Where(x => x.Content.CanBeRemoved))
            {
                if (state.EdgeHeadNode(edge.Id).IncomingEdges.Count == 1)
                {
                    RemoveDummyActivity(state, edge.Id);
                }
            }

            // Next, go through and remove all the dummy edges that
            // are the only outgoing edge of their tail node.
            foreach (Edge<T, TActivity> edge in GetDummyEdgesInDescendingOrder(state).Where(x => x.Content.CanBeRemoved))
            {
                if (state.EdgeTailNode(edge.Id).OutgoingEdges.Count == 1)
                {
                    RemoveDummyActivity(state, edge.Id);
                }
            }

            // Remove parallel dummy edges (if they exist).
            foreach (Node<T, IEvent<T>> node in state.Nodes.ToList())
            {
                RemoveParallelIncomingDummyEdges(state, node);
            }

            return true;
        }

        /// <inheritdoc/>
        public List<Edge<T, TActivity>> GetDummyEdgesInDescendingOrder(
            ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var recordedEdges = new HashSet<T>();
            var edgesInDescendingOrder = new List<Edge<T, TActivity>>();
            GetEdgesInDescendingOrder(state, state.StartNode.Id, edgesInDescendingOrder, recordedEdges);
            return edgesInDescendingOrder.Where(x => x.Content.IsDummy).ToList();
        }

        #endregion

        #region Private Methods

        private static void GetEdgesInDescendingOrder(
            ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            T nodeId,
            List<Edge<T, TActivity>> edgesInDescendingOrder,
            HashSet<T> recordedEdges)
        {
            if (edgesInDescendingOrder is null)
            {
                throw new ArgumentNullException(nameof(edgesInDescendingOrder));
            }
            if (recordedEdges is null)
            {
                throw new ArgumentNullException(nameof(recordedEdges));
            }

            Node<T, IEvent<T>> node = state.Node(nodeId);

            if (node.NodeType == NodeType.End || node.NodeType == NodeType.Isolated)
            {
                return;
            }

            // Go through each of the node's outgoing edges, record them,
            // then do the same to their head nodes.
            foreach (Edge<T, TActivity> outgoingEdge in node.OutgoingEdges.Select(x => state.Edge(x)))
            {
                if (!recordedEdges.Contains(outgoingEdge.Id))
                {
                    edgesInDescendingOrder.Add(outgoingEdge);
                    recordedEdges.Add(outgoingEdge.Id);
                }
                GetEdgesInDescendingOrder(state, state.EdgeHeadNode(outgoingEdge.Id).Id, edgesInDescendingOrder, recordedEdges);
            }
        }

        /// <summary>
        /// Check to make sure that no other edges will be made parallel
        /// by removing this edge. If there is an intersection between
        /// the ancestor/descendant nodes of the edge's tail node, and the
        /// ancestor/descendant nodes of the head node, then do not remove it.
        /// </summary>
        private static bool HaveDescendantOrAncestorOverlap(
            ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            Node<T, IEvent<T>> tailNode,
            Node<T, IEvent<T>> headNode)
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
                tailNeighbours.UnionWith(tailNode.OutgoingEdges.Select(x => state.EdgeHeadNode(x).Id).Except(new[] { headNode.Id }));
            }
            // Then the ancestors of the tail node.
            if (tailNode.NodeType != NodeType.Start && tailNode.NodeType != NodeType.Isolated)
            {
                tailNeighbours.UnionWith(tailNode.IncomingEdges.Select(x => state.EdgeTailNode(x).Id).Except(new[] { headNode.Id }));
            }

            var headNeighbours = new HashSet<T>();
            // Next the ancestors of the head node.
            if (headNode.NodeType != NodeType.Start && headNode.NodeType != NodeType.Isolated)
            {
                headNeighbours.UnionWith(headNode.IncomingEdges.Select(x => state.EdgeTailNode(x).Id).Except(new[] { tailNode.Id }));
            }
            // Then the descendants of the head node.
            if (headNode.NodeType != NodeType.End && headNode.NodeType != NodeType.Isolated)
            {
                headNeighbours.UnionWith(headNode.OutgoingEdges.Select(x => state.EdgeHeadNode(x).Id).Except(new[] { tailNode.Id }));
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

        private void RemoveParallelIncomingDummyEdges(
            ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            Node<T, IEvent<T>> node)
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
                .Select(x => state.Edge(x))
                .Where(x => x.Content.IsDummy && x.Content.CanBeRemoved)
                .Select(x => x.Id)
                .ToList();

            foreach (T incomingDummyEdgeId in removableIncomingDummyEdgeIds)
            {
                T tailNodeId = state.EdgeTailNode(incomingDummyEdgeId).Id;
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
                    RemoveDummyActivity(state, dummyEdgeIds[i]);
                }
            }
        }

        private bool ChangeEdgeTailNodeWithoutCleanup(
            ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            T edgeId,
            T newTailNodeId)
        {
            // Do not attend this unless all dependencies are satisfied.
            if (!state.AllDependenciesSatisfied)
            {
                return false;
            }
            // Retrieve the activity edge.
            if (!state.ContainsEdge(edgeId))
            {
                return false;
            }
            // Retrieve the new tail event node.
            if (!state.TryGetNode(newTailNodeId, out Node<T, IEvent<T>> newTailNode))
            {
                return false;
            }

            // Remove the connection from the current tail node.
            Node<T, IEvent<T>> oldTailNode = state.EdgeTailNode(edgeId);
            oldTailNode.OutgoingEdges.Remove(edgeId);
            state.RemoveEdgeTailNode(edgeId);

            // Attach to the new tail node.
            newTailNode.OutgoingEdges.Add(edgeId);
            state.SetEdgeTailNode(edgeId, newTailNode);
            return true;
        }

        private bool ChangeEdgeHeadNodeWithoutCleanup(
            ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            T edgeId,
            T newHeadNodeId)
        {
            // Do not attend this unless all dependencies are satisfied.
            if (!state.AllDependenciesSatisfied)
            {
                return false;
            }
            // Retrieve the activity edge.
            if (!state.ContainsEdge(edgeId))
            {
                return false;
            }
            // Retrieve the new head event node.
            if (!state.TryGetNode(newHeadNodeId, out Node<T, IEvent<T>> newHeadNode))
            {
                return false;
            }

            // Remove the connection from the current head node.
            Node<T, IEvent<T>> currentHeadNode = state.EdgeHeadNode(edgeId);
            currentHeadNode.IncomingEdges.Remove(edgeId);
            state.RemoveEdgeHeadNode(edgeId);

            // Attach to the new head node.
            newHeadNode.IncomingEdges.Add(edgeId);
            state.SetEdgeHeadNode(edgeId, newHeadNode);
            return true;
        }

        private bool ChangeEdgeTailNode(
            ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            T edgeId,
            T newTailNodeId)
        {
            if (!state.AllDependenciesSatisfied)
            {
                return false;
            }

            Node<T, IEvent<T>> oldTailNode = state.EdgeTailNode(edgeId);
            bool changeTailSuccess = ChangeEdgeTailNodeWithoutCleanup(state, edgeId, newTailNodeId);
            if (!changeTailSuccess)
            {
                throw new InvalidOperationException($@"Unable to change tail node of edge {edgeId} to node {newTailNodeId} without cleanup");
            }

            // If the old tail node has no other outgoing edges, then
            // connect its incoming edges to the current head node.
            IList<T> oldTailNodeOutgoingEdgeIds = oldTailNode.OutgoingEdges.ToList();
            if (!oldTailNodeOutgoingEdgeIds.Any())
            {
                Node<T, IEvent<T>> headNode = state.EdgeHeadNode(edgeId);
                IList<T> oldTailNodeIncomingEdgeIds = oldTailNode.IncomingEdges.ToList();
                foreach (T oldTailNodeIncomingEdgeId in oldTailNodeIncomingEdgeIds)
                {
                    bool changeHeadSuccess = ChangeEdgeHeadNodeWithoutCleanup(state, oldTailNodeIncomingEdgeId, headNode.Id);
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
                state.RemoveNode(oldTailNode.Id);
            }
            return true;
        }

        private bool ChangeEdgeHeadNode(
            ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            T edgeId,
            T newHeadNodeId)
        {
            // Do not attend this unless all dependencies are satisfied.
            if (!state.AllDependenciesSatisfied)
            {
                return false;
            }

            Node<T, IEvent<T>> oldHeadNode = state.EdgeHeadNode(edgeId);
            bool changeHeadSuccess = ChangeEdgeHeadNodeWithoutCleanup(state, edgeId, newHeadNodeId);
            if (!changeHeadSuccess)
            {
                throw new InvalidOperationException($@"Unable to change head node of edge {edgeId} to node {newHeadNodeId} without cleanup");
            }

            // If the old head node has no other incoming edges, then
            // connect its outgoing edges to the current tail node.
            IList<T> oldHeadNodeIncomingEdgeIds = oldHeadNode.IncomingEdges.ToList();
            if (!oldHeadNodeIncomingEdgeIds.Any())
            {
                Node<T, IEvent<T>> tailNode = state.EdgeTailNode(edgeId);
                IList<T> oldHeadNodeOutgoingEdgeIds = oldHeadNode.OutgoingEdges.ToList();
                foreach (T oldHeadNodeOutgoingEdgeId in oldHeadNodeOutgoingEdgeIds)
                {
                    bool changeTailSuccess = ChangeEdgeTailNodeWithoutCleanup(state, oldHeadNodeOutgoingEdgeId, tailNode.Id);
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
                state.RemoveNode(oldHeadNode.Id);
            }
            return true;
        }

        #endregion
    }
}
