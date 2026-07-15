using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    // Builder for Activity-on-Arrow graphs. Owns all graph state directly,
    // encapsulated in a single ArrowGraphState instance that is shared with the
    // injected engines (SCC finder, CPM engine, dummy-edge orchestrator, transitive
    // reducer). The public constructors wire up default engine instances; the
    // internal constructors accept injected engines for testability.
    /// <summary>
    /// Builds and maintains an Activity-on-Arrow graph (activities on edges, events on nodes): dynamic dependency resolution via dummy edges, transitive reduction and critical-path calculation. Prefer driving it through <see cref="ArrowGraphCompiler{T, TResourceId, TWorkStreamId, TDependentActivity}"/>.
    /// </summary>
    public class ArrowGraphBuilder<T, TResourceId, TWorkStreamId, TActivity>
        : ICloneObject, IResourceSchedulingGraph<T, TResourceId, TWorkStreamId>
        where TActivity : class, IActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        #region Fields

        private static readonly IActivityGenerator<T, TResourceId, TWorkStreamId, TActivity> s_DefaultDummyActivityGenerator = new DummyActivityGenerator<T, TResourceId, TWorkStreamId, TActivity>();
        private static readonly IEventGenerator<T> s_DefaultEventGenerator = new EventGenerator<T>();

        private readonly IIdGenerator<T> m_EdgeIdGenerator;
        private readonly IIdGenerator<T> m_NodeIdGenerator;
        private readonly IActivityGenerator<T, TResourceId, TWorkStreamId, TActivity> m_DummyActivityGenerator;
        private readonly IEventGenerator<T> m_EventGenerator;

        private readonly IArrowStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity> m_SccFinder;
        private readonly IArrowCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity> m_CriticalPathEngine;
        private readonly IResourceSchedulingEngine<T, TResourceId, TWorkStreamId> m_ResourceSchedulingEngine;
        private readonly ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> m_State;
        // Assigned in Initialize()/graph assimilation, which every constructor calls.
        private IDummyEdgeOrchestrator<T, TResourceId, TWorkStreamId, TActivity> m_DummyEdgeOrchestrator = null!;
        private ITransitiveReducer<T> m_TransitiveReducer = null!;
        // Default factories; overwritten by the engines-bundle constructors.
        private readonly IDummyEdgeOrchestratorFactory<T, TResourceId, TWorkStreamId, TActivity> m_DummyEdgeOrchestratorFactory =
            new DummyEdgeOrchestratorFactory<T, TResourceId, TWorkStreamId, TActivity>();
        private readonly IArrowTransitiveReducerFactory<T, TResourceId, TWorkStreamId, TActivity> m_TransitiveReducerFactory =
            new ArrowTransitiveReducerFactory<T, TResourceId, TWorkStreamId, TActivity>();

        #endregion

        #region Ctors

        // Public constructor - stable API surface. Wires up default engine instances.
        /// <summary>
        /// Creates a builder with default engines, using the given edge (activity) and node (event) ID generators.
        /// </summary>
        public ArrowGraphBuilder(
            IIdGenerator<T> edgeIdGenerator,
            IIdGenerator<T> nodeIdGenerator)
            : this(
                  edgeIdGenerator,
                  nodeIdGenerator,
                  s_DefaultDummyActivityGenerator,
                  s_DefaultEventGenerator,
                  new ArrowTarjanStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity>(),
                  new ArrowCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity>(),
                  new PriorityListResourceScheduler<T, TResourceId, TWorkStreamId>())
        {
        }

        // Engines-bundle constructor - every engine/factory defaults to the standard
        // implementation; set only the bundle properties to customise. Additions to
        // the bundle do not break this signature.
        /// <summary>
        /// Creates a builder from an engines bundle; every bundle property defaults to the standard implementation.
        /// </summary>
        public ArrowGraphBuilder(ArrowGraphBuilderEngines<T, TResourceId, TWorkStreamId, TActivity> engines)
            : this(
                  (engines ?? throw new ArgumentNullException(nameof(engines))).EdgeIdGenerator,
                  engines.NodeIdGenerator,
                  engines.DummyActivityGenerator,
                  engines.EventGenerator,
                  engines.SccFinder,
                  engines.CriticalPathEngine,
                  engines.ResourceSchedulingEngine)
        {
            m_DummyEdgeOrchestratorFactory = engines.DummyEdgeOrchestratorFactory ?? throw new ArgumentNullException(nameof(engines));
            m_TransitiveReducerFactory = engines.TransitiveReducerFactory ?? throw new ArgumentNullException(nameof(engines));
            m_DummyEdgeOrchestrator = CreateOrchestrator();
            m_TransitiveReducer = CreateTransitiveReducer();
        }

        // Engines-bundle graph-loading constructor.
        /// <summary>
        /// Creates a builder by assimilating an existing graph, from an engines bundle.
        /// </summary>
        public ArrowGraphBuilder(
            Graph<T, TActivity, IEvent<T>> graph,
            ArrowGraphBuilderEngines<T, TResourceId, TWorkStreamId, TActivity> engines)
            : this(
                  graph,
                  (engines ?? throw new ArgumentNullException(nameof(engines))).EdgeIdGenerator,
                  engines.NodeIdGenerator,
                  engines.DummyActivityGenerator,
                  engines.EventGenerator,
                  engines.SccFinder,
                  engines.CriticalPathEngine,
                  engines.ResourceSchedulingEngine)
        {
            m_DummyEdgeOrchestratorFactory = engines.DummyEdgeOrchestratorFactory ?? throw new ArgumentNullException(nameof(engines));
            m_TransitiveReducerFactory = engines.TransitiveReducerFactory ?? throw new ArgumentNullException(nameof(engines));
            m_DummyEdgeOrchestrator = CreateOrchestrator();
            m_TransitiveReducer = CreateTransitiveReducer();
        }

        // Engine-injecting constructor - supply custom engines + dummy/event generators.
        /// <summary>
        /// Creates a builder with custom engines and generators.
        /// </summary>
        public ArrowGraphBuilder(
            IIdGenerator<T> edgeIdGenerator,
            IIdGenerator<T> nodeIdGenerator,
            IActivityGenerator<T, TResourceId, TWorkStreamId, TActivity> dummyActivityGenerator,
            IEventGenerator<T> eventGenerator,
            IArrowStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity> sccFinder,
            IArrowCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity> criticalPathEngine,
            IResourceSchedulingEngine<T, TResourceId, TWorkStreamId> resourceSchedulingEngine)
        {
            m_EdgeIdGenerator = edgeIdGenerator ?? throw new ArgumentNullException(nameof(edgeIdGenerator));
            m_NodeIdGenerator = nodeIdGenerator ?? throw new ArgumentNullException(nameof(nodeIdGenerator));
            m_DummyActivityGenerator = dummyActivityGenerator ?? throw new ArgumentNullException(nameof(dummyActivityGenerator));
            m_EventGenerator = eventGenerator ?? throw new ArgumentNullException(nameof(eventGenerator));
            m_SccFinder = sccFinder ?? throw new ArgumentNullException(nameof(sccFinder));
            m_CriticalPathEngine = criticalPathEngine ?? throw new ArgumentNullException(nameof(criticalPathEngine));
            m_ResourceSchedulingEngine = resourceSchedulingEngine ?? throw new ArgumentNullException(nameof(resourceSchedulingEngine));

            m_State = new ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity>();
            ShuffleProcessingOrder = false;
            Initialize();
        }

        // Public graph-loading constructor (from existing Graph<T, TActivity, IEvent<T>>).
        /// <summary>
        /// Creates a builder by assimilating an existing graph, with default engines.
        /// </summary>
        public ArrowGraphBuilder(
            Graph<T, TActivity, IEvent<T>> graph,
            IIdGenerator<T> edgeIdGenerator,
            IIdGenerator<T> nodeIdGenerator)
            : this(
                  graph,
                  edgeIdGenerator,
                  nodeIdGenerator,
                  s_DefaultDummyActivityGenerator,
                  s_DefaultEventGenerator,
                  new ArrowTarjanStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity>(),
                  new ArrowCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity>(),
                  new PriorityListResourceScheduler<T, TResourceId, TWorkStreamId>())
        {
        }

        // Engine-injecting graph-loading constructor.
        /// <summary>
        /// Creates a builder by assimilating an existing graph, with custom engines and generators.
        /// </summary>
        public ArrowGraphBuilder(
            Graph<T, TActivity, IEvent<T>> graph,
            IIdGenerator<T> edgeIdGenerator,
            IIdGenerator<T> nodeIdGenerator,
            IActivityGenerator<T, TResourceId, TWorkStreamId, TActivity> dummyActivityGenerator,
            IEventGenerator<T> eventGenerator,
            IArrowStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity> sccFinder,
            IArrowCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity> criticalPathEngine,
            IResourceSchedulingEngine<T, TResourceId, TWorkStreamId> resourceSchedulingEngine)
        {
            if (graph is null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            m_EdgeIdGenerator = edgeIdGenerator ?? throw new ArgumentNullException(nameof(edgeIdGenerator));
            m_NodeIdGenerator = nodeIdGenerator ?? throw new ArgumentNullException(nameof(nodeIdGenerator));
            m_DummyActivityGenerator = dummyActivityGenerator ?? throw new ArgumentNullException(nameof(dummyActivityGenerator));
            m_EventGenerator = eventGenerator ?? throw new ArgumentNullException(nameof(eventGenerator));
            m_SccFinder = sccFinder ?? throw new ArgumentNullException(nameof(sccFinder));
            m_CriticalPathEngine = criticalPathEngine ?? throw new ArgumentNullException(nameof(criticalPathEngine));
            m_ResourceSchedulingEngine = resourceSchedulingEngine ?? throw new ArgumentNullException(nameof(resourceSchedulingEngine));

            m_State = new ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity>();
            ShuffleProcessingOrder = false;

            foreach (Edge<T, TActivity> edge in graph.Edges)
            {
                m_State.AddEdge(edge);
            }

            foreach (Node<T, IEvent<T>> node in graph.Nodes)
            {
                if (node.NodeType != NodeType.Start && node.NodeType != NodeType.Isolated)
                {
                    foreach (T edgeId in node.IncomingEdges)
                    {
                        m_State.SetEdgeHeadNode(edgeId, node);
                    }
                }
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

            // Check Start and End nodes.
            if (StartNodes.Count() == 1)
            {
                m_State.StartNode = StartNodes.First();
            }
            else
            {
                throw new ArgumentException(Properties.Resources.Message_ArrowGraphContainsMoreThanOneStartNode);
            }
            if (EndNodes.Count() == 1)
            {
                m_State.EndNode = EndNodes.First();
            }
            else
            {
                throw new ArgumentException(Properties.Resources.Message_ArrowGraphContainsMoreThanOneEndNode);
            }

            // Wire up the orchestrator and reducer AFTER the state has been populated.
            m_DummyEdgeOrchestrator = CreateOrchestrator();
            m_TransitiveReducer = CreateTransitiveReducer();
        }

        #endregion

        #region Properties

        /// <summary>
        /// The single start node of the arrow graph.
        /// </summary>
        public Node<T, IEvent<T>> StartNode => m_State.StartNode;

        /// <summary>
        /// The single end node of the arrow graph.
        /// </summary>
        public Node<T, IEvent<T>> EndNode => m_State.EndNode;

        /// <summary>
        /// The nodes with only outgoing edges.
        /// </summary>
        public IEnumerable<Node<T, IEvent<T>>> StartNodes => m_State.StartNodes;

        /// <summary>
        /// The nodes with only incoming edges.
        /// </summary>
        public IEnumerable<Node<T, IEvent<T>>> EndNodes => m_State.EndNodes;

        /// <summary>
        /// The nodes with both incoming and outgoing edges.
        /// </summary>
        public IEnumerable<Node<T, IEvent<T>>> NormalNodes => m_State.NormalNodes;

        /// <summary>
        /// The nodes with no edges at all.
        /// </summary>
        public IEnumerable<Node<T, IEvent<T>>> IsolatedNodes => m_State.IsolatedNodes;

        /// <summary>
        /// The IDs of all edges (activities).
        /// </summary>
        public IEnumerable<T> EdgeIds => m_State.EdgeIds;

        /// <summary>
        /// The IDs of all nodes (events).
        /// </summary>
        public IEnumerable<T> NodeIds => m_State.NodeIds;

        /// <summary>
        /// The activities carried on the edges (including dummy activities).
        /// </summary>
        public IEnumerable<TActivity> Activities => m_State.Activities;

        /// <summary>
        /// The events carried on the nodes.
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
        /// All edges (activities).
        /// </summary>
        public IEnumerable<Edge<T, TActivity>> Edges => m_State.Edges;

        /// <summary>
        /// All nodes (events).
        /// </summary>
        public IEnumerable<Node<T, IEvent<T>>> Nodes => m_State.Nodes;

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
        public TActivity Activity(T key) => m_State.Edge(key).Content;

        /// <summary>
        /// Resolves the event with the given ID.
        /// </summary>
        public IEvent<T> Event(T key) => m_State.Node(key).Content;

        /// <summary>
        /// Resolves the edge with the given ID.
        /// </summary>
        public Edge<T, TActivity> Edge(T key) => m_State.Edge(key);

        /// <summary>
        /// Resolves the node with the given ID.
        /// </summary>
        public Node<T, IEvent<T>> Node(T key) => m_State.Node(key);

        /// <summary>
        /// Resolves the node the given edge points to.
        /// </summary>
        public Node<T, IEvent<T>> EdgeHeadNode(T key) => m_State.EdgeHeadNode(key);

        /// <summary>
        /// Resolves the node the given edge starts from.
        /// </summary>
        public Node<T, IEvent<T>> EdgeTailNode(T key) => m_State.EdgeTailNode(key);

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
            if (m_State.ContainsEdge(activity.Id))
            {
                return false;
            }
            if (dependencies.Contains(activity.Id))
            {
                return false;
            }

            // Create a new edge for the activity.
            var edge = new Edge<T, TActivity>(activity);
            m_State.AddEdge(edge);

            // We expect dependencies at some point.
            if (dependencies.Count != 0)
            {
                // Since we use dummy edges to connect all tail nodes, we can create
                // a new tail node for this edge.
                T tailEventId = m_NodeIdGenerator.Generate();
                var tailNode = new Node<T, IEvent<T>>(m_EventGenerator.Generate(tailEventId));
                tailNode.OutgoingEdges.Add(edge.Id);
                m_State.SetEdgeTailNode(edge.Id, tailNode);
                m_State.AddNode(tailNode);

                // Check which of the expected dependencies currently exist.
                IList<T> existingDependencies = m_State.EdgeIds.Intersect(dependencies).ToList();
                IList<T> nonExistingDependencies = dependencies.Except(existingDependencies).ToList();

                // If any expected dependencies currently exist, then hook up their head
                // node to this edge's tail node with dummy edges.
                foreach (T dependencyId in existingDependencies)
                {
                    Node<T, IEvent<T>> dependencyHeadNode = m_State.EdgeHeadNode(dependencyId);
                    T dummyEdgeId = m_EdgeIdGenerator.Generate();
                    var dummyEdge = new Edge<T, TActivity>(m_DummyActivityGenerator.Generate(dummyEdgeId));
                    tailNode.IncomingEdges.Add(dummyEdgeId);
                    m_State.SetEdgeHeadNode(dummyEdgeId, tailNode);
                    // If the head node of the dependency is the End node, then convert it.
                    if (dependencyHeadNode.NodeType == NodeType.End)
                    {
                        dependencyHeadNode.SetNodeType(NodeType.Normal);
                    }
                    dependencyHeadNode.OutgoingEdges.Add(dummyEdgeId);
                    m_State.SetEdgeTailNode(dummyEdgeId, dependencyHeadNode);
                    m_State.AddEdge(dummyEdge);
                }

                // If any expected dependencies currently do not exist, then record their
                // IDs and add this edge's tail node as an unsatisfied successor.
                foreach (T dependencyId in nonExistingDependencies)
                {
                    m_State.AddUnsatisfiedSuccessor(dependencyId, tailNode);
                }
            }
            else
            {
                // No dependencies, so attach it directly to the start node.
                m_State.StartNode.OutgoingEdges.Add(edge.Id);
                m_State.SetEdgeTailNode(edge.Id, m_State.StartNode);
            }
            ResolveUnsatisfiedSuccessorActivities(edge.Id);
            return true;
        }

        /// <summary>
        /// Adds the given dependencies to an existing activity.
        /// </summary>
        public bool AddActivityDependencies(T activityId, HashSet<T> dependencies)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Removes the activity with the given ID. Returns false if it cannot be removed.
        /// </summary>
        public bool RemoveActivity(T activityId)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Removes the given dependencies from an existing activity.
        /// </summary>
        public bool RemoveActivityDependencies(T activityId, HashSet<T> dependencies)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Removes a dummy activity edge, merging adjacent nodes where possible.
        /// </summary>
        public bool RemoveDummyActivity(T activityId)
        {
            return m_DummyEdgeOrchestrator.RemoveDummyActivity(activityId);
        }

        /// <summary>
        /// Returns the IDs of the activities the given activity currently depends on within the graph.
        /// </summary>
        public List<T> ActivityDependencyIds(T activityId)
        {
            Node<T, IEvent<T>> tailNode = m_State.EdgeTailNode(activityId);
            if (tailNode.NodeType == NodeType.Start || tailNode.NodeType == NodeType.Isolated)
            {
                return new List<T>();
            }
            var output = new List<T>();
            foreach (Edge<T, TActivity> incomingEdge in tailNode.IncomingEdges.Select(x => m_State.Edge(x)))
            {
                output.Add(incomingEdge.Id);
            }
            return output;
        }

        /// <summary>
        /// Returns the strong (resolved) dependency IDs for the given activity ID.
        /// </summary>
        public List<T> StrongActivityDependencyIds(T activityId)
        {
            Node<T, IEvent<T>> tailNode = m_State.EdgeTailNode(activityId);
            if (tailNode.NodeType == NodeType.Start || tailNode.NodeType == NodeType.Isolated)
            {
                return new List<T>();
            }
            // Iterative walk (dummy edges are transparent, real edges terminate the walk)
            // so a long dummy chain cannot overflow the stack. Each node's incoming edges
            // are walked once; the resulting set of real dependency IDs is unaffected
            // (callers use it as a set).
            var output = new List<T>();
            var visitedNodes = new HashSet<T>();
            var stack = new Stack<T>();
            stack.Push(tailNode.Id);
            while (stack.Count != 0)
            {
                T currentNodeId = stack.Pop();
                if (!visitedNodes.Add(currentNodeId))
                {
                    continue;
                }
                Node<T, IEvent<T>> currentNode = m_State.Node(currentNodeId);
                if (currentNode.NodeType == NodeType.Start || currentNode.NodeType == NodeType.Isolated)
                {
                    continue;
                }
                foreach (Edge<T, TActivity> incomingEdge in currentNode.IncomingEdges.Select(x => m_State.Edge(x)))
                {
                    if (incomingEdge.Content.IsDummy)
                    {
                        stack.Push(m_State.EdgeTailNode(incomingEdge.Id).Id);
                    }
                    else
                    {
                        output.Add(incomingEdge.Id);
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
            ConstraintChecker<T, TResourceId, TWorkStreamId>.FindInvalidPostCompilationConstraints(Activities.Cast<IActivity<T, TResourceId, TWorkStreamId>>().ToList());

        /// <summary>
        /// Builds a lookup from each node ID to the full set of its ancestor node IDs. Returns null if the graph has unsatisfied or circular dependencies.
        /// </summary>
        public Dictionary<T, HashSet<T>>? GetAncestorNodesLookup()
        {
            return m_TransitiveReducer.GetAncestorNodesLookup();
        }

        /// <summary>
        /// Performs transitive reduction, removing all redundant dummy edges. Returns false if it cannot be performed.
        /// </summary>
        public bool TransitiveReduction() => m_TransitiveReducer.ReduceGraph();

        /// <summary>
        /// Redirects redundant dummy edges (canonical arrow-graph normalisation).
        /// </summary>
        public bool RedirectEdges() => m_DummyEdgeOrchestrator.RedirectDummyEdges();

        /// <summary>
        /// Removes dummy edges that are transitively implied.
        /// </summary>
        public bool RemoveRedundantEdges() => m_DummyEdgeOrchestrator.RemoveRedundantDummyEdges();

        /// <summary>
        /// Redirects and then removes redundant dummy edges until the graph is minimal.
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

            bool allDependenciesSatisfied = AllDependenciesSatisfied;
            List<IInvalidConstraint<T>> constraints = allDependenciesSatisfied
                ? FindInvalidPreCompilationConstraints() : new List<IInvalidConstraint<T>>();

            if (!m_CriticalPathEngine.CalculateEventEarliestFinishTimes(m_State, constraints, ShuffleProcessingOrder))
            {
                throw new InvalidOperationException(Properties.Resources.Message_CannotCalculateEventEarliestFinishTimes);
            }

            if (!m_CriticalPathEngine.CalculateEventLatestFinishTimes(m_State, constraints, ShuffleProcessingOrder))
            {
                throw new InvalidOperationException(Properties.Resources.Message_CannotCalculateEventLatestFinishTimes);
            }

            if (!m_CriticalPathEngine.CalculateCriticalPathVariables(m_State, constraints))
            {
                throw new InvalidOperationException(Properties.Resources.Message_CannotCalculateCriticalPath);
            }

            if (!RedirectEdges())
            {
                throw new InvalidOperationException(Properties.Resources.Message_CannotPerformEdgeRedirection);
            }
        }

        /// <summary>
        /// Returns the activity IDs in scheduling priority order (most critical first).
        /// </summary>
        public List<T> CalculateCriticalPathPriorityList()
        {
            var tmpGraphBuilder = (ArrowGraphBuilder<T, TResourceId, TWorkStreamId, TActivity>)CloneObject();
            return CalculateCriticalPathPriorityList(tmpGraphBuilder);
        }

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

            bool infiniteResources = resources.Count == 0;
            List<IResource<TResourceId, TWorkStreamId>> filteredResources = resources.Where(x => !x.IsInactive).ToList();

            if (!infiniteResources)
            {
                ValidateActivitiesAgainstResources(filteredResources);
            }

            var tmpGraphBuilder = (ArrowGraphBuilder<T, TResourceId, TWorkStreamId, TActivity>)CloneObject();
            List<T> priorityList = CalculateCriticalPathPriorityList(
                (ArrowGraphBuilder<T, TResourceId, TWorkStreamId, TActivity>)tmpGraphBuilder.CloneObject());

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

        /// <summary>
        /// Exports the graph structure (cloned edges and nodes). Throws <see cref="InvalidOperationException"/> if the graph cannot be cleaned up.
        /// </summary>
        public Graph<T, TActivity, IEvent<T>> ToGraph()
        {
            if (!CleanUpEdges())
            {
                // Throw rather than silently return null: a graph that cannot be
                // cleaned up cannot be faithfully exported.
                throw new InvalidOperationException(Properties.Resources.Message_UnableToRemoveUnnecessaryEdges);
            }
            return new Graph<T, TActivity, IEvent<T>>(
                m_State.Edges.Select(x => (Edge<T, TActivity>)x.CloneObject()),
                m_State.Nodes.Select(x => (Node<T, IEvent<T>>)x.CloneObject()));
        }

        /// <summary>
        /// Clears the graph and returns the builder to its initial state.
        /// </summary>
        public void Reset()
        {
            m_State.Clear();
            Initialize();
        }

        #endregion

        #region Private Methods

        private void Initialize()
        {
            T startEventId = m_NodeIdGenerator.Generate();
            var startNode = new Node<T, IEvent<T>>(NodeType.Start, m_EventGenerator.Generate(startEventId, 0, 0));
            m_State.StartNode = startNode;
            m_State.AddNode(startNode);
            T endEventId = m_NodeIdGenerator.Generate();
            var endNode = new Node<T, IEvent<T>>(NodeType.End, m_EventGenerator.Generate(endEventId));
            m_State.EndNode = endNode;
            m_State.AddNode(endNode);
            m_DummyEdgeOrchestrator = CreateOrchestrator();
            m_TransitiveReducer = CreateTransitiveReducer();
        }

        private IDummyEdgeOrchestrator<T, TResourceId, TWorkStreamId, TActivity> CreateOrchestrator()
        {
            return m_DummyEdgeOrchestratorFactory.Create(
                m_EdgeIdGenerator,
                m_DummyActivityGenerator,
                m_SccFinder,
                m_State);
        }

        private ITransitiveReducer<T> CreateTransitiveReducer()
        {
            return m_TransitiveReducerFactory.Create(
                m_DummyEdgeOrchestrator,
                m_SccFinder,
                m_State);
        }

        private void ClearCriticalPathVariables()
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

        private List<ICircularDependency<T>> FindStronglyConnectedComponents()
        {
            return m_SccFinder.FindStronglyConnectedComponents(m_State, ignoreDummies: true);
        }

        private void ResolveUnsatisfiedSuccessorActivities(T activityId)
        {
            // Check to make sure the edge really exists.
            if (!m_State.ContainsEdge(activityId))
            {
                return;
            }

            T headEventId = m_NodeIdGenerator.Generate();
            var headNode = new Node<T, IEvent<T>>(m_EventGenerator.Generate(headEventId));
            headNode.IncomingEdges.Add(activityId);
            m_State.SetEdgeHeadNode(activityId, headNode);
            m_State.AddNode(headNode);

            // Check to see if any existing activities were expecting this activity
            // as a dependency. If so, then then hook up their tail nodes to this
            // activity's head node with a dummy edge.
            if (m_State.TryGetUnsatisfiedSuccessors(activityId, out HashSet<Node<T, IEvent<T>>> unsatisfiedSuccessorTailNodes))
            {
                foreach (Node<T, IEvent<T>> tailNode in unsatisfiedSuccessorTailNodes)
                {
                    m_DummyEdgeOrchestrator.ConnectWithDummyEdge(headNode, tailNode);
                }
                m_State.RemoveUnsatisfiedSuccessors(activityId);
            }
            else
            {
                // No existing activities were expecting this activity as a dependency,
                // so attach it directly to the end node via a dummy.
                m_DummyEdgeOrchestrator.ConnectWithDummyEdge(headNode, m_State.EndNode);
            }
        }

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
                    IEnumerable<TResourceId> unavailableResourceIds = activity.TargetResources.Except(filteredResources.Select(x => x.Id));
                    if (unavailableResourceIds.Any())
                    {
                        unavailableResourcesSet.Add(new UnavailableResources<T, TResourceId>(activity.Id, unavailableResourceIds));
                    }
                }
                else if (activity.TargetResourceOperator == LogicalOperator.OR
                         || activity.TargetResourceOperator == LogicalOperator.ACTIVE_AND)
                {
                    IEnumerable<TResourceId> intersection = activity.TargetResources.Intersect(filteredResources.Select(x => x.Id));
                    if (!intersection.Any())
                    {
                        unavailableResourcesSet.Add(new UnavailableResources<T, TResourceId>(activity.Id, activity.TargetResources));
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

        private static List<T> CalculateCriticalPathPriorityList(ArrowGraphBuilder<T, TResourceId, TWorkStreamId, TActivity> graphBuilder)
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
                    .DefaultIfEmpty().Min();

                IList<T> criticalActivityIds = graphBuilder.Activities
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

        #endregion

        #region ICloneObject

        /// <inheritdoc/>
        public virtual object CloneObject()
        {
            Graph<T, TActivity, IEvent<T>> arrowGraphCopy = ToGraph();
            T minNodeId = arrowGraphCopy.Nodes.Select(x => x.Id).DefaultIfEmpty().Min();
            minNodeId = minNodeId.Previous();
            T minEdgeId = arrowGraphCopy.Edges.Select(x => x.Id).DefaultIfEmpty().Min();
            minEdgeId = minEdgeId.Previous();
            // Preserve the injected (stateless) engines and factories on the clone;
            // only the id generators are recreated, since they carry per-graph counters.
            return new ArrowGraphBuilder<T, TResourceId, TWorkStreamId, TActivity>(
                arrowGraphCopy,
                new ArrowGraphBuilderEngines<T, TResourceId, TWorkStreamId, TActivity>
                {
                    EdgeIdGenerator = new PreviousIdGenerator<T>(minEdgeId),
                    NodeIdGenerator = new PreviousIdGenerator<T>(minNodeId),
                    DummyActivityGenerator = m_DummyActivityGenerator,
                    EventGenerator = m_EventGenerator,
                    SccFinder = m_SccFinder,
                    CriticalPathEngine = m_CriticalPathEngine,
                    ResourceSchedulingEngine = m_ResourceSchedulingEngine,
                    DummyEdgeOrchestratorFactory = m_DummyEdgeOrchestratorFactory,
                    TransitiveReducerFactory = m_TransitiveReducerFactory,
                });
        }

        #endregion
    }
}
