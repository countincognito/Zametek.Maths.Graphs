using Shouldly;
using System;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class VertexCriticalPathEngineTests
    {
        [Fact]
        public void VertexCriticalPathEngine_GivenCalculateCriticalPathForwardFlow_WithNullState_ThenThrowsArgumentNullException()
        {
            var engine = new VertexCriticalPathEngine<int, int, int, Activity<int, int, int>>();
            Action act = () => engine.CalculateCriticalPathForwardFlow(null, [], false);
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void VertexCriticalPathEngine_GivenCalculateCriticalPathForwardFlow_WithNullInvalidConstraints_ThenThrowsArgumentNullException()
        {
            var engine = new VertexCriticalPathEngine<int, int, int, Activity<int, int, int>>();
            var state = new VertexGraphState<int, int, int, Activity<int, int, int>>();
            Action act = () => engine.CalculateCriticalPathForwardFlow(state, null, false);
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void VertexCriticalPathEngine_GivenCalculateCriticalPathForwardFlow_WithInvalidConstraints_ThenReturnsFalse()
        {
            var engine = new VertexCriticalPathEngine<int, int, int, Activity<int, int, int>>();
            var state = BuildLinearVertexState();
            var invalidConstraints = new[] { new InvalidConstraint<int>(99, "some-constraint") };

            bool result = engine.CalculateCriticalPathForwardFlow(state, [.. invalidConstraints.Cast<IInvalidConstraint<int>>()], false);

            result.ShouldBeFalse();
        }

        [Fact]
        public void VertexCriticalPathEngine_GivenCalculateCriticalPathForwardFlow_WithLinearGraph_ThenComputesEarliestStartTimes()
        {
            var engine = new VertexCriticalPathEngine<int, int, int, Activity<int, int, int>>();
            var state = BuildLinearVertexState();

            bool result = engine.CalculateCriticalPathForwardFlow(state, [], false);

            result.ShouldBeTrue();
            state.Node(1).Content.EarliestStartTime.ShouldBe(0);
            state.Node(2).Content.EarliestStartTime.ShouldBe(3);
            state.Node(1).Content.EarliestFinishTime.ShouldBe(3);
            state.Node(2).Content.EarliestFinishTime.ShouldBe(5);
        }

        [Fact]
        public void VertexCriticalPathEngine_GivenCalculateCriticalPathBackwardFlow_WithNullState_ThenThrowsArgumentNullException()
        {
            var engine = new VertexCriticalPathEngine<int, int, int, Activity<int, int, int>>();
            Action act = () => engine.CalculateCriticalPathBackwardFlow(null, [], false);
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void VertexCriticalPathEngine_GivenCalculateCriticalPathBackwardFlow_WithNullInvalidConstraints_ThenThrowsArgumentNullException()
        {
            var engine = new VertexCriticalPathEngine<int, int, int, Activity<int, int, int>>();
            var state = new VertexGraphState<int, int, int, Activity<int, int, int>>();
            Action act = () => engine.CalculateCriticalPathBackwardFlow(state, null, false);
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void VertexCriticalPathEngine_GivenCalculateCriticalPathBackwardFlow_WithInvalidConstraints_ThenReturnsFalse()
        {
            var engine = new VertexCriticalPathEngine<int, int, int, Activity<int, int, int>>();
            var state = BuildLinearVertexStateWithForwardFlow();
            var invalidConstraints = new[] { new InvalidConstraint<int>(99, "some-constraint") };

            bool result = engine.CalculateCriticalPathBackwardFlow(state, [.. invalidConstraints.Cast<IInvalidConstraint<int>>()], false);

            result.ShouldBeFalse();
        }

        [Fact]
        public void VertexCriticalPathEngine_GivenCalculateCriticalPathBackwardFlow_WithMissingEarliestFinishTimes_ThenReturnsFalse()
        {
            var engine = new VertexCriticalPathEngine<int, int, int, Activity<int, int, int>>();
            var state = BuildLinearVertexState();

            bool result = engine.CalculateCriticalPathBackwardFlow(state, [], false);

            result.ShouldBeFalse();
        }

        [Fact]
        public void VertexCriticalPathEngine_GivenCalculateCriticalPathBackwardFlow_WithLinearGraph_ThenComputesLatestFinishTimesAndFreeSlack()
        {
            var engine = new VertexCriticalPathEngine<int, int, int, Activity<int, int, int>>();
            var state = BuildLinearVertexStateWithForwardFlow();

            bool result = engine.CalculateCriticalPathBackwardFlow(state, [], false);

            result.ShouldBeTrue();
            state.Node(2).Content.LatestFinishTime.ShouldBe(5);
            state.Node(1).Content.LatestFinishTime.ShouldBe(3);
            state.Node(1).Content.FreeSlack.ShouldBe(0);
            state.Node(2).Content.FreeSlack.ShouldBe(0);
        }

        [Fact]
        public void VertexCriticalPathEngine_GivenBackFillIsolatedNodes_WithNullState_ThenThrowsArgumentNullException()
        {
            var engine = new VertexCriticalPathEngine<int, int, int, Activity<int, int, int>>();
            Action act = () => engine.BackFillIsolatedNodes(null, []);
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void VertexCriticalPathEngine_GivenBackFillIsolatedNodes_WithNullInvalidConstraints_ThenThrowsArgumentNullException()
        {
            var engine = new VertexCriticalPathEngine<int, int, int, Activity<int, int, int>>();
            var state = new VertexGraphState<int, int, int, Activity<int, int, int>>();
            Action act = () => engine.BackFillIsolatedNodes(state, null);
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void VertexCriticalPathEngine_GivenBackFillIsolatedNodes_WithInvalidConstraints_ThenReturnsFalse()
        {
            var engine = new VertexCriticalPathEngine<int, int, int, Activity<int, int, int>>();
            var state = BuildIsolatedAndEndVertexState();
            var invalidConstraints = new[] { new InvalidConstraint<int>(99, "some-constraint") };

            bool result = engine.BackFillIsolatedNodes(state, [.. invalidConstraints.Cast<IInvalidConstraint<int>>()]);

            result.ShouldBeFalse();
        }

        [Fact]
        public void VertexCriticalPathEngine_GivenBackFillIsolatedNodes_WithEndNodeMissingLatestFinishTime_ThenReturnsFalse()
        {
            var engine = new VertexCriticalPathEngine<int, int, int, Activity<int, int, int>>();
            var state = BuildIsolatedAndEndVertexState();

            bool result = engine.BackFillIsolatedNodes(state, []);

            result.ShouldBeFalse();
        }

        [Fact]
        public void VertexCriticalPathEngine_GivenBackFillIsolatedNodes_WithEndAndIsolatedNodes_ThenSetsLatestFinishTimeAndFreeSlackOnIsolated()
        {
            var engine = new VertexCriticalPathEngine<int, int, int, Activity<int, int, int>>();
            var state = BuildIsolatedAndEndVertexState();

            state.EndNodes.First().Content.EarliestStartTime = 0;
            state.EndNodes.First().Content.LatestFinishTime = 10;

            state.IsolatedNodes.First().Content.EarliestStartTime = 0;
            state.IsolatedNodes.First().Content.LatestFinishTime = 5;

            bool result = engine.BackFillIsolatedNodes(state, []);

            result.ShouldBeTrue();
            state.IsolatedNodes.First().Content.LatestFinishTime.ShouldBe(10);
            state.IsolatedNodes.First().Content.FreeSlack.ShouldBe(10 - 4);
        }

        private static VertexGraphState<int, int, int, Activity<int, int, int>> BuildLinearVertexState()
        {
            var state = new VertexGraphState<int, int, int, Activity<int, int, int>>();

            var n1 = new Node<int, Activity<int, int, int>>(NodeType.Start, new Activity<int, int, int>(1, 3));
            var n2 = new Node<int, Activity<int, int, int>>(NodeType.End, new Activity<int, int, int>(2, 2));
            state.AddNode(n1);
            state.AddNode(n2);

            int edgeId = 100;
            var ev = new Event<int>(edgeId);
            var edge = new Edge<int, IEvent<int>>(ev);
            state.AddEdge(edge);
            n1.OutgoingEdges.Add(edgeId);
            n2.IncomingEdges.Add(edgeId);
            state.SetEdgeTailNode(edgeId, n1);
            state.SetEdgeHeadNode(edgeId, n2);

            return state;
        }

        private static VertexGraphState<int, int, int, Activity<int, int, int>> BuildLinearVertexStateWithForwardFlow()
        {
            var state = BuildLinearVertexState();
            var engine = new VertexCriticalPathEngine<int, int, int, Activity<int, int, int>>();
            engine.CalculateCriticalPathForwardFlow(state, [], false);
            return state;
        }

        private static VertexGraphState<int, int, int, Activity<int, int, int>> BuildIsolatedAndEndVertexState()
        {
            var state = new VertexGraphState<int, int, int, Activity<int, int, int>>();

            var isolated = new Node<int, Activity<int, int, int>>(NodeType.Isolated, new Activity<int, int, int>(1, 4));
            var n2 = new Node<int, Activity<int, int, int>>(NodeType.Start, new Activity<int, int, int>(2, 5));
            var n3 = new Node<int, Activity<int, int, int>>(NodeType.End, new Activity<int, int, int>(3, 5));
            state.AddNode(isolated);
            state.AddNode(n2);
            state.AddNode(n3);

            int edgeId = 100;
            var ev = new Event<int>(edgeId);
            var edge = new Edge<int, IEvent<int>>(ev);
            state.AddEdge(edge);
            n2.OutgoingEdges.Add(edgeId);
            n3.IncomingEdges.Add(edgeId);
            state.SetEdgeTailNode(edgeId, n2);
            state.SetEdgeHeadNode(edgeId, n3);

            return state;
        }
    }
}
