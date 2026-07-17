using Shouldly;
using System;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class NodeTests
    {
        [Fact]
        public void Node_GivenCtor_WithNullContent_ThenThrowsArgumentNullException()
        {
            Action act = () => new Node<int, Event<int>>(null);
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void Node_GivenCtor_WithContentOnly_ThenNodeTypeIsNormal()
        {
            var node = new Node<int, Event<int>>(new Event<int>(1));

            node.NodeType.ShouldBe(NodeType.Normal);
            node.Id.ShouldBe(1);
        }

        [Fact]
        public void Node_GivenIncomingEdges_WhenStartNode_ThenThrowsInvalidOperationException()
        {
            var node = new Node<int, Event<int>>(NodeType.Start, new Event<int>(1));

            Action act = () => _ = node.IncomingEdges;
            act.ShouldThrow<InvalidOperationException>();
        }

        [Fact]
        public void Node_GivenIncomingEdges_WhenIsolatedNode_ThenThrowsInvalidOperationException()
        {
            var node = new Node<int, Event<int>>(NodeType.Isolated, new Event<int>(1));

            Action act = () => _ = node.IncomingEdges;
            act.ShouldThrow<InvalidOperationException>();
        }

        [Fact]
        public void Node_GivenOutgoingEdges_WhenEndNode_ThenThrowsInvalidOperationException()
        {
            var node = new Node<int, Event<int>>(NodeType.End, new Event<int>(1));

            Action act = () => _ = node.OutgoingEdges;
            act.ShouldThrow<InvalidOperationException>();
        }

        [Fact]
        public void Node_GivenOutgoingEdges_WhenIsolatedNode_ThenThrowsInvalidOperationException()
        {
            var node = new Node<int, Event<int>>(NodeType.Isolated, new Event<int>(1));

            Action act = () => _ = node.OutgoingEdges;
            act.ShouldThrow<InvalidOperationException>();
        }

        [Fact]
        public void Node_GivenSetNodeType_ThenNodeTypeChanges()
        {
            var node = new Node<int, Event<int>>(NodeType.Start, new Event<int>(1));

            node.SetNodeType(NodeType.Normal);

            node.NodeType.ShouldBe(NodeType.Normal);
        }

        [Fact]
        public void Node_GivenEquals_WhenSameIdTypeAndEdges_ThenEqual()
        {
            var node1 = new Node<int, Event<int>>(new Event<int>(1));
            node1.IncomingEdges.Add(10);
            node1.OutgoingEdges.Add(20);
            var node2 = new Node<int, Event<int>>(new Event<int>(1));
            node2.IncomingEdges.Add(10);
            node2.OutgoingEdges.Add(20);

            node1.Equals(node2).ShouldBeTrue();
            node1.GetHashCode().ShouldBe(node2.GetHashCode());
        }

        [Fact]
        public void Node_GivenEquals_WhenDifferentNodeType_ThenNotEqual()
        {
            var node1 = new Node<int, Event<int>>(NodeType.Normal, new Event<int>(1));
            var node2 = new Node<int, Event<int>>(NodeType.End, new Event<int>(1));

            node1.Equals(node2).ShouldBeFalse();
        }

        [Fact]
        public void Node_GivenEquals_WhenDifferentEdges_ThenNotEqual()
        {
            var node1 = new Node<int, Event<int>>(new Event<int>(1));
            node1.IncomingEdges.Add(10);
            var node2 = new Node<int, Event<int>>(new Event<int>(1));
            node2.IncomingEdges.Add(11);

            node1.Equals(node2).ShouldBeFalse();
        }

        [Fact]
        public void Node_GivenCloneObject_ThenTypeContentAndEdgesPreserved()
        {
            var content = new Event<int>(1, 2, 3);
            var node = new Node<int, Event<int>>(NodeType.Normal, content);
            node.IncomingEdges.Add(10);
            node.OutgoingEdges.Add(20);

            var clone = (Node<int, Event<int>>)node.CloneObject();

            clone.NodeType.ShouldBe(NodeType.Normal);
            clone.Content.ShouldNotBeSameAs(content);
            clone.Content.EarliestFinishTime.ShouldBe(2);
            clone.IncomingEdges.ShouldBe([10]);
            clone.OutgoingEdges.ShouldBe([20]);
        }

        [Fact]
        public void Node_GivenCloneObject_ThenEdgeSetsAreIndependentCopies()
        {
            var node = new Node<int, Event<int>>(new Event<int>(1));
            node.IncomingEdges.Add(10);

            var clone = (Node<int, Event<int>>)node.CloneObject();
            clone.IncomingEdges.Add(11);

            node.IncomingEdges.Count.ShouldBe(1);
            clone.IncomingEdges.Count.ShouldBe(2);
        }
    }
}
