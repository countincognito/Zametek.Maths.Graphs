using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    // Adapts the read-only Arrow (Activity-on-Arrow) graph state to the IGraphTraversal
    // view consumed by the shared Tarjan algorithm. Activities are edges, so the
    // algorithm traverses edge-space.
    internal sealed class ArrowGraphTraversal<T, TResourceId, TWorkStreamId, TActivity>
        : IGraphTraversal<T>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
        where TActivity : class, IActivity<T, TResourceId, TWorkStreamId>
    {
        private readonly IArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> m_State;

        public ArrowGraphTraversal(IArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state)
        {
            m_State = state ?? throw new ArgumentNullException(nameof(state));
        }

        public IEnumerable<T> Keys => m_State.EdgeIds;

        public IEnumerable<T> PredecessorKeys(T referenceId)
        {
            // The predecessors of an edge are the incoming edges of its tail node.
            Node<T, IEvent<T>> tailNode = m_State.EdgeTailNode(referenceId);
            if (tailNode.NodeType == NodeType.End || tailNode.NodeType == NodeType.Normal)
            {
                return tailNode.IncomingEdges;
            }
            return Enumerable.Empty<T>();
        }

        public bool IsRemovable(T referenceId)
        {
            return m_State.Edge(referenceId).Content.CanBeRemoved;
        }
    }
}
