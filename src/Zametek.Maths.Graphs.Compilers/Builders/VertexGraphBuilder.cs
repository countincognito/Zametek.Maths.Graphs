using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    // Sealed builder for Activity-on-Vertex graphs. Owns all graph state directly.
    // Algorithm work is delegated to injected engine instances (SCC finder, CPM engine).
    // The public constructor wires up default engine instances; the internal constructor
    // accepts injected engines for testability.
    public sealed class VertexGraphBuilder<T, TResourceId, TWorkStreamId, TActivity>
        : ICloneObject
        where TActivity : IActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        #region Fields

        private static readonly Func<T, IEvent<T>> s_EventGenerator = (id) =>
        {
            var output = new Event<T>(id);
            output.SetAsRemovable();
            return output;
        };

        private readonly Func<T> m_EdgeIdGenerator;
        private readonly Func<T> m_NodeIdGenerator;

        private readonly IVertexStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity, IEvent<T>> m_SccFinder;
        private readonly IVertexCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity, IEvent<T>> m_CriticalPathEngine;
        private readonly IResourceSchedulingEngine<T, TResourceId, TWorkStreamId> m_ResourceSchedulingEngine;
        private ITransitiveReducer<T> m_TransitiveReducer;

        #endregion

        #region Graph State (previously in GraphBuilderBase)

        private readonly Dictionary<T, Edge<T, IEvent<T>>> m_EdgeLookup;
        private readonly Dictionary<T, Node<T, TActivity>> m_NodeLookup;
        private readonly Dictionary<T, HashSet<Node<T, TActivity>>> m_UnsatisfiedSuccessorsLookup;
        private readonly Dictionary<T, Node<T, TActivity>> m_EdgeHeadNodeLookup;
        private readonly Dictionary<T, Node<T, TActivity>> m_EdgeTailNodeLookup;

        #endregion

        #region Ctors

        // Public constructor — stable API surface. Wires up default engine instances.
        public VertexGraphBuilder(
            Func<T> edgeIdGenerator,
            Func<T> nodeIdGenerator)
            : this(
                  edgeIdGenerator,
                  nodeIdGenerator,
                  new VertexTarjanStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity, IEvent<T>>(),
                  new VertexCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity, IEvent<T>>(),
                  new PriorityListResourceScheduler<T, TResourceId, TWorkStreamId>())
        {
        }

        // Internal constructor — accepts injected engines for testability.
        internal VertexGraphBuilder(
            Func<T> edgeIdGenerator,
            Func<T> nodeIdGenerator,
            IVertexStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity, IEvent<T>> sccFinder,
            IVertexCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity, IEvent<T>> criticalPathEngine,
            IResourceSchedulingEngine<T, TResourceId, TWorkStreamId> resourceSchedulingEngine = null)
        {
            m_EdgeIdGenerator = edgeIdGenerator ?? throw new ArgumentNullException(nameof(edgeIdGenerator));
            m_NodeIdGenerator = nodeIdGenerator ?? throw new ArgumentNullException(nameof(nodeIdGenerator));
            m_SccFinder = sccFinder ?? throw new ArgumentNullException(nameof(sccFinder));
            m_CriticalPathEngine = criticalPathEngine ?? throw new ArgumentNullException(nameof(criticalPathEngine));
            m_ResourceSchedulingEngine = resourceSchedulingEngine ?? new PriorityListResourceScheduler<T, TResourceId, TWorkStreamId>();

            m_EdgeLookup = new Dictionary<T, Edge<T, IEvent<T>>>();
            m_NodeLookup = new Dictionary<T, Node<T, TActivity>>();
            m_UnsatisfiedSuccessorsLookup = new Dictionary<T, HashSet<Node<T, TActivity>>>();
            m_EdgeHeadNodeLookup = new Dictionary<T, Node<T, TActivity>>();
            m_EdgeTailNodeLookup = new Dictionary<T, Node<T, TActivity>>();
            WhenTesting = false;
            m_TransitiveReducer = CreateTransitiveReducer();
        }

        // Graph-loading constructor (from existing Graph<T, IEvent<T>, TActivity>).
        public VertexGraphBuilder(
            Graph<T, IEvent<T>, TActivity> graph,
            Func<T> edgeIdGenerator,
            Func<T> nodeIdGenerator)
            : this(
                  graph,
                  edgeIdGenerator,
                  nodeIdGenerator,
                  new VertexTarjanStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity, IEvent<T>>(),
                  new VertexCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity, IEvent<T>>(),
                  new PriorityListResourceScheduler<T, TResourceId, TWorkStreamId>())
        {
        }

        // Internal graph-loading constructor with engine injection.
        internal VertexGraphBuilder(
            Graph<T, IEvent<T>, TActivity> graph,
            Func<T> edgeIdGenerator,
            Func<T> nodeIdGenerator,
            IVertexStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity, IEvent<T>> sccFinder,
            IVertexCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity, IEvent<T>> criticalPathEngine,
            IResourceSchedulingEngine<T, TResourceId, TWorkStreamId> resourceSchedulingEngine = null)
        {
            if (graph is null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            m_EdgeIdGenerator = edgeIdGenerator ?? throw new ArgumentNullException(nameof(edgeIdGenerator));
            m_NodeIdGenerator = nodeIdGenerator ?? throw new ArgumentNullException(nameof(nodeIdGenerator));
            m_SccFinder = sccFinder ?? throw new ArgumentNullException(nameof(sccFinder));
            m_CriticalPathEngine = criticalPathEngine ?? throw new ArgumentNullException(nameof(criticalPathEngine));
            m_ResourceSchedulingEngine = resourceSchedulingEngine ?? new PriorityListResourceScheduler<T, TResourceId, TWorkStreamId>();

            m_EdgeLookup = new Dictionary<T, Edge<T, IEvent<T>>>();
            m_NodeLookup = new Dictionary<T, Node<T, TActivity>>();
            m_UnsatisfiedSuccessorsLookup = new Dictionary<T, HashSet<Node<T, TActivity>>>();
            m_EdgeHeadNodeLookup = new Dictionary<T, Node<T, TActivity>>();
            m_EdgeTailNodeLookup = new Dictionary<T, Node<T, TActivity>>();
            WhenTesting = false;

            foreach (Edge<T, IEvent<T>> edge in graph.Edges)
            {
                m_EdgeLookup.Add(edge.Id, edge);
            }

            foreach (Node<T, TActivity> node in graph.Nodes)
            {
                // Assimilate incoming edges.
                if (node.NodeType != NodeType.Start && node.NodeType != NodeType.Isolated)
                {
                    foreach (T edgeId in node.IncomingEdges)
                    {
                        m_EdgeHeadNodeLookup.Add(edgeId, node);
                    }
                }
                // Assimilate outgoing edges.
                if (node.NodeType != NodeType.End && node.NodeType != NodeType.Isolated)
                {
                    foreach (T edgeId in node.OutgoingEdges)
                    {
                        m_EdgeTailNodeLookup.Add(edgeId, node);
                    }
                }
                m_NodeLookup.Add(node.Id, node);
            }

            // Check all edges are used.
            if (!m_EdgeLookup.Keys.OrderBy(x => x).SequenceEqual(m_EdgeHeadNodeLookup.Keys.OrderBy(x => x)))
            {
                throw new ArgumentException(Properties.Resources.Message_ListOfEdgeIdsAndEdgesReferencedByHeadNodesDoNotMatch);
            }
            if (!m_EdgeLookup.Keys.OrderBy(x => x).SequenceEqual(m_EdgeTailNodeLookup.Keys.OrderBy(x => x)))
            {
                throw new ArgumentException(Properties.Resources.Message_ListOfEdgeIdsAndEdgesReferencedByTailNodesDoNotMatch);
            }

            // Check all nodes are used.
            IEnumerable<T> edgeNodeLookupIds = m_EdgeHeadNodeLookup.Values.Select(x => x.Id).Union(m_EdgeTailNodeLookup.Values.Select(x => x.Id));
            if (!m_NodeLookup.Values.Where(x => x.NodeType != NodeType.Isolated).Select(x => x.Id).OrderBy(x => x).SequenceEqual(edgeNodeLookupIds.OrderBy(x => x)))
            {
                throw new ArgumentException(Properties.Resources.Message_ListOfNodeIdsAndEdgesReferencedByTailNodesDoNotMatch);
            }

            // Check Start and End nodes when normal nodes are present.
            if (NormalNodes.Any())
            {
                if (!StartNodes.Any())
                {
                    throw new ArgumentException(Properties.Resources.Message_VertexGraphCannotContainNormalNodesWithoutAnyStartNodes);
                }
                if (!EndNodes.Any())
                {
                    throw new ArgumentException(Properties.Resources.Message_VertexGraphCannotContainNormalNodesWithoutAnyEndNodes);
                }
            }

            m_TransitiveReducer = CreateTransitiveReducer();
        }

        #endregion

        #region Properties

        public IEnumerable<Node<T, TActivity>> StartNodes =>
            m_NodeLookup.Values.Where(x => x.NodeType == NodeType.Start);

        public IEnumerable<Node<T, TActivity>> EndNodes =>
            m_NodeLookup.Values.Where(x => x.NodeType == NodeType.End);

        public IEnumerable<Node<T, TActivity>> NormalNodes =>
            m_NodeLookup.Values.Where(x => x.NodeType == NodeType.Normal);

        public IEnumerable<Node<T, TActivity>> IsolatedNodes =>
            m_NodeLookup.Values.Where(x => x.NodeType == NodeType.Isolated);

        public IEnumerable<T> EdgeIds => m_EdgeLookup.Keys;

        public IEnumerable<T> NodeIds => m_NodeLookup.Keys;

        // In vertex graphs, activities are nodes and events are edges.
        public IEnumerable<TActivity> Activities => m_NodeLookup.Values.Select(x => x.Content);

        public IEnumerable<IEvent<T>> Events => m_EdgeLookup.Values.Select(x => x.Content);

        public IEnumerable<T> ActivityIds => Activities.Select(x => x.Id);

        public IEnumerable<T> EventIds => Events.Select(x => x.Id);

        public IEnumerable<Edge<T, IEvent<T>>> Edges => m_EdgeLookup.Values;

        public IEnumerable<Node<T, TActivity>> Nodes => m_NodeLookup.Values;

        public IEnumerable<T> InvalidDependencies => m_UnsatisfiedSuccessorsLookup.Keys;

        public bool AllDependenciesSatisfied => !m_UnsatisfiedSuccessorsLookup.Any();

        public int StartTime =>
            Activities.Select(x => x.EarliestStartTime.GetValueOrDefault()).DefaultIfEmpty().Min();

        public int FinishTime =>
            Activities.Select(x => x.LatestFinishTime.GetValueOrDefault()).DefaultIfEmpty().Max();

        public bool WhenTesting { get; set; }

        #endregion

        #region Public Methods

        public TActivity Activity(T key)
        {
            return m_NodeLookup[key].Content;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "No better term available")]
        public IEvent<T> Event(T key)
        {
            return m_EdgeLookup[key].Content;
        }

        public Edge<T, IEvent<T>> Edge(T key)
        {
            if (!m_EdgeLookup.TryGetValue(key, out Edge<T, IEvent<T>> edge))
            {
                return null;
            }
            return edge;
        }

        public Node<T, TActivity> Node(T key)
        {
            if (!m_NodeLookup.TryGetValue(key, out Node<T, TActivity> node))
            {
                return null;
            }
            return node;
        }

        public Node<T, TActivity> EdgeHeadNode(T key)
        {
            Node<T, TActivity> output = null;
            if (m_EdgeHeadNodeLookup.ContainsKey(key))
            {
                output = m_EdgeHeadNodeLookup[key];
            }
            return output;
        }

        public Node<T, TActivity> EdgeTailNode(T key)
        {
            Node<T, TActivity> output = null;
            if (m_EdgeTailNodeLookup.ContainsKey(key))
            {
                output = m_EdgeTailNodeLookup[key];
            }
            return output;
        }

        public bool AddActivity(TActivity activity)
        {
            return AddActivity(activity, new HashSet<T>());
        }

        public bool AddActivity(TActivity activity, HashSet<T> dependencies)
        {
            if (activity == null)
            {
                throw new ArgumentNullException(nameof(activity));
            }
            if (dependencies is null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }
            if (m_NodeLookup.ContainsKey(activity.Id))
            {
                return false;
            }
            if (dependencies.Contains(activity.Id))
            {
                return false;
            }
            // Create a new Isolated node for the activity.
            var node = new Node<T, TActivity>(NodeType.Isolated, activity);
            m_NodeLookup.Add(node.Id, node);

            // We expect dependencies at some point.
            if (dependencies.Any())
            {
                node.SetNodeType(NodeType.End);

                // Check which of the expected dependencies currently exist.
                IList<T> existingDependencies = m_NodeLookup.Keys.Intersect(dependencies).ToList();
                IList<T> nonExistingDependencies = dependencies.Except(existingDependencies).ToList();

                // If any expected dependencies currently exist, generate an edge to connect them.
                foreach (T dependencyId in existingDependencies)
                {
                    Node<T, TActivity> dependencyNode = m_NodeLookup[dependencyId];
                    T edgeId = m_EdgeIdGenerator();
                    var edge = new Edge<T, IEvent<T>>(s_EventGenerator(edgeId));
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
                    m_EdgeLookup.Add(edgeId, edge);
                }

                // If any expected dependencies currently do not exist, then record their
                // IDs and add this node as an unsatisfied successor.
                foreach (T dependencyId in nonExistingDependencies)
                {
                    if (!m_UnsatisfiedSuccessorsLookup.TryGetValue(dependencyId, out HashSet<Node<T, TActivity>> successorNodes))
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

        public bool AddActivityDependencies(T activityId, HashSet<T> dependencies)
        {
            if (dependencies is null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }

            if (!m_NodeLookup.TryGetValue(activityId, out Node<T, TActivity> node))
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

            // If the node is a Start or Isolated node, then convert it.
            if (node.NodeType == NodeType.Start)
            {
                node.SetNodeType(NodeType.Normal);
            }
            else if (node.NodeType == NodeType.Isolated)
            {
                node.SetNodeType(NodeType.End);
            }

            // Check which of the expected dependencies currently exist.
            IList<T> existingDependencies = m_NodeLookup.Keys.Intersect(dependencies).ToList();
            IList<T> nonExistingDependencies = dependencies.Except(existingDependencies).ToList();

            // If any expected dependencies currently exist, generate an edge to connect them.
            foreach (T dependencyId in existingDependencies)
            {
                Node<T, TActivity> dependencyNode = m_NodeLookup[dependencyId];
                T edgeId = m_EdgeIdGenerator();
                var edge = new Edge<T, IEvent<T>>(s_EventGenerator(edgeId));
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
                m_EdgeLookup.Add(edgeId, edge);
            }

            // If any expected dependencies currently do not exist, then record their
            // IDs and add this node as an unsatisfied successor.
            foreach (T dependencyId in nonExistingDependencies)
            {
                if (!m_UnsatisfiedSuccessorsLookup.TryGetValue(dependencyId, out HashSet<Node<T, TActivity>> successorNodes))
                {
                    successorNodes = new HashSet<Node<T, TActivity>>();
                    m_UnsatisfiedSuccessorsLookup.Add(dependencyId, successorNodes);
                }
                successorNodes.Add(node);
            }
            return true;
        }

        public bool RemoveActivity(T activityId)
        {
            if (!m_NodeLookup.TryGetValue(activityId, out Node<T, TActivity> node)) return false;
            if (!node.Content.CanBeRemoved) return false;

            RemoveUnsatisfiedSuccessorActivity(activityId);
            m_NodeLookup.Remove(node.Id);

            if (node.NodeType == NodeType.Isolated) return true;

            if (node.NodeType == NodeType.End || node.NodeType == NodeType.Normal)
            {
                RemoveIncomingEdgesFromNode(node);
            }

            if (node.NodeType == NodeType.Start || node.NodeType == NodeType.Normal)
            {
                RemoveOutgoingEdgesFromNode(node);
            }
            return true;
        }

        private void RemoveIncomingEdgesFromNode(Node<T, TActivity> node)
        {
            foreach (T edgeId in node.IncomingEdges.ToList())
            {
                Node<T, TActivity> tailNode = m_EdgeTailNodeLookup[edgeId];

                tailNode.OutgoingEdges.Remove(edgeId);
                m_EdgeTailNodeLookup.Remove(edgeId);
                if (!tailNode.OutgoingEdges.Any()) DowngradeOutboundNodeType(tailNode);

                node.IncomingEdges.Remove(edgeId);
                m_EdgeHeadNodeLookup.Remove(edgeId);
                if (!node.IncomingEdges.Any()) DowngradeInboundNodeType(node);

                m_EdgeLookup.Remove(edgeId);
            }
        }

        private void RemoveOutgoingEdgesFromNode(Node<T, TActivity> node)
        {
            foreach (T edgeId in node.OutgoingEdges.ToList())
            {
                Node<T, TActivity> headNode = m_EdgeHeadNodeLookup[edgeId];

                headNode.IncomingEdges.Remove(edgeId);
                m_EdgeHeadNodeLookup.Remove(edgeId);
                if (!headNode.IncomingEdges.Any()) DowngradeInboundNodeType(headNode);

                node.OutgoingEdges.Remove(edgeId);
                m_EdgeTailNodeLookup.Remove(edgeId);
                if (!node.OutgoingEdges.Any()) DowngradeOutboundNodeType(node);

                m_EdgeLookup.Remove(edgeId);
            }
        }

        // When a node loses its last outgoing edge it can no longer be Start or Normal.
        private static void DowngradeOutboundNodeType(Node<T, TActivity> node)
        {
            if (node.NodeType == NodeType.Normal) node.SetNodeType(NodeType.End);
            else if (node.NodeType == NodeType.Start) node.SetNodeType(NodeType.Isolated);
        }

        // When a node loses its last incoming edge it can no longer be End or Normal.
        private static void DowngradeInboundNodeType(Node<T, TActivity> node)
        {
            if (node.NodeType == NodeType.Normal) node.SetNodeType(NodeType.Start);
            else if (node.NodeType == NodeType.End) node.SetNodeType(NodeType.Isolated);
        }

        public bool RemoveActivityDependencies(T activityId, HashSet<T> dependencies)
        {
            if (dependencies is null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }
            if (!m_NodeLookup.TryGetValue(activityId, out Node<T, TActivity> node))
            {
                return false;
            }
            if (!dependencies.Any())
            {
                return true;
            }

            RemoveUnsatisfiedSuccessorActivityDependencies(activityId, dependencies);

            if (node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated)
            {
                return true;
            }

            // Remove edges whose tail node is in the specified dependency set.
            var existingDependencyLookup = new HashSet<T>(m_NodeLookup.Keys.Intersect(dependencies));

            foreach (T edgeId in node.IncomingEdges.ToList())
            {
                Node<T, TActivity> tailNode = m_EdgeTailNodeLookup[edgeId];
                if (!existingDependencyLookup.Contains(tailNode.Id)) continue;

                tailNode.OutgoingEdges.Remove(edgeId);
                m_EdgeTailNodeLookup.Remove(edgeId);
                if (!tailNode.OutgoingEdges.Any()) DowngradeOutboundNodeType(tailNode);

                node.IncomingEdges.Remove(edgeId);
                m_EdgeHeadNodeLookup.Remove(edgeId);
                m_EdgeLookup.Remove(edgeId);
            }

            if (!node.IncomingEdges.Any()) DowngradeInboundNodeType(node);

            return true;
        }

        public IList<T> ActivityDependencyIds(T activityId)
        {
            Node<T, TActivity> node = m_NodeLookup[activityId];
            if (node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated)
            {
                return new List<T>();
            }
            var output = new List<T>();
            foreach (Edge<T, IEvent<T>> incomingEdge in node.IncomingEdges.Select(x => m_EdgeLookup[x]))
            {
                Node<T, TActivity> tailNode = m_EdgeTailNodeLookup[incomingEdge.Id];
                output.Add(tailNode.Id);
            }
            return output;
        }

        public IList<T> StrongActivityDependencyIds(T activityId)
        {
            Node<T, TActivity> node = m_NodeLookup[activityId];
            if (node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated)
            {
                return new List<T>();
            }
            var output = new List<T>();
            foreach (Edge<T, IEvent<T>> incomingEdge in node.IncomingEdges.Select(x => m_EdgeLookup[x]))
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

        public IList<ICircularDependency<T>> FindStrongCircularDependencies()
        {
            return FindStronglyConnectedComponents().Where(x => x.Dependencies.Count > 1).ToList();
        }

        public IList<IInvalidConstraint<T>> FindInvalidPreCompilationConstraints() =>
            ConstraintChecker<T, TResourceId, TWorkStreamId>.FindInvalidPreCompilationConstraints(
                Activities.Cast<IActivity<T, TResourceId, TWorkStreamId>>());

        public IList<IInvalidConstraint<T>> FindInvalidPostCompilationConstraints() =>
            ConstraintChecker<T, TResourceId, TWorkStreamId>.FindInvalidPostCompilationConstraints(
                Activities.Cast<IActivity<T, TResourceId, TWorkStreamId>>());

        public IDictionary<T, HashSet<T>> GetAncestorNodesLookup()
        {
            return m_TransitiveReducer.GetAncestorNodesLookup();
        }

        public bool TransitiveReduction()
        {
            return m_TransitiveReducer.ReduceGraph();
        }

        public bool RedirectEdges()
        {
            // Edges should not need to be redirected in a vertex graph.
            return true;
        }

        public bool RemoveRedundantEdges()
        {
            // All redundant edges should have been removed by other methods.
            return true;
        }

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

        // Exposed as public for direct testing of the forward/backward flow steps separately.
        public void ClearCriticalPathVariables()
        {
            foreach (TActivity activity in Activities)
            {
                activity.FreeSlack = null;
                activity.EarliestStartTime = null;
                activity.LatestFinishTime = null;
            }
            foreach (IEvent<T> evt in Events)
            {
                evt.EarliestFinishTime = null;
                evt.LatestFinishTime = null;
            }
        }

        // Returns bool (vs throwing) so tests can assert the return value directly.
        public bool CalculateCriticalPathForwardFlow()
        {
            if (!AllDependenciesSatisfied)
            {
                return false;
            }
            if (FindInvalidPreCompilationConstraints().Any())
            {
                return false;
            }
            return m_CriticalPathEngine.CalculateCriticalPathForwardFlow(
                EdgeIds,
                m_EdgeLookup,
                m_NodeLookup,
                m_EdgeHeadNodeLookup,
                m_EdgeTailNodeLookup,
                new List<IInvalidConstraint<T>>(),
                IsolatedNodes,
                StartNodes,
                EndNodes,
                WhenTesting);
        }

        // Returns bool (vs throwing) so tests can assert the return value directly.
        public bool CalculateCriticalPathBackwardFlow()
        {
            if (!AllDependenciesSatisfied)
            {
                return false;
            }
            if (FindInvalidPreCompilationConstraints().Any())
            {
                return false;
            }
            return m_CriticalPathEngine.CalculateCriticalPathBackwardFlow(
                EdgeIds,
                m_EdgeLookup,
                m_NodeLookup,
                m_EdgeHeadNodeLookup,
                m_EdgeTailNodeLookup,
                new List<IInvalidConstraint<T>>(),
                IsolatedNodes,
                StartNodes,
                EndNodes,
                Events,
                Activities,
                WhenTesting);
        }

        // Exposes the priority list calculation used internally by CalculateResourceSchedulesByPriorityList.
        public IList<T> CalculateCriticalPathPriorityList()
        {
            var tmpGraphBuilder = (VertexGraphBuilder<T, TResourceId, TWorkStreamId, TActivity>)CloneObject();
            return CalculateCriticalPathPriorityList(tmpGraphBuilder);
        }

        public void CalculateCriticalPath()
        {
            bool edgesCleaned = CleanUpEdges();
            if (!edgesCleaned)
            {
                throw new InvalidOperationException(Properties.Resources.Message_CannotPerformEdgeCleanUp);
            }

            ClearCriticalPathVariables();

            IList<IInvalidConstraint<T>> constraints = AllDependenciesSatisfied
                ? FindInvalidPreCompilationConstraints()
                : new List<IInvalidConstraint<T>>();

            if (!m_CriticalPathEngine.CalculateCriticalPathForwardFlow(
                EdgeIds,
                m_EdgeLookup,
                m_NodeLookup,
                m_EdgeHeadNodeLookup,
                m_EdgeTailNodeLookup,
                constraints,
                IsolatedNodes,
                StartNodes,
                EndNodes,
                WhenTesting))
            {
                throw new InvalidOperationException(Properties.Resources.Message_CannotCalculateCriticalPathForwardFlow);
            }
            if (!m_CriticalPathEngine.CalculateCriticalPathBackwardFlow(
                EdgeIds,
                m_EdgeLookup,
                m_NodeLookup,
                m_EdgeHeadNodeLookup,
                m_EdgeTailNodeLookup,
                constraints,
                IsolatedNodes,
                StartNodes,
                EndNodes,
                Events,
                Activities,
                WhenTesting))
            {
                throw new InvalidOperationException(Properties.Resources.Message_CannotCalculateCriticalPathBackwardFlow);
            }
        }

        public bool BackFillIsolatedNodes()
        {
            IList<IInvalidConstraint<T>> constraints = AllDependenciesSatisfied
                ? FindInvalidPreCompilationConstraints()
                : new List<IInvalidConstraint<T>>();
            return m_CriticalPathEngine.BackFillIsolatedNodes(constraints, IsolatedNodes, EndNodes);
        }

        public IEnumerable<IResourceSchedule<T, TResourceId, TWorkStreamId>> CalculateResourceSchedulesByPriorityList(
            IList<IResource<TResourceId, TWorkStreamId>> resources)
        {
            if (resources is null)
            {
                throw new ArgumentNullException(nameof(resources));
            }
            if (resources.Count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(resources), Properties.Resources.Message_ValueCannotBeNegative);
            }
            if (!Activities.Any())
            {
                return Enumerable.Empty<IResourceSchedule<T, TResourceId, TWorkStreamId>>();
            }

            // If resources are 0, assume infinite.
            bool infiniteResources = !resources.Any();

            // Filter out inactive resources.
            IList<IResource<TResourceId, TWorkStreamId>> filteredResources = resources.Where(x => !x.IsInactive).ToList();

            // If resources are limited, check to make sure all activities can be accepted.
            if (!infiniteResources)
            {
                ValidateActivitiesAgainstResources(filteredResources);
            }

            var tmpGraphBuilder = (VertexGraphBuilder<T, TResourceId, TWorkStreamId, TActivity>)CloneObject();

            // Use a separate clone for the priority list calculation so that tmpGraphBuilder retains
            // original activity durations for the scheduling loop below.
            IList<T> priorityList = CalculateCriticalPathPriorityList(
                (VertexGraphBuilder<T, TResourceId, TWorkStreamId, TActivity>)tmpGraphBuilder.CloneObject());

            return m_ResourceSchedulingEngine.CalculateResourceSchedules(
                priorityList,
                filteredResources,
                infiniteResources,
                id => tmpGraphBuilder.Activity(id),
                id => tmpGraphBuilder.StrongActivityDependencyIds(id),
                () => Activities.Select(x => (IActivity<T, TResourceId, TWorkStreamId>)x.CloneObject()).ToList());
        }

        private void ValidateActivitiesAgainstResources(IList<IResource<TResourceId, TWorkStreamId>> filteredResources)
        {
            var unavailableResourcesSet = new List<IUnavailableResources<T, TResourceId>>();

            foreach (TActivity activity in Activities)
            {
                if (!activity.TargetResources.Any()) continue;

                if (activity.TargetResourceOperator == LogicalOperator.AND)
                {
                    IEnumerable<TResourceId> unavailableResourceIds =
                        activity.TargetResources.Except(filteredResources.Select(x => x.Id));

                    if (unavailableResourceIds.Any())
                    {
                        unavailableResourcesSet.Add(
                            new UnavailableResources<T, TResourceId>(activity.Id, unavailableResourceIds));
                    }
                }
                else if (activity.TargetResourceOperator == LogicalOperator.OR
                         || activity.TargetResourceOperator == LogicalOperator.ACTIVE_AND)
                {
                    IEnumerable<TResourceId> intersection =
                        activity.TargetResources.Intersect(filteredResources.Select(x => x.Id));

                    if (!intersection.Any())
                    {
                        unavailableResourcesSet.Add(
                            new UnavailableResources<T, TResourceId>(activity.Id, activity.TargetResources));
                    }
                }
            }

            if (unavailableResourcesSet.Any())
            {
                throw new InvalidOperationException(Properties.Resources.Message_AtLeastOneOfSpecifiedTargetResourcesAreNotAvailableInResourcesProvided);
            }

            bool allResourcesAreExplicitTargets = filteredResources.All(x => x.IsExplicitTarget);
            bool atLeastOneActivityRequiresNonExplicitTargetResource = Activities.Any(x => !x.IsDummy && !x.TargetResources.Any());
            if (allResourcesAreExplicitTargets && atLeastOneActivityRequiresNonExplicitTargetResource)
            {
                throw new InvalidOperationException(Properties.Resources.Message_AtLeastOneActivityRequiresNonExplicitTargetResourceButAllProvidedResourcesAreExplicitTargets);
            }
        }

        private static IList<T> CalculateCriticalPathPriorityList(VertexGraphBuilder<T, TResourceId, TWorkStreamId, TActivity> graphBuilder)
        {
            if (graphBuilder is null)
            {
                throw new ArgumentNullException(nameof(graphBuilder));
            }
            var priorityList = new List<T>();
            bool cont = true;
            while (cont)
            {
                graphBuilder.CalculateCriticalPath();

                int minFloat = graphBuilder.Activities
                    .Where(x => !x.IsDummy && x.TotalSlack.HasValue)
                    .Select(x => x.TotalSlack.Value)
                    .DefaultIfEmpty()
                    .Min();

                IList<T> criticalActivityIds =
                    graphBuilder.Activities
                    .Where(x => x.TotalSlack == minFloat && !x.IsDummy)
                    .OrderBy(x => x.EarliestStartTime)
                    .Select(x => x.Id)
                    .ToList();

                if (criticalActivityIds.Any())
                {
                    T criticalActivityId = criticalActivityIds.First();
                    priorityList.Add(criticalActivityId);
                    graphBuilder.Activity(criticalActivityId).Duration = 0;
                }
                else
                {
                    cont = false;
                }
            }
            if (graphBuilder.Activities.Any(x => !x.IsDummy))
            {
                throw new InvalidOperationException(Properties.Resources.Message_CannotCalculateCriticalPathPriorityList);
            }
            return priorityList;
        }

        public Graph<T, IEvent<T>, TActivity> ToGraph()
        {
            bool edgesCleanedUp = CleanUpEdges();
            if (!edgesCleanedUp)
            {
                return null;
            }
            return new Graph<T, IEvent<T>, TActivity>(
                m_EdgeLookup.Values.Select(x => (Edge<T, IEvent<T>>)x.CloneObject()),
                m_NodeLookup.Values.Select(x => (Node<T, TActivity>)x.CloneObject()));
        }

        public void Reset()
        {
            m_EdgeLookup.Clear();
            m_NodeLookup.Clear();
            m_UnsatisfiedSuccessorsLookup.Clear();
            m_EdgeHeadNodeLookup.Clear();
            m_EdgeTailNodeLookup.Clear();
        }

        // Sets the compiled and planning dependencies for an activity, reconciling them
        // with any existing resource dependencies already wired into the graph.
        public bool SetActivityDependencies(T activityId, HashSet<T> dependencies, HashSet<T> planningDependencies)
        {
            if (dependencies is null) throw new ArgumentNullException(nameof(dependencies));
            if (planningDependencies is null) throw new ArgumentNullException(nameof(planningDependencies));
            if (!ActivityIds.Contains(activityId)) return false;

            TActivity activityObj = Activity(activityId);

            // Cast to IDependentActivity to access ResourceDependencies — only valid for
            // TActivity subtypes that implement IDependentActivity (e.g. the compiler path).
            if (!(activityObj is IDependentActivity<T, TResourceId, TWorkStreamId> dependentActivity))
            {
                // Non-dependent activity: just wire in the dependencies directly.
                IList<T> currentDeps = ActivityDependencyIds(activityId);
                var toBeRemoved = new HashSet<T>(currentDeps.Except(dependencies.Union(planningDependencies)));
                var toBeAdded = new HashSet<T>(dependencies.Union(planningDependencies).Except(currentDeps));
                bool removed = RemoveActivityDependencies(activityId, toBeRemoved);
                bool added = AddActivityDependencies(activityId, toBeAdded);
                return removed && added;
            }

            var resourceAndCompiledDependencies = new HashSet<T>(dependentActivity.ResourceDependencies.Intersect(dependentActivity.Dependencies));
            var resourceAndPlanningDependencies = new HashSet<T>(dependentActivity.ResourceDependencies.Intersect(dependentActivity.PlanningDependencies));

            var resourceOrCompiledDependencies = new HashSet<T>(dependentActivity.ResourceDependencies.Union(dependentActivity.Dependencies));
            var resourceOrPlanningDependencies = new HashSet<T>(dependentActivity.ResourceDependencies.Union(dependentActivity.PlanningDependencies));

            var compiledNotResourceDependencies = new HashSet<T>(dependentActivity.Dependencies.Except(dependentActivity.ResourceDependencies));
            var planningNotResourceDependencies = new HashSet<T>(dependentActivity.PlanningDependencies.Except(dependentActivity.ResourceDependencies));

            var resourceNotCompiledDependencies = new HashSet<T>(dependentActivity.ResourceDependencies.Except(dependentActivity.Dependencies));
            var resourceNotPlanningDependencies = new HashSet<T>(dependentActivity.ResourceDependencies.Except(dependentActivity.PlanningDependencies));

            bool successfullyRemoved = true;
            bool successfullyAdded = true;

            // Resource: 1, Core: 1, New: 0
            {
                IList<T> toBeRemovedFromCompiledDependencies = resourceAndCompiledDependencies.Except(dependencies).ToList();
                foreach (T dependencyId in toBeRemovedFromCompiledDependencies) dependentActivity.Dependencies.Remove(dependencyId);

                IList<T> toBeRemovedFromPlanningDependencies = resourceAndPlanningDependencies.Except(planningDependencies).ToList();
                foreach (T dependencyId in toBeRemovedFromPlanningDependencies) dependentActivity.PlanningDependencies.Remove(dependencyId);

                List<T> updatedDependencies = dependentActivity.Dependencies.Union(dependentActivity.PlanningDependencies).Union(dependentActivity.ResourceDependencies).ToList();
                IList<T> currentDependencies = ActivityDependencyIds(activityId);
                var toBeRemoved = new HashSet<T>(currentDependencies.Except(updatedDependencies).Union(toBeRemovedFromCompiledDependencies).Union(toBeRemovedFromPlanningDependencies));
                successfullyRemoved &= RemoveActivityDependencies(activityId, toBeRemoved);
            }

            // Resource: 1, Core: 0, New: 1
            {
                var toBeAddedToCompiledDependencies = resourceNotCompiledDependencies.Intersect(dependencies);
                foreach (T dependencyId in toBeAddedToCompiledDependencies) dependentActivity.Dependencies.Add(dependencyId);

                var toBeAddedToPlanningDependencies = resourceNotPlanningDependencies.Intersect(planningDependencies);
                foreach (T dependencyId in toBeAddedToPlanningDependencies) dependentActivity.PlanningDependencies.Add(dependencyId);

                List<T> updatedDependencies = dependentActivity.Dependencies.Union(dependentActivity.PlanningDependencies).Union(dependentActivity.ResourceDependencies).ToList();
                IList<T> currentDependencies = ActivityDependencyIds(activityId);
                var toBeAdded = new HashSet<T>(updatedDependencies.Except(currentDependencies).Union(toBeAddedToCompiledDependencies).Union(toBeAddedToPlanningDependencies));
                successfullyAdded &= AddActivityDependencies(activityId, toBeAdded);
            }

            // Resource: 0, Core: 1, New: 0
            {
                var toBeRemovedFromCompiledDependencies = compiledNotResourceDependencies.Except(dependencies);
                foreach (T dependencyId in toBeRemovedFromCompiledDependencies) dependentActivity.Dependencies.Remove(dependencyId);

                var toBeRemovedFromPlanningDependencies = planningNotResourceDependencies.Except(planningDependencies);
                foreach (T dependencyId in toBeRemovedFromPlanningDependencies) dependentActivity.PlanningDependencies.Remove(dependencyId);

                List<T> updatedDependencies = dependentActivity.Dependencies.Union(dependentActivity.PlanningDependencies).Union(dependentActivity.ResourceDependencies).ToList();
                IList<T> currentDependencies = ActivityDependencyIds(activityId);
                var toBeRemoved = new HashSet<T>(currentDependencies.Except(updatedDependencies).Union(toBeRemovedFromCompiledDependencies).Union(toBeRemovedFromPlanningDependencies));
                successfullyRemoved &= RemoveActivityDependencies(activityId, toBeRemoved);
            }

            // Resource: 0, Core: 0, New: X
            {
                var toBeAddedToCompiledDependencies = dependencies.Except(resourceOrCompiledDependencies);
                foreach (T dependencyId in toBeAddedToCompiledDependencies) dependentActivity.Dependencies.Add(dependencyId);

                var toBeAddedToPlanningDependencies = planningDependencies.Except(resourceOrPlanningDependencies);
                foreach (T dependencyId in toBeAddedToPlanningDependencies) dependentActivity.PlanningDependencies.Add(dependencyId);

                List<T> updatedDependencies = dependentActivity.Dependencies.Union(dependentActivity.PlanningDependencies).Union(dependentActivity.ResourceDependencies).ToList();
                IList<T> currentDependencies = ActivityDependencyIds(activityId);
                var toBeAdded = new HashSet<T>(updatedDependencies.Except(currentDependencies).Union(toBeAddedToCompiledDependencies).Union(toBeAddedToPlanningDependencies));
                successfullyAdded &= AddActivityDependencies(activityId, toBeAdded);
            }

            return successfullyRemoved && successfullyAdded;
        }

        // Strips resource-only dependencies and clears resource allocation state before a compile pass.
        public void ResetResourceState(IEnumerable<TActivity> activities)
        {
            foreach (TActivity activity in activities)
            {
                if (!(activity is IDependentActivity<T, TResourceId, TWorkStreamId> dependentActivity)) continue;
                IEnumerable<T> coreDependencies = dependentActivity.Dependencies.Union(dependentActivity.PlanningDependencies);
                RemoveActivityDependencies(activity.Id, new HashSet<T>(dependentActivity.ResourceDependencies.Except(coreDependencies)));
                dependentActivity.ResourceDependencies.Clear();
                dependentActivity.AllocatedToResources.Clear();
            }
        }

        // Wires resource dependencies into the graph from the finished schedule.
        public void AssignResourceDependencies(IList<IResourceSchedule<T, TResourceId, TWorkStreamId>> resourceSchedules)
        {
            foreach (IResourceSchedule<T, TResourceId, TWorkStreamId> schedule in resourceSchedules)
            {
                IResource<TResourceId, TWorkStreamId> resource = schedule.Resource;
                T previousId = default;
                bool first = true;

                foreach (IScheduledActivity<T> scheduledActivity in schedule.ScheduledActivities.OrderBy(x => x.StartTime))
                {
                    T currentId = scheduledActivity.Id;
                    TActivity activityObj = Activity(currentId);
                    if (!(activityObj is IDependentActivity<T, TResourceId, TWorkStreamId> dependentActivity)) continue;

                    if (resource != null) dependentActivity.AllocatedToResources.Add(resource.Id);

                    if (!first)
                    {
                        dependentActivity.ResourceDependencies.Add(previousId);
                        IEnumerable<T> coreDependencies = dependentActivity.Dependencies.Union(dependentActivity.PlanningDependencies);
                        AddActivityDependencies(currentId, new HashSet<T>(dependentActivity.ResourceDependencies.Except(coreDependencies)));
                    }

                    first = false;
                    previousId = scheduledActivity.Id;
                }
            }
        }

        // Removes resource-only dependencies (those that are not core compiled or planning dependencies).
        public void RemoveResourceOnlyDependencies(IEnumerable<TActivity> activities)
        {
            foreach (TActivity activity in activities)
            {
                if (!(activity is IDependentActivity<T, TResourceId, TWorkStreamId> dependentActivity)) continue;
                IEnumerable<T> coreDependencies = dependentActivity.Dependencies.Union(dependentActivity.PlanningDependencies);
                RemoveActivityDependencies(activity.Id, new HashSet<T>(dependentActivity.ResourceDependencies.Except(coreDependencies)));
            }
        }

        // Recomputes each activity's Successors set from the current graph structure.
        public void UpdateActivitySuccessors(IEnumerable<TActivity> activities)
        {
            foreach (TActivity activity in activities)
            {
                if (!(activity is IDependentActivity<T, TResourceId, TWorkStreamId> dependentActivity)) continue;
                dependentActivity.Successors.Clear();
                Node<T, TActivity> node = Node(activity.Id);
                if (node.NodeType != NodeType.Start && node.NodeType != NodeType.Normal) continue;
                IEnumerable<T> successorNodeIds = node.OutgoingEdges.Select(EdgeHeadNode).Select(x => x.Id);
                dependentActivity.Successors.UnionWith(successorNodeIds);
            }
        }

        // Checks pre-compilation conditions and appends any errors found.
        public void AddPreCompilationErrors(
            List<GraphCompilationError> errors,
            IEnumerable<T> invalidDependencies,
            IEnumerable<TActivity> activities,
            IEnumerable<ICircularDependency<T>> circularDependencies,
            IEnumerable<IInvalidConstraint<T>> invalidPrecompilationConstraints,
            bool allResourcesExplicitTargetsButNotAllActivitiesTargeted,
            IList<IUnavailableResources<T, TResourceId>> unavailableResourcesSet)
        {
            if (invalidDependencies.Any())
            {
                IEnumerable<IDependentActivity<T, TResourceId, TWorkStreamId>> dependentActivities =
                    activities.OfType<IDependentActivity<T, TResourceId, TWorkStreamId>>();
                errors.Add(new GraphCompilationError(GraphCompilationErrorCode.P0010,
                    GraphCompilationErrorFormatter<T, TResourceId, TWorkStreamId, IDependentActivity<T, TResourceId, TWorkStreamId>>
                        .BuildInvalidDependenciesErrorMessage(invalidDependencies, dependentActivities)));
            }

            if (circularDependencies.Any())
                errors.Add(new GraphCompilationError(GraphCompilationErrorCode.P0020,
                    GraphCompilationErrorFormatter<T, TResourceId, TWorkStreamId, IDependentActivity<T, TResourceId, TWorkStreamId>>
                        .BuildCircularDependenciesErrorMessage(circularDependencies)));

            if (invalidPrecompilationConstraints.Any())
                errors.Add(new GraphCompilationError(GraphCompilationErrorCode.P0030,
                    GraphCompilationErrorFormatter<T, TResourceId, TWorkStreamId, IDependentActivity<T, TResourceId, TWorkStreamId>>
                        .BuildInvalidConstraintsErrorMessage(invalidPrecompilationConstraints)));

            if (allResourcesExplicitTargetsButNotAllActivitiesTargeted)
                errors.Add(new GraphCompilationError(GraphCompilationErrorCode.P0040,
                    $@"{Properties.Resources.Message_AllResourcesExplicitTargetsNotAllActivitiesTargeted}{Environment.NewLine}"));

            if (!CleanUpEdges())
                errors.Add(new GraphCompilationError(GraphCompilationErrorCode.P0050,
                    $@"{Properties.Resources.Message_UnableToRemoveUnnecessaryEdges}{Environment.NewLine}"));

            if (unavailableResourcesSet.Count != 0)
                errors.Add(new GraphCompilationError(GraphCompilationErrorCode.P0060,
                    GraphCompilationErrorFormatter<T, TResourceId, TWorkStreamId, IDependentActivity<T, TResourceId, TWorkStreamId>>
                        .BuildUnavailableResourcesErrorMessage(unavailableResourcesSet)));
        }

        #endregion

        #region Private Methods

        private IList<ICircularDependency<T>> FindStronglyConnectedComponents()
        {
            return m_SccFinder.FindStronglyConnectedComponents(
                NodeIds,
                m_NodeLookup,
                m_EdgeHeadNodeLookup,
                m_EdgeTailNodeLookup);
        }

        private ITransitiveReducer<T> CreateTransitiveReducer()
        {
            return new VertexTransitiveReducer<T, TResourceId, TWorkStreamId, TActivity>(
                () => AllDependenciesSatisfied,
                () => FindStrongCircularDependencies(),
                () => EndNodes.Select(x => x.Id),
                m_EdgeLookup,
                m_NodeLookup,
                m_EdgeHeadNodeLookup,
                m_EdgeTailNodeLookup);
        }

        private void ResolveUnsatisfiedSuccessorActivities(T activityId)
        {
            // Check to make sure the node really exists.
            if (!m_NodeLookup.TryGetValue(activityId, out Node<T, TActivity> dependencyNode))
            {
                return;
            }

            // Check to see if any existing activities were expecting this activity
            // as a dependency. If so, then hook their nodes to this activity with an edge.
            if (m_UnsatisfiedSuccessorsLookup.TryGetValue(activityId, out HashSet<Node<T, TActivity>> unsatisfiedSuccessorNodes))
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
                    var edge = new Edge<T, IEvent<T>>(s_EventGenerator(edgeId));
                    dependencyNode.OutgoingEdges.Add(edgeId);
                    m_EdgeTailNodeLookup.Add(edgeId, dependencyNode);
                    successorNode.IncomingEdges.Add(edgeId);
                    m_EdgeHeadNodeLookup.Add(edgeId, successorNode);
                    m_EdgeLookup.Add(edgeId, edge);
                }
                m_UnsatisfiedSuccessorsLookup.Remove(activityId);
            }
        }

        private void RemoveUnsatisfiedSuccessorActivity(T activityId)
        {
            // Check to make sure the node really exists.
            if (!m_NodeLookup.TryGetValue(activityId, out Node<T, TActivity> node))
            {
                return;
            }

            if (node.NodeType == NodeType.End || node.NodeType == NodeType.Normal)
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
                        m_UnsatisfiedSuccessorsLookup.Remove(kvp.Key);
                    }
                }
            }
        }

        private void RemoveUnsatisfiedSuccessorActivityDependencies(T activityId, HashSet<T> dependencies)
        {
            if (dependencies is null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }

            // If the activity was an unsatisfied successor for these dependencies,
            // then remove them from the lookup.
            foreach (T dependencyId in dependencies)
            {
                if (m_UnsatisfiedSuccessorsLookup.TryGetValue(dependencyId, out HashSet<Node<T, TActivity>> unsatisfiedSuccessorNodes))
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

        #region ICloneObject

        public object CloneObject()
        {
            Graph<T, IEvent<T>, TActivity> vertexGraphCopy = ToGraph();
            T minNodeId = vertexGraphCopy.Nodes.Select(x => x.Id).DefaultIfEmpty().Min();
            minNodeId = minNodeId.Previous();
            T minEdgeId = vertexGraphCopy.Edges.Select(x => x.Id).DefaultIfEmpty().Min();
            minEdgeId = minEdgeId.Previous();
            return new VertexGraphBuilder<T, TResourceId, TWorkStreamId, TActivity>(
                vertexGraphCopy,
                () => minEdgeId = minEdgeId.Previous(),
                () => minNodeId = minNodeId.Previous());
        }

        #endregion
    }
}
