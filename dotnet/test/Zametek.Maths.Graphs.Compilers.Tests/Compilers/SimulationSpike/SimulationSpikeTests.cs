using Shouldly;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    // Phase 0 of the plan for the Monte Carlo schedule simulation: what an iteration costs,
    // whether a lean scheduling pass gives the answer of a full compile, and whether the
    // samplers and the critical-path calculation reproduce the published numbers of the
    // reference tool.
    //
    // The tests without the Explicit mark are checks and run with the others. The tests
    // with it are measurements. Build in Release first, then:
    //   .\Zametek.Maths.Graphs.Compilers.Tests.exe -explicit only -method "*SimulationSpikeTests*" -showLiveOutput
    public class SimulationSpikeTests
    {
        private readonly ITestOutputHelper m_Output;

        public SimulationSpikeTests(ITestOutputHelper output)
        {
            m_Output = output;
        }

        #region The plan of the application

        [Fact]
        public void SamplePlan_GivenThePlanOfTheApplication_ThenTheLibraryCompilesItToTheFinishTheApplicationStored()
        {
            // Not about the simulation. It shows that the fixture is the plan it claims to
            // be, and that the data the library needs is all in it: a wrong fixture would
            // make every later comparison meaningless.
            SpikePlan plan = SpikePlan.LoadSample();

            (int finish, bool[] critical, int errors) = plan.CompileOracle(plan.MostLikelyDurations(), useResources: true);

            errors.ShouldBe(0);
            finish.ShouldBe(plan.StoredFinishTime);
            critical.Any(x => x).ShouldBeTrue();
        }

        #endregion

        #region The samplers

        private static (double Mean, double Variance, double Minimum, double Maximum) Moments(
            Func<SpikeRandom, double> draw,
            int count = 400_000)
        {
            var random = new SpikeRandom(12345);
            double sum = 0;
            double sumOfSquares = 0;
            double minimum = double.MaxValue;
            double maximum = double.MinValue;
            for (int i = 0; i < count; i++)
            {
                double x = draw(random);
                sum += x;
                sumOfSquares += x * x;
                minimum = Math.Min(minimum, x);
                maximum = Math.Max(maximum, x);
            }
            double mean = sum / count;
            return (mean, (sumOfSquares / count) - (mean * mean), minimum, maximum);
        }

        [Fact]
        public void SpikeRandom_GivenTheSameSeed_ThenTheSequenceIsTheSame()
        {
            var a = new SpikeRandom(7);
            var b = new SpikeRandom(7);
            var c = new SpikeRandom(8);

            bool differsFromOtherSeed = false;
            for (int i = 0; i < 100; i++)
            {
                ulong x = a.NextUInt64();
                x.ShouldBe(b.NextUInt64());
                differsFromOtherSeed |= x != c.NextUInt64();
            }
            differsFromOtherSeed.ShouldBeTrue();
        }

        [Fact]
        public void SpikeRandom_GivenAShapeBelowOne_ThenTheGammaHasItsMeanAndVariance()
        {
            // The gamma of shape k has mean k and variance k. Marsaglia and Tsang need a
            // correction for a shape under one, and a slip in it would not show elsewhere.
            (double mean, double variance, _, _) = Moments(r => r.NextGamma(0.5));

            mean.ShouldBe(0.5, 0.01);
            variance.ShouldBe(0.5, 0.02);
        }

        [Fact]
        public void BetaPertMomentMatched_GivenAnEstimate_ThenTheMeanAndVarianceAreThoseOfThePertFormulas()
        {
            // O = 4, ML = 5, P = 12: mean (4 + 20 + 12) / 6 = 6 and variance (8 / 6) squared.
            (double mean, double variance, double minimum, double maximum) =
                Moments(r => SpikeSamplers.BetaPertMomentMatched(r, 4, 5, 12));

            mean.ShouldBe(6.0, 0.02);
            variance.ShouldBe(64.0 / 36.0, 0.03);
            minimum.ShouldBeGreaterThanOrEqualTo(4.0);
            maximum.ShouldBeLessThanOrEqualTo(12.0);
        }

        [Fact]
        public void BetaPertShape4_GivenAnEstimate_ThenTheMeanIsThePertMeanAndTheVarianceIsTheModifiedPertVariance()
        {
            // The mean is (O + 4 ML + P) / 6 = 6 again, but the variance is
            // (mean - O)(P - mean) / 7 = 12 / 7, which is not the ((P - O) / 6) squared of
            // the formula. The two betas are different distributions.
            (double mean, double variance, double minimum, double maximum) =
                Moments(r => SpikeSamplers.BetaPertShaped(r, 4, 5, 12, 4.0));

            mean.ShouldBe(6.0, 0.02);
            variance.ShouldBe(12.0 / 7.0, 0.03);
            minimum.ShouldBeGreaterThanOrEqualTo(4.0);
            maximum.ShouldBeLessThanOrEqualTo(12.0);
        }

        [Fact]
        public void Triangular_GivenAnEstimate_ThenTheMeanAndVarianceAreThoseOfTheTriangle()
        {
            (double mean, double variance, double minimum, double maximum) =
                Moments(r => SpikeSamplers.Triangular(r, 4, 5, 12));

            mean.ShouldBe(7.0, 0.02);
            variance.ShouldBe(57.0 / 18.0, 0.04);
            minimum.ShouldBeGreaterThanOrEqualTo(4.0);
            maximum.ShouldBeLessThanOrEqualTo(12.0);
        }

        [Fact]
        public void Normal_GivenAnEstimate_ThenNoDrawIsOutsideTheRangeOfTheEstimate()
        {
            // The mean is 6 and the deviation 4 / 3, so the lower end (4) is 1.5 deviations
            // out and about 6.7 % of the draws are held there. That lifts the mean a little.
            (double mean, _, double minimum, double maximum) =
                Moments(r => SpikeSamplers.Normal(r, 4, 5, 12));

            minimum.ShouldBe(4.0);
            maximum.ShouldBeLessThanOrEqualTo(12.0);
            mean.ShouldBe(6.04, 0.03);
        }

        [Fact]
        public void Uniform_GivenAnEstimate_ThenTheDrawsFillTheRange()
        {
            (double mean, double variance, double minimum, double maximum) =
                Moments(r => SpikeSamplers.Sample(SpikeDistribution.Uniform, r, 4, 5, 12));

            mean.ShouldBe(8.0, 0.02);
            variance.ShouldBe(64.0 / 12.0, 0.05);
            minimum.ShouldBeGreaterThanOrEqualTo(4.0);
            maximum.ShouldBeLessThan(12.0);
        }

        #endregion

        #region The reference tool

        private sealed class Summary
        {
            internal int Count;
            internal double Mean;
            internal double StandardDeviation;
            internal double Minimum;
            internal double Maximum;
            internal double P50;
            internal double P80;
            internal double P90;
            internal List<(string Path, int Count)> Paths;

            internal double Share(string path) =>
                100.0 * Paths.Where(x => x.Path == path).Select(x => x.Count).FirstOrDefault() / Count;
        }

        private static Summary Summarise(IReadOnlyList<double> finishes, IReadOnlyList<string> labels)
        {
            int n = finishes.Count;
            double[] sorted = finishes.OrderBy(x => x).ToArray();
            double mean = finishes.Average();
            // The variance is taken over n, as the reference tool takes it.
            double variance = finishes.Sum(x => (x - mean) * (x - mean)) / n;
            double Percentile(double p) => sorted[Math.Max(0, (int)Math.Ceiling(p * n) - 1)];
            return new Summary
            {
                Count = n,
                Mean = mean,
                StandardDeviation = Math.Sqrt(variance),
                Minimum = sorted[0],
                Maximum = sorted[n - 1],
                P50 = Percentile(0.5),
                P80 = Percentile(0.8),
                P90 = Percentile(0.9),
                Paths = labels.GroupBy(x => x).Select(g => (g.Key, g.Count())).OrderByDescending(x => x.Item2).ToList(),
            };
        }

        private static (SpikeCriticalPaths Cpm, int[][] Predecessors, string[] Names) BuildCpm(SpikePlan plan)
        {
            Dictionary<int, int> indexById = plan.Activities.Select((x, i) => (x.Id, i)).ToDictionary(x => x.Id, x => x.i);
            int[][] predecessors = plan.Activities
                .Select(a => a.Dependencies.Concat(a.PlanningDependencies).Select(d => indexById[d]).ToArray())
                .ToArray();
            return (new SpikeCriticalPaths(predecessors), predecessors, plan.Activities.Select(x => x.Name).ToArray());
        }

        // The simulation of the reference tool: durations in hundredths, the critical-path
        // calculation of the reference tool, and no resources.
        private static Summary RunLikeTheReferenceTool(SpikePlan plan, SpikeDistribution distribution, int iterations, ulong seed)
        {
            (SpikeCriticalPaths cpm, int[][] predecessors, string[] names) = BuildCpm(plan);
            var random = new SpikeRandom(seed);
            var durations = new int[plan.Activities.Count];
            var critical = new bool[plan.Activities.Count];
            var finishes = new double[iterations];
            var labels = new string[iterations];

            for (int it = 0; it < iterations; it++)
            {
                for (int i = 0; i < durations.Length; i++)
                {
                    SpikePlan.ActivityRecord a = plan.Activities[i];
                    durations[i] = a.Optimistic == a.Pessimistic && a.Optimistic == a.Duration
                        ? a.Duration * 100
                        : Math.Max(0, (int)Math.Round(SpikeSamplers.Sample(distribution, random, a.Optimistic, a.Duration, a.Pessimistic) * 100.0, MidpointRounding.AwayFromZero));
                }
                finishes[it] = cpm.Calculate(durations, critical) / 100.0;
                labels[it] = SpikeCriticalPaths.Label(predecessors, critical, names, durations);
            }
            return Summarise(finishes, labels);
        }

        [Fact]
        public void ReferenceSample_GivenThePertMeanDurations_ThenTheFinishIs52AndThePathIsADFHLO()
        {
            SpikePlan plan = SpikePlan.ReferenceSample();
            (SpikeCriticalPaths cpm, int[][] predecessors, string[] names) = BuildCpm(plan);

            // The PERT mean of each activity, in hundredths.
            int[] durations = plan.Activities
                .Select(a => (int)Math.Round((a.Optimistic + (4.0 * a.Duration) + a.Pessimistic) / 6.0 * 100.0))
                .ToArray();
            var critical = new bool[plan.Activities.Count];

            int finish = cpm.Calculate(durations, critical);

            finish.ShouldBe(5200);
            SpikeCriticalPaths.Label(predecessors, critical, names, durations).ShouldBe("A>D>F>H>L>O");
        }

        [Fact]
        public void ReferenceSample_GivenTheMomentMatchedBeta_ThenTheSimulationReproducesTheNumbersOfTheReferenceTool()
        {
            // The reference tool published these from one run of 10,000 iterations: mean
            // 52.39, deviation 1.59, and the three paths in 70.1, 24.6 and 5.2 % of the
            // iterations. This run has twenty times as many iterations, so the tolerances
            // are about five standard errors of the published run.
            SpikePlan plan = SpikePlan.ReferenceSample();

            Summary summary = RunLikeTheReferenceTool(plan, SpikeDistribution.BetaPertMomentMatched, 200_000, seed: 2026);

            m_Output.WriteLine(
                $@"mean {summary.Mean:F3}, deviation {summary.StandardDeviation:F3}, min {summary.Minimum:F2}, max {summary.Maximum:F2}");
            foreach ((string path, int count) in summary.Paths.Take(5))
            {
                m_Output.WriteLine($@"  {path,-14} {100.0 * count / summary.Count:F2} %");
            }

            summary.Mean.ShouldBe(52.39, 0.08);
            summary.StandardDeviation.ShouldBe(1.59, 0.06);
            summary.Share("A>D>F>H>L>O").ShouldBe(70.1, 2.0);
            summary.Share("A>D>I>M>O").ShouldBe(24.6, 2.0);
            summary.Share("A>E>J>M>O").ShouldBe(5.2, 1.0);
            summary.Share("Multiple").ShouldBeLessThan(1.0);
        }

        #endregion

        #region The lean path against the full compile

        private static List<int[]> DrawVectors(SpikePlan plan, SpikeDistribution distribution, int count, ulong seed)
        {
            var random = new SpikeRandom(seed);
            var vectors = new List<int[]>();
            for (int i = 0; i < count; i++)
            {
                var durations = new int[plan.Activities.Count];
                SpikeSimulation.DrawDurations(plan, distribution, random, durations);
                vectors.Add(durations);
            }
            return vectors;
        }

        // The number of duration vectors on which the lean path and a full compile of a new
        // compiler give different finish times or different critical activities. The lean
        // scheduler is the same object for every vector, so a result that depends on what
        // an earlier iteration left behind shows up here as well.
        private static int CountMismatches(SpikePlan plan, SpikeLeanScheduler lean, bool useResources, List<int[]> vectors)
        {
            int mismatches = 0;
            var critical = new bool[plan.Activities.Count];
            foreach (int[] durations in vectors)
            {
                int leanFinish = lean.Run(durations, critical, CancellationToken.None);
                (int finish, bool[] oracleCritical, int errors) = plan.CompileOracle(durations, useResources);

                errors.ShouldBe(0);
                if (leanFinish != finish || !critical.SequenceEqual(oracleCritical))
                {
                    mismatches++;
                }
            }
            return mismatches;
        }

        [Fact]
        public void LeanPath_GivenThePlanOfTheApplicationWithItsResources_ThenItGivesWhatAFullCompileGives()
        {
            SpikePlan plan = SpikePlan.LoadSample();

            // The first vector is the plan as it stands, and the rest are random.
            List<int[]> vectors = DrawVectors(plan, SpikeDistribution.BetaPertMomentMatched, 40, seed: 11);
            vectors.Insert(0, plan.MostLikelyDurations());

            CountMismatches(plan, new SpikeLeanScheduler(plan, useResources: true), useResources: true, vectors).ShouldBe(0);
        }

        // The shortcut for a plan with no resources: stop after the first critical-path
        // calculation. It must give the same finish time as the compile, and its critical
        // activities must be among those of the compile. They are not the same set. With no
        // resources the compile still schedules onto synthetic resources, and a tie between
        // the finish of one activity and the start of the next lets a resource link make a
        // chain of activities critical that no tight dependency makes critical.
        private static (int FinishDifferences, int NotASubset, int Larger) CompareShortcutWithCompile(
            SpikePlan plan,
            List<int[]> vectors)
        {
            var shortcut = new SpikeLeanScheduler(plan, useResources: false) { CriticalPathOnlyWithoutResources = true };
            var critical = new bool[plan.Activities.Count];
            int finishDifferences = 0;
            int notASubset = 0;
            int larger = 0;
            foreach (int[] durations in vectors)
            {
                int finish = shortcut.Run(durations, critical, CancellationToken.None);
                (int oracleFinish, bool[] oracleCritical, int errors) = plan.CompileOracle(durations, useResources: false);
                errors.ShouldBe(0);
                finishDifferences += finish == oracleFinish ? 0 : 1;
                notASubset += critical.Where((x, i) => x && !oracleCritical[i]).Any() ? 1 : 0;
                larger += oracleCritical.Count(x => x) > critical.Count(x => x) ? 1 : 0;
            }
            return (finishDifferences, notASubset, larger);
        }

        [Fact]
        public void LeanPath_GivenThePlanOfTheApplicationWithNoResources_ThenItGivesWhatAFullCompileGives()
        {
            SpikePlan plan = SpikePlan.LoadSample();
            List<int[]> vectors = DrawVectors(plan, SpikeDistribution.BetaPertMomentMatched, 40, seed: 12);

            CountMismatches(
                plan,
                new SpikeLeanScheduler(plan, useResources: false) { CriticalPathOnlyWithoutResources = false },
                useResources: false,
                vectors).ShouldBe(0);

            (int finishDifferences, int notASubset, int larger) = CompareShortcutWithCompile(plan, vectors);
            finishDifferences.ShouldBe(0);
            notASubset.ShouldBe(0);
            m_Output.WriteLine($@"Shortcut against compile, sample plan with no resources: {larger} of {vectors.Count} compiles have a larger critical set.");
        }

        [Fact]
        public void LeanPath_GivenALayeredPlan_ThenItGivesWhatAFullCompileGives()
        {
            SpikePlan plan = SpikePlan.Layered(150, layers: 6, resourceCount: 12);
            List<int[]> vectors = DrawVectors(plan, SpikeDistribution.Triangular, 25, seed: 13);

            CountMismatches(plan, new SpikeLeanScheduler(plan, useResources: true), useResources: true, vectors).ShouldBe(0);
        }

        [Fact]
        public void LeanPath_GivenTheReferenceSample_ThenItGivesWhatAFullCompileGives()
        {
            SpikePlan plan = SpikePlan.ReferenceSample();
            List<int[]> vectors = DrawVectors(plan, SpikeDistribution.BetaPertMomentMatched, 60, seed: 14);

            CountMismatches(
                plan,
                new SpikeLeanScheduler(plan, useResources: false) { CriticalPathOnlyWithoutResources = false },
                useResources: false,
                vectors).ShouldBe(0);

            (int finishDifferences, int notASubset, int larger) = CompareShortcutWithCompile(plan, vectors);
            finishDifferences.ShouldBe(0);
            notASubset.ShouldBe(0);
            larger.ShouldBeGreaterThan(0);
            m_Output.WriteLine($@"Shortcut against compile, reference sample: {larger} of {vectors.Count} compiles have a larger critical set.");
        }

        [Fact(Explicit = true)]
        public void DiagnoseShortcutMismatches()
        {
            SpikePlan plan = SpikePlan.ReferenceSample();
            List<int[]> vectors = DrawVectors(plan, SpikeDistribution.BetaPertMomentMatched, 60, seed: 14);
            var shortcut = new SpikeLeanScheduler(plan, useResources: false) { CriticalPathOnlyWithoutResources = true };
            var full = new SpikeLeanScheduler(plan, useResources: false) { CriticalPathOnlyWithoutResources = false };
            var criticalA = new bool[plan.Activities.Count];
            var criticalB = new bool[plan.Activities.Count];

            string Names(bool[] c) => string.Join(",", plan.Activities.Where((x, i) => c[i] && x.Name.Length == 1).Select(x => x.Name));

            for (int v = 0; v < vectors.Count; v++)
            {
                int[] d = vectors[v];
                int a = shortcut.Run(d, criticalA, CancellationToken.None);
                int b = full.Run(d, criticalB, CancellationToken.None);
                (int finish, bool[] oracle, _) = plan.CompileOracle(d, useResources: false);

                if (a != finish || b != finish || !criticalA.SequenceEqual(oracle) || !criticalB.SequenceEqual(oracle))
                {
                    m_Output.WriteLine($@"vector {v}: durations {string.Join(" ", d)}");
                    for (int i = 0; i < plan.Activities.Count; i++)
                    {
                        IDependentActivity<int, int, int> activity = full.Activity(i);
                        if (plan.Activities[i].Name.Length == 1)
                        {
                            m_Output.WriteLine(
                                $@"    {plan.Activities[i].Name} d={d[i],2} ES={activity.EarliestStartTime,2} EF={activity.EarliestFinishTime,2} LF={activity.LatestFinishTime,2} slack={activity.TotalSlack,2} deps=[{string.Join(",", activity.Dependencies.Select(x => plan.Activities.First(y => y.Id == x).Name))}] resourceDeps=[{string.Join(",", activity.ResourceDependencies.Select(x => plan.Activities.First(y => y.Id == x).Name))}]");
                        }
                    }
                    m_Output.WriteLine($@"  shortcut finish {a} critical {Names(criticalA)}");
                    m_Output.WriteLine($@"  full lean finish {b} critical {Names(criticalB)}");
                    m_Output.WriteLine($@"  compile  finish {finish} critical {Names(oracle)}");
                }
            }
        }

        [Fact]
        public void LeanPath_GivenASecondPassThatIsLeftOut_ThenTheComparisonWithTheCompileFails()
        {
            // The check on the check. Without the second critical-path calculation the
            // finish time does not include the delays that the resources cause, and the
            // comparison must notice. A comparison that cannot fail proves nothing.
            SpikePlan plan = SpikePlan.LoadSample();
            List<int[]> vectors = DrawVectors(plan, SpikeDistribution.BetaPertMomentMatched, 10, seed: 15);

            CountMismatches(
                plan,
                new SpikeLeanScheduler(plan, useResources: true) { FaultSkipSecondPass = true },
                useResources: true,
                vectors).ShouldBeGreaterThan(0);
        }

        [Fact]
        public void LeanPath_GivenTheResetThatIsLeftOut_ThenTheComparisonWithTheCompileFails()
        {
            // The second fault: the resource dependencies of one iteration stay in the graph
            // and shape the next one.
            SpikePlan plan = SpikePlan.LoadSample();
            List<int[]> vectors = DrawVectors(plan, SpikeDistribution.BetaPertMomentMatched, 10, seed: 16);

            CountMismatches(
                plan,
                new SpikeLeanScheduler(plan, useResources: true) { FaultSkipReset = true },
                useResources: true,
                vectors).ShouldBeGreaterThan(0);
        }

        [Fact]
        public void PriorityListModes_GivenTheMostLikelyDurations_ThenTheFrozenListGivesTheExactFinishAndCriticalActivities()
        {
            // The check on the experiment that follows. The frozen list is the exact list
            // for the most likely durations, so for those durations the two must agree. The
            // static order must at least schedule every iteration without a stall.
            SpikePlan plan = SpikePlan.LoadSample();
            int[] mostLikely = plan.MostLikelyDurations();
            var exactCritical = new bool[plan.Activities.Count];
            var frozenCritical = new bool[plan.Activities.Count];

            int exact = new SpikeLeanScheduler(plan, useResources: true).Run(mostLikely, exactCritical, CancellationToken.None);
            int frozen = new SpikeLeanScheduler(plan, useResources: true)
            {
                PriorityListMode = SpikePriorityList.FrozenFromTheMostLikelyDurations,
            }.Run(mostLikely, frozenCritical, CancellationToken.None);

            frozen.ShouldBe(exact);
            frozenCritical.ShouldBe(exactCritical);

            SpikeSimulation.Result staticOrder = SpikeSimulation.Run(
                plan, SpikeDistribution.BetaPertMomentMatched, 128, seed: 3, threads: 1, useResources: true, withLabels: false,
                priorityList: SpikePriorityList.StaticSlackOrder);
            staticOrder.Failed.ShouldBe(0);
        }

        [Fact]
        public void Run_GivenNoSpreadInTheEstimates_ThenEveryIterationGivesTheFinishOfThePlan()
        {
            // The zero-variance check: when O, ML and P are equal, each iteration is the
            // compile of the plan itself, and the finish is the one the application stored.
            SpikePlan plan = SpikePlan.LoadSample();
            plan.SetEstimatesFromMostLikely(optimisticFactor: 1.0, pessimisticFactor: 1.0);

            SpikeSimulation.Result result =
                SpikeSimulation.Run(plan, SpikeDistribution.BetaPertMomentMatched, 64, seed: 1, threads: 1, useResources: true, withLabels: true);

            result.Failed.ShouldBe(0);
            result.Finishes.ShouldAllBe(x => x == plan.StoredFinishTime);
            result.Labels.Distinct().Count().ShouldBe(1);
        }

        [Fact]
        public void Run_GivenDifferentNumbersOfThreads_ThenTheResultsAreTheSame()
        {
            // The design: a chunk of iterations has a generator of its own, so that the
            // result does not depend on how many threads share the work.
            SpikePlan plan = SpikePlan.LoadSample();

            SpikeSimulation.Result one =
                SpikeSimulation.Run(plan, SpikeDistribution.BetaPertMomentMatched, 192, seed: 99, threads: 1, useResources: true, withLabels: false);
            SpikeSimulation.Result three =
                SpikeSimulation.Run(plan, SpikeDistribution.BetaPertMomentMatched, 192, seed: 99, threads: 3, useResources: true, withLabels: false);
            SpikeSimulation.Result otherSeed =
                SpikeSimulation.Run(plan, SpikeDistribution.BetaPertMomentMatched, 192, seed: 100, threads: 1, useResources: true, withLabels: false);

            three.Finishes.ShouldBe(one.Finishes);
            otherSeed.Finishes.ShouldNotBe(one.Finishes);
            one.Finishes.Distinct().Count().ShouldBeGreaterThan(5);
        }

        #endregion

        #region Measurements

        // Runs body until it has run for at least the given time, so that the just-in-time
        // compiler has promoted the code that matters. Without this, a plan that is cheap to
        // schedule is timed while its code is still the slow first version, and the figures
        // for it come out several times too large. (The first version of these measurements
        // had that fault, and a speed-up of four for two threads showed it.)
        private static void WarmUp(Action<int[]> body, List<int[]> vectors, double seconds = 1.5)
        {
            long start = Stopwatch.GetTimestamp();
            int i = 0;
            while (i < 10 || (Stopwatch.GetTimestamp() - start) < seconds * Stopwatch.Frequency)
            {
                body(vectors[i++ % vectors.Count]);
            }
        }

        // The mean time of one call of body, in milliseconds: the best of three rounds, after
        // a warm-up, because noise on a machine only ever adds time.
        private static double BestMillisecondsPerIteration(Action<int[]> body, List<int[]> vectors, int iterations)
        {
            WarmUp(body, vectors);

            double best = double.MaxValue;
            for (int round = 0; round < 3; round++)
            {
                long start = Stopwatch.GetTimestamp();
                for (int i = 0; i < iterations; i++)
                {
                    body(vectors[i % vectors.Count]);
                }
                double milliseconds = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency / iterations;
                best = Math.Min(best, milliseconds);
            }
            return best;
        }

        private static IEnumerable<SpikePlan> MeasuredPlans()
        {
            yield return SpikePlan.LoadSample();
            yield return SpikePlan.Layered(100, layers: 4, resourceCount: 10);
            yield return SpikePlan.Layered(250, layers: 10, resourceCount: 20);
            yield return SpikePlan.Layered(500, layers: 20, resourceCount: 20);
            yield return SpikePlan.Layered(1_000, layers: 40, resourceCount: 20);
        }

        [Fact(Explicit = true)]
        public void MeasureIterationCost()
        {
            m_Output.WriteLine(@"The cost of one iteration that schedules the resources again, on one thread.");
            m_Output.WriteLine(@"Best of three rounds after a warm-up. Durations are drawn from estimates of 0.75 and 1.6 times the duration.");
            m_Output.WriteLine(@"'new compile' = a new compiler and a compile; 'reused compile' = a compile on the same compiler; 'lean' = the four steps of the lean path.");
            m_Output.WriteLine(@"");
            m_Output.WriteLine(
                $@"{"plan",-12} {"acts",5} {"res",4} {"new compile",12} {"reused",9} {"lean",8} {"lean/new",9} {"10,000 lean",12}");

            var profiles = new List<string>();
            foreach (SpikePlan plan in MeasuredPlans())
            {
                List<int[]> vectors = DrawVectors(plan, SpikeDistribution.BetaPertMomentMatched, 24, seed: 5);
                var critical = new bool[plan.Activities.Count];

                var lean = new SpikeLeanScheduler(plan, useResources: true);
                var reused = new SpikeLeanScheduler(plan, useResources: true);

                // A warmed-up timing run first, to choose how many iterations make a round of
                // about 700 ms.
                WarmUp(v => lean.Run(v, critical, CancellationToken.None), vectors);
                long probe = Stopwatch.GetTimestamp();
                for (int i = 0; i < 8; i++)
                {
                    lean.Run(vectors[i % vectors.Count], critical, CancellationToken.None);
                }
                double probeMilliseconds = (Stopwatch.GetTimestamp() - probe) * 1000.0 / Stopwatch.Frequency / 8;
                int iterations = Math.Clamp((int)(700.0 / Math.Max(0.05, probeMilliseconds)), 4, 2000);

                double newCompile = BestMillisecondsPerIteration(v => plan.CompileOracle(v, useResources: true), vectors, iterations);
                double reusedCompile = BestMillisecondsPerIteration(v => reused.RunFullCompileOnTheSameCompiler(v, CancellationToken.None), vectors, iterations);
                double leanMilliseconds = BestMillisecondsPerIteration(v => lean.Run(v, critical, CancellationToken.None), vectors, iterations);

                m_Output.WriteLine(
                    $@"{plan.Name,-12} {plan.Activities.Count,5} {plan.Resources.Count,4} {newCompile.ToString("F2") + " ms",12} {reusedCompile.ToString("F2") + " ms",9} {leanMilliseconds.ToString("F2") + " ms",8} {(leanMilliseconds / newCompile).ToString("P0"),9} {(leanMilliseconds * 10_000 / 1000.0).ToString("F0") + " s",12}");

                // The profile: the same iterations again, with the stopwatch on each part.
                var profiled = new SpikeLeanScheduler(plan, useResources: true)
                {
                    Timing = new SpikeLeanScheduler.Split(),
                    ProfileInsideSchedule = true,
                };
                WarmUp(v => profiled.Run(v, critical, CancellationToken.None), vectors, seconds: 1.0);
                profiled.Timing = new SpikeLeanScheduler.Split();
                for (int i = 0; i < iterations; i++)
                {
                    profiled.Run(vectors[i % vectors.Count], critical, CancellationToken.None);
                }
                SpikeLeanScheduler.Split s = profiled.Timing;
                double clone = s.PerIterationMilliseconds(SpikeLeanScheduler.Split.Clone);
                double priorityWithClone = s.PerIterationMilliseconds(SpikeLeanScheduler.Split.PriorityListWithClone);
                double schedule = s.PerIterationMilliseconds(SpikeLeanScheduler.Split.Schedule);
                double priorityCore = Math.Max(0.0, priorityWithClone - clone);
                double engine = Math.Max(0.0, schedule - (2.0 * clone) - priorityCore);
                double total = s.PerIterationMilliseconds(SpikeLeanScheduler.Split.Reset)
                    + s.PerIterationMilliseconds(SpikeLeanScheduler.Split.FirstPass)
                    + schedule
                    + s.PerIterationMilliseconds(SpikeLeanScheduler.Split.Assign)
                    + s.PerIterationMilliseconds(SpikeLeanScheduler.Split.SecondPass)
                    + s.PerIterationMilliseconds(SpikeLeanScheduler.Split.Rest);
                string Pct(double x) => (100.0 * x / total).ToString("F0") + "%";
                profiles.Add(
                    $@"{plan.Name,-12} reset {Pct(s.PerIterationMilliseconds(SpikeLeanScheduler.Split.Reset)),4} | CPM 1 {Pct(s.PerIterationMilliseconds(SpikeLeanScheduler.Split.FirstPass)),4} | schedule {Pct(schedule),4} (2 clones {Pct(2.0 * clone),4}, priority list {Pct(priorityCore),4}, tick loop and the rest {Pct(engine),4}) | assign {Pct(s.PerIterationMilliseconds(SpikeLeanScheduler.Split.Assign)),4} | CPM 2 {Pct(s.PerIterationMilliseconds(SpikeLeanScheduler.Split.SecondPass)),4} | rest {Pct(s.PerIterationMilliseconds(SpikeLeanScheduler.Split.Rest)),4}   [{total:F2} ms]");
            }

            m_Output.WriteLine(@"");
            m_Output.WriteLine(@"Where an iteration of the lean path spends its time (a profile, with the clone and the priority list timed on the side):");
            foreach (string line in profiles)
            {
                m_Output.WriteLine(line);
            }
        }

        [Fact(Explicit = true)]
        public void MeasureParallelThroughput()
        {
            m_Output.WriteLine(@"Iterations per second of the whole simulation (lean path, resources scheduled again) for 1 to 4 threads.");
            m_Output.WriteLine($@"Logical processors on this machine: {Environment.ProcessorCount}.");
            m_Output.WriteLine(@"");
            m_Output.WriteLine($@"{"plan",-12} {"threads",8} {"iterations",11} {"seconds",9} {"iter/s",9} {"speed-up",9}");

            foreach (SpikePlan plan in new[] { SpikePlan.LoadSample(), SpikePlan.Layered(250, layers: 10, resourceCount: 20) })
            {
                // About three seconds on one thread, once the code is warm.
                var critical = new bool[plan.Activities.Count];
                List<int[]> vectors = DrawVectors(plan, SpikeDistribution.BetaPertMomentMatched, 8, seed: 5);
                var probe = new SpikeLeanScheduler(plan, useResources: true);
                WarmUp(v => probe.Run(v, critical, CancellationToken.None), vectors, seconds: 2.0);
                long start = Stopwatch.GetTimestamp();
                for (int i = 0; i < 20; i++)
                {
                    probe.Run(vectors[i % vectors.Count], critical, CancellationToken.None);
                }
                double milliseconds = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency / 20;
                int iterations = Math.Max(256, (int)(3000.0 / milliseconds) / 64 * 64);

                // Warm the whole path, threads included, before any figure is taken.
                SpikeSimulation.Run(plan, SpikeDistribution.BetaPertMomentMatched, 512, seed: 1, threads: 4, useResources: true, withLabels: false);

                double single = 0;
                foreach (int threads in new[] { 1, 2, 3, 4 })
                {
                    double seconds = double.MaxValue;
                    for (int repeat = 0; repeat < 2; repeat++)
                    {
                        seconds = Math.Min(
                            seconds,
                            SpikeSimulation.Run(
                                plan, SpikeDistribution.BetaPertMomentMatched, iterations, seed: 77, threads, useResources: true, withLabels: false).Seconds);
                    }
                    double rate = iterations / seconds;
                    single = threads == 1 ? rate : single;
                    m_Output.WriteLine(
                        $@"{plan.Name,-12} {threads,8} {iterations,11} {seconds,9:F2} {rate,9:F0} {rate / single,9:F2}");
                }
            }
        }

        [Fact(Explicit = true)]
        public void MeasureTheReferenceSampleInWholeDays()
        {
            // The same sample as the reference tool, with the simulation of the application:
            // whole days, and the scheduler of the library with no resources. The first row
            // of each group is the reference tool's own arithmetic, in hundredths of a day.
            SpikePlan plan = SpikePlan.ReferenceSample();
            const int iterations = 100_000;

            m_Output.WriteLine($@"{iterations:N0} iterations ({iterations / 3:N0} for 'compile').");
            m_Output.WriteLine(@"'ref' = the reference tool's arithmetic, in hundredths of a day.");
            m_Output.WriteLine(@"'days' = whole days, critical path of the dependencies only (the shortcut of the lean path).");
            m_Output.WriteLine(@"'compile' = whole days, critical activities as a full compile marks them (synthetic resources included).");
            m_Output.WriteLine($@"{"distribution",-24} {"mode",8} {"mean",7} {"sd",6} {"min",6} {"max",6} {"P50",6} {"P80",6} {"P90",6}  paths (top three and multiple)");

            foreach (SpikeDistribution distribution in Enum.GetValues(typeof(SpikeDistribution)))
            {
                Summary reference = RunLikeTheReferenceTool(plan, distribution, iterations, seed: 2026);
                WriteSummary(distribution.ToString(), "ref", reference);

                SpikeSimulation.Result days = SpikeSimulation.Run(
                    plan, distribution, iterations, seed: 2026, threads: 2, useResources: false, withLabels: true);
                WriteSummary(string.Empty, "days", Summarise(days.Finishes.Select(x => (double)x).ToList(), days.Labels));

                SpikeSimulation.Result compile = SpikeSimulation.Run(
                    plan, distribution, iterations / 3, seed: 2026, threads: 2, useResources: false, withLabels: true, criticalPathOnlyWithoutResources: false);
                WriteSummary(string.Empty, "compile", Summarise(compile.Finishes.Select(x => (double)x).ToList(), compile.Labels));
            }
        }

        [Fact(Explicit = true)]
        public void MeasurePriorityListApproximations()
        {
            // What it costs in accuracy to find the priority list in a cheaper way, and what it
            // saves in time. The exact list is the library's calculation. The two others are
            // approximations (refer to SpikePriorityList). All three use the same random
            // durations, so each difference is a difference of the list and nothing else.
            //
            // The time of the frozen list is also the best that any faster but exact
            // calculation of the list could do: that calculation cannot cost less than nothing.
            m_Output.WriteLine(@"Same durations in every mode. 'differ' = the share of the iterations whose finish time differs from the exact mode.");
            m_Output.WriteLine(
                $@"{"plan",-12} {"mode",-10} {"failed",7} {"mean",8} {"sd",7} {"P50",6} {"P80",6} {"P90",6} {"differ",8} {"mean |diff|",12} {"mean diff",10} {"ms/iter",9} {"speed-up",9}");

            foreach ((SpikePlan plan, int iterations, int timedIterations) in new[]
            {
                (SpikePlan.LoadSample(), 3_072, 300),
                (SpikePlan.Layered(100, layers: 4, resourceCount: 10), 2_048, 200),
                (SpikePlan.Layered(250, layers: 10, resourceCount: 20), 1_024, 40),
            })
            {
                const ulong seed = 41;
                SpikeSimulation.Result exact = SpikeSimulation.Run(
                    plan, SpikeDistribution.BetaPertMomentMatched, iterations, seed, threads: 2, useResources: true, withLabels: false);

                double exactMilliseconds = 0;
                foreach ((SpikePriorityList mode, string name) in new[]
                {
                    (SpikePriorityList.Exact, "exact"),
                    (SpikePriorityList.FrozenFromTheMostLikelyDurations, "frozen"),
                    (SpikePriorityList.StaticSlackOrder, "static"),
                })
                {
                    SpikeSimulation.Result result = mode == SpikePriorityList.Exact
                        ? exact
                        : SpikeSimulation.Run(
                            plan, SpikeDistribution.BetaPertMomentMatched, iterations, seed, threads: 2, useResources: true, withLabels: false, priorityList: mode);

                    int[] a = exact.Finishes;
                    int[] b = result.Finishes;
                    int ok = Enumerable.Range(0, iterations).Count(i => b[i] >= 0);
                    int differ = Enumerable.Range(0, iterations).Count(i => b[i] >= 0 && a[i] != b[i]);
                    double meanAbsolute = Enumerable.Range(0, iterations).Where(i => b[i] >= 0).Average(i => Math.Abs(b[i] - a[i]));
                    double meanSigned = Enumerable.Range(0, iterations).Where(i => b[i] >= 0).Average(i => b[i] - a[i]);
                    List<double> finishes = b.Where(x => x >= 0).Select(x => (double)x).ToList();
                    Summary s = Summarise(finishes, finishes.Select(x => string.Empty).ToList());

                    // The time, on one thread and warmed up.
                    var timed = new SpikeLeanScheduler(plan, useResources: true) { PriorityListMode = mode };
                    var critical = new bool[plan.Activities.Count];
                    List<int[]> vectors = DrawVectors(plan, SpikeDistribution.BetaPertMomentMatched, 24, seed: 5);
                    double milliseconds = BestMillisecondsPerIteration(v => timed.Run(v, critical, CancellationToken.None), vectors, timedIterations);
                    exactMilliseconds = mode == SpikePriorityList.Exact ? milliseconds : exactMilliseconds;

                    m_Output.WriteLine(
                        $@"{plan.Name,-12} {name,-10} {result.Failed,7} {s.Mean,8:F2} {s.StandardDeviation,7:F2} {s.P50,6:F0} {s.P80,6:F0} {s.P90,6:F0} {(100.0 * differ / ok).ToString("F1") + "%",8} {meanAbsolute,12:F2} {meanSigned,10:F2} {milliseconds,9:F2} {(exactMilliseconds / milliseconds).ToString("F2") + "x",9}");
                }
            }
        }

        [Fact(Explicit = true)]
        public void MeasureFrozenResourceOrderAgainstSchedulingAgain()
        {
            // The alternative to scheduling the resources again in each iteration: keep the
            // resource order that the compile of the plan itself found, as extra dependencies,
            // and calculate only the critical path. It costs microseconds and not
            // milliseconds. This measures what it costs in accuracy, on the same draws.
            m_Output.WriteLine(@"Same durations in both. 'again' = the resources are scheduled again in each iteration; 'frozen' = the resource order of the compiled plan.");
            m_Output.WriteLine(
                $@"{"plan",-12} {"mode",7} {"iterations",11} {"mean",8} {"sd",7} {"P50",7} {"P80",7} {"P90",7} {"us/iter",9}   {"iterations that differ",-24} {"mean |difference|",-18}");

            foreach ((SpikePlan plan, int iterations) in new[] { (SpikePlan.LoadSample(), 3_072), (SpikePlan.Layered(250, layers: 10, resourceCount: 20), 1_024) })
            {
                const ulong seed = 31;
                SpikeSimulation.Result again = SpikeSimulation.Run(
                    plan, SpikeDistribution.BetaPertMomentMatched, iterations, seed, threads: 2, useResources: true, withLabels: false);

                // The resource order of the plan itself: one compile with the most likely
                // durations, then the dependencies it holds, resource dependencies included.
                var baseCompile = new SpikeLeanScheduler(plan, useResources: true);
                var critical = new bool[plan.Activities.Count];
                baseCompile.Run(plan.MostLikelyDurations(), critical, CancellationToken.None);
                Dictionary<int, int> indexById = plan.Activities.Select((x, i) => (x.Id, i)).ToDictionary(x => x.Id, x => x.i);
                var frozenCpm = new SpikeCriticalPaths(baseCompile.PredecessorIndexes(indexById));

                var frozenFinishes = new int[iterations];
                var durations = new int[plan.Activities.Count];
                for (int warm = 0; warm < 20_000; warm++)
                {
                    frozenCpm.Calculate(plan.MostLikelyDurations(), critical);
                }
                long start = Stopwatch.GetTimestamp();
                int chunks = (iterations + SpikeSimulation.ChunkSize - 1) / SpikeSimulation.ChunkSize;
                long drawTicks = 0;
                for (int chunk = 0; chunk < chunks; chunk++)
                {
                    SpikeRandom random = SpikeSimulation.ChunkRandom(seed, chunk);
                    for (int i = chunk * SpikeSimulation.ChunkSize; i < Math.Min(iterations, (chunk + 1) * SpikeSimulation.ChunkSize); i++)
                    {
                        long d0 = Stopwatch.GetTimestamp();
                        SpikeSimulation.DrawDurations(plan, SpikeDistribution.BetaPertMomentMatched, random, durations);
                        drawTicks += Stopwatch.GetTimestamp() - d0;
                        frozenFinishes[i] = frozenCpm.Calculate(durations, critical);
                    }
                }
                double frozenMicroseconds = (Stopwatch.GetTimestamp() - start - drawTicks) * 1_000_000.0 / Stopwatch.Frequency / iterations;

                int differ = Enumerable.Range(0, iterations).Count(i => again.Finishes[i] != frozenFinishes[i]);
                double meanAbsolute = Enumerable.Range(0, iterations).Average(i => Math.Abs(again.Finishes[i] - frozenFinishes[i]));

                Summary a = Summarise(again.Finishes.Select(x => (double)x).ToList(), again.Finishes.Select(x => string.Empty).ToList());
                Summary f = Summarise(frozenFinishes.Select(x => (double)x).ToList(), frozenFinishes.Select(x => string.Empty).ToList());
                m_Output.WriteLine(
                    $@"{plan.Name,-12} {"again",7} {iterations,11} {a.Mean,8:F2} {a.StandardDeviation,7:F2} {a.P50,7:F0} {a.P80,7:F0} {a.P90,7:F0} {"-",9}   {differ + " (" + (100.0 * differ / iterations).ToString("F1") + "%)",-24} {meanAbsolute,-18:F2}");
                m_Output.WriteLine(
                    $@"{plan.Name,-12} {"frozen",7} {iterations,11} {f.Mean,8:F2} {f.StandardDeviation,7:F2} {f.P50,7:F0} {f.P80,7:F0} {f.P90,7:F0} {frozenMicroseconds,9:F1}");
            }
        }

        private void WriteSummary(string name, string mode, Summary s)
        {
            IEnumerable<(string Path, int Count)> shown = s.Paths.Take(3);
            if (!shown.Any(x => x.Path == "Multiple"))
            {
                shown = shown.Concat(s.Paths.Where(x => x.Path == "Multiple"));
            }
            string paths = string.Join(", ", shown.Select(x => $@"{x.Path} {100.0 * x.Count / s.Count:F1}%"));
            m_Output.WriteLine(
                $@"{name,-24} {mode,8} {s.Mean,7:F2} {s.StandardDeviation,6:F2} {s.Minimum,6:F1} {s.Maximum,6:F1} {s.P50,6:F1} {s.P80,6:F1} {s.P90,6:F1}  {paths}");
        }

        #endregion
    }
}
