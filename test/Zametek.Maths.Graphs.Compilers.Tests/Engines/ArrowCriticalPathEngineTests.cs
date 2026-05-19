using Shouldly;
using System;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class ArrowCriticalPathEngineTests
    {
        [Fact]
        public void ArrowCriticalPathEngine_GivenCalculateEventEarliestFinishTimes_WithNullState_ThenThrowsArgumentNullException()
        {
            var engine = new ArrowCriticalPathEngine<int, int, int, Activity<int, int, int>>();
            Action act = () => engine.CalculateEventEarliestFinishTimes(null, [], false);
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void ArrowCriticalPathEngine_GivenCalculateEventEarliestFinishTimes_WithNullInvalidConstraints_ThenThrowsArgumentNullException()
        {
            var engine = new ArrowCriticalPathEngine<int, int, int, Activity<int, int, int>>();
            var state = new ArrowGraphState<int, int, int, Activity<int, int, int>>();
            Action act = () => engine.CalculateEventEarliestFinishTimes(state, null, false);
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void ArrowCriticalPathEngine_GivenCalculateEventEarliestFinishTimes_WithMissingStartNode_ThenThrowsInvalidOperationException()
        {
            var engine = new ArrowCriticalPathEngine<int, int, int, Activity<int, int, int>>();
            var state = new ArrowGraphState<int, int, int, Activity<int, int, int>>();
            Action act = () => engine.CalculateEventEarliestFinishTimes(state, [], false);
            act.ShouldThrow<InvalidOperationException>();
        }

        [Fact]
        public void ArrowCriticalPathEngine_GivenCalculateEventEarliestFinishTimes_WithMissingEndNode_ThenThrowsInvalidOperationException()
        {
            var engine = new ArrowCriticalPathEngine<int, int, int, Activity<int, int, int>>();
            var state = new ArrowGraphState<int, int, int, Activity<int, int, int>>();
            var startNode = new Node<int, IEvent<int>>(NodeType.Start, new Event<int>(1));
            state.AddNode(startNode);
            state.StartNode = startNode;

            Action act = () => engine.CalculateEventEarliestFinishTimes(state, [], false);
            act.ShouldThrow<InvalidOperationException>();
        }

        [Fact]
        public void ArrowCriticalPathEngine_GivenCalculateEventEarliestFinishTimes_WithInvalidConstraints_ThenReturnsFalse()
        {
            var engine = new ArrowCriticalPathEngine<int, int, int, Activity<int, int, int>>();
            var state = BuildLinearArrowState();
            var invalidConstraints = new[] { new InvalidConstraint<int>(99, "some-constraint") };

            bool result = engine.CalculateEventEarliestFinishTimes(state, [.. invalidConstraints.Cast<IInvalidConstraint<int>>()], false);

            result.ShouldBeFalse();
        }

        [Fact]
        public void ArrowCriticalPathEngine_GivenCalculateEventEarliestFinishTimes_WithLinearGraph_ThenComputesEarliestFinishTimes()
        {
            var engine = new ArrowCriticalPathEngine<int, int, int, Activity<int, int, int>>();
            var state = BuildLinearArrowState();

            bool result = engine.CalculateEventEarliestFinishTimes(state, [], false);

            result.ShouldBeTrue();
            state.StartNode.Content.EarliestFinishTime.ShouldBe(0);
            state.Node(2).Content.EarliestFinishTime.ShouldBe(3);
            state.EndNode.Content.EarliestFinishTime.ShouldBe(5);
        }

        [Fact]
        public void ArrowCriticalPathEngine_GivenCalculateEventLatestFinishTimes_WithNullState_ThenThrowsArgumentNullException()
        {
            var engine = new ArrowCriticalPathEngine<int, int, int, Activity<int, int, int>>();
            Action act = () => engine.CalculateEventLatestFinishTimes(null, [], false);
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void ArrowCriticalPathEngine_GivenCalculateEventLatestFinishTimes_WithNullInvalidConstraints_ThenThrowsArgumentNullException()
        {
            var engine = new ArrowCriticalPathEngine<int, int, int, Activity<int, int, int>>();
            var state = new ArrowGraphState<int, int, int, Activity<int, int, int>>();
            Action act = () => engine.CalculateEventLatestFinishTimes(state, null, false);
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void ArrowCriticalPathEngine_GivenCalculateEventLatestFinishTimes_WithMissingEndNode_ThenThrowsInvalidOperationException()
        {
            var engine = new ArrowCriticalPathEngine<int, int, int, Activity<int, int, int>>();
            var state = new ArrowGraphState<int, int, int, Activity<int, int, int>>();
            Action act = () => engine.CalculateEventLatestFinishTimes(state, [], false);
            act.ShouldThrow<InvalidOperationException>();
        }

        [Fact]
        public void ArrowCriticalPathEngine_GivenCalculateEventLatestFinishTimes_WithInvalidConstraints_ThenReturnsFalse()
        {
            var engine = new ArrowCriticalPathEngine<int, int, int, Activity<int, int, int>>();
            var state = BuildLinearArrowStateWithEarliestFinishTimes();
            var invalidConstraints = new[] { new InvalidConstraint<int>(99, "some-constraint") };

            bool result = engine.CalculateEventLatestFinishTimes(state, [.. invalidConstraints.Cast<IInvalidConstraint<int>>()], false);

            result.ShouldBeFalse();
        }

        [Fact]
        public void ArrowCriticalPathEngine_GivenCalculateEventLatestFinishTimes_WithEventsMissingEarliestFinishTimes_ThenReturnsFalse()
        {
            var engine = new ArrowCriticalPathEngine<int, int, int, Activity<int, int, int>>();
            var state = BuildLinearArrowState();

            bool result = engine.CalculateEventLatestFinishTimes(state, [], false);

            result.ShouldBeFalse();
        }

        [Fact]
        public void ArrowCriticalPathEngine_GivenCalculateEventLatestFinishTimes_WithLinearGraph_ThenComputesLatestFinishTimes()
        {
            var engine = new ArrowCriticalPathEngine<int, int, int, Activity<int, int, int>>();
            var state = BuildLinearArrowStateWithEarliestFinishTimes();

            bool result = engine.CalculateEventLatestFinishTimes(state, [], false);

            result.ShouldBeTrue();
            state.EndNode.Content.LatestFinishTime.ShouldBe(5);
            state.Node(2).Content.LatestFinishTime.ShouldBe(3);
            state.StartNode.Content.LatestFinishTime.ShouldBe(0);
        }

        [Fact]
        public void ArrowCriticalPathEngine_GivenCalculateCriticalPathVariables_WithNullState_ThenThrowsArgumentNullException()
        {
            var engine = new ArrowCriticalPathEngine<int, int, int, Activity<int, int, int>>();
            Action act = () => engine.CalculateCriticalPathVariables(null, []);
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void ArrowCriticalPathEngine_GivenCalculateCriticalPathVariables_WithNullInvalidConstraints_ThenThrowsArgumentNullException()
        {
            var engine = new ArrowCriticalPathEngine<int, int, int, Activity<int, int, int>>();
            var state = new ArrowGraphState<int, int, int, Activity<int, int, int>>();
            Action act = () => engine.CalculateCriticalPathVariables(state, null);
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void ArrowCriticalPathEngine_GivenCalculateCriticalPathVariables_WithInvalidConstraints_ThenReturnsFalse()
        {
            var engine = new ArrowCriticalPathEngine<int, int, int, Activity<int, int, int>>();
            var state = BuildLinearArrowStateWithLatestFinishTimes();
            var invalidConstraints = new[] { new InvalidConstraint<int>(99, "some-constraint") };

            bool result = engine.CalculateCriticalPathVariables(state, [.. invalidConstraints.Cast<IInvalidConstraint<int>>()]);

            result.ShouldBeFalse();
        }

        [Fact]
        public void ArrowCriticalPathEngine_GivenCalculateCriticalPathVariables_WithEventsMissingEarliestFinishTimes_ThenReturnsFalse()
        {
            var engine = new ArrowCriticalPathEngine<int, int, int, Activity<int, int, int>>();
            var state = BuildLinearArrowState();

            bool result = engine.CalculateCriticalPathVariables(state, []);

            result.ShouldBeFalse();
        }

        [Fact]
        public void ArrowCriticalPathEngine_GivenCalculateCriticalPathVariables_WithLinearGraph_ThenComputesEdgeTimesAndFreeSlack()
        {
            var engine = new ArrowCriticalPathEngine<int, int, int, Activity<int, int, int>>();
            var state = BuildLinearArrowStateWithLatestFinishTimes();

            bool result = engine.CalculateCriticalPathVariables(state, []);

            result.ShouldBeTrue();
            state.Edge(11).Content.EarliestStartTime.ShouldBe(0);
            state.Edge(11).Content.LatestFinishTime.ShouldBe(3);
            state.Edge(11).Content.FreeSlack.ShouldBe(0);

            state.Edge(12).Content.EarliestStartTime.ShouldBe(3);
            state.Edge(12).Content.LatestFinishTime.ShouldBe(5);
            state.Edge(12).Content.FreeSlack.ShouldBe(0);
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

        private static ArrowGraphState<int, int, int, Activity<int, int, int>> BuildLinearArrowStateWithEarliestFinishTimes()
        {
            var state = BuildLinearArrowState();
            var engine = new ArrowCriticalPathEngine<int, int, int, Activity<int, int, int>>();
            engine.CalculateEventEarliestFinishTimes(state, [], false);
            return state;
        }

        private static ArrowGraphState<int, int, int, Activity<int, int, int>> BuildLinearArrowStateWithLatestFinishTimes()
        {
            var state = BuildLinearArrowStateWithEarliestFinishTimes();
            var engine = new ArrowCriticalPathEngine<int, int, int, Activity<int, int, int>>();
            engine.CalculateEventLatestFinishTimes(state, [], false);
            return state;
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
