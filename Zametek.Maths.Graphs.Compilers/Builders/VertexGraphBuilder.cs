using System;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    public class VertexGraphBuilder<T, TActivity>
        : VertexGraphBuilderBase<T, TActivity, IEvent<T>>
        where TActivity : IActivity<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        #region Fields

        private static readonly Func<T, IEvent<T>> s_CreateEvent = (id) =>
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
            : base(edgeIdGenerator, nodeIdGenerator, s_CreateEvent)
        { }

        public VertexGraphBuilder(
            Graph<T, IEvent<T>, TActivity> graph,
            Func<T> edgeIdGenerator,
            Func<T> nodeIdGenerator)
            : base(
                  graph,
                  edgeIdGenerator,
                  nodeIdGenerator,
                  s_CreateEvent)
        {
            if (NormalNodes.Any())
            {
                // Check Start and End nodes.
                if (!StartNodes.Any())
                {
                    throw new ArgumentException(@"VertexGraph cannot contain Normal nodes without any Start nodes");
                }
                if (!EndNodes.Any())
                {
                    throw new ArgumentException(@"VertexGraph cannot contain Normal nodes without any End nodes");
                }
            }
        }

        #endregion

        #region Overrides

        public override object WorkingCopy()
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
