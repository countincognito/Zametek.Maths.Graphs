﻿using System;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    public class ArrowGraphBuilder<T, TActivity>
        : ArrowGraphBuilderBase<T, TActivity, IEvent<T>>
        where TActivity : IActivity<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        #region Fields

        private static readonly Func<T, IEvent<T>> s_CreateEvent = (id) => new Event<T>(id);
        private static readonly Func<T, int?, int?, IEvent<T>> s_CreateEventWithTimes = (id, earliestFinishTime, latestFinishTime) => new Event<T>(id, earliestFinishTime, latestFinishTime);
        private static readonly Func<T, TActivity> s_CreateDummyActivity = (id) => (TActivity)Activity<T>.CreateActivityDummy(id);

        #endregion

        #region Ctors

        public ArrowGraphBuilder(
            Func<T> edgeIdGenerator,
            Func<T> nodeIdGenerator)
            : base(
                  edgeIdGenerator,
                  nodeIdGenerator,
                  s_CreateEvent,
                  s_CreateEventWithTimes,
                  s_CreateDummyActivity)
        { }

        public ArrowGraphBuilder(
            Graph<T, TActivity, IEvent<T>> graph,
            Func<T> edgeIdGenerator,
            Func<T> nodeIdGenerator)
            : base(
                  graph,
                  edgeIdGenerator,
                  nodeIdGenerator,
                  s_CreateEvent)
        { }

        #endregion

        #region Overrides

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