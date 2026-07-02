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
            var completedEdgeIds = new HashSet<T>();
            var remainingEdgeIds = new HashSet<T>(state.EdgeIds);

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
                    completedEdgeIds.Add(outgoingEdgeId);
                    remainingEdgeIds.Remove(outgoingEdgeId);
                }
            }

            // Forward flow algorithm.
            while (remainingEdgeIds.Count != 0)
            {
                bool progress = false;
                List<T> remainingEdgeIdList = remainingEdgeIds.ToList();

                if (shuffle)
                {
                    remainingEdgeIdList.Shuffle();
                }

                foreach (T edgeId in remainingEdgeIdList)
                {
                    Edge<T, IEvent<T>> edge = state.Edge(edgeId);

                    // Get the dependency node and its incoming edges IDs.
                    var dependencyNode = state.EdgeTailNode(edgeId);
                    var dependencyNodeIncomingEdgeIds = new HashSet<T>(dependencyNode.IncomingEdges);

                    // If calculations for all the dependency edges have been completed, then use them
                    // to complete the calculations for this edge.
                    if (dependencyNodeIncomingEdgeIds.IsSubsetOf(completedEdgeIds))
                    {
                        if (!dependencyNode.Content.EarliestStartTime.HasValue)
                        {
                            int earliestStartTime = dependencyNodeIncomingEdgeIds
                                .Select(x => state.Edge(x))
                                .Max(x => x.Content.EarliestFinishTime!.Value);

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
                    throw new InvalidOperationException(Properties.Resources.Message_CannotCalculateEarliestFinishTimesDueToCyclicDependency);
                }
            }

            // Now complete the End nodes.
            foreach (Node<T, TActivity> node in state.EndNodes)
            {
                var nodeIncomingEdgeIds = new HashSet<T>(node.IncomingEdges);
                if (!nodeIncomingEdgeIds.IsSubsetOf(completedEdgeIds))
                {
                    throw new InvalidOperationException($@"Cannot calculate EST for activity {node.Id} as not all dependency events have EFT values.");
                }

                if (!node.Content.EarliestStartTime.HasValue)
                {
                    int earliestStartTime = nodeIncomingEdgeIds
                        .Select(x => state.Edge(x))
                        .Max(x => x.Content.EarliestFinishTime!.Value);

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
            var completedEdgeIds = new HashSet<T>();
            var remainingEdgeIds = new HashSet<T>(state.EdgeIds);

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
                    completedEdgeIds.Add(incomingEdgeId);
                    remainingEdgeIds.Remove(incomingEdgeId);
                }
            }

            // Backward flow algorithm.
            while (remainingEdgeIds.Count != 0)
            {
                bool progress = false;
                List<T> remainingEdgeIdList = remainingEdgeIds.ToList();

                if (shuffle)
                {
                    remainingEdgeIdList.Shuffle();
                }

                foreach (T edgeId in remainingEdgeIdList)
                {
                    Edge<T, IEvent<T>> edge = state.Edge(edgeId);

                    // Get the successor node and its outgoing edges IDs.
                    var successorNode = state.EdgeHeadNode(edgeId);
                    var successorNodeOutgoingEdgeIds = new HashSet<T>(successorNode.OutgoingEdges);

                    // If calculations for all the successor edges have been completed, then use them
                    // to complete the calculations for this edge.
                    if (successorNodeOutgoingEdgeIds.IsSubsetOf(completedEdgeIds))
                    {
                        if (!successorNode.Content.LatestFinishTime.HasValue)
                        {
                            int latestFinishTime = successorNodeOutgoingEdgeIds
                                .Select(x => state.Edge(x))
                                .Min(x => x.Content.LatestFinishTime!.Value);

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
                                .Select(x => state.EdgeHeadNode(x))
                                .Min(x => x.Content.EarliestStartTime!.Value);

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
                    throw new InvalidOperationException(Properties.Resources.Message_CannotCalculateLatestFinishTimesDueToCyclicDependency);
                }
            }

            // Now complete the Start nodes.
            foreach (Node<T, TActivity> node in startNodesList)
            {
                var nodeOutgoingEdgeIds = new HashSet<T>(node.OutgoingEdges);
                if (!nodeOutgoingEdgeIds.IsSubsetOf(completedEdgeIds))
                {
                    throw new InvalidOperationException($@"Cannot calculate LFT for activity {node.Id} as not all dependency events have LFT values.");
                }

                if (!node.Content.LatestFinishTime.HasValue)
                {
                    int latestFinishTime = nodeOutgoingEdgeIds
                        .Select(x => state.Edge(x))
                        .Select(x => x.Content.LatestFinishTime!.Value)
                        .DefaultIfEmpty()
                        .Min();

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
                        .Select(x => state.EdgeHeadNode(x))
                        .Select(x => x.Content.EarliestStartTime!.Value)
                        .DefaultIfEmpty()
                        .Min();

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
    }
}
