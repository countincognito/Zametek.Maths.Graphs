using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    // A clone of a builder asks the ID generator factory of its original for generators of its own, for the IDs that
    // the graph copy holds. Thus the generators of a clone are of the same kind as the generators of the original,
    // also when the kind is not one of the library, and the clone gives the IDs that the original would give.
    public class GraphBuilderCloneIdGeneratorTests
    {
        #region Helpers

        // A factory of a kind that the library does not have: its generators give the multiples of a step, over the
        // highest ID that the graph holds.
        private sealed class StepIdGeneratorFactory
            : IIdGeneratorFactory<int>
        {
            private readonly int m_Step;

            public StepIdGeneratorFactory(int step)
            {
                m_Step = step;
            }

            public IIdGenerator<int> Create(IEnumerable<int> usedIds) =>
                new StepIdGenerator(m_Step, Math.Max(usedIds.DefaultIfEmpty(0).Max(), 0) / m_Step * m_Step);

            private sealed class StepIdGenerator
                : IIdGenerator<int>
            {
                private readonly int m_Step;
                private int m_Value;

                public StepIdGenerator(int step, int start)
                {
                    m_Step = step;
                    m_Value = start;
                }

                public int Generate()
                {
                    m_Value += m_Step;
                    return m_Value;
                }
            }
        }

        private static VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> VertexPlan(IIdGeneratorFactory<int> factory)
        {
            var builder = new VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(factory);
            builder.AddActivity(new DependentActivity<int, int, int>(1, 3), []).ShouldBeTrue();
            builder.AddActivity(new DependentActivity<int, int, int>(2, 5), [1]).ShouldBeTrue();
            return builder;
        }

        // 3 needs 1 and 2, and 4 needs only 2, which needs a dummy activity.
        private static ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>> ArrowPlan(
            IIdGeneratorFactory<int> edgeFactory,
            IIdGeneratorFactory<int> nodeFactory)
        {
            var builder = new ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(edgeFactory, nodeFactory);
            builder.AddActivity(new DependentActivity<int, int, int>(1, 3), []).ShouldBeTrue();
            builder.AddActivity(new DependentActivity<int, int, int>(2, 5), []).ShouldBeTrue();
            builder.AddActivity(new DependentActivity<int, int, int>(3, 2), [1, 2]).ShouldBeTrue();
            builder.AddActivity(new DependentActivity<int, int, int>(4, 4), [2]).ShouldBeTrue();
            return builder;
        }

        private static void AddToVertex(VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> builder) =>
            builder.AddActivity(new DependentActivity<int, int, int>(3, 2), [2]).ShouldBeTrue();

        // The ID of the activity that is added to an arrow plan: no generator of these tests gives it.
        private const int ArrowActivityId = 1_003;

        private static void AddToArrow(ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>> builder) =>
            builder.AddActivity(new DependentActivity<int, int, int>(ArrowActivityId, 1), [3, 4]).ShouldBeTrue();

        #endregion

        #region Vertex

        [Fact]
        public void VertexGraphBuilder_GivenCloneObject_AndAFactoryThatStepsUp_ThenTheCloneStepsUp()
        {
            VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> original = VertexPlan(new NextIdGeneratorFactory<int>(1_000_000));
            original.EventIds.ShouldBe([1_000_001]);

            var clone = (VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>)original.CloneObject();
            AddToVertex(clone);

            // The new event is over the old one, and not under it, as a generator that steps down would make it.
            clone.EventIds.Order().ShouldBe([1_000_001, 1_000_002]);
        }

        [Fact]
        public void VertexGraphBuilder_GivenCloneObject_AndTheStandardFactory_ThenTheCloneStepsDownByOne()
        {
            VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> original = VertexPlan(new StandardIdGeneratorFactory<int>());
            original.EventIds.ShouldBe([-1]);

            var clone = (VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>)original.CloneObject();
            AddToVertex(clone);

            // The next event is -2: there is no gap between the events of the clone.
            clone.EventIds.Order().ShouldBe([-2, -1]);
        }

        [Fact]
        public void VertexGraphBuilder_GivenCloneObject_OfAnEmptyBuilder_ThenTheCloneGivesTheIdsOfANewBuilder()
        {
            var original = new VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(new StandardIdGeneratorFactory<int>());

            var clone = (VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>)original.CloneObject();
            clone.AddActivity(new DependentActivity<int, int, int>(1, 3), []).ShouldBeTrue();
            clone.AddActivity(new DependentActivity<int, int, int>(2, 5), [1]).ShouldBeTrue();

            clone.EventIds.ShouldBe([-1]);
        }

        [Theory]
        [InlineData("Next")]
        [InlineData("Previous")]
        [InlineData("Standard")]
        [InlineData("Step")]
        public void VertexGraphBuilder_GivenCloneObject_ThenTheCloneGivesTheIdsThatTheOriginalGives(string kind)
        {
            VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> original = VertexPlan(FactoryOf(kind));
            var clone = (VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>)original.CloneObject();

            AddToVertex(original);
            AddToVertex(clone);

            clone.EventIds.Order().ShouldBe(original.EventIds.Order());
            original.EventIds.Count().ShouldBe(2);
        }

        [Fact]
        public void VertexGraphBuilder_GivenCloneObject_AndAFactoryOfAnotherKind_ThenTheCloneHasAGeneratorOfThatKind()
        {
            VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> original = VertexPlan(new StepIdGeneratorFactory(7));
            original.EventIds.ShouldBe([7]);

            var clone = (VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>)original.CloneObject();
            AddToVertex(clone);

            clone.EventIds.Order().ShouldBe([7, 14]);
        }

        [Fact]
        public void VertexGraphBuilder_GivenCloneObject_ThenTheClonesGeneratorIsNotTheGeneratorOfTheOriginal()
        {
            VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> original = VertexPlan(new StandardIdGeneratorFactory<int>());
            var clone = (VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>)original.CloneObject();

            AddToVertex(clone);
            clone.AddActivity(new DependentActivity<int, int, int>(4, 1), [3]).ShouldBeTrue();
            AddToVertex(original);

            // The clone made two events, and that did not move the generator of the original.
            clone.EventIds.Order().ShouldBe([-3, -2, -1]);
            original.EventIds.Order().ShouldBe([-2, -1]);
        }

        #endregion

        #region Arrow

        [Fact]
        public void ArrowGraphBuilder_GivenCloneObject_AndFactoriesThatStepUp_ThenWhatTheCloneMakesStepsUp()
        {
            ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>> original = ArrowPlan(
                new NextIdGeneratorFactory<int>(1_000_000),
                new NextIdGeneratorFactory<int>(2_000_000));
            int highestNode = original.NodeIds.Max();
            int highestEdge = original.EdgeIds.Max();

            var clone = (ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>>)original.CloneObject();
            AddToArrow(clone);

            List<int> nodes = clone.NodeIds.Except(original.NodeIds).ToList();
            nodes.ShouldNotBeEmpty();
            nodes.ShouldAllBe(x => x > highestNode);
            List<int> dummies = clone.EdgeIds.Except(original.EdgeIds).Except([ArrowActivityId]).ToList();
            dummies.ShouldNotBeEmpty();
            dummies.ShouldAllBe(x => x > highestEdge);
        }

        [Fact]
        public void ArrowGraphBuilder_GivenCloneObject_AndTheStandardFactories_ThenWhatTheCloneMakesStepsDown()
        {
            ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>> original = ArrowPlan(
                new StandardIdGeneratorFactory<int>(),
                new StandardIdGeneratorFactory<int>());
            int lowestNode = original.NodeIds.Min();
            int lowestEdge = original.EdgeIds.Min();

            var clone = (ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>>)original.CloneObject();
            AddToArrow(clone);

            List<int> nodes = clone.NodeIds.Except(original.NodeIds).ToList();
            nodes.ShouldNotBeEmpty();
            nodes.ShouldAllBe(x => x < lowestNode);
            List<int> dummies = clone.EdgeIds.Except(original.EdgeIds).Except([ArrowActivityId]).ToList();
            dummies.ShouldNotBeEmpty();
            dummies.ShouldAllBe(x => x < lowestEdge);
        }

        [Theory]
        [InlineData("Next")]
        [InlineData("Previous")]
        [InlineData("Standard")]
        [InlineData("Step")]
        public void ArrowGraphBuilder_GivenCloneObject_ThenTheCloneGivesTheIdsThatTheOriginalGives(string kind)
        {
            ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>> original = ArrowPlan(FactoryOf(kind), FactoryOf(kind, 11));
            var clone = (ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>>)original.CloneObject();

            AddToArrow(original);
            AddToArrow(clone);

            clone.NodeIds.Order().ShouldBe(original.NodeIds.Order());
            clone.EdgeIds.Order().ShouldBe(original.EdgeIds.Order());
        }

        [Fact]
        public void ArrowGraphBuilder_GivenCloneObject_AndFactoriesOfAnotherKind_ThenTheCloneHasGeneratorsOfThatKind()
        {
            ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>> original = ArrowPlan(new StepIdGeneratorFactory(5), new StepIdGeneratorFactory(11));
            int highestNode = original.NodeIds.Max();
            int highestEdge = original.EdgeIds.Max();

            var clone = (ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>>)original.CloneObject();
            AddToArrow(clone);

            // Every node of the original is a multiple of 11, and so is every node that the clone made.
            original.NodeIds.ShouldAllBe(x => x % 11 == 0);
            List<int> nodes = clone.NodeIds.Except(original.NodeIds).ToList();
            nodes.ShouldNotBeEmpty();
            nodes.ShouldAllBe(x => x > highestNode && x % 11 == 0);
            // The dummy activities are the edges that are not activities, and the same holds for them with 5.
            List<int> dummies = clone.EdgeIds.Except(original.EdgeIds).Except([ArrowActivityId]).ToList();
            dummies.ShouldNotBeEmpty();
            dummies.ShouldAllBe(x => x > highestEdge && x % 5 == 0);
        }

        #endregion

        // A factory of the given kind. The step is that of the factory of the kind Step.
        private static IIdGeneratorFactory<int> FactoryOf(string kind, int step = 7) =>
            kind switch
            {
                "Next" => new NextIdGeneratorFactory<int>(1_000_000),
                "Previous" => new PreviousIdGeneratorFactory<int>(-1_000),
                "Standard" => new StandardIdGeneratorFactory<int>(),
                "Step" => new StepIdGeneratorFactory(step),
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };
    }
}
