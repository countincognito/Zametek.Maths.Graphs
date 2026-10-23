using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    // A simulation schedules a plan with the builders that a factory of its caller gives it, and so with the
    // engines of those builders. These tests show that the simulator uses the builders (and the engines) of the
    // factory, in the first check of the plan and in every iteration, that a factory with the standard engines
    // gives the result of the simulator with no factory, that an engine that schedules in another way changes
    // the result, and that a type of activity of the caller's own is kept.
    //
    // The first check of the plan is a compile, and the compiler for it comes from the same factory. These tests
    // also show that the simulator asks the factory for one compiler, that the check is a compile by the
    // compiler that it gives, and that a factory that is missing, or that gives nothing, is refused.
    public class ScheduleSimulatorFactoryTests
    {
        #region Helpers

        // The standard factory, with the given scheduler in place of the standard one.
        private static VertexGraphFactory<int, int, int, IDependentActivity<int, int, int>> FactoryOf(
            IResourceSchedulingEngine<int, int, int> scheduler = null) =>
            new(new VertexGraphBuilderEngines<int, int, int, IDependentActivity<int, int, int>>
            {
                ResourceSchedulingEngine = scheduler ?? new PriorityListResourceScheduler<int, int, int>(),
            });

        private static ScheduleSimulator<int, int, int, IDependentActivity<int, int, int>> SimulatorOf(
            SpikePlan plan,
            IVertexGraphFactory<int, int, int, IDependentActivity<int, int, int>> factory) =>
            new(
                ScheduleSimulatorTests.ActivitiesOf(plan),
                plan.NewResources(true),
                plan.NewWorkStreams(),
                ScheduleSimulatorTests.EstimatesOf(plan),
                factory);

        private static ScheduleSimulator<int, int, int> StandardSimulatorOf(SpikePlan plan) =>
            new(
                ScheduleSimulatorTests.ActivitiesOf(plan),
                plan.NewResources(true),
                plan.NewWorkStreams(),
                ScheduleSimulatorTests.EstimatesOf(plan));

        // Passes every call to the factory that it wraps, and counts the builders and the compilers that it asks for.
        private sealed class CountingFactory
            : IVertexGraphFactory<int, int, int, IDependentActivity<int, int, int>>
        {
            private readonly IVertexGraphFactory<int, int, int, IDependentActivity<int, int, int>> m_Inner;
            private int m_Builders;
            private int m_Compilers;

            public CountingFactory(IVertexGraphFactory<int, int, int, IDependentActivity<int, int, int>> inner) => m_Inner = inner;

            public int Builders => Volatile.Read(ref m_Builders);

            public int Compilers => Volatile.Read(ref m_Compilers);

            public VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> CreateBuilder()
            {
                Interlocked.Increment(ref m_Builders);
                return m_Inner.CreateBuilder();
            }

            public VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>> CreateCompiler()
            {
                Interlocked.Increment(ref m_Compilers);
                return m_Inner.CreateCompiler();
            }
        }

        // Makes the builders with one factory and the compilers with another, so that a test can give the
        // compiler engines that are not the ones of the builders.
        private sealed class SplitFactory
            : IVertexGraphFactory<int, int, int, IDependentActivity<int, int, int>>
        {
            private readonly IVertexGraphFactory<int, int, int, IDependentActivity<int, int, int>> m_Builders;
            private readonly IVertexGraphFactory<int, int, int, IDependentActivity<int, int, int>> m_Compilers;

            public SplitFactory(
                IVertexGraphFactory<int, int, int, IDependentActivity<int, int, int>> builders,
                IVertexGraphFactory<int, int, int, IDependentActivity<int, int, int>> compilers)
            {
                m_Builders = builders;
                m_Compilers = compilers;
            }

            public VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> CreateBuilder() => m_Builders.CreateBuilder();

            public VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>> CreateCompiler() => m_Compilers.CreateCompiler();
        }

        // A compiler of a class of the caller's own, which makes its own builder. The application has one.
        private sealed class OwnCompiler
            : VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>
        {
        }

        // Makes the builders with the standard factory, and a compiler of the class above, which it remembers.
        private sealed class OwnCompilerFactory
            : IVertexGraphFactory<int, int, int, IDependentActivity<int, int, int>>
        {
            private readonly VertexGraphFactory<int, int, int, IDependentActivity<int, int, int>> m_Inner = FactoryOf();

            public List<OwnCompiler> Compilers { get; } = [];

            public VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> CreateBuilder() => m_Inner.CreateBuilder();

            public VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>> CreateCompiler()
            {
                var compiler = new OwnCompiler();
                Compilers.Add(compiler);
                return compiler;
            }
        }

        // A factory that does not do its job: it gives nothing.
        private sealed class NullFactory
            : IVertexGraphFactory<int, int, int, IDependentActivity<int, int, int>>
        {
            public VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> CreateBuilder() => null;

            public VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>> CreateCompiler() => null;
        }

        // A factory that gives the same builder each time, which is a mistake: a builder holds the graph of one plan.
        private sealed class SharedBuilderFactory
            : IVertexGraphFactory<int, int, int, IDependentActivity<int, int, int>>
        {
            private readonly VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> m_Builder =
                FactoryOf().CreateBuilder();

            public VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> CreateBuilder() => m_Builder;

            public VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>> CreateCompiler() => new(m_Builder);
        }

        // A resource scheduler that hands everything to the standard one. A test changes what it needs.
        private class DelegatingScheduler
            : IResourceSchedulingEngine<int, int, int>
        {
            private readonly PriorityListResourceScheduler<int, int, int> m_Inner = new();

            public virtual IEnumerable<IResourceSchedule<int, int, int>> CalculateResourceSchedules(
                List<int> priorityList,
                List<IResource<int, int>> filteredResources,
                bool infiniteResources,
                IResourceSchedulingGraph<int, int, int> graph,
                CancellationToken cancellationToken) =>
                m_Inner.CalculateResourceSchedules(priorityList, filteredResources, infiniteResources, graph, cancellationToken);

            public IList<IUnavailableResources<int, int>> GatherUnavailableResources(
                List<IActivity<int, int, int>> activities,
                List<IResource<int, int>> filteredResources) =>
                m_Inner.GatherUnavailableResources(activities, filteredResources);

            public List<IResourceSchedule<int, int, int>> ReplaceWithSyntheticResources(
                List<IResourceSchedule<int, int, int>> resourceSchedules) =>
                m_Inner.ReplaceWithSyntheticResources(resourceSchedules);

            public IEnumerable<IResourceSchedule<int, int, int>> RebuildAlignedResourceSchedules(
                List<IResourceSchedule<int, int, int>> resourceSchedules,
                bool infiniteResources,
                IResourceSchedulingGraph<int, int, int> graph,
                List<IActivity<int, int, int>> finalActivities,
                int startTime,
                int finishTime) =>
                m_Inner.RebuildAlignedResourceSchedules(resourceSchedules, infiniteResources, graph, finalActivities, startTime, finishTime);

            public IEnumerable<IResourceSchedule<int, int, int>> CollectIndirectResourceSchedules(
                List<IResource<int, int>> filteredResources,
                List<IResourceSchedule<int, int, int>> scheduledResources,
                List<IActivity<int, int, int>> finalActivities,
                int startTime,
                int finishTime) =>
                m_Inner.CollectIndirectResourceSchedules(filteredResources, scheduledResources, finalActivities, startTime, finishTime);

            public HashSet<int> GetResourcePhasesUsed(
                List<IResourceSchedule<int, int, int>> totalSchedules,
                HashSet<int> workstreamsUsed) =>
                m_Inner.GetResourcePhasesUsed(totalSchedules, workstreamsUsed);
        }

        // Counts the plans that its schedulers are asked to schedule. The count is shared by the schedulers of
        // all the builders.
        private sealed class Counter
        {
            private int m_Schedules;

            public int Schedules => Volatile.Read(ref m_Schedules);

            public void CountSchedule() => Interlocked.Increment(ref m_Schedules);
        }

        private sealed class CountingScheduler
            : DelegatingScheduler
        {
            private readonly Counter m_Counter;

            public CountingScheduler(Counter counter) => m_Counter = counter;

            public override IEnumerable<IResourceSchedule<int, int, int>> CalculateResourceSchedules(
                List<int> priorityList,
                List<IResource<int, int>> filteredResources,
                bool infiniteResources,
                IResourceSchedulingGraph<int, int, int> graph,
                CancellationToken cancellationToken)
            {
                m_Counter.CountSchedule();
                return base.CalculateResourceSchedules(priorityList, filteredResources, infiniteResources, graph, cancellationToken);
            }
        }

        // Schedules the activities in the reverse of the order of the priority list.
        private sealed class ReversingScheduler
            : DelegatingScheduler
        {
            public override IEnumerable<IResourceSchedule<int, int, int>> CalculateResourceSchedules(
                List<int> priorityList,
                List<IResource<int, int>> filteredResources,
                bool infiniteResources,
                IResourceSchedulingGraph<int, int, int> graph,
                CancellationToken cancellationToken) =>
                base.CalculateResourceSchedules([.. priorityList.AsEnumerable().Reverse()], filteredResources, infiniteResources, graph, cancellationToken);
        }

        // An activity type of a caller's own, which keeps what it is given through a clone.
        private class SpecialActivity
            : DependentActivity<int, int, int>
        {
            public SpecialActivity(IDependentActivity<int, int, int> from)
                : base(
                      from.Id, from.Name, from.Notes, from.TargetWorkStreams, from.TargetResources,
                      from.Dependencies, from.PlanningDependencies, from.ResourceDependencies, from.Successors,
                      from.TargetResourceOperator, from.AllocatedToResources, from.CanBeRemoved, from.HasNoCost, from.HasNoBilling, from.HasNoEffort,
                      from.Duration, from.FreeSlack, from.EarliestStartTime, from.LatestFinishTime, from.MinimumFreeSlack, from.MinimumEarliestStartTime, from.MaximumLatestFinishTime)
            {
            }

            public override object CloneObject() => new SpecialActivity(this);
        }

        // An activity type that clones itself to its base type, which is a mistake.
        private sealed class WrongCloneActivity
            : SpecialActivity
        {
            public WrongCloneActivity(IDependentActivity<int, int, int> from)
                : base(from)
            {
            }

            public override object CloneObject() => new DependentActivity<int, int, int>(Id, Duration);
        }

        private static SimulationSettings SettingsOf(int iterations, int threads) =>
            new() { Iterations = iterations, Seed = 4242, MaxDegreeOfParallelism = threads };

        // The finish time of a compile of the plan by a compiler of a factory with the given scheduler. This is
        // what the check of a simulator gives when the compiler of its factory has that scheduler.
        private static int FinishOf(SpikePlan plan, IResourceSchedulingEngine<int, int, int> scheduler)
        {
            VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>> compiler = FactoryOf(scheduler).CreateCompiler();
            foreach (IDependentActivity<int, int, int> activity in ScheduleSimulatorTests.ActivitiesOf(plan))
            {
                compiler.AddActivity(activity);
            }

            compiler.Compile(plan.NewResources(true), plan.NewWorkStreams(), TestContext.Current.CancellationToken)
                .CompilationErrors.ShouldBeEmpty();
            return compiler.FinishTime;
        }

        #endregion

        #region The builders of the factory

        [Fact]
        public void Run_Given_AFactoryWithTheStandardEngines_Then_GivesTheResultOfTheSimulatorWithNoFactory()
        {
            SpikePlan plan = SpikePlan.LoadSample();
            ScheduleSimulator<int, int, int> standard = StandardSimulatorOf(plan);
            ScheduleSimulator<int, int, int, IDependentActivity<int, int, int>> withFactory = SimulatorOf(plan, FactoryOf());

            SimulationResult<int> expected = standard.Run(SettingsOf(300, 4), TestContext.Current.CancellationToken);
            SimulationResult<int> actual = withFactory.Run(SettingsOf(300, 4), TestContext.Current.CancellationToken);

            ScheduleSimulatorTests.ShouldBeTheSame(actual, expected);
            withFactory.PlannedFinishTime.ShouldBe(standard.PlannedFinishTime);
            withFactory.CompilationErrors.ShouldBeEmpty();
        }

        [Fact]
        public void Run_Given_ASchedulerThatCountsItsCalls_Then_TheSimulatorUsesTheEnginesOfTheFactoryForTheCheckAndForEveryIteration()
        {
            var counter = new Counter();
            var factory = new CountingFactory(FactoryOf(new CountingScheduler(counter)));
            ScheduleSimulator<int, int, int, IDependentActivity<int, int, int>> simulator = SimulatorOf(SpikePlan.LoadSample(), factory);

            // The first check of the plan is a compile, and a compile schedules the plan once.
            counter.Schedules.ShouldBeGreaterThan(0);
            int checkSchedules = counter.Schedules;

            // One thread asks for one builder, and each iteration schedules the plan once.
            SimulationResult<int> result = simulator.Run(SettingsOf(100, 1), TestContext.Current.CancellationToken);

            result.FailedIterationCount.ShouldBe(0);
            factory.Builders.ShouldBe(1);
            (counter.Schedules - checkSchedules).ShouldBe(100);
        }

        [Fact]
        public void Run_Given_ManyThreads_Then_EachThreadHasABuilderOfItsOwn()
        {
            var factory = new CountingFactory(FactoryOf());
            ScheduleSimulator<int, int, int, IDependentActivity<int, int, int>> simulator = SimulatorOf(SpikePlan.LoadSample(), factory);

            simulator.Run(SettingsOf(640, 4), TestContext.Current.CancellationToken);

            // One for each task that the run starts. A task takes chunks of 64 iterations, and so there are at
            // most 10 of them. Each has a builder of its own.
            factory.Builders.ShouldBeInRange(1, 10);
        }

        [Fact]
        public void Run_Given_ASchedulerThatSchedulesInAnotherOrder_Then_TheResultIsNotTheStandardOne()
        {
            // Few resources for many activities, so that the order in which they are scheduled decides the finish.
            SpikePlan plan = SpikePlan.Layered(100, 10, 3);
            SimulationResult<int> standard = StandardSimulatorOf(plan).Run(SettingsOf(120, 2), TestContext.Current.CancellationToken);

            ScheduleSimulator<int, int, int, IDependentActivity<int, int, int>> reversing = SimulatorOf(plan, FactoryOf(new ReversingScheduler()));
            reversing.CompilationErrors.ShouldBeEmpty();
            SimulationResult<int> actual = reversing.Run(SettingsOf(120, 2), TestContext.Current.CancellationToken);

            actual.FinishTimes.ShouldNotBe(standard.FinishTimes);
        }

        #endregion

        #region The compiler of the factory

        [Fact]
        public void Ctor_Given_AFactory_Then_AsksForOneCompilerAndForNoBuilder()
        {
            var factory = new CountingFactory(FactoryOf());

            ScheduleSimulator<int, int, int, IDependentActivity<int, int, int>> simulator = SimulatorOf(SpikePlan.LoadSample(), factory);

            // The compiler comes with a builder of its own, and the iterations have not started.
            factory.Compilers.ShouldBe(1);
            factory.Builders.ShouldBe(0);
            simulator.CompilationErrors.ShouldBeEmpty();
        }

        [Fact]
        public void Run_Given_ManyThreads_Then_TheFactoryIsNotAskedForAnotherCompiler()
        {
            var factory = new CountingFactory(FactoryOf());
            ScheduleSimulator<int, int, int, IDependentActivity<int, int, int>> simulator = SimulatorOf(SpikePlan.LoadSample(), factory);

            simulator.Run(SettingsOf(640, 4), TestContext.Current.CancellationToken);

            // Only the check of the plan is a compile. An iteration schedules with the builder of its thread.
            factory.Compilers.ShouldBe(1);
            factory.Builders.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void Ctor_Given_ACompilerWithOtherEngines_Then_TheCheckOfThePlanIsACompileByThatCompiler()
        {
            var ofTheBuilders = new Counter();
            var ofTheCompiler = new Counter();

            ScheduleSimulator<int, int, int, IDependentActivity<int, int, int>> simulator = SimulatorOf(
                SpikePlan.LoadSample(),
                new SplitFactory(
                    FactoryOf(new CountingScheduler(ofTheBuilders)),
                    FactoryOf(new CountingScheduler(ofTheCompiler))));

            // The compile went through the compiler. The builders of the factory have scheduled nothing yet.
            simulator.CompilationErrors.ShouldBeEmpty();
            ofTheCompiler.Schedules.ShouldBeGreaterThan(0);
            ofTheBuilders.Schedules.ShouldBe(0);
        }

        [Fact]
        public void Ctor_Given_ACompilerOfAnotherScheduler_Then_ThePlannedFinishIsTheFinishOfThatCompiler()
        {
            // Few resources for many activities, so that the order in which they are scheduled decides the finish.
            SpikePlan plan = SpikePlan.Layered(100, 10, 3);
            int standard = StandardSimulatorOf(plan).PlannedFinishTime;
            int reversed = FinishOf(plan, new ReversingScheduler());
            reversed.ShouldNotBe(standard);

            // The builders of the factory have the standard engines, and the compiler reverses.
            ScheduleSimulator<int, int, int, IDependentActivity<int, int, int>> simulator = SimulatorOf(
                plan,
                new SplitFactory(FactoryOf(), FactoryOf(new ReversingScheduler())));

            simulator.PlannedFinishTime.ShouldBe(reversed);
        }

        [Fact]
        public void Ctor_Given_ACompilerOfAClassOfItsOwn_Then_TheCheckOfThePlanIsACompileByThatCompiler()
        {
            var factory = new OwnCompilerFactory();

            ScheduleSimulator<int, int, int, IDependentActivity<int, int, int>> simulator = SimulatorOf(SpikePlan.LoadSample(), factory);

            // The simulator added the plan to the compiler that the factory gave, and compiled it there.
            simulator.CompilationErrors.ShouldBeEmpty();
            OwnCompiler compiler = factory.Compilers.ShouldHaveSingleItem();
            compiler.FinishTime.ShouldBeGreaterThan(0);
            compiler.FinishTime.ShouldBe(simulator.PlannedFinishTime);
        }

        #endregion

        #region The type of the activity

        [Fact]
        public void Run_Given_AnActivityTypeOfItsOwn_Then_GivesTheResultOfTheStandardSimulator()
        {
            SpikePlan plan = SpikePlan.LoadSample();
            var simulator = new ScheduleSimulator<int, int, int, SpecialActivity>(
                ScheduleSimulatorTests.ActivitiesOf(plan).Select(x => new SpecialActivity(x)),
                plan.NewResources(true),
                plan.NewWorkStreams(),
                ScheduleSimulatorTests.EstimatesOf(plan),
                new VertexGraphFactory<int, int, int, SpecialActivity>());

            SimulationResult<int> actual = simulator.Run(SettingsOf(200, 2), TestContext.Current.CancellationToken);
            SimulationResult<int> expected = StandardSimulatorOf(plan).Run(SettingsOf(200, 2), TestContext.Current.CancellationToken);

            ScheduleSimulatorTests.ShouldBeTheSame(actual, expected);
        }

        [Fact]
        public void Ctor_Given_AnActivityThatClonesItselfToAnotherType_Then_Throws()
        {
            SpikePlan plan = SpikePlan.LoadSample();

            var thrown = Should.Throw<ArgumentException>(() => new ScheduleSimulator<int, int, int, WrongCloneActivity>(
                ScheduleSimulatorTests.ActivitiesOf(plan).Select(x => new WrongCloneActivity(x)),
                plan.NewResources(true),
                plan.NewWorkStreams(),
                ScheduleSimulatorTests.EstimatesOf(plan),
                new VertexGraphFactory<int, int, int, WrongCloneActivity>()));

            thrown.Message.ShouldContain(nameof(WrongCloneActivity));
        }

        #endregion

        #region A factory that does not do its job

        [Fact]
        public void Ctor_Given_NoFactory_Then_Throws()
        {
            SpikePlan plan = SpikePlan.LoadSample();

            var thrown = Should.Throw<ArgumentNullException>(() => SimulatorOf(plan, null));

            thrown.ParamName.ShouldBe("factory");
        }

        [Fact]
        public void Ctor_Given_AFactoryThatGivesANullCompiler_Then_Throws()
        {
            SpikePlan plan = SpikePlan.LoadSample();

            var thrown = Should.Throw<InvalidOperationException>(() => SimulatorOf(plan, new NullFactory()));

            // The message names the compiler, so that it cannot be taken for the message about the builder.
            thrown.Message.ShouldContain("compiler", Case.Insensitive);
        }

        [Fact]
        public void Run_Given_AFactoryThatGivesANullBuilder_Then_Throws()
        {
            SpikePlan plan = SpikePlan.LoadSample();
            ScheduleSimulator<int, int, int, IDependentActivity<int, int, int>> simulator = SimulatorOf(
                plan,
                new SplitFactory(new NullFactory(), FactoryOf()));

            // The check of the plan passes: only an iteration asks for a builder.
            simulator.CompilationErrors.ShouldBeEmpty();
            var thrown = Should.Throw<AggregateException>(() => simulator.Run(SettingsOf(64, 1), TestContext.Current.CancellationToken));

            InvalidOperationException inner = thrown.InnerExceptions.OfType<InvalidOperationException>().ShouldHaveSingleItem();
            inner.Message.ShouldContain("builder", Case.Insensitive);
        }

        [Fact]
        public void Run_Given_AFactoryThatGivesTheSameBuilderEachTime_Then_Throws()
        {
            SpikePlan plan = SpikePlan.LoadSample();
            ScheduleSimulator<int, int, int, IDependentActivity<int, int, int>> simulator = SimulatorOf(plan, new SharedBuilderFactory());

            // The check of the plan has filled the builder, and an iteration needs a new one.
            var thrown = Should.Throw<AggregateException>(() => simulator.Run(SettingsOf(64, 1), TestContext.Current.CancellationToken));

            thrown.InnerExceptions.ShouldContain(x => x is InvalidOperationException);
        }

        #endregion
    }
}
