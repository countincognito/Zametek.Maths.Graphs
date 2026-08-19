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
                int earliestStartTime = 0;

                if (node.Content.MinimumEarliestStartTime.HasValue)
                {
                    int proposedEarliestStartTime = node.Content.MinimumEarliestStartTime.Value;

                    // Augment the earliest start time artificially (if required).
                    if (proposedEarliestStartTime > earliestStartTime)
                    {
                        earliestStartTime = proposedEarliestStartTime;
                    }
                }

                if (node.Content.MaximumLatestFinishTime.HasValue)
                {
                    int proposedLatestStartTime = node.Content.MaximumLatestFinishTime.Value - node.Content.Duration;

                    // Diminish the earliest start time artificially (if required).
                    if (proposedLatestStartTime < earliestStartTime)
                    {
                        earliestStartTime = proposedLatestStartTime;
                    }
                }

                node.Content.EarliestStartTime = earliestStartTime;

                // Latest Finish Time.
                int latestFinishTime = node.Content.EarliestFinishTime!.Value;

                if (node.Content.MaximumLatestFinishTime.HasValue)
                {
                    int proposedLatestFinishTime = node.Content.MaximumLatestFinishTime.Value;

                    // Diminish the latest finish time artificially (if required).
                    if (proposedLatestFinishTime < latestFinishTime)
                    {
                        latestFinishTime = proposedLatestFinishTime;
                    }
                }
                else if (node.Content.MinimumFreeSlack.HasValue)
                {
                    int proposedLatestFinishTime = latestFinishTime + node.Content.MinimumFreeSlack.Value;

                    // Augment the latest finish time artificially (if required).
                    if (proposedLatestFinishTime > latestFinishTime)
                    {
                        latestFinishTime = proposedLatestFinishTime;
                    }
                }

                node.Content.LatestFinishTime = latestFinishTime;
            }

            // Complete the Start nodes first to ensure the completed edge IDs contains something.
            foreach (Node<T, TActivity> node in state.StartNodes)
            {
                int earliestStartTime = 0;

                if (node.Content.MinimumEarliestStartTime.HasValue)
                {
                    int proposedEarliestStartTime = node.Content.MinimumEarliestStartTime.Value;

                    // Augment the earliest start time artificially (if required).
                    if (proposedEarliestStartTime > earliestStartTime)
                    {
                        earliestStartTime = proposedEarliestStartTime;
                    }
                }

                if (node.Content.MaximumLatestFinishTime.HasValue)
                {
                    int proposedLatestStartTime = node.Content.MaximumLatestFinishTime.Value - node.Content.Duration;

                    // Diminish the earliest start time artificially (if required).
                    if (proposedLatestStartTime < earliestStartTime)
                    {
                        earliestStartTime = proposedLatestStartTime;
                    }
                }

                node.Content.EarliestStartTime = earliestStartTime;

                foreach (T outgoingEdgeId in node.OutgoingEdges)
                {
                    Edge<T, IEvent<T>> outgoingEdge = state.Edge(outgoingEdgeId);
                    int earliestFinishTime = node.Content.EarliestFinishTime!.Value;

                    if (node.Content.MinimumFreeSlack.HasValue)
                    {
                        int proposedEarliestFinishTime = earliestFinishTime + node.Content.MinimumFreeSlack.Value;

                        // Augment the earliest finish time artificially (if required).
                        if (proposedEarliestFinishTime > earliestFinishTime)
                        {
                            earliestFinishTime = proposedEarliestFinishTime;
                        }
                    }

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
                        int earliestStartTime = MaxEdgeEarliestFinishTime(state, dependencyNode.IncomingEdges);

                        if (dependencyNode.Content.MinimumEarliestStartTime.HasValue)
                        {
                            int proposedEarliestStartTime = dependencyNode.Content.MinimumEarliestStartTime.Value;

                            // Augment the earliest start time artificially (if required).
                            if (proposedEarliestStartTime > earliestStartTime)
                            {
                                earliestStartTime = proposedEarliestStartTime;
                            }
                        }

                        if (dependencyNode.Content.MaximumLatestFinishTime.HasValue)
                        {
                            int proposedLatestStartTime = dependencyNode.Content.MaximumLatestFinishTime.Value - dependencyNode.Content.Duration;

                            // Diminish the earliest start time artificially (if required).
                            if (proposedLatestStartTime < earliestStartTime)
                            {
                                earliestStartTime = proposedLatestStartTime;
                            }
                        }

                        dependencyNode.Content.EarliestStartTime = earliestStartTime;
                    }

                    int earliestFinishTime = dependencyNode.Content.EarliestFinishTime!.Value;

                    if (dependencyNode.Content.MaximumLatestFinishTime.HasValue)
                    {
                        int proposedLatestFinishTime = dependencyNode.Content.MaximumLatestFinishTime.Value;

                        // Diminish the earliest finish time artificially (if required).
                        if (proposedLatestFinishTime < earliestFinishTime)
                        {
                            earliestFinishTime = proposedLatestFinishTime;
                        }
                    }
                    else if (dependencyNode.Content.MinimumFreeSlack.HasValue)
                    {
                        int proposedEarliestFinishTime = earliestFinishTime + dependencyNode.Content.MinimumFreeSlack.Value;

                        // Augment the earliest finish time artificially (if required).
                        if (proposedEarliestFinishTime > earliestFinishTime)
                        {
                            earliestFinishTime = proposedEarliestFinishTime;
                        }
                    }

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
                    int earliestStartTime = MaxEdgeEarliestFinishTime(state, node.IncomingEdges);

                    if (node.Content.MinimumEarliestStartTime.HasValue)
                    {
                        int proposedEarliestStartTime = node.Content.MinimumEarliestStartTime.Value;

                        // Augment the earliest start time artificially (if required).
                        if (proposedEarliestStartTime > earliestStartTime)
                        {
                            earliestStartTime = proposedEarliestStartTime;
                        }
                    }

                    if (node.Content.MaximumLatestFinishTime.HasValue)
                    {
                        int proposedLatestStartTime = node.Content.MaximumLatestFinishTime.Value - node.Content.Duration;

                        // Diminish the earliest start time artificially (if required).
                        if (proposedLatestStartTime < earliestStartTime)
                        {
                            earliestStartTime = proposedLatestStartTime;
                        }
                    }

                    node.Content.EarliestStartTime = earliestStartTime;
                }

                if (!node.Content.LatestFinishTime.HasValue)
                {
                    int latestFinishTime = node.Content.EarliestFinishTime!.Value;

                    if (node.Content.MaximumLatestFinishTime.HasValue)
                    {
                        int proposedLatestFinishTime = node.Content.MaximumLatestFinishTime.Value;

                        // Diminish the latest finish time artificially (if required).
                        if (proposedLatestFinishTime < latestFinishTime)
                        {
                            latestFinishTime = proposedLatestFinishTime;
                        }
                    }
                    else if (node.Content.MinimumFreeSlack.HasValue)
                    {
                        int proposedLatestFinishTime = latestFinishTime + node.Content.MinimumFreeSlack.Value;

                        // Augment the latest finish time artificially (if required).
                        if (proposedLatestFinishTime > latestFinishTime)
                        {
                            latestFinishTime = proposedLatestFinishTime;
                        }
                    }

                    node.Content.LatestFinishTime = latestFinishTime;
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
                }

                // Free float/slack calculations.
                node.Content.FreeSlack = node.Content.LatestFinishTime - node.Content.EarliestFinishTime;

                foreach (T incomingEdgeId in node.IncomingEdges)
                {
                    Edge<T, IEvent<T>> incomingEdge = state.Edge(incomingEdgeId);
                    int? latestFinishTime = node.Content.LatestStartTime;

                    if (node.Content.MaximumLatestFinishTime.HasValue)
                    {
                        int proposedLatestFinishTime = node.Content.MaximumLatestFinishTime.Value;

                        // Diminish the latest finish time artificially (if required).
                        if (proposedLatestFinishTime < latestFinishTime.GetValueOrDefault())
                        {
                            latestFinishTime = proposedLatestFinishTime;
                        }
                    }

                    incomingEdge.Content.LatestFinishTime = latestFinishTime;
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
                    {
                        if (!successorNode.Content.LatestFinishTime.HasValue)
                        {
                            int latestFinishTime = MinEdgeLatestFinishTime(state, successorNode.OutgoingEdges);

                            if (successorNode.Content.MaximumLatestFinishTime.HasValue)
                            {
                                int proposedLatestFinishTime = successorNode.Content.MaximumLatestFinishTime.Value;

                                // Diminish the latest finish time artificially (if required).
                                if (proposedLatestFinishTime < latestFinishTime)
                                {
                                    latestFinishTime = proposedLatestFinishTime;
                                }
                            }

                            successorNode.Content.LatestFinishTime = latestFinishTime;
                        }

                        if (!successorNode.Content.FreeSlack.HasValue)
                        {
                            int latestFinishTime = MinSuccessorEarliestStartTime(state, successorNode.OutgoingEdges);

                            if (successorNode.Content.LatestFinishTime.HasValue)
                            {
                                int proposedLatestFinishTime = successorNode.Content.LatestFinishTime.Value;

                                // Diminish the latest finish time artificially (if required).
                                if (proposedLatestFinishTime < latestFinishTime)
                                {
                                    latestFinishTime = proposedLatestFinishTime;
                                }
                            }

                            if (successorNode.Content.MaximumLatestFinishTime.HasValue)
                            {
                                int proposedLatestFinishTime = successorNode.Content.MaximumLatestFinishTime.Value;

                                // Diminish the latest finish time artificially (if required).
                                if (proposedLatestFinishTime < latestFinishTime)
                                {
                                    latestFinishTime = proposedLatestFinishTime;
                                }
                            }

                            // Free float/slack calculations.
                            successorNode.Content.FreeSlack = latestFinishTime - successorNode.Content.EarliestStartTime - successorNode.Content.Duration;
                        }

                        // Only End nodes had their incoming edges completed beforehand,
                        // and a node is processed once, so every incoming edge here is
                        // still outstanding.
                        foreach (T edgeId in successorNode.IncomingEdges)
                        {
                            Edge<T, IEvent<T>> edge = state.Edge(edgeId);
                            edge.Content.LatestFinishTime = successorNode.Content.LatestStartTime;
                            outstandingEdgeCount--;
                            ReleasePredecessor(state, edgeId, pendingOutgoingEdgeCounts, nextReadyNodes);
                        }
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
                    int latestFinishTime = MinEdgeLatestFinishTime(state, node.OutgoingEdges);

                    if (node.Content.LatestFinishTime.HasValue)
                    {
                        int proposedLatestFinishTime = node.Content.LatestFinishTime.Value;

                        // Diminish the latest finish time artificially (if required).
                        if (proposedLatestFinishTime < latestFinishTime)
                        {
                            latestFinishTime = proposedLatestFinishTime;
                        }
                    }

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
                }

                if (!node.Content.FreeSlack.HasValue)
                {
                    int latestFinishTime = MinSuccessorEarliestStartTime(state, node.OutgoingEdges);

                    if (node.Content.LatestFinishTime.HasValue)
                    {
                        int proposedLatestFinishTime = node.Content.LatestFinishTime.Value;

                        // Diminish the latest finish time artificially (if required).
                        if (proposedLatestFinishTime < latestFinishTime)
                        {
                            latestFinishTime = proposedLatestFinishTime;
                        }
                    }

                    if (node.Content.MaximumLatestFinishTime.HasValue)
                    {
                        int proposedLatestFinishTime = node.Content.MaximumLatestFinishTime.Value;

                        // Diminish the latest finish time artificially (if required).
                        if (proposedLatestFinishTime < latestFinishTime)
                        {
                            latestFinishTime = proposedLatestFinishTime;
                        }
                    }

                    // Free float/slack calculations.
                    node.Content.FreeSlack = latestFinishTime - node.Content.EarliestStartTime - node.Content.Duration;
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
