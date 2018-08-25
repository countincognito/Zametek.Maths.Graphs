using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    public abstract class ArrowGraphBuilderBase<T, TActivity, TEvent>
        : GraphBuilderBase<T, TActivity, TEvent, TActivity, TEvent>
        where TActivity : IActivity<T>
        where TEvent : IEvent<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        #region Fields

        protected readonly Func<T, int?, int?, TEvent> m_CreateEventWithTimes;
        protected readonly Func<T, TActivity> m_CreateDummyActivity;

        #endregion

        #region Ctors

        protected ArrowGraphBuilderBase(
            Func<T> edgeIdGenerator,
            Func<T> nodeIdGenerator,
            Func<T, TEvent> createEvent,
            Func<T, int?, int?, TEvent> createEventWithTimes,
            Func<T, TActivity> createDummyActivity)
            : base(edgeIdGenerator, nodeIdGenerator, createEvent)
        {
            m_CreateEventWithTimes = createEventWithTimes ?? throw new ArgumentNullException(nameof(createEventWithTimes));
            m_CreateDummyActivity = createDummyActivity ?? throw new ArgumentNullException(nameof(createDummyActivity));
            Initialize();
        }

        protected ArrowGraphBuilderBase(
            Graph<T, TActivity, TEvent> graph,
            Func<T> edgeIdGenerator,
            Func<T> nodeIdGenerator,
            Func<T, TEvent> createEvent)
            : base(graph, edgeIdGenerator, nodeIdGenerator, createEvent)
        {
            // Check Start and End nodes.
            if (StartNodes.Count() == 1)
            {
                StartNode = StartNodes.First();
            }
            else
            {
                throw new ArgumentException(@"ArrowGraph contains more than one Start node");
            }
            if (EndNodes.Count() == 1)
            {
                EndNode = EndNodes.First();
            }
            else
            {
                throw new ArgumentException(@"ArrowGraph contains more than one End node");
            }
        }

        #endregion

        #region Properties

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
            Edge<T, TActivity> edge;
            if (!m_Edges.TryGetValue(activityId, out edge))
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

            Node<T, TEvent> tailNode = m_EdgeTailNodeLookup[activityId];
            Node<T, TEvent> headNode = m_EdgeHeadNodeLookup[activityId];

            // Check to make sure that no other edges will be made parallel
            // by removing this edge.
            if (HaveDecendantOrAncestorOverlap(tailNode, headNode)
                && !ShareMoreThanOneEdge(tailNode, headNode))
            {
                return false;
            }

            // Remove the edge from the tail node.
            tailNode.OutgoingEdges.Remove(activityId);
            m_EdgeTailNodeLookup.Remove(activityId);

            // Remove the edge from the head node.
            headNode.IncomingEdges.Remove(activityId);
            m_EdgeHeadNodeLookup.Remove(activityId);

            // Remove the edge completely.
            m_Edges.Remove(activityId);

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

        #region Protected Methods

        protected TEvent CreateEvent(T id, int? earliestFinishTime, int? latestFinishTime)
        {
            return m_CreateEventWithTimes(id, earliestFinishTime, latestFinishTime);
        }

        protected TActivity CreateDummyActivity(T id)
        {
            return m_CreateDummyActivity(id);
        }

        #endregion

        #region Private Methods

        private void Initialize()
        {
            T startEventId = m_NodeIdGenerator();
            StartNode = new Node<T, TEvent>(NodeType.Start, CreateEvent(startEventId, 0, 0));
            m_Nodes.Add(StartNode.Id, StartNode);
            T endEventId = m_NodeIdGenerator();
            EndNode = new Node<T, TEvent>(NodeType.End, CreateEvent(endEventId));
            m_Nodes.Add(EndNode.Id, EndNode);
        }

        private bool RedirectDummyEdges()
        {
            if (!AllDependenciesSatisfied)
            {
                return false;
            }
            IList<CircularDependency<T>> circularDependencies = FindStrongCircularDependencies();
            if (circularDependencies.Any())
            {
                return false;
            }

            // Go through each node that is not an End or Isolated node.
            foreach (Node<T, TEvent> node in m_Nodes.Values.Where(x => x.NodeType != NodeType.End && x.NodeType != NodeType.Isolated).ToList())
            {
                // Get the outgoing dummy edges and their head nodes.
                var outgoingDummyEdgeIdLookup = new HashSet<T>(
                    node.OutgoingEdges.Select(x => m_Edges[x])
                    .Where(x => x.Content.IsDummy && x.Content.CanBeRemoved).Select(x => x.Id));

                IList<Node<T, TEvent>> dummyEdgeSuccessorNodes =
                    outgoingDummyEdgeIdLookup.Select(x => m_EdgeHeadNodeLookup[x]).ToList();

                // Now from the successor nodes, work backwards to find
                // all the dependency nodes that share the same successor
                // nodes via dummy edges.

                // First find all the removable dummy edges that have the
                // successor nodes as head nodes.
                IList<IEnumerable<T>> dummyEdgeIdsToSuccessorNodes =
                    dummyEdgeSuccessorNodes
                    .Select(x => x.IncomingEdges.Select(y => m_Edges[y])
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
                    dummyEdgeIdsToSuccessorNodes.Select(x => x.Select(y => m_EdgeTailNodeLookup[y].Id))
                    .Aggregate((previous, next) => previous.Intersect(next)).ToList();

                var commonDependencyNodeLookup = new HashSet<T>(commonDependencyNodes);

                // Now filter the dummy edges by whether they originate from
                // the common dependency nodes.
                IList<T> commonDependencyEdgeIds =
                    dummyEdgeIdsToSuccessorNodes
                    .SelectMany(x => x)
                    .Where(x => commonDependencyNodeLookup.Contains(m_EdgeTailNodeLookup[x].Id)).ToList();

                // In order to redirect any common dependencies to the original
                // node, it cannot have any successor nodes other than the common
                // successor nodes (i.e. its successor nodes must be a subset of
                // the common successor nodes).
                var allSuccessorNodeLookup = new HashSet<T>(node.OutgoingEdges.Select(x => m_EdgeHeadNodeLookup[x].Id));

                var commonSuccessorNodes =
                    commonDependencyEdgeIds.Select(x => m_EdgeHeadNodeLookup[x].Id);

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
            if (node == null)
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
                node.IncomingEdges.Select(x => m_Edges[x])
                .Where(x => x.Content.IsDummy && x.Content.CanBeRemoved)
                .Select(x => x.Id);

            foreach (T incomingDummyEdgeId in removableIncomingDummyEdgeIds)
            {
                T tailNodeId = m_EdgeTailNodeLookup[incomingDummyEdgeId].Id;
                HashSet<T> dummyEdgeIds;
                if (!tailNodeParallelDummyEdgesLookup.TryGetValue(tailNodeId, out dummyEdgeIds))
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
            IList<CircularDependency<T>> circularDependencies = FindStrongCircularDependencies();
            if (circularDependencies.Any())
            {
                return false;
            }

            // Go through and remove all the dummy edges that are
            // the only outgoing edge of their tail node, and also
            // the only incoming edge of their head node.
            foreach (Edge<T, TActivity> edge in GetDummyEdgesInDecendingOrder().Where(x => x.Content.CanBeRemoved))
            {
                Node<T, TEvent> tailNode = m_EdgeTailNodeLookup[edge.Id];
                Node<T, TEvent> headNode = m_EdgeHeadNodeLookup[edge.Id];
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
                if (m_EdgeHeadNodeLookup[edge.Id].IncomingEdges.Count == 1)
                {
                    RemoveDummyActivity(edge.Id);
                }
            }

            // Next, go through and remove all the dummy edges that
            // are the only outgoing edge of their tail node.
            foreach (Edge<T, TActivity> edge in GetDummyEdgesInDecendingOrder().Where(x => x.Content.CanBeRemoved))
            {
                if (m_EdgeTailNodeLookup[edge.Id].OutgoingEdges.Count == 1)
                {
                    RemoveDummyActivity(edge.Id);
                }
            }

            // Remove parallel dummy edges (if they exist).
            foreach (Node<T, TEvent> node in m_Nodes.Values.ToList())
            {
                RemoveParallelIncomingDummyEdges(node);
            }
            return true;
        }

        private void RemoveRedundantIncomingDummyEdges(T nodeId, IDictionary<T, HashSet<T>> nodeIdAncestorLookup)
        {
            if (nodeIdAncestorLookup == null)
            {
                throw new ArgumentNullException(nameof(nodeIdAncestorLookup));
            }
            Node<T, TEvent> node = m_Nodes[nodeId];
            if (node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated)
            {
                return;
            }

            // Go through all the incoming edges and collate the
            // ancestors of their tail nodes.
            var tailNodeAncestors = new HashSet<T>(node.IncomingEdges
                .Select(x => m_EdgeTailNodeLookup[x].Id)
                .SelectMany(x => nodeIdAncestorLookup[x]));

            // Go through the incoming dummy edges and remove any that
            // connect directly to any ancestors of the non-dummy edges'
            // tail nodes.
            foreach (T dummyEdgeId in node.IncomingEdges.Select(x => m_Edges[x]).Where(x => x.Content.IsDummy && x.Content.CanBeRemoved).Select(x => x.Id).ToList())
            {
                T dummyEdgeTailNodeId = m_EdgeTailNodeLookup[dummyEdgeId].Id;
                if (tailNodeAncestors.Contains(dummyEdgeTailNodeId))
                {
                    RemoveDummyActivity(dummyEdgeId);
                }
            }

            // Go through all the remaining incoming edges and repeat.
            foreach (T tailNodeId in node.IncomingEdges.Select(x => m_EdgeTailNodeLookup[x].Id).ToList())
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
            if (!m_Edges.ContainsKey(activityId))
            {
                return;
            }
            // Check to see if any existing activities were expecting this activity
            // as a dependency. If so, then then hook up their tail nodes to this
            // activity's head node with a dummy edge.
            HashSet<Node<T, TEvent>> unsatisfiedSuccessorTailNodes;
            if (m_UnsatisfiedSuccessorsLookup.TryGetValue(activityId, out unsatisfiedSuccessorTailNodes))
            {
                // We know that there are unsatisfied dependencies, so create a head node.
                T headEventId = m_NodeIdGenerator();
                var headNode = new Node<T, TEvent>(CreateEvent(headEventId));
                headNode.IncomingEdges.Add(activityId);
                m_EdgeHeadNodeLookup.Add(activityId, headNode);
                m_Nodes.Add(headNode.Id, headNode);

                foreach (Node<T, TEvent> tailNode in unsatisfiedSuccessorTailNodes)
                {
                    T dummyEdgeId = m_EdgeIdGenerator();
                    var dummyEdge = new Edge<T, TActivity>(CreateDummyActivity(dummyEdgeId));
                    tailNode.IncomingEdges.Add(dummyEdgeId);
                    m_EdgeHeadNodeLookup.Add(dummyEdgeId, tailNode);
                    headNode.OutgoingEdges.Add(dummyEdgeId);
                    m_EdgeTailNodeLookup.Add(dummyEdgeId, headNode);
                    m_Edges.Add(dummyEdge.Id, dummyEdge);
                }
                m_UnsatisfiedSuccessorsLookup.Remove(activityId);
            }
            else
            {
                // No existing activities were expecting this activity as a dependency,
                // so attach it directly to the end node via a dummy.
                T headEventId = m_NodeIdGenerator();
                Node<T, TEvent> dependencyHeadNode = new Node<T, TEvent>(CreateEvent(headEventId));

                dependencyHeadNode.IncomingEdges.Add(activityId);
                m_EdgeHeadNodeLookup.Add(activityId, dependencyHeadNode);
                m_Nodes.Add(dependencyHeadNode.Id, dependencyHeadNode);

                T dummyEdgeId = m_EdgeIdGenerator();
                var dummyEdge = new Edge<T, TActivity>(CreateDummyActivity(dummyEdgeId));

                dependencyHeadNode.OutgoingEdges.Add(dummyEdgeId);
                m_EdgeTailNodeLookup.Add(dummyEdgeId, dependencyHeadNode);
                m_Edges.Add(dummyEdgeId, dummyEdge);

                EndNode.IncomingEdges.Add(dummyEdgeId);
                m_EdgeHeadNodeLookup.Add(dummyEdgeId, EndNode);
            }
        }

        #endregion

        #region Overrides

        public override IEnumerable<TActivity> Activities => m_Edges.Values.Select(x => x.Content);

        public override IEnumerable<TEvent> Events => m_Nodes.Values.Select(x => x.Content);

        public override TActivity Activity(T key)
        {
            return m_Edges[key].Content;
        }

        public override TEvent Event(T key)
        {
            return m_Nodes[key].Content;
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
            if (m_Edges.ContainsKey(activity.Id))
            {
                return false;
            }
            if (dependencies.Contains(activity.Id))
            {
                return false;
            }
            // Create a new edge for the activity.
            var edge = new Edge<T, TActivity>(activity);
            m_Edges.Add(edge.Id, edge);

            // We expect dependencies at some point.
            if (dependencies.Any())
            {
                // Since we use dummy edges to connect all tail nodes, we can create
                // a new tail node for this edge.
                T tailEventId = m_NodeIdGenerator();
                var tailNode = new Node<T, TEvent>(CreateEvent(tailEventId));
                tailNode.OutgoingEdges.Add(edge.Id);
                m_EdgeTailNodeLookup.Add(edge.Id, tailNode);
                m_Nodes.Add(tailNode.Id, tailNode);

                // Check which of the expected dependencies currently exist.
                IList<T> existingDependencies = m_Edges.Keys.Intersect(dependencies).ToList();
                IList<T> nonExistingDependencies = dependencies.Except(existingDependencies).ToList();

                // If any expected dependencies currently exist, then hook up their head
                // node to this edge's tail node with dummy edges.
                foreach (T dependencyId in existingDependencies)
                {
                    Node<T, TEvent> dependencyHeadNode = m_EdgeHeadNodeLookup[dependencyId];
                    T dummyEdgeId = m_EdgeIdGenerator();
                    var dummyEdge = new Edge<T, TActivity>(CreateDummyActivity(dummyEdgeId));
                    tailNode.IncomingEdges.Add(dummyEdgeId);
                    m_EdgeHeadNodeLookup.Add(dummyEdgeId, tailNode);

                    // If the head node of the dependency is the End node, then convert it.
                    if (dependencyHeadNode.NodeType == NodeType.End)
                    {
                        dependencyHeadNode.SetNodeType(NodeType.Normal);
                    }

                    dependencyHeadNode.OutgoingEdges.Add(dummyEdgeId);
                    m_EdgeTailNodeLookup.Add(dummyEdgeId, dependencyHeadNode);
                    m_Edges.Add(dummyEdgeId, dummyEdge);
                }

                // If any expected dependencies currently do not exist, then record their
                // IDs and add this edge's tail node as an unsatisfied successor.
                foreach (T dependencyId in nonExistingDependencies)
                {
                    HashSet<Node<T, TEvent>> tailNodes;
                    if (!m_UnsatisfiedSuccessorsLookup.TryGetValue(dependencyId, out tailNodes))
                    {
                        tailNodes = new HashSet<Node<T, TEvent>>();
                        m_UnsatisfiedSuccessorsLookup.Add(dependencyId, tailNodes);
                    }
                    tailNodes.Add(tailNode);
                }
            }
            else
            {
                // No dependencies, so attach it directly to the start node.
                StartNode.OutgoingEdges.Add(edge.Id);
                m_EdgeTailNodeLookup.Add(edge.Id, StartNode);
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
            Node<T, TEvent> tailNode = m_EdgeTailNodeLookup[activityId];
            if (tailNode.NodeType == NodeType.Start || tailNode.NodeType == NodeType.Isolated)
            {
                return new List<T>();
            }
            var output = new List<T>();
            foreach (Edge<T, TActivity> incomingEdge in tailNode.IncomingEdges.Select(x => m_Edges[x]))
            {
                output.Add(incomingEdge.Id);
            }
            return output;
        }

        public override IList<T> StrongActivityDependencyIds(T activityId)
        {
            Node<T, TEvent> tailNode = m_EdgeTailNodeLookup[activityId];
            if (tailNode.NodeType == NodeType.Start || tailNode.NodeType == NodeType.Isolated)
            {
                return new List<T>();
            }
            var output = new List<T>();
            foreach (Edge<T, TActivity> incomingEdge in tailNode.IncomingEdges.Select(x => m_Edges[x]))
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
            if (ancestorNodesLookup == null)
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
            bool edgesCleaned = CleanUpEdges();
            if (!edgesCleaned)
            {
                throw new InvalidOperationException(@"Cannot perform edge clean up");
            }
            this.ClearCriticalPathVariables();
            if (!this.CalculateEventEarliestFinishTimes())
            {
                throw new InvalidOperationException(@"Cannot calculate Event earliest finish times");
            }
            if (!this.CalculateEventLatestFinishTimes())
            {
                throw new InvalidOperationException(@"Cannot calculate Event latest finish times");
            }
            if (!this.CalculateCriticalPathVariables())
            {
                throw new InvalidOperationException(@"Cannot calculate critical path");
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

            foreach (T id in EdgeIds)
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

                Edge<T, TActivity> referenceEdge = m_Edges[referenceId];
                Node<T, TEvent> tailNode = m_EdgeTailNodeLookup[referenceId];
                if (tailNode.NodeType == NodeType.End || tailNode.NodeType == NodeType.Normal)
                {
                    foreach (T incomingEdgeId in tailNode.IncomingEdges)
                    {
                        if (indexLookup[incomingEdgeId] < 0)
                        {
                            strongConnect(incomingEdgeId);
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
                        Edge<T, TActivity> currentEdge = m_Edges[currentId];
                        if (!currentEdge.Content.CanBeRemoved)
                        {
                            circularDependency.Dependencies.Add(currentId);
                        }
                    } while (!referenceId.Equals(currentId));
                    circularDependencies.Add(circularDependency);
                }
            };

            foreach (T id in EdgeIds)
            {
                if (indexLookup[id] < 0)
                {
                    strongConnect(id);
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
