using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class ArrowTarjanStronglyConnectedComponentsFinderTests
    {
        [Fact]
        public void ArrowTarjanStronglyConnectedComponentsFinder_GivenFindStronglyConnectedComponents_WithNullState_ThenThrowsArgumentNullException()
        {
            var finder = new ArrowTarjanStronglyConnectedComponentsFinder<int, int, int, Activity<int, int, int>>();
            Action act = () => finder.FindStronglyConnectedComponents(null, ignoreDummies: false);
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void ArrowTarjanStronglyConnectedComponentsFinder_GivenFindStronglyConnectedComponents_WithEmptyState_ThenReturnsEmpty()
        {
            var state = new ArrowGraphState<int, int, int, Activity<int, int, int>>();
            var finder = new ArrowTarjanStronglyConnectedComponentsFinder<int, int, int, Activity<int, int, int>>();

            IList<ICircularDependency<int>> output = finder.FindStronglyConnectedComponents(state, ignoreDummies: false);

            output.ShouldBeEmpty();
        }

        [Fact]
        public void ArrowTarjanStronglyConnectedComponentsFinder_GivenFindStronglyConnectedComponents_WithAcyclicGraph_ThenNoCircularDependencyContainsMoreThanOneEdge()
        {
            var state = BuildAcyclicArrowState();
            var finder = new ArrowTarjanStronglyConnectedComponentsFinder<int, int, int, Activity<int, int, int>>();

            IList<ICircularDependency<int>> output = finder.FindStronglyConnectedComponents(state, ignoreDummies: false);

            output.All(x => x.Dependencies.Count <= 1).ShouldBeTrue();
        }

        [Fact]
        public void ArrowTarjanStronglyConnectedComponentsFinder_GivenFindStronglyConnectedComponents_WithCyclicGraph_ThenReturnsCircularDependencyContainingAllEdgesInCycle()
        {
            var state = BuildCyclicArrowState();
            var finder = new ArrowTarjanStronglyConnectedComponentsFinder<int, int, int, Activity<int, int, int>>();

            IList<ICircularDependency<int>> output = finder.FindStronglyConnectedComponents(state, ignoreDummies: false);

            ICircularDependency<int> cycle = output.FirstOrDefault(x => x.Dependencies.Count > 1);
            cycle.ShouldNotBeNull();
            cycle.Dependencies.ShouldContain(11);
            cycle.Dependencies.ShouldContain(12);
            cycle.Dependencies.ShouldContain(13);
        }

        [Fact]
        public void ArrowTarjanStronglyConnectedComponentsFinder_GivenFindStronglyConnectedComponentsIgnoringDummies_WithRemovableEdgesInCycle_ThenCircularDependencyExcludesRemovableEdges()
        {
            var state = BuildCyclicArrowStateWithRemovableEdge();
            var finder = new ArrowTarjanStronglyConnectedComponentsFinder<int, int, int, Activity<int, int, int>>();

            IList<ICircularDependency<int>> output = finder.FindStronglyConnectedComponents(state, ignoreDummies: true);

            ICircularDependency<int> cycle = output.FirstOrDefault(x => x.Dependencies.Count > 1);
            cycle.ShouldNotBeNull();
            cycle.Dependencies.ShouldNotContain(12);
            cycle.Dependencies.ShouldContain(11);
            cycle.Dependencies.ShouldContain(13);
        }

        [Fact]
        public void ArrowTarjanStronglyConnectedComponentsFinder_GivenFindStronglyConnectedComponents_WithRemovableEdgesInCycle_ThenCircularDependencyIncludesRemovableEdges()
        {
            var state = BuildCyclicArrowStateWithRemovableEdge();
            var finder = new ArrowTarjanStronglyConnectedComponentsFinder<int, int, int, Activity<int, int, int>>();

            IList<ICircularDependency<int>> output = finder.FindStronglyConnectedComponents(state, ignoreDummies: false);

            ICircularDependency<int> cycle = output.FirstOrDefault(x => x.Dependencies.Count > 1);
            cycle.ShouldNotBeNull();
            cycle.Dependencies.ShouldContain(12);
            cycle.Dependencies.ShouldContain(11);
            cycle.Dependencies.ShouldContain(13);
        }

        private static ArrowGraphState<int, int, int, Activity<int, int, int>> BuildAcyclicArrowState()
        {
            var state = new ArrowGraphState<int, int, int, Activity<int, int, int>>();

            var startNode = new Node<int, IEvent<int>>(NodeType.Start, new Event<int>(1));
            var endNode = new Node<int, IEvent<int>>(NodeType.End, new Event<int>(2));
            state.AddNode(startNode);
            state.AddNode(endNode);
            state.StartNode = startNode;
            state.EndNode = endNode;

            var activity = new Activity<int, int, int>(11, 5);
            var edge = new Edge<int, Activity<int, int, int>>(activity);
            state.AddEdge(edge);
            startNode.OutgoingEdges.Add(edge.Id);
            endNode.IncomingEdges.Add(edge.Id);
            state.SetEdgeTailNode(edge.Id, startNode);
            state.SetEdgeHeadNode(edge.Id, endNode);

            return state;
        }

        private static ArrowGraphState<int, int, int, Activity<int, int, int>> BuildCyclicArrowState()
        {
            var state = new ArrowGraphState<int, int, int, Activity<int, int, int>>();

            var n1 = new Node<int, IEvent<int>>(NodeType.Normal, new Event<int>(1));
            var n2 = new Node<int, IEvent<int>>(NodeType.Normal, new Event<int>(2));
            var n3 = new Node<int, IEvent<int>>(NodeType.Normal, new Event<int>(3));
            state.AddNode(n1);
            state.AddNode(n2);
            state.AddNode(n3);

            AddEdge(state, 11, n1, n2, duration: 1);
            AddEdge(state, 12, n2, n3, duration: 1);
            AddEdge(state, 13, n3, n1, duration: 1);

            return state;
        }

        private static ArrowGraphState<int, int, int, Activity<int, int, int>> BuildCyclicArrowStateWithRemovableEdge()
        {
            var state = new ArrowGraphState<int, int, int, Activity<int, int, int>>();

            var n1 = new Node<int, IEvent<int>>(NodeType.Normal, new Event<int>(1));
            var n2 = new Node<int, IEvent<int>>(NodeType.Normal, new Event<int>(2));
            var n3 = new Node<int, IEvent<int>>(NodeType.Normal, new Event<int>(3));
            state.AddNode(n1);
            state.AddNode(n2);
            state.AddNode(n3);

            AddEdge(state, 11, n1, n2, duration: 1);
            AddEdge(state, 12, n2, n3, duration: 0, canBeRemoved: true);
            AddEdge(state, 13, n3, n1, duration: 1);

            return state;
        }

        private static void AddEdge(
            ArrowGraphState<int, int, int, Activity<int, int, int>> state,
            int edgeId, Node<int, IEvent<int>> tailNode, Node<int, IEvent<int>> headNode,
            int duration, bool canBeRemoved = false)
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
