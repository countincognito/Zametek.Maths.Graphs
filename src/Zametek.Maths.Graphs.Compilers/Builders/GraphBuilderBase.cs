using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    public abstract class GraphBuilderBase<T, TEdgeContent, TNodeContent, TActivity, TEvent>
        : IWorkingCopy
        where T : struct, IComparable<T>, IEquatable<T>
        where TEdgeContent : IHaveId<T>, IWorkingCopy
        where TNodeContent : IHaveId<T>, IWorkingCopy
        where TActivity : IActivity<T>
        where TEvent : IEvent<T>
    {
        #region Fields

        protected readonly Func<T> m_EdgeIdGenerator;
        protected readonly Func<T> m_NodeIdGenerator;
        protected readonly Func<T, TEvent> m_CreateEvent;

        protected readonly IDictionary<T, Edge<T, TEdgeContent>> m_Edges;
        protected readonly IDictionary<T, Node<T, TNodeContent>> m_Nodes;
        protected readonly IDictionary<T, HashSet<Node<T, TNodeContent>>> m_UnsatisfiedSuccessorsLookup;
        protected readonly IDictionary<T, Node<T, TNodeContent>> m_EdgeHeadNodeLookup;
        protected readonly IDictionary<T, Node<T, TNodeContent>> m_EdgeTailNodeLookup;

        #endregion

        #region Ctors

        private GraphBuilderBase()
        {
            m_Edges = new Dictionary<T, Edge<T, TEdgeContent>>();
            m_Nodes = new Dictionary<T, Node<T, TNodeContent>>();
            m_UnsatisfiedSuccessorsLookup = new Dictionary<T, HashSet<Node<T, TNodeContent>>>();
            m_EdgeHeadNodeLookup = new Dictionary<T, Node<T, TNodeContent>>();
            m_EdgeTailNodeLookup = new Dictionary<T, Node<T, TNodeContent>>();
        }

        protected GraphBuilderBase(
            Func<T> edgeIdGenerator,
            Func<T> nodeIdGenerator,
            Func<T, TEvent> createEvent)
            : this()
        {
            m_EdgeIdGenerator = edgeIdGenerator ?? throw new ArgumentNullException(nameof(edgeIdGenerator));
            m_NodeIdGenerator = nodeIdGenerator ?? throw new ArgumentNullException(nameof(nodeIdGenerator));
            m_CreateEvent = createEvent ?? throw new ArgumentNullException(nameof(createEvent));
        }

        protected GraphBuilderBase(
            Graph<T, TEdgeContent, TNodeContent> arrowGraph,
            Func<T> edgeIdGenerator,
            Func<T> nodeIdGenerator,
            Func<T, TEvent> createEvent)
            : this(edgeIdGenerator, nodeIdGenerator, createEvent)
        {
            if (arrowGraph == null)
            {
                throw new ArgumentNullException(nameof(arrowGraph));
            }
            foreach (Edge<T, TEdgeContent> edge in arrowGraph.Edges)
            {
                m_Edges.Add(edge.Id, edge);
            }

            foreach (Node<T, TNodeContent> node in arrowGraph.Nodes)
            {
                // Assimilate incoming edges.
                if (node.NodeType != NodeType.Start && node.NodeType != NodeType.Isolated)
                {
                    foreach (T edgeId in node.IncomingEdges)
                    {
                        m_EdgeHeadNodeLookup.Add(edgeId, node);
                    }
                }
                // Assimilate Outgoing edges.
                if (node.NodeType != NodeType.End && node.NodeType != NodeType.Isolated)
                {
                    foreach (T edgeId in node.OutgoingEdges)
                    {
                        m_EdgeTailNodeLookup.Add(edgeId, node);
                    }
                }
                m_Nodes.Add(node.Id, node);
            }

            // Check all edges are used.
            if (!m_Edges.Keys.OrderBy(x => x).SequenceEqual(m_EdgeHeadNodeLookup.Keys.OrderBy(x => x)))
            {
                throw new ArgumentException(@"List of Edge IDs and Edges referenced by head Nodes do not match");
            }
            if (!m_Edges.Keys.OrderBy(x => x).SequenceEqual(m_EdgeTailNodeLookup.Keys.OrderBy(x => x)))
            {
                throw new ArgumentException(@"List of Edge IDs and Edges referenced by tail Nodes do not match");
            }

            // Check all nodes are used.
            IEnumerable<T> edgeNodeLookupIds = m_EdgeHeadNodeLookup.Values.Select(x => x.Id).Union(m_EdgeTailNodeLookup.Values.Select(x => x.Id));
            if (!m_Nodes.Values.Where(x => x.NodeType != NodeType.Isolated).Select(x => x.Id).OrderBy(x => x).SequenceEqual(edgeNodeLookupIds.OrderBy(x => x)))
            {
                throw new ArgumentException(@"List of Node IDs and Edges referenced by tail Nodes do not match");
            }
        }

        #endregion

        #region Properties

        public IEnumerable<Node<T, TNodeContent>> StartNodes =>
            m_Nodes.Values.Where(x => x.NodeType == NodeType.Start);

        public IEnumerable<Node<T, TNodeContent>> EndNodes =>
            m_Nodes.Values.Where(x => x.NodeType == NodeType.End);

        public IEnumerable<Node<T, TNodeContent>> NormalNodes =>
            m_Nodes.Values.Where(x => x.NodeType == NodeType.Normal);

        public IEnumerable<Node<T, TNodeContent>> IsolatedNodes =>
            m_Nodes.Values.Where(x => x.NodeType == NodeType.Isolated);

        public IEnumerable<T> EdgeIds => m_Edges.Keys;

        public IEnumerable<T> NodeIds => m_Nodes.Keys;

        public IEnumerable<T> ActivityIds => Activities.Select(x => x.Id);

        public IEnumerable<T> EventIds => Events.Select(x => x.Id);

        public IEnumerable<Edge<T, TEdgeContent>> Edges
        {
            get
            {
                return m_Edges.Values;
            }
        }

        public IEnumerable<Node<T, TNodeContent>> Nodes
        {
            get
            {
                return m_Nodes.Values;
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

        public IEnumerable<T> MissingDependencies => m_UnsatisfiedSuccessorsLookup.Keys;

        public bool AllDependenciesSatisfied => !m_UnsatisfiedSuccessorsLookup.Any();

        public int Duration
        {
            get
            {
                return Activities.Select(x => x.LatestFinishTime.GetValueOrDefault()).DefaultIfEmpty().Max();
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
                m_Edges.Values.Select(x => (Edge<T, TEdgeContent>)x.WorkingCopy()),
                m_Nodes.Values.Select(x => (Node<T, TNodeContent>)x.WorkingCopy()));
        }

        public Edge<T, TEdgeContent> Edge(T key)
        {
            if (!m_Edges.TryGetValue(key, out Edge<T, TEdgeContent> edge))
            {
                return null;
            }
            return edge;
        }

        public Node<T, TNodeContent> Node(T key)
        {
            if (!m_Nodes.TryGetValue(key, out Node<T, TNodeContent> node))
            {
                return null;
            }
            return node;
        }

        public Node<T, TNodeContent> EdgeHeadNode(T key)
        {
            Node<T, TNodeContent> output = null;
            if (m_EdgeHeadNodeLookup.ContainsKey(key))
            {
                output = m_EdgeHeadNodeLookup[key];
            }
            return output;
        }

        public Node<T, TNodeContent> EdgeTailNode(T key)
        {
            Node<T, TNodeContent> output = null;
            if (m_EdgeTailNodeLookup.ContainsKey(key))
            {
                output = m_EdgeTailNodeLookup[key];
            }
            return output;
        }

        public abstract TActivity Activity(T key);

        public abstract TEvent Event(T key);

        public abstract bool AddActivity(TActivity activity);

        public abstract bool AddActivity(TActivity activity, HashSet<T> dependencies);

        public abstract bool AddActivityDependencies(T activityId, HashSet<T> dependencies);

        public abstract bool RemoveActivity(T activityId);

        public abstract bool RemoveActivityDependencies(T activityId, HashSet<T> dependencies);

        public IList<CircularDependency<T>> FindStrongCircularDependencies()
        {
            return FindStronglyConnectedComponents().Where(x => x.Dependencies.Count > 1).ToList();
        }

        public IDictionary<T, HashSet<T>> GetAncestorNodesLookup()
        {
            if (!AllDependenciesSatisfied)
            {
                return null;
            }
            IList<CircularDependency<T>> circularDependencies = FindStrongCircularDependencies();
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
            m_Edges.Clear();
            m_Nodes.Clear();
            m_UnsatisfiedSuccessorsLookup.Clear();
            m_EdgeHeadNodeLookup.Clear();
            m_EdgeTailNodeLookup.Clear();
        }

        #endregion

        #region Protected Methods

        protected TEvent CreateEvent(T id)
        {
            return m_CreateEvent(id);
        }

        protected bool ChangeEdgeTailNode(T edgeId, T newTailNodeId)
        {
            // Do not attend this unless all dependencies are satisfied.
            if (!AllDependenciesSatisfied)
            {
                return false;
            }

            Node<T, TNodeContent> oldTailNode = m_EdgeTailNodeLookup[edgeId];
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
                Node<T, TNodeContent> headNode = m_EdgeHeadNodeLookup[edgeId];
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
                m_Nodes.Remove(oldTailNode.Id);
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

            Node<T, TNodeContent> oldHeadNode = m_EdgeHeadNodeLookup[edgeId];
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
                Node<T, TNodeContent> tailNode = m_EdgeTailNodeLookup[edgeId];
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
                m_Nodes.Remove(oldHeadNode.Id);
            }
            return true;
        }

        protected void GetEdgesInDecendingOrder(T nodeId, IList<Edge<T, TEdgeContent>> edgesInDecendingOrder, HashSet<T> recordedEdges)
        {
            if (recordedEdges == null)
            {
                throw new ArgumentNullException(nameof(recordedEdges));
            }
            Node<T, TNodeContent> node = m_Nodes[nodeId];
            if (node.NodeType == NodeType.End || node.NodeType == NodeType.Isolated)
            {
                return;
            }

            // Go through each of the node's outgoing edges, record them,
            // then do the same to their head nodes.
            foreach (Edge<T, TEdgeContent> outgoingEdge in node.OutgoingEdges.Select(x => m_Edges[x]))
            {
                if (!recordedEdges.Contains(outgoingEdge.Id))
                {
                    edgesInDecendingOrder.Add(outgoingEdge);
                    recordedEdges.Add(outgoingEdge.Id);
                }
                GetEdgesInDecendingOrder(m_EdgeHeadNodeLookup[outgoingEdge.Id].Id, edgesInDecendingOrder, recordedEdges);
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
            if (tailNode == null)
            {
                throw new ArgumentNullException(nameof(tailNode));
            }
            if (headNode == null)
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
                    .Select(x => m_Edges[x])
                    .Select(x => m_EdgeHeadNodeLookup[x.Id].Id)
                    .Except(new[] { headNode.Id }));
            }

            // Then the ancestors of the tail node.
            if (tailNode.NodeType != NodeType.Start
                && tailNode.NodeType != NodeType.Isolated)
            {
                tailNodeAncestorsAndDecendants.UnionWith(
                    tailNode.IncomingEdges
                    .Select(x => m_Edges[x])
                    .Select(x => m_EdgeTailNodeLookup[x.Id].Id)
                    .Except(new[] { headNode.Id }));
            }

            // Next the ancestors of the head node.
            var headNodeAncestorsAndDecendants = new HashSet<T>();
            if (headNode.NodeType != NodeType.Start
                && headNode.NodeType != NodeType.Isolated)
            {
                headNodeAncestorsAndDecendants.UnionWith(
                    headNode.IncomingEdges
                    .Select(x => m_Edges[x])
                    .Select(x => m_EdgeTailNodeLookup[x.Id].Id)
                    .Except(new[] { tailNode.Id }));
            }

            // Then the decendants of the head node.
            if (headNode.NodeType != NodeType.End
                && headNode.NodeType != NodeType.Isolated)
            {
                headNodeAncestorsAndDecendants.UnionWith(
                    headNode.OutgoingEdges
                    .Select(x => m_Edges[x])
                    .Select(x => m_EdgeHeadNodeLookup[x.Id].Id)
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
            if (tailNode == null)
            {
                throw new ArgumentNullException(nameof(tailNode));
            }
            if (headNode == null)
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

        protected abstract IList<CircularDependency<T>> FindStronglyConnectedComponents();

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
            if (!m_Edges.TryGetValue(edgeId, out Edge<T, TEdgeContent> _))
            {
                return false;
            }
            // Retrieve the new tail event node.
            if (!m_Nodes.TryGetValue(newTailNodeId, out Node<T, TNodeContent> newTailNode))
            {
                return false;
            }

            // Remove the connection from the current tail node.
            Node<T, TNodeContent> oldTailNode = m_EdgeTailNodeLookup[edgeId];
            oldTailNode.OutgoingEdges.Remove(edgeId);
            m_EdgeTailNodeLookup.Remove(edgeId);

            // Attach to the new tail node.
            newTailNode.OutgoingEdges.Add(edgeId);
            m_EdgeTailNodeLookup.Add(edgeId, newTailNode);
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
            if (!m_Edges.TryGetValue(edgeId, out Edge<T, TEdgeContent> _))
            {
                return false;
            }
            // Retrieve the new head event node.
            if (!m_Nodes.TryGetValue(newHeadNodeId, out Node<T, TNodeContent> newHeadNode))
            {
                return false;
            }

            // Remove the connection from the current head node.
            Node<T, TNodeContent> currentHeadNode = m_EdgeHeadNodeLookup[edgeId];
            currentHeadNode.IncomingEdges.Remove(edgeId);
            m_EdgeHeadNodeLookup.Remove(edgeId);

            // Attach to the new head node.
            newHeadNode.IncomingEdges.Add(edgeId);
            m_EdgeHeadNodeLookup.Add(edgeId, newHeadNode);
            return true;
        }

        private HashSet<T> GetAncestorNodes(T nodeId, IDictionary<T, HashSet<T>> nodeIdAncestorLookup)
        {
            if (nodeIdAncestorLookup == null)
            {
                throw new ArgumentNullException(nameof(nodeIdAncestorLookup));
            }
            Node<T, TNodeContent> node = m_Nodes[nodeId];
            var totalAncestorNodes = new HashSet<T>();
            if (node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated)
            {
                return totalAncestorNodes;
            }

            // Go through each incoming edge and find the nodes
            // to which they connect.
            foreach (T tailNodeId in node.IncomingEdges.Select(x => m_EdgeTailNodeLookup[x].Id).ToList())
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

        #region IWorkingCopy

        public abstract object WorkingCopy();

        #endregion
    }
}
