using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    // Sealed builder for Activity-on-Arrow graphs. Owns all graph state directly.
    // Algorithm work is delegated to injected engine instances (SCC finder, CPM engine,
    // dummy-edge orchestrator). The public constructors wire up default engine instances;
    // the internal constructors accept injected engines for testability.
    public sealed class ArrowGraphBuilder<T, TResourceId, TWorkStreamId, TActivity>
        : ICloneObject
        where TActivity : class, IActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        #region Fields

        private static readonly Func<T, IEvent<T>> s_EventGenerator = (id) => new Event<T>(id);
        private static readonly Func<T, int?, int?, IEvent<T>> s_EventGeneratorWithTimes = (id, earliestFinishTime, latestFinishTime) => new Event<T>(id, earliestFinishTime, latestFinishTime);
        private static readonly Func<T, TActivity> s_DefaultDummyActivityGenerator = (id) => new Activity<T, TResourceId, TWorkStreamId>(id, 0, canBeRemoved: true) as TActivity;

        private readonly Func<T> m_EdgeIdGenerator;
        private readonly Func<T> m_NodeIdGenerator;
        private readonly Func<T, TActivity> m_DummyActivityGenerator;

        private readonly IArrowStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity, IEvent<T>> m_SccFinder;
        private readonly IArrowCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity, IEvent<T>> m_CriticalPathEngine;
        private readonly IResourceSchedulingEngine<T, TResourceId, TWorkStreamId> m_ResourceSchedulingEngine;
        private IDummyEdgeOrchestrator<T, TResourceId, TWorkStreamId, TActivity> m_DummyEdgeOrchestrator;
        private ITransitiveReducer<T> m_TransitiveReducer;

        #endregion

        #region Graph State

        private readonly Dictionary<T, Edge<T, TActivity>> m_EdgeLookup;
        private readonly Dictionary<T, Node<T, IEvent<T>>> m_NodeLookup;
        private readonly Dictionary<T, HashSet<Node<T, IEvent<T>>>> m_UnsatisfiedSuccessorsLookup;
        private readonly Dictionary<T, Node<T, IEvent<T>>> m_EdgeHeadNodeLookup;
        private readonly Dictionary<T, Node<T, IEvent<T>>> m_EdgeTailNodeLookup;

        #endregion

        #region Ctors

        // Public constructor — stable API surface. Wires up default engine instances.
        public ArrowGraphBuilder(
            Func<T> edgeIdGenerator,
            Func<T> nodeIdGenerator)
            : this(
                  edgeIdGenerator,
                  nodeIdGenerator,
                  s_DefaultDummyActivityGenerator,
                  new ArrowTarjanStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity, IEvent<T>>(),
                  new ArrowCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity, IEvent<T>>(),
                  new PriorityListResourceScheduler<T, TResourceId, TWorkStreamId>())
        {
        }

        // Internal constructor — accepts all engines + dummy generator for testability.
        internal ArrowGraphBuilder(
            Func<T> edgeIdGenerator,
            Func<T> nodeIdGenerator,
            Func<T, TActivity> dummyActivityGenerator,
            IArrowStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity, IEvent<T>> sccFinder,
            IArrowCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity, IEvent<T>> criticalPathEngine,
            IResourceSchedulingEngine<T, TResourceId, TWorkStreamId> resourceSchedulingEngine)
        {
            m_EdgeIdGenerator = edgeIdGenerator ?? throw new ArgumentNullException(nameof(edgeIdGenerator));
            m_NodeIdGenerator = nodeIdGenerator ?? throw new ArgumentNullException(nameof(nodeIdGenerator));
            m_DummyActivityGenerator = dummyActivityGenerator ?? throw new ArgumentNullException(nameof(dummyActivityGenerator));
            m_SccFinder = sccFinder ?? throw new ArgumentNullException(nameof(sccFinder));
            m_CriticalPathEngine = criticalPathEngine ?? throw new ArgumentNullException(nameof(criticalPathEngine));
            m_ResourceSchedulingEngine = resourceSchedulingEngine ?? throw new ArgumentNullException(nameof(resourceSchedulingEngine));

            m_EdgeLookup = new Dictionary<T, Edge<T, TActivity>>();
            m_NodeLookup = new Dictionary<T, Node<T, IEvent<T>>>();
            m_UnsatisfiedSuccessorsLookup = new Dictionary<T, HashSet<Node<T, IEvent<T>>>>();
            m_EdgeHeadNodeLookup = new Dictionary<T, Node<T, IEvent<T>>>();
            m_EdgeTailNodeLookup = new Dictionary<T, Node<T, IEvent<T>>>();
            WhenTesting = false;
            Initialize();
        }

        // Public graph-loading constructor (from existing Graph<T, TActivity, IEvent<T>>).
        public ArrowGraphBuilder(
            Graph<T, TActivity, IEvent<T>> graph,
            Func<T> edgeIdGenerator,
            Func<T> nodeIdGenerator)
            : this(
                  graph,
                  edgeIdGenerator,
                  nodeIdGenerator,
                  s_DefaultDummyActivityGenerator,
                  new ArrowTarjanStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity, IEvent<T>>(),
                  new ArrowCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity, IEvent<T>>(),
                  new PriorityListResourceScheduler<T, TResourceId, TWorkStreamId>())
        {
        }

        // Internal graph-loading constructor with full engine injection.
        internal ArrowGraphBuilder(
            Graph<T, TActivity, IEvent<T>> graph,
            Func<T> edgeIdGenerator,
            Func<T> nodeIdGenerator,
            Func<T, TActivity> dummyActivityGenerator,
            IArrowStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity, IEvent<T>> sccFinder,
            IArrowCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity, IEvent<T>> criticalPathEngine,
            IResourceSchedulingEngine<T, TResourceId, TWorkStreamId> resourceSchedulingEngine)
        {
            if (graph is null) throw new ArgumentNullException(nameof(graph));

            m_EdgeIdGenerator = edgeIdGenerator ?? throw new ArgumentNullException(nameof(edgeIdGenerator));
            m_NodeIdGenerator = nodeIdGenerator ?? throw new ArgumentNullException(nameof(nodeIdGenerator));
            m_DummyActivityGenerator = dummyActivityGenerator ?? throw new ArgumentNullException(nameof(dummyActivityGenerator));
            m_SccFinder = sccFinder ?? throw new ArgumentNullException(nameof(sccFinder));
            m_CriticalPathEngine = criticalPathEngine ?? throw new ArgumentNullException(nameof(criticalPathEngine));
            m_ResourceSchedulingEngine = resourceSchedulingEngine ?? throw new ArgumentNullException(nameof(resourceSchedulingEngine));

            m_EdgeLookup = new Dictionary<T, Edge<T, TActivity>>();
            m_NodeLookup = new Dictionary<T, Node<T, IEvent<T>>>();
            m_UnsatisfiedSuccessorsLookup = new Dictionary<T, HashSet<Node<T, IEvent<T>>>>();
            m_EdgeHeadNodeLookup = new Dictionary<T, Node<T, IEvent<T>>>();
            m_EdgeTailNodeLookup = new Dictionary<T, Node<T, IEvent<T>>>();
            WhenTesting = false;

            foreach (Edge<T, TActivity> edge in graph.Edges)
                m_EdgeLookup.Add(edge.Id, edge);

            foreach (Node<T, IEvent<T>> node in graph.Nodes)
            {
                if (node.NodeType != NodeType.Start && node.NodeType != NodeType.Isolated)
                    foreach (T edgeId in node.IncomingEdges)
                        m_EdgeHeadNodeLookup.Add(edgeId, node);
                if (node.NodeType != NodeType.End && node.NodeType != NodeType.Isolated)
                    foreach (T edgeId in node.OutgoingEdges)
                        m_EdgeTailNodeLookup.Add(edgeId, node);
                m_NodeLookup.Add(node.Id, node);
            }

            // Check all edges are used.
            if (!m_EdgeLookup.Keys.OrderBy(x => x).SequenceEqual(m_EdgeHeadNodeLookup.Keys.OrderBy(x => x)))
                throw new ArgumentException(Properties.Resources.Message_ListOfEdgeIdsAndEdgesReferencedByHeadNodesDoNotMatch);
            if (!m_EdgeLookup.Keys.OrderBy(x => x).SequenceEqual(m_EdgeTailNodeLookup.Keys.OrderBy(x => x)))
                throw new ArgumentException(Properties.Resources.Message_ListOfEdgeIdsAndEdgesReferencedByTailNodesDoNotMatch);

            // Check all nodes are used.
            IEnumerable<T> edgeNodeLookupIds = m_EdgeHeadNodeLookup.Values.Select(x => x.Id).Union(m_EdgeTailNodeLookup.Values.Select(x => x.Id));
            if (!m_NodeLookup.Values.Where(x => x.NodeType != NodeType.Isolated).Select(x => x.Id).OrderBy(x => x).SequenceEqual(edgeNodeLookupIds.OrderBy(x => x)))
                throw new ArgumentException(Properties.Resources.Message_ListOfNodeIdsAndEdgesReferencedByTailNodesDoNotMatch);

            // Check Start and End nodes.
            if (StartNodes.Count() == 1) StartNode = StartNodes.First();
            else throw new ArgumentException(Properties.Resources.Message_ArrowGraphContainsMoreThanOneStartNode);
            if (EndNodes.Count() == 1) EndNode = EndNodes.First();
            else throw new ArgumentException(Properties.Resources.Message_ArrowGraphContainsMoreThanOneEndNode);

            // Wire up the orchestrator and reducer AFTER the dictionaries are populated.
            m_DummyEdgeOrchestrator = CreateOrchestrator();
            m_TransitiveReducer = CreateTransitiveReducer();
        }

        #endregion

        #region Properties

        public Node<T, IEvent<T>> StartNode { get; private set; }

        public Node<T, IEvent<T>> EndNode { get; private set; }

        public IEnumerable<Node<T, IEvent<T>>> StartNodes =>
            m_NodeLookup.Values.Where(x => x.NodeType == NodeType.Start);

        public IEnumerable<Node<T, IEvent<T>>> EndNodes =>
            m_NodeLookup.Values.Where(x => x.NodeType == NodeType.End);

        public IEnumerable<Node<T, IEvent<T>>> NormalNodes =>
            m_NodeLookup.Values.Where(x => x.NodeType == NodeType.Normal);

        public IEnumerable<Node<T, IEvent<T>>> IsolatedNodes =>
            m_NodeLookup.Values.Where(x => x.NodeType == NodeType.Isolated);

        public IEnumerable<T> EdgeIds => m_EdgeLookup.Keys;

        public IEnumerable<T> NodeIds => m_NodeLookup.Keys;

        public IEnumerable<TActivity> Activities => m_EdgeLookup.Values.Select(x => x.Content);

        public IEnumerable<IEvent<T>> Events => m_NodeLookup.Values.Select(x => x.Content);

        public IEnumerable<T> ActivityIds => Activities.Select(x => x.Id);

        public IEnumerable<T> EventIds => Events.Select(x => x.Id);

        public IEnumerable<Edge<T, TActivity>> Edges => m_EdgeLookup.Values;

        public IEnumerable<Node<T, IEvent<T>>> Nodes => m_NodeLookup.Values;

        public IEnumerable<T> InvalidDependencies => m_UnsatisfiedSuccessorsLookup.Keys;

        public bool AllDependenciesSatisfied => !m_UnsatisfiedSuccessorsLookup.Any();

        public int StartTime =>
            Activities.Select(x => x.EarliestStartTime.GetValueOrDefault()).DefaultIfEmpty().Min();

        public int FinishTime =>
            Activities.Select(x => x.LatestFinishTime.GetValueOrDefault()).DefaultIfEmpty().Max();

        public bool WhenTesting { get; set; }

        #endregion

        #region Public Methods

        public TActivity Activity(T key) => m_EdgeLookup[key].Content;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "No better term available")]
        public IEvent<T> Event(T key) => m_NodeLookup[key].Content;

        public Edge<T, TActivity> Edge(T key)
        {
            m_EdgeLookup.TryGetValue(key, out Edge<T, TActivity> edge);
            return edge;
        }

        public Node<T, IEvent<T>> Node(T key)
        {
            m_NodeLookup.TryGetValue(key, out Node<T, IEvent<T>> node);
            return node;
        }

        public Node<T, IEvent<T>> EdgeHeadNode(T key)
        {
            m_EdgeHeadNodeLookup.TryGetValue(key, out Node<T, IEvent<T>> node);
            return node;
        }

        public Node<T, IEvent<T>> EdgeTailNode(T key)
        {
            m_EdgeTailNodeLookup.TryGetValue(key, out Node<T, IEvent<T>> node);
            return node;
        }

        public bool AddActivity(TActivity activity)
        {
            return AddActivity(activity, new HashSet<T>());
        }

        public bool AddActivity(TActivity activity, HashSet<T> dependencies)
        {
            if (activity == null) throw new ArgumentNullException(nameof(activity));
            if (dependencies is null) throw new ArgumentNullException(nameof(dependencies));
            if (m_EdgeLookup.ContainsKey(activity.Id)) return false;
            if (dependencies.Contains(activity.Id)) return false;

            var edge = new Edge<T, TActivity>(activity);
            m_EdgeLookup.Add(edge.Id, edge);

            if (dependencies.Any())
            {
                T tailEventId = m_NodeIdGenerator();
                var tailNode = new Node<T, IEvent<T>>(s_EventGenerator(tailEventId));
                tailNode.OutgoingEdges.Add(edge.Id);
                m_EdgeTailNodeLookup.Add(edge.Id, tailNode);
                m_NodeLookup.Add(tailNode.Id, tailNode);

                IList<T> existingDependencies = m_EdgeLookup.Keys.Intersect(dependencies).ToList();
                IList<T> nonExistingDependencies = dependencies.Except(existingDependencies).ToList();

                foreach (T dependencyId in existingDependencies)
                {
                    Node<T, IEvent<T>> dependencyHeadNode = m_EdgeHeadNodeLookup[dependencyId];
                    T dummyEdgeId = m_EdgeIdGenerator();
                    var dummyEdge = new Edge<T, TActivity>(m_DummyActivityGenerator(dummyEdgeId));
                    tailNode.IncomingEdges.Add(dummyEdgeId);
                    m_EdgeHeadNodeLookup.Add(dummyEdgeId, tailNode);
                    if (dependencyHeadNode.NodeType == NodeType.End)
                        dependencyHeadNode.SetNodeType(NodeType.Normal);
                    dependencyHeadNode.OutgoingEdges.Add(dummyEdgeId);
                    m_EdgeTailNodeLookup.Add(dummyEdgeId, dependencyHeadNode);
                    m_EdgeLookup.Add(dummyEdge.Id, dummyEdge);
                }

                foreach (T dependencyId in nonExistingDependencies)
                {
                    if (!m_UnsatisfiedSuccessorsLookup.TryGetValue(dependencyId, out HashSet<Node<T, IEvent<T>>> tailNodes))
                    {
                        tailNodes = new HashSet<Node<T, IEvent<T>>>();
                        m_UnsatisfiedSuccessorsLookup.Add(dependencyId, tailNodes);
                    }
                    tailNodes.Add(tailNode);
                }
            }
            else
            {
                StartNode.OutgoingEdges.Add(edge.Id);
                m_EdgeTailNodeLookup.Add(edge.Id, StartNode);
            }
            ResolveUnsatisfiedSuccessorActivities(edge.Id);
            return true;
        }

        public bool AddActivityDependencies(T activityId, HashSet<T> dependencies)
        {
            throw new NotImplementedException();
        }

        public bool RemoveActivity(T activityId)
        {
            throw new NotImplementedException();
        }

        public bool RemoveActivityDependencies(T activityId, HashSet<T> dependencies)
        {
            throw new NotImplementedException();
        }

        public bool RemoveDummyActivity(T activityId)
        {
            return m_DummyEdgeOrchestrator.RemoveDummyActivity(activityId);
        }

        public IList<T> ActivityDependencyIds(T activityId)
        {
            Node<T, IEvent<T>> tailNode = m_EdgeTailNodeLookup[activityId];
            if (tailNode.NodeType == NodeType.Start || tailNode.NodeType == NodeType.Isolated)
                return new List<T>();
            var output = new List<T>();
            foreach (Edge<T, TActivity> incomingEdge in tailNode.IncomingEdges.Select(x => m_EdgeLookup[x]))
                output.Add(incomingEdge.Id);
            return output;
        }

        public IList<T> StrongActivityDependencyIds(T activityId)
        {
            Node<T, IEvent<T>> tailNode = m_EdgeTailNodeLookup[activityId];
            if (tailNode.NodeType == NodeType.Start || tailNode.NodeType == NodeType.Isolated)
                return new List<T>();
            var output = new List<T>();
            foreach (Edge<T, TActivity> incomingEdge in tailNode.IncomingEdges.Select(x => m_EdgeLookup[x]))
            {
                if (incomingEdge.Content.IsDummy)
                    output.AddRange(StrongActivityDependencyIds(incomingEdge.Id));
                else
                    output.Add(incomingEdge.Id);
            }
            return output;
        }

        public IList<ICircularDependency<T>> FindStrongCircularDependencies()
        {
            return FindStronglyConnectedComponents().Where(x => x.Dependencies.Count > 1).ToList();
        }

        public IList<IInvalidConstraint<T>> FindInvalidPreCompilationConstraints() =>
            ConstraintChecker<T, TResourceId, TWorkStreamId>.FindInvalidPreCompilationConstraints(Activities);

        public IList<IInvalidConstraint<T>> FindInvalidPostCompilationConstraints() =>
            ConstraintChecker<T, TResourceId, TWorkStreamId>.FindInvalidPostCompilationConstraints(Activities);

        public IDictionary<T, HashSet<T>> GetAncestorNodesLookup()
        {
            return m_TransitiveReducer.GetAncestorNodesLookup();
        }

        public bool TransitiveReduction()
        {
            return m_TransitiveReducer.ReduceGraph();
        }

        public bool RedirectEdges() => m_DummyEdgeOrchestrator.RedirectDummyEdges();

        public bool RemoveRedundantEdges() => m_DummyEdgeOrchestrator.RemoveRedundantDummyEdges();

        public bool CleanUpEdges()
        {
            if (!RedirectEdges()) return false;
            if (!RemoveRedundantEdges()) return false;
            return true;
        }

        public void CalculateCriticalPath()
        {
            if (!CleanUpEdges())
                throw new InvalidOperationException(Properties.Resources.Message_CannotPerformEdgeCleanUp);

            ClearCriticalPathVariables();

            bool allDependenciesSatisfied = AllDependenciesSatisfied;
            IList<IInvalidConstraint<T>> constraints = allDependenciesSatisfied
                ? FindInvalidPreCompilationConstraints() : new List<IInvalidConstraint<T>>();

            if (!m_CriticalPathEngine.CalculateEventEarliestFinishTimes(
                NodeIds, m_EdgeLookup, m_NodeLookup, m_EdgeHeadNodeLookup, m_EdgeTailNodeLookup,
                constraints, StartNode, EndNode, WhenTesting))
            {
                throw new InvalidOperationException(Properties.Resources.Message_CannotCalculateEventEarliestFinishTimes);
            }
            if (!m_CriticalPathEngine.CalculateEventLatestFinishTimes(
                NodeIds, m_EdgeLookup, m_NodeLookup, m_EdgeHeadNodeLookup, m_EdgeTailNodeLookup,
                constraints, EndNode, WhenTesting))
            {
                throw new InvalidOperationException(Properties.Resources.Message_CannotCalculateEventLatestFinishTimes);
            }
            if (!m_CriticalPathEngine.CalculateCriticalPathVariables(
                EdgeIds, m_EdgeLookup, m_EdgeHeadNodeLookup, m_EdgeTailNodeLookup,
                constraints, Events))
            {
                throw new InvalidOperationException(Properties.Resources.Message_CannotCalculateCriticalPath);
            }
        }

        public IList<T> CalculateCriticalPathPriorityList()
        {
            var tmpGraphBuilder = (ArrowGraphBuilder<T, TResourceId, TWorkStreamId, TActivity>)CloneObject();
            return CalculateCriticalPathPriorityList(tmpGraphBuilder);
        }

        public IEnumerable<IResourceSchedule<T, TResourceId, TWorkStreamId>> CalculateResourceSchedulesByPriorityList(
            IList<IResource<TResourceId, TWorkStreamId>> resources)
        {
            if (resources is null) throw new ArgumentNullException(nameof(resources));
            if (resources.Count < 0) throw new ArgumentOutOfRangeException(nameof(resources), Properties.Resources.Message_ValueCannotBeNegative);
            if (!Activities.Any()) return Enumerable.Empty<IResourceSchedule<T, TResourceId, TWorkStreamId>>();

            bool infiniteResources = !resources.Any();
            IList<IResource<TResourceId, TWorkStreamId>> filteredResources = resources.Where(x => !x.IsInactive).ToList();

            if (!infiniteResources) ValidateActivitiesAgainstResources(filteredResources);

            var tmpGraphBuilder = (ArrowGraphBuilder<T, TResourceId, TWorkStreamId, TActivity>)CloneObject();
            IList<T> priorityList = CalculateCriticalPathPriorityList(
                (ArrowGraphBuilder<T, TResourceId, TWorkStreamId, TActivity>)tmpGraphBuilder.CloneObject());

            return m_ResourceSchedulingEngine.CalculateResourceSchedules(
                priorityList, filteredResources, infiniteResources,
                id => tmpGraphBuilder.Activity(id),
                id => tmpGraphBuilder.StrongActivityDependencyIds(id),
                () => Activities.Select(x => (IActivity<T, TResourceId, TWorkStreamId>)x.CloneObject()).ToList());
        }

        public Graph<T, TActivity, IEvent<T>> ToGraph()
        {
            if (!CleanUpEdges()) return null;
            return new Graph<T, TActivity, IEvent<T>>(
                m_EdgeLookup.Values.Select(x => (Edge<T, TActivity>)x.CloneObject()),
                m_NodeLookup.Values.Select(x => (Node<T, IEvent<T>>)x.CloneObject()));
        }

        public void Reset()
        {
            m_EdgeLookup.Clear();
            m_NodeLookup.Clear();
            m_UnsatisfiedSuccessorsLookup.Clear();
            m_EdgeHeadNodeLookup.Clear();
            m_EdgeTailNodeLookup.Clear();
            Initialize();
        }

        #endregion

        #region Private Methods

        private void Initialize()
        {
            T startEventId = m_NodeIdGenerator();
            StartNode = new Node<T, IEvent<T>>(NodeType.Start, s_EventGeneratorWithTimes(startEventId, 0, 0));
            m_NodeLookup.Add(StartNode.Id, StartNode);
            T endEventId = m_NodeIdGenerator();
            EndNode = new Node<T, IEvent<T>>(NodeType.End, s_EventGenerator(endEventId));
            m_NodeLookup.Add(EndNode.Id, EndNode);
            m_DummyEdgeOrchestrator = CreateOrchestrator();
            m_TransitiveReducer = CreateTransitiveReducer();
        }

        private IDummyEdgeOrchestrator<T, TResourceId, TWorkStreamId, TActivity> CreateOrchestrator()
        {
            return new DummyEdgeOrchestrator<T, TResourceId, TWorkStreamId, TActivity>(
                m_EdgeIdGenerator,
                m_DummyActivityGenerator,
                () => AllDependenciesSatisfied,
                () => FindStrongCircularDependencies(),
                m_EdgeLookup,
                m_NodeLookup,
                m_EdgeHeadNodeLookup,
                m_EdgeTailNodeLookup,
                () => StartNode,
                () => EndNode);
        }

        private ITransitiveReducer<T> CreateTransitiveReducer()
        {
            return new ArrowTransitiveReducer<T, TResourceId, TWorkStreamId, TActivity>(
                () => AllDependenciesSatisfied,
                () => FindStrongCircularDependencies(),
                () => EndNodes.Select(x => x.Id),
                m_DummyEdgeOrchestrator,
                m_NodeLookup,
                m_EdgeTailNodeLookup);
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

        private IList<ICircularDependency<T>> FindStronglyConnectedComponents()
        {
            return m_SccFinder.FindStronglyConnectedComponents(
                EdgeIds, m_EdgeLookup, m_EdgeHeadNodeLookup, m_EdgeTailNodeLookup);
        }

        private void ResolveUnsatisfiedSuccessorActivities(T activityId)
        {
            if (!m_EdgeLookup.ContainsKey(activityId)) return;

            T headEventId = m_NodeIdGenerator();
            var headNode = new Node<T, IEvent<T>>(s_EventGenerator(headEventId));
            headNode.IncomingEdges.Add(activityId);
            m_EdgeHeadNodeLookup.Add(activityId, headNode);
            m_NodeLookup.Add(headNode.Id, headNode);

            if (m_UnsatisfiedSuccessorsLookup.TryGetValue(activityId, out HashSet<Node<T, IEvent<T>>> unsatisfiedSuccessorTailNodes))
            {
                foreach (Node<T, IEvent<T>> tailNode in unsatisfiedSuccessorTailNodes)
                    m_DummyEdgeOrchestrator.ConnectWithDummyEdge(headNode, tailNode);
                m_UnsatisfiedSuccessorsLookup.Remove(activityId);
            }
            else
            {
                m_DummyEdgeOrchestrator.ConnectWithDummyEdge(headNode, EndNode);
            }
        }

        private void ValidateActivitiesAgainstResources(IList<IResource<TResourceId, TWorkStreamId>> filteredResources)
        {
            var unavailableResourcesSet = new List<IUnavailableResources<T, TResourceId>>();
            foreach (TActivity activity in Activities)
            {
                if (!activity.TargetResources.Any()) continue;
                if (activity.TargetResourceOperator == LogicalOperator.AND)
                {
                    IEnumerable<TResourceId> unavailableResourceIds = activity.TargetResources.Except(filteredResources.Select(x => x.Id));
                    if (unavailableResourceIds.Any())
                        unavailableResourcesSet.Add(new UnavailableResources<T, TResourceId>(activity.Id, unavailableResourceIds));
                }
                else if (activity.TargetResourceOperator == LogicalOperator.OR
                         || activity.TargetResourceOperator == LogicalOperator.ACTIVE_AND)
                {
                    IEnumerable<TResourceId> intersection = activity.TargetResources.Intersect(filteredResources.Select(x => x.Id));
                    if (!intersection.Any())
                        unavailableResourcesSet.Add(new UnavailableResources<T, TResourceId>(activity.Id, activity.TargetResources));
                }
            }
            if (unavailableResourcesSet.Any())
                throw new InvalidOperationException(Properties.Resources.Message_AtLeastOneOfSpecifiedTargetResourcesAreNotAvailableInResourcesProvided);

            bool allResourcesAreExplicitTargets = filteredResources.All(x => x.IsExplicitTarget);
            bool atLeastOneActivityRequiresNonExplicitTargetResource = Activities.Any(x => !x.IsDummy && !x.TargetResources.Any());
            if (allResourcesAreExplicitTargets && atLeastOneActivityRequiresNonExplicitTargetResource)
                throw new InvalidOperationException(Properties.Resources.Message_AtLeastOneActivityRequiresNonExplicitTargetResourceButAllProvidedResourcesAreExplicitTargets);
        }

        private static IList<T> CalculateCriticalPathPriorityList(ArrowGraphBuilder<T, TResourceId, TWorkStreamId, TActivity> graphBuilder)
        {
            if (graphBuilder is null) throw new ArgumentNullException(nameof(graphBuilder));
            var priorityList = new List<T>();
            bool cont = true;
            while (cont)
            {
                graphBuilder.CalculateCriticalPath();

                int minFloat = graphBuilder.Activities
                    .Where(x => !x.IsDummy && x.TotalSlack.HasValue)
                    .Select(x => x.TotalSlack.Value)
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
                    graphBuilder.Activity(criticalActivityId).Duration = 0;
                }
                else
                {
                    cont = false;
                }
            }
            if (graphBuilder.Activities.Any(x => !x.IsDummy))
                throw new InvalidOperationException(Properties.Resources.Message_CannotCalculateCriticalPathPriorityList);
            return priorityList;
        }

        #endregion

        #region ICloneObject

        public object CloneObject()
        {
            Graph<T, TActivity, IEvent<T>> arrowGraphCopy = ToGraph();
            T minNodeId = arrowGraphCopy.Nodes.Select(x => x.Id).DefaultIfEmpty().Min();
            minNodeId = minNodeId.Previous();
            T minEdgeId = arrowGraphCopy.Edges.Select(x => x.Id).DefaultIfEmpty().Min();
            minEdgeId = minEdgeId.Previous();
            return new ArrowGraphBuilder<T, TResourceId, TWorkStreamId, TActivity>(
                arrowGraphCopy,
                () => minEdgeId = minEdgeId.Previous(),
                () => minNodeId = minNodeId.Previous(),
                m_DummyActivityGenerator,
                m_SccFinder,
                m_CriticalPathEngine,
                m_ResourceSchedulingEngine);
        }

        #endregion
    }
}
