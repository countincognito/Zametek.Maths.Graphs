using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class VertexTarjanStronglyConnectedComponentsFinderTests
    {
        [Fact]
        public void VertexTarjanStronglyConnectedComponentsFinder_GivenFindConnectedComponents_WithNullState_ThenThrowsArgumentNullException()
        {
            var finder = new VertexTarjanStronglyConnectedComponentsFinder<int, int, int, Activity<int, int, int>>();
            Action act = () => finder.FindStronglyConnectedComponents(null, ignoreDummies: false);
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void VertexTarjanStronglyConnectedComponentsFinder_GivenFindConnectedComponents_WithEmptyState_ThenReturnsEmpty()
        {
            var state = new VertexGraphState<int, int, int, Activity<int, int, int>>();
            var finder = new VertexTarjanStronglyConnectedComponentsFinder<int, int, int, Activity<int, int, int>>();

            IList<ICircularDependency<int>> output = finder.FindStronglyConnectedComponents(state, ignoreDummies: false);

            output.ShouldBeEmpty();
        }

        [Fact]
        public void VertexTarjanStronglyConnectedComponentsFinder_GivenFindConnectedComponents_WithAcyclicGraph_ThenNoCircularDependencyContainsMoreThanOneNode()
        {
            var state = BuildAcyclicVertexState();
            var finder = new VertexTarjanStronglyConnectedComponentsFinder<int, int, int, Activity<int, int, int>>();

            IList<ICircularDependency<int>> output = finder.FindStronglyConnectedComponents(state, ignoreDummies: false);

            output.All(x => x.Dependencies.Count <= 1).ShouldBeTrue();
        }

        [Fact]
        public void VertexTarjanStronglyConnectedComponentsFinder_GivenFindConnectedComponents_WithCyclicGraph_ThenReturnsCircularDependencyContainingAllNodesInCycle()
        {
            var state = BuildCyclicVertexState();
            var finder = new VertexTarjanStronglyConnectedComponentsFinder<int, int, int, Activity<int, int, int>>();

            IList<ICircularDependency<int>> output = finder.FindStronglyConnectedComponents(state, ignoreDummies: false);

            ICircularDependency<int> cycle = output.FirstOrDefault(x => x.Dependencies.Count > 1);
            cycle.ShouldNotBeNull();
            cycle.Dependencies.ShouldContain(1);
            cycle.Dependencies.ShouldContain(2);
            cycle.Dependencies.ShouldContain(3);
        }

        [Fact]
        public void VertexTarjanStronglyConnectedComponentsFinder_GivenFindConnectedComponentsIgnoringDummies_WithRemovableNodeInCycle_ThenCircularDependencyExcludesRemovableNode()
        {
            var state = BuildCyclicVertexStateWithRemovableNode();
            var finder = new VertexTarjanStronglyConnectedComponentsFinder<int, int, int, Activity<int, int, int>>();

            IList<ICircularDependency<int>> output = finder.FindStronglyConnectedComponents(state, ignoreDummies: true);

            ICircularDependency<int> cycle = output.FirstOrDefault(x => x.Dependencies.Count > 1);
            cycle.ShouldNotBeNull();
            cycle.Dependencies.ShouldNotContain(2);
            cycle.Dependencies.ShouldContain(1);
            cycle.Dependencies.ShouldContain(3);
        }

        [Fact]
        public void VertexTarjanStronglyConnectedComponentsFinder_GivenFindConnectedComponents_WithRemovableNodeInCycle_ThenCircularDependencyIncludesRemovableNode()
        {
            var state = BuildCyclicVertexStateWithRemovableNode();
            var finder = new VertexTarjanStronglyConnectedComponentsFinder<int, int, int, Activity<int, int, int>>();

            IList<ICircularDependency<int>> output = finder.FindStronglyConnectedComponents(state, ignoreDummies: false);

            ICircularDependency<int> cycle = output.FirstOrDefault(x => x.Dependencies.Count > 1);
            cycle.ShouldNotBeNull();
            cycle.Dependencies.ShouldContain(2);
            cycle.Dependencies.ShouldContain(1);
            cycle.Dependencies.ShouldContain(3);
        }

        private static VertexGraphState<int, int, int, Activity<int, int, int>> BuildAcyclicVertexState()
        {
            var state = new VertexGraphState<int, int, int, Activity<int, int, int>>();

            var n1 = new Node<int, Activity<int, int, int>>(NodeType.Start, new Activity<int, int, int>(1, 5));
            var n2 = new Node<int, Activity<int, int, int>>(NodeType.End, new Activity<int, int, int>(2, 5));
            state.AddNode(n1);
            state.AddNode(n2);

            AddEdge(state, 100, n1, n2);

            return state;
        }

        private static VertexGraphState<int, int, int, Activity<int, int, int>> BuildCyclicVertexState()
        {
            var state = new VertexGraphState<int, int, int, Activity<int, int, int>>();

            var n1 = new Node<int, Activity<int, int, int>>(NodeType.Normal, new Activity<int, int, int>(1, 1));
            var n2 = new Node<int, Activity<int, int, int>>(NodeType.Normal, new Activity<int, int, int>(2, 1));
            var n3 = new Node<int, Activity<int, int, int>>(NodeType.Normal, new Activity<int, int, int>(3, 1));
            state.AddNode(n1);
            state.AddNode(n2);
            state.AddNode(n3);

            AddEdge(state, 100, n1, n2);
            AddEdge(state, 101, n2, n3);
            AddEdge(state, 102, n3, n1);

            return state;
        }

        private static VertexGraphState<int, int, int, Activity<int, int, int>> BuildCyclicVertexStateWithRemovableNode()
        {
            var state = new VertexGraphState<int, int, int, Activity<int, int, int>>();

            var n1 = new Node<int, Activity<int, int, int>>(NodeType.Normal, new Activity<int, int, int>(1, 1));
            var n2 = new Node<int, Activity<int, int, int>>(NodeType.Normal, new Activity<int, int, int>(2, 1, canBeRemoved: true));
            var n3 = new Node<int, Activity<int, int, int>>(NodeType.Normal, new Activity<int, int, int>(3, 1));
            state.AddNode(n1);
            state.AddNode(n2);
            state.AddNode(n3);

            AddEdge(state, 100, n1, n2);
            AddEdge(state, 101, n2, n3);
            AddEdge(state, 102, n3, n1);

            return state;
        }

        private static void AddEdge(
            VertexGraphState<int, int, int, Activity<int, int, int>> state,
            int edgeId,
            Node<int, Activity<int, int, int>> tailNode,
            Node<int, Activity<int, int, int>> headNode)
        {
            var ev = new Event<int>(edgeId);
            var edge = new Edge<int, IEvent<int>>(ev);
            state.AddEdge(edge);
            tailNode.OutgoingEdges.Add(edgeId);
            headNode.IncomingEdges.Add(edgeId);
            state.SetEdgeTailNode(edgeId, tailNode);
            state.SetEdgeHeadNode(edgeId, headNode);
        }
    }
}
