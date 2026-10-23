using Shouldly;
using System;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class NextIdGeneratorFactoryTests
    {
        [Fact]
        public void NextIdGeneratorFactory_GivenCreate_WithNoIds_ThenGivesIncrementingValuesFromOne()
        {
            var factory = new NextIdGeneratorFactory<int>();

            IIdGenerator<int> generator = factory.Create([]);

            generator.Generate().ShouldBe(1);
            generator.Generate().ShouldBe(2);
            generator.Generate().ShouldBe(3);
        }

        [Fact]
        public void NextIdGeneratorFactory_GivenCreate_WithInitialValue_ThenGivesValuesAfterIt()
        {
            var factory = new NextIdGeneratorFactory<int>(10);

            IIdGenerator<int> generator = factory.Create([]);

            generator.Generate().ShouldBe(11);
            generator.Generate().ShouldBe(12);
        }

        [Fact]
        public void NextIdGeneratorFactory_GivenCreate_WithIdsAboveInitial_ThenStartsOverTheHighestId()
        {
            var factory = new NextIdGeneratorFactory<int>();

            IIdGenerator<int> generator = factory.Create([5, 17, 3]);

            generator.Generate().ShouldBe(18);
            generator.Generate().ShouldBe(19);
        }

        [Fact]
        public void NextIdGeneratorFactory_GivenCreate_WithIdsAllBelowInitial_ThenStartsFromInitial()
        {
            var factory = new NextIdGeneratorFactory<int>();

            IIdGenerator<int> generator = factory.Create([-4, -2]);

            generator.Generate().ShouldBe(1);
        }

        [Fact]
        public void NextIdGeneratorFactory_GivenCreate_WithIdsAllBelowANonZeroInitial_ThenStartsFromInitial()
        {
            var factory = new NextIdGeneratorFactory<int>(100);

            IIdGenerator<int> generator = factory.Create([20, 30]);

            generator.Generate().ShouldBe(101);
        }

        [Fact]
        public void NextIdGeneratorFactory_GivenCreate_WithIdsAboveANonZeroInitial_ThenStartsOverTheHighestId()
        {
            var factory = new NextIdGeneratorFactory<int>(100);

            IIdGenerator<int> generator = factory.Create([20, 130]);

            generator.Generate().ShouldBe(131);
        }

        [Fact]
        public void NextIdGeneratorFactory_GivenCreate_WithNoSequence_ThenThrows()
        {
            var factory = new NextIdGeneratorFactory<int>();

            var thrown = Should.Throw<ArgumentNullException>(() => factory.Create(null));

            thrown.ParamName.ShouldBe("usedIds");
        }

        [Fact]
        public void NextIdGeneratorFactory_GivenCreateTwice_ThenTheGeneratorsShareNoState()
        {
            var factory = new NextIdGeneratorFactory<int>();
            IIdGenerator<int> first = factory.Create([]);
            IIdGenerator<int> second = factory.Create([]);

            first.Generate().ShouldBe(1);
            first.Generate().ShouldBe(2);

            second.Generate().ShouldBe(1);
        }

        [Fact]
        public void NextIdGeneratorFactory_GivenCreate_ThenGivesANextIdGenerator()
        {
            var factory = new NextIdGeneratorFactory<int>();

            factory.Create([]).ShouldBeOfType<NextIdGenerator<int>>();
        }
    }
}
