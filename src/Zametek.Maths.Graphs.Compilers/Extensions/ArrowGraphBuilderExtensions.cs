using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    internal static class ArrowGraphBuilderExtensions
    {
        internal static void CalculateCriticalPath<T, TResourceId, TActivity, TEvent>
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

            bool edgesCleaned = arrowGraphBuilder.CleanUpEdges();

            if (!edgesCleaned)
            {
                throw new InvalidOperationException(Properties.Resources.CannotPerformEdgeCleanUp);
            }

            arrowGraphBuilder.ClearCriticalPathVariables();

            if (!arrowGraphBuilder.CalculateEventEarliestFinishTimes())
            {
                throw new InvalidOperationException(Properties.Resources.CannotCalculateEventEarliestFinishTimes);
            }
            if (!arrowGraphBuilder.CalculateEventLatestFinishTimes())
            {
                throw new InvalidOperationException(Properties.Resources.CannotCalculateEventLatestFinishTimes);
            }
            if (!arrowGraphBuilder.CalculateCriticalPathVariables())
            {
                throw new InvalidOperationException(Properties.Resources.CannotCalculateCriticalPath);
            }
        }

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
            if (arrowGraphBuilder.FindInvalidConstraints().Any())
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

            // Earliest Start Time.
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
                    var dependencyNodeIds = new HashSet<T>(incomingEdges.Select(arrowGraphBuilder.EdgeTailNode).Select(x => x.Id));

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
            if (arrowGraphBuilder.FindInvalidConstraints().Any())
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
            Node<T, TEvent> endNode = arrowGraphBuilder.EndNode;

            // Make sure the remainingNodeIds contain the End node.
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

            // Complete the End node first to ensure the completed node IDs contains something.
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
                    var successorNodeIds = new HashSet<T>(outgoingEdges.Select(arrowGraphBuilder.EdgeHeadNode).Select(x => x.Id));

                    if (successorNodeIds.IsSubsetOf(completedNodeIds))
                    {
                        int latestFinishTime = endNodeLatestFinishTime;

                        foreach (T outgoingEdgeId in outgoingEdges)
                        {
                            Edge<T, TActivity> outgoingEdge = arrowGraphBuilder.Edge(outgoingEdgeId);
                            Node<T, TEvent> outgoingEdgeHeadnode = arrowGraphBuilder.EdgeHeadNode(outgoingEdgeId);

                            if (outgoingEdgeHeadnode.Content.LatestFinishTime.HasValue)
                            {
                                int proposedLatestFinishTime = outgoingEdgeHeadnode.Content.LatestFinishTime.Value - outgoingEdge.Content.Duration;

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
            if (arrowGraphBuilder.FindInvalidConstraints().Any())
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

            // We can assume at this point that all the activity constraints are valid.

            // Earliest Start Times and Latest Finish Times.
            foreach (T edgeId in arrowGraphBuilder.EdgeIds.ToList())
            {
                Edge<T, TActivity> edge = arrowGraphBuilder.Edge(edgeId);

                int? earliestStartTime = arrowGraphBuilder.EdgeTailNode(edge.Id).Content.EarliestFinishTime;

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

                int? latestFinishTime = arrowGraphBuilder.EdgeHeadNode(edge.Id).Content.LatestFinishTime;

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
            foreach (T edgeId in arrowGraphBuilder.EdgeIds.ToList())
            {
                Edge<T, TActivity> edge = arrowGraphBuilder.Edge(edgeId);
                Node<T, TEvent> headNode = arrowGraphBuilder.EdgeHeadNode(edgeId);

                if (headNode.NodeType == NodeType.End)
                {
                    if (headNode.Content.EarliestFinishTime.HasValue
                        && edge.Content.EarliestFinishTime.HasValue)
                    {
                        int freeSlack = headNode.Content.EarliestFinishTime.Value - edge.Content.EarliestFinishTime.Value;

                        if (edge.Content.MaximumLatestFinishTime.HasValue)
                        {
                            int proposedfreeSlack = edge.Content.MaximumLatestFinishTime.Value - edge.Content.EarliestFinishTime.Value;

                            // Diminish the free slack artificially (if required).
                            if (proposedfreeSlack < freeSlack)
                            {
                                freeSlack = proposedfreeSlack;
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
                    Edge<T, TActivity> outgoingEdge = arrowGraphBuilder.Edge(outgoingEdgeId);

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
                        int proposedfreeSlack = edge.Content.MaximumLatestFinishTime.Value - edge.Content.EarliestFinishTime.Value;

                        // Diminish the free slack artificially (if required).
                        if (proposedfreeSlack < freeSlack)
                        {
                            freeSlack = proposedfreeSlack;
                        }
                    }

                    edge.Content.FreeSlack = freeSlack;
                }
            }
            return true;
        }
    }
}
