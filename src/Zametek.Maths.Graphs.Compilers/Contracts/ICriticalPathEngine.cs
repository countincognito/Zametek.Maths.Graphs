using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    // Calculates critical path variables for Activity-on-Arrow graphs.
    // The engine operates on the shared ArrowGraphState supplied to its methods,
    // plus the in-flight constraint list for the current pass.
    internal interface IArrowCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
        where TActivity : class, IActivity<T, TResourceId, TWorkStreamId>
    {
        bool CalculateEventEarliestFinishTimes(
            ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            IEnumerable<IInvalidConstraint<T>> invalidConstraints,
            bool shuffle);

        bool CalculateEventLatestFinishTimes(
            ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            IEnumerable<IInvalidConstraint<T>> invalidConstraints,
            bool shuffle);

        bool CalculateCriticalPathVariables(
            ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            IEnumerable<IInvalidConstraint<T>> invalidConstraints);
    }

    // Calculates critical path variables for Activity-on-Vertex graphs.
    internal interface IVertexCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
        where TActivity : IActivity<T, TResourceId, TWorkStreamId>
    {
        bool CalculateCriticalPathForwardFlow(
            VertexGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            IEnumerable<IInvalidConstraint<T>> invalidConstraints,
            bool shuffle);

        bool CalculateCriticalPathBackwardFlow(
            VertexGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            IEnumerable<IInvalidConstraint<T>> invalidConstraints,
            bool shuffle);

        bool BackFillIsolatedNodes(
            VertexGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            IEnumerable<IInvalidConstraint<T>> invalidConstraints);
    }
}
