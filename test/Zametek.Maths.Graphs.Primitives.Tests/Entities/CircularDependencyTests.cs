using Shouldly;
using System;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class CircularDependencyTests
    {
        [Fact]
        public void CircularDependency_GivenCtor_ThenDependenciesCopied()
        {
            var circular = new CircularDependency<int>([1, 2, 3]);

            circular.Dependencies.ShouldBe([1, 2, 3], ignoreOrder: true);
        }

        [Fact]
        public void CircularDependency_GivenEquals_WhenSameDependencies_ThenEqualRegardlessOfOrder()
        {
            var circular1 = new CircularDependency<int>([1, 2, 3]);
            var circular2 = new CircularDependency<int>([3, 2, 1]);

            circular1.Equals(circular2).ShouldBeTrue();
            circular1.GetHashCode().ShouldBe(circular2.GetHashCode());
        }

        [Fact]
        public void CircularDependency_GivenEquals_WhenDifferentDependencies_ThenNotEqual()
        {
            var circular1 = new CircularDependency<int>([1, 2]);
            var circular2 = new CircularDependency<int>([1, 3]);

            circular1.Equals(circular2).ShouldBeFalse();
        }
    }
}
