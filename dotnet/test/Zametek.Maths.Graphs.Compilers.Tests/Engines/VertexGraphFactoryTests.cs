using Shouldly;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    // The standard factory of Activity-on-Vertex graphs: that it makes new, independent builders and compilers, that
    // they have the engines of the bundle that the factory was made from (the standard engines, when there is no
    // bundle), that it does not use the ID generator of the bundle, and that many threads can use it.
    public class VertexGraphFactoryTests
    {
        #region Helpers

        // The engines of a bundle, each behind a proxy that counts the calls that are made to it.
        private sealed class CountedEngines
        {
            private readonly Dictionary<string, CountingProxy> m_Counters = [];

            public CountedEngines()
            {
                var standard = new VertexGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>>();
                Bundle = new VertexGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>>
                {
                    EventGenerator = Count(nameof(Bundle.EventGenerator), standard.EventGenerator),
                    SccFinder = Count(nameof(Bundle.SccFinder), standard.SccFinder),
                    CriticalPathEngine = Count(nameof(Bundle.CriticalPathEngine), standard.CriticalPathEngine),
                    ResourceSchedulingEngine = Count(nameof(Bundle.ResourceSchedulingEngine), standard.ResourceSchedulingEngine),
                    TransitiveReducer = Count(nameof(Bundle.TransitiveReducer), standard.TransitiveReducer),
                };
            }

            public VertexGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>> Bundle { get; }

            // The number of calls to the engine of the bundle with the given name.
            public int CallsTo(string engine) => m_Counters[engine].Calls;

            // The names of the engines that have had no call.
            public List<string> Idle() => m_Counters.Where(x => x.Value.Calls == 0).Select(x => x.Key).Order().ToList();

            private TEngine Count<TEngine>(string name, TEngine engine)
                where TEngine : class
            {
                TEngine proxy = CountingProxy.Wrap(engine, out CountingProxy counter);
                m_Counters.Add(name, counter);
                return proxy;
            }
        }

        // An ID generator that must not be used.
        private sealed class UnusedIdGenerator
            : IIdGenerator<int>
        {
            public int Generate() => throw new InvalidOperationException(@"The factory used the ID generator of the bundle.");
        }

        private static VertexGraphFactory<int, int, int, IDependentActivity<int, int, int>> FactoryOf(
            VertexGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>> engines) =>
            new(engines);

        private static Resource<int, int> OneResource() =>
            new(1, @"R1", false, false, InterActivityAllocationType.None, 0.0, 0.0, 0, []);

        // A small plan: 1 then 2 then 3, where 3 also names 1, which 2 implies, and so the reduction has work to do.
        // With one resource the plan finishes at 10.
        private const int PlanFinish = 10;

        private static void AddPlan(VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> builder)
        {
            builder.AddActivity(new DependentActivity<int, int, int>(1, 3), []).ShouldBeTrue();
            builder.AddActivity(new DependentActivity<int, int, int>(2, 5), [1]).ShouldBeTrue();
            builder.AddActivity(new DependentActivity<int, int, int>(3, 2), [1, 2]).ShouldBeTrue();
        }

        private static void AddPlan(VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>> compiler)
        {
            compiler.AddActivity(new DependentActivity<int, int, int>(1, 3)).ShouldBeTrue();
            compiler.AddActivity(new DependentActivity<int, int, int>(2, 5, [1])).ShouldBeTrue();
            compiler.AddActivity(new DependentActivity<int, int, int>(3, 2, [1, 2])).ShouldBeTrue();
        }

        // Takes a builder through every engine: the events, the finding of cycles, the reduction, the critical path
        // and the scheduling.
        private static void Drive(VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> builder)
        {
            AddPlan(builder);
            builder.TransitiveReduction().ShouldBeTrue();
            builder.CalculateCriticalPath();
            builder.CalculateResourceSchedulesByPriorityList([OneResource()], TestContext.Current.CancellationToken)
                .ShouldNotBeEmpty();
        }

        // Takes a compiler through every engine.
        private static void Drive(VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>> compiler)
        {
            AddPlan(compiler);
            compiler.TransitiveReduction();
            compiler.Compile([OneResource()], TestContext.Current.CancellationToken)
                .CompilationErrors.ShouldBeEmpty();
        }

        #endregion

        #region The bundle

        [Fact]
        public void VertexGraphFactory_GivenNoBundle_ThenThrows()
        {
            var thrown = Should.Throw<ArgumentNullException>(() => FactoryOf(null));

            thrown.ParamName.ShouldBe("engines");
        }

        [Theory]
        [InlineData("EventGenerator", "event generator")]
        [InlineData("SccFinder", "strongly connected")]
        [InlineData("CriticalPathEngine", "critical-path")]
        [InlineData("ResourceSchedulingEngine", "resource scheduling")]
        [InlineData("TransitiveReducer", "transitive reducer")]
        public void VertexGraphFactory_GivenABundleWithoutAnEngine_ThenThrowsAndNamesTheEngine(string engine, string described)
        {
            var engines = new VertexGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>>();
            switch (engine)
            {
                case nameof(engines.EventGenerator):
                    engines.EventGenerator = null;
                    break;
                case nameof(engines.SccFinder):
                    engines.SccFinder = null;
                    break;
                case nameof(engines.CriticalPathEngine):
                    engines.CriticalPathEngine = null;
                    break;
                case nameof(engines.ResourceSchedulingEngine):
                    engines.ResourceSchedulingEngine = null;
                    break;
                case nameof(engines.TransitiveReducer):
                    engines.TransitiveReducer = null;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(engine));
            }

            var thrown = Should.Throw<ArgumentNullException>(() => FactoryOf(engines));

            thrown.ParamName.ShouldBe("engines");
            thrown.Message.ShouldContain(described);
        }

        [Fact]
        public void VertexGraphFactory_GivenABundle_ThenABuilderUsesEveryEngineOfIt()
        {
            var counted = new CountedEngines();
            VertexGraphFactory<int, int, int, IDependentActivity<int, int, int>> factory = FactoryOf(counted.Bundle);

            Drive(factory.CreateBuilder());

            counted.Idle().ShouldBeEmpty();
        }

        [Fact]
        public void VertexGraphFactory_GivenABundle_ThenACompilerUsesEveryEngineOfIt()
        {
            var counted = new CountedEngines();
            VertexGraphFactory<int, int, int, IDependentActivity<int, int, int>> factory = FactoryOf(counted.Bundle);

            Drive(factory.CreateCompiler());

            counted.Idle().ShouldBeEmpty();
        }

        [Fact]
        public void VertexGraphFactory_GivenABundle_ThenAllTheBuildersShareItsEngines()
        {
            var counted = new CountedEngines();
            VertexGraphFactory<int, int, int, IDependentActivity<int, int, int>> factory = FactoryOf(counted.Bundle);

            Drive(factory.CreateBuilder());
            int afterFirst = counted.CallsTo(nameof(counted.Bundle.CriticalPathEngine));
            Drive(factory.CreateBuilder());

            // The second builder called the engine that the first had called, and not an engine of its own.
            afterFirst.ShouldBeGreaterThan(0);
            counted.CallsTo(nameof(counted.Bundle.CriticalPathEngine)).ShouldBeGreaterThan(afterFirst);
        }

        [Fact]
        public void VertexGraphFactory_GivenABundleThatChangesAfterwards_ThenTheFactoryKeepsTheEnginesItHad()
        {
            var counted = new CountedEngines();
            var other = new CountedEngines();
            VertexGraphFactory<int, int, int, IDependentActivity<int, int, int>> factory = FactoryOf(counted.Bundle);

            counted.Bundle.EventGenerator = other.Bundle.EventGenerator;
            Drive(factory.CreateBuilder());

            counted.CallsTo(nameof(counted.Bundle.EventGenerator)).ShouldBeGreaterThan(0);
            other.CallsTo(nameof(other.Bundle.EventGenerator)).ShouldBe(0);
        }

        [Fact]
        public void VertexGraphFactory_GivenABundleWithAnIdGenerator_ThenThatGeneratorIsNotUsed()
        {
            var engines = new VertexGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>>
            {
                EdgeIdGenerator = new UnusedIdGenerator(),
            };
            VertexGraphFactory<int, int, int, IDependentActivity<int, int, int>> factory = FactoryOf(engines);

            VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> builder = factory.CreateBuilder();
            AddPlan(builder);

            // The builder has a PreviousIdGenerator: it steps down from 0.
            builder.EventIds.ShouldNotBeEmpty();
            builder.EventIds.ShouldAllBe(x => x < 0);
        }

        #endregion

        #region The builders and the compilers

        [Fact]
        public void VertexGraphFactory_GivenCreateBuilder_ThenGivesANewEmptyBuilderEachTime()
        {
            var factory = new VertexGraphFactory<int, int, int, IDependentActivity<int, int, int>>();

            VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> first = factory.CreateBuilder();
            VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> second = factory.CreateBuilder();

            first.ShouldNotBeSameAs(second);
            first.ActivityIds.ShouldBeEmpty();
            second.ActivityIds.ShouldBeEmpty();
        }

        [Fact]
        public void VertexGraphFactory_GivenTwoBuilders_ThenTheGraphOfOneIsNotTheGraphOfTheOther()
        {
            var factory = new VertexGraphFactory<int, int, int, IDependentActivity<int, int, int>>();
            VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> first = factory.CreateBuilder();
            VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> second = factory.CreateBuilder();

            AddPlan(first);

            first.ActivityIds.Count().ShouldBe(3);
            second.ActivityIds.ShouldBeEmpty();
        }

        [Fact]
        public void VertexGraphFactory_GivenTwoBuilders_ThenEachHasAnIdGeneratorOfItsOwn()
        {
            var factory = new VertexGraphFactory<int, int, int, IDependentActivity<int, int, int>>();
            VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> first = factory.CreateBuilder();
            VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> second = factory.CreateBuilder();

            AddPlan(first);
            AddPlan(second);

            // The same plan gives the same events. If the builders shared a generator, the second would carry on
            // from where the first stopped.
            second.EventIds.Order().ShouldBe(first.EventIds.Order());
        }

        [Fact]
        public void VertexGraphFactory_GivenNoBundle_ThenACompilerCompilesAPlan()
        {
            var factory = new VertexGraphFactory<int, int, int, IDependentActivity<int, int, int>>();

            VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>> compiler = factory.CreateCompiler();
            Drive(compiler);

            compiler.FinishTime.ShouldBe(PlanFinish);
        }

        [Fact]
        public void VertexGraphFactory_GivenCreateCompiler_ThenTheCompilerIsMadeLikeTheDefaultOne()
        {
            var factory = new VertexGraphFactory<int, int, int, IDependentActivity<int, int, int>>();
            VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>> made = factory.CreateCompiler();
            var byDefault = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();

            Drive(made);
            Drive(byDefault);

            // The same plan gives the same events and the same finish. The events have IDs from a
            // PreviousIdGenerator, which steps down from 0.
            int[] madeEvents = [.. made.ToGraph().Edges.Select(x => x.Id).Order()];
            int[] defaultEvents = [.. byDefault.ToGraph().Edges.Select(x => x.Id).Order()];
            defaultEvents.ShouldBe(madeEvents);
            defaultEvents.ShouldNotBeEmpty();
            defaultEvents.ShouldAllBe(x => x < 0);
            byDefault.FinishTime.ShouldBe(made.FinishTime);
        }

        [Fact]
        public void VertexGraphFactory_GivenCreateCompiler_ThenGivesANewCompilerWithABuilderOfItsOwn()
        {
            var factory = new VertexGraphFactory<int, int, int, IDependentActivity<int, int, int>>();
            VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>> first = factory.CreateCompiler();
            VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>> second = factory.CreateCompiler();

            Drive(first);
            second.AddActivity(new DependentActivity<int, int, int>(1, 3)).ShouldBeTrue();
            second.Compile([OneResource()], TestContext.Current.CancellationToken).CompilationErrors.ShouldBeEmpty();

            // The plan of the second compiler is one activity, and the first compiler's plan is not in it.
            first.ShouldNotBeSameAs(second);
            first.FinishTime.ShouldBe(PlanFinish);
            second.FinishTime.ShouldBe(3);
        }

        #endregion

        #region Many threads

        [Fact]
        public void VertexGraphFactory_GivenManyThreads_ThenEachCompilerIsItsOwnAndGivesTheSameResult()
        {
            var counted = new CountedEngines();
            VertexGraphFactory<int, int, int, IDependentActivity<int, int, int>> factory = FactoryOf(counted.Bundle);
            var finishes = new ConcurrentBag<int>();

            Parallel.For(
                0,
                64,
                new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = TestContext.Current.CancellationToken },
                _ =>
                {
                    VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>> compiler = factory.CreateCompiler();
                    Drive(compiler);
                    finishes.Add(compiler.FinishTime);
                });

            // The engines of the bundle are shared by all the compilers, and they do not disturb each other.
            finishes.Count.ShouldBe(64);
            finishes.ShouldAllBe(x => x == PlanFinish);
            counted.Idle().ShouldBeEmpty();
        }

        #endregion
    }
}
