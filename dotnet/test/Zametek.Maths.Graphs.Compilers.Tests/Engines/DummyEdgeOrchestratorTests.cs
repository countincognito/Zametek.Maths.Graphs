using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class DummyEdgeOrchestratorTests
    {
        // The orchestrator is stateless: the graph state - and the ID/activity
        // generators and SCC finder each operation needs - are supplied per call.
        private static DummyEdgeOrchestrator<int, int, int, Activity<int, int, int>> Orchestrator() =>
            new DummyEdgeOrchestrator<int, int, int, Activity<int, int, int>>();

        private static DummyActivityGenerator<int, int, int, Activity<int, int, int>> DummyGen() =>
            new DummyActivityGenerator<int, int, int, Activity<int, int, int>>();

        private static ArrowTarjanStronglyConnectedComponentsFinder<int, int, int, Activity<int, int, int>> SccFinder() =>
            new ArrowTarjanStronglyConnectedComponentsFinder<int, int, int, Activity<int, int, int>>();

        [Fact]
        public void DummyEdgeOrchestrator_GivenConnectWithDummyEdgeWithNullEdgeIdGenerator_ThenThrowsArgumentNullException()
        {
            var state = new ArrowGraphState<int, int, int, Activity<int, int, int>>();
            var tail = new Node<int, IEvent<int>>(NodeType.Normal, new Event<int>(1));
            var head = new Node<int, IEvent<int>>(NodeType.Normal, new Event<int>(2));
            Action act = () => Orchestrator().ConnectWithDummyEdge(state, null, DummyGen(), tail, head);
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void DummyEdgeOrchestrator_GivenConnectWithDummyEdgeWithNullDummyActivityGenerator_ThenThrowsArgumentNullException()
        {
            var state = new ArrowGraphState<int, int, int, Activity<int, int, int>>();
            var tail = new Node<int, IEvent<int>>(NodeType.Normal, new Event<int>(1));
            var head = new Node<int, IEvent<int>>(NodeType.Normal, new Event<int>(2));
            Action act = () => Orchestrator().ConnectWithDummyEdge(state, new NextIdGenerator<int>(1), null, tail, head);
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void DummyEdgeOrchestrator_GivenRedirectDummyEdgesWithNullStronglyConnectedComponentsFinder_ThenThrowsArgumentNullException()
        {
            var state = BuildArrowStateWithDummies();
            Action act = () => Orchestrator().RedirectDummyEdges(state, null);
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void DummyEdgeOrchestrator_GivenRemoveDummyActivityWithNullState_ThenThrowsArgumentNullException()
        {
            Action act = () => Orchestrator().RemoveDummyActivity(null, 999);
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

            Orchestrator().ConnectWithDummyEdge(state, new NextIdGenerator<int>(dummyEdgeId - 1), DummyGen(), tail, head);

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

            bool result = Orchestrator().RemoveDummyActivity(state, 999);

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

            bool result = Orchestrator().RemoveDummyActivity(state, edgeId);

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

            bool result = Orchestrator().RemoveDummyActivity(state, edgeId);

            result.ShouldBeFalse();
        }

        [Fact]
        public void DummyEdgeOrchestrator_GivenGetDummyEdgesInDescendingOrder_WithMixedEdges_ThenReturnsOnlyDummies()
        {
            var state = BuildArrowStateWithDummies();

            IList<Edge<int, Activity<int, int, int>>> output = Orchestrator().GetDummyEdgesInDescendingOrder(state);

            output.All(x => x.Content.IsDummy).ShouldBeTrue();
        }

        [Fact]
        public void DummyEdgeOrchestrator_GivenRedirectDummyEdges_WithUnsatisfiedDependencies_ThenReturnsFalse()
        {
            var state = BuildArrowStateWithDummies();
            var dependentNode = new Node<int, IEvent<int>>(NodeType.Normal, new Event<int>(99));
            state.AddUnsatisfiedSuccessor(7777, dependentNode);

            bool result = Orchestrator().RedirectDummyEdges(state, SccFinder());

            result.ShouldBeFalse();
        }

        [Fact]
        public void DummyEdgeOrchestrator_GivenRedirectDummyEdges_WithCircularDependencies_ThenReturnsFalse()
        {
            var state = BuildArrowStateWithDummiesAndCircularDependency();

            bool result = Orchestrator().RedirectDummyEdges(state, SccFinder());

            result.ShouldBeFalse();
        }

        [Fact]
        public void DummyEdgeOrchestrator_GivenRemoveRedundantDummyEdges_WithUnsatisfiedDependencies_ThenReturnsFalse()
        {
            var state = BuildArrowStateWithDummies();
            var dependentNode = new Node<int, IEvent<int>>(NodeType.Normal, new Event<int>(99));
            state.AddUnsatisfiedSuccessor(7777, dependentNode);

            bool result = Orchestrator().RemoveRedundantDummyEdges(state, SccFinder());

            result.ShouldBeFalse();
        }

        [Fact]
        public void DummyEdgeOrchestrator_GivenRemoveRedundantDummyEdges_WithCircularDependencies_ThenReturnsFalse()
        {
            var state = BuildArrowStateWithDummiesAndCircularDependency();

            bool result = Orchestrator().RemoveRedundantDummyEdges(state, SccFinder());

            result.ShouldBeFalse();
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
