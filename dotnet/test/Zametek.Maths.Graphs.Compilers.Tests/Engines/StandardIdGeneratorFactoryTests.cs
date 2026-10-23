using Shouldly;
using System;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    // The factory that builders have by default chooses the generator from the type of the ID.
    public class StandardIdGeneratorFactoryTests
    {
        [Fact]
        public void StandardIdGeneratorFactory_GivenCreate_WithIntAndNoIds_ThenGivesDecrementingValuesFromMinusOne()
        {
            var factory = new StandardIdGeneratorFactory<int>();

            IIdGenerator<int> generator = factory.Create([]);

            generator.Generate().ShouldBe(-1);
            generator.Generate().ShouldBe(-2);
        }

        [Fact]
        public void StandardIdGeneratorFactory_GivenCreate_WithIntAndIds_ThenStartsUnderTheLowestId()
        {
            var factory = new StandardIdGeneratorFactory<int>();

            IIdGenerator<int> generator = factory.Create([5, -7, 3]);

            generator.Generate().ShouldBe(-8);
        }

        [Fact]
        public void StandardIdGeneratorFactory_GivenCreate_WithInt_ThenGivesAPreviousIdGenerator()
        {
            var factory = new StandardIdGeneratorFactory<int>();

            factory.Create([]).ShouldBeOfType<PreviousIdGenerator<int>>();
        }

        [Fact]
        public void StandardIdGeneratorFactory_GivenCreate_WithGuid_ThenGivesANextIdGeneratorAndNewGuids()
        {
            var factory = new StandardIdGeneratorFactory<Guid>();
            Guid[] used = [Guid.NewGuid(), Guid.NewGuid()];

            IIdGenerator<Guid> generator = factory.Create(used);
            Guid[] made = [.. Enumerable.Range(0, 20).Select(_ => generator.Generate())];

            generator.ShouldBeOfType<NextIdGenerator<Guid>>();
            made.Distinct().Count().ShouldBe(20);
            made.Intersect(used).ShouldBeEmpty();
        }

        [Fact]
        public void StandardIdGeneratorFactory_GivenCreate_WithAnotherTypeOfId_ThenTheGeneratorThrowsWhenAsked()
        {
            // There is no key generation for this type, and so the generator says so when it is asked for an ID.
            var factory = new StandardIdGeneratorFactory<long>();

            IIdGenerator<long> generator = factory.Create([]);

            var thrown = Should.Throw<InvalidOperationException>(() => generator.Generate());
            thrown.Message.ShouldContain("not supported");
        }

        [Fact]
        public void StandardIdGeneratorFactory_GivenCreate_WithNoSequence_ThenThrows()
        {
            var factory = new StandardIdGeneratorFactory<int>();

            var thrown = Should.Throw<ArgumentNullException>(() => factory.Create(null));

            thrown.ParamName.ShouldBe("usedIds");
        }

        [Fact]
        public void StandardIdGeneratorFactory_GivenANewBundle_ThenTheBundleHasIt()
        {
            new VertexGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>>().EdgeIdGeneratorFactory
                .ShouldBeOfType<StandardIdGeneratorFactory<int>>();

            var arrow = new ArrowGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>>();
            arrow.EdgeIdGeneratorFactory.ShouldBeOfType<StandardIdGeneratorFactory<int>>();
            arrow.NodeIdGeneratorFactory.ShouldBeOfType<StandardIdGeneratorFactory<int>>();
        }
    }
}
