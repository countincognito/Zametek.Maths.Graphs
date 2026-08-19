using System;
using System.Collections.Generic;
using System.Linq;
using Zametek.Utility;

namespace Zametek.Maths.Graphs
{
    // Calculates the critical path for Activity-on-Vertex graphs.
    // Implements the forward pass (earliest start times), backward pass (latest finish times),
    // free slack, and isolated node backfill. Operates on the shared VertexGraphState.
    /// <summary>
    /// Default critical-path engine for Activity-on-Vertex graphs.
    /// </summary>
    public sealed class VertexCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity>
        : IVertexCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
        where TActivity : IActivity<T, TResourceId, TWorkStreamId>
    {
        /// <inheritdoc/>
        public bool CalculateCriticalPathForwardFlow(
            IVertexGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            List<IInvalidConstraint<T>> invalidConstraints,
            bool shuffle)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }
            if (invalidConstraints is null)
            {
                throw new ArgumentNullException(nameof(invalidConstraints));
            }

            if (invalidConstraints.Count != 0)
            {
                return false;
            }

            // We can assume at this point that all the activity constraints are valid.
            //
            // An edge is complete exactly when the node at its tail has been processed,
            // so rather than tracking edge identity in sets, each node counts how many
            // of its incoming edges are still outstanding. Only nodes with incoming
            // edges - Normal and End nodes - need an entry. This is what the flow used
            // to spend two edge-sized hash sets per call to discover.
            var pendingIncomingEdgeCounts = new Dictionary<T, int>(state.NodeCount);

            foreach (Node<T, TActivity> node in state.Nodes)
            {
                if (node.NodeType == NodeType.Start
                    || node.NodeType == NodeType.Isolated)
                {
                    continue;
                }
                pendingIncomingEdgeCounts[node.Id] = node.IncomingEdges.Count;
            }

            // Counted down as edges are completed; anything left at the end means the
            // walk could not reach every edge, which only a cycle can cause.
            int outstandingEdgeCount = state.EdgeCount;
            var readyNodes = new List<Node<T, TActivity>>();

            // First complete the Isolated nodes.
            foreach (Node<T, TActivity> node in state.IsolatedNodes)
            {
                // Earliest Start Time.
                node.Content.EarliestStartTime = ClampEarliestStartTime(node.Content, 0);

                // Latest Finish Time.
                node.Content.LatestFinishTime =
                    AugmentedFinishTime(node.Content, node.Content.EarliestFinishTime!.Value);
            }

            // Complete the Start nodes first to ensure the completed edge IDs contains something.
            foreach (Node<T, TActivity> node in state.StartNodes)
            {
                node.Content.EarliestStartTime = ClampEarliestStartTime(node.Content, 0);

                int earliestFinishTime =
                    StartNodeEdgeEarliestFinishTime(node.Content, node.Content.EarliestFinishTime!.Value);

                foreach (T outgoingEdgeId in node.OutgoingEdges)
                {
                    Edge<T, IEvent<T>> outgoingEdge = state.Edge(outgoingEdgeId);
                    outgoingEdge.Content.EarliestFinishTime = earliestFinishTime;
                    outstandingEdgeCount--;
                    ReleaseSuccessor(state, outgoingEdgeId, pendingIncomingEdgeCounts, readyNodes);
                }
            }

            // Forward flow algorithm.
            //
            // An edge can be completed once every incoming edge of its tail node is
            // complete, and the value it receives depends only on that tail node - so
            // every outgoing edge of a node is given the same earliest finish time.
            // That makes this a walk over nodes in dependency order rather than a
            // search over edges: a node is processed once all of its predecessors are
            // done, and it then completes all of its outgoing edges at once.
            //
            // The previous form swept the entire remaining edge set repeatedly, keeping
            // whichever edges had become ready, which cost O(depth x E). Visiting each
            // node once as it becomes ready costs O(V + E) and produces identical
            // values, because an edge is still only completed after exactly the same
            // predecessors are.
            //
            // Nodes are handled in rounds, and the shuffle hook shuffles each round.
            // Within a round the order genuinely cannot matter - that is what
            // ShuffleProcessingOrder exists to demonstrate.
            //
            // The first round was seeded by the Start node pass above: every node whose
            // only unfinished dependency was a Start node is now ready. Nothing else can
            // be ready to begin with, because any other node has an incoming edge from a
            // node that has yet to be processed.
            while (readyNodes.Count != 0)
            {
                if (shuffle)
                {
                    readyNodes.Shuffle();
                }

                var nextReadyNodes = new List<Node<T, TActivity>>();

                foreach (Node<T, TActivity> dependencyNode in readyNodes)
                {
                    if (!dependencyNode.Content.EarliestStartTime.HasValue)
                    {
                        dependencyNode.Content.EarliestStartTime = ClampEarliestStartTime(
                            dependencyNode.Content,
                            MaxEdgeEarliestFinishTime(state, dependencyNode.IncomingEdges));
                    }

                    int earliestFinishTime =
                        AugmentedFinishTime(dependencyNode.Content, dependencyNode.Content.EarliestFinishTime!.Value);

                    // A node is only ever processed once, and only Start nodes had their
                    // outgoing edges completed beforehand, so every outgoing edge here
                    // is still outstanding.
                    foreach (T edgeId in dependencyNode.OutgoingEdges)
                    {
                        Edge<T, IEvent<T>> edge = state.Edge(edgeId);
                        edge.Content.EarliestFinishTime = earliestFinishTime;
                        outstandingEdgeCount--;
                        ReleaseSuccessor(state, edgeId, pendingIncomingEdgeCounts, nextReadyNodes);
                    }
                }

                readyNodes = nextReadyNodes;
            }

            // If edges are still outstanding once nothing more can become ready, then a
            // cycle must exist in the graph and we will not be able to calculate the
            // earliest finish times.
            if (outstandingEdgeCount != 0)
            {
                throw new InvalidOperationException(Properties.Resources.Message_CannotCalculateEarliestFinishTimesDueToCyclicDependency);
            }

            // Now complete the End nodes.
            foreach (Node<T, TActivity> node in state.EndNodes)
            {
                // An edge is complete precisely when it has been given an earliest finish
                // time, and the pass above clears these before it starts, so this is the
                // same check the flow used to make against its set of completed edges.
                if (!AllEdgesHaveEarliestFinishTime(state, node.IncomingEdges))
                {
                    throw new InvalidOperationException($@"Cannot calculate EST for activity {node.Id} as not all dependency events have EFT values.");
                }

                if (!node.Content.EarliestStartTime.HasValue)
                {
                    node.Content.EarliestStartTime = ClampEarliestStartTime(
                        node.Content,
                        MaxEdgeEarliestFinishTime(state, node.IncomingEdges));
                }

                if (!node.Content.LatestFinishTime.HasValue)
                {
                    node.Content.LatestFinishTime =
                        AugmentedFinishTime(node.Content, node.Content.EarliestFinishTime!.Value);
                }
            }
            return true;
        }

        /// <inheritdoc/>
        public bool CalculateCriticalPathBackwardFlow(
            IVertexGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            List<IInvalidConstraint<T>> invalidConstraints,
            bool shuffle)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }
            if (invalidConstraints is null)
            {
                throw new ArgumentNullException(nameof(invalidConstraints));
            }

            if (invalidConstraints.Count != 0)
            {
                return false;
            }

            // Only perform if all events have earliest finish times.
            if (!state.Events.All(x => x.EarliestFinishTime.HasValue))
            {
                return false;
            }

            // Only perform if all activities have earliest finish times.
            if (!state.Activities.All(x => x.EarliestFinishTime.HasValue))
            {
                return false;
            }

            // Snapshot these before potentially modifying them.
            IList<Node<T, TActivity>> endNodesList = state.EndNodes.ToList();
            IList<Node<T, TActivity>> isolatedNodesList = state.IsolatedNodes.ToList();
            IList<Node<T, TActivity>> startNodesList = state.StartNodes.ToList();

            // Only perform if all end nodes have latest finish times.
            if (!endNodesList.All(x => x.Content.LatestFinishTime.HasValue))
            {
                return false;
            }

            // We can assume at this point that all the activity constraints are valid.
            // As in the forward flow, an edge is complete exactly when the node at its
            // head has been processed, so outstanding edges are counted per node rather
            // than tracked by identity. Only nodes with outgoing edges - Start and
            // Normal nodes - need an entry.
            var pendingOutgoingEdgeCounts = new Dictionary<T, int>(state.NodeCount);

            foreach (Node<T, TActivity> node in state.Nodes)
            {
                if (node.NodeType == NodeType.End
                    || node.NodeType == NodeType.Isolated)
                {
                    continue;
                }
                pendingOutgoingEdgeCounts[node.Id] = node.OutgoingEdges.Count;
            }

            int outstandingEdgeCount = state.EdgeCount;
            var readyNodes = new List<Node<T, TActivity>>();

            int endNodesEndTime = endNodesList.Select(x => x.Content.LatestFinishTime!.Value).DefaultIfEmpty().Max();
            int isolatedNodesEndTime = isolatedNodesList.Select(x => x.Content.LatestFinishTime!.Value).DefaultIfEmpty().Max();
            int endTime = Math.Max(endNodesEndTime, isolatedNodesEndTime);

            // Complete the End nodes first.
            foreach (Node<T, TActivity> node in endNodesList)
            {
                // Latest Finish Time.
                node.Content.LatestFinishTime = ClampLatestFinishTime(node.Content, endTime);

                // Free float/slack calculations.
                node.Content.FreeSlack = node.Content.LatestFinishTime - node.Content.EarliestFinishTime;

                int? edgeLatestFinishTime = EndNodeIncomingEdgeLatestFinishTime(node.Content);

                foreach (T incomingEdgeId in node.IncomingEdges)
                {
                    Edge<T, IEvent<T>> incomingEdge = state.Edge(incomingEdgeId);
                    incomingEdge.Content.LatestFinishTime = edgeLatestFinishTime;
                    outstandingEdgeCount--;
                    ReleasePredecessor(state, incomingEdgeId, pendingOutgoingEdgeCounts, readyNodes);
                }
            }

            // Backward flow algorithm.
            //
            // The mirror image of the forward flow: an edge can be completed once every
            // outgoing edge of its head node is complete, and the value it receives
            // depends only on that head node, so every incoming edge of a node is given
            // the same latest finish time. The same reasoning therefore applies - this
            // walks nodes in reverse dependency order, visiting each once, instead of
            // sweeping the remaining edge set repeatedly. The first round was seeded by
            // the End node pass above.
            while (readyNodes.Count != 0)
            {
                if (shuffle)
                {
                    readyNodes.Shuffle();
                }

                var nextReadyNodes = new List<Node<T, TActivity>>();

                foreach (Node<T, TActivity> successorNode in readyNodes)
                {
                    if (!successorNode.Content.LatestFinishTime.HasValue)
                    {
                        successorNode.Content.LatestFinishTime = ClampLatestFinishTime(
                            successorNode.Content,
                            MinEdgeLatestFinishTime(state, successorNode.OutgoingEdges));
                    }

                    if (!successorNode.Content.FreeSlack.HasValue)
                    {
                        successorNode.Content.FreeSlack = FreeSlackFromSuccessors(
                            successorNode.Content,
                            MinSuccessorEarliestStartTime(state, successorNode.OutgoingEdges));
                    }

                    // Only End nodes had their incoming edges completed beforehand,
                    // and a node is processed once, so every incoming edge here is
                    // still outstanding.
                    int? edgeLatestFinishTime = successorNode.Content.LatestStartTime;

                    foreach (T edgeId in successorNode.IncomingEdges)
                    {
                        Edge<T, IEvent<T>> edge = state.Edge(edgeId);
                        edge.Content.LatestFinishTime = edgeLatestFinishTime;
                        outstandingEdgeCount--;
                        ReleasePredecessor(state, edgeId, pendingOutgoingEdgeCounts, nextReadyNodes);
                    }
                }

                readyNodes = nextReadyNodes;
            }

            // If edges are still outstanding once nothing more can become ready, then a
            // cycle must exist in the graph and we will not be able to calculate the
            // latest finish times.
            if (outstandingEdgeCount != 0)
            {
                throw new InvalidOperationException(Properties.Resources.Message_CannotCalculateLatestFinishTimesDueToCyclicDependency);
            }

            // Now complete the Start nodes.
            foreach (Node<T, TActivity> node in startNodesList)
            {
                // An edge is complete precisely when it has been given a latest finish
                // time, which is the same check the flow used to make against its set of
                // completed edges.
                if (!AllEdgesHaveLatestFinishTime(state, node.OutgoingEdges))
                {
                    throw new InvalidOperationException($@"Cannot calculate LFT for activity {node.Id} as not all dependency events have LFT values.");
                }

                if (!node.Content.LatestFinishTime.HasValue)
                {
                    // The latest-finish diminishment the original applied here as well is
                    // unreachable: this branch only runs when there is no value to diminish
                    // by. Dropped rather than reproduced, since a helper cannot express it.
                    node.Content.LatestFinishTime = ClampLatestFinishTime(
                        node.Content,
                        MinEdgeLatestFinishTime(state, node.OutgoingEdges));
                }

                if (!node.Content.FreeSlack.HasValue)
                {
                    node.Content.FreeSlack = FreeSlackFromSuccessors(
                        node.Content,
                        MinSuccessorEarliestStartTime(state, node.OutgoingEdges));
                }
            }

            // At this point, the Isolated Nodes will not have finish times
            // or free slack values. That needs to be done after all critical
            // paths have been calculated, otherwise it will screw up the
            // priority list calculations.
            return true;
        }

        /// <inheritdoc/>
        public bool BackFillIsolatedNodes(
            IVertexGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            List<IInvalidConstraint<T>> invalidConstraints)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }
            if (invalidConstraints is null)
            {
                throw new ArgumentNullException(nameof(invalidConstraints));
            }

            if (invalidConstraints.Count != 0)
            {
                return false;
            }

            IList<Node<T, TActivity>> endNodesList = state.EndNodes.ToList();
            IList<Node<T, TActivity>> isolatedNodesList = state.IsolatedNodes.ToList();

            // Only perform if all end nodes have latest finish times.
            if (!endNodesList.All(x => x.Content.LatestFinishTime.HasValue))
            {
                return false;
            }

            int endNodesEndTime = endNodesList.Select(x => x.Content.LatestFinishTime!.Value).DefaultIfEmpty().Max();
            int isolatedNodesEndTime = isolatedNodesList.Select(x => x.Content.LatestFinishTime!.Value).DefaultIfEmpty().Max();
            int endTime = Math.Max(endNodesEndTime, isolatedNodesEndTime);

            // Now backfill the Isolated Nodes.
            foreach (Node<T, TActivity> node in isolatedNodesList)
            {
                // Latest Finish Time.
                int latestFinishTime = endTime;

                if (node.Content.MaximumLatestFinishTime.HasValue)
                {
                    int proposedLatestFinishTime = node.Content.MaximumLatestFinishTime.Value;

                    // Diminish the latest finish time artificially (if required).
                    if (proposedLatestFinishTime < latestFinishTime)
                    {
                        latestFinishTime = proposedLatestFinishTime;
                    }
                }

                node.Content.LatestFinishTime = latestFinishTime;

                // Free float/slack calculations.
                node.Content.FreeSlack = node.Content.LatestFinishTime - node.Content.EarliestFinishTime;
            }

            return true;
        }

        // Records that a forward-flow edge is complete, and queues the node at its head
        // if that was the last thing it was waiting for. An End node is never queued:
        // it has no outgoing edges to carry the flow onwards and is completed by the End
        // node pass instead.
        private static void ReleaseSuccessor(
            IVertexGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            T edgeId,
            Dictionary<T, int> pendingIncomingEdgeCounts,
            List<Node<T, TActivity>> readyNodes)
        {
            Node<T, TActivity> successorNode = state.EdgeHeadNode(edgeId);
            int pendingIncomingEdges = pendingIncomingEdgeCounts[successorNode.Id] - 1;
            pendingIncomingEdgeCounts[successorNode.Id] = pendingIncomingEdges;

            if (pendingIncomingEdges == 0
                && successorNode.NodeType != NodeType.End)
            {
                readyNodes.Add(successorNode);
            }
        }

        // The backward-flow mirror: records that an edge is complete and queues the node
        // at its tail once it has nothing left outstanding. A Start node is never queued,
        // for the same reason an End node is not queued going forwards.
        private static void ReleasePredecessor(
            IVertexGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            T edgeId,
            Dictionary<T, int> pendingOutgoingEdgeCounts,
            List<Node<T, TActivity>> readyNodes)
        {
            Node<T, TActivity> dependencyNode = state.EdgeTailNode(edgeId);
            int pendingOutgoingEdges = pendingOutgoingEdgeCounts[dependencyNode.Id] - 1;
            pendingOutgoingEdgeCounts[dependencyNode.Id] = pendingOutgoingEdges;

            if (pendingOutgoingEdges == 0
                && dependencyNode.NodeType != NodeType.Start)
            {
                readyNodes.Add(dependencyNode);
            }
        }

        /// <inheritdoc/>
        public IVertexIncrementalCriticalPath<T>? BeginIncrementalCriticalPath(
            IVertexGraphState<T, TResourceId, TWorkStreamId, TActivity> state)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var nodesInTopologicalOrder = new List<Node<T, TActivity>>(state.NodeCount);
            var topologicalIndexByNodeId = new Dictionary<T, int>(state.NodeCount);
            var pendingIncomingEdgeCounts = new Dictionary<T, int>(state.NodeCount);
            var ready = new Queue<Node<T, TActivity>>();

            foreach (Node<T, TActivity> node in state.Nodes)
            {
                int incomingEdgeCount = node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated
                    ? 0 : node.IncomingEdges.Count;
                pendingIncomingEdgeCounts[node.Id] = incomingEdgeCount;

                if (incomingEdgeCount == 0)
                {
                    ready.Enqueue(node);
                }
            }

            while (ready.Count != 0)
            {
                Node<T, TActivity> node = ready.Dequeue();
                topologicalIndexByNodeId[node.Id] = nodesInTopologicalOrder.Count;
                nodesInTopologicalOrder.Add(node);

                if (node.NodeType == NodeType.End || node.NodeType == NodeType.Isolated)
                {
                    continue;
                }

                foreach (T edgeId in node.OutgoingEdges)
                {
                    Node<T, TActivity> successorNode = state.EdgeHeadNode(edgeId);
                    int pendingIncomingEdges = pendingIncomingEdgeCounts[successorNode.Id] - 1;
                    pendingIncomingEdgeCounts[successorNode.Id] = pendingIncomingEdges;

                    if (pendingIncomingEdges == 0)
                    {
                        ready.Enqueue(successorNode);
                    }
                }
            }

            // A node left unordered means the walk could not reach it, which only a
            // cycle can cause. Report that by declining to start rather than by
            // throwing, so the caller falls back to the full passes and gets their
            // diagnosis instead of a second, less specific one.
            if (nodesInTopologicalOrder.Count != state.NodeCount)
            {
                return null;
            }

            return new IncrementalCriticalPath(state, nodesInTopologicalOrder, topologicalIndexByNodeId);
        }

        // The incremental recalculation.
        //
        // Between iterations of the priority-list calculation exactly one thing changes -
        // one activity's duration is reduced to zero - and very little moves with it.
        // Measured on a 2,000-activity graph, about 19 earliest start times and 99 latest
        // finish times change per iteration, so a full pass recomputes 2,000 values in
        // order to alter around 118 of them and discards the rest of the work.
        //
        // This propagates outwards from the changed activity and stops wherever a
        // recomputed value equals the one already there. That early stop is the entire
        // saving, and it is exact rather than approximate: a value that has not changed
        // cannot change anything downstream of it, because every value below depends on
        // the ones above it only through the numbers being compared here.
        //
        // The graph structure does not change during a priority-list calculation, so the
        // topological order is computed once and reused. Visiting changed nodes in that
        // order means each is recomputed at most once, with all of its predecessors
        // already final - which is what makes a single comparison per node sufficient.
        private sealed class IncrementalCriticalPath : IVertexIncrementalCriticalPath<T>
        {
            private readonly IVertexGraphState<T, TResourceId, TWorkStreamId, TActivity> m_State;
            private readonly List<Node<T, TActivity>> m_NodesInTopologicalOrder;
            private readonly Dictionary<T, int> m_TopologicalIndexByNodeId;

            // The nodes the project finish time is measured from, and where they sit in
            // the order. Held rather than asked for: the state finds them by filtering
            // every node, and both the finish time and the seeding below want them on
            // iterations that are supposed to cost nothing proportional to the graph.
            private readonly List<Node<T, TActivity>> m_EndNodes = new List<Node<T, TActivity>>();
            private readonly List<Node<T, TActivity>> m_IsolatedNodes = new List<Node<T, TActivity>>();
            private readonly List<int> m_EndNodeTopologicalIndexes = new List<int>();

            // The work lists are fields rather than locals so that an iteration allocates
            // nothing; the whole point of this class is to stop doing work per activity
            // that is proportional to the graph.
            private readonly SortedSet<int> m_PendingTopologicalIndexes = new SortedSet<int>();
            private readonly HashSet<T> m_EarliestStartTimeChanged = new HashSet<T>();
            private readonly HashSet<T> m_LatestFinishTimeChanged = new HashSet<T>();
            private readonly HashSet<T> m_FreeSlackToRecompute = new HashSet<T>();

            // The project finish time the current latest finish times were built from.
            // It has to be remembered rather than recomputed on entry, because by then the
            // caller has already changed the duration and the graph no longer holds the
            // value that is being compared against.
            private int m_EndTime;

            // Whether the forward propagation reached a node the finish time is measured
            // from, and so whether that finish time is worth recomputing at all.
            private bool m_EndOrIsolatedNodeReached;

            internal IncrementalCriticalPath(
                IVertexGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
                List<Node<T, TActivity>> nodesInTopologicalOrder,
                Dictionary<T, int> topologicalIndexByNodeId)
            {
                m_State = state;
                m_NodesInTopologicalOrder = nodesInTopologicalOrder;
                m_TopologicalIndexByNodeId = topologicalIndexByNodeId;

                foreach (Node<T, TActivity> node in nodesInTopologicalOrder)
                {
                    if (node.NodeType == NodeType.End)
                    {
                        m_EndNodes.Add(node);
                        m_EndNodeTopologicalIndexes.Add(m_TopologicalIndexByNodeId[node.Id]);
                    }
                    else if (node.NodeType == NodeType.Isolated)
                    {
                        m_IsolatedNodes.Add(node);
                    }
                }

                m_EndTime = EndTime();
            }

            /// <inheritdoc/>
            public bool ApplyDurationChange(T activityId)
            {
                if (!m_TopologicalIndexByNodeId.TryGetValue(activityId, out int changedNodeIndex))
                {
                    return false;
                }

                m_EarliestStartTimeChanged.Clear();
                m_LatestFinishTimeChanged.Clear();
                m_FreeSlackToRecompute.Clear();
                m_PendingTopologicalIndexes.Clear();

                // Forwards, in topological order.
                m_EndOrIsolatedNodeReached = false;
                m_PendingTopologicalIndexes.Add(changedNodeIndex);

                while (m_PendingTopologicalIndexes.Count != 0)
                {
                    int index = m_PendingTopologicalIndexes.Min;
                    m_PendingTopologicalIndexes.Remove(index);
                    PropagateEarliestTimes(m_NodesInTopologicalOrder[index]);
                }

                // The project finish time is only worth recomputing if the propagation
                // reached something it is measured from. On a graph whose last layer is
                // wide, that scan is otherwise the one part of an iteration that stays
                // proportional to the graph.
                bool endTimeChanged = false;

                if (m_EndOrIsolatedNodeReached)
                {
                    int endTime = EndTime();
                    endTimeChanged = endTime != m_EndTime;
                    m_EndTime = endTime;
                }

                // Backwards, in reverse topological order, seeded by the changed activity -
                // its duration feeds the latest finish times of everything before it.
                //
                // A moved finish time is seeded rather than surrendered to. It changes
                // every End node's latest finish time and so, in principle, the whole
                // graph; but only in principle, and the propagation stops wherever a value
                // has not actually moved just as it does anywhere else. This matters more
                // than it looks: the selection picks the activity with the least total
                // slack, which is one on the critical path, so shortening it moves the
                // finish time far more often than the share of iterations measured on
                // shallow graphs would suggest - and the deeper the graph, the more often.
                m_PendingTopologicalIndexes.Add(changedNodeIndex);

                if (endTimeChanged)
                {
                    foreach (int endNodeIndex in m_EndNodeTopologicalIndexes)
                    {
                        m_PendingTopologicalIndexes.Add(endNodeIndex);
                    }
                }

                while (m_PendingTopologicalIndexes.Count != 0)
                {
                    int index = m_PendingTopologicalIndexes.Max;
                    m_PendingTopologicalIndexes.Remove(index);
                    PropagateLatestTimes(m_NodesInTopologicalOrder[index]);
                }

                RecomputeFreeSlack(activityId);
                return true;
            }

            // The forward pass's finish value for the End and Isolated nodes, which is
            // what it feeds the backward pass. The End nodes' stored latest finish time
            // cannot be read back for this, because the backward pass overwrites it with a
            // value derived from this one - so it is recomputed from the earliest finish
            // time, exactly as the forward pass computes it.
            private int EndTime()
            {
                int endNodesEndTime = 0;
                bool anyEndNode = false;

                foreach (Node<T, TActivity> node in m_EndNodes)
                {
                    int latestFinishTime =
                        AugmentedFinishTime(node.Content, node.Content.EarliestFinishTime!.Value);

                    if (!anyEndNode || latestFinishTime > endNodesEndTime)
                    {
                        endNodesEndTime = latestFinishTime;
                        anyEndNode = true;
                    }
                }

                int isolatedNodesEndTime = 0;
                bool anyIsolatedNode = false;

                foreach (Node<T, TActivity> node in m_IsolatedNodes)
                {
                    int latestFinishTime = node.Content.LatestFinishTime!.Value;

                    if (!anyIsolatedNode || latestFinishTime > isolatedNodesEndTime)
                    {
                        isolatedNodesEndTime = latestFinishTime;
                        anyIsolatedNode = true;
                    }
                }

                return Math.Max(endNodesEndTime, isolatedNodesEndTime);
            }

            private void PropagateEarliestTimes(Node<T, TActivity> node)
            {
                int earliestStartTime = ClampEarliestStartTime(
                    node.Content,
                    node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated
                        ? 0
                        : MaxEdgeEarliestFinishTime(m_State, node.IncomingEdges));

                if (node.Content.EarliestStartTime != earliestStartTime)
                {
                    node.Content.EarliestStartTime = earliestStartTime;
                    m_EarliestStartTimeChanged.Add(node.Id);
                }

                if (node.NodeType == NodeType.Isolated)
                {
                    // An Isolated node's latest finish time comes from the forward pass and
                    // is never revisited by the backward one, so it is maintained here.
                    node.Content.LatestFinishTime =
                        AugmentedFinishTime(node.Content, node.Content.EarliestFinishTime!.Value);
                    m_EndOrIsolatedNodeReached = true;
                    return;
                }

                if (node.NodeType == NodeType.End)
                {
                    m_EndOrIsolatedNodeReached = true;
                    return;
                }

                // Every outgoing edge of a node is given the same value, so one comparison
                // decides whether anything downstream needs revisiting. The value is
                // recomputed even when the earliest start time did not move, because the
                // changed activity's own duration feeds it directly.
                int edgeEarliestFinishTime = node.NodeType == NodeType.Start
                    ? StartNodeEdgeEarliestFinishTime(node.Content, node.Content.EarliestFinishTime!.Value)
                    : AugmentedFinishTime(node.Content, node.Content.EarliestFinishTime!.Value);

                foreach (T edgeId in node.OutgoingEdges)
                {
                    Edge<T, IEvent<T>> edge = m_State.Edge(edgeId);

                    if (edge.Content.EarliestFinishTime == edgeEarliestFinishTime)
                    {
                        continue;
                    }

                    edge.Content.EarliestFinishTime = edgeEarliestFinishTime;
                    m_PendingTopologicalIndexes.Add(
                        m_TopologicalIndexByNodeId[m_State.EdgeHeadNode(edgeId).Id]);
                }
            }

            private void PropagateLatestTimes(Node<T, TActivity> node)
            {
                // An End node takes its latest finish time from the project finish time;
                // everything else takes it from the edges leaving it. An Isolated node has
                // neither, and keeps the value the forward propagation gave it.
                if (node.NodeType != NodeType.Isolated)
                {
                    int latestFinishTime = node.NodeType == NodeType.End
                        ? ClampLatestFinishTime(node.Content, m_EndTime)
                        : ClampLatestFinishTime(
                            node.Content,
                            MinEdgeLatestFinishTime(m_State, node.OutgoingEdges));

                    if (node.Content.LatestFinishTime != latestFinishTime)
                    {
                        node.Content.LatestFinishTime = latestFinishTime;
                        m_LatestFinishTimeChanged.Add(node.Id);
                    }
                }

                if (node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated)
                {
                    return;
                }

                int? edgeLatestFinishTime = node.NodeType == NodeType.End
                    ? EndNodeIncomingEdgeLatestFinishTime(node.Content)
                    : node.Content.LatestStartTime;

                foreach (T edgeId in node.IncomingEdges)
                {
                    Edge<T, IEvent<T>> edge = m_State.Edge(edgeId);

                    if (edge.Content.LatestFinishTime == edgeLatestFinishTime)
                    {
                        continue;
                    }

                    edge.Content.LatestFinishTime = edgeLatestFinishTime;
                    m_PendingTopologicalIndexes.Add(
                        m_TopologicalIndexByNodeId[m_State.EdgeTailNode(edgeId).Id]);
                }
            }

            // Free slack is tracked separately from the two flows because it depends on
            // both, and on the successors as well as on the node itself: a node's free
            // slack moves if its own earliest start, latest finish or duration moved, or
            // if any of its successors' earliest start times did.
            //
            // The priority-list selection does not read free slack - it works from total
            // slack, which is derived from the two flows - so this is not needed to get
            // the list right. It is maintained so that an incrementally updated graph is
            // indistinguishable from a fully recalculated one, which is what lets the two
            // be compared value for value.
            private void RecomputeFreeSlack(T changedActivityId)
            {
                m_FreeSlackToRecompute.Add(changedActivityId);

                foreach (T nodeId in m_LatestFinishTimeChanged)
                {
                    m_FreeSlackToRecompute.Add(nodeId);
                }

                foreach (T nodeId in m_EarliestStartTimeChanged)
                {
                    m_FreeSlackToRecompute.Add(nodeId);

                    Node<T, TActivity> node = m_State.Node(nodeId);

                    if (node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated)
                    {
                        continue;
                    }

                    foreach (T edgeId in node.IncomingEdges)
                    {
                        m_FreeSlackToRecompute.Add(m_State.EdgeTailNode(edgeId).Id);
                    }
                }

                foreach (T nodeId in m_FreeSlackToRecompute)
                {
                    Node<T, TActivity> node = m_State.Node(nodeId);

                    if (node.NodeType == NodeType.Isolated)
                    {
                        // Left alone, as the full passes leave it: an Isolated node's free
                        // slack is filled in afterwards by BackFillIsolatedNodes.
                        continue;
                    }

                    node.Content.FreeSlack = node.NodeType == NodeType.End
                        ? node.Content.LatestFinishTime - node.Content.EarliestFinishTime
                        : FreeSlackFromSuccessors(
                            node.Content,
                            MinSuccessorEarliestStartTime(m_State, node.OutgoingEdges));
                }
            }
        }

        // The value helpers below are the arithmetic of the two flows, factored out so
        // that the full passes above and the incremental recalculation below compute from
        // one copy rather than two. Each reproduces its original statements verbatim,
        // including where a null propagates and where GetValueOrDefault does not.

        // The earliest start time of a node, given the value implied by its predecessors
        // (zero for Start and Isolated nodes). Note that this depends on the duration as
        // well as on the predecessors, through the maximum-latest-finish clamp - so
        // shortening an activity can raise its own earliest start.
        private static int ClampEarliestStartTime(TActivity activity, int earliestStartTime)
        {
            if (activity.MinimumEarliestStartTime.HasValue)
            {
                int proposedEarliestStartTime = activity.MinimumEarliestStartTime.Value;

                // Augment the earliest start time artificially (if required).
                if (proposedEarliestStartTime > earliestStartTime)
                {
                    earliestStartTime = proposedEarliestStartTime;
                }
            }

            if (activity.MaximumLatestFinishTime.HasValue)
            {
                int proposedLatestStartTime = activity.MaximumLatestFinishTime.Value - activity.Duration;

                // Diminish the earliest start time artificially (if required).
                if (proposedLatestStartTime < earliestStartTime)
                {
                    earliestStartTime = proposedLatestStartTime;
                }
            }

            return earliestStartTime;
        }

        // The finish time a node hands on: diminished by its maximum, or else augmented by
        // its minimum free slack. This is the value given to a Normal node's outgoing
        // edges, and also the latest finish time the forward pass gives End and Isolated
        // nodes - the same three statements in all three places.
        private static int AugmentedFinishTime(TActivity activity, int finishTime)
        {
            if (activity.MaximumLatestFinishTime.HasValue)
            {
                int proposedFinishTime = activity.MaximumLatestFinishTime.Value;

                // Diminish the finish time artificially (if required).
                if (proposedFinishTime < finishTime)
                {
                    finishTime = proposedFinishTime;
                }
            }
            else if (activity.MinimumFreeSlack.HasValue)
            {
                int proposedFinishTime = finishTime + activity.MinimumFreeSlack.Value;

                // Augment the finish time artificially (if required).
                if (proposedFinishTime > finishTime)
                {
                    finishTime = proposedFinishTime;
                }
            }

            return finishTime;
        }

        // The Start-node variant of the above, which applies the minimum free slack
        // without the maximum diminishment. The asymmetry is deliberate and long-standing.
        private static int StartNodeEdgeEarliestFinishTime(TActivity activity, int finishTime)
        {
            if (activity.MinimumFreeSlack.HasValue)
            {
                int proposedFinishTime = finishTime + activity.MinimumFreeSlack.Value;

                // Augment the finish time artificially (if required).
                if (proposedFinishTime > finishTime)
                {
                    finishTime = proposedFinishTime;
                }
            }

            return finishTime;
        }

        // A latest finish time diminished by the node's maximum.
        private static int ClampLatestFinishTime(TActivity activity, int latestFinishTime)
        {
            if (activity.MaximumLatestFinishTime.HasValue)
            {
                int proposedLatestFinishTime = activity.MaximumLatestFinishTime.Value;

                // Diminish the latest finish time artificially (if required).
                if (proposedLatestFinishTime < latestFinishTime)
                {
                    latestFinishTime = proposedLatestFinishTime;
                }
            }

            return latestFinishTime;
        }

        // Free slack for Start and Normal nodes, from the earliest start times of their
        // successors. Stays nullable arithmetic because the original was: a node with no
        // earliest start time takes no free slack rather than taking it from zero.
        private static int? FreeSlackFromSuccessors(TActivity activity, int minSuccessorEarliestStartTime)
        {
            int latestFinishTime = minSuccessorEarliestStartTime;

            if (activity.LatestFinishTime.HasValue)
            {
                int proposedLatestFinishTime = activity.LatestFinishTime.Value;

                // Diminish the latest finish time artificially (if required).
                if (proposedLatestFinishTime < latestFinishTime)
                {
                    latestFinishTime = proposedLatestFinishTime;
                }
            }

            if (activity.MaximumLatestFinishTime.HasValue)
            {
                int proposedLatestFinishTime = activity.MaximumLatestFinishTime.Value;

                // Diminish the latest finish time artificially (if required).
                if (proposedLatestFinishTime < latestFinishTime)
                {
                    latestFinishTime = proposedLatestFinishTime;
                }
            }

            // Free float/slack calculations.
            return latestFinishTime - activity.EarliestStartTime - activity.Duration;
        }

        // The latest finish time an End node gives its incoming edges. The comparison is
        // against GetValueOrDefault, so a node without a latest start time is treated as
        // starting at zero here - which is not the same as propagating the null.
        private static int? EndNodeIncomingEdgeLatestFinishTime(TActivity activity)
        {
            int? latestFinishTime = activity.LatestStartTime;

            if (activity.MaximumLatestFinishTime.HasValue)
            {
                int proposedLatestFinishTime = activity.MaximumLatestFinishTime.Value;

                // Diminish the latest finish time artificially (if required).
                if (proposedLatestFinishTime < latestFinishTime.GetValueOrDefault())
                {
                    latestFinishTime = proposedLatestFinishTime;
                }
            }

            return latestFinishTime;
        }

        // The aggregate helpers below replace LINQ chains that sat on the hot path. Each
        // was allocating an enumerator and a closure for every node on every pass, and
        // the priority-list calculation runs a pass per activity. They return zero for an
        // empty edge set, matching the DefaultIfEmpty the Start and End node passes used;
        // the main loops only ever call them for nodes that have at least one edge.
        private static int MaxEdgeEarliestFinishTime(
            IVertexGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            HashSet<T> edgeIds)
        {
            bool found = false;
            int maximum = 0;
            foreach (T edgeId in edgeIds)
            {
                int earliestFinishTime = state.Edge(edgeId).Content.EarliestFinishTime!.Value;
                if (!found || earliestFinishTime > maximum)
                {
                    maximum = earliestFinishTime;
                    found = true;
                }
            }
            return maximum;
        }

        private static int MinEdgeLatestFinishTime(
            IVertexGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            HashSet<T> edgeIds)
        {
            bool found = false;
            int minimum = 0;
            foreach (T edgeId in edgeIds)
            {
                int latestFinishTime = state.Edge(edgeId).Content.LatestFinishTime!.Value;
                if (!found || latestFinishTime < minimum)
                {
                    minimum = latestFinishTime;
                    found = true;
                }
            }
            return minimum;
        }

        private static int MinSuccessorEarliestStartTime(
            IVertexGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            HashSet<T> edgeIds)
        {
            bool found = false;
            int minimum = 0;
            foreach (T edgeId in edgeIds)
            {
                int earliestStartTime = state.EdgeHeadNode(edgeId).Content.EarliestStartTime!.Value;
                if (!found || earliestStartTime < minimum)
                {
                    minimum = earliestStartTime;
                    found = true;
                }
            }
            return minimum;
        }

        private static bool AllEdgesHaveEarliestFinishTime(
            IVertexGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            HashSet<T> edgeIds)
        {
            foreach (T edgeId in edgeIds)
            {
                if (!state.Edge(edgeId).Content.EarliestFinishTime.HasValue)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool AllEdgesHaveLatestFinishTime(
            IVertexGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            HashSet<T> edgeIds)
        {
            foreach (T edgeId in edgeIds)
            {
                if (!state.Edge(edgeId).Content.LatestFinishTime.HasValue)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
