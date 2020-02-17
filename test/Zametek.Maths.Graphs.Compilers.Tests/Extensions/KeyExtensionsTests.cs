using FluentAssertions;
using System;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class KeyExtensionsTests
    {
        [Fact]
        public void KeyExtensions_GivenNextInt_ThenValueIsIncrementedByOne()
        {
            int first = new Random().Next();
            int second = KeyExtensions.NextInt(first);
            second.Should().Be(first + 1);
        }

        [Fact]
        public void KeyExtensions_GivenNextTypeInt_ThenValueIsIncrementedByOne()
        {
            int first = new Random().Next();
            int second = first.Next();
            second.Should().Be(first + 1);
        }

        [Fact]
        public void KeyExtensions_GivenNextGuid_ThenValueIsDifferent()
        {
            Guid first = Guid.NewGuid();
            Guid second = KeyExtensions.NextGuid(first);
            second.Should().NotBe(first);
        }

        [Fact]
        public void KeyExtensions_GivenNextTypeGuid_ThenValueIsDifferent()
        {
            Guid first = Guid.NewGuid();
            Guid second = first.Next();
            second.Should().NotBe(first);
        }

        [Fact]
        public void KeyExtensions_GivenPreviousInt_ThenValueIsDecrementedByOne()
        {
            int first = new Random().Next();
            int second = KeyExtensions.PreviousInt(first);
            second.Should().Be(first - 1);
        }

        [Fact]
        public void KeyExtensions_GivenPreviousTypeInt_ThenValueIsDecrementedByOne()
        {
            int first = new Random().Next();
            int second = first.Previous();
            second.Should().Be(first - 1);
        }

        [Fact]
        public void KeyExtensions_GivenPreviousGuid_ThenValueIsDifferent()
        {
            Guid first = Guid.NewGuid();
            Guid second = KeyExtensions.PreviousGuid(first);
            second.Should().NotBe(first);
        }

        [Fact]
        public void KeyExtensions_GivenPreviousTypeGuid_ThenValueIsDifferent()
        {
            Guid first = Guid.NewGuid();
            Guid second = first.Previous();
            second.Should().NotBe(first);
        }
    }
}
