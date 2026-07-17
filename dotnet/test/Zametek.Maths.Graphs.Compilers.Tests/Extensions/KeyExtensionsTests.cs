using Shouldly;
using System;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class KeyExtensionsTests
    {
        [Fact]
        public void KeyExtensions_GivenNextTypeInt_ThenValueIsIncrementedByOne()
        {
            int first = new Random().Next();
            int second = first.Next();
            second.ShouldBe(first + 1);
        }

        [Fact]
        public void KeyExtensions_GivenNextTypeGuid_ThenValueIsDifferent()
        {
            Guid first = Guid.NewGuid();
            Guid second = first.Next();
            second.ShouldNotBe(first);
        }

        [Fact]
        public void KeyExtensions_GivenPreviousTypeInt_ThenValueIsDecrementedByOne()
        {
            int first = new Random().Next();
            int second = first.Previous();
            second.ShouldBe(first - 1);
        }

        [Fact]
        public void KeyExtensions_GivenPreviousTypeGuid_ThenThrows()
        {
            Guid first = Guid.NewGuid();
            Should.Throw<InvalidOperationException>(() => first.Previous());
        }
    }
}
