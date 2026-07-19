using Shouldly;
using System;
using System.Collections.Generic;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class VertexTransitiveReducerTests
    {
        // The reducer is stateless: the graph state and SCC finder are supplied per
        // call rather than bound at construction.
        private static VertexTarjanStronglyConnectedComponentsFinder<int, int, int, Activity<int, int, int>> SccFinder() =>
            new VertexTarjanStronglyConnectedComponentsFinder<int, int, int, Activity<int, int, int>>();

        [Fact]
        public void VertexTransitiveReducer_GivenReduceGraphWithNullState_ThenThrowsArgumentNullException()
        {
            var reducer = new VertexTransitiveReducer<int, int, int, Activity<int, int, int>>();
            Action act = () => reducer.ReduceGraph(null, SccFinder());
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void VertexTransitiveReducer_GivenReduceGraphWithNullStronglyConnectedComponentsFinder_ThenThrowsArgumentNullException()
        {
            var state = BuildLinearVertexState();
            var reducer = new VertexTransitiveReducer<int, int, int, Activity<int, int, int>>();
            Action act = () => reducer.ReduceGraph(state, null);
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void VertexTransitiveReducer_GivenGetAncestorNodesLookup_WithUnsatisfiedDependencies_ThenReturnsNull()
        {
            var state = BuildLinearVertexState();
            var dependentNode = new Node<int, Activity<int, int, int>>(NodeType.Normal, new Activity<int, int, int>(99, 1));
            state.AddUnsatisfiedSuccessor(7777, dependentNode);

            var reducer = new VertexTransitiveReducer<int, int, int, Activity<int, int, int>>();

            IDictionary<int, HashSet<int>> output = reducer.GetAncestorNodesLookup(state, SccFinder());

            output.ShouldBeNull();
        }

        [Fact]
        public void VertexTransitiveReducer_GivenGetAncestorNodesLookup_WithCircularDependencies_ThenReturnsNull()
        {
            var state = BuildLinearVertexStateWithCircularDependency();
            var reducer = new VertexTransitiveReducer<int, int, int, Activity<int, int, int>>();

            IDictionary<int, HashSet<int>> output = reducer.GetAncestorNodesLookup(state, SccFinder());

            output.ShouldBeNull();
        }

        [Fact]
        public void VertexTransitiveReducer_GivenGetAncestorNodesLookup_WithLinearGraph_ThenReturnsCorrectAncestorLookup()
        {
            var state = BuildLinearVertexState();
            var reducer = new VertexTransitiveReducer<int, int, int, Activity<int, int, int>>();

            Dictionary<int, HashSet<int>> output = reducer.GetAncestorNodesLookup(state, SccFinder());

            output.ShouldNotBeNull();
            output[3].ShouldContain(1);
            output[3].ShouldContain(2);
        }

        [Fact]
        public void VertexTransitiveReducer_GivenReduceGraph_WithUnsatisfiedDependencies_ThenReturnsFalse()
        {
            var state = BuildLinearVertexState();
            var dependentNode = new Node<int, Activity<int, int, int>>(NodeType.Normal, new Activity<int, int, int>(99, 1));
            state.AddUnsatisfiedSuccessor(7777, dependentNode);

            var reducer = new VertexTransitiveReducer<int, int, int, Activity<int, int, int>>();

            bool result = reducer.ReduceGraph(state, SccFinder());

            result.ShouldBeFalse();
        }

        [Fact]
        public void VertexTransitiveReducer_GivenReduceGraph_WithRedundantEdge_ThenRemovesRedundantEdge()
        {
            var state = BuildVertexStateWithRedundantEdge();
            int initialEdgeCount = state.EdgeCount;

            var reducer = new VertexTransitiveReducer<int, int, int, Activity<int, int, int>>();

            bool result = reducer.ReduceGraph(state, SccFinder());

            result.ShouldBeTrue();
            state.EdgeCount.ShouldBeLessThan(initialEdgeCount);
        }

        private static VertexGraphState<int, int, int, Activity<int, int, int>> BuildLinearVertexState()
        {
            var state = new VertexGraphState<int, int, int, Activity<int, int, int>>();

            var n1 = new Node<int, Activity<int, int, int>>(NodeType.Start, new Activity<int, int, int>(1, 1));
            var n2 = new Node<int, Activity<int, int, int>>(NodeType.Normal, new Activity<int, int, int>(2, 1));
            var n3 = new Node<int, Activity<int, int, int>>(NodeType.End, new Activity<int, int, int>(3, 1));
            state.AddNode(n1);
            state.AddNode(n2);
            state.AddNode(n3);

            AddEdge(state, 100, n1, n2);
            AddEdge(state, 101, n2, n3);

            return state;
        }

        private static VertexGraphState<int, int, int, Activity<int, int, int>> BuildLinearVertexStateWithCircularDependency()
        {
            var state = new VertexGraphState<int, int, int, Activity<int, int, int>>();

            var n1 = new Node<int, Activity<int, int, int>>(NodeType.Start, new Activity<int, int, int>(1, 1));
            var n2 = new Node<int, Activity<int, int, int>>(NodeType.Normal, new Activity<int, int, int>(2, 1));
            var n3 = new Node<int, Activity<int, int, int>>(NodeType.Normal, new Activity<int, int, int>(3, 1));
            var n4 = new Node<int, Activity<int, int, int>>(NodeType.End, new Activity<int, int, int>(4, 1));
            state.AddNode(n1);
            state.AddNode(n2);
            state.AddNode(n3);
            state.AddNode(n4);

            AddEdge(state, 100, n1, n2);
            AddEdge(state, 101, n2, n3);
            AddEdge(state, 102, n3, n2);
            AddEdge(state, 103, n3, n4);

            return state;
        }

        private static VertexGraphState<int, int, int, Activity<int, int, int>> BuildVertexStateWithRedundantEdge()
        {
            // Three nodes in a chain: 1 -> 2 -> 3 PLUS a redundant edge 1 -> 3.
            // The edge 1 -> 3 should be removed by the reduction.
            var state = new VertexGraphState<int, int, int, Activity<int, int, int>>();

            var n1 = new Node<int, Activity<int, int, int>>(NodeType.Start, new Activity<int, int, int>(1, 1, canBeRemoved: true));
            var n2 = new Node<int, Activity<int, int, int>>(NodeType.Normal, new Activity<int, int, int>(2, 1, canBeRemoved: true));
            var n3 = new Node<int, Activity<int, int, int>>(NodeType.End, new Activity<int, int, int>(3, 1, canBeRemoved: true));
            state.AddNode(n1);
            state.AddNode(n2);
            state.AddNode(n3);

            AddEdge(state, 100, n1, n2, removable: true);
            AddEdge(state, 101, n2, n3, removable: true);
            AddEdge(state, 102, n1, n3, removable: true);

            return state;
        }

        private static void AddEdge(
            VertexGraphState<int, int, int, Activity<int, int, int>> state,
            int edgeId,
            Node<int, Activity<int, int, int>> tailNode,
            Node<int, Activity<int, int, int>> headNode,
            bool removable = false)
        {
            var ev = new Event<int>(edgeId);
            if (removable)
            {
                ev.SetAsRemovable();
            }
            var edge = new Edge<int, IEvent<int>>(ev);
            state.AddEdge(edge);
            tailNode.OutgoingEdges.Add(edgeId);
            headNode.IncomingEdges.Add(edgeId);
            state.SetEdgeTailNode(edgeId, tailNode);
            state.SetEdgeHeadNode(edgeId, headNode);
        }
    }
}
