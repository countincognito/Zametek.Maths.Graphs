using System;
using System.Collections.Generic;
using System.Linq;
using Zametek.Utility;

namespace Zametek.Maths.Graphs
{
    internal static class VertexGraphBuilderExtensions
    {
        internal static void CalculateCriticalPath<T, TResourceId, TActivity, TEvent>
            (this VertexGraphBuilderBase<T, TResourceId, TActivity, TEvent> vertexGraphBuilder)
            where TActivity : IActivity<T, TResourceId>
            where TEvent : IEvent<T>
            where T : struct, IComparable<T>, IEquatable<T>
            where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        {
            if (vertexGraphBuilder is null)
            {
                throw new ArgumentNullException(nameof(vertexGraphBuilder));
            }

            bool edgesCleaned = vertexGraphBuilder.CleanUpEdges();

            if (!edgesCleaned)
            {
                throw new InvalidOperationException(Properties.Resources.CannotPerformEdgeCleanUp);
            }

            vertexGraphBuilder.ClearCriticalPathVariables();

            if (!vertexGraphBuilder.CalculateCriticalPathForwardFlow())
            {
                throw new InvalidOperationException(Properties.Resources.CannotCalculateCriticalPathForwardFlow);
            }
            if (!vertexGraphBuilder.CalculateCriticalPathBackwardFlow())
            {
                throw new InvalidOperationException(Properties.Resources.CannotCalculateCriticalPathBackwardFlow);
            }
        }

        internal static bool CalculateCriticalPathForwardFlow<T, TResourceId, TActivity, TEvent>
            (this VertexGraphBuilderBase<T, TResourceId, TActivity, TEvent> vertexGraphBuilder)
            where TActivity : IActivity<T, TResourceId>
            where TEvent : IEvent<T>
            where T : struct, IComparable<T>, IEquatable<T>
            where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        {
            if (vertexGraphBuilder is null)
            {
                throw new ArgumentNullException(nameof(vertexGraphBuilder));
            }
            if (!vertexGraphBuilder.AllDependenciesSatisfied)
            {
                return false;
            }
            if (vertexGraphBuilder.FindInvalidPreCompilationConstraints().Any())
            {
                return false;
            }

            // We can assume at this point that all the activity constraints are valid.

            var completedEdgeIds = new HashSet<T>();
            var remainingEdgeIds = new HashSet<T>(vertexGraphBuilder.EdgeIds);

            // First complete the Isolated nodes.
            foreach (Node<T, TActivity> node in vertexGraphBuilder.IsolatedNodes)
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
                int latestFinishTime = node.Content.EarliestFinishTime.Value;

                if (node.Content.MaximumLatestFinishTime.HasValue)
                {
                    int proposedLatestFinishTime = node.Content.MaximumLatestFinishTime.Value;

                    // Diminish the earliest finish time artificially (if required).
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

            // Complete the Start nodes first to ensure the completed edge IDs
            // contains something.
            foreach (Node<T, TActivity> node in vertexGraphBuilder.StartNodes)
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
                    Edge<T, TEvent> outgoingEdge = vertexGraphBuilder.Edge(outgoingEdgeId);
                    int earliestFinishTime = node.Content.EarliestFinishTime.Value;

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
                    completedEdgeIds.Add(outgoingEdgeId);
                    remainingEdgeIds.Remove(outgoingEdgeId);
                }
            }

            // Forward flow algorithm.
            while (remainingEdgeIds.Any())
            {
                bool progress = false;
                List<T> remainingEdgeIdList = remainingEdgeIds.ToList();

                if (vertexGraphBuilder.WhenTesting)
                {
                    remainingEdgeIdList.Shuffle();
                }

                foreach (T edgeId in remainingEdgeIdList)
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
                            int earliestStartTime = dependencyNodeIncomingEdgeIds
                                .Select(vertexGraphBuilder.Edge)
                                .Max(x => x.Content.EarliestFinishTime.Value);

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

                        int earliestFinishTime = dependencyNode.Content.EarliestFinishTime.Value;

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
                    int earliestStartTime = nodeIncomingEdgeIds
                        .Select(vertexGraphBuilder.Edge)
                        .Max(x => x.Content.EarliestFinishTime.Value);

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
                    int latestFinishTime = node.Content.EarliestFinishTime.Value;

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

        internal static bool CalculateCriticalPathBackwardFlow<T, TResourceId, TActivity, TEvent>
            (this VertexGraphBuilderBase<T, TResourceId, TActivity, TEvent> vertexGraphBuilder)
            where TActivity : IActivity<T, TResourceId>
            where TEvent : IEvent<T>
            where T : struct, IComparable<T>, IEquatable<T>
            where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        {
            if (vertexGraphBuilder is null)
            {
                throw new ArgumentNullException(nameof(vertexGraphBuilder));
            }
            if (!vertexGraphBuilder.AllDependenciesSatisfied)
            {
                return false;
            }
            if (vertexGraphBuilder.FindInvalidPreCompilationConstraints().Any())
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

            // We can assume at this point that all the activity constraints are valid.

            var completedEdgeIds = new HashSet<T>();
            var remainingEdgeIds = new HashSet<T>(vertexGraphBuilder.EdgeIds);

            int endNodesEndTime = vertexGraphBuilder.EndNodes.Select(x => x.Content.LatestFinishTime.Value).DefaultIfEmpty().Max();
            int isolatedNodesEndTime = vertexGraphBuilder.IsolatedNodes.Select(x => x.Content.LatestFinishTime.Value).DefaultIfEmpty().Max();

            int endTime = Math.Max(endNodesEndTime, isolatedNodesEndTime);

            // Complete the End nodes first to ensure the completed edge IDs
            // contains something.
            foreach (Node<T, TActivity> node in vertexGraphBuilder.EndNodes)
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
                    Edge<T, TEvent> incomingEdge = vertexGraphBuilder.Edge(incomingEdgeId);
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
                    completedEdgeIds.Add(incomingEdgeId);
                    remainingEdgeIds.Remove(incomingEdgeId);
                }
            }

            // Backward flow algorithm.
            while (remainingEdgeIds.Any())
            {
                bool progress = false;
                List<T> remainingEdgeIdList = remainingEdgeIds.ToList();

                if (vertexGraphBuilder.WhenTesting)
                {
                    remainingEdgeIdList.Shuffle();
                }

                foreach (T edgeId in remainingEdgeIdList)
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
                            int latestFinishTime = successorNodeOutgoingEdgeIds
                                .Select(vertexGraphBuilder.Edge)
                                .Min(x => x.Content.LatestFinishTime.Value);

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
                            int latestFinishTime = successorNodeOutgoingEdgeIds
                                .Select(vertexGraphBuilder.EdgeHeadNode)
                                .Min(x => x.Content.EarliestStartTime.Value);

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

                            successorNode.Content.FreeSlack = latestFinishTime - successorNode.Content.EarliestStartTime - successorNode.Content.Duration;
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
                    int latestFinishTime = nodeOutgoingEdgeIds
                        .Select(vertexGraphBuilder.Edge)
                        .Min(x => x.Content.LatestFinishTime.Value);

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
                    int latestFinishTime = nodeOutgoingEdgeIds
                        .Select(vertexGraphBuilder.EdgeHeadNode)
                        .Min(x => x.Content.EarliestStartTime.Value);

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

                    node.Content.FreeSlack = latestFinishTime - node.Content.EarliestStartTime - node.Content.Duration;
                }
            }
            return true;
        }
    }
}
