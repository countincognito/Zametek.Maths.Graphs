using System;
using System.Collections.Generic;
using System.Linq;
using Zametek.Utility;

namespace Zametek.Maths.Graphs
{
    // Calculates the critical path for Activity-on-Arrow graphs.
    // Implements the forward pass (earliest event finish times), backward pass
    // (latest event finish times), and per-activity critical path variable calculation.
    // Operates on the shared ArrowGraphState passed to each method.
    /// <summary>
    /// Default critical-path engine for Activity-on-Arrow graphs.
    /// </summary>
    public sealed class ArrowCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity>
        : IArrowCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
        where TActivity : class, IActivity<T, TResourceId, TWorkStreamId>
    {
        /// <inheritdoc/>
        public bool CalculateEventEarliestFinishTimes(
            IArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
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
            if (state.StartNode is null)
            {
                throw new InvalidOperationException(Properties.Resources.Message_ArrowGraphStateHasNoStartNode);
            }
            if (state.EndNode is null)
            {
                throw new InvalidOperationException(Properties.Resources.Message_ArrowGraphStateHasNoEndNode);
            }

            if (invalidConstraints.Count != 0)
            {
                return false;
            }

            Node<T, IEvent<T>> startNode = state.StartNode;
            // For later when checking the Maximum LFT.
            Node<T, IEvent<T>> endNode = state.EndNode;

            // An event can be given its earliest finish time once every event it depends
            // on - the tail of each of its incoming edges - has been given one. Rather
            // than sweeping the remaining events repeatedly to find which have become
            // ready, each event counts how many incoming edges it is still waiting on and
            // is queued the moment that count reaches zero. Only the Start node is
            // excluded: it depends on nothing and is completed below.
            var pendingIncomingEdgeCounts = new Dictionary<T, int>(state.NodeCount);
            bool startNodePresent = false;

            foreach (Node<T, IEvent<T>> node in state.Nodes)
            {
                if (node == startNode)
                {
                    startNodePresent = true;
                    continue;
                }
                pendingIncomingEdgeCounts[node.Id] = node.IncomingEdges.Count;
            }

            // Make sure the graph contains the Start node.
            if (!startNodePresent)
            {
                return false;
            }

            var readyNodes = new List<Node<T, IEvent<T>>>();

            // An event with no incoming edges at all is ready from the outset, and nothing
            // will ever release an edge into it to say so. The sweep this replaces reached
            // such an event on its first pass, so it is queued here to match.
            foreach (KeyValuePair<T, int> pendingIncomingEdgeCount in pendingIncomingEdgeCounts)
            {
                if (pendingIncomingEdgeCount.Value == 0)
                {
                    readyNodes.Add(state.Node(pendingIncomingEdgeCount.Key));
                }
            }

            // Complete the Start node first, which seeds the first round.
            // Earliest Start Time.
            startNode.Content.EarliestFinishTime = 0;
            int completedNodeCount = 1;

            foreach (T outgoingEdgeId in startNode.OutgoingEdges)
            {
                ReleaseSuccessorEvent(state, outgoingEdgeId, pendingIncomingEdgeCounts, readyNodes);
            }

            // Forward flow algorithm.
            //
            // This walks the events in dependency order, visiting each once, instead of
            // sweeping the remaining set repeatedly at O(depth x V) per pass. The value an
            // event receives is still aggregated over all of its incoming edges, exactly as
            // before, and an event is still only given a value once every event it depends
            // on has one - so the numbers are unchanged.
            //
            // Events are handled in rounds, and the shuffle hook shuffles each round as
            // well as each event's incoming edges. Within a round the order genuinely
            // cannot matter - that is what ShuffleProcessingOrder exists to demonstrate.
            while (readyNodes.Count != 0)
            {
                if (shuffle)
                {
                    readyNodes.Shuffle();
                }

                var nextReadyNodes = new List<Node<T, IEvent<T>>>();

                foreach (Node<T, IEvent<T>> node in readyNodes)
                {
                    // Get the incoming edges and the dependency nodes IDs.
                    List<T> incomingEdges = new List<T>(node.IncomingEdges);

                    if (shuffle)
                    {
                        incomingEdges.Shuffle();
                    }

                    int earliestFinishTime = 0;

                    foreach (T incomingEdgeId in incomingEdges)
                    {
                        Edge<T, TActivity> incomingEdge = state.Edge(incomingEdgeId);
                        Node<T, IEvent<T>> incomingEdgeTailNode = state.EdgeTailNode(incomingEdgeId);

                        if (incomingEdgeTailNode.Content.EarliestFinishTime.HasValue)
                        {
                            int proposedEarliestFinishTime = incomingEdgeTailNode.Content.EarliestFinishTime.Value + incomingEdge.Content.Duration;

                            proposedEarliestFinishTime += incomingEdge.Content.MinimumFreeSlack.GetValueOrDefault();

                            // Augment the earliest finish time artificially (if required).
                            if (proposedEarliestFinishTime > earliestFinishTime)
                            {
                                earliestFinishTime = proposedEarliestFinishTime;
                            }
                        }

                        if (incomingEdge.Content.MinimumEarliestStartTime.HasValue)
                        {
                            int proposedEarliestFinishTime = incomingEdge.Content.MinimumEarliestStartTime.Value + incomingEdge.Content.Duration;

                            // Augment the earliest finish time artificially (if required).
                            if (proposedEarliestFinishTime > earliestFinishTime)
                            {
                                earliestFinishTime = proposedEarliestFinishTime;
                            }
                        }

                        // It is only necessary to check the Maximum LFT if the head node is not the
                        // EndNode, and if the tail node is not the StartNode.
                        // Otherwise, it ends up imposing an LFT value that is unnecessarily constrained
                        // without any good reason (i.e. there is nothing before the StartNode or after
                        // the EndNode to be impacted by the constraint).
                        if (node != endNode && incomingEdgeTailNode != startNode)
                        {
                            if (incomingEdge.Content.MaximumLatestFinishTime.HasValue)
                            {
                                int proposedLatestFinishTime = incomingEdge.Content.MaximumLatestFinishTime.Value;

                                // Diminish the earliest finish time artificially (if required).
                                if (proposedLatestFinishTime < earliestFinishTime)
                                {
                                    earliestFinishTime = proposedLatestFinishTime;
                                }
                            }
                        }
                    }

                    node.Content.EarliestFinishTime = earliestFinishTime;
                    completedNodeCount++;

                    // An End node has no outgoing edges to carry the flow onwards, and
                    // asking it for them throws.
                    if (node.NodeType != NodeType.End)
                    {
                        foreach (T outgoingEdgeId in node.OutgoingEdges)
                        {
                            ReleaseSuccessorEvent(state, outgoingEdgeId, pendingIncomingEdgeCounts, nextReadyNodes);
                        }
                    }
                }

                readyNodes = nextReadyNodes;
            }

            // If some events were never reached once nothing more can become ready, then a
            // cycle must exist in the graph and we will not be able to calculate the
            // earliest finish times.
            if (completedNodeCount != state.NodeCount)
            {
                throw new InvalidOperationException(Properties.Resources.Message_CannotCalculateEarliestFinishTimesDueToCyclicDependency);
            }
            return true;
        }

        /// <inheritdoc/>
        public bool CalculateEventLatestFinishTimes(
            IArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
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
            if (state.EndNode is null)
            {
                throw new InvalidOperationException(Properties.Resources.Message_ArrowGraphStateHasNoEndNode);
            }

            if (invalidConstraints.Count != 0)
            {
                return false;
            }

            // Only perform if all events have earliest finish times.
            if (!state.Nodes.All(x => x.Content.EarliestFinishTime.HasValue))
            {
                return false;
            }

            Node<T, IEvent<T>> endNode = state.EndNode;

            // The mirror image of the forward flow: an event can be given its latest finish
            // time once every event that depends on it - the head of each of its outgoing
            // edges - has been given one. Only the End node is excluded; nothing depends on
            // it and it is completed below.
            var pendingOutgoingEdgeCounts = new Dictionary<T, int>(state.NodeCount);
            bool endNodePresent = false;

            foreach (Node<T, IEvent<T>> node in state.Nodes)
            {
                if (node == endNode)
                {
                    endNodePresent = true;
                    continue;
                }
                pendingOutgoingEdgeCounts[node.Id] = node.OutgoingEdges.Count;
            }

            // Make sure the graph contains the End node.
            if (!endNodePresent)
            {
                return false;
            }

            // Complete the End node first, which seeds the first round.
            endNode.Content.LatestFinishTime = endNode.Content.EarliestFinishTime;

            if (!endNode.Content.LatestFinishTime.HasValue)
            {
                return false;
            }

            int endNodeLatestFinishTime = endNode.Content.LatestFinishTime.Value;
            int completedNodeCount = 1;
            var readyNodes = new List<Node<T, IEvent<T>>>();

            // An event that nothing depends on is ready from the outset, for the same
            // reason its forward-flow counterpart is.
            foreach (KeyValuePair<T, int> pendingOutgoingEdgeCount in pendingOutgoingEdgeCounts)
            {
                if (pendingOutgoingEdgeCount.Value == 0)
                {
                    readyNodes.Add(state.Node(pendingOutgoingEdgeCount.Key));
                }
            }

            foreach (T incomingEdgeId in endNode.IncomingEdges)
            {
                ReleasePredecessorEvent(state, incomingEdgeId, pendingOutgoingEdgeCounts, readyNodes);
            }

            // Backward flow algorithm - the reverse-order walk matching the forward flow
            // above, visiting each event once rather than sweeping the remaining set.
            while (readyNodes.Count != 0)
            {
                if (shuffle)
                {
                    readyNodes.Shuffle();
                }

                var nextReadyNodes = new List<Node<T, IEvent<T>>>();

                foreach (Node<T, IEvent<T>> node in readyNodes)
                {
                    // Get the outgoing edges and the successor nodes IDs.
                    List<T> outgoingEdges = new List<T>(node.OutgoingEdges);

                    if (shuffle)
                    {
                        outgoingEdges.Shuffle();
                    }

                    int latestFinishTime = endNodeLatestFinishTime;

                    foreach (T outgoingEdgeId in outgoingEdges)
                    {
                        Edge<T, TActivity> outgoingEdge = state.Edge(outgoingEdgeId);
                        Node<T, IEvent<T>> outgoingEdgeHeadNode = state.EdgeHeadNode(outgoingEdgeId);

                        if (outgoingEdgeHeadNode.Content.LatestFinishTime.HasValue)
                        {
                            int proposedLatestFinishTime = outgoingEdgeHeadNode.Content.LatestFinishTime.Value - outgoingEdge.Content.Duration;

                            // Diminish the latest finish time artificially (if required).
                            if (proposedLatestFinishTime < latestFinishTime)
                            {
                                latestFinishTime = proposedLatestFinishTime;
                            }
                        }

                        if (outgoingEdge.Content.MaximumLatestFinishTime.HasValue)
                        {
                            int proposedLatestFinishTime = outgoingEdge.Content.MaximumLatestFinishTime.Value - outgoingEdge.Content.Duration;

                            // Diminish the latest finish time artificially (if required).
                            if (proposedLatestFinishTime < latestFinishTime)
                            {
                                latestFinishTime = proposedLatestFinishTime;
                            }
                        }
                    }

                    node.Content.LatestFinishTime = latestFinishTime;
                    completedNodeCount++;

                    // A Start node has no incoming edges to carry the flow backwards, and
                    // asking it for them throws.
                    if (node.NodeType != NodeType.Start)
                    {
                        foreach (T incomingEdgeId in node.IncomingEdges)
                        {
                            ReleasePredecessorEvent(state, incomingEdgeId, pendingOutgoingEdgeCounts, nextReadyNodes);
                        }
                    }
                }

                readyNodes = nextReadyNodes;
            }

            // If some events were never reached once nothing more can become ready, then a
            // cycle must exist in the graph and we will not be able to calculate the latest
            // finish times.
            if (completedNodeCount != state.NodeCount)
            {
                throw new InvalidOperationException(Properties.Resources.Message_CannotCalculateLatestFinishTimesDueToCyclicDependency);
            }
            return true;
        }

        // Records that a forward-flow edge is complete, and queues the event at its head
        // once that was the last incoming edge it was waiting on.
        private static void ReleaseSuccessorEvent(
            IArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            T edgeId,
            Dictionary<T, int> pendingIncomingEdgeCounts,
            List<Node<T, IEvent<T>>> readyNodes)
        {
            Node<T, IEvent<T>> headNode = state.EdgeHeadNode(edgeId);
            int pendingIncomingEdges = pendingIncomingEdgeCounts[headNode.Id] - 1;
            pendingIncomingEdgeCounts[headNode.Id] = pendingIncomingEdges;

            if (pendingIncomingEdges == 0)
            {
                readyNodes.Add(headNode);
            }
        }

        // The backward-flow mirror: queues the event at an edge's tail once it has no
        // outgoing edges left outstanding.
        private static void ReleasePredecessorEvent(
            IArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            T edgeId,
            Dictionary<T, int> pendingOutgoingEdgeCounts,
            List<Node<T, IEvent<T>>> readyNodes)
        {
            Node<T, IEvent<T>> tailNode = state.EdgeTailNode(edgeId);
            int pendingOutgoingEdges = pendingOutgoingEdgeCounts[tailNode.Id] - 1;
            pendingOutgoingEdgeCounts[tailNode.Id] = pendingOutgoingEdges;

            if (pendingOutgoingEdges == 0)
            {
                readyNodes.Add(tailNode);
            }
        }

        /// <inheritdoc/>
        public bool CalculateCriticalPathVariables(
            IArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
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

            // Only perform if all events have earliest finish times.
            if (!state.Events.All(x => x.EarliestFinishTime.HasValue))
            {
                return false;
            }

            // Only perform if all events have latest finish times.
            if (!state.Events.All(x => x.LatestFinishTime.HasValue))
            {
                return false;
            }

            // We can assume at this point that all the activity constraints are valid.

            // Earliest Start Times and Latest Finish Times.
            foreach (T edgeId in state.EdgeIds.ToList())
            {
                Edge<T, TActivity> edge = state.Edge(edgeId);

                int? earliestStartTime = state.EdgeTailNode(edge.Id).Content.EarliestFinishTime;

                if (edge.Content.MinimumEarliestStartTime.HasValue)
                {
                    int proposedEarliestStartTime = edge.Content.MinimumEarliestStartTime.Value;

                    // Augment the earliest start time artificially (if required).
                    if (proposedEarliestStartTime > earliestStartTime)
                    {
                        earliestStartTime = proposedEarliestStartTime;
                    }
                }

                if (edge.Content.MaximumLatestFinishTime.HasValue)
                {
                    int proposedLatestStartTime = edge.Content.MaximumLatestFinishTime.Value - edge.Content.Duration;

                    // Diminish the earliest start time artificially (if required).
                    if (proposedLatestStartTime < earliestStartTime)
                    {
                        earliestStartTime = proposedLatestStartTime;
                    }
                }

                edge.Content.EarliestStartTime = earliestStartTime;

                int? latestFinishTime = state.EdgeHeadNode(edge.Id).Content.LatestFinishTime;

                if (edge.Content.MaximumLatestFinishTime.HasValue)
                {
                    int proposedLatestFinishTime = edge.Content.MaximumLatestFinishTime.Value;

                    // Diminish the latest finish time artificially (if required).
                    if (proposedLatestFinishTime < latestFinishTime)
                    {
                        latestFinishTime = proposedLatestFinishTime;
                    }
                }

                edge.Content.LatestFinishTime = latestFinishTime;
            }

            // Free float/slack calculations.
            foreach (T edgeId in state.EdgeIds.ToList())
            {
                Edge<T, TActivity> edge = state.Edge(edgeId);
                Node<T, IEvent<T>> headNode = state.EdgeHeadNode(edgeId);

                if (headNode.NodeType == NodeType.End)
                {
                    if (headNode.Content.EarliestFinishTime.HasValue
                        && edge.Content.EarliestFinishTime.HasValue)
                    {
                        int freeSlack = headNode.Content.EarliestFinishTime.Value - edge.Content.EarliestFinishTime.Value;

                        if (edge.Content.MaximumLatestFinishTime.HasValue)
                        {
                            int proposedFreeSlack = edge.Content.MaximumLatestFinishTime.Value - edge.Content.EarliestFinishTime.Value;

                            // Diminish the free slack artificially (if required).
                            if (proposedFreeSlack < freeSlack)
                            {
                                freeSlack = proposedFreeSlack;
                            }
                        }

                        edge.Content.FreeSlack = freeSlack;
                    }

                    continue;
                }

                HashSet<T> outgoingEdges = headNode.OutgoingEdges;
                int minEarliestStartTimeOfOutgoingEdges = headNode.Content.LatestFinishTime.GetValueOrDefault();

                foreach (T outgoingEdgeId in outgoingEdges)
                {
                    Edge<T, TActivity> outgoingEdge = state.Edge(outgoingEdgeId);

                    if (outgoingEdge.Content.EarliestStartTime.HasValue)
                    {
                        int proposedEarliestStartTime = outgoingEdge.Content.EarliestStartTime.Value;

                        if (proposedEarliestStartTime < minEarliestStartTimeOfOutgoingEdges)
                        {
                            minEarliestStartTimeOfOutgoingEdges = proposedEarliestStartTime;
                        }
                    }

                    if (outgoingEdge.Content.MaximumLatestFinishTime.HasValue)
                    {
                        int proposedLatestStartTime = outgoingEdge.Content.MaximumLatestFinishTime.Value - outgoingEdge.Content.Duration;

                        if (proposedLatestStartTime < minEarliestStartTimeOfOutgoingEdges)
                        {
                            minEarliestStartTimeOfOutgoingEdges = proposedLatestStartTime;
                        }
                    }
                }

                if (edge.Content.EarliestFinishTime.HasValue)
                {
                    int freeSlack = minEarliestStartTimeOfOutgoingEdges - edge.Content.EarliestFinishTime.Value;

                    if (edge.Content.MaximumLatestFinishTime.HasValue)
                    {
                        int proposedFreeSlack = edge.Content.MaximumLatestFinishTime.Value - edge.Content.EarliestFinishTime.Value;

                        // Diminish the free slack artificially (if required).
                        if (proposedFreeSlack < freeSlack)
                        {
                            freeSlack = proposedFreeSlack;
                        }
                    }

                    edge.Content.FreeSlack = freeSlack;
                }
            }
            return true;
        }
    }
}
