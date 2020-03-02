using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    internal static class VertexGraphBuilderExtensions
    {
        internal static bool CalculateCriticalPathForwardFlow<T, TActivity, TEvent>
            (this VertexGraphBuilderBase<T, TActivity, TEvent> vertexGraphBuilder)
            where TActivity : IActivity<T>
            where TEvent : IEvent<T>
            where T : struct, IComparable<T>, IEquatable<T>
        {
            if (vertexGraphBuilder == null)
            {
                throw new ArgumentNullException(nameof(vertexGraphBuilder));
            }
            if (!vertexGraphBuilder.AllDependenciesSatisfied)
            {
                return false;
            }

            var completedEdgeIds = new HashSet<T>();
            var remainingEdgeIds = new HashSet<T>(vertexGraphBuilder.EdgeIds);

            // First complete the Isolated nodes.
            foreach (Node<T, TActivity> node in vertexGraphBuilder.IsolatedNodes)
            {
                // Earliest Start Time.
                int earliestStartTime = 0;

                if (node.Content.MinimumEarliestStartTime.HasValue)
                {
                    // At this point, augment the earliest finish time artificially (if required).
                    int proposedEarliestStartTime = node.Content.MinimumEarliestStartTime.Value;
                    if (proposedEarliestStartTime > earliestStartTime)
                    {
                        earliestStartTime = proposedEarliestStartTime;
                    }
                }

                node.Content.EarliestStartTime = earliestStartTime;

                // Latest Finish Time.
                int latestFinishTime = node.Content.EarliestFinishTime.Value;

                if (node.Content.MinimumFreeSlack.HasValue)
                {
                    // At this point, augment the free slack artificially (if required).
                    int proposedLatestFinishTime = latestFinishTime + node.Content.MinimumFreeSlack.Value;
                    if (proposedLatestFinishTime > latestFinishTime)
                    {
                        latestFinishTime = proposedLatestFinishTime;
                    }
                }

                node.Content.LatestFinishTime = latestFinishTime;
            }

            // Complete the Start nodes first to ensure the completed edge IDs
            // contains something.
            foreach (Node<T, TActivity> node in vertexGraphBuilder.StartNodes)
            {
                int earliestStartTime = 0;

                if (node.Content.MinimumEarliestStartTime.HasValue)
                {
                    // At this point, augment the earliest finish time artificially (if required).
                    int proposedEarliestStartTime = node.Content.MinimumEarliestStartTime.Value;
                    if (proposedEarliestStartTime > earliestStartTime)
                    {
                        earliestStartTime = proposedEarliestStartTime;
                    }
                }

                node.Content.EarliestStartTime = earliestStartTime;

                foreach (T outgoingEdgeId in node.OutgoingEdges)
                {
                    Edge<T, TEvent> outgoingEdge = vertexGraphBuilder.Edge(outgoingEdgeId);
                    int earliestFinishTime = node.Content.EarliestFinishTime.Value;

                    if (node.Content.MinimumFreeSlack.HasValue)
                    {
                        // At this point, augment the free slack artificially (if required).
                        int proposedEarliestFinishTime = earliestFinishTime + node.Content.MinimumFreeSlack.Value;
                        if (proposedEarliestFinishTime > earliestFinishTime)
                        {
                            earliestFinishTime = proposedEarliestFinishTime;
                        }
                    }

                    outgoingEdge.Content.EarliestFinishTime = earliestFinishTime;
                    completedEdgeIds.Add(outgoingEdgeId);
                    remainingEdgeIds.Remove(outgoingEdgeId);
                }
            }

            // Forward flow algorithm.
            while (remainingEdgeIds.Any())
            {
                bool progress = false;
                foreach (T edgeId in remainingEdgeIds.ToList())
                {
                    Edge<T, TEvent> edge = vertexGraphBuilder.Edge(edgeId);

                    // Get the dependency node and its incoming edges IDs.
                    var dependencyNode = vertexGraphBuilder.EdgeTailNode(edgeId);
                    var dependencyNodeIncomingEdgeIds = new HashSet<T>(dependencyNode.IncomingEdges);

                    // If calculations for all the dependency edges have been completed, then use them
                    // to complete the calculations for this edge.
                    if (dependencyNodeIncomingEdgeIds.IsSubsetOf(completedEdgeIds))
                    {
                        if (!dependencyNode.Content.EarliestStartTime.HasValue)
                        {
                            int earliestStartTime =
                                dependencyNodeIncomingEdgeIds.Select(vertexGraphBuilder.Edge).Max(x => x.Content.EarliestFinishTime.Value);

                            if (dependencyNode.Content.MinimumEarliestStartTime.HasValue)
                            {
                                // At this point, augment the earliest finish time artificially (if required).
                                int proposedEarliestStartTime = dependencyNode.Content.MinimumEarliestStartTime.Value;
                                if (proposedEarliestStartTime > earliestStartTime)
                                {
                                    earliestStartTime = proposedEarliestStartTime;
                                }
                            }

                            dependencyNode.Content.EarliestStartTime = earliestStartTime;
                        }

                        int earliestFinishTime = dependencyNode.Content.EarliestFinishTime.Value;

                        if (dependencyNode.Content.MinimumFreeSlack.HasValue)
                        {
                            // At this point, augment the free slack artificially (if required).
                            int proposedEarliestFinishTime = earliestFinishTime + dependencyNode.Content.MinimumFreeSlack.Value;
                            if (proposedEarliestFinishTime > earliestFinishTime)
                            {
                                earliestFinishTime = proposedEarliestFinishTime;
                            }
                        }

                        edge.Content.EarliestFinishTime = earliestFinishTime;
                        completedEdgeIds.Add(edgeId);
                        remainingEdgeIds.Remove(edgeId);

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

            // Now complete the End nodes
            foreach (Node<T, TActivity> node in vertexGraphBuilder.EndNodes)
            {
                var nodeIncomingEdgeIds = new HashSet<T>(node.IncomingEdges);
                if (!nodeIncomingEdgeIds.IsSubsetOf(completedEdgeIds))
                {
                    throw new InvalidOperationException($@"Cannot calculate EST for activity {node.Id} as not all dependency events have EFT values.");
                }

                if (!node.Content.EarliestStartTime.HasValue)
                {
                    int earliestStartTime =
                        nodeIncomingEdgeIds.Select(vertexGraphBuilder.Edge).Max(x => x.Content.EarliestFinishTime.Value);

                    if (node.Content.MinimumEarliestStartTime.HasValue)
                    {
                        // At this point, augment the earliest finish time artificially (if required).
                        int proposedEarliestStartTime = node.Content.MinimumEarliestStartTime.Value;
                        if (proposedEarliestStartTime > earliestStartTime)
                        {
                            earliestStartTime = proposedEarliestStartTime;
                        }
                    }

                    node.Content.EarliestStartTime = earliestStartTime;
                }

                if (!node.Content.LatestFinishTime.HasValue)
                {
                    int latestFinishTime = node.Content.EarliestFinishTime.Value;

                    if (node.Content.MinimumFreeSlack.HasValue)
                    {
                        // At this point, augment the free slack artificially (if required).
                        int proposedLatestFinishTime = latestFinishTime + node.Content.MinimumFreeSlack.Value;
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

        internal static bool CalculateCriticalPathBackwardFlow<T, TActivity, TEvent>
            (this VertexGraphBuilderBase<T, TActivity, TEvent> vertexGraphBuilder)
            where TActivity : IActivity<T>
            where TEvent : IEvent<T>
            where T : struct, IComparable<T>, IEquatable<T>
        {
            if (vertexGraphBuilder == null)
            {
                throw new ArgumentNullException(nameof(vertexGraphBuilder));
            }
            if (!vertexGraphBuilder.AllDependenciesSatisfied)
            {
                return false;
            }

            // Only perform if all events a have earliest finish times.
            if (!vertexGraphBuilder.Events.All(x => x.EarliestFinishTime.HasValue))
            {
                return false;
            }

            // Only perform if all activities a have earliest finish times.
            if (!vertexGraphBuilder.Activities.All(x => x.EarliestFinishTime.HasValue))
            {
                return false;
            }

            // Only perform if all end nodes a have latest finish times.
            if (!vertexGraphBuilder.EndNodes.All(x => x.Content.LatestFinishTime.HasValue))
            {
                return false;
            }

            var completedEdgeIds = new HashSet<T>();
            var remainingEdgeIds = new HashSet<T>(vertexGraphBuilder.EdgeIds);

            int endNodesEndTime = vertexGraphBuilder.EndNodes.Select(x => x.Content.LatestFinishTime.Value).DefaultIfEmpty().Max();
            int isolatedNodesEndTime = vertexGraphBuilder.IsolatedNodes.Select(x => x.Content.LatestFinishTime.Value).DefaultIfEmpty().Max();

            int endTime = Math.Max(endNodesEndTime, isolatedNodesEndTime);

            // Complete the End nodes first to ensure the completed edge IDs
            // contains something.
            foreach (Node<T, TActivity> node in vertexGraphBuilder.EndNodes)
            {
                // Latest Finish Time.
                node.Content.LatestFinishTime = endTime;

                // Free float/slack calculations.
                node.Content.FreeSlack = node.Content.LatestFinishTime - node.Content.EarliestFinishTime;

                foreach (T incomingEdgeId in node.IncomingEdges)
                {
                    Edge<T, TEvent> incomingEdge = vertexGraphBuilder.Edge(incomingEdgeId);
                    incomingEdge.Content.LatestFinishTime = node.Content.LatestStartTime;
                    completedEdgeIds.Add(incomingEdgeId);
                    remainingEdgeIds.Remove(incomingEdgeId);
                }
            }

            // Backward flow algorithm.
            while (remainingEdgeIds.Any())
            {
                bool progress = false;
                foreach (T edgeId in remainingEdgeIds.ToList())
                {
                    Edge<T, TEvent> edge = vertexGraphBuilder.Edge(edgeId);

                    // Get the successor node and its outgoing edges IDs.
                    var successorNode = vertexGraphBuilder.EdgeHeadNode(edgeId);
                    var successorNodeOutgoingEdgeIds = new HashSet<T>(successorNode.OutgoingEdges);

                    // If calculations for all the successor edges have been completed, then use them
                    // to complete the calculations for this edge.
                    if (successorNodeOutgoingEdgeIds.IsSubsetOf(completedEdgeIds))
                    {
                        if (!successorNode.Content.LatestFinishTime.HasValue)
                        {
                            successorNode.Content.LatestFinishTime =
                                successorNodeOutgoingEdgeIds.Select(vertexGraphBuilder.Edge).Min(x => x.Content.LatestFinishTime.Value);
                        }

                        if (!successorNode.Content.FreeSlack.HasValue)
                        {
                            successorNode.Content.FreeSlack =
                                successorNodeOutgoingEdgeIds.Select(vertexGraphBuilder.EdgeHeadNode).Min(x => x.Content.EarliestStartTime.Value) -
                                successorNode.Content.EarliestStartTime -
                                successorNode.Content.Duration;
                        }

                        edge.Content.LatestFinishTime = successorNode.Content.LatestStartTime;
                        completedEdgeIds.Add(edgeId);
                        remainingEdgeIds.Remove(edgeId);

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

            // Now complete the Start nodes.
            foreach (Node<T, TActivity> node in vertexGraphBuilder.StartNodes)
            {
                var nodeOutgoingEdgeIds = new HashSet<T>(node.OutgoingEdges);
                if (!nodeOutgoingEdgeIds.IsSubsetOf(completedEdgeIds))
                {
                    throw new InvalidOperationException($@"Cannot calculate LFT for activity {node.Id} as not all dependency events have LFT values.");
                }

                if (!node.Content.LatestFinishTime.HasValue)
                {
                    node.Content.LatestFinishTime =
                        nodeOutgoingEdgeIds.Select(vertexGraphBuilder.Edge).Min(x => x.Content.LatestFinishTime.Value);
                }

                if (!node.Content.FreeSlack.HasValue)
                {
                    node.Content.FreeSlack =
                        nodeOutgoingEdgeIds.Select(vertexGraphBuilder.EdgeHeadNode).Min(x => x.Content.EarliestStartTime.Value) -
                        node.Content.EarliestStartTime -
                        node.Content.Duration;
                }
            }
            return true;
        }
    }
}
