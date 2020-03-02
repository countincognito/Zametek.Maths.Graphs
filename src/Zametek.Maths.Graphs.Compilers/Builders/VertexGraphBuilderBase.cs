using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    // In a vertex graph, all edges should be removable.
    public abstract class VertexGraphBuilderBase<T, TActivity, TEvent>
        : GraphBuilderBase<T, TEvent, TActivity, TActivity, TEvent>
        where TActivity : IActivity<T>
        where TEvent : IEvent<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        #region Ctors

        protected VertexGraphBuilderBase(
            Func<T> edgeIdGenerator,
            Func<T> nodeIdGenerator,
            Func<T, TEvent> createEvent)
            : base(edgeIdGenerator, nodeIdGenerator, createEvent)
        { }

        protected VertexGraphBuilderBase(
            Graph<T, TEvent, TActivity> graph,
            Func<T> edgeIdGenerator,
            Func<T> nodeIdGenerator,
            Func<T, TEvent> createEvent)
            : base(graph, edgeIdGenerator, nodeIdGenerator, createEvent)
        { }

        #endregion

        #region Private Methods

        private void RemoveParallelIncomingEdges(Node<T, TActivity> node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }
            // Clean up any dummy edges that are parallel coming into the head node.
            if (node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated)
            {
                return;
            }
            // First, find the tail nodes that connect to this node via ALL edges.
            // In a vertex graph, all edges should be removable.
            var tailNodeParallelEdgesLookup = new Dictionary<T, HashSet<T>>();
            IEnumerable<T> removableIncomingEdgeIds =
                node.IncomingEdges.Select(x => EdgeLookup[x])
                .Where(x => x.Content.CanBeRemoved)
                .Select(x => x.Id);

            foreach (T incomingEdgeId in removableIncomingEdgeIds)
            {
                T tailNodeId = EdgeTailNodeLookup[incomingEdgeId].Id;
                if (!tailNodeParallelEdgesLookup.TryGetValue(tailNodeId, out HashSet<T> edgeIds))
                {
                    edgeIds = new HashSet<T>();
                    tailNodeParallelEdgesLookup.Add(tailNodeId, edgeIds);
                }
                if (!edgeIds.Contains(incomingEdgeId))
                {
                    edgeIds.Add(incomingEdgeId);
                }
            }

            // Now find the tail nodes that connect to this node via multiple edges.
            IList<T> setsOfMoreThanOneEdge =
                tailNodeParallelEdgesLookup
                .Where(x => x.Value.Count > 1)
                .Select(x => x.Key)
                .ToList();

            foreach (T tailNodeId in setsOfMoreThanOneEdge)
            {
                Node<T, TActivity> tailNode = EdgeTailNodeLookup[tailNodeId];
                IList<T> edgeIds = tailNodeParallelEdgesLookup[tailNodeId].ToList();
                int length = edgeIds.Count;
                // Leave one edge behind.
                for (int i = 1; i < length; i++)
                {
                    T edgeId = edgeIds[i];

                    // Remove the edge from the tail node.
                    tailNode.OutgoingEdges.Remove(edgeId);
                    EdgeTailNodeLookup.Remove(edgeId);

                    // Remove the edge from the head node.
                    node.IncomingEdges.Remove(edgeId);
                    EdgeHeadNodeLookup.Remove(edgeId);

                    // Remove the edge completely.
                    EdgeLookup.Remove(edgeId);
                }
            }
        }

        private void RemoveRedundantIncomingEdges(T nodeId, IDictionary<T, HashSet<T>> nodeIdAncestorLookup)
        {
            if (nodeIdAncestorLookup == null)
            {
                throw new ArgumentNullException(nameof(nodeIdAncestorLookup));
            }
            Node<T, TActivity> node = NodeLookup[nodeId];
            if (node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated)
            {
                return;
            }

            // Go through all the incoming edges and collate the
            // ancestors of their tail nodes.
            var tailNodeAncestors = new HashSet<T>(node.IncomingEdges
                .Select(x => EdgeTailNodeLookup[x].Id)
                .SelectMany(x => nodeIdAncestorLookup[x]));

            // Go through the incoming edges and remove any that connect
            // directly to any ancestors of the edges' tail nodes.
            // In a vertex graph, all edges should be removable.
            foreach (T edgeId in node.IncomingEdges.Select(x => EdgeLookup[x]).Where(x => x.Content.CanBeRemoved).Select(x => x.Id).ToList())
            {
                Node<T, TActivity> tailNode = EdgeTailNodeLookup[edgeId];
                T edgeTailNodeId = tailNode.Id;
                if (tailNodeAncestors.Contains(edgeTailNodeId))
                {
                    // Remove the edge from the tail node.
                    tailNode.OutgoingEdges.Remove(edgeId);
                    EdgeTailNodeLookup.Remove(edgeId);

                    // Remove the edge from the node itself.
                    node.IncomingEdges.Remove(edgeId);
                    EdgeHeadNodeLookup.Remove(edgeId);

                    // Remove the edge completely.
                    EdgeLookup.Remove(edgeId);
                }
            }

            // Go through all the remaining incoming edges and repeat.
            foreach (T tailNodeId in node.IncomingEdges.Select(x => EdgeTailNodeLookup[x].Id).ToList())
            {
                RemoveRedundantIncomingEdges(tailNodeId, nodeIdAncestorLookup);
            }
        }

        private void ResolveUnsatisfiedSuccessorActivities(T activityId)
        {
            // Check to make sure the node really exists.
            if (!NodeLookup.TryGetValue(activityId, out Node<T, TActivity> dependencyNode))
            {
                return;
            }

            // Check to see if any existing activities were expecting this activity
            // as a dependency. If so, then then hook their nodes to this activity with an edge.
            if (UnsatisfiedSuccessorsLookup.TryGetValue(activityId, out HashSet<Node<T, TActivity>> unsatisfiedSuccessorNodes))
            {
                // If the dependency node is an End or Isolated node, then convert it.
                if (dependencyNode.NodeType == NodeType.End)
                {
                    dependencyNode.SetNodeType(NodeType.Normal);
                }
                else if (dependencyNode.NodeType == NodeType.Isolated)
                {
                    dependencyNode.SetNodeType(NodeType.Start);
                }

                foreach (Node<T, TActivity> successorNode in unsatisfiedSuccessorNodes)
                {
                    T edgeId = EdgeIdGenerator();
                    var edge = new Edge<T, TEvent>(CreateEvent(edgeId));
                    dependencyNode.OutgoingEdges.Add(edgeId);
                    EdgeTailNodeLookup.Add(edgeId, dependencyNode);
                    successorNode.IncomingEdges.Add(edgeId);
                    EdgeHeadNodeLookup.Add(edgeId, successorNode);
                    EdgeLookup.Add(edgeId, edge);
                }
                UnsatisfiedSuccessorsLookup.Remove(activityId);
            }
        }

        private void RemoveUnsatisfiedSuccessorActivity(T activityId)
        {
            // Check to make sure the node really exists.
            if (!NodeLookup.TryGetValue(activityId, out Node<T, TActivity> node))
            {
                return;
            }

            if (node.NodeType == NodeType.End
                || node.NodeType == NodeType.Normal)
            {
                // If the activity was an unsatisfied successor, then remove it from the lookup.
                IList<KeyValuePair<T, HashSet<Node<T, TActivity>>>> kvps =
                    UnsatisfiedSuccessorsLookup.Where(x => x.Value.Select(y => y.Id).Contains(activityId)).ToList();

                foreach (KeyValuePair<T, HashSet<Node<T, TActivity>>> kvp in kvps)
                {
                    HashSet<Node<T, TActivity>> unsatisfiedSuccessorNodes = kvp.Value;
                    unsatisfiedSuccessorNodes.RemoveWhere(x => x.Id.Equals(activityId));
                    if (!unsatisfiedSuccessorNodes.Any())
                    {
                        UnsatisfiedSuccessorsLookup.Remove(kvp);
                    }
                }
            }
        }

        private void RemoveUnsatisfiedSuccessorActivityDependencies(T activityId, HashSet<T> dependencies)
        {
            if (dependencies == null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }

            // If the activity was an unsatisfied successor for these dependencies,
            // then remove them from the lookup.
            foreach (T dependencyId in dependencies)
            {
                if (UnsatisfiedSuccessorsLookup.TryGetValue(dependencyId, out HashSet<Node<T, TActivity>> unsatisfiedSuccessorNodes))
                {
                    unsatisfiedSuccessorNodes.RemoveWhere(x => x.Id.Equals(activityId));
                    if (!unsatisfiedSuccessorNodes.Any())
                    {
                        UnsatisfiedSuccessorsLookup.Remove(dependencyId);
                    }
                }
            }
        }

        #endregion

        #region Overrides

        public override IEnumerable<TActivity> Activities => NodeLookup.Values.Select(x => x.Content);

        public override IEnumerable<TEvent> Events => EdgeLookup.Values.Select(x => x.Content);

        public override TActivity Activity(T key)
        {
            return NodeLookup[key].Content;
        }

        public override TEvent Event(T key)
        {
            return EdgeLookup[key].Content;
        }

        public override bool AddActivity(TActivity activity)
        {
            return AddActivity(activity, new HashSet<T>());
        }

        public override bool AddActivity(TActivity activity, HashSet<T> dependencies)
        {
            if (activity == null)
            {
                throw new ArgumentNullException(nameof(activity));
            }
            if (dependencies == null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }
            if (NodeLookup.ContainsKey(activity.Id))
            {
                return false;
            }
            if (dependencies.Contains(activity.Id))
            {
                return false;
            }
            // Create a new Isolated node for the activity.
            var node = new Node<T, TActivity>(NodeType.Isolated, activity);
            NodeLookup.Add(node.Id, node);

            // We expect dependencies at some point.
            if (dependencies.Any())
            {
                node.SetNodeType(NodeType.End);

                // Check which of the expected dependencies currently exist.
                IList<T> existingDependencies = NodeLookup.Keys.Intersect(dependencies).ToList();
                IList<T> nonExistingDependencies = dependencies.Except(existingDependencies).ToList();

                // If any expected dependencies currently exist, generate an edge to connect them.
                foreach (T dependencyId in existingDependencies)
                {
                    Node<T, TActivity> dependencyNode = NodeLookup[dependencyId];
                    T edgeId = EdgeIdGenerator();
                    var edge = new Edge<T, TEvent>(CreateEvent(edgeId));
                    node.IncomingEdges.Add(edgeId);
                    EdgeHeadNodeLookup.Add(edgeId, node);

                    // If the dependency node is an End or Isolated node, then convert it.
                    if (dependencyNode.NodeType == NodeType.End)
                    {
                        dependencyNode.SetNodeType(NodeType.Normal);
                    }
                    else if (dependencyNode.NodeType == NodeType.Isolated)
                    {
                        dependencyNode.SetNodeType(NodeType.Start);
                    }

                    dependencyNode.OutgoingEdges.Add(edgeId);
                    EdgeTailNodeLookup.Add(edgeId, dependencyNode);
                    EdgeLookup.Add(edgeId, edge);
                }

                // If any expected dependencies currently do not exist, then record their
                // IDs and add this node as an unsatisfied successor.
                foreach (T dependencyId in nonExistingDependencies)
                {
                    if (!UnsatisfiedSuccessorsLookup.TryGetValue(dependencyId, out HashSet<Node<T, TActivity>> successorNodes))
                    {
                        successorNodes = new HashSet<Node<T, TActivity>>();
                        UnsatisfiedSuccessorsLookup.Add(dependencyId, successorNodes);
                    }
                    successorNodes.Add(node);
                }
            }
            ResolveUnsatisfiedSuccessorActivities(node.Id);
            return true;
        }

        public override bool AddActivityDependencies(T activityId, HashSet<T> dependencies)
        {
            if (dependencies == null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }

            if (!NodeLookup.TryGetValue(activityId, out Node<T, TActivity> node))
            {
                return false;
            }
            if (!dependencies.Any())
            {
                return true;
            }
            if (dependencies.Contains(activityId))
            {
                return false;
            }

            // If the node is an Start or Isolated node, then convert it.
            if (node.NodeType == NodeType.Start)
            {
                node.SetNodeType(NodeType.Normal);
            }
            else if (node.NodeType == NodeType.Isolated)
            {
                node.SetNodeType(NodeType.End);
            }

            // Check which of the expected dependencies currently exist.
            IList<T> existingDependencies = NodeLookup.Keys.Intersect(dependencies).ToList();
            IList<T> nonExistingDependencies = dependencies.Except(existingDependencies).ToList();

            // If any expected dependencies currently exist, generate an edge to connect them.
            foreach (T dependencyId in existingDependencies)
            {
                Node<T, TActivity> dependencyNode = NodeLookup[dependencyId];
                T edgeId = EdgeIdGenerator();
                var edge = new Edge<T, TEvent>(CreateEvent(edgeId));
                node.IncomingEdges.Add(edgeId);
                EdgeHeadNodeLookup.Add(edgeId, node);

                // If the dependency node is an End or Isolated node, then convert it.
                if (dependencyNode.NodeType == NodeType.End)
                {
                    dependencyNode.SetNodeType(NodeType.Normal);
                }
                else if (dependencyNode.NodeType == NodeType.Isolated)
                {
                    dependencyNode.SetNodeType(NodeType.Start);
                }

                dependencyNode.OutgoingEdges.Add(edgeId);
                EdgeTailNodeLookup.Add(edgeId, dependencyNode);
                EdgeLookup.Add(edgeId, edge);
            }

            // If any expected dependencies currently do not exist, then record their
            // IDs and add this node as an unsatisfied successor.
            foreach (T dependencyId in nonExistingDependencies)
            {
                if (!UnsatisfiedSuccessorsLookup.TryGetValue(dependencyId, out HashSet<Node<T, TActivity>> successorNodes))
                {
                    successorNodes = new HashSet<Node<T, TActivity>>();
                    UnsatisfiedSuccessorsLookup.Add(dependencyId, successorNodes);
                }
                successorNodes.Add(node);
            }
            return true;
        }

        public override bool RemoveActivity(T activityId)
        {
            // Retrieve the activity's node.
            if (!NodeLookup.TryGetValue(activityId, out Node<T, TActivity> node))
            {
                return false;
            }
            if (!node.Content.CanBeRemoved)
            {
                return false;
            }

            RemoveUnsatisfiedSuccessorActivity(activityId);
            NodeLookup.Remove(node.Id);

            if (node.NodeType == NodeType.Isolated)
            {
                return true;
            }

            if (node.NodeType == NodeType.End
                || node.NodeType == NodeType.Normal)
            {
                IList<T> incomingEdgeIds = node.IncomingEdges.ToList();
                int length = incomingEdgeIds.Count;
                for (int i = 0; i < length; i++)
                {
                    T edgeId = incomingEdgeIds[i];
                    Node<T, TActivity> tailNode = EdgeTailNodeLookup[edgeId];

                    // Remove the edge from the tail node.
                    tailNode.OutgoingEdges.Remove(edgeId);
                    EdgeTailNodeLookup.Remove(edgeId);

                    if (!tailNode.OutgoingEdges.Any())
                    {
                        if (tailNode.NodeType == NodeType.Normal)
                        {
                            tailNode.SetNodeType(NodeType.End);
                        }
                        else if (tailNode.NodeType == NodeType.Start)
                        {
                            tailNode.SetNodeType(NodeType.Isolated);
                        }
                    }

                    // Remove the edge from the head node.
                    node.IncomingEdges.Remove(edgeId);
                    EdgeHeadNodeLookup.Remove(edgeId);

                    if (!node.IncomingEdges.Any())
                    {
                        if (node.NodeType == NodeType.Normal)
                        {
                            node.SetNodeType(NodeType.Start);
                        }
                        else if (node.NodeType == NodeType.End)
                        {
                            node.SetNodeType(NodeType.Isolated);
                        }
                    }

                    // Remove the edge completely.
                    EdgeLookup.Remove(edgeId);
                }
            }

            if (node.NodeType == NodeType.Start
                || node.NodeType == NodeType.Normal)
            {
                IList<T> outgoingEdgeIds = node.OutgoingEdges.ToList();
                int length = outgoingEdgeIds.Count;
                for (int i = 0; i < length; i++)
                {
                    T edgeId = outgoingEdgeIds[i];
                    Node<T, TActivity> headNode = EdgeHeadNodeLookup[edgeId];

                    // Remove the edge from the head node.
                    headNode.IncomingEdges.Remove(edgeId);
                    EdgeHeadNodeLookup.Remove(edgeId);

                    if (!headNode.IncomingEdges.Any())
                    {
                        if (headNode.NodeType == NodeType.Normal)
                        {
                            headNode.SetNodeType(NodeType.Start);
                        }
                        else if (headNode.NodeType == NodeType.End)
                        {
                            headNode.SetNodeType(NodeType.Isolated);
                        }
                    }

                    // Remove the edge from the tail node.
                    node.OutgoingEdges.Remove(edgeId);
                    EdgeTailNodeLookup.Remove(edgeId);

                    if (!node.OutgoingEdges.Any())
                    {
                        if (node.NodeType == NodeType.Normal)
                        {
                            node.SetNodeType(NodeType.End);
                        }
                        else if (node.NodeType == NodeType.Start)
                        {
                            node.SetNodeType(NodeType.Isolated);
                        }
                    }

                    // Remove the edge completely.
                    EdgeLookup.Remove(edgeId);
                }
            }
            return true;
        }

        public override bool RemoveActivityDependencies(T activityId, HashSet<T> dependencies)
        {
            if (dependencies == null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }
            if (!NodeLookup.TryGetValue(activityId, out Node<T, TActivity> node))
            {
                return false;
            }
            if (!dependencies.Any())
            {
                return true;
            }

            RemoveUnsatisfiedSuccessorActivityDependencies(activityId, dependencies);

            if (node.NodeType == NodeType.Start
                || node.NodeType == NodeType.Isolated)
            {
                return true;
            }

            // If any dependencies currently exist, remove them.
            var existingDependencyLookup = new HashSet<T>(NodeLookup.Keys.Intersect(dependencies));
            IList<T> incomingEdgeIds = node.IncomingEdges.ToList();
            int length = incomingEdgeIds.Count;
            for (int i = 0; i < length; i++)
            {
                T edgeId = incomingEdgeIds[i];
                Node<T, TActivity> tailNode = EdgeTailNodeLookup[edgeId];

                if (!existingDependencyLookup.Contains(tailNode.Id))
                {
                    continue;
                }

                // Remove the edge from the tail node.
                tailNode.OutgoingEdges.Remove(edgeId);
                EdgeTailNodeLookup.Remove(edgeId);

                if (!tailNode.OutgoingEdges.Any())
                {
                    if (tailNode.NodeType == NodeType.Normal)
                    {
                        tailNode.SetNodeType(NodeType.End);
                    }
                    else if (tailNode.NodeType == NodeType.Start)
                    {
                        tailNode.SetNodeType(NodeType.Isolated);
                    }
                }

                // Remove the edge from the head node.
                node.IncomingEdges.Remove(edgeId);
                EdgeHeadNodeLookup.Remove(edgeId);

                // Remove the edge completely.
                EdgeLookup.Remove(edgeId);
            }

            if (!node.IncomingEdges.Any())
            {
                if (node.NodeType == NodeType.Normal)
                {
                    node.SetNodeType(NodeType.Start);
                }
                else if (node.NodeType == NodeType.End)
                {
                    node.SetNodeType(NodeType.Isolated);
                }
            }

            return true;
        }

        public override IList<T> ActivityDependencyIds(T activityId)
        {
            Node<T, TActivity> node = NodeLookup[activityId];
            if (node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated)
            {
                return new List<T>();
            }
            var output = new List<T>();
            foreach (Edge<T, TEvent> incomingEdge in node.IncomingEdges.Select(x => EdgeLookup[x]))
            {
                Node<T, TActivity> tailNode = EdgeTailNodeLookup[incomingEdge.Id];
                output.Add(tailNode.Id);
            }
            return output;
        }

        public override IList<T> StrongActivityDependencyIds(T activityId)
        {
            Node<T, TActivity> node = NodeLookup[activityId];
            if (node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated)
            {
                return new List<T>();
            }
            var output = new List<T>();
            foreach (Edge<T, TEvent> incomingEdge in node.IncomingEdges.Select(x => EdgeLookup[x]))
            {
                Node<T, TActivity> tailNode = EdgeTailNodeLookup[incomingEdge.Id];
                if (tailNode.Content.IsDummy)
                {
                    output.AddRange(StrongActivityDependencyIds(tailNode.Id));
                }
                else
                {
                    output.Add(tailNode.Id);
                }
            }
            return output;
        }

        public override bool TransitiveReduction()
        {
            IDictionary<T, HashSet<T>> ancestorNodesLookup = GetAncestorNodesLookup();
            if (ancestorNodesLookup == null)
            {
                return false;
            }
            foreach (T endNodeId in EndNodes.Select(x => x.Id))
            {
                RemoveRedundantIncomingEdges(endNodeId, ancestorNodesLookup);
            }
            return true;
        }

        public override bool RedirectEdges()
        {
            // Edges should not need to be redirected in a vertex graph.
            return true;
        }

        public override bool RemoveRedundantEdges()
        {
            // All redundant edges should have been removed by other methods.
            return true;
        }

        public override void CalculateCriticalPath()
        {
            bool edgesCleaned = CleanUpEdges();
            if (!edgesCleaned)
            {
                throw new InvalidOperationException(@"Cannot perform edge clean up");
            }
            this.ClearCriticalPathVariables();
            if (!this.CalculateCriticalPathForwardFlow())
            {
                throw new InvalidOperationException(@"Cannot calculate critical path forward flow");
            }
            if (!this.CalculateCriticalPathBackwardFlow())
            {
                throw new InvalidOperationException(@"Cannot calculate critical path backward flow");
            }
        }

        // Tarjan's strongly connected components algorithm.
        // https://en.wikipedia.org/wiki/Tarjan%27s_strongly_connected_components_algorithm
        protected override IList<CircularDependency<T>> FindStronglyConnectedComponents()
        {
            int index = 0;
            var stack = new Stack<T>();
            var indexLookup = new Dictionary<T, int>();
            var lowLinkLookup = new Dictionary<T, int>();
            var circularDependencies = new List<CircularDependency<T>>();

            foreach (T id in NodeIds)
            {
                indexLookup.Add(id, -1);
                lowLinkLookup.Add(id, -1);
            }

            void StrongConnect(T referenceId)
            {
                indexLookup[referenceId] = index;
                lowLinkLookup[referenceId] = index;
                index++;
                stack.Push(referenceId);

                Node<T, TActivity> referenceNode = NodeLookup[referenceId];
                if (referenceNode.NodeType == NodeType.End || referenceNode.NodeType == NodeType.Normal)
                {
                    foreach (T incomingEdgeId in referenceNode.IncomingEdges)
                    {
                        Node<T, TActivity> tailNode = EdgeTailNodeLookup[incomingEdgeId];
                        T tailNodeId = tailNode.Id;
                        if (indexLookup[tailNodeId] < 0)
                        {
                            StrongConnect(tailNodeId);
                            lowLinkLookup[referenceId] = Math.Min(lowLinkLookup[referenceId], lowLinkLookup[tailNodeId]);
                        }
                        else if (stack.Contains(tailNodeId))
                        {
                            lowLinkLookup[referenceId] = Math.Min(lowLinkLookup[referenceId], indexLookup[tailNodeId]);
                        }
                    }
                }

                if (lowLinkLookup[referenceId] == indexLookup[referenceId])
                {
                    var circularDependency = new CircularDependency<T>(Enumerable.Empty<T>());
                    T currentId;
                    do
                    {
                        currentId = stack.Pop();
                        Node<T, TActivity> currentNode = NodeLookup[currentId];
                        if (!currentNode.Content.CanBeRemoved)
                        {
                            circularDependency.Dependencies.Add(currentId);
                        }
                    } while (!referenceId.Equals(currentId));
                    circularDependencies.Add(circularDependency);
                }
            };

            foreach (T id in NodeIds)
            {
                if (indexLookup[id] < 0)
                {
                    StrongConnect(id);
                }
            }

            return circularDependencies;
        }

        #endregion
    }
}
