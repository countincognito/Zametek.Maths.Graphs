using System;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    public class ArrowGraphBuilder<T, TActivity>
        : ArrowGraphBuilderBase<T, TActivity, IEvent<T>>
        where TActivity : IActivity<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        #region Ctors

        public ArrowGraphBuilder(
            Func<T> edgeIdGenerator,
            Func<T> nodeIdGenerator)
            : base(
                  edgeIdGenerator,
                  nodeIdGenerator,
                  (id) => new Event<T>(id),
                  (id, earliestFinishTime, latestFinishTime) => new Event<T>(id, earliestFinishTime, latestFinishTime))
        { }

        public ArrowGraphBuilder(
            Graph<T, TActivity, IEvent<T>> graph,
            Func<T> edgeIdGenerator,
            Func<T> nodeIdGenerator)
            : base(graph, edgeIdGenerator, nodeIdGenerator)
        { }

        #endregion

        #region Private Methods

        #endregion

        #region Overrides

        protected override TActivity CreateDummyActivity(T id)
        {
            return (TActivity)Activity<T>.CreateActivityDummy(id);
        }

        public override object WorkingCopy()
        {
            Graph<T, TActivity, IEvent<T>> arrowGraphCopy = ToGraph();
            T minNodeId = arrowGraphCopy.Nodes.Select(x => x.Id).DefaultIfEmpty().Min();
            minNodeId = minNodeId.Previous();
            T minEdgeId = arrowGraphCopy.Edges.Select(x => x.Id).DefaultIfEmpty().Min();
            minEdgeId = minEdgeId.Previous();
            return new ArrowGraphBuilder<T, TActivity>(
                arrowGraphCopy,
                () => minEdgeId = minEdgeId.Previous(),
                () => minNodeId = minNodeId.Previous());
        }

        #endregion
    }
}
