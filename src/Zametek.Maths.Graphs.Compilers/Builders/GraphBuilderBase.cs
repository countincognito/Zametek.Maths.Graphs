using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    public abstract class GraphBuilderBase<T, TResourceId, TEdgeContent, TNodeContent, TActivity, TEvent>
        : ICloneObject
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TEdgeContent : IHaveId<T>, ICloneObject
        where TNodeContent : IHaveId<T>, ICloneObject
        where TActivity : IActivity<T, TResourceId>
        where TEvent : IEvent<T>
    {
        #region Ctors

        private GraphBuilderBase()
        {
            EdgeLookup = new Dictionary<T, Edge<T, TEdgeContent>>();
            NodeLookup = new Dictionary<T, Node<T, TNodeContent>>();
            UnsatisfiedSuccessorsLookup = new Dictionary<T, HashSet<Node<T, TNodeContent>>>();
            EdgeHeadNodeLookup = new Dictionary<T, Node<T, TNodeContent>>();
            EdgeTailNodeLookup = new Dictionary<T, Node<T, TNodeContent>>();
        }

        protected GraphBuilderBase(
            Func<T> edgeIdGenerator,
            Func<T> nodeIdGenerator,
            Func<T, TEvent> eventGenerator)
            : this()
        {
            EdgeIdGenerator = edgeIdGenerator ?? throw new ArgumentNullException(nameof(edgeIdGenerator));
            NodeIdGenerator = nodeIdGenerator ?? throw new ArgumentNullException(nameof(nodeIdGenerator));
            EventGenerator = eventGenerator ?? throw new ArgumentNullException(nameof(eventGenerator));
        }

        protected GraphBuilderBase(
            Graph<T, TEdgeContent, TNodeContent> arrowGraph,
            Func<T> edgeIdGenerator,
            Func<T> nodeIdGenerator,
            Func<T, TEvent> eventGenerator)
            : this(edgeIdGenerator, nodeIdGenerator, eventGenerator)
        {
            if (arrowGraph is null)
            {
                throw new ArgumentNullException(nameof(arrowGraph));
            }
            foreach (Edge<T, TEdgeContent> edge in arrowGraph.Edges)
            {
                EdgeLookup.Add(edge.Id, edge);
            }

            foreach (Node<T, TNodeContent> node in arrowGraph.Nodes)
            {
                // Assimilate incoming edges.
                if (node.NodeType != NodeType.Start && node.NodeType != NodeType.Isolated)
                {
                    foreach (T edgeId in node.IncomingEdges)
                    {
                        EdgeHeadNodeLookup.Add(edgeId, node);
                    }
                }
                // Assimilate Outgoing edges.
                if (node.NodeType != NodeType.End && node.NodeType != NodeType.Isolated)
                {
                    foreach (T edgeId in node.OutgoingEdges)
                    {
                        EdgeTailNodeLookup.Add(edgeId, node);
                    }
                }
                NodeLookup.Add(node.Id, node);
            }

            // Check all edges are used.
            if (!EdgeLookup.Keys.OrderBy(x => x).SequenceEqual(EdgeHeadNodeLookup.Keys.OrderBy(x => x)))
            {
                throw new ArgumentException(Properties.Resources.ListOfEdgeIdsAndEdgesReferencedByHeadNodesDoNotMatch);
            }
            if (!EdgeLookup.Keys.OrderBy(x => x).SequenceEqual(EdgeTailNodeLookup.Keys.OrderBy(x => x)))
            {
                throw new ArgumentException(Properties.Resources.ListOfEdgeIdsAndEdgesReferencedByTailNodesDoNotMatch);
            }

            // Check all nodes are used.
            IEnumerable<T> edgeNodeLookupIds = EdgeHeadNodeLookup.Values.Select(x => x.Id).Union(EdgeTailNodeLookup.Values.Select(x => x.Id));
            if (!NodeLookup.Values.Where(x => x.NodeType != NodeType.Isolated).Select(x => x.Id).OrderBy(x => x).SequenceEqual(edgeNodeLookupIds.OrderBy(x => x)))
            {
                throw new ArgumentException(Properties.Resources.ListOfNodeIdsAndEdgesReferencedByTailNodesDoNotMatch);
            }
        }

        #endregion

        #region Properties

        protected Func<T> EdgeIdGenerator { get; }

        protected Func<T> NodeIdGenerator { get; }

        protected Func<T, TEvent> EventGenerator { get; }

        protected IDictionary<T, Edge<T, TEdgeContent>> EdgeLookup { get; }

        protected IDictionary<T, Node<T, TNodeContent>> NodeLookup { get; }

        protected IDictionary<T, HashSet<Node<T, TNodeContent>>> UnsatisfiedSuccessorsLookup { get; }

        protected IDictionary<T, Node<T, TNodeContent>> EdgeHeadNodeLookup { get; }

        protected IDictionary<T, Node<T, TNodeContent>> EdgeTailNodeLookup { get; }

        public IEnumerable<Node<T, TNodeContent>> StartNodes =>
            NodeLookup.Values.Where(x => x.NodeType == NodeType.Start);

        public IEnumerable<Node<T, TNodeContent>> EndNodes =>
            NodeLookup.Values.Where(x => x.NodeType == NodeType.End);

        public IEnumerable<Node<T, TNodeContent>> NormalNodes =>
            NodeLookup.Values.Where(x => x.NodeType == NodeType.Normal);

        public IEnumerable<Node<T, TNodeContent>> IsolatedNodes =>
            NodeLookup.Values.Where(x => x.NodeType == NodeType.Isolated);

        public IEnumerable<T> EdgeIds => EdgeLookup.Keys;

        public IEnumerable<T> NodeIds => NodeLookup.Keys;

        public IEnumerable<T> ActivityIds => Activities.Select(x => x.Id);

        public IEnumerable<T> EventIds => Events.Select(x => x.Id);

        public IEnumerable<Edge<T, TEdgeContent>> Edges
        {
            get
            {
                return EdgeLookup.Values;
            }
        }

        public IEnumerable<Node<T, TNodeContent>> Nodes
        {
            get
            {
                return NodeLookup.Values;
            }
        }

        public abstract IEnumerable<TActivity> Activities
        {
            get;
        }

        public abstract IEnumerable<TEvent> Events
        {
            get;
        }

        public IEnumerable<T> MissingDependencies => UnsatisfiedSuccessorsLookup.Keys;

        public bool AllDependenciesSatisfied => !UnsatisfiedSuccessorsLookup.Any();

        public int Duration
        {
            get
            {
                return Activities.Select(x => x.EarliestFinishTime.GetValueOrDefault()).DefaultIfEmpty().Max();
            }
        }

        #endregion

        #region Public Methods

        public Graph<T, TEdgeContent, TNodeContent> ToGraph()
        {
            bool edgesCleanedUp = CleanUpEdges();
            if (!edgesCleanedUp)
            {
                return null;
            }
            return new Graph<T, TEdgeContent, TNodeContent>(
                EdgeLookup.Values.Select(x => (Edge<T, TEdgeContent>)x.CloneObject()),
                NodeLookup.Values.Select(x => (Node<T, TNodeContent>)x.CloneObject()));
        }

        public Edge<T, TEdgeContent> Edge(T key)
        {
            if (!EdgeLookup.TryGetValue(key, out Edge<T, TEdgeContent> edge))
            {
                return null;
            }
            return edge;
        }

        public Node<T, TNodeContent> Node(T key)
        {
            if (!NodeLookup.TryGetValue(key, out Node<T, TNodeContent> node))
            {
                return null;
            }
            return node;
        }

        public Node<T, TNodeContent> EdgeHeadNode(T key)
        {
            Node<T, TNodeContent> output = null;
            if (EdgeHeadNodeLookup.ContainsKey(key))
            {
                output = EdgeHeadNodeLookup[key];
            }
            return output;
        }

        public Node<T, TNodeContent> EdgeTailNode(T key)
        {
            Node<T, TNodeContent> output = null;
            if (EdgeTailNodeLookup.ContainsKey(key))
            {
                output = EdgeTailNodeLookup[key];
            }
            return output;
        }

        public abstract TActivity Activity(T key);


        [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "No better term available")]
        public abstract TEvent Event(T key);

        public abstract bool AddActivity(TActivity activity);

        public abstract bool AddActivity(TActivity activity, HashSet<T> dependencies);

        public abstract bool AddActivityDependencies(T activityId, HashSet<T> dependencies);

        public abstract bool RemoveActivity(T activityId);

        public abstract bool RemoveActivityDependencies(T activityId, HashSet<T> dependencies);

        public IList<ICircularDependency<T>> FindStrongCircularDependencies()
        {
            return FindStronglyConnectedComponents().Where(x => x.Dependencies.Count > 1).ToList();
        }

        public IList<IInvalidConstraint<T>> FindInvalidPreCompilationConstraints()
        {
            var activitiesWithInvalidConstraints = new List<IInvalidConstraint<T>>();

            foreach (IActivity<T, TResourceId> activity in Activities)
            {
                if (activity.MinimumFreeSlack.HasValue
                    && activity.MaximumLatestFinishTime.HasValue)
                {
                    activitiesWithInvalidConstraints.Add(
                        new InvalidConstraint<T>(activity.Id, Resources.Message_CannotSetMinimumFreeSlackAndMaximumLatestFinishTime));
                    continue;
                }
                if (activity.MinimumEarliestStartTime.HasValue
                    && activity.MaximumLatestFinishTime.HasValue
                    && (activity.MinimumEarliestStartTime.Value + activity.Duration) > activity.MaximumLatestFinishTime.Value)
                {
                    activitiesWithInvalidConstraints.Add(
                        new InvalidConstraint<T>(activity.Id, Resources.Message_MinimumEarliestStartTimePlusDurationMustBeGreaterThanMaximumLatestFinishTime));
                    continue;
                }
            }

            return activitiesWithInvalidConstraints;
        }

        public IList<IInvalidConstraint<T>> FindInvalidPostCompilationConstraints()
        {
            var activitiesWithInvalidConstraints = new List<IInvalidConstraint<T>>();

            foreach (IActivity<T, TResourceId> activity in Activities)
            {
                if (activity.EarliestStartTime.HasValue
                    && activity.EarliestFinishTime.HasValue)
                {
                    if (activity.EarliestStartTime < 0)
                    {
                        activitiesWithInvalidConstraints.Add(
                            new InvalidConstraint<T>(activity.Id, Resources.Message_EarliestStartTimeLessThanZero));
                    }

                    if (activity.EarliestFinishTime < 0)
                    {
                        activitiesWithInvalidConstraints.Add(
                            new InvalidConstraint<T>(activity.Id, Resources.Message_EarliestFinishTimeLessThanZero));
                    }
                }

                if (activity.LatestStartTime.HasValue
                    && activity.LatestFinishTime.HasValue)
                {
                    if (activity.LatestStartTime < 0)
                    {
                        activitiesWithInvalidConstraints.Add(
                            new InvalidConstraint<T>(activity.Id, Resources.Message_LatestStartTimeLessThanZero));
                    }

                    if (activity.LatestFinishTime < 0)
                    {
                        activitiesWithInvalidConstraints.Add(
                            new InvalidConstraint<T>(activity.Id, Resources.Message_LatestFinishTimeLessThanZero));
                    }
                }

                if (activity.EarliestStartTime.HasValue
                    && activity.LatestStartTime.HasValue)
                {
                    if (activity.LatestStartTime < activity.EarliestStartTime)
                    {
                        activitiesWithInvalidConstraints.Add(
                            new InvalidConstraint<T>(activity.Id, Resources.Message_LatestStartTimeLessThanEarliestStartTime));
                    }
                }

                if (activity.EarliestFinishTime.HasValue
                    && activity.LatestFinishTime.HasValue)
                {
                    if (activity.LatestFinishTime < activity.EarliestFinishTime)
                    {
                        activitiesWithInvalidConstraints.Add(
                            new InvalidConstraint<T>(activity.Id, Resources.Message_LatestFinishTimeLessThanEarliestFinishTime));
                    }
                }
            }

            return activitiesWithInvalidConstraints;
        }

        public IDictionary<T, HashSet<T>> GetAncestorNodesLookup()
        {
            if (!AllDependenciesSatisfied)
            {
                return null;
            }
            IList<ICircularDependency<T>> circularDependencies = FindStrongCircularDependencies();
            if (circularDependencies.Any())
            {
                return null;
            }
            var nodeIdAncestorLookup = new Dictionary<T, HashSet<T>>();
            foreach (T endNodeId in EndNodes.Select(x => x.Id))
            {
                HashSet<T> totalAncestorNodes = GetAncestorNodes(endNodeId, nodeIdAncestorLookup);
                nodeIdAncestorLookup.Add(endNodeId, totalAncestorNodes);
            }
            return nodeIdAncestorLookup;
        }

        public abstract IList<T> ActivityDependencyIds(T activityId);

        public abstract IList<T> StrongActivityDependencyIds(T activityId);

        public abstract bool TransitiveReduction();

        public abstract bool RedirectEdges();

        public abstract bool RemoveRedundantEdges();

        public bool CleanUpEdges()
        {
            bool edgesRedirected = RedirectEdges();
            if (!edgesRedirected)
            {
                return false;
            }
            bool redundantEdgesRemoved = RemoveRedundantEdges();
            if (!redundantEdgesRemoved)
            {
                return false;
            }
            return true;
        }

        public abstract void CalculateCriticalPath();

        public virtual void Reset()
        {
            EdgeLookup.Clear();
            NodeLookup.Clear();
            UnsatisfiedSuccessorsLookup.Clear();
            EdgeHeadNodeLookup.Clear();
            EdgeTailNodeLookup.Clear();
        }

        #endregion

        #region Protected Methods

        protected bool ChangeEdgeTailNode(T edgeId, T newTailNodeId)
        {
            // Do not attend this unless all dependencies are satisfied.
            if (!AllDependenciesSatisfied)
            {
                return false;
            }

            Node<T, TNodeContent> oldTailNode = EdgeTailNodeLookup[edgeId];
            bool changeTailSuccess = ChangeEdgeTailNodeWithoutCleanup(edgeId, newTailNodeId);
            if (!changeTailSuccess)
            {
                throw new InvalidOperationException($@"Unable to change tail node of edge {edgeId} to node {newTailNodeId} without cleanup");
            }

            // If the old tail node has no other outgoing edges, then
            // connect its incoming edges to the current head node.
            IList<T> oldTailNodeOutgoingEdgeIds = oldTailNode.OutgoingEdges.ToList();
            if (!oldTailNodeOutgoingEdgeIds.Any())
            {
                Node<T, TNodeContent> headNode = EdgeHeadNodeLookup[edgeId];
                IList<T> oldTailNodeIncomingEdgeIds = oldTailNode.IncomingEdges.ToList();
                foreach (T oldTailNodeIncomingEdgeId in oldTailNodeIncomingEdgeIds)
                {
                    bool changeHeadSuccess = ChangeEdgeHeadNodeWithoutCleanup(oldTailNodeIncomingEdgeId, headNode.Id);
                    if (!changeHeadSuccess)
                    {
                        throw new InvalidOperationException($@"Unable to change head node of edge {oldTailNodeIncomingEdgeId} to node {headNode.Id} without cleanup");
                    }
                }
            }

            // Final check to see if the tail node has no incoming or outgoing edges.
            // If it does not then remove it.
            if (oldTailNode.NodeType != NodeType.Start
                && oldTailNode.NodeType != NodeType.Isolated
                && !oldTailNode.IncomingEdges.Any()
                && !oldTailNode.OutgoingEdges.Any())
            {
                NodeLookup.Remove(oldTailNode.Id);
            }
            return true;
        }

        protected bool ChangeEdgeHeadNode(T edgeId, T newHeadNodeId)
        {
            // Do not attend this unless all dependencies are satisfied.
            if (!AllDependenciesSatisfied)
            {
                return false;
            }

            Node<T, TNodeContent> oldHeadNode = EdgeHeadNodeLookup[edgeId];
            bool changeHeadSuccess = ChangeEdgeHeadNodeWithoutCleanup(edgeId, newHeadNodeId);
            if (!changeHeadSuccess)
            {
                throw new InvalidOperationException($@"Unable to change head node of edge {edgeId} to node {newHeadNodeId} without cleanup");
            }

            // If the old head node has no other incoming edges, then
            // connect its outgoing edges to the current tail node.
            IList<T> oldHeadNodeIncomingEdgeIds = oldHeadNode.IncomingEdges.ToList();
            if (!oldHeadNodeIncomingEdgeIds.Any())
            {
                Node<T, TNodeContent> tailNode = EdgeTailNodeLookup[edgeId];
                IList<T> oldHeadNodeOutgoingEdgeIds = oldHeadNode.OutgoingEdges.ToList();
                foreach (T oldHeadNodeOutgoingEdgeId in oldHeadNodeOutgoingEdgeIds)
                {
                    bool changeTailSuccess = ChangeEdgeTailNodeWithoutCleanup(oldHeadNodeOutgoingEdgeId, tailNode.Id);
                    if (!changeTailSuccess)
                    {
                        throw new InvalidOperationException($@"Unable to change tail node of edge {oldHeadNodeOutgoingEdgeId} to node {tailNode.Id} without cleanup");
                    }
                }
            }

            // Final check to see if the head node has no incoming or outgoing edges.
            // If it does not then remove it.
            if (oldHeadNode.NodeType != NodeType.End
                && oldHeadNode.NodeType != NodeType.Isolated
                && !oldHeadNode.IncomingEdges.Any()
                && !oldHeadNode.OutgoingEdges.Any())
            {
                NodeLookup.Remove(oldHeadNode.Id);
            }
            return true;
        }

        protected void GetEdgesInDecendingOrder(T nodeId, IList<Edge<T, TEdgeContent>> edgesInDecendingOrder, HashSet<T> recordedEdges)
        {
            if (edgesInDecendingOrder is null)
            {
                throw new ArgumentNullException(nameof(edgesInDecendingOrder));
            }
            if (recordedEdges is null)
            {
                throw new ArgumentNullException(nameof(recordedEdges));
            }
            Node<T, TNodeContent> node = NodeLookup[nodeId];
            if (node.NodeType == NodeType.End || node.NodeType == NodeType.Isolated)
            {
                return;
            }

            // Go through each of the node's outgoing edges, record them,
            // then do the same to their head nodes.
            foreach (Edge<T, TEdgeContent> outgoingEdge in node.OutgoingEdges.Select(x => EdgeLookup[x]))
            {
                if (!recordedEdges.Contains(outgoingEdge.Id))
                {
                    edgesInDecendingOrder.Add(outgoingEdge);
                    recordedEdges.Add(outgoingEdge.Id);
                }
                GetEdgesInDecendingOrder(EdgeHeadNodeLookup[outgoingEdge.Id].Id, edgesInDecendingOrder, recordedEdges);
            }
        }

        /// <summary>
        /// Check to make sure that no other edges will be made parallel
        /// by removing this edge. If there is an intersection between
        /// the ancestor/decendant nodes of the edge's tail node, and the
        /// ancestor/decendant nodes of the head node, then do not remove it.
        /// </summary>
        /// <param name="tailNode"></param>
        /// <param name="headNode"></param>
        /// <returns></returns>
        protected bool HaveDecendantOrAncestorOverlap(Node<T, TNodeContent> tailNode, Node<T, TNodeContent> headNode)
        {
            if (tailNode is null)
            {
                throw new ArgumentNullException(nameof(tailNode));
            }
            if (headNode is null)
            {
                throw new ArgumentNullException(nameof(headNode));
            }

            // First the decendants of the tail node.
            var tailNodeAncestorsAndDecendants = new HashSet<T>();
            if (tailNode.NodeType != NodeType.End
                && tailNode.NodeType != NodeType.Isolated)
            {
                tailNodeAncestorsAndDecendants.UnionWith(
                    tailNode.OutgoingEdges
                    .Select(x => EdgeLookup[x])
                    .Select(x => EdgeHeadNodeLookup[x.Id].Id)
                    .Except(new[] { headNode.Id }));
            }

            // Then the ancestors of the tail node.
            if (tailNode.NodeType != NodeType.Start
                && tailNode.NodeType != NodeType.Isolated)
            {
                tailNodeAncestorsAndDecendants.UnionWith(
                    tailNode.IncomingEdges
                    .Select(x => EdgeLookup[x])
                    .Select(x => EdgeTailNodeLookup[x.Id].Id)
                    .Except(new[] { headNode.Id }));
            }

            // Next the ancestors of the head node.
            var headNodeAncestorsAndDecendants = new HashSet<T>();
            if (headNode.NodeType != NodeType.Start
                && headNode.NodeType != NodeType.Isolated)
            {
                headNodeAncestorsAndDecendants.UnionWith(
                    headNode.IncomingEdges
                    .Select(x => EdgeLookup[x])
                    .Select(x => EdgeTailNodeLookup[x.Id].Id)
                    .Except(new[] { tailNode.Id }));
            }

            // Then the decendants of the head node.
            if (headNode.NodeType != NodeType.End
                && headNode.NodeType != NodeType.Isolated)
            {
                headNodeAncestorsAndDecendants.UnionWith(
                    headNode.OutgoingEdges
                    .Select(x => EdgeLookup[x])
                    .Select(x => EdgeHeadNodeLookup[x.Id].Id)
                    .Except(new[] { tailNode.Id }));
            }

            IEnumerable<T> overlap = tailNodeAncestorsAndDecendants.Intersect(headNodeAncestorsAndDecendants);
            if (overlap.Any())
            {
                return true;
            }
            return false;
        }

        protected bool ShareMoreThanOneEdge(Node<T, TNodeContent> tailNode, Node<T, TNodeContent> headNode)
        {
            if (tailNode is null)
            {
                throw new ArgumentNullException(nameof(tailNode));
            }
            if (headNode is null)
            {
                throw new ArgumentNullException(nameof(headNode));
            }
            var tailNodeOutgoingEdgeIds = new HashSet<T>();
            if (tailNode.NodeType != NodeType.End
                && tailNode.NodeType != NodeType.Isolated)
            {
                tailNodeOutgoingEdgeIds.UnionWith(tailNode.OutgoingEdges);
            }
            var headNodeIncomingEdgeIds = new HashSet<T>();
            if (headNode.NodeType != NodeType.Start
                && headNode.NodeType != NodeType.Isolated)
            {
                headNodeIncomingEdgeIds.UnionWith(headNode.IncomingEdges);
            }
            IEnumerable<T> overlap = tailNodeOutgoingEdgeIds.Intersect(headNodeIncomingEdgeIds);
            if (overlap.Count() > 1)
            {
                return true;
            }
            return false;
        }

        protected abstract IList<ICircularDependency<T>> FindStronglyConnectedComponents();

        #endregion

        #region Private Methods

        private bool ChangeEdgeTailNodeWithoutCleanup(T edgeId, T newTailNodeId)
        {
            // Do not attend this unless all dependencies are satisfied.
            if (!AllDependenciesSatisfied)
            {
                return false;
            }
            // Retrieve the activity edge.
            if (!EdgeLookup.TryGetValue(edgeId, out Edge<T, TEdgeContent> _))
            {
                return false;
            }
            // Retrieve the new tail event node.
            if (!NodeLookup.TryGetValue(newTailNodeId, out Node<T, TNodeContent> newTailNode))
            {
                return false;
            }

            // Remove the connection from the current tail node.
            Node<T, TNodeContent> oldTailNode = EdgeTailNodeLookup[edgeId];
            oldTailNode.OutgoingEdges.Remove(edgeId);
            EdgeTailNodeLookup.Remove(edgeId);

            // Attach to the new tail node.
            newTailNode.OutgoingEdges.Add(edgeId);
            EdgeTailNodeLookup.Add(edgeId, newTailNode);
            return true;
        }

        private bool ChangeEdgeHeadNodeWithoutCleanup(T edgeId, T newHeadNodeId)
        {
            // Do not attend this unless all dependencies are satisfied.
            if (!AllDependenciesSatisfied)
            {
                return false;
            }
            // Retrieve the activity edge.
            if (!EdgeLookup.TryGetValue(edgeId, out Edge<T, TEdgeContent> _))
            {
                return false;
            }
            // Retrieve the new head event node.
            if (!NodeLookup.TryGetValue(newHeadNodeId, out Node<T, TNodeContent> newHeadNode))
            {
                return false;
            }

            // Remove the connection from the current head node.
            Node<T, TNodeContent> currentHeadNode = EdgeHeadNodeLookup[edgeId];
            currentHeadNode.IncomingEdges.Remove(edgeId);
            EdgeHeadNodeLookup.Remove(edgeId);

            // Attach to the new head node.
            newHeadNode.IncomingEdges.Add(edgeId);
            EdgeHeadNodeLookup.Add(edgeId, newHeadNode);
            return true;
        }

        private HashSet<T> GetAncestorNodes(T nodeId, IDictionary<T, HashSet<T>> nodeIdAncestorLookup)
        {
            if (nodeIdAncestorLookup is null)
            {
                throw new ArgumentNullException(nameof(nodeIdAncestorLookup));
            }
            Node<T, TNodeContent> node = NodeLookup[nodeId];
            var totalAncestorNodes = new HashSet<T>();
            if (node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated)
            {
                return totalAncestorNodes;
            }

            // Go through each incoming edge and find the nodes
            // to which they connect.
            foreach (T tailNodeId in node.IncomingEdges.Select(x => EdgeTailNodeLookup[x].Id).ToList())
            {
                if (!totalAncestorNodes.Contains(tailNodeId))
                {
                    totalAncestorNodes.Add(tailNodeId);
                }
                // If the lookup holds the ancestor nodes for the tail
                // node then add them to the ancestor nodes. Otherwise
                // calculate the ancestor nodes for the tail node too.
                if (!nodeIdAncestorLookup.TryGetValue(tailNodeId, out HashSet<T> tailNodeAncestorNodes))
                {
                    tailNodeAncestorNodes = GetAncestorNodes(tailNodeId, nodeIdAncestorLookup);
                    nodeIdAncestorLookup.Add(tailNodeId, tailNodeAncestorNodes);
                }
                totalAncestorNodes.UnionWith(tailNodeAncestorNodes);
            }
            return totalAncestorNodes;
        }

        #endregion

        #region ICloneObject

        public abstract object CloneObject();

        #endregion
    }
}
