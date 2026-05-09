using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    // Calculates critical path variables for Activity-on-Arrow graphs.
    // The engine receives the mutable graph state it needs via dictionaries and delegates.
    internal interface IArrowCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity, TEvent>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
        where TActivity : IActivity<T, TResourceId, TWorkStreamId>
        where TEvent : IEvent<T>
    {
        bool CalculateEventEarliestFinishTimes(
            IEnumerable<T> nodeIds,
            IDictionary<T, Edge<T, TActivity>> edgeLookup,
            IDictionary<T, Node<T, TEvent>> nodeLookup,
            IDictionary<T, Node<T, TEvent>> edgeHeadNodeLookup,
            IDictionary<T, Node<T, TEvent>> edgeTailNodeLookup,
            IEnumerable<IInvalidConstraint<T>> invalidConstraints,
            Node<T, TEvent> startNode,
            Node<T, TEvent> endNode,
            bool shuffle);

        bool CalculateEventLatestFinishTimes(
            IEnumerable<T> nodeIds,
            IDictionary<T, Edge<T, TActivity>> edgeLookup,
            IDictionary<T, Node<T, TEvent>> nodeLookup,
            IDictionary<T, Node<T, TEvent>> edgeHeadNodeLookup,
            IDictionary<T, Node<T, TEvent>> edgeTailNodeLookup,
            IEnumerable<IInvalidConstraint<T>> invalidConstraints,
            Node<T, TEvent> endNode,
            bool shuffle);

        bool CalculateCriticalPathVariables(
            IEnumerable<T> edgeIds,
            IDictionary<T, Edge<T, TActivity>> edgeLookup,
            IDictionary<T, Node<T, TEvent>> edgeHeadNodeLookup,
            IDictionary<T, Node<T, TEvent>> edgeTailNodeLookup,
            IEnumerable<IInvalidConstraint<T>> invalidConstraints,
            IEnumerable<TEvent> events);
    }
}
