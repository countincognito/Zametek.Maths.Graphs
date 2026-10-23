using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    // The ID of an activity can be a Guid. A Guid has no previous value, so that the standard engines give new GUIDs
    // for the events, the dummy activities and the nodes that a builder makes. These tests take a plan with Guid IDs
    // through the builders, the compilers and the simulation on the standard engines, and through a clone of a builder,
    // which asks the ID generator factory of its original for generators of its own.
    public class GuidKeyTests
    {
        #region Helpers

        private static readonly Guid s_A = new("00000000-0000-0000-0000-00000000000a");
        private static readonly Guid s_B = new("00000000-0000-0000-0000-00000000000b");
        private static readonly Guid s_C = new("00000000-0000-0000-0000-00000000000c");
        private static readonly Guid s_D = new("00000000-0000-0000-0000-00000000000d");

        private static Resource<int, int> OneResource() =>
            new(1, @"R1", false, false, InterActivityAllocationType.None, 0.0, 0.0, 0, []);

        // C needs A and B, and B needs A: so that the vertex network has an event for each of three dependencies.
        // With one resource the plan finishes at 10.
        private const int VertexPlanFinish = 10;

        private static DependentActivity<Guid, int, int> Activity(Guid id, int duration, params Guid[] dependencies) =>
            new(id, duration, dependencies);

        private static void AddVertexPlan(VertexGraphBuilder<Guid, int, int, IDependentActivity<Guid, int, int>> builder)
        {
            builder.AddActivity(Activity(s_A, 3), []).ShouldBeTrue();
            builder.AddActivity(Activity(s_B, 5), [s_A]).ShouldBeTrue();
            builder.AddActivity(Activity(s_C, 2), [s_A, s_B]).ShouldBeTrue();
        }

        // C needs A and B, and D needs only B, which needs a dummy activity.
        private static void AddArrowPlan(ArrowGraphBuilder<Guid, int, int, IDependentActivity<Guid, int, int>> builder)
        {
            builder.AddActivity(Activity(s_A, 3), []).ShouldBeTrue();
            builder.AddActivity(Activity(s_B, 5), []).ShouldBeTrue();
            builder.AddActivity(Activity(s_C, 2), [s_A, s_B]).ShouldBeTrue();
            builder.AddActivity(Activity(s_D, 4), [s_B]).ShouldBeTrue();
        }

        #endregion

        #region Builders

        [Fact]
        public void VertexGraphBuilder_GivenGuidKeys_ThenTheEventsHaveNewGuids()
        {
            var builder = new VertexGraphBuilder<Guid, int, int, IDependentActivity<Guid, int, int>>(new VertexGraphBuilderEngines<Guid, int, int, IDependentActivity<Guid, int, int>>());

            AddVertexPlan(builder);

            builder.EventIds.Count().ShouldBe(3);
            builder.EventIds.Distinct().Count().ShouldBe(3);
            builder.EventIds.Intersect(builder.ActivityIds).ShouldBeEmpty();
        }

        [Fact]
        public void VertexGraphBuilder_GivenGuidKeys_ThenACloneIsMadeAndGivesNewGuids()
        {
            var original = new VertexGraphBuilder<Guid, int, int, IDependentActivity<Guid, int, int>>(new VertexGraphBuilderEngines<Guid, int, int, IDependentActivity<Guid, int, int>>());
            AddVertexPlan(original);

            var clone = (VertexGraphBuilder<Guid, int, int, IDependentActivity<Guid, int, int>>)original.CloneObject();
            clone.AddActivity(Activity(s_D, 1), [s_C]).ShouldBeTrue();

            clone.ActivityIds.Count().ShouldBe(4);
            clone.EventIds.Count().ShouldBe(4);
            clone.EventIds.Distinct().Count().ShouldBe(4);
            original.ActivityIds.Count().ShouldBe(3);
        }

        [Fact]
        public void VertexGraphBuilder_GivenGuidKeys_ThenThePriorityListAndTheSchedulesAreCalculated()
        {
            var builder = new VertexGraphBuilder<Guid, int, int, IDependentActivity<Guid, int, int>>(new VertexGraphBuilderEngines<Guid, int, int, IDependentActivity<Guid, int, int>>());
            AddVertexPlan(builder);

            List<Guid> priorityList = builder.CalculateCriticalPathPriorityList(TestContext.Current.CancellationToken);
            List<IResourceSchedule<Guid, int, int>> schedules = builder.CalculateResourceSchedulesByPriorityList([OneResource()], TestContext.Current.CancellationToken);

            priorityList.Order().ShouldBe(new[] { s_A, s_B, s_C }.Order());
            schedules.ShouldNotBeEmpty();
        }

        [Fact]
        public void ArrowGraphBuilder_GivenGuidKeys_ThenTheDummyActivitiesAndTheNodesHaveNewGuids()
        {
            var builder = new ArrowGraphBuilder<Guid, int, int, IDependentActivity<Guid, int, int>>(new ArrowGraphBuilderEngines<Guid, int, int, IDependentActivity<Guid, int, int>>());

            AddArrowPlan(builder);

            builder.NodeIds.Distinct().Count().ShouldBe(builder.NodeIds.Count());
            builder.EdgeIds.Distinct().Count().ShouldBe(builder.EdgeIds.Count());
            List<Guid> dummies = builder.Activities.Where(x => x.IsDummy).Select(x => x.Id).ToList();
            dummies.ShouldNotBeEmpty();
            dummies.Intersect(new[] { s_A, s_B, s_C, s_D }).ShouldBeEmpty();
        }

        [Fact]
        public void ArrowGraphBuilder_GivenGuidKeys_ThenACloneIsMadeAndGivesNewGuids()
        {
            var original = new ArrowGraphBuilder<Guid, int, int, IDependentActivity<Guid, int, int>>(new ArrowGraphBuilderEngines<Guid, int, int, IDependentActivity<Guid, int, int>>());
            AddArrowPlan(original);

            var clone = (ArrowGraphBuilder<Guid, int, int, IDependentActivity<Guid, int, int>>)original.CloneObject();
            clone.AddActivity(Activity(Guid.NewGuid(), 1), [s_C, s_D]).ShouldBeTrue();

            clone.NodeIds.Distinct().Count().ShouldBe(clone.NodeIds.Count());
            clone.EdgeIds.Distinct().Count().ShouldBe(clone.EdgeIds.Count());
            clone.NodeIds.Count().ShouldBeGreaterThan(original.NodeIds.Count());
        }

        #endregion

        #region Compilers and the simulation

        [Fact]
        public void VertexGraphCompiler_GivenGuidKeys_ThenCompilesAPlan()
        {
            var compiler = new VertexGraphCompiler<Guid, int, int, IDependentActivity<Guid, int, int>>();
            compiler.AddActivity(Activity(s_A, 3)).ShouldBeTrue();
            compiler.AddActivity(Activity(s_B, 5, s_A)).ShouldBeTrue();
            compiler.AddActivity(Activity(s_C, 2, s_A, s_B)).ShouldBeTrue();

            IGraphCompilation<Guid, int, int, IDependentActivity<Guid, int, int>> output =
                compiler.Compile([OneResource()], TestContext.Current.CancellationToken);

            output.CompilationErrors.ShouldBeEmpty();
            compiler.FinishTime.ShouldBe(VertexPlanFinish);
        }

        [Fact]
        public void ArrowGraphCompiler_GivenGuidKeys_ThenCompilesAPlan()
        {
            var compiler = new ArrowGraphCompiler<Guid, int, int, IDependentActivity<Guid, int, int>>();
            compiler.AddActivity(Activity(s_A, 3)).ShouldBeTrue();
            compiler.AddActivity(Activity(s_B, 5)).ShouldBeTrue();
            compiler.AddActivity(Activity(s_C, 2, s_A, s_B)).ShouldBeTrue();
            compiler.AddActivity(Activity(s_D, 4, s_B)).ShouldBeTrue();

            compiler.Compile(TestContext.Current.CancellationToken);

            compiler.FinishTime.ShouldBe(9);
        }

        [Fact]
        public void ScheduleSimulator_GivenGuidKeys_ThenRunsTheIterations()
        {
            Guid[] ids = [s_A, s_B, s_C];
            var simulator = new ScheduleSimulator<Guid, int, int>(
                [Activity(s_A, 3), Activity(s_B, 5, s_A), Activity(s_C, 2, s_A, s_B)],
                [OneResource()],
                [],
                new Dictionary<Guid, DurationEstimate>
                {
                    [s_A] = new DurationEstimate(2, 3, 4),
                    [s_B] = new DurationEstimate(4, 5, 8),
                    [s_C] = new DurationEstimate(1, 2, 3),
                });

            SimulationResult<Guid> result = simulator.Run(
                new SimulationSettings { Iterations = 100, Seed = 7, MaxDegreeOfParallelism = 2 },
                TestContext.Current.CancellationToken);

            result.IterationCount.ShouldBe(100);
            result.FailedIterationCount.ShouldBe(0);
            result.ActivityIds.Order().ShouldBe(ids.Order());
            // With one resource the three activities follow each other: from 2 + 4 + 1 to 4 + 8 + 3.
            result.FinishTimes.ShouldAllBe(x => x >= 7 && x <= 15);
        }

        #endregion
    }
}
