using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    // A simulation schedules a plan with the builder that its caller gives it, and so with the engines
    // of that builder. These tests show that the simulator uses the builder (and the engines) of the
    // factory, in the first check of the plan and in every iteration, that a builder with the standard
    // engines gives the result of the simulator with no factory, that an engine that schedules in
    // another way changes the result, and that a type of activity of the caller's own is kept.
    public class ScheduleSimulatorBuilderTests
    {
        #region Helpers

        private static VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> BuilderOf(
            IResourceSchedulingEngine<int, int, int> scheduler = null) =>
            new(
                new PreviousIdGenerator<int>(),
                new RemovableEventGenerator<int>(),
                new VertexTarjanStronglyConnectedComponentsFinder<int, int, int, IDependentActivity<int, int, int>>(),
                new VertexCriticalPathEngine<int, int, int, IDependentActivity<int, int, int>>(),
                scheduler);

        private static ScheduleSimulator<int, int, int, IDependentActivity<int, int, int>> SimulatorOf(
            SpikePlan plan,
            Func<VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>> builderFactory) =>
            new(
                ScheduleSimulatorTests.ActivitiesOf(plan),
                plan.NewResources(true),
                plan.NewWorkStreams(),
                ScheduleSimulatorTests.EstimatesOf(plan),
                builderFactory);

        private static ScheduleSimulator<int, int, int> StandardSimulatorOf(SpikePlan plan) =>
            new(
                ScheduleSimulatorTests.ActivitiesOf(plan),
                plan.NewResources(true),
                plan.NewWorkStreams(),
                ScheduleSimulatorTests.EstimatesOf(plan));

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

        // Counts the plans that it is asked to schedule. The count is shared by the schedulers of all the builders.
        private sealed class Counter
        {
            private int m_Schedules;
            private int m_Builders;

            public int Schedules => Volatile.Read(ref m_Schedules);

            public int Builders => Volatile.Read(ref m_Builders);

            public void CountSchedule() => Interlocked.Increment(ref m_Schedules);

            public void CountBuilder() => Interlocked.Increment(ref m_Builders);
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

        #endregion

        #region The engines of the factory

        [Fact]
        public void Run_Given_ABuilderWithTheStandardEngines_Then_GivesTheResultOfTheSimulatorWithNoFactory()
        {
            SpikePlan plan = SpikePlan.LoadSample();
            ScheduleSimulator<int, int, int> standard = StandardSimulatorOf(plan);
            ScheduleSimulator<int, int, int, IDependentActivity<int, int, int>> withFactory = SimulatorOf(plan, () => BuilderOf());

            SimulationResult<int> expected = standard.Run(SettingsOf(300, 4), TestContext.Current.CancellationToken);
            SimulationResult<int> actual = withFactory.Run(SettingsOf(300, 4), TestContext.Current.CancellationToken);

            ScheduleSimulatorTests.ShouldBeTheSame(actual, expected);
            withFactory.PlannedFinishTime.ShouldBe(standard.PlannedFinishTime);
            withFactory.CompilationErrors.ShouldBeEmpty();
        }

        [Fact]
        public void Run_Given_ASchedulerThatCountsItsCalls_Then_TheSimulatorUsesTheBuilderOfTheFactoryForTheCheckAndForEveryIteration()
        {
            var counter = new Counter();
            ScheduleSimulator<int, int, int, IDependentActivity<int, int, int>> simulator = SimulatorOf(
                SpikePlan.LoadSample(),
                () =>
                {
                    counter.CountBuilder();
                    return BuilderOf(new CountingScheduler(counter));
                });

            // The first check of the plan is a compile, and a compile schedules the plan once.
            counter.Builders.ShouldBe(1);
            counter.Schedules.ShouldBeGreaterThan(0);
            int checkSchedules = counter.Schedules;

            // One thread asks for one builder, and each iteration schedules the plan once.
            SimulationResult<int> result = simulator.Run(SettingsOf(100, 1), TestContext.Current.CancellationToken);

            result.FailedIterationCount.ShouldBe(0);
            counter.Builders.ShouldBe(2);
            (counter.Schedules - checkSchedules).ShouldBe(100);
        }

        [Fact]
        public void Run_Given_ManyThreads_Then_EachThreadHasABuilderOfItsOwn()
        {
            var counter = new Counter();
            ScheduleSimulator<int, int, int, IDependentActivity<int, int, int>> simulator = SimulatorOf(
                SpikePlan.LoadSample(),
                () =>
                {
                    counter.CountBuilder();
                    return BuilderOf();
                });

            simulator.Run(SettingsOf(640, 4), TestContext.Current.CancellationToken);

            // One for the check, and then one for each task that the run starts. A task takes chunks of 64
            // iterations, and so there are at most 10 of them. Each has a builder of its own.
            counter.Builders.ShouldBeInRange(2, 11);
        }

        [Fact]
        public void Run_Given_ASchedulerThatSchedulesInAnotherOrder_Then_TheResultIsNotTheStandardOne()
        {
            // Few resources for many activities, so that the order in which they are scheduled decides the finish.
            SpikePlan plan = SpikePlan.Layered(100, 10, 3);
            SimulationResult<int> standard = StandardSimulatorOf(plan).Run(SettingsOf(120, 2), TestContext.Current.CancellationToken);

            ScheduleSimulator<int, int, int, IDependentActivity<int, int, int>> reversing = SimulatorOf(plan, () => BuilderOf(new ReversingScheduler()));
            reversing.CompilationErrors.ShouldBeEmpty();
            SimulationResult<int> actual = reversing.Run(SettingsOf(120, 2), TestContext.Current.CancellationToken);

            actual.FinishTimes.ShouldNotBe(standard.FinishTimes);
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
                () => new VertexGraphBuilder<int, int, int, SpecialActivity>(new PreviousIdGenerator<int>()));

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
                () => new VertexGraphBuilder<int, int, int, WrongCloneActivity>(new PreviousIdGenerator<int>())));

            thrown.Message.ShouldContain(nameof(WrongCloneActivity));
        }

        #endregion

        #region A factory that does not do its job

        [Fact]
        public void Ctor_Given_NoFactory_Then_Throws()
        {
            SpikePlan plan = SpikePlan.LoadSample();

            Should.Throw<ArgumentNullException>(() => SimulatorOf(plan, null!));
        }

        [Fact]
        public void Ctor_Given_AFactoryThatGivesNull_Then_Throws()
        {
            SpikePlan plan = SpikePlan.LoadSample();

            Should.Throw<InvalidOperationException>(() => SimulatorOf(plan, () => null!));
        }

        [Fact]
        public void Run_Given_AFactoryThatGivesTheSameBuilderEachTime_Then_Throws()
        {
            SpikePlan plan = SpikePlan.LoadSample();
            VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> shared = BuilderOf();
            ScheduleSimulator<int, int, int, IDependentActivity<int, int, int>> simulator = SimulatorOf(plan, () => shared);

            // The check of the plan has filled the builder, and an iteration needs a new one.
            var thrown = Should.Throw<AggregateException>(() => simulator.Run(SettingsOf(64, 1), TestContext.Current.CancellationToken));

            thrown.InnerExceptions.ShouldContain(x => x is InvalidOperationException);
        }

        #endregion
    }
}
