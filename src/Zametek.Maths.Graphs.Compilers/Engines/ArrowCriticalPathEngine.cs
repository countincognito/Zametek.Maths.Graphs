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
    internal sealed class ArrowCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity>
        : IArrowCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
        where TActivity : class, IActivity<T, TResourceId, TWorkStreamId>
    {
        public bool CalculateEventEarliestFinishTimes(
            ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            IEnumerable<IInvalidConstraint<T>> invalidConstraints,
            bool shuffle)
        {
            if (state is null) throw new ArgumentNullException(nameof(state));
            if (invalidConstraints is null) throw new ArgumentNullException(nameof(invalidConstraints));
            if (state.StartNode is null) throw new InvalidOperationException(@"Arrow graph state has no Start node");
            if (state.EndNode is null) throw new InvalidOperationException(@"Arrow graph state has no End node");

            if (invalidConstraints.Any()) return false;

            Node<T, IEvent<T>> startNode = state.StartNode;
            Node<T, IEvent<T>> endNode = state.EndNode;

            var completedNodeIds = new HashSet<T>();
            var remainingNodeIds = new HashSet<T>(state.NodeIds);

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
                    Node<T, IEvent<T>> node = state.Node(nodeId);

                    // Get the incoming edges and the dependency nodes IDs.
                    List<T> incomingEdges = new List<T>(node.IncomingEdges);

                    if (shuffle)
                    {
                        incomingEdges.Shuffle();
                    }

                    var dependencyNodeIds = new HashSet<T>(incomingEdges.Select(x => state.EdgeTailNode(x).Id));

                    // If calculations for all the dependency nodes have been completed, then use them
                    // to complete the calculations for this node.
                    if (dependencyNodeIds.IsSubsetOf(completedNodeIds))
                    {
                        int earliestFinishTime = 0;

                        foreach (T incomingEdgeId in incomingEdges)
                        {
                            Edge<T, TActivity> incomingEdge = state.Edge(incomingEdgeId);
                            Node<T, IEvent<T>> incomingEdgeTailNode = state.EdgeTailNode(incomingEdgeId);

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
            ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            IEnumerable<IInvalidConstraint<T>> invalidConstraints,
            bool shuffle)
        {
            if (state is null) throw new ArgumentNullException(nameof(state));
            if (invalidConstraints is null) throw new ArgumentNullException(nameof(invalidConstraints));
            if (state.EndNode is null) throw new InvalidOperationException(@"Arrow graph state has no End node");

            if (invalidConstraints.Any()) return false;

            // Only perform if all events have earliest finish times.
            if (!state.Nodes.All(x => x.Content.EarliestFinishTime.HasValue))
            {
                return false;
            }

            Node<T, IEvent<T>> endNode = state.EndNode;

            var completedNodeIds = new HashSet<T>();
            var remainingNodeIds = new HashSet<T>(state.NodeIds);

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
                    Node<T, IEvent<T>> node = state.Node(nodeId);

                    // Get the outgoing edges and the successor nodes IDs.
                    List<T> outgoingEdges = new List<T>(node.OutgoingEdges);

                    if (shuffle)
                    {
                        outgoingEdges.Shuffle();
                    }

                    var successorNodeIds = new HashSet<T>(outgoingEdges.Select(x => state.EdgeHeadNode(x).Id));

                    if (successorNodeIds.IsSubsetOf(completedNodeIds))
                    {
                        int latestFinishTime = endNodeLatestFinishTime;

                        foreach (T outgoingEdgeId in outgoingEdges)
                        {
                            Edge<T, TActivity> outgoingEdge = state.Edge(outgoingEdgeId);
                            Node<T, IEvent<T>> outgoingEdgeHeadNode = state.EdgeHeadNode(outgoingEdgeId);

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
            ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            IEnumerable<IInvalidConstraint<T>> invalidConstraints)
        {
            if (state is null) throw new ArgumentNullException(nameof(state));
            if (invalidConstraints is null) throw new ArgumentNullException(nameof(invalidConstraints));

            if (invalidConstraints.Any()) return false;

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

            // Earliest Start Times and Latest Finish Times.
            foreach (T edgeId in state.EdgeIds.ToList())
            {
                Edge<T, TActivity> edge = state.Edge(edgeId);

                int? earliestStartTime = state.EdgeTailNode(edge.Id).Content.EarliestFinishTime;

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

                int? latestFinishTime = state.EdgeHeadNode(edge.Id).Content.LatestFinishTime;

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
