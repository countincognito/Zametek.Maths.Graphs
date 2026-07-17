using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    // Adapts the read-only Vertex (Activity-on-Vertex) graph state to the IGraphTraversal
    // view consumed by the shared Tarjan algorithm. Activities are nodes, so the
    // algorithm traverses node-space.
    internal sealed class VertexGraphTraversal<T, TResourceId, TWorkStreamId, TActivity>
        : IGraphTraversal<T>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
        where TActivity : IActivity<T, TResourceId, TWorkStreamId>
    {
        private readonly IVertexGraphState<T, TResourceId, TWorkStreamId, TActivity> m_State;

        public VertexGraphTraversal(IVertexGraphState<T, TResourceId, TWorkStreamId, TActivity> state)
        {
            m_State = state ?? throw new ArgumentNullException(nameof(state));
        }

        public IEnumerable<T> Keys => m_State.NodeIds;

        public IEnumerable<T> PredecessorKeys(T referenceId)
        {
            // The predecessors of a node are the tail nodes of its incoming edges.
            Node<T, TActivity> referenceNode = m_State.Node(referenceId);
            if (referenceNode.NodeType == NodeType.End || referenceNode.NodeType == NodeType.Normal)
            {
                return referenceNode.IncomingEdges.Select(x => m_State.EdgeTailNode(x).Id);
            }
            return Enumerable.Empty<T>();
        }

        public bool IsRemovable(T referenceId)
        {
            return m_State.Node(referenceId).Content.CanBeRemoved;
        }
    }
}
