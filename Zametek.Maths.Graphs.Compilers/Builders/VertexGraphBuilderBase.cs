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
                node.IncomingEdges.Select(x => m_Edges[x])
                .Where(x => x.Content.CanBeRemoved)
                .Select(x => x.Id);

            foreach (T incomingEdgeId in removableIncomingEdgeIds)
            {
                T tailNodeId = m_EdgeTailNodeLookup[incomingEdgeId].Id;
                HashSet<T> edgeIds;
                if (!tailNodeParallelEdgesLookup.TryGetValue(tailNodeId, out edgeIds))
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
                Node<T, TActivity> tailNode = m_EdgeTailNodeLookup[tailNodeId];
                IList<T> edgeIds = tailNodeParallelEdgesLookup[tailNodeId].ToList();
                int length = edgeIds.Count;
                // Leave one edge behind.
                for (int i = 1; i < length; i++)
                {
                    T edgeId = edgeIds[i];

                    // Remove the edge from the tail node.
                    tailNode.OutgoingEdges.Remove(edgeId);
                    m_EdgeTailNodeLookup.Remove(edgeId);

                    // Remove the edge from the head node.
                    node.IncomingEdges.Remove(edgeId);
                    m_EdgeHeadNodeLookup.Remove(edgeId);

                    // Remove the edge completely.
                    m_Edges.Remove(edgeId);
                }
            }
        }

        private void RemoveRedundantIncomingEdges(T nodeId, IDictionary<T, HashSet<T>> nodeIdAncestorLookup)
        {
            if (nodeIdAncestorLookup == null)
            {
                throw new ArgumentNullException(nameof(nodeIdAncestorLookup));
            }
            Node<T, TActivity> node = m_Nodes[nodeId];
            if (node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated)
            {
                return;
            }

            // Go through all the incoming edges and collate the
            // ancestors of their tail nodes.
            var tailNodeAncestors = new HashSet<T>(node.IncomingEdges
                .Select(x => m_EdgeTailNodeLookup[x].Id)
                .SelectMany(x => nodeIdAncestorLookup[x]));

            // Go through the incoming edges and remove any that connect
            // directly to any ancestors of the edges' tail nodes.
            // In a vertex graph, all edges should be removable.
            foreach (T edgeId in node.IncomingEdges.Select(x => m_Edges[x]).Where(x => x.Content.CanBeRemoved).Select(x => x.Id).ToList())
            {
                Node<T, TActivity> tailNode = m_EdgeTailNodeLookup[edgeId];
                T edgeTailNodeId = tailNode.Id;
                if (tailNodeAncestors.Contains(edgeTailNodeId))
                {
                    // Remove the edge from the tail node.
                    tailNode.OutgoingEdges.Remove(edgeId);
                    m_EdgeTailNodeLookup.Remove(edgeId);

                    // Remove the edge from the node itself.
                    node.IncomingEdges.Remove(edgeId);
                    m_EdgeHeadNodeLookup.Remove(edgeId);

                    // Remove the edge completely.
                    m_Edges.Remove(edgeId);
                }
            }

            // Go through all the remaining incoming edges and repeat.
            foreach (T tailNodeId in node.IncomingEdges.Select(x => m_EdgeTailNodeLookup[x].Id).ToList())
            {
                RemoveRedundantIncomingEdges(tailNodeId, nodeIdAncestorLookup);
            }
        }

        private void ResolveUnsatisfiedSuccessorActivities(T activityId)
        {
            // Check to make sure the node really exists.
            Node<T, TActivity> dependencyNode;
            if (!m_Nodes.TryGetValue(activityId, out dependencyNode))
            {
                return;
            }

            // Check to see if any existing activities were expecting this activity
            // as a dependency. If so, then then hook their nodes to this activity with an edge.
            HashSet<Node<T, TActivity>> unsatisfiedSuccessorNodes;
            if (m_UnsatisfiedSuccessorsLookup.TryGetValue(activityId, out unsatisfiedSuccessorNodes))
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
                    T edgeId = m_EdgeIdGenerator();
                    var edge = new Edge<T, TEvent>(CreateEvent(edgeId));
                    dependencyNode.OutgoingEdges.Add(edgeId);
                    m_EdgeTailNodeLookup.Add(edgeId, dependencyNode);
                    successorNode.IncomingEdges.Add(edgeId);
                    m_EdgeHeadNodeLookup.Add(edgeId, successorNode);
                    m_Edges.Add(edgeId, edge);
                }
                m_UnsatisfiedSuccessorsLookup.Remove(activityId);
            }
        }

        private void RemoveUnsatisfiedSuccessorActivity(T activityId)
        {
            // Check to make sure the node really exists.
            Node<T, TActivity> node;
            if (!m_Nodes.TryGetValue(activityId, out node))
            {
                return;
            }

            if (node.NodeType == NodeType.End
                || node.NodeType == NodeType.Normal)
            {
                // If the activity was an unsatisfied successor, then remove it from the lookup.
                IList<KeyValuePair<T, HashSet<Node<T, TActivity>>>> kvps =
                    m_UnsatisfiedSuccessorsLookup.Where(x => x.Value.Select(y => y.Id).Contains(activityId)).ToList();

                foreach (KeyValuePair<T, HashSet<Node<T, TActivity>>> kvp in kvps)
                {
                    HashSet<Node<T, TActivity>> unsatisfiedSuccessorNodes = kvp.Value;
                    unsatisfiedSuccessorNodes.RemoveWhere(x => x.Id.Equals(activityId));
                    if (!unsatisfiedSuccessorNodes.Any())
                    {
                        m_UnsatisfiedSuccessorsLookup.Remove(kvp);
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
                HashSet<Node<T, TActivity>> unsatisfiedSuccessorNodes;
                if (m_UnsatisfiedSuccessorsLookup.TryGetValue(dependencyId, out unsatisfiedSuccessorNodes))
                {
                    unsatisfiedSuccessorNodes.RemoveWhere(x => x.Id.Equals(activityId));
                    if (!unsatisfiedSuccessorNodes.Any())
                    {
                        m_UnsatisfiedSuccessorsLookup.Remove(dependencyId);
                    }
                }
            }
        }

        #endregion

        #region Overrides

        public override IEnumerable<TActivity> Activities => m_Nodes.Values.Select(x => x.Content);

        public override IEnumerable<TEvent> Events => m_Edges.Values.Select(x => x.Content);

        public override TActivity Activity(T key)
        {
            return m_Nodes[key].Content;
        }

        public override TEvent Event(T key)
        {
            return m_Edges[key].Content;
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
            if (m_Nodes.ContainsKey(activity.Id))
            {
                return false;
            }
            if (dependencies.Contains(activity.Id))
            {
                return false;
            }
            // Create a new Isolated node for the activity.
            var node = new Node<T, TActivity>(NodeType.Isolated, activity);
            m_Nodes.Add(node.Id, node);

            // We expect dependencies at some point.
            if (dependencies.Any())
            {
                node.SetNodeType(NodeType.End);

                // Check which of the expected dependencies currently exist.
                IList<T> existingDependencies = m_Nodes.Keys.Intersect(dependencies).ToList();
                IList<T> nonExistingDependencies = dependencies.Except(existingDependencies).ToList();

                // If any expected dependencies currently exist, generate an edge to connect them.
                foreach (T dependencyId in existingDependencies)
                {
                    Node<T, TActivity> dependencyNode = m_Nodes[dependencyId];
                    T edgeId = m_EdgeIdGenerator();
                    var edge = new Edge<T, TEvent>(CreateEvent(edgeId));
                    node.IncomingEdges.Add(edgeId);
                    m_EdgeHeadNodeLookup.Add(edgeId, node);

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
                    m_EdgeTailNodeLookup.Add(edgeId, dependencyNode);
                    m_Edges.Add(edgeId, edge);
                }

                // If any expected dependencies currently do not exist, then record their
                // IDs and add this node as an unsatisfied successor.
                foreach (T dependencyId in nonExistingDependencies)
                {
                    HashSet<Node<T, TActivity>> successorNodes;
                    if (!m_UnsatisfiedSuccessorsLookup.TryGetValue(dependencyId, out successorNodes))
                    {
                        successorNodes = new HashSet<Node<T, TActivity>>();
                        m_UnsatisfiedSuccessorsLookup.Add(dependencyId, successorNodes);
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

            Node<T, TActivity> node;
            if (!m_Nodes.TryGetValue(activityId, out node))
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
            IList<T> existingDependencies = m_Nodes.Keys.Intersect(dependencies).ToList();
            IList<T> nonExistingDependencies = dependencies.Except(existingDependencies).ToList();

            // If any expected dependencies currently exist, generate an edge to connect them.
            foreach (T dependencyId in existingDependencies)
            {
                Node<T, TActivity> dependencyNode = m_Nodes[dependencyId];
                T edgeId = m_EdgeIdGenerator();
                var edge = new Edge<T, TEvent>(CreateEvent(edgeId));
                node.IncomingEdges.Add(edgeId);
                m_EdgeHeadNodeLookup.Add(edgeId, node);

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
                m_EdgeTailNodeLookup.Add(edgeId, dependencyNode);
                m_Edges.Add(edgeId, edge);
            }

            // If any expected dependencies currently do not exist, then record their
            // IDs and add this node as an unsatisfied successor.
            foreach (T dependencyId in nonExistingDependencies)
            {
                HashSet<Node<T, TActivity>> successorNodes;
                if (!m_UnsatisfiedSuccessorsLookup.TryGetValue(dependencyId, out successorNodes))
                {
                    successorNodes = new HashSet<Node<T, TActivity>>();
                    m_UnsatisfiedSuccessorsLookup.Add(dependencyId, successorNodes);
                }
                successorNodes.Add(node);
            }
            return true;
        }

        public override bool RemoveActivity(T activityId)
        {
            // Retrieve the activity's node.
            Node<T, TActivity> node;
            if (!m_Nodes.TryGetValue(activityId, out node))
            {
                return false;
            }
            if (!node.Content.CanBeRemoved)
            {
                return false;
            }

            RemoveUnsatisfiedSuccessorActivity(activityId);
            m_Nodes.Remove(node.Id);

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
                    Node<T, TActivity> tailNode = m_EdgeTailNodeLookup[edgeId];

                    // Remove the edge from the tail node.
                    tailNode.OutgoingEdges.Remove(edgeId);
                    m_EdgeTailNodeLookup.Remove(edgeId);

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
                    m_EdgeHeadNodeLookup.Remove(edgeId);

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
                    m_Edges.Remove(edgeId);
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
                    Node<T, TActivity> headNode = m_EdgeHeadNodeLookup[edgeId];

                    // Remove the edge from the head node.
                    headNode.IncomingEdges.Remove(edgeId);
                    m_EdgeHeadNodeLookup.Remove(edgeId);

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
                    m_EdgeTailNodeLookup.Remove(edgeId);

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
                    m_Edges.Remove(edgeId);
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
            Node<T, TActivity> node;
            if (!m_Nodes.TryGetValue(activityId, out node))
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
            var existingDependencyLookup = new HashSet<T>(m_Nodes.Keys.Intersect(dependencies));
            IList<T> incomingEdgeIds = node.IncomingEdges.ToList();
            int length = incomingEdgeIds.Count;
            for (int i = 0; i < length; i++)
            {
                T edgeId = incomingEdgeIds[i];
                Node<T, TActivity> tailNode = m_EdgeTailNodeLookup[edgeId];

                if (!existingDependencyLookup.Contains(tailNode.Id))
                {
                    continue;
                }

                // Remove the edge from the tail node.
                tailNode.OutgoingEdges.Remove(edgeId);
                m_EdgeTailNodeLookup.Remove(edgeId);

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
                m_EdgeHeadNodeLookup.Remove(edgeId);

                // Remove the edge completely.
                m_Edges.Remove(edgeId);
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

        public override IList<T> StrongActivityDependencyIds(T activityId)
        {
            Node<T, TActivity> node = m_Nodes[activityId];
            if (node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated)
            {
                return new List<T>();
            }
            var output = new List<T>();
            foreach (Edge<T, TEvent> incomingEdge in node.IncomingEdges.Select(x => m_Edges[x]))
            {
                Node<T, TActivity> tailNode = m_EdgeTailNodeLookup[incomingEdge.Id];
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
                throw new InvalidOperationException("Cannot perform edge clean up");
            }
            this.ClearCriticalPathVariables();
            if (!this.CalculateCriticalPathForwardFlow())
            {
                throw new InvalidOperationException("Cannot calculate critical path forward flow");
            }
            if (!this.CalculateCriticalPathBackwardFlow())
            {
                throw new InvalidOperationException("Cannot calculate critical path backward flow");
            }
        }

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

            Action<T> strongConnect = null;
            strongConnect = referenceId =>
            {
                indexLookup[referenceId] = index;
                lowLinkLookup[referenceId] = index;
                index++;
                stack.Push(referenceId);

                Node<T, TActivity> referenceNode = m_Nodes[referenceId];
                if (referenceNode.NodeType == NodeType.End || referenceNode.NodeType == NodeType.Normal)
                {
                    foreach (T incomingEdgeId in referenceNode.IncomingEdges)
                    {
                        Node<T, TActivity> tailNode = m_EdgeTailNodeLookup[incomingEdgeId];
                        T tailNodeId = tailNode.Id;
                        if (indexLookup[tailNodeId] < 0)
                        {
                            strongConnect(tailNodeId);
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
                        Node<T, TActivity> currentNode = m_Nodes[currentId];
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
                    strongConnect(id);
                }
            }

            return circularDependencies;
        }

        #endregion
    }
}
