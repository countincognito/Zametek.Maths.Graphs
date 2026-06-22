using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    // Adapts the read-only Arrow graph state to the node-space IAncestorGraphView used by
    // the shared ancestor-node calculation.
    internal sealed class ArrowAncestorGraphView<T, TResourceId, TWorkStreamId, TActivity>
        : IAncestorGraphView<T>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
        where TActivity : class, IActivity<T, TResourceId, TWorkStreamId>
    {
        private readonly IArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> m_State;

        public ArrowAncestorGraphView(IArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state)
        {
            m_State = state ?? throw new ArgumentNullException(nameof(state));
        }

        public IEnumerable<T> EndNodeIds => m_State.EndNodes.Select(x => x.Id);

        public bool IsRootNode(T nodeId)
        {
            Node<T, IEvent<T>> node = m_State.Node(nodeId);
            return node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated;
        }

        public IEnumerable<T> ParentNodeIds(T nodeId)
        {
            return m_State.Node(nodeId).IncomingEdges.Select(x => m_State.EdgeTailNode(x).Id);
        }
    }
}
