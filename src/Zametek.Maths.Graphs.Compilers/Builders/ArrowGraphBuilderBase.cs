using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    public abstract class ArrowGraphBuilderBase<T, TResourceId, TWorkStreamId, TActivity, TEvent>
        : GraphBuilderBase<T, TResourceId, TWorkStreamId, TActivity, TEvent, TActivity, TEvent>
        where TActivity : IActivity<T, TResourceId, TWorkStreamId>
        where TEvent : IEvent<T>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        #region Ctors

        protected ArrowGraphBuilderBase(
            Func<T> edgeIdGenerator,
            Func<T> nodeIdGenerator,
            Func<T, TEvent> eventGenerator,
            Func<T, int?, int?, TEvent> eventGeneratorWithTimes,
            Func<T, TActivity> dummyActivityGenerator)
            : base(edgeIdGenerator, nodeIdGenerator, eventGenerator)
        {
            EventGeneratorWithTimes = eventGeneratorWithTimes ?? throw new ArgumentNullException(nameof(eventGeneratorWithTimes));
            DummyActivityGenerator = dummyActivityGenerator ?? throw new ArgumentNullException(nameof(dummyActivityGenerator));
            Initialize();
        }

        protected ArrowGraphBuilderBase(
            Graph<T, TActivity, TEvent> graph,
            Func<T> edgeIdGenerator,
            Func<T> nodeIdGenerator,
            Func<T, TEvent> eventGenerator)
            : base(graph, edgeIdGenerator, nodeIdGenerator, eventGenerator)
        {
            // Check Start and End nodes.
            if (StartNodes.Count() == 1)
            {
                StartNode = StartNodes.First();
            }
            else
            {
                throw new ArgumentException(Properties.Resources.Message_ArrowGraphContainsMoreThanOneStartNode);
            }
            if (EndNodes.Count() == 1)
            {
                EndNode = EndNodes.First();
            }
            else
            {
                throw new ArgumentException(Properties.Resources.Message_ArrowGraphContainsMoreThanOneEndNode);
            }
        }

        #endregion

        #region Properties

        protected Func<T, int?, int?, TEvent> EventGeneratorWithTimes { get; }

        protected Func<T, TActivity> DummyActivityGenerator { get; }

        public Node<T, TEvent> StartNode
        {
            get;
            protected set;
        }

        public Node<T, TEvent> EndNode
        {
            get;
            protected set;
        }

        #endregion

        #region Public Methods

        public bool RemoveDummyActivity(T activityId)
        {
            // Retrieve the activity's edge.
            if (!EdgeLookup.TryGetValue(activityId, out Edge<T, TActivity> edge))
            {
                return false;
            }
            if (!edge.Content.IsDummy)
            {
                return false;
            }
            if (!edge.Content.CanBeRemoved)
            {
                return false;
            }

            Node<T, TEvent> tailNode = EdgeTailNodeLookup[activityId];
            Node<T, TEvent> headNode = EdgeHeadNodeLookup[activityId];

            // Check to make sure that no other edges will be made parallel
            // by removing this edge.
            if (HaveDecendantOrAncestorOverlap(tailNode, headNode)
                && !ShareMoreThanOneEdge(tailNode, headNode))
            {
                return false;
            }

            // Remove the edge from the tail node.
            tailNode.OutgoingEdges.Remove(activityId);
            EdgeTailNodeLookup.Remove(activityId);

            // Remove the edge from the head node.
            headNode.IncomingEdges.Remove(activityId);
            EdgeHeadNodeLookup.Remove(activityId);

            // Remove the edge completely.
            EdgeLookup.Remove(activityId);

            // If the head node is not the End node, and it has no more incoming
            // edges, then transfer the head node's outgoing edges to the tail node.
            if (headNode.NodeType != NodeType.End
                && headNode.NodeType != NodeType.Isolated
                && !headNode.IncomingEdges.Any())
            {
                IList<T> headNodeOutgoingEdgeIds = headNode.OutgoingEdges.ToList();
                foreach (T headNodeOutgoingEdgeId in headNodeOutgoingEdgeIds)
                {
                    bool changeTailSuccess = ChangeEdgeTailNode(headNodeOutgoingEdgeId, tailNode.Id);
                    if (!changeTailSuccess)
                    {
                        throw new InvalidOperationException($@"Unable to change tail node of edge {headNodeOutgoingEdgeId} to node {tailNode.Id} when removing dummy activity {activityId}");
                    }
                }
            }
            else if (tailNode.NodeType != NodeType.Start
                && tailNode.NodeType != NodeType.Isolated
                && !tailNode.OutgoingEdges.Any())
            {
                // If the tail node is not the Start node, and it has no more outgoing
                // edges, then transfer the tail node's incoming edges to the head node.
                IList<T> tailNodeIncomingEdgeIds = tailNode.IncomingEdges.ToList();
                foreach (T tailNodeIncomingEdgeId in tailNodeIncomingEdgeIds)
                {
                    bool changeHeadSuccess = ChangeEdgeHeadNode(tailNodeIncomingEdgeId, headNode.Id);
                    if (!changeHeadSuccess)
                    {
                        throw new InvalidOperationException($@"Unable to change head node of edge {tailNodeIncomingEdgeId} to node {headNode.Id} when removing dummy activity {activityId}");
                    }
                }
            }
            return true;
        }

        #endregion

        #region Private Methods

        private void Initialize()
        {
            T startEventId = NodeIdGenerator();
            StartNode = new Node<T, TEvent>(NodeType.Start, EventGeneratorWithTimes(startEventId, 0, 0));
            NodeLookup.Add(StartNode.Id, StartNode);
            T endEventId = NodeIdGenerator();
            EndNode = new Node<T, TEvent>(NodeType.End, EventGenerator(endEventId));
            NodeLookup.Add(EndNode.Id, EndNode);
        }

        private bool RedirectDummyEdges()
        {
            if (!AllDependenciesSatisfied)
            {
                return false;
            }
            IList<ICircularDependency<T>> circularDependencies = FindStrongCircularDependencies();
            if (circularDependencies.Any())
            {
                return false;
            }

            // Go through each node that is not an End or Isolated node.
            foreach (Node<T, TEvent> node in NodeLookup.Values.Where(x => x.NodeType != NodeType.End && x.NodeType != NodeType.Isolated).ToList())
            {
                // Get the outgoing dummy edges and their head nodes.
                var outgoingDummyEdgeIdLookup = new HashSet<T>(
                    node.OutgoingEdges.Select(x => EdgeLookup[x])
                    .Where(x => x.Content.IsDummy && x.Content.CanBeRemoved).Select(x => x.Id));

                IList<Node<T, TEvent>> dummyEdgeSuccessorNodes =
                    outgoingDummyEdgeIdLookup.Select(x => EdgeHeadNodeLookup[x]).ToList();

                // Now from the successor nodes, work backwards to find
                // all the dependency nodes that share the same successor
                // nodes via dummy edges.

                // First find all the removable dummy edges that have the
                // successor nodes as head nodes.
                IList<IEnumerable<T>> dummyEdgeIdsToSuccessorNodes =
                    dummyEdgeSuccessorNodes
                    .Select(x => x.IncomingEdges.Select(y => EdgeLookup[y])
                    .Where(y => y.Content.IsDummy && y.Content.CanBeRemoved)
                    .Select(y => y.Id))
                    .ToList();

                if (!dummyEdgeIdsToSuccessorNodes.Any())
                {
                    continue;
                }

                // Now find the subset of dependency nodes that are common
                // to all the successor nodes via removable dummy edges.
                IList<T> commonDependencyNodes =
                    dummyEdgeIdsToSuccessorNodes.Select(x => x.Select(y => EdgeTailNodeLookup[y].Id))
                    .Aggregate((previous, next) => previous.Intersect(next)).ToList();

                var commonDependencyNodeLookup = new HashSet<T>(commonDependencyNodes);

                // Now filter the dummy edges by whether they originate from
                // the common dependency nodes.
                IList<T> commonDependencyEdgeIds =
                    dummyEdgeIdsToSuccessorNodes
                    .SelectMany(x => x)
                    .Where(x => commonDependencyNodeLookup.Contains(EdgeTailNodeLookup[x].Id)).ToList();

                // In order to redirect any common dependencies to the original
                // node, it cannot have any successor nodes other than the common
                // successor nodes (i.e. its successor nodes must be a subset of
                // the common successor nodes).
                var allSuccessorNodeLookup = new HashSet<T>(node.OutgoingEdges.Select(x => EdgeHeadNodeLookup[x].Id));

                var commonSuccessorNodes =
                    commonDependencyEdgeIds.Select(x => EdgeHeadNodeLookup[x].Id);

                var commonSuccessorNodeLookup = new HashSet<T>(commonSuccessorNodes);

                if (!allSuccessorNodeLookup.IsSubsetOf(commonSuccessorNodeLookup))
                {
                    continue;
                }

                // Redirect all common dependencies towards the original node.
                foreach (T commonDependencyEdgeId in commonDependencyEdgeIds.Where(x => !outgoingDummyEdgeIdLookup.Contains(x)).OrderBy(x => x))
                {
                    bool changeHeadSuccess = ChangeEdgeHeadNode(commonDependencyEdgeId, node.Id);
                    if (!changeHeadSuccess)
                    {
                        throw new InvalidOperationException($@"Unable to change head node of edge {commonDependencyEdgeId} to node {node.Id} when redirecting dummy activities");
                    }
                }

                RemoveParallelIncomingDummyEdges(node);
            }
            return true;
        }

        private void RemoveParallelIncomingDummyEdges(Node<T, TEvent> node)
        {
            if (node is null)
            {
                throw new ArgumentNullException(nameof(node));
            }
            // Clean up any dummy edges that are parallel coming into the head node.
            if (node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated)
            {
                return;
            }
            // First, find the tail nodes that connect to this node via dummy edges.
            var tailNodeParallelDummyEdgesLookup = new Dictionary<T, HashSet<T>>();
            IEnumerable<T> removableIncomingDummyEdgeIds =
                node.IncomingEdges.Select(x => EdgeLookup[x])
                .Where(x => x.Content.IsDummy && x.Content.CanBeRemoved)
                .Select(x => x.Id);

            foreach (T incomingDummyEdgeId in removableIncomingDummyEdgeIds)
            {
                T tailNodeId = EdgeTailNodeLookup[incomingDummyEdgeId].Id;
                if (!tailNodeParallelDummyEdgesLookup.TryGetValue(tailNodeId, out HashSet<T> dummyEdgeIds))
                {
                    dummyEdgeIds = new HashSet<T>();
                    tailNodeParallelDummyEdgesLookup.Add(tailNodeId, dummyEdgeIds);
                }
                if (!dummyEdgeIds.Contains(incomingDummyEdgeId))
                {
                    dummyEdgeIds.Add(incomingDummyEdgeId);
                }
            }

            // Now find the tail nodes that connect to this node via multiple dummy edges.
            IList<T> setsOfMoreThanOneDummyEdge =
                tailNodeParallelDummyEdgesLookup
                .Where(x => x.Value.Count > 1)
                .Select(x => x.Key)
                .ToList();

            foreach (T tailNodeId in setsOfMoreThanOneDummyEdge)
            {
                IList<T> dummyEdgeIds = tailNodeParallelDummyEdgesLookup[tailNodeId].ToList();
                int length = dummyEdgeIds.Count;
                // Leave one dummy edge behind.
                for (int i = 1; i < length; i++)
                {
                    RemoveDummyActivity(dummyEdgeIds[i]);
                }
            }
        }

        private bool RemoveRedundantDummyEdges()
        {
            if (!AllDependenciesSatisfied)
            {
                return false;
            }
            IList<ICircularDependency<T>> circularDependencies = FindStrongCircularDependencies();
            if (circularDependencies.Any())
            {
                return false;
            }

            // Go through and remove all the dummy edges that are
            // the only outgoing edge of their tail node, and also
            // the only incoming edge of their head node.
            foreach (Edge<T, TActivity> edge in GetDummyEdgesInDecendingOrder().Where(x => x.Content.CanBeRemoved))
            {
                Node<T, TEvent> tailNode = EdgeTailNodeLookup[edge.Id];
                Node<T, TEvent> headNode = EdgeHeadNodeLookup[edge.Id];
                if (tailNode.OutgoingEdges.Count == 1
                    && headNode.IncomingEdges.Count == 1)
                {
                    RemoveDummyActivity(edge.Id);
                }
            }

            // Next, go through and remove all the dummy edges that
            // are the only incoming edge of their head node.
            foreach (Edge<T, TActivity> edge in GetDummyEdgesInDecendingOrder().Where(x => x.Content.CanBeRemoved))
            {
                if (EdgeHeadNodeLookup[edge.Id].IncomingEdges.Count == 1)
                {
                    RemoveDummyActivity(edge.Id);
                }
            }

            // Next, go through and remove all the dummy edges that
            // are the only outgoing edge of their tail node.
            foreach (Edge<T, TActivity> edge in GetDummyEdgesInDecendingOrder().Where(x => x.Content.CanBeRemoved))
            {
                if (EdgeTailNodeLookup[edge.Id].OutgoingEdges.Count == 1)
                {
                    RemoveDummyActivity(edge.Id);
                }
            }

            // Remove parallel dummy edges (if they exist).
            foreach (Node<T, TEvent> node in NodeLookup.Values.ToList())
            {
                RemoveParallelIncomingDummyEdges(node);
            }
            return true;
        }

        private void RemoveRedundantIncomingDummyEdges(T nodeId, IDictionary<T, HashSet<T>> nodeIdAncestorLookup)
        {
            if (nodeIdAncestorLookup is null)
            {
                throw new ArgumentNullException(nameof(nodeIdAncestorLookup));
            }
            Node<T, TEvent> node = NodeLookup[nodeId];
            if (node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated)
            {
                return;
            }

            // Go through all the incoming edges and collate the
            // ancestors of their tail nodes.
            var tailNodeAncestors = new HashSet<T>(node.IncomingEdges
                .Select(x => EdgeTailNodeLookup[x].Id)
                .SelectMany(x => nodeIdAncestorLookup[x]));

            // Go through the incoming dummy edges and remove any that
            // connect directly to any ancestors of the non-dummy edges'
            // tail nodes.
            foreach (T dummyEdgeId in node.IncomingEdges.Select(x => EdgeLookup[x]).Where(x => x.Content.IsDummy && x.Content.CanBeRemoved).Select(x => x.Id).ToList())
            {
                T dummyEdgeTailNodeId = EdgeTailNodeLookup[dummyEdgeId].Id;
                if (tailNodeAncestors.Contains(dummyEdgeTailNodeId))
                {
                    RemoveDummyActivity(dummyEdgeId);
                }
            }

            // Go through all the remaining incoming edges and repeat.
            foreach (T tailNodeId in node.IncomingEdges.Select(x => EdgeTailNodeLookup[x].Id).ToList())
            {
                RemoveRedundantIncomingDummyEdges(tailNodeId, nodeIdAncestorLookup);
            }
        }

        private IList<Edge<T, TActivity>> GetDummyEdgesInDecendingOrder()
        {
            var recordedEdges = new HashSet<T>();
            T startNodeId = StartNode.Id;
            var edgesInDecendingOrder = new List<Edge<T, TActivity>>();
            GetEdgesInDecendingOrder(startNodeId, edgesInDecendingOrder, recordedEdges);
            return edgesInDecendingOrder.Where(x => x.Content.IsDummy).ToList();
        }

        private void ResolveUnsatisfiedSuccessorActivities(T activityId)
        {
            // Check to make sure the edge really exists.
            if (!EdgeLookup.ContainsKey(activityId))
            {
                return;
            }
            // Check to see if any existing activities were expecting this activity
            // as a dependency. If so, then then hook up their tail nodes to this
            // activity's head node with a dummy edge.
            if (UnsatisfiedSuccessorsLookup.TryGetValue(activityId, out HashSet<Node<T, TEvent>> unsatisfiedSuccessorTailNodes))
            {
                // We know that there are unsatisfied dependencies, so create a head node.
                T headEventId = NodeIdGenerator();
                var headNode = new Node<T, TEvent>(EventGenerator(headEventId));
                headNode.IncomingEdges.Add(activityId);
                EdgeHeadNodeLookup.Add(activityId, headNode);
                NodeLookup.Add(headNode.Id, headNode);

                foreach (Node<T, TEvent> tailNode in unsatisfiedSuccessorTailNodes)
                {
                    T dummyEdgeId = EdgeIdGenerator();
                    var dummyEdge = new Edge<T, TActivity>(DummyActivityGenerator(dummyEdgeId));
                    tailNode.IncomingEdges.Add(dummyEdgeId);
                    EdgeHeadNodeLookup.Add(dummyEdgeId, tailNode);
                    headNode.OutgoingEdges.Add(dummyEdgeId);
                    EdgeTailNodeLookup.Add(dummyEdgeId, headNode);
                    EdgeLookup.Add(dummyEdge.Id, dummyEdge);
                }
                UnsatisfiedSuccessorsLookup.Remove(activityId);
            }
            else
            {
                // No existing activities were expecting this activity as a dependency,
                // so attach it directly to the end node via a dummy.
                T headEventId = NodeIdGenerator();
                Node<T, TEvent> dependencyHeadNode = new Node<T, TEvent>(EventGenerator(headEventId));

                dependencyHeadNode.IncomingEdges.Add(activityId);
                EdgeHeadNodeLookup.Add(activityId, dependencyHeadNode);
                NodeLookup.Add(dependencyHeadNode.Id, dependencyHeadNode);

                T dummyEdgeId = EdgeIdGenerator();
                var dummyEdge = new Edge<T, TActivity>(DummyActivityGenerator(dummyEdgeId));

                dependencyHeadNode.OutgoingEdges.Add(dummyEdgeId);
                EdgeTailNodeLookup.Add(dummyEdgeId, dependencyHeadNode);
                EdgeLookup.Add(dummyEdgeId, dummyEdge);

                EndNode.IncomingEdges.Add(dummyEdgeId);
                EdgeHeadNodeLookup.Add(dummyEdgeId, EndNode);
            }
        }

        #endregion

        #region Overrides

        public override IEnumerable<TActivity> Activities => EdgeLookup.Values.Select(x => x.Content);

        public override IEnumerable<TEvent> Events => NodeLookup.Values.Select(x => x.Content);

        public override TActivity Activity(T key)
        {
            return EdgeLookup[key].Content;
        }

        public override TEvent Event(T key)
        {
            return NodeLookup[key].Content;
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
            if (dependencies is null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }
            if (EdgeLookup.ContainsKey(activity.Id))
            {
                return false;
            }
            if (dependencies.Contains(activity.Id))
            {
                return false;
            }
            // Create a new edge for the activity.
            var edge = new Edge<T, TActivity>(activity);
            EdgeLookup.Add(edge.Id, edge);

            // We expect dependencies at some point.
            if (dependencies.Any())
            {
                // Since we use dummy edges to connect all tail nodes, we can create
                // a new tail node for this edge.
                T tailEventId = NodeIdGenerator();
                var tailNode = new Node<T, TEvent>(EventGenerator(tailEventId));
                tailNode.OutgoingEdges.Add(edge.Id);
                EdgeTailNodeLookup.Add(edge.Id, tailNode);
                NodeLookup.Add(tailNode.Id, tailNode);

                // Check which of the expected dependencies currently exist.
                IList<T> existingDependencies = EdgeLookup.Keys.Intersect(dependencies).ToList();
                IList<T> nonExistingDependencies = dependencies.Except(existingDependencies).ToList();

                // If any expected dependencies currently exist, then hook up their head
                // node to this edge's tail node with dummy edges.
                foreach (T dependencyId in existingDependencies)
                {
                    Node<T, TEvent> dependencyHeadNode = EdgeHeadNodeLookup[dependencyId];
                    T dummyEdgeId = EdgeIdGenerator();
                    var dummyEdge = new Edge<T, TActivity>(DummyActivityGenerator(dummyEdgeId));
                    tailNode.IncomingEdges.Add(dummyEdgeId);
                    EdgeHeadNodeLookup.Add(dummyEdgeId, tailNode);

                    // If the head node of the dependency is the End node, then convert it.
                    if (dependencyHeadNode.NodeType == NodeType.End)
                    {
                        dependencyHeadNode.SetNodeType(NodeType.Normal);
                    }

                    dependencyHeadNode.OutgoingEdges.Add(dummyEdgeId);
                    EdgeTailNodeLookup.Add(dummyEdgeId, dependencyHeadNode);
                    EdgeLookup.Add(dummyEdgeId, dummyEdge);
                }

                // If any expected dependencies currently do not exist, then record their
                // IDs and add this edge's tail node as an unsatisfied successor.
                foreach (T dependencyId in nonExistingDependencies)
                {
                    if (!UnsatisfiedSuccessorsLookup.TryGetValue(dependencyId, out HashSet<Node<T, TEvent>> tailNodes))
                    {
                        tailNodes = new HashSet<Node<T, TEvent>>();
                        UnsatisfiedSuccessorsLookup.Add(dependencyId, tailNodes);
                    }
                    tailNodes.Add(tailNode);
                }
            }
            else
            {
                // No dependencies, so attach it directly to the start node.
                StartNode.OutgoingEdges.Add(edge.Id);
                EdgeTailNodeLookup.Add(edge.Id, StartNode);
            }
            ResolveUnsatisfiedSuccessorActivities(edge.Id);
            return true;
        }

        public override bool AddActivityDependencies(T activityId, HashSet<T> dependencies)
        {
            throw new NotImplementedException();
        }

        public override bool RemoveActivity(T activityId)
        {
            throw new NotImplementedException();
        }

        public override bool RemoveActivityDependencies(T activityId, HashSet<T> dependencies)
        {
            throw new NotImplementedException();
        }

        public override IList<T> ActivityDependencyIds(T activityId)
        {
            Node<T, TEvent> tailNode = EdgeTailNodeLookup[activityId];
            if (tailNode.NodeType == NodeType.Start || tailNode.NodeType == NodeType.Isolated)
            {
                return new List<T>();
            }
            var output = new List<T>();
            foreach (Edge<T, TActivity> incomingEdge in tailNode.IncomingEdges.Select(x => EdgeLookup[x]))
            {
                output.Add(incomingEdge.Id);
            }
            return output;
        }

        public override IList<T> StrongActivityDependencyIds(T activityId)
        {
            Node<T, TEvent> tailNode = EdgeTailNodeLookup[activityId];
            if (tailNode.NodeType == NodeType.Start || tailNode.NodeType == NodeType.Isolated)
            {
                return new List<T>();
            }
            var output = new List<T>();
            foreach (Edge<T, TActivity> incomingEdge in tailNode.IncomingEdges.Select(x => EdgeLookup[x]))
            {
                if (incomingEdge.Content.IsDummy)
                {
                    output.AddRange(StrongActivityDependencyIds(incomingEdge.Id));
                }
                else
                {
                    output.Add(incomingEdge.Id);
                }
            }
            return output;
        }

        public override bool TransitiveReduction()
        {
            // For Arrow Graphs only dummy edges need to be reduced.
            IDictionary<T, HashSet<T>> ancestorNodesLookup = GetAncestorNodesLookup();
            if (ancestorNodesLookup is null)
            {
                return false;
            }
            foreach (T endNodeId in EndNodes.Select(x => x.Id))
            {
                RemoveRedundantIncomingDummyEdges(endNodeId, ancestorNodesLookup);
            }
            return true;
        }

        public override bool RedirectEdges()
        {
            return RedirectDummyEdges();
        }

        public override bool RemoveRedundantEdges()
        {
            return RemoveRedundantDummyEdges();
        }

        public override void CalculateCriticalPath()
        {
            ArrowGraphBuilderExtensions.CalculateCriticalPath(this);
        }

        // Tarjan's strongly connected components algorithm.
        // https://en.wikipedia.org/wiki/Tarjan%27s_strongly_connected_components_algorithm
        protected override IList<ICircularDependency<T>> FindStronglyConnectedComponents()
        {
            int index = 0;
            var stack = new Stack<T>();
            var indexLookup = new Dictionary<T, int>();
            var lowLinkLookup = new Dictionary<T, int>();
            var circularDependencies = new List<ICircularDependency<T>>();

            foreach (T id in EdgeIds)
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

                Edge<T, TActivity> referenceEdge = EdgeLookup[referenceId];
                Node<T, TEvent> tailNode = EdgeTailNodeLookup[referenceId];
                if (tailNode.NodeType == NodeType.End || tailNode.NodeType == NodeType.Normal)
                {
                    foreach (T incomingEdgeId in tailNode.IncomingEdges)
                    {
                        if (indexLookup[incomingEdgeId] < 0)
                        {
                            StrongConnect(incomingEdgeId);
                            lowLinkLookup[referenceId] = Math.Min(lowLinkLookup[referenceId], lowLinkLookup[incomingEdgeId]);
                        }
                        else if (stack.Contains(incomingEdgeId))
                        {
                            lowLinkLookup[referenceId] = Math.Min(lowLinkLookup[referenceId], indexLookup[incomingEdgeId]);
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
                        Edge<T, TActivity> currentEdge = EdgeLookup[currentId];
                        if (!currentEdge.Content.CanBeRemoved)
                        {
                            circularDependency.Dependencies.Add(currentId);
                        }
                    } while (!referenceId.Equals(currentId));
                    circularDependencies.Add(circularDependency);
                }
            }

            foreach (T id in EdgeIds)
            {
                if (indexLookup[id] < 0)
                {
                    StrongConnect(id);
                }
            }

            return circularDependencies;
        }

        public override void Reset()
        {
            base.Reset();
            Initialize();
        }

        #endregion
    }
}
