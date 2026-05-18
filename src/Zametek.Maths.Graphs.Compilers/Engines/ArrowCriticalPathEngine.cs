using System;
using System.Collections.Generic;
using System.Linq;
using Zametek.Utility;

namespace Zametek.Maths.Graphs
{
    // Calculates the critical path for Activity-on-Arrow graphs.
    // Implements the forward pass (earliest event finish times), backward pass
    // (latest event finish times), and per-activity critical path variable calculation.
    internal sealed class ArrowCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity, TEvent>
        : IArrowCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity, TEvent>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
        where TActivity : IActivity<T, TResourceId, TWorkStreamId>
        where TEvent : IEvent<T>
    {
        public bool CalculateEventEarliestFinishTimes(
            IEnumerable<T> nodeIds,
            IDictionary<T, Edge<T, TActivity>> edgeLookup,
            IDictionary<T, Node<T, TEvent>> nodeLookup,
            IDictionary<T, Node<T, TEvent>> edgeHeadNodeLookup,
            IDictionary<T, Node<T, TEvent>> edgeTailNodeLookup,
            IEnumerable<IInvalidConstraint<T>> invalidConstraints,
            Node<T, TEvent> startNode,
            Node<T, TEvent> endNode,
            bool shuffle)
        {
            if (nodeIds is null)
            {
                throw new ArgumentNullException(nameof(nodeIds));
            }
            if (edgeLookup is null)
            {
                throw new ArgumentNullException(nameof(edgeLookup));
            }
            if (nodeLookup is null)
            {
                throw new ArgumentNullException(nameof(nodeLookup));
            }
            if (edgeHeadNodeLookup is null)
            {
                throw new ArgumentNullException(nameof(edgeHeadNodeLookup));
            }
            if (edgeTailNodeLookup is null)
            {
                throw new ArgumentNullException(nameof(edgeTailNodeLookup));
            }
            if (invalidConstraints is null)
            {
                throw new ArgumentNullException(nameof(invalidConstraints));
            }
            if (startNode is null)
            {
                throw new ArgumentNullException(nameof(startNode));
            }
            if (endNode is null)
            {
                throw new ArgumentNullException(nameof(endNode));
            }

            if (invalidConstraints.Any())
            {
                return false;
            }

            var completedNodeIds = new HashSet<T>();
            var remainingNodeIds = new HashSet<T>(nodeIds);

            // Make sure the remainingNodeIds contain the Start node.
            if (!remainingNodeIds.Contains(startNode.Id))
            {
                return false;
            }

            // Complete the Start node first to ensure the completed node IDs contains something.
            startNode.Content.EarliestFinishTime = 0;
            completedNodeIds.Add(startNode.Id);
            remainingNodeIds.Remove(startNode.Id);

            // Forward flow algorithm.
            while (remainingNodeIds.Any())
            {
                bool progress = false;
                List<T> remainingNodeIdList = remainingNodeIds.ToList();

                if (shuffle)
                {
                    remainingNodeIdList.Shuffle();
                }

                foreach (T nodeId in remainingNodeIdList)
                {
                    Node<T, TEvent> node = nodeLookup[nodeId];

                    // Get the incoming edges and the dependency nodes IDs.
                    List<T> incomingEdges = new List<T>(node.IncomingEdges);

                    if (shuffle)
                    {
                        incomingEdges.Shuffle();
                    }

                    var dependencyNodeIds = new HashSet<T>(incomingEdges.Select(x => edgeTailNodeLookup[x].Id));

                    // If calculations for all the dependency nodes have been completed, then use them
                    // to complete the calculations for this node.
                    if (dependencyNodeIds.IsSubsetOf(completedNodeIds))
                    {
                        int earliestFinishTime = 0;

                        foreach (T incomingEdgeId in incomingEdges)
                        {
                            Edge<T, TActivity> incomingEdge = edgeLookup[incomingEdgeId];
                            Node<T, TEvent> incomingEdgeTailNode = edgeTailNodeLookup[incomingEdgeId];

                            if (incomingEdgeTailNode.Content.EarliestFinishTime.HasValue)
                            {
                                int proposedEarliestFinishTime = incomingEdgeTailNode.Content.EarliestFinishTime.Value + incomingEdge.Content.Duration;

                                proposedEarliestFinishTime += incomingEdge.Content.MinimumFreeSlack.GetValueOrDefault();

                                if (proposedEarliestFinishTime > earliestFinishTime)
                                {
                                    earliestFinishTime = proposedEarliestFinishTime;
                                }
                            }

                            if (incomingEdge.Content.MinimumEarliestStartTime.HasValue)
                            {
                                int proposedEarliestFinishTime = incomingEdge.Content.MinimumEarliestStartTime.Value + incomingEdge.Content.Duration;

                                if (proposedEarliestFinishTime > earliestFinishTime)
                                {
                                    earliestFinishTime = proposedEarliestFinishTime;
                                }
                            }

                            // It is only necessary to check the Maximum LFT if the head node is not the
                            // EndNode, and if the tail node is not the StartNode.
                            if (node != endNode && incomingEdgeTailNode != startNode)
                            {
                                if (incomingEdge.Content.MaximumLatestFinishTime.HasValue)
                                {
                                    int proposedLatestFinishTime = incomingEdge.Content.MaximumLatestFinishTime.Value;

                                    if (proposedLatestFinishTime < earliestFinishTime)
                                    {
                                        earliestFinishTime = proposedLatestFinishTime;
                                    }
                                }
                            }
                        }

                        node.Content.EarliestFinishTime = earliestFinishTime;
                        completedNodeIds.Add(nodeId);
                        remainingNodeIds.Remove(nodeId);
                        progress = true;
                    }
                }

                if (!progress)
                {
                    throw new InvalidOperationException(Properties.Resources.Message_CannotCalculateEarliestFinishTimesDueToCyclicDependency);
                }
            }
            return true;
        }

        public bool CalculateEventLatestFinishTimes(
            IEnumerable<T> nodeIds,
            IDictionary<T, Edge<T, TActivity>> edgeLookup,
            IDictionary<T, Node<T, TEvent>> nodeLookup,
            IDictionary<T, Node<T, TEvent>> edgeHeadNodeLookup,
            IDictionary<T, Node<T, TEvent>> edgeTailNodeLookup,
            IEnumerable<IInvalidConstraint<T>> invalidConstraints,
            Node<T, TEvent> endNode,
            bool shuffle)
        {
            if (nodeIds is null)
            {
                throw new ArgumentNullException(nameof(nodeIds));
            }
            if (edgeLookup is null)
            {
                throw new ArgumentNullException(nameof(edgeLookup));
            }
            if (nodeLookup is null)
            {
                throw new ArgumentNullException(nameof(nodeLookup));
            }
            if (edgeHeadNodeLookup is null)
            {
                throw new ArgumentNullException(nameof(edgeHeadNodeLookup));
            }
            if (edgeTailNodeLookup is null)
            {
                throw new ArgumentNullException(nameof(edgeTailNodeLookup));
            }
            if (invalidConstraints is null)
            {
                throw new ArgumentNullException(nameof(invalidConstraints));
            }
            if (endNode is null)
            {
                throw new ArgumentNullException(nameof(endNode));
            }

            if (invalidConstraints.Any())
            {
                return false;
            }

            // Only perform if all events have earliest finish times.
            if (!nodeLookup.Values.All(x => x.Content.EarliestFinishTime.HasValue))
            {
                return false;
            }

            var completedNodeIds = new HashSet<T>();
            var remainingNodeIds = new HashSet<T>(nodeIds);

            if (!remainingNodeIds.Contains(endNode.Id))
            {
                return false;
            }

            endNode.Content.LatestFinishTime = endNode.Content.EarliestFinishTime;

            if (!endNode.Content.LatestFinishTime.HasValue)
            {
                return false;
            }

            int endNodeLatestFinishTime = endNode.Content.LatestFinishTime.Value;
            completedNodeIds.Add(endNode.Id);
            remainingNodeIds.Remove(endNode.Id);

            // Backward flow algorithm.
            while (remainingNodeIds.Any())
            {
                bool progress = false;
                var remainingNodeIdList = remainingNodeIds.ToList();

                if (shuffle)
                {
                    remainingNodeIdList.Shuffle();
                }

                foreach (T nodeId in remainingNodeIdList)
                {
                    Node<T, TEvent> node = nodeLookup[nodeId];

                    // Get the outgoing edges and the successor nodes IDs.
                    List<T> outgoingEdges = new List<T>(node.OutgoingEdges);

                    if (shuffle)
                    {
                        outgoingEdges.Shuffle();
                    }

                    var successorNodeIds = new HashSet<T>(outgoingEdges.Select(x => edgeHeadNodeLookup[x].Id));

                    if (successorNodeIds.IsSubsetOf(completedNodeIds))
                    {
                        int latestFinishTime = endNodeLatestFinishTime;

                        foreach (T outgoingEdgeId in outgoingEdges)
                        {
                            Edge<T, TActivity> outgoingEdge = edgeLookup[outgoingEdgeId];
                            Node<T, TEvent> outgoingEdgeHeadNode = edgeHeadNodeLookup[outgoingEdgeId];

                            if (outgoingEdgeHeadNode.Content.LatestFinishTime.HasValue)
                            {
                                int proposedLatestFinishTime = outgoingEdgeHeadNode.Content.LatestFinishTime.Value - outgoingEdge.Content.Duration;

                                if (proposedLatestFinishTime < latestFinishTime)
                                {
                                    latestFinishTime = proposedLatestFinishTime;
                                }
                            }

                            if (outgoingEdge.Content.MaximumLatestFinishTime.HasValue)
                            {
                                int proposedLatestFinishTime = outgoingEdge.Content.MaximumLatestFinishTime.Value - outgoingEdge.Content.Duration;

                                if (proposedLatestFinishTime < latestFinishTime)
                                {
                                    latestFinishTime = proposedLatestFinishTime;
                                }
                            }
                        }

                        node.Content.LatestFinishTime = latestFinishTime;
                        completedNodeIds.Add(nodeId);
                        remainingNodeIds.Remove(nodeId);
                        progress = true;
                    }
                }

                if (!progress)
                {
                    throw new InvalidOperationException(Properties.Resources.Message_CannotCalculateLatestFinishTimesDueToCyclicDependency);
                }
            }
            return true;
        }

        public bool CalculateCriticalPathVariables(
            IEnumerable<T> edgeIds,
            IDictionary<T, Edge<T, TActivity>> edgeLookup,
            IDictionary<T, Node<T, TEvent>> edgeHeadNodeLookup,
            IDictionary<T, Node<T, TEvent>> edgeTailNodeLookup,
            IEnumerable<IInvalidConstraint<T>> invalidConstraints,
            IEnumerable<TEvent> events)
        {
            if (edgeIds is null)
            {
                throw new ArgumentNullException(nameof(edgeIds));
            }
            if (edgeLookup is null)
            {
                throw new ArgumentNullException(nameof(edgeLookup));
            }
            if (edgeHeadNodeLookup is null)
            {
                throw new ArgumentNullException(nameof(edgeHeadNodeLookup));
            }
            if (edgeTailNodeLookup is null)
            {
                throw new ArgumentNullException(nameof(edgeTailNodeLookup));
            }
            if (invalidConstraints is null)
            {
                throw new ArgumentNullException(nameof(invalidConstraints));
            }
            if (events is null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            if (invalidConstraints.Any())
            {
                return false;
            }

            // Only perform if all events have earliest finish times.
            if (!events.All(x => x.EarliestFinishTime.HasValue))
            {
                return false;
            }

            // Only perform if all events have latest finish times.
            if (!events.All(x => x.LatestFinishTime.HasValue))
            {
                return false;
            }

            // Earliest Start Times and Latest Finish Times.
            foreach (T edgeId in edgeIds.ToList())
            {
                Edge<T, TActivity> edge = edgeLookup[edgeId];

                int? earliestStartTime = edgeTailNodeLookup[edge.Id].Content.EarliestFinishTime;

                if (edge.Content.MinimumEarliestStartTime.HasValue)
                {
                    int proposedEarliestStartTime = edge.Content.MinimumEarliestStartTime.Value;

                    if (proposedEarliestStartTime > earliestStartTime)
                    {
                        earliestStartTime = proposedEarliestStartTime;
                    }
                }

                if (edge.Content.MaximumLatestFinishTime.HasValue)
                {
                    int proposedLatestStartTime = edge.Content.MaximumLatestFinishTime.Value - edge.Content.Duration;

                    if (proposedLatestStartTime < earliestStartTime)
                    {
                        earliestStartTime = proposedLatestStartTime;
                    }
                }

                edge.Content.EarliestStartTime = earliestStartTime;

                int? latestFinishTime = edgeHeadNodeLookup[edge.Id].Content.LatestFinishTime;

                if (edge.Content.MaximumLatestFinishTime.HasValue)
                {
                    int proposedLatestFinishTime = edge.Content.MaximumLatestFinishTime.Value;

                    if (proposedLatestFinishTime < latestFinishTime)
                    {
                        latestFinishTime = proposedLatestFinishTime;
                    }
                }

                edge.Content.LatestFinishTime = latestFinishTime;
            }

            // Free float/slack calculations.
            foreach (T edgeId in edgeIds.ToList())
            {
                Edge<T, TActivity> edge = edgeLookup[edgeId];
                Node<T, TEvent> headNode = edgeHeadNodeLookup[edgeId];

                if (headNode.NodeType == NodeType.End)
                {
                    if (headNode.Content.EarliestFinishTime.HasValue
                        && edge.Content.EarliestFinishTime.HasValue)
                    {
                        int freeSlack = headNode.Content.EarliestFinishTime.Value - edge.Content.EarliestFinishTime.Value;

                        if (edge.Content.MaximumLatestFinishTime.HasValue)
                        {
                            int proposedFreeSlack = edge.Content.MaximumLatestFinishTime.Value - edge.Content.EarliestFinishTime.Value;

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
                    Edge<T, TActivity> outgoingEdge = edgeLookup[outgoingEdgeId];

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
