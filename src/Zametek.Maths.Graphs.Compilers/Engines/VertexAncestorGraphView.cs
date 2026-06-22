using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    // Adapts the read-only Vertex graph state to the node-space IAncestorGraphView used by
    // the shared ancestor-node calculation.
    internal sealed class VertexAncestorGraphView<T, TResourceId, TWorkStreamId, TActivity>
        : IAncestorGraphView<T>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
        where TActivity : IActivity<T, TResourceId, TWorkStreamId>
    {
        private readonly IVertexGraphState<T, TResourceId, TWorkStreamId, TActivity> m_State;

        public VertexAncestorGraphView(IVertexGraphState<T, TResourceId, TWorkStreamId, TActivity> state)
        {
            m_State = state ?? throw new ArgumentNullException(nameof(state));
        }

        public IEnumerable<T> EndNodeIds => m_State.EndNodes.Select(x => x.Id);

        public bool IsRootNode(T nodeId)
        {
            Node<T, TActivity> node = m_State.Node(nodeId);
            return node.NodeType == NodeType.Start || node.NodeType == NodeType.Isolated;
        }

        public IEnumerable<T> ParentNodeIds(T nodeId)
        {
            return m_State.Node(nodeId).IncomingEdges.Select(x => m_State.EdgeTailNode(x).Id);
        }
    }
}
