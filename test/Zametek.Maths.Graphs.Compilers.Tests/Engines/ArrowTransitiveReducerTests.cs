using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class ArrowTransitiveReducerTests
    {
        [Fact]
        public void ArrowTransitiveReducer_GivenCtorCalledWithNullFindStrongCircularDependencies_ThenThrowsArgumentNullException()
        {
            var state = new ArrowGraphState<int, int, int, Activity<int, int, int>>();
            IDummyEdgeOrchestrator<int, int, int, Activity<int, int, int>> orchestrator =
                BuildOrchestrator(state);

            Action act = () => new ArrowTransitiveReducer<int, int, int, Activity<int, int, int>>(
                null, () => [], orchestrator, state);
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void ArrowTransitiveReducer_GivenCtorCalledWithNullGetEndNodeIds_ThenThrowsArgumentNullException()
        {
            var state = new ArrowGraphState<int, int, int, Activity<int, int, int>>();
            IDummyEdgeOrchestrator<int, int, int, Activity<int, int, int>> orchestrator =
                BuildOrchestrator(state);

            Action act = () => new ArrowTransitiveReducer<int, int, int, Activity<int, int, int>>(
                () => new List<ICircularDependency<int>>(), null, orchestrator, state);
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void ArrowTransitiveReducer_GivenCtorCalledWithNullDummyEdgeOrchestrator_ThenThrowsArgumentNullException()
        {
            var state = new ArrowGraphState<int, int, int, Activity<int, int, int>>();
            Action act = () => new ArrowTransitiveReducer<int, int, int, Activity<int, int, int>>(
                () => new List<ICircularDependency<int>>(), () => [], null, state);
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void ArrowTransitiveReducer_GivenCtorCalledWithNullState_ThenThrowsArgumentNullException()
        {
            var state = new ArrowGraphState<int, int, int, Activity<int, int, int>>();
            IDummyEdgeOrchestrator<int, int, int, Activity<int, int, int>> orchestrator =
                BuildOrchestrator(state);

            Action act = () => new ArrowTransitiveReducer<int, int, int, Activity<int, int, int>>(
                () => new List<ICircularDependency<int>>(), () => [], orchestrator, null);
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void ArrowTransitiveReducer_GivenGetAncestorNodesLookup_WithUnsatisfiedDependencies_ThenReturnsNull()
        {
            var state = BuildLinearArrowState();
            var dependentNode = new Node<int, IEvent<int>>(NodeType.Normal, new Event<int>(99));
            state.AddUnsatisfiedSuccessor(7777, dependentNode);

            var reducer = new ArrowTransitiveReducer<int, int, int, Activity<int, int, int>>(
                () => [],
                () => [state.EndNode.Id],
                BuildOrchestrator(state),
                state);

            IDictionary<int, HashSet<int>> output = reducer.GetAncestorNodesLookup();

            output.ShouldBeNull();
        }

        [Fact]
        public void ArrowTransitiveReducer_GivenGetAncestorNodesLookup_WithCircularDependencies_ThenReturnsNull()
        {
            var state = BuildLinearArrowState();
            var reducer = new ArrowTransitiveReducer<int, int, int, Activity<int, int, int>>(
                () => [new CircularDependency<int>([1, 2])],
                () => [state.EndNode.Id],
                BuildOrchestrator(state),
                state);

            IDictionary<int, HashSet<int>> output = reducer.GetAncestorNodesLookup();

            output.ShouldBeNull();
        }

        [Fact]
        public void ArrowTransitiveReducer_GivenGetAncestorNodesLookup_WithLinearGraph_ThenReturnsCorrectAncestorLookup()
        {
            var state = BuildLinearArrowState();
            var reducer = new ArrowTransitiveReducer<int, int, int, Activity<int, int, int>>(
                () => [],
                () => [state.EndNode.Id],
                BuildOrchestrator(state),
                state);

            IDictionary<int, HashSet<int>> output = reducer.GetAncestorNodesLookup();

            output.ShouldNotBeNull();
            output[state.EndNode.Id].ShouldContain(state.StartNode.Id);
            output[state.EndNode.Id].ShouldContain(2);
        }

        [Fact]
        public void ArrowTransitiveReducer_GivenReduceGraph_WithUnsatisfiedDependencies_ThenReturnsFalse()
        {
            var state = BuildLinearArrowState();
            var dependentNode = new Node<int, IEvent<int>>(NodeType.Normal, new Event<int>(99));
            state.AddUnsatisfiedSuccessor(7777, dependentNode);

            var reducer = new ArrowTransitiveReducer<int, int, int, Activity<int, int, int>>(
                () => [],
                () => [state.EndNode.Id],
                BuildOrchestrator(state),
                state);

            bool result = reducer.ReduceGraph();

            result.ShouldBeFalse();
        }

        [Fact]
        public void ArrowTransitiveReducer_GivenReduceGraph_WithValidGraph_ThenReturnsTrue()
        {
            var state = BuildLinearArrowState();
            var reducer = new ArrowTransitiveReducer<int, int, int, Activity<int, int, int>>(
                () => [],
                () => [state.EndNode.Id],
                BuildOrchestrator(state),
                state);

            bool result = reducer.ReduceGraph();

            result.ShouldBeTrue();
        }

        private static ArrowGraphState<int, int, int, Activity<int, int, int>> BuildLinearArrowState()
        {
            var state = new ArrowGraphState<int, int, int, Activity<int, int, int>>();

            var startNode = new Node<int, IEvent<int>>(NodeType.Start, new Event<int>(1));
            var middleNode = new Node<int, IEvent<int>>(NodeType.Normal, new Event<int>(2));
            var endNode = new Node<int, IEvent<int>>(NodeType.End, new Event<int>(3));
            state.AddNode(startNode);
            state.AddNode(middleNode);
            state.AddNode(endNode);
            state.StartNode = startNode;
            state.EndNode = endNode;

            AddEdge(state, 11, startNode, middleNode, duration: 3);
            AddEdge(state, 12, middleNode, endNode, duration: 2);

            return state;
        }

        private static IDummyEdgeOrchestrator<int, int, int, Activity<int, int, int>> BuildOrchestrator(
            ArrowGraphState<int, int, int, Activity<int, int, int>> state)
        {
            int nextId = 9000;
            return new DummyEdgeOrchestrator<int, int, int, Activity<int, int, int>>(
                () => nextId++,
                id => new Activity<int, int, int>(id, 0, canBeRemoved: true),
                () => [],
                state);
        }

        private static void AddEdge(
            ArrowGraphState<int, int, int, Activity<int, int, int>> state,
            int edgeId, Node<int, IEvent<int>> tailNode, Node<int, IEvent<int>> headNode,
            int duration)
        {
            var activity = new Activity<int, int, int>(edgeId, duration);
            var edge = new Edge<int, Activity<int, int, int>>(activity);
            state.AddEdge(edge);
            tailNode.OutgoingEdges.Add(edgeId);
            headNode.IncomingEdges.Add(edgeId);
            state.SetEdgeTailNode(edgeId, tailNode);
            state.SetEdgeHeadNode(edgeId, headNode);
        }
    }
}
