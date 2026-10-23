using Shouldly;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    // The standard factory of Activity-on-Arrow graphs: that it makes new, independent builders and compilers, that
    // they have the engines of the bundle that the factory was made from (the standard engines, when there is no
    // bundle), that it does not use the ID generators of the bundle, and that many threads can use it.
    public class ArrowGraphFactoryTests
    {
        #region Helpers

        // The engines of a bundle, each behind a proxy that counts the calls that are made to it.
        private sealed class CountedEngines
        {
            private readonly Dictionary<string, CountingProxy> m_Counters = [];

            public CountedEngines()
            {
                var standard = new ArrowGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>>();
                Bundle = new ArrowGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>>
                {
                    DummyActivityGenerator = Count(nameof(Bundle.DummyActivityGenerator), standard.DummyActivityGenerator),
                    EventGenerator = Count(nameof(Bundle.EventGenerator), standard.EventGenerator),
                    SccFinder = Count(nameof(Bundle.SccFinder), standard.SccFinder),
                    CriticalPathEngine = Count(nameof(Bundle.CriticalPathEngine), standard.CriticalPathEngine),
                    ResourceSchedulingEngine = Count(nameof(Bundle.ResourceSchedulingEngine), standard.ResourceSchedulingEngine),
                    DummyEdgeOrchestrator = Count(nameof(Bundle.DummyEdgeOrchestrator), standard.DummyEdgeOrchestrator),
                    TransitiveReducer = Count(nameof(Bundle.TransitiveReducer), standard.TransitiveReducer),
                };
            }

            public ArrowGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>> Bundle { get; }

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
            public int Generate() => throw new InvalidOperationException(@"The factory used an ID generator of the bundle.");
        }

        private static ArrowGraphFactory<int, int, int, IDependentActivity<int, int, int>> FactoryOf(
            ArrowGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>> engines) =>
            new(engines);

        private static Resource<int, int> OneResource() =>
            new(1, @"R1", false, false, InterActivityAllocationType.None, 0.0, 0.0, 0, []);

        // A small plan in which 3 needs 1 and 2, and 4 needs only 2. The arrow network needs a dummy activity for
        // that, and the plan finishes at 9.
        private const int PlanFinish = 9;

        private static void AddPlan(ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>> builder)
        {
            builder.AddActivity(new DependentActivity<int, int, int>(1, 3), []).ShouldBeTrue();
            builder.AddActivity(new DependentActivity<int, int, int>(2, 5), []).ShouldBeTrue();
            builder.AddActivity(new DependentActivity<int, int, int>(3, 2), [1, 2]).ShouldBeTrue();
            builder.AddActivity(new DependentActivity<int, int, int>(4, 4), [2]).ShouldBeTrue();
        }

        private static void AddPlan(ArrowGraphCompiler<int, int, int, IDependentActivity<int, int, int>> compiler)
        {
            compiler.AddActivity(new DependentActivity<int, int, int>(1, 3)).ShouldBeTrue();
            compiler.AddActivity(new DependentActivity<int, int, int>(2, 5)).ShouldBeTrue();
            compiler.AddActivity(new DependentActivity<int, int, int>(3, 2, [1, 2])).ShouldBeTrue();
            compiler.AddActivity(new DependentActivity<int, int, int>(4, 4, [2])).ShouldBeTrue();
        }

        // Takes a builder through every engine: the events, the dummy activities, the finding of cycles, the
        // reduction, the critical path and the scheduling.
        private static void Drive(ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>> builder)
        {
            AddPlan(builder);
            builder.TransitiveReduction().ShouldBeTrue();
            builder.CalculateCriticalPath(TestContext.Current.CancellationToken);
            builder.CalculateResourceSchedulesByPriorityList([OneResource()], TestContext.Current.CancellationToken)
                .ShouldNotBeEmpty();
        }

        // Takes a compiler through the engines that a compile uses. An arrow compile does not schedule resources.
        private static void Drive(ArrowGraphCompiler<int, int, int, IDependentActivity<int, int, int>> compiler)
        {
            AddPlan(compiler);
            compiler.Compile(TestContext.Current.CancellationToken);
        }

        #endregion

        #region The bundle

        [Fact]
        public void ArrowGraphFactory_GivenNoBundle_ThenThrows()
        {
            var thrown = Should.Throw<ArgumentNullException>(() => FactoryOf(null));

            thrown.ParamName.ShouldBe("engines");
        }

        [Theory]
        [InlineData("DummyActivityGenerator", "dummy activities")]
        [InlineData("EventGenerator", "event generator")]
        [InlineData("SccFinder", "strongly connected")]
        [InlineData("CriticalPathEngine", "critical-path")]
        [InlineData("ResourceSchedulingEngine", "resource scheduling")]
        [InlineData("DummyEdgeOrchestrator", "dummy-edge orchestrator")]
        [InlineData("TransitiveReducer", "transitive reducer")]
        public void ArrowGraphFactory_GivenABundleWithoutAnEngine_ThenThrowsAndNamesTheEngine(string engine, string described)
        {
            var engines = new ArrowGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>>();
            switch (engine)
            {
                case nameof(engines.DummyActivityGenerator):
                    engines.DummyActivityGenerator = null;
                    break;
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
                case nameof(engines.DummyEdgeOrchestrator):
                    engines.DummyEdgeOrchestrator = null;
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
        public void ArrowGraphFactory_GivenABundle_ThenABuilderUsesEveryEngineOfIt()
        {
            var counted = new CountedEngines();
            ArrowGraphFactory<int, int, int, IDependentActivity<int, int, int>> factory = FactoryOf(counted.Bundle);

            Drive(factory.CreateBuilder());

            counted.Idle().ShouldBeEmpty();
        }

        [Fact]
        public void ArrowGraphFactory_GivenABundle_ThenACompilerUsesTheEnginesOfItThatACompileUses()
        {
            var counted = new CountedEngines();
            ArrowGraphFactory<int, int, int, IDependentActivity<int, int, int>> factory = FactoryOf(counted.Bundle);

            Drive(factory.CreateCompiler());

            // An arrow compile reduces the graph and finds the critical path, and it schedules no resources.
            counted.Idle().ShouldBe([nameof(counted.Bundle.ResourceSchedulingEngine)]);
        }

        [Fact]
        public void ArrowGraphFactory_GivenABundle_ThenAllTheBuildersShareItsEngines()
        {
            var counted = new CountedEngines();
            ArrowGraphFactory<int, int, int, IDependentActivity<int, int, int>> factory = FactoryOf(counted.Bundle);

            Drive(factory.CreateBuilder());
            int afterFirst = counted.CallsTo(nameof(counted.Bundle.CriticalPathEngine));
            Drive(factory.CreateBuilder());

            // The second builder called the engine that the first had called, and not an engine of its own.
            afterFirst.ShouldBeGreaterThan(0);
            counted.CallsTo(nameof(counted.Bundle.CriticalPathEngine)).ShouldBeGreaterThan(afterFirst);
        }

        [Fact]
        public void ArrowGraphFactory_GivenABundleThatChangesAfterwards_ThenTheFactoryKeepsTheEnginesItHad()
        {
            var counted = new CountedEngines();
            var other = new CountedEngines();
            ArrowGraphFactory<int, int, int, IDependentActivity<int, int, int>> factory = FactoryOf(counted.Bundle);

            counted.Bundle.EventGenerator = other.Bundle.EventGenerator;
            Drive(factory.CreateBuilder());

            counted.CallsTo(nameof(counted.Bundle.EventGenerator)).ShouldBeGreaterThan(0);
            other.CallsTo(nameof(other.Bundle.EventGenerator)).ShouldBe(0);
        }

        [Fact]
        public void ArrowGraphFactory_GivenABundleWithIdGenerators_ThenThoseGeneratorsAreNotUsed()
        {
            var engines = new ArrowGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>>
            {
                EdgeIdGenerator = new UnusedIdGenerator(),
                NodeIdGenerator = new UnusedIdGenerator(),
            };
            ArrowGraphFactory<int, int, int, IDependentActivity<int, int, int>> factory = FactoryOf(engines);

            ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>> builder = factory.CreateBuilder();
            AddPlan(builder);

            // The builder has PreviousIdGenerator objects: they step down from 0.
            builder.NodeIds.ShouldNotBeEmpty();
            builder.NodeIds.ShouldAllBe(x => x < 0);
        }

        #endregion

        #region The builders and the compilers

        [Fact]
        public void ArrowGraphFactory_GivenCreateBuilder_ThenGivesANewEmptyBuilderEachTime()
        {
            var factory = new ArrowGraphFactory<int, int, int, IDependentActivity<int, int, int>>();

            ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>> first = factory.CreateBuilder();
            ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>> second = factory.CreateBuilder();

            first.ShouldNotBeSameAs(second);
            first.ActivityIds.ShouldBeEmpty();
            second.ActivityIds.ShouldBeEmpty();
        }

        [Fact]
        public void ArrowGraphFactory_GivenTwoBuilders_ThenTheGraphOfOneIsNotTheGraphOfTheOther()
        {
            var factory = new ArrowGraphFactory<int, int, int, IDependentActivity<int, int, int>>();
            ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>> first = factory.CreateBuilder();
            ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>> second = factory.CreateBuilder();

            AddPlan(first);

            first.ActivityIds.ShouldContain(1);
            second.ActivityIds.ShouldBeEmpty();
        }

        [Fact]
        public void ArrowGraphFactory_GivenTwoBuilders_ThenEachHasIdGeneratorsOfItsOwn()
        {
            var factory = new ArrowGraphFactory<int, int, int, IDependentActivity<int, int, int>>();
            ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>> first = factory.CreateBuilder();
            ArrowGraphBuilder<int, int, int, IDependentActivity<int, int, int>> second = factory.CreateBuilder();

            AddPlan(first);
            AddPlan(second);

            // The same plan gives the same edges (the dummy activities have IDs from the generator) and the same
            // nodes. If the builders shared generators, the second would carry on from where the first stopped.
            second.EdgeIds.Order().ShouldBe(first.EdgeIds.Order());
            second.NodeIds.Order().ShouldBe(first.NodeIds.Order());
        }

        [Fact]
        public void ArrowGraphFactory_GivenNoBundle_ThenACompilerCompilesAPlan()
        {
            var factory = new ArrowGraphFactory<int, int, int, IDependentActivity<int, int, int>>();

            ArrowGraphCompiler<int, int, int, IDependentActivity<int, int, int>> compiler = factory.CreateCompiler();
            Drive(compiler);

            compiler.FinishTime.ShouldBe(PlanFinish);
        }

        [Fact]
        public void ArrowGraphFactory_GivenCreateCompiler_ThenTheCompilerIsMadeLikeTheDefaultOne()
        {
            var factory = new ArrowGraphFactory<int, int, int, IDependentActivity<int, int, int>>();
            ArrowGraphCompiler<int, int, int, IDependentActivity<int, int, int>> made = factory.CreateCompiler();
            var byDefault = new ArrowGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();

            Drive(made);
            Drive(byDefault);

            // The same plan gives the same edges and nodes, and the same finish. The nodes have IDs from a
            // PreviousIdGenerator, which steps down from 0.
            Graph<int, IDependentActivity<int, int, int>, IEvent<int>> madeGraph = made.ToGraph();
            Graph<int, IDependentActivity<int, int, int>, IEvent<int>> defaultGraph = byDefault.ToGraph();
            int[] defaultNodes = [.. defaultGraph.Nodes.Select(x => x.Id).Order()];
            defaultNodes.ShouldBe([.. madeGraph.Nodes.Select(x => x.Id).Order()]);
            defaultNodes.ShouldNotBeEmpty();
            defaultNodes.ShouldAllBe(x => x < 0);
            defaultGraph.Edges.Select(x => x.Id).Order().ShouldBe(madeGraph.Edges.Select(x => x.Id).Order());
            byDefault.FinishTime.ShouldBe(made.FinishTime);
        }

        [Fact]
        public void ArrowGraphFactory_GivenCreateCompiler_ThenGivesANewCompilerWithABuilderOfItsOwn()
        {
            var factory = new ArrowGraphFactory<int, int, int, IDependentActivity<int, int, int>>();
            ArrowGraphCompiler<int, int, int, IDependentActivity<int, int, int>> first = factory.CreateCompiler();
            ArrowGraphCompiler<int, int, int, IDependentActivity<int, int, int>> second = factory.CreateCompiler();

            Drive(first);
            second.AddActivity(new DependentActivity<int, int, int>(1, 3)).ShouldBeTrue();
            second.Compile(TestContext.Current.CancellationToken);

            // The plan of the second compiler is one activity, and the first compiler's plan is not in it.
            first.ShouldNotBeSameAs(second);
            first.FinishTime.ShouldBe(PlanFinish);
            second.FinishTime.ShouldBe(3);
        }

        #endregion

        #region Many threads

        [Fact]
        public void ArrowGraphFactory_GivenManyThreads_ThenEachCompilerIsItsOwnAndGivesTheSameResult()
        {
            var counted = new CountedEngines();
            ArrowGraphFactory<int, int, int, IDependentActivity<int, int, int>> factory = FactoryOf(counted.Bundle);
            var finishes = new ConcurrentBag<int>();

            Parallel.For(
                0,
                64,
                new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = TestContext.Current.CancellationToken },
                _ =>
                {
                    ArrowGraphCompiler<int, int, int, IDependentActivity<int, int, int>> compiler = factory.CreateCompiler();
                    Drive(compiler);
                    finishes.Add(compiler.FinishTime);
                });

            // The engines of the bundle are shared by all the compilers, and they do not disturb each other.
            finishes.Count.ShouldBe(64);
            finishes.ShouldAllBe(x => x == PlanFinish);
            counted.Idle().ShouldBe([nameof(counted.Bundle.ResourceSchedulingEngine)]);
        }

        #endregion
    }
}
