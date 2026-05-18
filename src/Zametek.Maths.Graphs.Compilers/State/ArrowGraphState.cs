using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    // Owns all mutable graph state for an Activity-on-Arrow graph: the five lookup
    // dictionaries plus the single Start and End event nodes. Both the builder and
    // every Arrow engine (orchestrator, transitive reducer, CPM engine, SCC finder)
    // operate on a single instance of this class, so the same dictionaries are
    // never threaded through method calls as raw parameters.
    internal sealed class ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity>
        where TActivity : class, IActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        #region Fields

        private readonly Dictionary<T, Edge<T, TActivity>> m_EdgeLookup = new Dictionary<T, Edge<T, TActivity>>();
        private readonly Dictionary<T, Node<T, IEvent<T>>> m_NodeLookup = new Dictionary<T, Node<T, IEvent<T>>>();
        private readonly Dictionary<T, HashSet<Node<T, IEvent<T>>>> m_UnsatisfiedSuccessorsLookup = new Dictionary<T, HashSet<Node<T, IEvent<T>>>>();
        private readonly Dictionary<T, Node<T, IEvent<T>>> m_EdgeHeadNodeLookup = new Dictionary<T, Node<T, IEvent<T>>>();
        private readonly Dictionary<T, Node<T, IEvent<T>>> m_EdgeTailNodeLookup = new Dictionary<T, Node<T, IEvent<T>>>();

        #endregion

        #region Properties

        public Node<T, IEvent<T>> StartNode { get; set; }

        public Node<T, IEvent<T>> EndNode { get; set; }

        public IEnumerable<T> EdgeIds => m_EdgeLookup.Keys;

        public IEnumerable<T> NodeIds => m_NodeLookup.Keys;

        public IEnumerable<Edge<T, TActivity>> Edges => m_EdgeLookup.Values;

        public IEnumerable<Node<T, IEvent<T>>> Nodes => m_NodeLookup.Values;

        public IEnumerable<T> InvalidDependencies => m_UnsatisfiedSuccessorsLookup.Keys;

        public bool AllDependenciesSatisfied => m_UnsatisfiedSuccessorsLookup.Count == 0;

        public int EdgeCount => m_EdgeLookup.Count;

        public int NodeCount => m_NodeLookup.Count;

        // Activities live on edges in an arrow graph; events live on nodes.
        public IEnumerable<TActivity> Activities => m_EdgeLookup.Values.Select(x => x.Content);

        public IEnumerable<IEvent<T>> Events => m_NodeLookup.Values.Select(x => x.Content);

        public IEnumerable<Node<T, IEvent<T>>> StartNodes =>
            m_NodeLookup.Values.Where(x => x.NodeType == NodeType.Start);

        public IEnumerable<Node<T, IEvent<T>>> EndNodes =>
            m_NodeLookup.Values.Where(x => x.NodeType == NodeType.End);

        public IEnumerable<Node<T, IEvent<T>>> NormalNodes =>
            m_NodeLookup.Values.Where(x => x.NodeType == NodeType.Normal);

        public IEnumerable<Node<T, IEvent<T>>> IsolatedNodes =>
            m_NodeLookup.Values.Where(x => x.NodeType == NodeType.Isolated);

        #endregion

        #region Read API

        public bool ContainsEdge(T edgeId) => m_EdgeLookup.ContainsKey(edgeId);

        public bool ContainsNode(T nodeId) => m_NodeLookup.ContainsKey(nodeId);

        public Edge<T, TActivity> Edge(T edgeId)
        {
            m_EdgeLookup.TryGetValue(edgeId, out Edge<T, TActivity> edge);
            return edge;
        }

        public Node<T, IEvent<T>> Node(T nodeId)
        {
            m_NodeLookup.TryGetValue(nodeId, out Node<T, IEvent<T>> node);
            return node;
        }

        public Node<T, IEvent<T>> EdgeHeadNode(T edgeId)
        {
            m_EdgeHeadNodeLookup.TryGetValue(edgeId, out Node<T, IEvent<T>> node);
            return node;
        }

        public Node<T, IEvent<T>> EdgeTailNode(T edgeId)
        {
            m_EdgeTailNodeLookup.TryGetValue(edgeId, out Node<T, IEvent<T>> node);
            return node;
        }

        public bool TryGetEdge(T edgeId, out Edge<T, TActivity> edge) =>
            m_EdgeLookup.TryGetValue(edgeId, out edge);

        public bool TryGetNode(T nodeId, out Node<T, IEvent<T>> node) =>
            m_NodeLookup.TryGetValue(nodeId, out node);

        public bool TryGetEdgeHeadNode(T edgeId, out Node<T, IEvent<T>> node) =>
            m_EdgeHeadNodeLookup.TryGetValue(edgeId, out node);

        public bool TryGetEdgeTailNode(T edgeId, out Node<T, IEvent<T>> node) =>
            m_EdgeTailNodeLookup.TryGetValue(edgeId, out node);

        public bool TryGetUnsatisfiedSuccessors(T dependencyId, out HashSet<Node<T, IEvent<T>>> successors) =>
            m_UnsatisfiedSuccessorsLookup.TryGetValue(dependencyId, out successors);

        #endregion

        #region Mutation API

        public void AddEdge(Edge<T, TActivity> edge)
        {
            if (edge is null)
            {
                throw new ArgumentNullException(nameof(edge));
            }
            m_EdgeLookup.Add(edge.Id, edge);
        }

        public bool RemoveEdge(T edgeId) => m_EdgeLookup.Remove(edgeId);

        public void AddNode(Node<T, IEvent<T>> node)
        {
            if (node is null)
            {
                throw new ArgumentNullException(nameof(node));
            }
            m_NodeLookup.Add(node.Id, node);
        }

        public bool RemoveNode(T nodeId) => m_NodeLookup.Remove(nodeId);

        public void SetEdgeHeadNode(T edgeId, Node<T, IEvent<T>> node)
        {
            if (node is null)
            {
                throw new ArgumentNullException(nameof(node));
            }
            m_EdgeHeadNodeLookup.Add(edgeId, node);
        }

        public bool RemoveEdgeHeadNode(T edgeId) => m_EdgeHeadNodeLookup.Remove(edgeId);

        public void SetEdgeTailNode(T edgeId, Node<T, IEvent<T>> node)
        {
            if (node is null)
            {
                throw new ArgumentNullException(nameof(node));
            }
            m_EdgeTailNodeLookup.Add(edgeId, node);
        }

        public bool RemoveEdgeTailNode(T edgeId) => m_EdgeTailNodeLookup.Remove(edgeId);

        public void AddUnsatisfiedSuccessor(T dependencyId, Node<T, IEvent<T>> successor)
        {
            if (successor is null)
            {
                throw new ArgumentNullException(nameof(successor));
            }
            if (!m_UnsatisfiedSuccessorsLookup.TryGetValue(dependencyId, out HashSet<Node<T, IEvent<T>>> nodes))
            {
                nodes = new HashSet<Node<T, IEvent<T>>>();
                m_UnsatisfiedSuccessorsLookup.Add(dependencyId, nodes);
            }
            nodes.Add(successor);
        }

        public bool RemoveUnsatisfiedSuccessors(T dependencyId) =>
            m_UnsatisfiedSuccessorsLookup.Remove(dependencyId);

        public void Clear()
        {
            m_EdgeLookup.Clear();
            m_NodeLookup.Clear();
            m_UnsatisfiedSuccessorsLookup.Clear();
            m_EdgeHeadNodeLookup.Clear();
            m_EdgeTailNodeLookup.Clear();
            StartNode = null;
            EndNode = null;
        }

        // Validation helpers used by the graph-loading builder constructor.
        public bool EdgeKeysMatch(IEnumerable<T> otherKeys) =>
            m_EdgeLookup.Keys.OrderBy(x => x).SequenceEqual(otherKeys.OrderBy(x => x));

        public IEnumerable<T> EdgeHeadNodeKeys => m_EdgeHeadNodeLookup.Keys;

        public IEnumerable<T> EdgeTailNodeKeys => m_EdgeTailNodeLookup.Keys;

        public IEnumerable<Node<T, IEvent<T>>> EdgeHeadNodes => m_EdgeHeadNodeLookup.Values;

        public IEnumerable<Node<T, IEvent<T>>> EdgeTailNodes => m_EdgeTailNodeLookup.Values;

        #endregion
    }
}
