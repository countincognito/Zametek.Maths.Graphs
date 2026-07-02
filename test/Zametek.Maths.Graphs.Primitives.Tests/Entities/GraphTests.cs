using Shouldly;
using System;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class GraphTests
    {
        private static Graph<int, Event<int>, Activity<int, int, int>> BuildSimpleGraph()
        {
            var edge = new Edge<int, Event<int>>(new Event<int>(10));
            var node1 = new Node<int, Activity<int, int, int>>(NodeType.Start, new Activity<int, int, int>(1, 0));
            node1.OutgoingEdges.Add(10);
            var node2 = new Node<int, Activity<int, int, int>>(NodeType.End, new Activity<int, int, int>(2, 0));
            node2.IncomingEdges.Add(10);
            return new Graph<int, Event<int>, Activity<int, int, int>>([edge], [node1, node2]);
        }

        [Fact]
        public void Graph_GivenCtor_WithNullEdges_ThenThrowsArgumentNullException()
        {
            Action act = () => new Graph<int, Event<int>, Activity<int, int, int>>(
                null,
                Array.Empty<Node<int, Activity<int, int, int>>>());
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void Graph_GivenCtor_WithNullNodes_ThenThrowsArgumentNullException()
        {
            Action act = () => new Graph<int, Event<int>, Activity<int, int, int>>(
                Array.Empty<Edge<int, Event<int>>>(),
                null);
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void Graph_GivenDefaultCtor_ThenEmpty()
        {
            var graph = new Graph<int, Event<int>, Activity<int, int, int>>();

            graph.Edges.ShouldBeEmpty();
            graph.Nodes.ShouldBeEmpty();
        }

        [Fact]
        public void Graph_GivenEquals_WhenSameStructure_ThenEqual()
        {
            var graph1 = BuildSimpleGraph();
            var graph2 = BuildSimpleGraph();

            graph1.Equals(graph2).ShouldBeTrue();
            graph1.GetHashCode().ShouldBe(graph2.GetHashCode());
        }

        [Fact]
        public void Graph_GivenEquals_WhenDifferentStructure_ThenNotEqual()
        {
            var graph1 = BuildSimpleGraph();
            var graph2 = new Graph<int, Event<int>, Activity<int, int, int>>();

            graph1.Equals(graph2).ShouldBeFalse();
        }
    }
}
