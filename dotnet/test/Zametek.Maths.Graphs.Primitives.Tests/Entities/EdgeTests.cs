using Shouldly;
using System;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class EdgeTests
    {
        [Fact]
        public void Edge_GivenCtor_WithNullContent_ThenThrowsArgumentNullException()
        {
            Action act = () => new Edge<int, Event<int>>(null);
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void Edge_GivenId_ThenDelegatesToContentId()
        {
            var edge = new Edge<int, Event<int>>(new Event<int>(7));

            edge.Id.ShouldBe(7);
        }

        [Fact]
        public void Edge_GivenEquals_WhenSameId_ThenEqualEvenIfContentDiffers()
        {
            var edge1 = new Edge<int, Event<int>>(new Event<int>(7, 1, 2));
            var edge2 = new Edge<int, Event<int>>(new Event<int>(7, 3, 4));

            edge1.Equals(edge2).ShouldBeTrue();
            edge1.GetHashCode().ShouldBe(edge2.GetHashCode());
        }

        [Fact]
        public void Edge_GivenEquals_WhenDifferentId_ThenNotEqual()
        {
            var edge1 = new Edge<int, Event<int>>(new Event<int>(7));
            var edge2 = new Edge<int, Event<int>>(new Event<int>(8));

            edge1.Equals(edge2).ShouldBeFalse();
        }

        [Fact]
        public void Edge_GivenEquals_WhenNull_ThenNotEqual()
        {
            var edge = new Edge<int, Event<int>>(new Event<int>(7));

            edge.Equals((Edge<int, Event<int>>)null).ShouldBeFalse();
        }

        [Fact]
        public void Edge_GivenCloneObject_ThenContentIsClonedNotShared()
        {
            var content = new Event<int>(7, 1, 2);
            content.SetAsRemovable();
            var edge = new Edge<int, Event<int>>(content);

            var clone = (Edge<int, Event<int>>)edge.CloneObject();

            clone.Id.ShouldBe(7);
            clone.Content.ShouldNotBeSameAs(content);
            clone.Content.EarliestFinishTime.ShouldBe(1);
            clone.Content.LatestFinishTime.ShouldBe(2);
            clone.Content.CanBeRemoved.ShouldBeTrue();
        }
    }
}
