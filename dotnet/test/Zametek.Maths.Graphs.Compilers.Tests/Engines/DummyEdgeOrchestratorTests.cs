using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class DummyEdgeOrchestratorTests
    {
        [Fact]
        public void DummyEdgeOrchestrator_GivenCtorCalledWithNullEdgeIdGenerator_ThenThrowsArgumentNullException()
        {
            var state = new ArrowGraphState<int, int, int, Activity<int, int, int>>();
            Action act = () => new DummyEdgeOrchestrator<int, int, int, Activity<int, int, int>>(
                null,
                new DummyActivityGenerator<int, int, int, Activity<int, int, int>>(),
                new ArrowTarjanStronglyConnectedComponentsFinder<int, int, int, Activity<int, int, int>>(),
                state);
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void DummyEdgeOrchestrator_GivenCtorCalledWithNullDummyActivityGenerator_ThenThrowsArgumentNullException()
        {
            var state = new ArrowGraphState<int, int, int, Activity<int, int, int>>();
            Action act = () => new DummyEdgeOrchestrator<int, int, int, Activity<int, int, int>>(
                new NextIdGenerator<int>(1),
                null,
                new ArrowTarjanStronglyConnectedComponentsFinder<int, int, int, Activity<int, int, int>>(),
                state);
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void DummyEdgeOrchestrator_GivenCtorCalledWithNullFindStrongCircularDependencies_ThenThrowsArgumentNullException()
        {
            var state = new ArrowGraphState<int, int, int, Activity<int, int, int>>();
            Action act = () => new DummyEdgeOrchestrator<int, int, int, Activity<int, int, int>>(
                new NextIdGenerator<int>(1),
                new DummyActivityGenerator<int, int, int, Activity<int, int, int>>(),
                null,
                state);
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void DummyEdgeOrchestrator_GivenCtorCalledWithNullState_ThenThrowsArgumentNullException()
        {
            Action act = () => new DummyEdgeOrchestrator<int, int, int, Activity<int, int, int>>(
                new NextIdGenerator<int>(1),
                new DummyActivityGenerator<int, int, int, Activity<int, int, int>>(),
                new ArrowTarjanStronglyConnectedComponentsFinder<int, int, int, Activity<int, int, int>>(),
                null);
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void DummyEdgeOrchestrator_GivenConnectWithDummyEdge_ThenAddsDummyEdgeBetweenNodes()
        {
            var state = new ArrowGraphState<int, int, int, Activity<int, int, int>>();
            var tail = new Node<int, IEvent<int>>(NodeType.Normal, new Event<int>(1));
            var head = new Node<int, IEvent<int>>(NodeType.Normal, new Event<int>(2));
            state.AddNode(tail);
            state.AddNode(head);

            const int dummyEdgeId = 555;
            var orchestrator = BuildOrchestrator(state, new NextIdGenerator<int>(dummyEdgeId - 1));

            orchestrator.ConnectWithDummyEdge(tail, head);

            state.ContainsEdge(dummyEdgeId).ShouldBeTrue();
            state.Edge(dummyEdgeId).Content.IsDummy.ShouldBeTrue();
            state.Edge(dummyEdgeId).Content.CanBeRemoved.ShouldBeTrue();
            tail.OutgoingEdges.ShouldContain(dummyEdgeId);
            head.IncomingEdges.ShouldContain(dummyEdgeId);
            state.EdgeTailNode(dummyEdgeId).Id.ShouldBe(tail.Id);
            state.EdgeHeadNode(dummyEdgeId).Id.ShouldBe(head.Id);
        }

        [Fact]
        public void DummyEdgeOrchestrator_GivenRemoveDummyActivity_WithNonExistentActivity_ThenReturnsFalse()
        {
            var state = new ArrowGraphState<int, int, int, Activity<int, int, int>>();
            var orchestrator = BuildOrchestrator(state, new NextIdGenerator<int>(100));

            bool result = orchestrator.RemoveDummyActivity(999);

            result.ShouldBeFalse();
        }

        [Fact]
        public void DummyEdgeOrchestrator_GivenRemoveDummyActivity_WithNonDummyActivity_ThenReturnsFalse()
        {
            var state = new ArrowGraphState<int, int, int, Activity<int, int, int>>();
            var tail = new Node<int, IEvent<int>>(NodeType.Normal, new Event<int>(1));
            var head = new Node<int, IEvent<int>>(NodeType.Normal, new Event<int>(2));
            state.AddNode(tail);
            state.AddNode(head);

            int edgeId = 100;
            var activity = new Activity<int, int, int>(edgeId, 5);
            var edge = new Edge<int, Activity<int, int, int>>(activity);
            state.AddEdge(edge);
            tail.OutgoingEdges.Add(edgeId);
            head.IncomingEdges.Add(edgeId);
            state.SetEdgeTailNode(edgeId, tail);
            state.SetEdgeHeadNode(edgeId, head);

            var orchestrator = BuildOrchestrator(state, new NextIdGenerator<int>(200));

            bool result = orchestrator.RemoveDummyActivity(edgeId);

            result.ShouldBeFalse();
        }

        [Fact]
        public void DummyEdgeOrchestrator_GivenRemoveDummyActivity_WithNonRemovableDummy_ThenReturnsFalse()
        {
            var state = new ArrowGraphState<int, int, int, Activity<int, int, int>>();
            var tail = new Node<int, IEvent<int>>(NodeType.Normal, new Event<int>(1));
            var head = new Node<int, IEvent<int>>(NodeType.Normal, new Event<int>(2));
            state.AddNode(tail);
            state.AddNode(head);

            int edgeId = 100;
            var activity = new Activity<int, int, int>(edgeId, 0, canBeRemoved: false);
            var edge = new Edge<int, Activity<int, int, int>>(activity);
            state.AddEdge(edge);
            tail.OutgoingEdges.Add(edgeId);
            head.IncomingEdges.Add(edgeId);
            state.SetEdgeTailNode(edgeId, tail);
            state.SetEdgeHeadNode(edgeId, head);

            var orchestrator = BuildOrchestrator(state, new NextIdGenerator<int>(200));

            bool result = orchestrator.RemoveDummyActivity(edgeId);

            result.ShouldBeFalse();
        }

        [Fact]
        public void DummyEdgeOrchestrator_GivenGetDummyEdgesInDescendingOrder_WithMixedEdges_ThenReturnsOnlyDummies()
        {
            var state = BuildArrowStateWithDummies();
            var orchestrator = BuildOrchestrator(state, new NextIdGenerator<int>(999));

            IList<Edge<int, Activity<int, int, int>>> output = orchestrator.GetDummyEdgesInDescendingOrder();

            output.All(x => x.Content.IsDummy).ShouldBeTrue();
        }

        [Fact]
        public void DummyEdgeOrchestrator_GivenRedirectDummyEdges_WithUnsatisfiedDependencies_ThenReturnsFalse()
        {
            var state = BuildArrowStateWithDummies();
            var dependentNode = new Node<int, IEvent<int>>(NodeType.Normal, new Event<int>(99));
            state.AddUnsatisfiedSuccessor(7777, dependentNode);

            var orchestrator = BuildOrchestrator(state, new NextIdGenerator<int>(999));

            bool result = orchestrator.RedirectDummyEdges();

            result.ShouldBeFalse();
        }

        [Fact]
        public void DummyEdgeOrchestrator_GivenRedirectDummyEdges_WithCircularDependencies_ThenReturnsFalse()
        {
            var state = BuildArrowStateWithDummiesAndCircularDependency();
            var orchestrator = new DummyEdgeOrchestrator<int, int, int, Activity<int, int, int>>(
                new NextIdGenerator<int>(999),
                new DummyActivityGenerator<int, int, int, Activity<int, int, int>>(),
                new ArrowTarjanStronglyConnectedComponentsFinder<int, int, int, Activity<int, int, int>>(),
                state);

            bool result = orchestrator.RedirectDummyEdges();

            result.ShouldBeFalse();
        }

        [Fact]
        public void DummyEdgeOrchestrator_GivenRemoveRedundantDummyEdges_WithUnsatisfiedDependencies_ThenReturnsFalse()
        {
            var state = BuildArrowStateWithDummies();
            var dependentNode = new Node<int, IEvent<int>>(NodeType.Normal, new Event<int>(99));
            state.AddUnsatisfiedSuccessor(7777, dependentNode);

            var orchestrator = BuildOrchestrator(state, new NextIdGenerator<int>(999));

            bool result = orchestrator.RemoveRedundantDummyEdges();

            result.ShouldBeFalse();
        }

        [Fact]
        public void DummyEdgeOrchestrator_GivenRemoveRedundantDummyEdges_WithCircularDependencies_ThenReturnsFalse()
        {
            var state = BuildArrowStateWithDummiesAndCircularDependency();
            var orchestrator = new DummyEdgeOrchestrator<int, int, int, Activity<int, int, int>>(
                new NextIdGenerator<int>(999),
                new DummyActivityGenerator<int, int, int, Activity<int, int, int>>(),
                new ArrowTarjanStronglyConnectedComponentsFinder<int, int, int, Activity<int, int, int>>(),
                state);

            bool result = orchestrator.RemoveRedundantDummyEdges();

            result.ShouldBeFalse();
        }

        [Fact]
        public void DummyEdgeOrchestrator_GivenRemoveRedundantIncomingDummyEdges_WithNullLookup_ThenThrowsArgumentNullException()
        {
            var state = BuildArrowStateWithDummies();
            var orchestrator = BuildOrchestrator(state, new NextIdGenerator<int>(999));

            Action act = () => orchestrator.RemoveRedundantIncomingDummyEdges(state.EndNode.Id, null);
            act.ShouldThrow<ArgumentNullException>();
        }

        private static DummyEdgeOrchestrator<int, int, int, Activity<int, int, int>> BuildOrchestrator(
            ArrowGraphState<int, int, int, Activity<int, int, int>> state,
            IIdGenerator<int> edgeIdGenerator)
        {
            return new DummyEdgeOrchestrator<int, int, int, Activity<int, int, int>>(
                edgeIdGenerator,
                new DummyActivityGenerator<int, int, int, Activity<int, int, int>>(),
                new ArrowTarjanStronglyConnectedComponentsFinder<int, int, int, Activity<int, int, int>>(),
                state);
        }

        private static ArrowGraphState<int, int, int, Activity<int, int, int>> BuildArrowStateWithDummies()
        {
            // Start -> [real 11, dur=3] -> N1 -> [dummy 12, dur=0] -> End
            var state = new ArrowGraphState<int, int, int, Activity<int, int, int>>();

            var startNode = new Node<int, IEvent<int>>(NodeType.Start, new Event<int>(1));
            var middleNode = new Node<int, IEvent<int>>(NodeType.Normal, new Event<int>(2));
            var endNode = new Node<int, IEvent<int>>(NodeType.End, new Event<int>(3));
            state.AddNode(startNode);
            state.AddNode(middleNode);
            state.AddNode(endNode);
            state.StartNode = startNode;
            state.EndNode = endNode;

            AddEdge(state, 11, startNode, middleNode, duration: 3, canBeRemoved: false);
            AddEdge(state, 12, middleNode, endNode, duration: 0, canBeRemoved: true);

            return state;
        }

        private static ArrowGraphState<int, int, int, Activity<int, int, int>> BuildArrowStateWithDummiesAndCircularDependency()
        {
            // Start -> [dummy 11, dur=0] -> N1 -> [real 12, dur=3] -> N2 [dummy 14, dur=0] -> End
            var state = new ArrowGraphState<int, int, int, Activity<int, int, int>>();

            var startNode = new Node<int, IEvent<int>>(NodeType.Start, new Event<int>(1));
            var middleNode1 = new Node<int, IEvent<int>>(NodeType.Normal, new Event<int>(2));
            var middleNode2 = new Node<int, IEvent<int>>(NodeType.Normal, new Event<int>(3));
            var endNode = new Node<int, IEvent<int>>(NodeType.End, new Event<int>(4));
            state.AddNode(startNode);
            state.AddNode(middleNode1);
            state.AddNode(middleNode2);
            state.AddNode(endNode);
            state.StartNode = startNode;
            state.EndNode = endNode;

            AddEdge(state, 11, startNode, middleNode1, duration: 0, canBeRemoved: true);
            AddEdge(state, 12, middleNode1, middleNode2, duration: 3, canBeRemoved: false);
            AddEdge(state, 13, middleNode2, middleNode1, duration: 3, canBeRemoved: false);
            AddEdge(state, 14, middleNode2, endNode, duration: 0, canBeRemoved: true);

            return state;
        }

        private static void AddEdge(
            ArrowGraphState<int, int, int, Activity<int, int, int>> state,
            int edgeId, Node<int, IEvent<int>> tailNode, Node<int, IEvent<int>> headNode,
            int duration, bool canBeRemoved)
        {
            var activity = new Activity<int, int, int>(edgeId, duration, canBeRemoved);
            var edge = new Edge<int, Activity<int, int, int>>(activity);
            state.AddEdge(edge);
            tailNode.OutgoingEdges.Add(edgeId);
            headNode.IncomingEdges.Add(edgeId);
            state.SetEdgeTailNode(edgeId, tailNode);
            state.SetEdgeHeadNode(edgeId, headNode);
        }
    }
}
