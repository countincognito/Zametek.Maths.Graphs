using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    // Builder for Activity-on-Vertex graphs. Owns all graph state directly,
    // encapsulated in a single VertexGraphState instance shared with the injected
    // engines (SCC finder, CPM engine, transitive reducer). The public constructor
    // wires up default engine instances; the internal constructor accepts injected
    // engines for testability.
    /// <summary>
    /// Builds and maintains an Activity-on-Vertex graph (activities on nodes, events on edges): dynamic dependency resolution, transitive reduction, critical-path calculation and resource scheduling. Prefer driving it through <see cref="VertexGraphCompiler{T, TResourceId, TWorkStreamId, TDependentActivity}"/>.
    /// </summary>
    public class VertexGraphBuilder<T, TResourceId, TWorkStreamId, TActivity>
        : ICloneObject, IResourceSchedulingGraph<T, TResourceId, TWorkStreamId>
        where TActivity : IActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        #region Fields

        private static readonly IEventGenerator<T> s_DefaultEventGenerator = new RemovableEventGenerator<T>();

        private readonly IIdGenerator<T> m_EdgeIdGenerator;
        private readonly IEventGenerator<T> m_EventGenerator;

        private readonly IVertexStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity> m_SccFinder;
        private readonly IVertexCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity> m_CriticalPathEngine;
        private readonly IResourceSchedulingEngine<T, TResourceId, TWorkStreamId> m_ResourceSchedulingEngine;
        private readonly VertexGraphState<T, TResourceId, TWorkStreamId, TActivity> m_State;
        private readonly ITransitiveReducer<T> m_TransitiveReducer;
        // Default factory; overwritten by the engines-bundle constructors.
        private readonly IVertexTransitiveReducerFactory<T, TResourceId, TWorkStreamId, TActivity> m_TransitiveReducerFactory =
            new VertexTransitiveReducerFactory<T, TResourceId, TWorkStreamId, TActivity>();

        #endregion

        #region Ctors

        // Public constructor - stable API surface. Wires up default engine instances.
        /// <summary>
        /// Creates a builder with default engines, using the given edge (event) ID generator.
        /// </summary>
        public VertexGraphBuilder(IIdGenerator<T> edgeIdGenerator)
            : this(
                  edgeIdGenerator,
                  s_DefaultEventGenerator,
                  new VertexTarjanStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity>(),
                  new VertexCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity>(),
                  new PriorityListResourceScheduler<T, TResourceId, TWorkStreamId>())
        {
        }

        // Engines-bundle constructor - every engine/factory defaults to the standard
        // implementation; set only the bundle properties to customise. Additions to
        // the bundle do not break this signature.
        /// <summary>
        /// Creates a builder from an engines bundle; every bundle property defaults to the standard implementation.
        /// </summary>
        public VertexGraphBuilder(VertexGraphBuilderEngines<T, TResourceId, TWorkStreamId, TActivity> engines)
            : this(
                  (engines ?? throw new ArgumentNullException(nameof(engines))).EdgeIdGenerator,
                  engines.EventGenerator,
                  engines.SccFinder,
                  engines.CriticalPathEngine,
                  engines.ResourceSchedulingEngine)
        {
            m_TransitiveReducerFactory = engines.TransitiveReducerFactory ?? throw new ArgumentNullException(nameof(engines));
            m_TransitiveReducer = CreateTransitiveReducer();
        }

        // Engines-bundle graph-loading constructor.
        /// <summary>
        /// Creates a builder by assimilating an existing graph, from an engines bundle.
        /// </summary>
        public VertexGraphBuilder(
            Graph<T, IEvent<T>, TActivity> graph,
            VertexGraphBuilderEngines<T, TResourceId, TWorkStreamId, TActivity> engines)
            : this(
                  graph,
                  (engines ?? throw new ArgumentNullException(nameof(engines))).EdgeIdGenerator,
                  engines.EventGenerator,
                  engines.SccFinder,
                  engines.CriticalPathEngine,
                  engines.ResourceSchedulingEngine)
        {
            m_TransitiveReducerFactory = engines.TransitiveReducerFactory ?? throw new ArgumentNullException(nameof(engines));
            m_TransitiveReducer = CreateTransitiveReducer();
        }

        // Engine-injecting constructor - supply custom engines + event generator.
        /// <summary>
        /// Creates a builder with custom engines and generators.
        /// </summary>
        public VertexGraphBuilder(
            IIdGenerator<T> edgeIdGenerator,
            IEventGenerator<T> eventGenerator,
            IVertexStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity> sccFinder,
            IVertexCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity> criticalPathEngine,
            IResourceSchedulingEngine<T, TResourceId, TWorkStreamId>? resourceSchedulingEngine = null)
        {
            m_EdgeIdGenerator = edgeIdGenerator ?? throw new ArgumentNullException(nameof(edgeIdGenerator));
            m_EventGenerator = eventGenerator ?? throw new ArgumentNullException(nameof(eventGenerator));
            m_SccFinder = sccFinder ?? throw new ArgumentNullException(nameof(sccFinder));
            m_CriticalPathEngine = criticalPathEngine ?? throw new ArgumentNullException(nameof(criticalPathEngine));
            m_ResourceSchedulingEngine = resourceSchedulingEngine ?? new PriorityListResourceScheduler<T, TResourceId, TWorkStreamId>();

            m_State = new VertexGraphState<T, TResourceId, TWorkStreamId, TActivity>();
            ShuffleProcessingOrder = false;
            m_TransitiveReducer = CreateTransitiveReducer();
        }

        // Graph-loading constructor (from existing Graph<T, IEvent<T>, TActivity>).
        /// <summary>
        /// Creates a builder by assimilating an existing graph, with default engines.
        /// </summary>
        public VertexGraphBuilder(
            Graph<T, IEvent<T>, TActivity> graph,
            IIdGenerator<T> edgeIdGenerator)
            : this(
                  graph,
                  edgeIdGenerator,
                  s_DefaultEventGenerator,
                  new VertexTarjanStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity>(),
                  new VertexCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity>(),
                  new PriorityListResourceScheduler<T, TResourceId, TWorkStreamId>())
        {
        }

        // Engine-injecting graph-loading constructor.
        /// <summary>
        /// Creates a builder by assimilating an existing graph, with custom engines and generators.
        /// </summary>
        public VertexGraphBuilder(
            Graph<T, IEvent<T>, TActivity> graph,
            IIdGenerator<T> edgeIdGenerator,
            IEventGenerator<T> eventGenerator,
            IVertexStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity> sccFinder,
            IVertexCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity> criticalPathEngine,
            IResourceSchedulingEngine<T, TResourceId, TWorkStreamId> resourceSchedulingEngine)
        {
            if (graph is null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            m_EdgeIdGenerator = edgeIdGenerator ?? throw new ArgumentNullException(nameof(edgeIdGenerator));
            m_EventGenerator = eventGenerator ?? throw new ArgumentNullException(nameof(eventGenerator));
            m_SccFinder = sccFinder ?? throw new ArgumentNullException(nameof(sccFinder));
            m_CriticalPathEngine = criticalPathEngine ?? throw new ArgumentNullException(nameof(criticalPathEngine));
            m_ResourceSchedulingEngine = resourceSchedulingEngine ?? throw new ArgumentNullException(nameof(resourceSchedulingEngine));

            m_State = new VertexGraphState<T, TResourceId, TWorkStreamId, TActivity>();
            ShuffleProcessingOrder = false;

            foreach (Edge<T, IEvent<T>> edge in graph.Edges)
            {
                m_State.AddEdge(edge);
            }

            foreach (Node<T, TActivity> node in graph.Nodes)
            {
                // Assimilate incoming edges.
                if (node.NodeType != NodeType.Start && node.NodeType != NodeType.Isolated)
                {
                    foreach (T edgeId in node.IncomingEdges)
                    {
                        m_State.SetEdgeHeadNode(edgeId, node);
                    }
                }
                // Assimilate outgoing edges.
                if (node.NodeType != NodeType.End && node.NodeType != NodeType.Isolated)
                {
                    foreach (T edgeId in node.OutgoingEdges)
                    {
                        m_State.SetEdgeTailNode(edgeId, node);
                    }
                }
                m_State.AddNode(node);
            }

            // Check all edges are used.
            if (!m_State.EdgeKeysMatch(m_State.EdgeHeadNodeKeys))
            {
                throw new ArgumentException(Properties.Resources.Message_ListOfEdgeIdsAndEdgesReferencedByHeadNodesDoNotMatch);
            }
            if (!m_State.EdgeKeysMatch(m_State.EdgeTailNodeKeys))
            {
                throw new ArgumentException(Properties.Resources.Message_ListOfEdgeIdsAndEdgesReferencedByTailNodesDoNotMatch);
            }

            // Check all nodes are used.
            IEnumerable<T> edgeNodeLookupIds = m_State.EdgeHeadNodes.Select(x => x.Id).Union(m_State.EdgeTailNodes.Select(x => x.Id));
            if (!m_State.Nodes.Where(x => x.NodeType != NodeType.Isolated).Select(x => x.Id).OrderBy(x => x).SequenceEqual(edgeNodeLookupIds.OrderBy(x => x)))
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

        /// <summary>
        /// The nodes with only outgoing edges.
        /// </summary>
        public IEnumerable<Node<T, TActivity>> StartNodes => m_State.StartNodes;

        /// <summary>
        /// The nodes with only incoming edges.
        /// </summary>
        public IEnumerable<Node<T, TActivity>> EndNodes => m_State.EndNodes;

        /// <summary>
        /// The nodes with both incoming and outgoing edges.
        /// </summary>
        public IEnumerable<Node<T, TActivity>> NormalNodes => m_State.NormalNodes;

        /// <summary>
        /// The nodes with no edges at all.
        /// </summary>
        public IEnumerable<Node<T, TActivity>> IsolatedNodes => m_State.IsolatedNodes;

        /// <summary>
        /// The IDs of all edges (events).
        /// </summary>
        public IEnumerable<T> EdgeIds => m_State.EdgeIds;

        /// <summary>
        /// The IDs of all nodes (activities).
        /// </summary>
        public IEnumerable<T> NodeIds => m_State.NodeIds;

        // In vertex graphs, activities are nodes and events are edges.
        /// <summary>
        /// The activities carried on the nodes.
        /// </summary>
        public IEnumerable<TActivity> Activities => m_State.Activities;

        /// <summary>
        /// The events carried on the edges.
        /// </summary>
        public IEnumerable<IEvent<T>> Events => m_State.Events;

        /// <summary>
        /// The IDs of all activities.
        /// </summary>
        public IEnumerable<T> ActivityIds => Activities.Select(x => x.Id);

        /// <summary>
        /// The IDs of all events.
        /// </summary>
        public IEnumerable<T> EventIds => Events.Select(x => x.Id);

        /// <summary>
        /// All edges (events).
        /// </summary>
        public IEnumerable<Edge<T, IEvent<T>>> Edges => m_State.Edges;

        /// <summary>
        /// All nodes (activities).
        /// </summary>
        public IEnumerable<Node<T, TActivity>> Nodes => m_State.Nodes;

        /// <summary>
        /// The IDs of dependencies that are referenced but not yet present in the graph.
        /// </summary>
        public IEnumerable<T> InvalidDependencies => m_State.InvalidDependencies;

        /// <summary>
        /// Whether every referenced dependency is present in the graph.
        /// </summary>
        public bool AllDependenciesSatisfied => m_State.AllDependenciesSatisfied;

        /// <summary>
        /// The earliest start time across all activities.
        /// </summary>
        public int StartTime =>
            Activities.Select(x => x.EarliestStartTime.GetValueOrDefault()).DefaultIfEmpty().Min();

        /// <summary>
        /// The latest finish time across all activities.
        /// </summary>
        public int FinishTime =>
            Activities.Select(x => x.LatestFinishTime.GetValueOrDefault()).DefaultIfEmpty().Max();

        // When true, the critical-path passes process remaining edges in a random
        // order on each iteration. The results must be identical either way; tests
        // enable this to prove the calculation is order-independent.
        /// <summary>
        /// When true, the critical-path passes process remaining elements in a random order on each iteration (results are identical either way; used to prove order-independence).
        /// </summary>
        public bool ShuffleProcessingOrder { get; set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Resolves the activity with the given ID.
        /// </summary>
        public TActivity Activity(T key) => m_State.Node(key).Content;

        /// <summary>
        /// Resolves the event with the given ID.
        /// </summary>
        public IEvent<T> Event(T key) => m_State.Edge(key).Content;

        /// <summary>
        /// Resolves the edge with the given ID.
        /// </summary>
        public Edge<T, IEvent<T>> Edge(T key) => m_State.Edge(key);

        /// <summary>
        /// Resolves the node with the given ID.
        /// </summary>
        public Node<T, TActivity> Node(T key) => m_State.Node(key);

        /// <summary>
        /// Resolves the node the given edge points to.
        /// </summary>
        public Node<T, TActivity> EdgeHeadNode(T key) => m_State.EdgeHeadNode(key);

        /// <summary>
        /// Resolves the node the given edge starts from.
        /// </summary>
        public Node<T, TActivity> EdgeTailNode(T key) => m_State.EdgeTailNode(key);

        /// <summary>
        /// Adds an activity with no dependencies. Returns false if the ID already exists.
        /// </summary>
        public bool AddActivity(TActivity activity)
        {
            return AddActivity(activity, new HashSet<T>());
        }

        /// <summary>
        /// Adds an activity and wires up the given dependencies. Returns false if the ID already exists.
        /// </summary>
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
            if (m_State.ContainsNode(activity.Id))
            {
                return false;
            }
            if (dependencies.Contains(activity.Id))
            {
                return false;
            }
            // Create a new Isolated node for the activity.
            var node = new Node<T, TActivity>(NodeType.Isolated, activity);
            m_State.AddNode(node);

            // We expect dependencies at some point.
            if (dependencies.Count != 0)
            {
                node.SetNodeType(NodeType.End);

                // Check which of the expected dependencies currently exist.
                IList<T> existingDependencies = m_State.NodeIds.Intersect(dependencies).ToList();
                IList<T> nonExistingDependencies = dependencies.Except(existingDependencies).ToList();

                // If any expected dependencies currently exist, generate an edge to connect them.
                foreach (T dependencyId in existingDependencies)
                {
                    Node<T, TActivity> dependencyNode = m_State.Node(dependencyId);
                    T edgeId = m_EdgeIdGenerator.Generate();
                    var edge = new Edge<T, IEvent<T>>(m_EventGenerator.Generate(edgeId));
                    node.IncomingEdges.Add(edgeId);
                    m_State.SetEdgeHeadNode(edgeId, node);

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
                    m_State.SetEdgeTailNode(edgeId, dependencyNode);
                    m_State.AddEdge(edge);
                }

                // If any expected dependencies currently do not exist, then record their
                // IDs and add this node as an unsatisfied successor.
                foreach (T dependencyId in nonExistingDependencies)
                {
                    m_State.AddUnsatisfiedSuccessor(dependencyId, node);
                }
            }
            ResolveUnsatisfiedSuccessorActivities(node.Id);
            return true;
        }

        /// <summary>
        /// Adds the given dependencies to an existing activity.
        /// </summary>
        public bool AddActivityDependencies(T activityId, HashSet<T> dependencies)
        {
            if (dependencies is null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }

            if (!m_State.TryGetNode(activityId, out Node<T, TActivity> node))
            {
                return false;
            }
            if (dependencies.Count == 0)
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
            IList<T> existingDependencies = m_State.NodeIds.Intersect(dependencies).ToList();
            IList<T> nonExistingDependencies = dependencies.Except(existingDependencies).ToList();

            // If any expected dependencies currently exist, generate an edge to connect them.
            foreach (T dependencyId in existingDependencies)
            {
                Node<T, TActivity> dependencyNode = m_State.Node(dependencyId);
                T edgeId = m_EdgeIdGenerator.Generate();
                var edge = new Edge<T, IEvent<T>>(m_EventGenerator.Generate(edgeId));
                node.IncomingEdges.Add(edgeId);
                m_State.SetEdgeHeadNode(edgeId, node);

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
                m_State.SetEdgeTailNode(edgeId, dependencyNode);
                m_State.AddEdge(edge);
            }

            // If any expected dependencies currently do not exist, then record their
            // IDs and add this node as an unsatisfied successor.
            foreach (T dependencyId in nonExistingDependencies)
            {
                m_State.AddUnsatisfiedSuccessor(dependencyId, node);
            }
            return true;
        }

        /// <summary>
        /// Removes the activity with the given ID. Returns false if it cannot be removed.
        /// </summary>
        public bool RemoveActivity(T activityId)
        {
            // Retrieve the activity's node.
            if (!m_State.TryGetNode(activityId, out Node<T, TActivity> node))
            {
                return false;
            }
            if (!node.Content.CanBeRemoved)
            {
                return false;
            }

            RemoveUnsatisfiedSuccessorActivity(activityId);
            m_State.RemoveNode(node.Id);

            if (node.NodeType == NodeType.Isolated)
            {
                return true;
            }

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
                Node<T, TActivity> tailNode = m_State.EdgeTailNode(edgeId);

                // Remove the edge from the tail node.
                tailNode.OutgoingEdges.Remove(edgeId);
                m_State.RemoveEdgeTailNode(edgeId);
                if (tailNode.OutgoingEdges.Count == 0)
                {
                    DowngradeOutboundNodeType(tailNode);
                }

                // Remove the edge from the head node.
                node.IncomingEdges.Remove(edgeId);
                m_State.RemoveEdgeHeadNode(edgeId);
                if (node.IncomingEdges.Count == 0)
                {
                    DowngradeInboundNodeType(node);
                }

                // Remove the edge completely.
                m_State.RemoveEdge(edgeId);
            }
        }

        private void RemoveOutgoingEdgesFromNode(Node<T, TActivity> node)
        {
            foreach (T edgeId in node.OutgoingEdges.ToList())
            {
                Node<T, TActivity> headNode = m_State.EdgeHeadNode(edgeId);

                // Remove the edge from the head node.
                headNode.IncomingEdges.Remove(edgeId);
                m_State.RemoveEdgeHeadNode(edgeId);
                if (headNode.IncomingEdges.Count == 0)
                {
                    DowngradeInboundNodeType(headNode);
                }

                // Remove the edge from the tail node.
                node.OutgoingEdges.Remove(edgeId);
                m_State.RemoveEdgeTailNode(edgeId);
                if (node.OutgoingEdges.Count == 0)
                {
                    DowngradeOutboundNodeType(node);
                }

                // Remove the edge completely.
                m_State.RemoveEdge(edgeId);
            }
        }

        // When a node loses its last outgoing edge it can no longer be Start or Normal.
        private static void DowngradeOutboundNodeType(Node<T, TActivity> node)
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

        // When a node loses its last incoming edge it can no longer be End or Normal.
        private static void DowngradeInboundNodeType(Node<T, TActivity> node)
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

        /// <summary>
        /// Removes the given dependencies from an existing activity.
        /// </summary>
        public bool RemoveActivityDependencies(T activityId, HashSet<T> dependencies)
        {
            if (dependencies is null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }
            if (!m_State.TryGetNode(activityId, out Node<T, TActivity> node))
            {
                return false;
            }
            if (dependencies.Count == 0)
            {
                return true;
            }

            RemoveUnsatisfiedSuccessorActivityDependencies(activityId, dependencies);

            if (node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated)
            {
                return true;
            }

            // Remove edges whose tail node is in the specified dependency set.
            var existingDependencyLookup = new HashSet<T>(m_State.NodeIds.Intersect(dependencies));

            foreach (T edgeId in node.IncomingEdges.ToList())
            {
                Node<T, TActivity> tailNode = m_State.EdgeTailNode(edgeId);
                if (!existingDependencyLookup.Contains(tailNode.Id))
                {
                    continue;
                }

                // Remove the edge from the tail node.
                tailNode.OutgoingEdges.Remove(edgeId);
                m_State.RemoveEdgeTailNode(edgeId);
                if (tailNode.OutgoingEdges.Count == 0)
                {
                    DowngradeOutboundNodeType(tailNode);
                }

                // Remove the edge from the head node.
                node.IncomingEdges.Remove(edgeId);
                m_State.RemoveEdgeHeadNode(edgeId);
                // Remove the edge completely.
                m_State.RemoveEdge(edgeId);
            }

            if (node.IncomingEdges.Count == 0)
            {
                DowngradeInboundNodeType(node);
            }

            return true;
        }

        /// <summary>
        /// Returns the IDs of the activities the given activity currently depends on within the graph.
        /// </summary>
        public List<T> ActivityDependencyIds(T activityId)
        {
            Node<T, TActivity> node = m_State.Node(activityId);
            if (node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated)
            {
                return new List<T>();
            }
            var output = new List<T>();
            foreach (Edge<T, IEvent<T>> incomingEdge in node.IncomingEdges.Select(x => m_State.Edge(x)))
            {
                Node<T, TActivity> tailNode = m_State.EdgeTailNode(incomingEdge.Id);
                output.Add(tailNode.Id);
            }
            return output;
        }

        /// <summary>
        /// Returns the strong (resolved) dependency IDs for the given activity ID.
        /// </summary>
        public List<T> StrongActivityDependencyIds(T activityId)
        {
            Node<T, TActivity> node = m_State.Node(activityId);
            if (node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated)
            {
                return new List<T>();
            }
            // Iterative walk (dummy tails are transparent, real tails terminate the
            // walk) so a long dummy chain cannot overflow the stack. Expanded dummy
            // node IDs are tracked so a shared dummy sub-path is walked only once; the
            // resulting set of real dependency IDs is unaffected (callers use it as a set).
            var output = new List<T>();
            var expandedDummies = new HashSet<T>();
            var stack = new Stack<T>();
            stack.Push(activityId);
            while (stack.Count != 0)
            {
                Node<T, TActivity> currentNode = m_State.Node(stack.Pop());
                // Start/Isolated nodes have no incoming edges to follow.
                if (currentNode.NodeType == NodeType.Start || currentNode.NodeType == NodeType.Isolated)
                {
                    continue;
                }
                foreach (T incomingEdgeId in currentNode.IncomingEdges)
                {
                    Node<T, TActivity> tailNode = m_State.EdgeTailNode(incomingEdgeId);
                    if (tailNode.Content.IsDummy)
                    {
                        if (expandedDummies.Add(tailNode.Id))
                        {
                            stack.Push(tailNode.Id);
                        }
                    }
                    else
                    {
                        output.Add(tailNode.Id);
                    }
                }
            }
            return output;
        }

        /// <summary>
        /// Finds the strongly-connected circular dependencies in the graph.
        /// </summary>
        public List<ICircularDependency<T>> FindStrongCircularDependencies()
        {
            return m_SccFinder.FindStronglyCircularDependencies(m_State, ignoreDummies: true);
        }

        /// <summary>
        /// Finds activity constraints that are self-contradictory before compilation.
        /// </summary>
        public List<IInvalidConstraint<T>> FindInvalidPreCompilationConstraints() =>
            ConstraintChecker<T, TResourceId, TWorkStreamId>.FindInvalidPreCompilationConstraints(
                Activities.Cast<IActivity<T, TResourceId, TWorkStreamId>>().ToList());

        /// <summary>
        /// Finds activity constraints violated by the computed times after compilation.
        /// </summary>
        public List<IInvalidConstraint<T>> FindInvalidPostCompilationConstraints() =>
            ConstraintChecker<T, TResourceId, TWorkStreamId>.FindInvalidPostCompilationConstraints(
                Activities.Cast<IActivity<T, TResourceId, TWorkStreamId>>().ToList());

        /// <summary>
        /// Builds a lookup from each node ID to the full set of its ancestor node IDs. Returns null if the graph has unsatisfied or circular dependencies.
        /// </summary>
        public Dictionary<T, HashSet<T>>? GetAncestorNodesLookup()
        {
            return m_TransitiveReducer.GetAncestorNodesLookup();
        }

        /// <summary>
        /// Performs transitive reduction, removing all redundant edges. Returns false if it cannot be performed.
        /// </summary>
        public bool TransitiveReduction()
        {
            return m_TransitiveReducer.ReduceGraph();
        }

        /// <summary>
        /// Redirects redundant edges; a documented no-op for vertex graphs.
        /// </summary>
        public bool RedirectEdges()
        {
            // Edges should not need to be redirected in a vertex graph.
            return true;
        }

        /// <summary>
        /// Removes transitively-implied edges; a documented no-op for vertex graphs.
        /// </summary>
        public bool RemoveRedundantEdges()
        {
            // All redundant edges should have been removed by other methods.
            return true;
        }

        /// <summary>
        /// Runs the edge clean-up sequence (redirection then redundant-edge removal).
        /// </summary>
        public bool CleanUpEdges()
        {
            if (!RedirectEdges())
            {
                return false;
            }

            if (!RemoveRedundantEdges())
            {
                return false;
            }

            return true;
        }

        // Exposed as public for direct testing of the forward/backward flow steps separately.
        /// <summary>
        /// Clears the computed critical-path values from all activities and events.
        /// </summary>
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
        /// <summary>
        /// Runs the forward (earliest times) critical-path pass.
        /// </summary>
        public bool CalculateCriticalPathForwardFlow()
        {
            if (!AllDependenciesSatisfied)
            {
                return false;
            }
            if (FindInvalidPreCompilationConstraints().Count != 0)
            {
                return false;
            }
            return m_CriticalPathEngine.CalculateCriticalPathForwardFlow(
                m_State,
                new List<IInvalidConstraint<T>>(),
                ShuffleProcessingOrder);
        }

        // Returns bool (vs throwing) so tests can assert the return value directly.
        /// <summary>
        /// Runs the backward (latest times and slack) critical-path pass.
        /// </summary>
        public bool CalculateCriticalPathBackwardFlow()
        {
            if (!AllDependenciesSatisfied)
            {
                return false;
            }
            if (FindInvalidPreCompilationConstraints().Count != 0)
            {
                return false;
            }
            return m_CriticalPathEngine.CalculateCriticalPathBackwardFlow(
                m_State,
                new List<IInvalidConstraint<T>>(),
                ShuffleProcessingOrder);
        }

        // Exposes the priority list calculation used internally by CalculateResourceSchedulesByPriorityList.
        /// <summary>
        /// Returns the activity IDs in scheduling priority order (most critical first).
        /// </summary>
        public List<T> CalculateCriticalPathPriorityList()
        {
            var tmpGraphBuilder = (VertexGraphBuilder<T, TResourceId, TWorkStreamId, TActivity>)CloneObject();
            return CalculateCriticalPathPriorityList(tmpGraphBuilder);
        }

        /// <summary>
        /// Calculates the critical path across the whole graph.
        /// </summary>
        public void CalculateCriticalPath()
        {
            if (!RemoveRedundantEdges())
            {
                throw new InvalidOperationException(Properties.Resources.Message_CannotRemoveRedundantEdges);
            }

            ClearCriticalPathVariables();

            List<IInvalidConstraint<T>> constraints = AllDependenciesSatisfied
                ? FindInvalidPreCompilationConstraints()
                : new List<IInvalidConstraint<T>>();

            if (!m_CriticalPathEngine.CalculateCriticalPathForwardFlow(m_State, constraints, ShuffleProcessingOrder))
            {
                throw new InvalidOperationException(Properties.Resources.Message_CannotCalculateCriticalPathForwardFlow);
            }

            if (!m_CriticalPathEngine.CalculateCriticalPathBackwardFlow(m_State, constraints, ShuffleProcessingOrder))
            {
                throw new InvalidOperationException(Properties.Resources.Message_CannotCalculateCriticalPathBackwardFlow);
            }

            if (!RedirectEdges())
            {
                throw new InvalidOperationException(Properties.Resources.Message_CannotPerformEdgeRedirection);
            }
        }

        /// <summary>
        /// Fills in critical-path values for isolated activities, which the flow passes do not reach.
        /// </summary>
        public bool BackFillIsolatedNodes()
        {
            List<IInvalidConstraint<T>> constraints = AllDependenciesSatisfied
                ? FindInvalidPreCompilationConstraints()
                : new List<IInvalidConstraint<T>>();
            return m_CriticalPathEngine.BackFillIsolatedNodes(m_State, constraints);
        }

        // Exposes the injected resource scheduling engine so the compiler can run the
        // surrounding scheduling pipeline (synthetic resources, aligned rebuild, etc.)
        // through the same abstraction.
        internal IResourceSchedulingEngine<T, TResourceId, TWorkStreamId> ResourceSchedulingEngine => m_ResourceSchedulingEngine;

        /// <summary>
        /// Schedules the activities onto the given resources in priority order and returns the per-resource schedules.
        /// </summary>
        public List<IResourceSchedule<T, TResourceId, TWorkStreamId>> CalculateResourceSchedulesByPriorityList(
            List<IResource<TResourceId, TWorkStreamId>> resources)
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
                return new List<IResourceSchedule<T, TResourceId, TWorkStreamId>>();
            }

            // If resources are 0, assume infinite.
            bool infiniteResources = resources.Count == 0;

            // Filter out inactive resources.
            List<IResource<TResourceId, TWorkStreamId>> filteredResources = resources.Where(x => !x.IsInactive).ToList();

            // If resources are limited, check to make sure all activities can be accepted.
            if (!infiniteResources)
            {
                ValidateActivitiesAgainstResources(filteredResources);
            }

            var tmpGraphBuilder = (VertexGraphBuilder<T, TResourceId, TWorkStreamId, TActivity>)CloneObject();

            // Use a separate clone for the priority list calculation so that tmpGraphBuilder retains
            // original activity durations for the scheduling loop below.
            List<T> priorityList = CalculateCriticalPathPriorityList(
                (VertexGraphBuilder<T, TResourceId, TWorkStreamId, TActivity>)tmpGraphBuilder.CloneObject());

            return m_ResourceSchedulingEngine.CalculateResourceSchedules(
                priorityList,
                filteredResources,
                infiniteResources,
                tmpGraphBuilder)
                .ToList();
        }

        #region IResourceSchedulingGraph

        IActivity<T, TResourceId, TWorkStreamId> IResourceSchedulingGraph<T, TResourceId, TWorkStreamId>.Activity(T id) =>
            Activity(id);

        List<T> IResourceSchedulingGraph<T, TResourceId, TWorkStreamId>.StrongActivityDependencyIds(T id) =>
            StrongActivityDependencyIds(id);

        List<IActivity<T, TResourceId, TWorkStreamId>> IResourceSchedulingGraph<T, TResourceId, TWorkStreamId>.CloneActivities() =>
            Activities.Select(x => (IActivity<T, TResourceId, TWorkStreamId>)x.CloneObject()).ToList();

        #endregion

        private void ValidateActivitiesAgainstResources(List<IResource<TResourceId, TWorkStreamId>> filteredResources)
        {
            var unavailableResourcesSet = new List<IUnavailableResources<T, TResourceId>>();

            foreach (TActivity activity in Activities)
            {
                if (activity.TargetResources.Count == 0)
                {
                    continue;
                }

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

            if (unavailableResourcesSet.Count != 0)
            {
                throw new InvalidOperationException(Properties.Resources.Message_AtLeastOneOfSpecifiedTargetResourcesAreNotAvailableInResourcesProvided);
            }

            bool allResourcesAreExplicitTargets = filteredResources.All(x => x.IsExplicitTarget);
            bool atLeastOneActivityRequiresNonExplicitTargetResource = Activities.Any(x => !x.IsDummy && x.TargetResources.Count == 0);
            if (allResourcesAreExplicitTargets && atLeastOneActivityRequiresNonExplicitTargetResource)
            {
                throw new InvalidOperationException(Properties.Resources.Message_AtLeastOneActivityRequiresNonExplicitTargetResourceButAllProvidedResourcesAreExplicitTargets);
            }
        }

        private static List<T> CalculateCriticalPathPriorityList(VertexGraphBuilder<T, TResourceId, TWorkStreamId, TActivity> graphBuilder)
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

                // Get the critical path in order of earliest start time.
                int minFloat = graphBuilder.Activities
                    .Where(x => !x.IsDummy && x.TotalSlack.HasValue)
                    .Select(x => x.TotalSlack!.Value)
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
                    // Set the processed activity to dummy.
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

        /// <summary>
        /// Exports the graph structure (cloned edges and nodes). Throws <see cref="InvalidOperationException"/> if the graph cannot be cleaned up.
        /// </summary>
        public Graph<T, IEvent<T>, TActivity> ToGraph()
        {
            bool edgesCleanedUp = CleanUpEdges();
            if (!edgesCleanedUp)
            {
                // Throw rather than silently return null: a graph that cannot be
                // cleaned up cannot be faithfully exported.
                throw new InvalidOperationException(Properties.Resources.Message_UnableToRemoveUnnecessaryEdges);
            }
            return new Graph<T, IEvent<T>, TActivity>(
                m_State.Edges.Select(x => (Edge<T, IEvent<T>>)x.CloneObject()),
                m_State.Nodes.Select(x => (Node<T, TActivity>)x.CloneObject()));
        }

        /// <summary>
        /// Clears the graph and returns the builder to its initial state.
        /// </summary>
        public void Reset()
        {
            m_State.Clear();
        }

        // Sets the compiled and planning dependencies for an activity, reconciling them
        // with any existing resource dependencies already wired into the graph.
        /// <summary>
        /// Replaces an activity's compiled and planning dependencies.
        /// </summary>
        public bool SetActivityDependencies(T activityId, HashSet<T> dependencies, HashSet<T> planningDependencies)
        {
            if (dependencies is null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }
            if (planningDependencies is null)
            {
                throw new ArgumentNullException(nameof(planningDependencies));
            }
            // O(1) node-key lookup instead of an O(A) scan over ActivityIds.
            if (!m_State.ContainsNode(activityId))
            {
                return false;
            }

            TActivity activityObj = Activity(activityId);

            // Cast to IDependentActivity to access ResourceDependencies - only valid for
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
                foreach (T dependencyId in toBeRemovedFromCompiledDependencies)
                {
                    dependentActivity.Dependencies.Remove(dependencyId);
                }

                IList<T> toBeRemovedFromPlanningDependencies = resourceAndPlanningDependencies.Except(planningDependencies).ToList();
                foreach (T dependencyId in toBeRemovedFromPlanningDependencies)
                {
                    dependentActivity.PlanningDependencies.Remove(dependencyId);
                }

                List<T> updatedDependencies = dependentActivity.Dependencies.Union(dependentActivity.PlanningDependencies).Union(dependentActivity.ResourceDependencies).ToList();
                IList<T> currentDependencies = ActivityDependencyIds(activityId);
                var toBeRemoved = new HashSet<T>(currentDependencies.Except(updatedDependencies).Union(toBeRemovedFromCompiledDependencies).Union(toBeRemovedFromPlanningDependencies));
                successfullyRemoved &= RemoveActivityDependencies(activityId, toBeRemoved);
            }

            // Resource: 1, Core: 0, New: 1
            {
                var toBeAddedToCompiledDependencies = resourceNotCompiledDependencies.Intersect(dependencies);
                foreach (T dependencyId in toBeAddedToCompiledDependencies)
                {
                    dependentActivity.Dependencies.Add(dependencyId);
                }

                var toBeAddedToPlanningDependencies = resourceNotPlanningDependencies.Intersect(planningDependencies);
                foreach (T dependencyId in toBeAddedToPlanningDependencies)
                {
                    dependentActivity.PlanningDependencies.Add(dependencyId);
                }

                List<T> updatedDependencies = dependentActivity.Dependencies.Union(dependentActivity.PlanningDependencies).Union(dependentActivity.ResourceDependencies).ToList();
                IList<T> currentDependencies = ActivityDependencyIds(activityId);
                var toBeAdded = new HashSet<T>(updatedDependencies.Except(currentDependencies).Union(toBeAddedToCompiledDependencies).Union(toBeAddedToPlanningDependencies));
                successfullyAdded &= AddActivityDependencies(activityId, toBeAdded);
            }

            // Resource: 0, Core: 1, New: 0
            {
                var toBeRemovedFromCompiledDependencies = compiledNotResourceDependencies.Except(dependencies);
                foreach (T dependencyId in toBeRemovedFromCompiledDependencies)
                {
                    dependentActivity.Dependencies.Remove(dependencyId);
                }

                var toBeRemovedFromPlanningDependencies = planningNotResourceDependencies.Except(planningDependencies);
                foreach (T dependencyId in toBeRemovedFromPlanningDependencies)
                {
                    dependentActivity.PlanningDependencies.Remove(dependencyId);
                }

                List<T> updatedDependencies = dependentActivity.Dependencies.Union(dependentActivity.PlanningDependencies).Union(dependentActivity.ResourceDependencies).ToList();
                IList<T> currentDependencies = ActivityDependencyIds(activityId);
                var toBeRemoved = new HashSet<T>(currentDependencies.Except(updatedDependencies).Union(toBeRemovedFromCompiledDependencies).Union(toBeRemovedFromPlanningDependencies));
                successfullyRemoved &= RemoveActivityDependencies(activityId, toBeRemoved);
            }

            // Resource: 0, Core: 0, New: X
            {
                var toBeAddedToCompiledDependencies = dependencies.Except(resourceOrCompiledDependencies);
                foreach (T dependencyId in toBeAddedToCompiledDependencies)
                {
                    dependentActivity.Dependencies.Add(dependencyId);
                }

                var toBeAddedToPlanningDependencies = planningDependencies.Except(resourceOrPlanningDependencies);
                foreach (T dependencyId in toBeAddedToPlanningDependencies)
                {
                    dependentActivity.PlanningDependencies.Add(dependencyId);
                }

                List<T> updatedDependencies = dependentActivity.Dependencies.Union(dependentActivity.PlanningDependencies).Union(dependentActivity.ResourceDependencies).ToList();
                IList<T> currentDependencies = ActivityDependencyIds(activityId);
                var toBeAdded = new HashSet<T>(updatedDependencies.Except(currentDependencies).Union(toBeAddedToCompiledDependencies).Union(toBeAddedToPlanningDependencies));
                successfullyAdded &= AddActivityDependencies(activityId, toBeAdded);
            }

            return successfullyRemoved && successfullyAdded;
        }

        // Strips resource-only dependencies and clears resource allocation state before a compile pass.
        /// <summary>
        /// Clears allocated-resource state from the given activities ahead of a new scheduling pass.
        /// </summary>
        public void ResetResourceState(List<TActivity> activities)
        {
            foreach (TActivity activity in activities)
            {
                if (!(activity is IDependentActivity<T, TResourceId, TWorkStreamId> dependentActivity))
                {
                    continue;
                }
                IEnumerable<T> coreDependencies = dependentActivity.Dependencies.Union(dependentActivity.PlanningDependencies);
                RemoveActivityDependencies(activity.Id, new HashSet<T>(dependentActivity.ResourceDependencies.Except(coreDependencies)));
                dependentActivity.ResourceDependencies.Clear();
                dependentActivity.AllocatedToResources.Clear();
            }
        }

        // Wires resource dependencies into the graph from the finished schedule.
        /// <summary>
        /// Wires resource dependencies into the graph from the finished schedules.
        /// </summary>
        public void AssignResourceDependencies(List<IResourceSchedule<T, TResourceId, TWorkStreamId>> resourceSchedules)
        {
            foreach (IResourceSchedule<T, TResourceId, TWorkStreamId> schedule in resourceSchedules)
            {
                IResource<TResourceId, TWorkStreamId>? resource = schedule.Resource;
                T previousId = default;
                bool first = true;

                foreach (IScheduledActivity<T> scheduledActivity in schedule.ScheduledActivities.OrderBy(x => x.StartTime))
                {
                    T currentId = scheduledActivity.Id;
                    TActivity activityObj = Activity(currentId);
                    if (!(activityObj is IDependentActivity<T, TResourceId, TWorkStreamId> dependentActivity))
                    {
                        continue;
                    }

                    if (resource != null)
                    {
                        dependentActivity.AllocatedToResources.Add(resource.Id);
                    }

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
        /// <summary>
        /// Removes dependencies that exist only because of resource allocation.
        /// </summary>
        public void RemoveResourceOnlyDependencies(List<TActivity> activities)
        {
            foreach (TActivity activity in activities)
            {
                if (!(activity is IDependentActivity<T, TResourceId, TWorkStreamId> dependentActivity))
                {
                    continue;
                }
                IEnumerable<T> coreDependencies = dependentActivity.Dependencies.Union(dependentActivity.PlanningDependencies);
                RemoveActivityDependencies(activity.Id, new HashSet<T>(dependentActivity.ResourceDependencies.Except(coreDependencies)));
            }
        }

        // Recomputes each activity's Successors set from the current graph structure.
        /// <summary>
        /// Populates each activity's successors from the current dependencies.
        /// </summary>
        public void UpdateActivitySuccessors(List<TActivity> activities)
        {
            foreach (TActivity activity in activities)
            {
                if (!(activity is IDependentActivity<T, TResourceId, TWorkStreamId> dependentActivity))
                {
                    continue;
                }
                dependentActivity.Successors.Clear();
                Node<T, TActivity> node = Node(activity.Id);
                if (node.NodeType != NodeType.Start && node.NodeType != NodeType.Normal)
                {
                    continue;
                }
                IEnumerable<T> successorNodeIds = node.OutgoingEdges.Select(EdgeHeadNode).Select(x => x.Id);
                dependentActivity.Successors.UnionWith(successorNodeIds);
            }
        }

        // Checks all pre-compilation conditions and appends any errors found.
        // Accepts filteredResources and infiniteResources so the compiler delegates
        // resource-flag computation here rather than inlining it.
        /// <summary>
        /// Checks all pre-compilation conditions and appends any errors found.
        /// </summary>
        public void AddPreCompilationErrors(
            List<GraphCompilationError> errors,
            List<IResource<TResourceId, TWorkStreamId>> filteredResources,
            bool infiniteResources)
        {
            List<TActivity> activities = Activities.ToList();
            List<T> invalidDependencies = InvalidDependencies.ToList();
            List<ICircularDependency<T>> circularDependencies = FindStrongCircularDependencies();
            List<IInvalidConstraint<T>> invalidPrecompilationConstraints = FindInvalidPreCompilationConstraints();

            // P0010
            if (invalidDependencies.Count != 0)
            {
                List<IDependentActivity<T, TResourceId, TWorkStreamId>> dependentActivities =
                    activities.OfType<IDependentActivity<T, TResourceId, TWorkStreamId>>().ToList();
                errors.Add(new GraphCompilationError(GraphCompilationErrorCode.P0010,
                    GraphCompilationErrorFormatter<T, TResourceId, TWorkStreamId, IDependentActivity<T, TResourceId, TWorkStreamId>>
                        .BuildInvalidDependenciesErrorMessage(invalidDependencies, dependentActivities)));
            }

            // P0020
            if (circularDependencies.Count != 0)
            {
                errors.Add(new GraphCompilationError(GraphCompilationErrorCode.P0020,
                    GraphCompilationErrorFormatter<T, TResourceId, TWorkStreamId, IDependentActivity<T, TResourceId, TWorkStreamId>>
                        .BuildCircularDependenciesErrorMessage(circularDependencies)));
            }

            // P0030
            if (invalidPrecompilationConstraints.Count != 0)
            {
                errors.Add(new GraphCompilationError(GraphCompilationErrorCode.P0030,
                    GraphCompilationErrorFormatter<T, TResourceId, TWorkStreamId, IDependentActivity<T, TResourceId, TWorkStreamId>>
                        .BuildInvalidConstraintsErrorMessage(invalidPrecompilationConstraints)));
            }

            // P0040
            bool allResourcesExplicitButNotAllActivitiesTargeted =
                !infiniteResources
                && filteredResources.All(x => x.IsExplicitTarget)
                && Activities.Any(x => !x.IsDummy && x.TargetResources.Count == 0);
            if (allResourcesExplicitButNotAllActivitiesTargeted)
            {
                errors.Add(new GraphCompilationError(GraphCompilationErrorCode.P0040,
                    $@"{Properties.Resources.Message_AllResourcesExplicitTargetsNotAllActivitiesTargeted}{Environment.NewLine}"));
            }

            // P0050
            if (!CleanUpEdges())
            {
                errors.Add(new GraphCompilationError(GraphCompilationErrorCode.P0050,
                    $@"{Properties.Resources.Message_UnableToRemoveUnnecessaryEdges}{Environment.NewLine}"));
            }

            // Check if any activities are obliged to use only explicit target resources
            // that are unavailable.
            List<IUnavailableResources<T, TResourceId>> unavailableResourcesSet =
                infiniteResources
                ? new List<IUnavailableResources<T, TResourceId>>()
                : m_ResourceSchedulingEngine.GatherUnavailableResources(
                    activities.Cast<IActivity<T, TResourceId, TWorkStreamId>>().ToList(), filteredResources)
                .ToList();
            // P0060
            if (unavailableResourcesSet.Count != 0)
            {
                errors.Add(new GraphCompilationError(GraphCompilationErrorCode.P0060,
                    GraphCompilationErrorFormatter<T, TResourceId, TWorkStreamId, IDependentActivity<T, TResourceId, TWorkStreamId>>
                        .BuildUnavailableResourcesErrorMessage(unavailableResourcesSet)));
            }
        }

        // Appends any post-compilation constraint errors to the error list.
        /// <summary>
        /// Appends any post-compilation constraint errors to the error list.
        /// </summary>
        public void AddPostCompilationErrors(List<GraphCompilationError> errors)
        {
            List<IInvalidConstraint<T>> invalidPostcompilationConstraints = FindInvalidPostCompilationConstraints();
            // C0010
            if (invalidPostcompilationConstraints.Count != 0)
            {
                errors.Add(new GraphCompilationError(
                    GraphCompilationErrorCode.C0010,
                    GraphCompilationErrorFormatter<T, TResourceId, TWorkStreamId, IDependentActivity<T, TResourceId, TWorkStreamId>>
                        .BuildInvalidConstraintsErrorMessage(invalidPostcompilationConstraints)));
            }
        }

        #endregion

        #region Private Methods

        private List<ICircularDependency<T>> FindStronglyConnectedComponents()
        {
            return m_SccFinder.FindStronglyConnectedComponents(m_State, ignoreDummies: false);
        }

        private ITransitiveReducer<T> CreateTransitiveReducer()
        {
            return m_TransitiveReducerFactory.Create(m_SccFinder, m_State);
        }

        private void ResolveUnsatisfiedSuccessorActivities(T activityId)
        {
            // Check to make sure the node really exists.
            if (!m_State.TryGetNode(activityId, out Node<T, TActivity> dependencyNode))
            {
                return;
            }

            // Check to see if any existing activities were expecting this activity
            // as a dependency. If so, then hook their nodes to this activity with an edge.
            if (m_State.TryGetUnsatisfiedSuccessors(activityId, out HashSet<Node<T, TActivity>> unsatisfiedSuccessorNodes))
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
                    T edgeId = m_EdgeIdGenerator.Generate();
                    var edge = new Edge<T, IEvent<T>>(m_EventGenerator.Generate(edgeId));
                    dependencyNode.OutgoingEdges.Add(edgeId);
                    m_State.SetEdgeTailNode(edgeId, dependencyNode);
                    successorNode.IncomingEdges.Add(edgeId);
                    m_State.SetEdgeHeadNode(edgeId, successorNode);
                    m_State.AddEdge(edge);
                }
                m_State.RemoveUnsatisfiedSuccessors(activityId);
            }
        }

        private void RemoveUnsatisfiedSuccessorActivity(T activityId)
        {
            // Check to make sure the node really exists.
            if (!m_State.TryGetNode(activityId, out Node<T, TActivity> node))
            {
                return;
            }

            // If the activity was an unsatisfied successor, then remove it from the lookup.
            if (node.NodeType == NodeType.End || node.NodeType == NodeType.Normal)
            {
                m_State.RemoveActivityFromAllUnsatisfiedSuccessors(activityId);
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
                m_State.RemoveActivityFromUnsatisfiedSuccessor(dependencyId, activityId);
            }
        }

        #endregion

        #region ICloneObject

        /// <inheritdoc/>
        public virtual object CloneObject()
        {
            Graph<T, IEvent<T>, TActivity> vertexGraphCopy = ToGraph();
            T minEdgeId = vertexGraphCopy.Edges.Select(x => x.Id).DefaultIfEmpty().Min();
            minEdgeId = minEdgeId.Previous();
            // Preserve the injected (stateless) engines and factories on the clone;
            // only the id generator is recreated, since it carries a per-graph counter.
            return new VertexGraphBuilder<T, TResourceId, TWorkStreamId, TActivity>(
                vertexGraphCopy,
                new VertexGraphBuilderEngines<T, TResourceId, TWorkStreamId, TActivity>
                {
                    EdgeIdGenerator = new PreviousIdGenerator<T>(minEdgeId),
                    EventGenerator = m_EventGenerator,
                    SccFinder = m_SccFinder,
                    CriticalPathEngine = m_CriticalPathEngine,
                    ResourceSchedulingEngine = m_ResourceSchedulingEngine,
                    TransitiveReducerFactory = m_TransitiveReducerFactory,
                });
        }

        #endregion
    }
}
