using Shouldly;
using System;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class DummyActivityGeneratorTests
    {
        [Fact]
        public void DummyActivityGenerator_GivenGenerate_ThenReturnsRemovableZeroDurationActivityWithGivenId()
        {
            var generator = new DummyActivityGenerator<int, int, int, IActivity<int, int, int>>();

            IActivity<int, int, int> output = generator.Generate(7);

            output.ShouldNotBeNull();
            output.Id.ShouldBe(7);
            output.Duration.ShouldBe(0);
            output.CanBeRemoved.ShouldBeTrue();
        }
    }
}
