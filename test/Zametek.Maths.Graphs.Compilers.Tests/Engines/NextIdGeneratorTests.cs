using Shouldly;
using System;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class NextIdGeneratorTests
    {
        [Fact]
        public void NextIdGenerator_GivenGenerate_WithDefaultInitial_ThenReturnsIncrementingValuesFromOne()
        {
            var generator = new NextIdGenerator<int>();

            generator.Generate().ShouldBe(1);
            generator.Generate().ShouldBe(2);
            generator.Generate().ShouldBe(3);
        }

        [Fact]
        public void NextIdGenerator_GivenGenerate_WithInitialValue_ThenReturnsValuesAfterInitial()
        {
            var generator = new NextIdGenerator<int>(10);

            generator.Generate().ShouldBe(11);
            generator.Generate().ShouldBe(12);
        }
    }
}
