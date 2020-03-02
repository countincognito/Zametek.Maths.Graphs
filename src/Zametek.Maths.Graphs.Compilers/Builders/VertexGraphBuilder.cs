using System;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    public sealed class VertexGraphBuilder<T, TActivity>
        : VertexGraphBuilderBase<T, TActivity, IEvent<T>>
        where TActivity : IActivity<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        #region Fields

        private static readonly Func<T, IEvent<T>> s_EventGenerator = (id) =>
        {
            var output = new Event<T>(id);
            output.SetAsRemovable();
            return output;
        };

        #endregion

        #region Ctors

        public VertexGraphBuilder(
            Func<T> edgeIdGenerator,
            Func<T> nodeIdGenerator)
            : base(edgeIdGenerator, nodeIdGenerator, s_EventGenerator)
        {
        }

        public VertexGraphBuilder(
            Graph<T, IEvent<T>, TActivity> graph,
            Func<T> edgeIdGenerator,
            Func<T> nodeIdGenerator)
            : base(
                  graph,
                  edgeIdGenerator,
                  nodeIdGenerator,
                  s_EventGenerator)
        {
            if (NormalNodes.Any())
            {
                // Check Start and End nodes.
                if (!StartNodes.Any())
                {
                    throw new ArgumentException(Properties.Resources.VertexGraphCannotContainNormalNodesWithoutAnyStartNodes);
                }
                if (!EndNodes.Any())
                {
                    throw new ArgumentException(Properties.Resources.VertexGraphCannotContainNormalNodesWithoutAnyEndNodes);
                }
            }
        }

        #endregion

        #region Overrides

        public override object CloneObject()
        {
            Graph<T, IEvent<T>, TActivity> vertexGraphCopy = ToGraph();
            T minNodeId = vertexGraphCopy.Nodes.Select(x => x.Id).DefaultIfEmpty().Min();
            minNodeId = minNodeId.Previous();
            T minEdgeId = vertexGraphCopy.Edges.Select(x => x.Id).DefaultIfEmpty().Min();
            minEdgeId = minEdgeId.Previous();
            return new VertexGraphBuilder<T, TActivity>(
                vertexGraphCopy,
                () => minEdgeId = minEdgeId.Previous(),
                () => minNodeId = minNodeId.Previous());
        }

        #endregion
    }
}
