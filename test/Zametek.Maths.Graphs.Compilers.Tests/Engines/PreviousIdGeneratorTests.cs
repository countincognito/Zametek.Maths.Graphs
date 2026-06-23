using Shouldly;
using System;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class PreviousIdGeneratorTests
    {
        [Fact]
        public void PreviousIdGenerator_GivenGenerate_WithDefaultInitial_ThenReturnsDecrementingValuesFromMinusOne()
        {
            var generator = new PreviousIdGenerator<int>();

            generator.Generate().ShouldBe(-1);
            generator.Generate().ShouldBe(-2);
            generator.Generate().ShouldBe(-3);
        }

        [Fact]
        public void PreviousIdGenerator_GivenGenerate_WithInitialValue_ThenReturnsValuesBeforeInitial()
        {
            var generator = new PreviousIdGenerator<int>(10);

            generator.Generate().ShouldBe(9);
            generator.Generate().ShouldBe(8);
        }
    }
}
