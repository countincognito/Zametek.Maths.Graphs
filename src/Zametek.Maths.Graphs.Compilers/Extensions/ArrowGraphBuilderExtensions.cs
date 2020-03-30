using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    internal static class ArrowGraphBuilderExtensions
    {
        internal static bool CalculateEventEarliestFinishTimes<T, TResourceId, TActivity, TEvent>
            (this ArrowGraphBuilderBase<T, TResourceId, TActivity, TEvent> arrowGraphBuilder)
            where TActivity : IActivity<T, TResourceId>
            where TEvent : IEvent<T>
            where T : struct, IComparable<T>, IEquatable<T>
            where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        {
            if (arrowGraphBuilder == null)
            {
                throw new ArgumentNullException(nameof(arrowGraphBuilder));
            }
            if (!arrowGraphBuilder.AllDependenciesSatisfied)
            {
                return false;
            }

            var completedNodeIds = new HashSet<T>();
            var remainingNodeIds = new HashSet<T>(arrowGraphBuilder.NodeIds);

            // Make sure the remainingNodeIds contain the Start node.
            if (!remainingNodeIds.Contains(arrowGraphBuilder.StartNode.Id))
            {
                return false;
            }

            // Complete the Start node first to ensure the completed node IDs
            // contains something.
            Node<T, TEvent> startNode = arrowGraphBuilder.StartNode;
            startNode.Content.EarliestFinishTime = 0;
            completedNodeIds.Add(startNode.Id);
            remainingNodeIds.Remove(startNode.Id);

            // Forward flow algorithm.
            while (remainingNodeIds.Any())
            {
                bool progress = false;
                foreach (T nodeId in remainingNodeIds.ToList())
                {
                    Node<T, TEvent> node = arrowGraphBuilder.Node(nodeId);

                    // Get the incoming edges and the dependency nodes IDs.
                    HashSet<T> incomingEdges = node.IncomingEdges;
                    var dependencyNodeIds =
                        new HashSet<T>(incomingEdges.Select(arrowGraphBuilder.EdgeTailNode).Select(x => x.Id));

                    // If calculations for all the dependency nodes have been completed, then use them
                    // to complete the calculations for this node.
                    if (dependencyNodeIds.IsSubsetOf(completedNodeIds))
                    {
                        int earliestFinishTime = 0;
                        foreach (T incomingEdgeId in incomingEdges)
                        {
                            Edge<T, TActivity> incomingEdge = arrowGraphBuilder.Edge(incomingEdgeId);
                            Node<T, TEvent> incomingEdgeTailNode = arrowGraphBuilder.EdgeTailNode(incomingEdgeId);

                            if (incomingEdgeTailNode.Content.EarliestFinishTime.HasValue)
                            {
                                int proposedEarliestFinishTime =
                                    incomingEdgeTailNode.Content.EarliestFinishTime.Value + incomingEdge.Content.Duration;

                                // At this point, augment the free slack artificially (if required).
                                proposedEarliestFinishTime += incomingEdge.Content.MinimumFreeSlack.GetValueOrDefault();
                                if (proposedEarliestFinishTime > earliestFinishTime)
                                {
                                    earliestFinishTime = proposedEarliestFinishTime;
                                }
                            }

                            if (incomingEdge.Content.MinimumEarliestStartTime.HasValue)
                            {
                                int proposedEarliestFinishTime =
                                    incomingEdge.Content.MinimumEarliestStartTime.Value + incomingEdge.Content.Duration;

                                // At this point, augment the earliest finish time artificially (if required).
                                if (proposedEarliestFinishTime > earliestFinishTime)
                                {
                                    earliestFinishTime = proposedEarliestFinishTime;
                                }
                            }
                        }

                        node.Content.EarliestFinishTime = earliestFinishTime;
                        completedNodeIds.Add(nodeId);
                        remainingNodeIds.Remove(nodeId);

                        // Note we are making progress.
                        progress = true;
                    }
                }
                // If we have not made any progress then a cycle must exist in
                // the graph and we will not be able to calculate the earliest
                // finish times.
                if (!progress)
                {
                    throw new InvalidOperationException(Properties.Resources.CannotCalculateEarliestFinishTimesDueToCyclicDependency);
                }
            }
            return true;
        }

        internal static bool CalculateEventLatestFinishTimes<T, TResourceId, TActivity, TEvent>
            (this ArrowGraphBuilderBase<T, TResourceId, TActivity, TEvent> arrowGraphBuilder)
            where TActivity : IActivity<T, TResourceId>
            where TEvent : IEvent<T>
            where T : struct, IComparable<T>, IEquatable<T>
            where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        {
            if (arrowGraphBuilder == null)
            {
                throw new ArgumentNullException(nameof(arrowGraphBuilder));
            }
            if (!arrowGraphBuilder.AllDependenciesSatisfied)
            {
                return false;
            }
            // Only perform if all events a have earliest finish times.
            if (!arrowGraphBuilder.Nodes.All(x => x.Content.EarliestFinishTime.HasValue))
            {
                return false;
            }

            var completedNodeIds = new HashSet<T>();
            var remainingNodeIds = new HashSet<T>(arrowGraphBuilder.NodeIds);
            // Make sure the remainingNodeIds contain the End node.
            if (!remainingNodeIds.Contains(arrowGraphBuilder.EndNode.Id))
            {
                return false;
            }

            arrowGraphBuilder.EndNode.Content.LatestFinishTime = arrowGraphBuilder.EndNode.Content.EarliestFinishTime;
            if (!arrowGraphBuilder.EndNode.Content.LatestFinishTime.HasValue)
            {
                return false;
            }
            int endNodeLatestFinishTime = arrowGraphBuilder.EndNode.Content.LatestFinishTime.Value;

            // Complete the End node first to ensure the completed node IDs contains something.
            Node<T, TEvent> endNode = arrowGraphBuilder.EndNode;
            endNode.Content.LatestFinishTime = endNode.Content.EarliestFinishTime;
            completedNodeIds.Add(endNode.Id);
            remainingNodeIds.Remove(endNode.Id);

            // Backward flow algorithm.
            while (remainingNodeIds.Any())
            {
                bool progress = false;
                foreach (T nodeId in remainingNodeIds.ToList())
                {
                    Node<T, TEvent> node = arrowGraphBuilder.Node(nodeId);

                    // Get the outgoing edges and the successor nodes IDs.
                    HashSet<T> outgoingEdges = node.OutgoingEdges;
                    var successorNodeIds =
                        new HashSet<T>(outgoingEdges.Select(arrowGraphBuilder.EdgeHeadNode).Select(x => x.Id));

                    if (successorNodeIds.IsSubsetOf(completedNodeIds))
                    {
                        int latestFinishTime = endNodeLatestFinishTime;
                        foreach (T outgoingEdgeId in outgoingEdges)
                        {
                            Edge<T, TActivity> outgoingEdge = arrowGraphBuilder.Edge(outgoingEdgeId);
                            Node<T, TEvent> outgoingEdgeHeadnode = arrowGraphBuilder.EdgeHeadNode(outgoingEdgeId);

                            if (outgoingEdgeHeadnode.Content.LatestFinishTime.HasValue)
                            {
                                int proposedLatestFinishTime =
                                    outgoingEdgeHeadnode.Content.LatestFinishTime.Value - outgoingEdge.Content.Duration;

                                if (proposedLatestFinishTime < latestFinishTime)
                                {
                                    latestFinishTime = proposedLatestFinishTime;
                                }
                            }
                        }
                        node.Content.LatestFinishTime = latestFinishTime;
                        completedNodeIds.Add(nodeId);
                        remainingNodeIds.Remove(nodeId);

                        // Note we are making progress.
                        progress = true;
                    }
                }
                // If we have not made any progress then a cycle must exist in
                // the graph and we will not be able to calculate the latest
                // finish times.
                if (!progress)
                {
                    throw new InvalidOperationException(Properties.Resources.CannotCalculateLatestFinishTimesDueToCyclicDependency);
                }
            }
            return true;
        }

        internal static bool CalculateCriticalPathVariables<T, TResourceId, TActivity, TEvent>
            (this ArrowGraphBuilderBase<T, TResourceId, TActivity, TEvent> arrowGraphBuilder)
            where TActivity : IActivity<T, TResourceId>
            where TEvent : IEvent<T>
            where T : struct, IComparable<T>, IEquatable<T>
            where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        {
            if (arrowGraphBuilder == null)
            {
                throw new ArgumentNullException(nameof(arrowGraphBuilder));
            }
            if (!arrowGraphBuilder.AllDependenciesSatisfied)
            {
                return false;
            }
            // Only perform if all events a have earliest finish times.
            if (!arrowGraphBuilder.Events.All(x => x.EarliestFinishTime.HasValue))
            {
                return false;
            }
            // Only perform if all events a have latest finish times.
            if (!arrowGraphBuilder.Events.All(x => x.LatestFinishTime.HasValue))
            {
                return false;
            }

            // Earliest Start Times and Latest Finish Times.
            foreach (T edgeId in arrowGraphBuilder.EdgeIds.ToList())
            {
                Edge<T, TActivity> edge = arrowGraphBuilder.Edge(edgeId);

                int? earliestStartTime = arrowGraphBuilder.EdgeTailNode(edge.Id).Content.EarliestFinishTime;
                int? minimumEarliestStartTime = edge.Content.MinimumEarliestStartTime.GetValueOrDefault();
                if (minimumEarliestStartTime.GetValueOrDefault() > earliestStartTime.GetValueOrDefault())
                {
                    earliestStartTime = minimumEarliestStartTime;
                }
                edge.Content.EarliestStartTime = earliestStartTime;

                edge.Content.LatestFinishTime = arrowGraphBuilder.EdgeHeadNode(edge.Id).Content.LatestFinishTime;
            }

            // Free float/slack calculations.
            foreach (T edgeId in arrowGraphBuilder.EdgeIds.ToList())
            {
                Edge<T, TActivity> edge = arrowGraphBuilder.Edge(edgeId);
                Node<T, TEvent> headNode = arrowGraphBuilder.EdgeHeadNode(edgeId);
                if (headNode.NodeType == NodeType.End)
                {
                    if (headNode.Content.EarliestFinishTime.HasValue
                        && edge.Content.EarliestFinishTime.HasValue)
                    {
                        edge.Content.FreeSlack =
                            headNode.Content.EarliestFinishTime.GetValueOrDefault() -
                            edge.Content.EarliestFinishTime.GetValueOrDefault();
                    }
                    continue;
                }

                HashSet<T> outgoingEdges = headNode.OutgoingEdges;
                int minEarliestStartTimeOfOutgoingEdges =
                    outgoingEdges.Min(x => arrowGraphBuilder.Edge(x).Content.EarliestStartTime.GetValueOrDefault());
                if (edge.Content.EarliestFinishTime.HasValue)
                {
                    edge.Content.FreeSlack =
                        minEarliestStartTimeOfOutgoingEdges -
                        edge.Content.EarliestFinishTime.GetValueOrDefault();
                }
            }
            return true;
        }
    }
}
