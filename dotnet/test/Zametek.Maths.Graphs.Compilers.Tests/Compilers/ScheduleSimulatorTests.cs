using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    // The simulation of a schedule: that an iteration gives what a compile gives, that a result
    // does not depend on the number of threads, and that the paths of a result are the paths of the
    // schedule.
    //
    // The plans come from the spike of phase 0 (the sample plan of the application with its 15
    // resources, a layered plan, and the sample of the reference tool), and a few small plans that
    // are built here, each for one behaviour.
    public class ScheduleSimulatorTests
    {
        #region Helpers

        private static List<IDependentActivity<int, int, int>> ActivitiesOf(SpikePlan plan) =>
            plan.Activities
                .Select(x => (IDependentActivity<int, int, int>)plan.NewActivity(x, EstimateOf(x).ExpectedDuration))
                .ToList();

        private static DurationEstimate EstimateOf(SpikePlan.ActivityRecord record) =>
            new DurationEstimate(record.Optimistic, record.Duration, record.Pessimistic);

        private static Dictionary<int, DurationEstimate> EstimatesOf(SpikePlan plan) =>
            plan.Activities.ToDictionary(x => x.Id, EstimateOf);

        private static ScheduleSimulator<int, int, int> SimulatorOf(SpikePlan plan, bool useResources) =>
            new ScheduleSimulator<int, int, int>(
                ActivitiesOf(plan),
                plan.NewResources(useResources),
                plan.NewWorkStreams(),
                EstimatesOf(plan));

        // One activity of a small plan: its ID, its duration (O = ML = P) and its dependencies.
        private static SpikePlan.ActivityRecord Record(int id, int duration, params int[] dependencies) =>
            new SpikePlan.ActivityRecord
            {
                Id = id,
                Name = id.ToString(),
                Duration = duration,
                Optimistic = duration,
                Pessimistic = duration,
                Dependencies = dependencies.ToList(),
            };

        private static SpikePlan SmallPlan(params SpikePlan.ActivityRecord[] activities) =>
            new SpikePlan { Activities = activities.ToList() };

        // Every run in these tests passes the token of the test, so that a test can be stopped.
        private static SimulationResult<int> Run(ScheduleSimulator<int, int, int> simulator, SimulationSettings settings) =>
            simulator.Run(settings, TestContext.Current.CancellationToken);

        private static string Describe(SimulationResult<int> result, int iteration) =>
            string.Join(
                @" | ",
                result.CriticalPathIndexes(iteration).ToArray().Select(x => string.Join(
                    @">",
                    result.CriticalPaths[x].Steps.Select(s => s.Link == CriticalPathLink.Resource ? $@"~{s.ActivityId}" : s.ActivityId.ToString()))));

        private static void ShouldBeTheSame(SimulationResult<int> actual, SimulationResult<int> expected)
        {
            actual.FinishTimes.ShouldBe(expected.FinishTimes);
            actual.CriticalPaths.Count.ShouldBe(expected.CriticalPaths.Count);
            for (int i = 0; i < expected.CriticalPaths.Count; i++)
            {
                actual.CriticalPaths[i].ShouldBe(expected.CriticalPaths[i]);
            }
            for (int iteration = 0; iteration < expected.IterationCount; iteration++)
            {
                actual.CriticalPathIndexes(iteration).ToArray().ShouldBe(expected.CriticalPathIndexes(iteration).ToArray());
                actual.IsCriticalPathListComplete(iteration).ShouldBe(expected.IsCriticalPathListComplete(iteration));
                for (int a = 0; a < expected.ActivityIds.Count; a++)
                {
                    actual.IsCritical(iteration, a).ShouldBe(expected.IsCritical(iteration, a));
                }
            }
        }

        #endregion

        #region The iteration against the compile

        // The compile is the oracle. One runner takes the iterations in turn, as it does in a
        // simulation, so that any state that an iteration leaves behind shows in the next one.
        private static void ShouldAgreeWithTheCompile(SpikePlan plan, bool useResources, bool scheduleOntoSyntheticResources, int iterations, DurationDistribution distribution)
        {
            var runner = new SimulationIterationRunner<int, int, int>(
                plan.Activities.Select(x => (IDependentActivity<int, int, int>)plan.NewActivity(x, x.Duration)).ToList(),
                plan.NewResources(useResources),
                scheduleOntoSyntheticResources);
            var tracer = new CriticalPathTracer<int, int, int>(
                runner.Activities,
                plan.Activities.Select((x, i) => (x.Id, i)).ToDictionary(x => x.Id, x => x.i));
            var random = new SimulationRandom(2024UL);
            var durations = new int[plan.Activities.Count];
            bool shortcut = !useResources && !scheduleOntoSyntheticResources;

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                for (int i = 0; i < durations.Length; i++)
                {
                    DurationEstimate estimate = EstimateOf(plan.Activities[i]);
                    durations[i] = estimate.HasSpread
                        ? DurationSampler.RoundToWholeUnits(DurationSampler.Sample(distribution, random, estimate, 4.0), estimate)
                        : estimate.MostLikely;
                }

                int finish = runner.Run(durations, CancellationToken.None);
                (int expectedFinish, bool[] expectedCritical, int errors) = plan.CompileOracle(durations, useResources);

                errors.ShouldBe(0, $@"iteration {iteration}");
                finish.ShouldBe(expectedFinish, $@"iteration {iteration}");
                for (int i = 0; i < durations.Length; i++)
                {
                    if (shortcut)
                    {
                        // With no resources the compile may mark more activities critical (see
                        // the notes of the setting), and never fewer.
                        if (runner.Activities[i].IsCritical)
                        {
                            expectedCritical[i].ShouldBeTrue($@"iteration {iteration}, activity {plan.Activities[i].Id}");
                        }
                    }
                    else
                    {
                        runner.Activities[i].IsCritical.ShouldBe(expectedCritical[i], $@"iteration {iteration}, activity {plan.Activities[i].Id}");
                    }
                }

                // And the paths that the tracer finds are chains of the schedule that it has been given.
                var paths = new List<int[]>();
                tracer.Trace(finish, durations, 1000, paths);
                paths.ShouldNotBeEmpty($@"iteration {iteration}");
                foreach (int[] path in paths)
                {
                    path.Sum(x => durations[x >> 2]).ShouldBe(finish, $@"iteration {iteration}");
                    path.ShouldAllBe(x => runner.Activities[x >> 2].IsCritical);
                    (path[0] & 3).ShouldBe((int)CriticalPathLink.None);
                }
            }
        }

        [Theory]
        [InlineData(DurationDistribution.BetaPert)]
        [InlineData(DurationDistribution.Uniform)]
        public void Iteration_Given_ThePlanOfTheApplicationWithItsResources_Then_GivesTheFinishAndTheCriticalActivitiesOfACompile(DurationDistribution distribution)
        {
            ShouldAgreeWithTheCompile(SpikePlan.LoadSample(), useResources: true, scheduleOntoSyntheticResources: false, iterations: 150, distribution);
        }

        [Fact]
        public void Iteration_Given_ThePlanOfTheApplicationWithNoResources_Then_GivesTheFinishAndTheCriticalActivitiesOfACompile()
        {
            ShouldAgreeWithTheCompile(SpikePlan.LoadSample(), useResources: false, scheduleOntoSyntheticResources: true, iterations: 100, DurationDistribution.BetaPert);
        }

        [Fact]
        public void Iteration_Given_ALayeredPlanWithResources_Then_GivesTheFinishAndTheCriticalActivitiesOfACompile()
        {
            ShouldAgreeWithTheCompile(SpikePlan.Layered(100, 10, 10), useResources: true, scheduleOntoSyntheticResources: false, iterations: 60, DurationDistribution.BetaPert);
        }

        [Fact]
        public void Iteration_Given_TheReferenceSampleWithNoResourcesAndTheShortcut_Then_GivesTheFinishOfACompileAndCriticalActivitiesThatItHas()
        {
            ShouldAgreeWithTheCompile(SpikePlan.ReferenceSample(), useResources: false, scheduleOntoSyntheticResources: false, iterations: 200, DurationDistribution.BetaPert);
        }

        [Fact]
        public void Iteration_Given_TheReferenceSampleWithNoResourcesAndTheCompileWay_Then_GivesTheCriticalActivitiesOfACompile()
        {
            ShouldAgreeWithTheCompile(SpikePlan.ReferenceSample(), useResources: false, scheduleOntoSyntheticResources: true, iterations: 200, DurationDistribution.BetaPert);
        }

        [Fact]
        public void Iteration_Given_ADrawOfZeroForAnActivityThatTakesTime_Then_TheActivityIsAMilestoneForThatIteration()
        {
            // A has the estimates 0, 3 and 6, and a resource of its own; one resource does the work.
            // A draw of 0 gives an activity as long as a milestone, which takes no resource. The
            // iteration must still agree with the compile when it does.
            var plan = SmallPlan(
                new SpikePlan.ActivityRecord { Id = 1, Name = "1", Duration = 3, Optimistic = 0, Pessimistic = 6 },
                new SpikePlan.ActivityRecord { Id = 2, Name = "2", Duration = 4, Optimistic = 4, Pessimistic = 4, Dependencies = new List<int> { 1 } },
                new SpikePlan.ActivityRecord { Id = 3, Name = "3", Duration = 5, Optimistic = 5, Pessimistic = 5 });
            plan.Resources.Add(new SpikePlan.ResourceRecord { Id = 1 });

            ShouldAgreeWithTheCompile(plan, useResources: true, scheduleOntoSyntheticResources: false, iterations: 300, DurationDistribution.Uniform);
        }

        #endregion

        #region A simulation

        [Fact]
        public void Run_Given_TheSameSettings_Then_GivesTheSameResultForOneThreadAndForMany()
        {
            ScheduleSimulator<int, int, int> simulator = SimulatorOf(SpikePlan.LoadSample(), useResources: true);
            var settings = new SimulationSettings { Iterations = 300, Seed = 77, MaxDegreeOfParallelism = 1 };
            SimulationResult<int> one = Run(simulator, settings);

            settings.MaxDegreeOfParallelism = 4;
            SimulationResult<int> four = Run(simulator, settings);

            ShouldBeTheSame(four, one);
            one.IterationCount.ShouldBe(300);
            one.FailedIterationCount.ShouldBe(0);
        }

        [Fact]
        public void Run_Given_TheSameSettingsTwice_Then_GivesTheSameResult()
        {
            ScheduleSimulator<int, int, int> simulator = SimulatorOf(SpikePlan.ReferenceSample(), useResources: false);
            var settings = new SimulationSettings { Iterations = 500, Seed = 5 };

            ShouldBeTheSame(Run(simulator, settings), Run(simulator, settings));
        }

        [Fact]
        public void Run_Given_ADifferentSeed_Then_GivesADifferentResult()
        {
            ScheduleSimulator<int, int, int> simulator = SimulatorOf(SpikePlan.ReferenceSample(), useResources: false);

            SimulationResult<int> first = Run(simulator, new SimulationSettings { Iterations = 500, Seed = 1 });
            SimulationResult<int> second = Run(simulator, new SimulationSettings { Iterations = 500, Seed = 2 });

            first.FinishTimes.ShouldNotBe(second.FinishTimes);
        }

        [Fact]
        public void Run_Given_FewerIterationsWithTheSameSeed_Then_GivesThePrefix()
        {
            // Each chunk of 64 iterations has its own generator, and so the first iterations of a
            // longer run are the iterations of a shorter run with the same seed.
            ScheduleSimulator<int, int, int> simulator = SimulatorOf(SpikePlan.ReferenceSample(), useResources: false);

            SimulationResult<int> longer = Run(simulator, new SimulationSettings { Iterations = 300, Seed = 9 });
            SimulationResult<int> shorter = Run(simulator, new SimulationSettings { Iterations = 128, Seed = 9 });

            shorter.FinishTimes.ShouldBe(longer.FinishTimes.Take(128).ToList());
        }

        [Fact]
        public void Run_Given_TheReferenceSample_Then_GivesTheFinishTimesAndThePathsThatThePhaseZeroMeasured()
        {
            // With the beta of the reference tool and whole days, phase 0 measured a mean finish of
            // 52.41, a standard deviation of 1.71, and the path A > D > F > H > L > O in 59.6 % of
            // the iterations (the reference tool, with real numbers, gave 52.39, 1.59 and 70.1 %).
            // The ids of the sample are 1 (Start), 2 to 16 (A to O) and 17 (Finish).
            ScheduleSimulator<int, int, int> simulator = SimulatorOf(SpikePlan.ReferenceSample(), useResources: false);
            simulator.PlannedFinishTime.ShouldBe(52);

            SimulationResult<int> result = Run(simulator, new SimulationSettings { Iterations = 20_000, Seed = 2026 });

            double mean = result.FinishTimes.Average();
            double deviation = Math.Sqrt(result.FinishTimes.Average(x => (x - mean) * (x - mean)));
            mean.ShouldBe(52.41, 0.1);
            deviation.ShouldBe(1.71, 0.06);

            int[] mainPath = { 2, 5, 7, 9, 13, 16 };
            int mainPathIndex = result.CriticalPaths
                .Select((x, i) => (Path: x, Index: i))
                .Single(x => x.Path.Steps.Select(s => s.ActivityId).SequenceEqual(mainPath)
                    && x.Path.Steps.All(s => s.Link != CriticalPathLink.Resource))
                .Index;

            // The iterations with that path alone (59.6 % in phase 0), the iterations with more than
            // one path (19.5 %), and so the iterations that have the path among others.
            double alone = Enumerable.Range(0, result.IterationCount)
                .Count(i => result.CriticalPathIndexes(i).ToArray().SequenceEqual(new[] { mainPathIndex }));
            double tied = Enumerable.Range(0, result.IterationCount)
                .Count(i => result.CriticalPathIndexes(i).Length > 1);
            double among = Enumerable.Range(0, result.IterationCount)
                .Count(i => result.CriticalPathIndexes(i).ToArray().Contains(mainPathIndex));
            (alone / result.IterationCount).ShouldBe(0.596, 0.03);
            (tied / result.IterationCount).ShouldBe(0.195, 0.03);
            (among / result.IterationCount).ShouldBeInRange(0.72, 0.84);
        }

        [Fact]
        public void Run_Given_ARunThatIsCancelled_Then_ThrowsOperationCanceledException()
        {
            ScheduleSimulator<int, int, int> simulator = SimulatorOf(SpikePlan.LoadSample(), useResources: true);
            using var source = new CancellationTokenSource();
            source.Cancel();

            Should.Throw<OperationCanceledException>(() => simulator.Run(new SimulationSettings { Iterations = 1000 }, source.Token));
        }

        [Fact]
        public void Run_Given_ACancellationDuringTheRun_Then_StopsBeforeTheEnd()
        {
            ScheduleSimulator<int, int, int> simulator = SimulatorOf(SpikePlan.LoadSample(), useResources: true);
            using var source = new CancellationTokenSource();
            int reported = 0;
            var progress = new SyncProgress(done =>
            {
                reported = Math.Max(reported, done);
                source.Cancel();
            });

            Should.Throw<OperationCanceledException>(() =>
                simulator.Run(new SimulationSettings { Iterations = 100_000, MaxDegreeOfParallelism = 2 }, progress, source.Token));

            reported.ShouldBeGreaterThan(0);
            reported.ShouldBeLessThan(100_000);
        }

        [Fact]
        public void Run_Given_AProgress_Then_ReportsUpToTheNumberOfIterations()
        {
            ScheduleSimulator<int, int, int> simulator = SimulatorOf(SpikePlan.ReferenceSample(), useResources: false);
            int largest = 0;
            var progress = new SyncProgress(done =>
            {
                int seen;
                do
                {
                    seen = Volatile.Read(ref largest);
                }
                while (done > seen && Interlocked.CompareExchange(ref largest, done, seen) != seen);
            });

            simulator.Run(new SimulationSettings { Iterations = 1000 }, progress, TestContext.Current.CancellationToken);

            largest.ShouldBe(1000);
        }

        private sealed class SyncProgress : IProgress<int>
        {
            private readonly Action<int> m_Action;

            internal SyncProgress(Action<int> action)
            {
                m_Action = action;
            }

            public void Report(int value) => m_Action(value);
        }

        [Fact]
        public void Run_Given_APlanWithNoSpread_Then_EveryIterationIsThePlannedFinish()
        {
            SpikePlan plan = SmallPlan(Record(1, 4), Record(2, 6, 1), Record(3, 3, 1));
            var simulator = new ScheduleSimulator<int, int, int>(ActivitiesOf(plan), plan.NewResources(false), plan.NewWorkStreams(), new Dictionary<int, DurationEstimate>());

            SimulationResult<int> result = Run(simulator, new SimulationSettings { Iterations = 200 });

            simulator.PlannedFinishTime.ShouldBe(10);
            result.FinishTimes.ShouldAllBe(x => x == 10);
            result.CriticalPaths.Count.ShouldBe(1);
            result.CriticalPaths[0].Steps.Select(x => x.ActivityId).ShouldBe(new[] { 1, 2 });
        }

        [Fact]
        public void Run_Given_APlanWithEstimates_Then_TheFinishStaysBetweenTheOptimisticAndThePessimisticSchedule()
        {
            var plan = SmallPlan(
                new SpikePlan.ActivityRecord { Id = 1, Name = "1", Duration = 4, Optimistic = 2, Pessimistic = 9 },
                new SpikePlan.ActivityRecord { Id = 2, Name = "2", Duration = 6, Optimistic = 5, Pessimistic = 12, Dependencies = new List<int> { 1 } });
            ScheduleSimulator<int, int, int> simulator = SimulatorOf(plan, useResources: false);

            SimulationResult<int> result = Run(simulator, new SimulationSettings { Iterations = 1000, Distribution = DurationDistribution.Triangular });

            result.FinishTimes.Min().ShouldBeGreaterThanOrEqualTo(7);
            result.FinishTimes.Max().ShouldBeLessThanOrEqualTo(21);
            result.FinishTimes.Distinct().Count().ShouldBeGreaterThan(5);
        }

        [Fact]
        public void Run_Given_ARunThatDoesNotRecordTheCriticalActivitiesOrPaths_Then_TheResultHasOnlyTheFinishTimes()
        {
            ScheduleSimulator<int, int, int> simulator = SimulatorOf(SpikePlan.ReferenceSample(), useResources: false);

            SimulationResult<int> result = Run(simulator, new SimulationSettings
            {
                Iterations = 100,
                RecordCriticalActivities = false,
                RecordCriticalPaths = false,
            });

            result.HasCriticalActivities.ShouldBeFalse();
            result.HasCriticalPaths.ShouldBeFalse();
            Should.Throw<InvalidOperationException>(() => result.IsCritical(0, 0));
            Should.Throw<InvalidOperationException>(() => result.CriticalPathIndexes(0).ToArray());
            result.FinishTimes.Count.ShouldBe(100);
        }

        [Fact]
        public void Run_Given_ARunThatRecordsTheCriticalActivities_Then_TheFinishingActivityOfEveryIterationIsCritical()
        {
            ScheduleSimulator<int, int, int> simulator = SimulatorOf(SpikePlan.ReferenceSample(), useResources: false);

            SimulationResult<int> result = Run(simulator, new SimulationSettings { Iterations = 200, Seed = 3 });

            int finish = simulator.ActivityIds.ToList().IndexOf(17);
            for (int iteration = 0; iteration < result.IterationCount; iteration++)
            {
                result.IsCritical(iteration, finish).ShouldBeTrue();
            }
        }

        [Fact]
        public void Run_Given_APlanWithErrors_Then_ThePlanReportsThemAndTheRunThrows()
        {
            // Two activities that depend on each other.
            SpikePlan plan = SmallPlan(Record(1, 4, 2), Record(2, 6, 1));
            var simulator = new ScheduleSimulator<int, int, int>(ActivitiesOf(plan), plan.NewResources(false), plan.NewWorkStreams(), new Dictionary<int, DurationEstimate>());

            simulator.CompilationErrors.ShouldNotBeEmpty();
            Should.Throw<InvalidOperationException>(() => Run(simulator, new SimulationSettings()));
        }

        [Fact]
        public void Ctor_Given_AnEstimateOfAnActivityThatIsNotInThePlan_Then_Throws()
        {
            SpikePlan plan = SmallPlan(Record(1, 4));

            Should.Throw<ArgumentException>(() => new ScheduleSimulator<int, int, int>(
                ActivitiesOf(plan),
                plan.NewResources(false),
                plan.NewWorkStreams(),
                new Dictionary<int, DurationEstimate> { [99] = new DurationEstimate(1, 2, 3) }));
        }

        [Fact]
        public void Ctor_Given_TwoActivitiesWithTheSameId_Then_Throws()
        {
            SpikePlan plan = SmallPlan(Record(1, 4), Record(1, 5));

            Should.Throw<ArgumentException>(() => new ScheduleSimulator<int, int, int>(
                ActivitiesOf(plan),
                plan.NewResources(false),
                plan.NewWorkStreams(),
                new Dictionary<int, DurationEstimate>()));
        }

        [Fact]
        public void Ctor_Given_APlan_Then_DoesNotChangeTheActivitiesThatItIsGiven()
        {
            SpikePlan plan = SpikePlan.LoadSample();
            List<IDependentActivity<int, int, int>> activities = ActivitiesOf(plan);
            int[] durations = activities.Select(x => x.Duration).ToArray();

            ScheduleSimulator<int, int, int> simulator = new ScheduleSimulator<int, int, int>(activities, plan.NewResources(true), plan.NewWorkStreams(), EstimatesOf(plan));
            Run(simulator, new SimulationSettings { Iterations = 64 });

            activities.Select(x => x.Duration).ShouldBe(durations);
            activities.ShouldAllBe(x => x.ResourceDependencies.Count == 0 && x.AllocatedToResources.Count == 0);
        }

        [Fact]
        public async Task Run_Given_TwoRunsAtTheSameTimeOnOneSimulator_Then_BothGiveTheirOwnCorrectResult()
        {
            ScheduleSimulator<int, int, int> simulator = SimulatorOf(SpikePlan.LoadSample(), useResources: true);
            SimulationResult<int> expectedA = Run(simulator, new SimulationSettings { Iterations = 128, Seed = 1 });
            SimulationResult<int> expectedB = Run(simulator, new SimulationSettings { Iterations = 128, Seed = 2 });

            Task<SimulationResult<int>> a = Task.Run(() => Run(simulator, new SimulationSettings { Iterations = 128, Seed = 1 }), TestContext.Current.CancellationToken);
            Task<SimulationResult<int>> b = Task.Run(() => Run(simulator, new SimulationSettings { Iterations = 128, Seed = 2 }), TestContext.Current.CancellationToken);

            ShouldBeTheSame(await a, expectedA);
            ShouldBeTheSame(await b, expectedB);
        }

        #endregion

        #region The paths

        [Fact]
        public void Run_Given_TwoEqualChainsOfActivities_Then_BothAreCriticalPathsOfEveryIteration()
        {
            // 1 > 2 > 4 and 1 > 3 > 4, with 2 and 3 of the same duration.
            SpikePlan plan = SmallPlan(Record(1, 2), Record(2, 5, 1), Record(3, 5, 1), Record(4, 3, 2, 3));
            var simulator = new ScheduleSimulator<int, int, int>(ActivitiesOf(plan), plan.NewResources(false), plan.NewWorkStreams(), new Dictionary<int, DurationEstimate>());

            SimulationResult<int> result = Run(simulator, new SimulationSettings { Iterations = 10 });

            result.CriticalPaths.Count.ShouldBe(2);
            for (int i = 0; i < 10; i++)
            {
                result.CriticalPathIndexes(i).Length.ShouldBe(2);
                result.IsCriticalPathListComplete(i).ShouldBeTrue();
            }
        }

        [Fact]
        public void Run_Given_MoreTiedPathsThanTheMaximum_Then_RecordsTheFirstOnesAndSaysThatTheListIsNotComplete()
        {
            // Six stages, each with two activities of the same duration: 2 to the power 6 paths.
            var records = new List<SpikePlan.ActivityRecord>();
            int previousA = 0;
            int previousB = 0;
            for (int stage = 0; stage < 6; stage++)
            {
                int a = (stage * 2) + 1;
                int b = (stage * 2) + 2;
                int[] dependencies = stage == 0 ? new int[0] : new[] { previousA, previousB };
                records.Add(Record(a, 3, dependencies));
                records.Add(Record(b, 3, dependencies));
                previousA = a;
                previousB = b;
            }
            SpikePlan plan = SmallPlan(records.ToArray());
            var simulator = new ScheduleSimulator<int, int, int>(ActivitiesOf(plan), plan.NewResources(false), plan.NewWorkStreams(), new Dictionary<int, DurationEstimate>());

            SimulationResult<int> all = Run(simulator, new SimulationSettings { Iterations = 3, MaximumCriticalPathsPerIteration = 1000 });
            SimulationResult<int> few = Run(simulator, new SimulationSettings { Iterations = 3, MaximumCriticalPathsPerIteration = 10 });

            all.CriticalPathIndexes(0).Length.ShouldBe(64);
            all.IsCriticalPathListComplete(0).ShouldBeTrue();
            few.CriticalPathIndexes(0).Length.ShouldBe(10);
            few.IsCriticalPathListComplete(0).ShouldBeFalse();

            // The first ones, in the order of the walk, are the same in both.
            few.CriticalPathIndexes(0).ToArray().Select(x => few.CriticalPaths[x])
                .ShouldBe(all.CriticalPathIndexes(0).ToArray().Take(10).Select(x => all.CriticalPaths[x]));
        }

        [Fact]
        public void Run_Given_ChainsThatDifferOnlyByAMilestone_Then_TheyAreOnePathAndTheMilestoneIsNotOnIt()
        {
            // 1 > 2 (a milestone) > 3, and 1 > 3 as well. Both chains are tight.
            SpikePlan plan = SmallPlan(Record(1, 5), Record(2, 0, 1), Record(3, 5, 1, 2));
            var simulator = new ScheduleSimulator<int, int, int>(ActivitiesOf(plan), plan.NewResources(false), plan.NewWorkStreams(), new Dictionary<int, DurationEstimate>());

            SimulationResult<int> result = Run(simulator, new SimulationSettings { Iterations = 4 });

            result.CriticalPaths.Count.ShouldBe(1);
            result.CriticalPaths[0].Steps.Select(x => x.ActivityId).ShouldBe(new[] { 1, 3 });
            result.CriticalPathIndexes(0).Length.ShouldBe(1);
        }

        [Fact]
        public void Run_Given_TwoActivitiesThatShareOneResource_Then_TheSecondFollowsTheFirstByAResourceLink()
        {
            // 1 and 2 are independent, and one resource does them in turn. The activity that goes
            // second is critical only because the resource is busy.
            SpikePlan plan = SmallPlan(Record(1, 5), Record(2, 5));
            plan.Resources.Add(new SpikePlan.ResourceRecord { Id = 1 });
            var simulator = new ScheduleSimulator<int, int, int>(ActivitiesOf(plan), plan.NewResources(true), plan.NewWorkStreams(), new Dictionary<int, DurationEstimate>());

            SimulationResult<int> result = Run(simulator, new SimulationSettings { Iterations = 2 });

            simulator.PlannedFinishTime.ShouldBe(10);
            result.CriticalPaths.Count.ShouldBe(1);
            result.CriticalPaths[0].Steps.Select(x => x.Link).ShouldBe(new[] { CriticalPathLink.None, CriticalPathLink.Resource });
        }

        [Fact]
        public void Run_Given_TheSamplePlanWithItsResources_Then_APathHasAResourceLinkWhenTheResourceDecidesTheOrder()
        {
            ScheduleSimulator<int, int, int> simulator = SimulatorOf(SpikePlan.LoadSample(), useResources: true);

            SimulationResult<int> result = Run(simulator, new SimulationSettings { Iterations = 200, Seed = 4 });

            result.CriticalPaths.ShouldContain(x => x.Steps.Any(s => s.Link == CriticalPathLink.Resource));
            result.CriticalPaths.ShouldContain(x => x.Steps.Any(s => s.Link == CriticalPathLink.Dependency));
            Describe(result, 0).ShouldNotBeEmpty();
        }

        #endregion

        #region The settings

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(1_000_001)]
        public void Iterations_Given_AValueOutOfRange_Then_Throws(int value)
        {
            Should.Throw<ArgumentOutOfRangeException>(() => new SimulationSettings { Iterations = value });
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public void ModifiedBetaShape_Given_AValueThatIsNotAPositiveNumber_Then_Throws(double value)
        {
            Should.Throw<ArgumentOutOfRangeException>(() => new SimulationSettings { ModifiedBetaShape = value });
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-2)]
        public void MaxDegreeOfParallelism_Given_AValueOutOfRange_Then_Throws(int value)
        {
            Should.Throw<ArgumentOutOfRangeException>(() => new SimulationSettings { MaxDegreeOfParallelism = value });
        }

        [Fact]
        public void MaximumCriticalPathsPerIteration_Given_ZeroPaths_Then_Throws()
        {
            Should.Throw<ArgumentOutOfRangeException>(() => new SimulationSettings { MaximumCriticalPathsPerIteration = 0 });
        }

        #endregion
    }
}
