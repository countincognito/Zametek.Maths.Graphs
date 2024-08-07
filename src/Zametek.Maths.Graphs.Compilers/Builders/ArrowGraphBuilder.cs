﻿using System;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    public sealed class ArrowGraphBuilder<T, TResourceId, TWorkStreamId, TActivity>
        : ArrowGraphBuilderBase<T, TResourceId, TWorkStreamId, TActivity, IEvent<T>>
        where TActivity : class, IActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        #region Fields

        private static readonly Func<T, IEvent<T>> s_EventGenerator = (id) => new Event<T>(id);
        private static readonly Func<T, int?, int?, IEvent<T>> s_EventGeneratorWithTimes = (id, earliestFinishTime, latestFinishTime) => new Event<T>(id, earliestFinishTime, latestFinishTime);
        private static readonly Func<T, TActivity> s_DummyActivityGenerator = (id) => new Activity<T, TResourceId, TWorkStreamId>(id, 0, canBeRemoved: true) as TActivity;

        #endregion

        #region Ctors

        public ArrowGraphBuilder(
            Func<T> edgeIdGenerator,
            Func<T> nodeIdGenerator)
            : base(
                  edgeIdGenerator,
                  nodeIdGenerator,
                  s_EventGenerator,
                  s_EventGeneratorWithTimes,
                  s_DummyActivityGenerator)
        {
        }

        public ArrowGraphBuilder(
            Graph<T, TActivity, IEvent<T>> graph,
            Func<T> edgeIdGenerator,
            Func<T> nodeIdGenerator)
            : base(
                  graph,
                  edgeIdGenerator,
                  nodeIdGenerator,
                  s_EventGenerator)
        {
        }

        #endregion

        #region Overrides

        public override object CloneObject()
        {
            Graph<T, TActivity, IEvent<T>> arrowGraphCopy = ToGraph();
            T minNodeId = arrowGraphCopy.Nodes.Select(x => x.Id).DefaultIfEmpty().Min();
            minNodeId = minNodeId.Previous();
            T minEdgeId = arrowGraphCopy.Edges.Select(x => x.Id).DefaultIfEmpty().Min();
            minEdgeId = minEdgeId.Previous();
            return new ArrowGraphBuilder<T, TResourceId, TWorkStreamId, TActivity>(
                arrowGraphCopy,
                () => minEdgeId = minEdgeId.Previous(),
                () => minNodeId = minNodeId.Previous());
        }

        #endregion
    }
}
