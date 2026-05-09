using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    // Sealed builder for Activity-on-Arrow graphs. Owns all graph state directly.
    // Algorithm work is delegated to injected engine instances (SCC finder, CPM engine).
    // The public constructor wires up default engine instances; the internal constructor
    // accepts injected engines for testability.
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

        #endregion

        #region Graph State (previously in GraphBuilderBase)

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

        // Internal constructor — accepts a custom dummy generator for subtype compatibility.
        internal ArrowGraphBuilder(
            Func<T> edgeIdGenerator,
            Func<T> nodeIdGenerator,
            Func<T, TActivity> dummyActivityGenerator)
            : this(
                  edgeIdGenerator,
                  nodeIdGenerator,
                  dummyActivityGenerator,
                  new ArrowTarjanStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity, IEvent<T>>(),
                  new ArrowCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity, IEvent<T>>(),
                  new PriorityListResourceScheduler<T, TResourceId, TWorkStreamId>())
        {
        }

        // Internal constructor — accepts injected engines for testability.
        internal ArrowGraphBuilder(
            Func<T> edgeIdGenerator,
            Func<T> nodeIdGenerator,
            IArrowStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity, IEvent<T>> sccFinder,
            IArrowCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity, IEvent<T>> criticalPathEngine)
            : this(
                  edgeIdGenerator,
                  nodeIdGenerator,
                  s_DefaultDummyActivityGenerator,
                  sccFinder,
                  criticalPathEngine,
                  new PriorityListResourceScheduler<T, TResourceId, TWorkStreamId>())
        {
        }

        // Internal constructor — accepts injected dummy generator and engines.
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

        // Graph-loading constructor (from existing Graph<T, TActivity, IEvent<T>>).
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

        // Internal graph-loading constructor with custom dummy generator.
        internal ArrowGraphBuilder(
            Graph<T, TActivity, IEvent<T>> graph,
            Func<T> edgeIdGenerator,
            Func<T> nodeIdGenerator,
            Func<T, TActivity> dummyActivityGenerator)
            : this(
                  graph,
                  edgeIdGenerator,
                  nodeIdGenerator,
                  dummyActivityGenerator,
                  new ArrowTarjanStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity, IEvent<T>>(),
                  new ArrowCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity, IEvent<T>>(),
                  new PriorityListResourceScheduler<T, TResourceId, TWorkStreamId>())
        {
        }

        // Internal graph-loading constructor with engine injection.
        internal ArrowGraphBuilder(
            Graph<T, TActivity, IEvent<T>> graph,
            Func<T> edgeIdGenerator,
            Func<T> nodeIdGenerator,
            IArrowStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity, IEvent<T>> sccFinder,
            IArrowCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity, IEvent<T>> criticalPathEngine)
            : this(
                  graph,
                  edgeIdGenerator,
                  nodeIdGenerator,
                  s_DefaultDummyActivityGenerator,
                  sccFinder,
                  criticalPathEngine,
                  new PriorityListResourceScheduler<T, TResourceId, TWorkStreamId>())
        {
        }

        // Internal graph-loading constructor with custom dummy generator and engine injection.
        internal ArrowGraphBuilder(
            Graph<T, TActivity, IEvent<T>> graph,
            Func<T> edgeIdGenerator,
            Func<T> nodeIdGenerator,
            Func<T, TActivity> dummyActivityGenerator,
            IArrowStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity, IEvent<T>> sccFinder,
            IArrowCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity, IEvent<T>> criticalPathEngine,
            IResourceSchedulingEngine<T, TResourceId, TWorkStreamId> resourceSchedulingEngine)
        {
            if (graph is null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

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
            {
                m_EdgeLookup.Add(edge.Id, edge);
            }

            foreach (Node<T, IEvent<T>> node in graph.Nodes)
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

        public TActivity Activity(T key)
        {
            return m_EdgeLookup[key].Content;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "No better term available")]
        public IEvent<T> Event(T key)
        {
            return m_NodeLookup[key].Content;
        }

        public Edge<T, TActivity> Edge(T key)
        {
            if (!m_EdgeLookup.TryGetValue(key, out Edge<T, TActivity> edge))
            {
                return null;
            }
            return edge;
        }

        public Node<T, IEvent<T>> Node(T key)
        {
            if (!m_NodeLookup.TryGetValue(key, out Node<T, IEvent<T>> node))
            {
                return null;
            }
            return node;
        }

        public Node<T, IEvent<T>> EdgeHeadNode(T key)
        {
            Node<T, IEvent<T>> output = null;
            if (m_EdgeHeadNodeLookup.ContainsKey(key))
            {
                output = m_EdgeHeadNodeLookup[key];
            }
            return output;
        }

        public Node<T, IEvent<T>> EdgeTailNode(T key)
        {
            Node<T, IEvent<T>> output = null;
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
            if (m_EdgeLookup.ContainsKey(activity.Id))
            {
                return false;
            }
            if (dependencies.Contains(activity.Id))
            {
                return false;
            }
            // Create a new edge for the activity.
            var edge = new Edge<T, TActivity>(activity);
            m_EdgeLookup.Add(edge.Id, edge);

            // We expect dependencies at some point.
            if (dependencies.Any())
            {
                // Since we use dummy edges to connect all tail nodes, we can create
                // a new tail node for this edge.
                T tailEventId = m_NodeIdGenerator();
                var tailNode = new Node<T, IEvent<T>>(s_EventGenerator(tailEventId));
                tailNode.OutgoingEdges.Add(edge.Id);
                m_EdgeTailNodeLookup.Add(edge.Id, tailNode);
                m_NodeLookup.Add(tailNode.Id, tailNode);

                // Check which of the expected dependencies currently exist.
                IList<T> existingDependencies = m_EdgeLookup.Keys.Intersect(dependencies).ToList();
                IList<T> nonExistingDependencies = dependencies.Except(existingDependencies).ToList();

                // If any expected dependencies currently exist, then hook up their head
                // node to this edge's tail node with dummy edges.
                foreach (T dependencyId in existingDependencies)
                {
                    Node<T, IEvent<T>> dependencyHeadNode = m_EdgeHeadNodeLookup[dependencyId];
                    T dummyEdgeId = m_EdgeIdGenerator();
                    var dummyEdge = new Edge<T, TActivity>(m_DummyActivityGenerator(dummyEdgeId));
                    tailNode.IncomingEdges.Add(dummyEdgeId);
                    m_EdgeHeadNodeLookup.Add(dummyEdgeId, tailNode);

                    // If the head node of the dependency is the End node, then convert it.
                    if (dependencyHeadNode.NodeType == NodeType.End)
                    {
                        dependencyHeadNode.SetNodeType(NodeType.Normal);
                    }

                    dependencyHeadNode.OutgoingEdges.Add(dummyEdgeId);
                    m_EdgeTailNodeLookup.Add(dummyEdgeId, dependencyHeadNode);
                    m_EdgeLookup.Add(dummyEdge.Id, dummyEdge);
                }

                // If any expected dependencies currently do not exist, then record their
                // IDs and add this edge's tail node as an unsatisfied successor.
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
                // No dependencies, so attach it directly to the start node.
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
            // Retrieve the activity's edge.
            if (!m_EdgeLookup.TryGetValue(activityId, out Edge<T, TActivity> edge))
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

            Node<T, IEvent<T>> tailNode = m_EdgeTailNodeLookup[activityId];
            Node<T, IEvent<T>> headNode = m_EdgeHeadNodeLookup[activityId];

            // Check to make sure that no other edges will be made parallel
            // by removing this edge.
            if (HaveDescendantOrAncestorOverlap(tailNode, headNode)
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
            m_EdgeLookup.Remove(activityId);

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

        public IList<T> ActivityDependencyIds(T activityId)
        {
            Node<T, IEvent<T>> tailNode = m_EdgeTailNodeLookup[activityId];
            if (tailNode.NodeType == NodeType.Start || tailNode.NodeType == NodeType.Isolated)
            {
                return new List<T>();
            }
            var output = new List<T>();
            foreach (Edge<T, TActivity> incomingEdge in tailNode.IncomingEdges.Select(x => m_EdgeLookup[x]))
            {
                output.Add(incomingEdge.Id);
            }
            return output;
        }

        public IList<T> StrongActivityDependencyIds(T activityId)
        {
            Node<T, IEvent<T>> tailNode = m_EdgeTailNodeLookup[activityId];
            if (tailNode.NodeType == NodeType.Start || tailNode.NodeType == NodeType.Isolated)
            {
                return new List<T>();
            }
            var output = new List<T>();
            foreach (Edge<T, TActivity> incomingEdge in tailNode.IncomingEdges.Select(x => m_EdgeLookup[x]))
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

        public IList<ICircularDependency<T>> FindStrongCircularDependencies()
        {
            return FindStronglyConnectedComponents().Where(x => x.Dependencies.Count > 1).ToList();
        }

        public IList<IInvalidConstraint<T>> FindInvalidPreCompilationConstraints()
        {
            var activitiesWithInvalidConstraints = new List<IInvalidConstraint<T>>();

            foreach (IActivity<T, TResourceId, TWorkStreamId> activity in Activities)
            {
                if (activity.MinimumFreeSlack.HasValue
                    && activity.MaximumLatestFinishTime.HasValue)
                {
                    activitiesWithInvalidConstraints.Add(
                        new InvalidConstraint<T>(activity.Id, Properties.Resources.Message_CannotSetMinimumFreeSlackAndMaximumLatestFinishTime));
                    continue;
                }
                if (activity.MinimumEarliestStartTime.HasValue
                    && activity.MaximumLatestFinishTime.HasValue
                    && (activity.MinimumEarliestStartTime.Value + activity.Duration) > activity.MaximumLatestFinishTime.Value)
                {
                    activitiesWithInvalidConstraints.Add(
                        new InvalidConstraint<T>(activity.Id, Properties.Resources.Message_MinimumEarliestStartTimePlusDurationMustBeGreaterThanMaximumLatestFinishTime));
                    continue;
                }
            }

            return activitiesWithInvalidConstraints;
        }

        public IList<IInvalidConstraint<T>> FindInvalidPostCompilationConstraints()
        {
            var activitiesWithInvalidConstraints = new List<IInvalidConstraint<T>>();

            foreach (IActivity<T, TResourceId, TWorkStreamId> activity in Activities)
            {
                if (activity.EarliestStartTime.HasValue && activity.EarliestFinishTime.HasValue)
                {
                    if (activity.EarliestStartTime < 0)
                    {
                        activitiesWithInvalidConstraints.Add(
                            new InvalidConstraint<T>(activity.Id, Properties.Resources.Message_EarliestStartTimeLessThanZero));
                    }
                    if (activity.EarliestFinishTime < 0)
                    {
                        activitiesWithInvalidConstraints.Add(
                            new InvalidConstraint<T>(activity.Id, Properties.Resources.Message_EarliestFinishTimeLessThanZero));
                    }
                }

                if (activity.LatestStartTime.HasValue && activity.LatestFinishTime.HasValue)
                {
                    if (activity.LatestStartTime < 0)
                    {
                        activitiesWithInvalidConstraints.Add(
                            new InvalidConstraint<T>(activity.Id, Properties.Resources.Message_LatestStartTimeLessThanZero));
                    }
                    if (activity.LatestFinishTime < 0)
                    {
                        activitiesWithInvalidConstraints.Add(
                            new InvalidConstraint<T>(activity.Id, Properties.Resources.Message_LatestFinishTimeLessThanZero));
                    }
                }

                if (activity.EarliestStartTime.HasValue && activity.LatestStartTime.HasValue)
                {
                    if (activity.LatestStartTime < activity.EarliestStartTime)
                    {
                        activitiesWithInvalidConstraints.Add(
                            new InvalidConstraint<T>(activity.Id, Properties.Resources.Message_LatestStartTimeLessThanEarliestStartTime));
                    }
                }

                if (activity.EarliestFinishTime.HasValue && activity.LatestFinishTime.HasValue)
                {
                    if (activity.LatestFinishTime < activity.EarliestFinishTime)
                    {
                        activitiesWithInvalidConstraints.Add(
                            new InvalidConstraint<T>(activity.Id, Properties.Resources.Message_LatestFinishTimeLessThanEarliestFinishTime));
                    }
                }

                if (activity.EarliestStartTime.HasValue && activity.MinimumEarliestStartTime.HasValue)
                {
                    if (activity.EarliestStartTime < activity.MinimumEarliestStartTime)
                    {
                        activitiesWithInvalidConstraints.Add(
                            new InvalidConstraint<T>(activity.Id, Properties.Resources.Message_EarliestStartTimeLessThanMinimumEarliestStartTime));
                    }
                }

                if (activity.LatestFinishTime.HasValue && activity.MaximumLatestFinishTime.HasValue)
                {
                    if (activity.LatestFinishTime > activity.MaximumLatestFinishTime)
                    {
                        activitiesWithInvalidConstraints.Add(
                            new InvalidConstraint<T>(activity.Id, Properties.Resources.Message_LatestFinishTimeMoreThanMaximumLatestFinishTime));
                    }
                }

                if (activity.FreeSlack.HasValue && activity.MinimumFreeSlack.HasValue)
                {
                    if (activity.FreeSlack < activity.MinimumFreeSlack)
                    {
                        activitiesWithInvalidConstraints.Add(
                            new InvalidConstraint<T>(activity.Id, Properties.Resources.Message_FreeSlackLessThanMinimumFreeSlack));
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

        public bool TransitiveReduction()
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

        public bool RedirectEdges()
        {
            return RedirectDummyEdges();
        }

        public bool RemoveRedundantEdges()
        {
            return RemoveRedundantDummyEdges();
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

        public void CalculateCriticalPath()
        {
            bool edgesCleaned = CleanUpEdges();
            if (!edgesCleaned)
            {
                throw new InvalidOperationException(Properties.Resources.Message_CannotPerformEdgeCleanUp);
            }

            ClearCriticalPathVariables();

            bool allDependenciesSatisfied = AllDependenciesSatisfied;

            if (!m_CriticalPathEngine.CalculateEventEarliestFinishTimes(
                NodeIds,
                m_EdgeLookup,
                m_NodeLookup,
                m_EdgeHeadNodeLookup,
                m_EdgeTailNodeLookup,
                allDependenciesSatisfied ? FindInvalidPreCompilationConstraints() : new List<IInvalidConstraint<T>>(),
                StartNode,
                EndNode,
                WhenTesting))
            {
                throw new InvalidOperationException(Properties.Resources.Message_CannotCalculateEventEarliestFinishTimes);
            }
            if (!m_CriticalPathEngine.CalculateEventLatestFinishTimes(
                NodeIds,
                m_EdgeLookup,
                m_NodeLookup,
                m_EdgeHeadNodeLookup,
                m_EdgeTailNodeLookup,
                allDependenciesSatisfied ? FindInvalidPreCompilationConstraints() : new List<IInvalidConstraint<T>>(),
                EndNode,
                WhenTesting))
            {
                throw new InvalidOperationException(Properties.Resources.Message_CannotCalculateEventLatestFinishTimes);
            }
            if (!m_CriticalPathEngine.CalculateCriticalPathVariables(
                EdgeIds,
                m_EdgeLookup,
                m_EdgeHeadNodeLookup,
                m_EdgeTailNodeLookup,
                allDependenciesSatisfied ? FindInvalidPreCompilationConstraints() : new List<IInvalidConstraint<T>>(),
                Events))
            {
                throw new InvalidOperationException(Properties.Resources.Message_CannotCalculateCriticalPath);
            }
        }

        // Exposes the priority list calculation used internally by CalculateResourceSchedulesByPriorityList.
        public IList<T> CalculateCriticalPathPriorityList()
        {
            var tmpGraphBuilder = (ArrowGraphBuilder<T, TResourceId, TWorkStreamId, TActivity>)CloneObject();
            return CalculateCriticalPathPriorityList(tmpGraphBuilder);
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

            var tmpGraphBuilder = (ArrowGraphBuilder<T, TResourceId, TWorkStreamId, TActivity>)CloneObject();

            // Use a separate clone for the priority list calculation so that tmpGraphBuilder retains
            // original activity durations for the scheduling loop below.
            IList<T> priorityList = CalculateCriticalPathPriorityList(
                (ArrowGraphBuilder<T, TResourceId, TWorkStreamId, TActivity>)tmpGraphBuilder.CloneObject());

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

        private static IList<T> CalculateCriticalPathPriorityList(ArrowGraphBuilder<T, TResourceId, TWorkStreamId, TActivity> graphBuilder)
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

        public Graph<T, TActivity, IEvent<T>> ToGraph()
        {
            bool edgesCleanedUp = CleanUpEdges();
            if (!edgesCleanedUp)
            {
                return null;
            }
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
                EdgeIds,
                m_EdgeLookup,
                m_EdgeHeadNodeLookup,
                m_EdgeTailNodeLookup);
        }

        private bool ChangeEdgeTailNodeWithoutCleanup(T edgeId, T newTailNodeId)
        {
            if (!AllDependenciesSatisfied)
            {
                return false;
            }
            if (!m_EdgeLookup.TryGetValue(edgeId, out Edge<T, TActivity> _))
            {
                return false;
            }
            if (!m_NodeLookup.TryGetValue(newTailNodeId, out Node<T, IEvent<T>> newTailNode))
            {
                return false;
            }

            Node<T, IEvent<T>> oldTailNode = m_EdgeTailNodeLookup[edgeId];
            oldTailNode.OutgoingEdges.Remove(edgeId);
            m_EdgeTailNodeLookup.Remove(edgeId);

            newTailNode.OutgoingEdges.Add(edgeId);
            m_EdgeTailNodeLookup.Add(edgeId, newTailNode);
            return true;
        }

        private bool ChangeEdgeHeadNodeWithoutCleanup(T edgeId, T newHeadNodeId)
        {
            if (!AllDependenciesSatisfied)
            {
                return false;
            }
            if (!m_EdgeLookup.TryGetValue(edgeId, out Edge<T, TActivity> _))
            {
                return false;
            }
            if (!m_NodeLookup.TryGetValue(newHeadNodeId, out Node<T, IEvent<T>> newHeadNode))
            {
                return false;
            }

            Node<T, IEvent<T>> currentHeadNode = m_EdgeHeadNodeLookup[edgeId];
            currentHeadNode.IncomingEdges.Remove(edgeId);
            m_EdgeHeadNodeLookup.Remove(edgeId);

            newHeadNode.IncomingEdges.Add(edgeId);
            m_EdgeHeadNodeLookup.Add(edgeId, newHeadNode);
            return true;
        }

        private bool ChangeEdgeTailNode(T edgeId, T newTailNodeId)
        {
            if (!AllDependenciesSatisfied)
            {
                return false;
            }

            Node<T, IEvent<T>> oldTailNode = m_EdgeTailNodeLookup[edgeId];
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
                Node<T, IEvent<T>> headNode = m_EdgeHeadNodeLookup[edgeId];
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
            if (oldTailNode.NodeType != NodeType.Start
                && oldTailNode.NodeType != NodeType.Isolated
                && !oldTailNode.IncomingEdges.Any()
                && !oldTailNode.OutgoingEdges.Any())
            {
                m_NodeLookup.Remove(oldTailNode.Id);
            }
            return true;
        }

        private bool ChangeEdgeHeadNode(T edgeId, T newHeadNodeId)
        {
            if (!AllDependenciesSatisfied)
            {
                return false;
            }

            Node<T, IEvent<T>> oldHeadNode = m_EdgeHeadNodeLookup[edgeId];
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
                Node<T, IEvent<T>> tailNode = m_EdgeTailNodeLookup[edgeId];
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
            if (oldHeadNode.NodeType != NodeType.End
                && oldHeadNode.NodeType != NodeType.Isolated
                && !oldHeadNode.IncomingEdges.Any()
                && !oldHeadNode.OutgoingEdges.Any())
            {
                m_NodeLookup.Remove(oldHeadNode.Id);
            }
            return true;
        }

        private void GetEdgesInDescendingOrder(T nodeId, IList<Edge<T, TActivity>> edgesInDescendingOrder, HashSet<T> recordedEdges)
        {
            if (edgesInDescendingOrder is null)
            {
                throw new ArgumentNullException(nameof(edgesInDescendingOrder));
            }
            if (recordedEdges is null)
            {
                throw new ArgumentNullException(nameof(recordedEdges));
            }
            Node<T, IEvent<T>> node = m_NodeLookup[nodeId];
            if (node.NodeType == NodeType.End || node.NodeType == NodeType.Isolated)
            {
                return;
            }

            foreach (Edge<T, TActivity> outgoingEdge in node.OutgoingEdges.Select(x => m_EdgeLookup[x]))
            {
                if (!recordedEdges.Contains(outgoingEdge.Id))
                {
                    edgesInDescendingOrder.Add(outgoingEdge);
                    recordedEdges.Add(outgoingEdge.Id);
                }
                GetEdgesInDescendingOrder(m_EdgeHeadNodeLookup[outgoingEdge.Id].Id, edgesInDescendingOrder, recordedEdges);
            }
        }

        private bool HaveDescendantOrAncestorOverlap(Node<T, IEvent<T>> tailNode, Node<T, IEvent<T>> headNode)
        {
            if (tailNode is null)
            {
                throw new ArgumentNullException(nameof(tailNode));
            }
            if (headNode is null)
            {
                throw new ArgumentNullException(nameof(headNode));
            }

            // First the descendants of the tail node.
            var tailNodeAncestorsAndDescendants = new HashSet<T>();
            if (tailNode.NodeType != NodeType.End && tailNode.NodeType != NodeType.Isolated)
            {
                tailNodeAncestorsAndDescendants.UnionWith(
                    tailNode.OutgoingEdges
                    .Select(x => m_EdgeLookup[x])
                    .Select(x => m_EdgeHeadNodeLookup[x.Id].Id)
                    .Except(new[] { headNode.Id }));
            }

            // Then the ancestors of the tail node.
            if (tailNode.NodeType != NodeType.Start && tailNode.NodeType != NodeType.Isolated)
            {
                tailNodeAncestorsAndDescendants.UnionWith(
                    tailNode.IncomingEdges
                    .Select(x => m_EdgeLookup[x])
                    .Select(x => m_EdgeTailNodeLookup[x.Id].Id)
                    .Except(new[] { headNode.Id }));
            }

            // Next the ancestors of the head node.
            var headNodeAncestorsAndDescendants = new HashSet<T>();
            if (headNode.NodeType != NodeType.Start && headNode.NodeType != NodeType.Isolated)
            {
                headNodeAncestorsAndDescendants.UnionWith(
                    headNode.IncomingEdges
                    .Select(x => m_EdgeLookup[x])
                    .Select(x => m_EdgeTailNodeLookup[x.Id].Id)
                    .Except(new[] { tailNode.Id }));
            }

            // Then the descendants of the head node.
            if (headNode.NodeType != NodeType.End && headNode.NodeType != NodeType.Isolated)
            {
                headNodeAncestorsAndDescendants.UnionWith(
                    headNode.OutgoingEdges
                    .Select(x => m_EdgeLookup[x])
                    .Select(x => m_EdgeHeadNodeLookup[x.Id].Id)
                    .Except(new[] { tailNode.Id }));
            }

            IEnumerable<T> overlap = tailNodeAncestorsAndDescendants.Intersect(headNodeAncestorsAndDescendants);
            if (overlap.Any())
            {
                return true;
            }
            return false;
        }

        private bool ShareMoreThanOneEdge(Node<T, IEvent<T>> tailNode, Node<T, IEvent<T>> headNode)
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
            if (tailNode.NodeType != NodeType.End && tailNode.NodeType != NodeType.Isolated)
            {
                tailNodeOutgoingEdgeIds.UnionWith(tailNode.OutgoingEdges);
            }
            var headNodeIncomingEdgeIds = new HashSet<T>();
            if (headNode.NodeType != NodeType.Start && headNode.NodeType != NodeType.Isolated)
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

        private HashSet<T> GetAncestorNodes(T nodeId, IDictionary<T, HashSet<T>> nodeIdAncestorLookup)
        {
            if (nodeIdAncestorLookup is null)
            {
                throw new ArgumentNullException(nameof(nodeIdAncestorLookup));
            }
            Node<T, IEvent<T>> node = m_NodeLookup[nodeId];
            var totalAncestorNodes = new HashSet<T>();
            if (node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated)
            {
                return totalAncestorNodes;
            }

            foreach (T tailNodeId in node.IncomingEdges.Select(x => m_EdgeTailNodeLookup[x].Id).ToList())
            {
                if (!totalAncestorNodes.Contains(tailNodeId))
                {
                    totalAncestorNodes.Add(tailNodeId);
                }
                if (!nodeIdAncestorLookup.TryGetValue(tailNodeId, out HashSet<T> tailNodeAncestorNodes))
                {
                    tailNodeAncestorNodes = GetAncestorNodes(tailNodeId, nodeIdAncestorLookup);
                    nodeIdAncestorLookup.Add(tailNodeId, tailNodeAncestorNodes);
                }
                totalAncestorNodes.UnionWith(tailNodeAncestorNodes);
            }
            return totalAncestorNodes;
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

            List<Node<T, IEvent<T>>> nodes = m_NodeLookup.Values
                .Where(x => x.NodeType != NodeType.End && x.NodeType != NodeType.Isolated)
                .OrderByDescending(x => x.Content.EarliestFinishTime)
                .ToList();

            foreach (Node<T, IEvent<T>> node in nodes)
            {
                var outgoingDummyEdgeIdLookup = new HashSet<T>(
                    node.OutgoingEdges.Select(x => m_EdgeLookup[x])
                    .Where(x => x.Content.IsDummy && x.Content.CanBeRemoved).Select(x => x.Id));

                IList<Node<T, IEvent<T>>> dummyEdgeSuccessorNodes =
                    outgoingDummyEdgeIdLookup.Select(x => m_EdgeHeadNodeLookup[x]).ToList();

                IList<IEnumerable<T>> dummyEdgeIdsToSuccessorNodes =
                    dummyEdgeSuccessorNodes
                    .Select(x => x.IncomingEdges.Select(y => m_EdgeLookup[y])
                    .Where(y => y.Content.IsDummy && y.Content.CanBeRemoved)
                    .Select(y => y.Id))
                    .ToList();

                if (!dummyEdgeIdsToSuccessorNodes.Any())
                {
                    continue;
                }

                IList<T> commonDependencyNodes =
                    dummyEdgeIdsToSuccessorNodes.Select(x => x.Select(y => m_EdgeTailNodeLookup[y].Id))
                    .Aggregate((previous, next) => previous.Intersect(next)).ToList();

                var commonDependencyNodeLookup = new HashSet<T>(commonDependencyNodes);

                IList<T> commonDependencyEdgeIds =
                    dummyEdgeIdsToSuccessorNodes
                    .SelectMany(x => x)
                    .Where(x => commonDependencyNodeLookup.Contains(m_EdgeTailNodeLookup[x].Id))
                    .ToList();

                var allSuccessorNodeLookup = new HashSet<T>(node.OutgoingEdges.Select(x => m_EdgeHeadNodeLookup[x].Id));
                var commonSuccessorNodeLookup = new HashSet<T>(commonDependencyEdgeIds.Select(x => m_EdgeHeadNodeLookup[x].Id));

                if (!allSuccessorNodeLookup.IsSubsetOf(commonSuccessorNodeLookup))
                {
                    continue;
                }

                List<T> commonDependencyEdgeIdsForOriginalNode = commonDependencyEdgeIds
                    .Where(x => !outgoingDummyEdgeIdLookup.Contains(x))
                    .OrderBy(x => x)
                    .ToList();

                foreach (T commonDependencyEdgeId in commonDependencyEdgeIdsForOriginalNode)
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

        private void RemoveParallelIncomingDummyEdges(Node<T, IEvent<T>> node)
        {
            if (node is null)
            {
                throw new ArgumentNullException(nameof(node));
            }
            if (node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated)
            {
                return;
            }
            var tailNodeParallelDummyEdgesLookup = new Dictionary<T, HashSet<T>>();
            IEnumerable<T> removableIncomingDummyEdgeIds = node.IncomingEdges
                .Select(x => m_EdgeLookup[x])
                .Where(x => x.Content.IsDummy && x.Content.CanBeRemoved)
                .Select(x => x.Id)
                .ToList();

            foreach (T incomingDummyEdgeId in removableIncomingDummyEdgeIds)
            {
                T tailNodeId = m_EdgeTailNodeLookup[incomingDummyEdgeId].Id;
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

            IList<T> setsOfMoreThanOneDummyEdge = tailNodeParallelDummyEdgesLookup
                .Where(x => x.Value.Count > 1)
                .Select(x => x.Key)
                .ToList();

            foreach (T tailNodeId in setsOfMoreThanOneDummyEdge)
            {
                IList<T> dummyEdgeIds = tailNodeParallelDummyEdgesLookup[tailNodeId].ToList();
                int length = dummyEdgeIds.Count;
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

            foreach (Edge<T, TActivity> edge in GetDummyEdgesInDescendingOrder().Where(x => x.Content.CanBeRemoved))
            {
                Node<T, IEvent<T>> tailNode = m_EdgeTailNodeLookup[edge.Id];
                Node<T, IEvent<T>> headNode = m_EdgeHeadNodeLookup[edge.Id];
                if (tailNode.OutgoingEdges.Count == 1 && headNode.IncomingEdges.Count == 1)
                {
                    RemoveDummyActivity(edge.Id);
                }
            }

            foreach (Edge<T, TActivity> edge in GetDummyEdgesInDescendingOrder().Where(x => x.Content.CanBeRemoved))
            {
                if (m_EdgeHeadNodeLookup[edge.Id].IncomingEdges.Count == 1)
                {
                    RemoveDummyActivity(edge.Id);
                }
            }

            foreach (Edge<T, TActivity> edge in GetDummyEdgesInDescendingOrder().Where(x => x.Content.CanBeRemoved))
            {
                if (m_EdgeTailNodeLookup[edge.Id].OutgoingEdges.Count == 1)
                {
                    RemoveDummyActivity(edge.Id);
                }
            }

            foreach (Node<T, IEvent<T>> node in m_NodeLookup.Values.ToList())
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
            Node<T, IEvent<T>> node = m_NodeLookup[nodeId];
            if (node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated)
            {
                return;
            }

            var tailNodeAncestors = new HashSet<T>(node.IncomingEdges
                .Select(x => m_EdgeTailNodeLookup[x].Id)
                .SelectMany(x => nodeIdAncestorLookup[x]));

            List<T> incomingDummyEdges = node.IncomingEdges
                .Select(x => m_EdgeLookup[x])
                .Where(x => x.Content.IsDummy && x.Content.CanBeRemoved)
                .Select(x => x.Id)
                .ToList();

            foreach (T dummyEdgeId in incomingDummyEdges)
            {
                T dummyEdgeTailNodeId = m_EdgeTailNodeLookup[dummyEdgeId].Id;
                if (tailNodeAncestors.Contains(dummyEdgeTailNodeId))
                {
                    RemoveDummyActivity(dummyEdgeId);
                }
            }

            List<T> remainingIncomingEdges = node.IncomingEdges
                .Select(x => m_EdgeTailNodeLookup[x].Id)
                .ToList();

            foreach (T tailNodeId in remainingIncomingEdges)
            {
                RemoveRedundantIncomingDummyEdges(tailNodeId, nodeIdAncestorLookup);
            }
        }

        private IList<Edge<T, TActivity>> GetDummyEdgesInDescendingOrder()
        {
            var recordedEdges = new HashSet<T>();
            T startNodeId = StartNode.Id;
            var edgesInDescendingOrder = new List<Edge<T, TActivity>>();
            GetEdgesInDescendingOrder(startNodeId, edgesInDescendingOrder, recordedEdges);
            return edgesInDescendingOrder.Where(x => x.Content.IsDummy).ToList();
        }

        private void ResolveUnsatisfiedSuccessorActivities(T activityId)
        {
            if (!m_EdgeLookup.ContainsKey(activityId))
            {
                return;
            }
            if (m_UnsatisfiedSuccessorsLookup.TryGetValue(activityId, out HashSet<Node<T, IEvent<T>>> unsatisfiedSuccessorTailNodes))
            {
                T headEventId = m_NodeIdGenerator();
                var headNode = new Node<T, IEvent<T>>(s_EventGenerator(headEventId));
                headNode.IncomingEdges.Add(activityId);
                m_EdgeHeadNodeLookup.Add(activityId, headNode);
                m_NodeLookup.Add(headNode.Id, headNode);

                foreach (Node<T, IEvent<T>> tailNode in unsatisfiedSuccessorTailNodes)
                {
                    T dummyEdgeId = m_EdgeIdGenerator();
                    var dummyEdge = new Edge<T, TActivity>(m_DummyActivityGenerator(dummyEdgeId));
                    tailNode.IncomingEdges.Add(dummyEdgeId);
                    m_EdgeHeadNodeLookup.Add(dummyEdgeId, tailNode);
                    headNode.OutgoingEdges.Add(dummyEdgeId);
                    m_EdgeTailNodeLookup.Add(dummyEdgeId, headNode);
                    m_EdgeLookup.Add(dummyEdge.Id, dummyEdge);
                }
                m_UnsatisfiedSuccessorsLookup.Remove(activityId);
            }
            else
            {
                T headEventId = m_NodeIdGenerator();
                Node<T, IEvent<T>> dependencyHeadNode = new Node<T, IEvent<T>>(s_EventGenerator(headEventId));

                dependencyHeadNode.IncomingEdges.Add(activityId);
                m_EdgeHeadNodeLookup.Add(activityId, dependencyHeadNode);
                m_NodeLookup.Add(dependencyHeadNode.Id, dependencyHeadNode);

                T dummyEdgeId = m_EdgeIdGenerator();
                var dummyEdge = new Edge<T, TActivity>(m_DummyActivityGenerator(dummyEdgeId));

                dependencyHeadNode.OutgoingEdges.Add(dummyEdgeId);
                m_EdgeTailNodeLookup.Add(dummyEdgeId, dependencyHeadNode);
                m_EdgeLookup.Add(dummyEdgeId, dummyEdge);

                EndNode.IncomingEdges.Add(dummyEdgeId);
                m_EdgeHeadNodeLookup.Add(dummyEdgeId, EndNode);
            }
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
                m_DummyActivityGenerator);
        }

        #endregion
    }
}
