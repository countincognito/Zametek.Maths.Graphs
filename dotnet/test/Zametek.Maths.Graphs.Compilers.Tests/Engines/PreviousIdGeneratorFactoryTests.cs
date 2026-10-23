using Shouldly;
using System;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class PreviousIdGeneratorFactoryTests
    {
        [Fact]
        public void PreviousIdGeneratorFactory_GivenCreate_WithNoIds_ThenGivesDecrementingValuesFromMinusOne()
        {
            var factory = new PreviousIdGeneratorFactory<int>();

            IIdGenerator<int> generator = factory.Create([]);

            generator.Generate().ShouldBe(-1);
            generator.Generate().ShouldBe(-2);
            generator.Generate().ShouldBe(-3);
        }

        [Fact]
        public void PreviousIdGeneratorFactory_GivenCreate_WithInitialValue_ThenGivesValuesBeforeIt()
        {
            var factory = new PreviousIdGeneratorFactory<int>(10);

            IIdGenerator<int> generator = factory.Create([]);

            generator.Generate().ShouldBe(9);
            generator.Generate().ShouldBe(8);
        }

        [Fact]
        public void PreviousIdGeneratorFactory_GivenCreate_WithIdsBelowInitial_ThenStartsUnderTheLowestId()
        {
            var factory = new PreviousIdGeneratorFactory<int>();

            IIdGenerator<int> generator = factory.Create([5, -7, 3]);

            generator.Generate().ShouldBe(-8);
            generator.Generate().ShouldBe(-9);
        }

        [Fact]
        public void PreviousIdGeneratorFactory_GivenCreate_WithIdsAllAboveInitial_ThenStartsFromInitial()
        {
            // The IDs are those of activities, which are positive. The new IDs stay negative.
            var factory = new PreviousIdGeneratorFactory<int>();

            IIdGenerator<int> generator = factory.Create([5, 3, 100]);

            generator.Generate().ShouldBe(-1);
            generator.Generate().ShouldBe(-2);
        }

        [Fact]
        public void PreviousIdGeneratorFactory_GivenCreate_WithIdsAllAboveANonZeroInitial_ThenStartsFromInitial()
        {
            var factory = new PreviousIdGeneratorFactory<int>(10);

            IIdGenerator<int> generator = factory.Create([20, 30]);

            generator.Generate().ShouldBe(9);
        }

        [Fact]
        public void PreviousIdGeneratorFactory_GivenCreate_WithIdsBelowANonZeroInitial_ThenStartsUnderTheLowestId()
        {
            var factory = new PreviousIdGeneratorFactory<int>(10);

            IIdGenerator<int> generator = factory.Create([4, 20]);

            generator.Generate().ShouldBe(3);
        }

        [Fact]
        public void PreviousIdGeneratorFactory_GivenCreate_WithNoSequence_ThenThrows()
        {
            var factory = new PreviousIdGeneratorFactory<int>();

            var thrown = Should.Throw<ArgumentNullException>(() => factory.Create(null));

            thrown.ParamName.ShouldBe("usedIds");
        }

        [Fact]
        public void PreviousIdGeneratorFactory_GivenCreateTwice_ThenTheGeneratorsShareNoState()
        {
            var factory = new PreviousIdGeneratorFactory<int>();
            IIdGenerator<int> first = factory.Create([]);
            IIdGenerator<int> second = factory.Create([]);

            first.Generate().ShouldBe(-1);
            first.Generate().ShouldBe(-2);

            second.Generate().ShouldBe(-1);
        }

        [Fact]
        public void PreviousIdGeneratorFactory_GivenCreate_ThenGivesAPreviousIdGenerator()
        {
            var factory = new PreviousIdGeneratorFactory<int>();

            factory.Create([]).ShouldBeOfType<PreviousIdGenerator<int>>();
        }
    }
}
