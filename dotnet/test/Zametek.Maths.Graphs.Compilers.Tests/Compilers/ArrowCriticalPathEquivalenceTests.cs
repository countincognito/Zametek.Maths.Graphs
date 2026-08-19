using Shouldly;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    // The safety net for any change to the arrow critical-path engine.
    //
    // The engine computes the earliest and latest finish times of every event, and from
    // those the start, finish and slack values of every activity. Those values decide what
    // the compiled arrow graph reports as critical, so a faster calculation that produces
    // different numbers is a behaviour change, not an optimisation. These tests pin the
    // current output exactly, over the same corpus the vertex side uses.
    //
    // If a change makes one of these fail, the change is not equivalent. Regenerate the
    // baseline only when the output is *intended* to change, using the explicit
    // RegenerateBaseline test below.
    public class ArrowCriticalPathEquivalenceTests
    {
        private static readonly string s_BaselineRelativePath =
            Path.Combine(@"Compilers", @"TestFiles", @"ArrowCriticalPathBaseline.txt");

        private readonly ITestOutputHelper m_Output;

        public ArrowCriticalPathEquivalenceTests(ITestOutputHelper output)
        {
            m_Output = output;
        }

        #region Helpers

        // Wraps the real engine and accumulates the time its three passes spend, so the
        // measurement below can separate the critical path from the redundant-edge removal
        // that CalculateCriticalPath performs first. Injected through the ordinary engines
        // bundle - no production code is aware of it.
        private sealed class TimingCriticalPathEngine
            : IArrowCriticalPathEngine<int, int, int, IActivity<int, int, int>>
        {
            private readonly IArrowCriticalPathEngine<int, int, int, IActivity<int, int, int>> m_Inner =
                new ArrowCriticalPathEngine<int, int, int, IActivity<int, int, int>>();

            internal Stopwatch Elapsed { get; } = new Stopwatch();

            public bool CalculateEventEarliestFinishTimes(
                IArrowGraphState<int, int, int, IActivity<int, int, int>> state,
                List<IInvalidConstraint<int>> invalidConstraints,
                bool shuffle)
            {
                Elapsed.Start();
                try
                {
                    return m_Inner.CalculateEventEarliestFinishTimes(state, invalidConstraints, shuffle);
                }
                finally
                {
                    Elapsed.Stop();
                }
            }

            public bool CalculateEventLatestFinishTimes(
                IArrowGraphState<int, int, int, IActivity<int, int, int>> state,
                List<IInvalidConstraint<int>> invalidConstraints,
                bool shuffle)
            {
                Elapsed.Start();
                try
                {
                    return m_Inner.CalculateEventLatestFinishTimes(state, invalidConstraints, shuffle);
                }
                finally
                {
                    Elapsed.Stop();
                }
            }

            public bool CalculateCriticalPathVariables(
                IArrowGraphState<int, int, int, IActivity<int, int, int>> state,
                List<IInvalidConstraint<int>> invalidConstraints)
            {
                Elapsed.Start();
                try
                {
                    return m_Inner.CalculateCriticalPathVariables(state, invalidConstraints);
                }
                finally
                {
                    Elapsed.Stop();
                }
            }
        }

        // Renders every value the engine is responsible for: each event's earliest and
        // latest finish time, then each activity's earliest start, latest finish and free
        // slack. Ordered by ID so the line is stable regardless of iteration order.
        private static string FormatCase(ArrowCriticalPathCorpus.Case testCase)
        {
            testCase.GraphBuilder.CalculateCriticalPath();

            var output = new StringBuilder();
            output.Append(testCase.Name);
            output.Append(@": events ");
            output.Append(string.Join(@",", testCase.GraphBuilder.Events
                .OrderBy(x => x.Id)
                .Select(x => $@"{x.Id}={Show(x.EarliestFinishTime)}/{Show(x.LatestFinishTime)}")));
            output.Append(@" activities ");
            output.Append(string.Join(@",", testCase.GraphBuilder.Activities
                .OrderBy(x => x.Id)
                .Select(x => $@"{x.Id}={Show(x.EarliestStartTime)}/{Show(x.LatestFinishTime)}/{Show(x.FreeSlack)}")));
            return output.ToString();
        }

        private static string Show(int? value) => value.HasValue ? value.GetValueOrDefault().ToString() : @"-";

        private static List<string> BuildCurrentLines()
        {
            return ArrowCriticalPathCorpus.Generate().Select(FormatCase).ToList();
        }

        private static List<string> ReadBaselineLines()
        {
            string path = Path.Combine(AppContext.BaseDirectory, s_BaselineRelativePath);
            File.Exists(path).ShouldBeTrue(
                $@"The arrow critical-path baseline is missing from the test output at {path}. Run the RegenerateBaseline test to create it.");
            return File.ReadAllLines(path).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        }

        private static string FindSourceBaselinePath()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && directory.GetFiles(@"*.csproj").Length == 0)
            {
                directory = directory.Parent;
            }
            directory.ShouldNotBeNull(@"Could not locate the test project directory from the test output directory.");
            return Path.Combine(directory!.FullName, s_BaselineRelativePath);
        }

        #endregion

        [Fact]
        public void ArrowCriticalPath_GivenCorpus_ThenMatchesCommittedBaseline()
        {
            List<string> expected = ReadBaselineLines();
            List<string> actual = BuildCurrentLines();

            // Compare case by case so a failure names the shape that diverged rather than
            // just reporting that two large lists differ.
            actual.Count.ShouldBe(expected.Count,
                @"The corpus has changed size; the baseline needs regenerating deliberately.");

            var divergences = new List<string>();
            for (int i = 0; i < expected.Count; i++)
            {
                if (!string.Equals(expected[i], actual[i], StringComparison.Ordinal))
                {
                    divergences.Add($@"expected: {expected[i]}{Environment.NewLine}  actual: {actual[i]}");
                }
            }

            divergences.ShouldBeEmpty(
                $@"The arrow critical path changed for {divergences.Count} of {expected.Count} corpus cases:{Environment.NewLine}{string.Join(Environment.NewLine, divergences.Take(3))}");
        }

        [Fact]
        public void ArrowCriticalPath_GivenCorpus_ThenIsIndependentOfProcessingOrder()
        {
            // ShuffleProcessingOrder randomises the order the engine walks its work lists.
            // The computed values must not depend on it - if they do, any rewrite of that
            // engine is free to change the results silently.
            //
            // Each variant gets its own builder, and deliberately so: CalculateCriticalPath
            // begins by removing redundant edges, which is a structural change, so calling
            // it twice on one builder is not the same as calling it twice on the same
            // graph. (Measured: on fan-in-5 a second call with no shuffle at all moves a
            // dummy activity's free slack from 2 to 1.) The corpus is deterministic, so two
            // generations give identical graphs to compare.
            List<ArrowCriticalPathCorpus.Case> orderedCases = ArrowCriticalPathCorpus.Generate();
            List<ArrowCriticalPathCorpus.Case> shuffledCases = ArrowCriticalPathCorpus.Generate();

            for (int i = 0; i < orderedCases.Count; i++)
            {
                string ordered = FormatCase(orderedCases[i]);

                shuffledCases[i].GraphBuilder.ShuffleProcessingOrder = true;
                string shuffled = FormatCase(shuffledCases[i]);

                shuffled.ShouldBe(ordered, $@"case {orderedCases[i].Name}");
            }
        }

        [Fact]
        public void ArrowCriticalPath_GivenCorpus_ThenEveryEventAndActivityHasValues()
        {
            // An invariant the calculation must preserve regardless of traversal: it
            // reaches everything. A walk that silently skipped part of the graph would
            // leave values unset rather than wrong, which a value comparison alone could
            // miss if the baseline were ever regenerated against it.
            foreach (ArrowCriticalPathCorpus.Case testCase in ArrowCriticalPathCorpus.Generate())
            {
                testCase.GraphBuilder.CalculateCriticalPath();

                testCase.GraphBuilder.Events.ShouldAllBe(x => x.EarliestFinishTime.HasValue, $@"case {testCase.Name}");
                testCase.GraphBuilder.Events.ShouldAllBe(x => x.LatestFinishTime.HasValue, $@"case {testCase.Name}");
                testCase.GraphBuilder.Activities.ShouldAllBe(x => x.EarliestStartTime.HasValue, $@"case {testCase.Name}");
                testCase.GraphBuilder.Activities.ShouldAllBe(x => x.LatestFinishTime.HasValue, $@"case {testCase.Name}");
                testCase.GraphBuilder.Activities.ShouldAllBe(x => x.FreeSlack.HasValue, $@"case {testCase.Name}");
            }
        }

        [Fact(Explicit = true)]
        public void RegenerateBaseline()
        {
            // Explicit: run only when the arrow critical-path output is intended to change.
            //   dotnet test --filter "FullyQualifiedName~ArrowCriticalPathEquivalenceTests.RegenerateBaseline" -- xUnit.ExplicitTests=on
            string path = FindSourceBaselinePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllLines(path, BuildCurrentLines());
            File.Exists(path).ShouldBeTrue();
        }

        [Fact(Explicit = true)]
        public void MeasureArrowCriticalPathScaling()
        {
            // Explicit, because it takes far longer than a normal test. Build in Release
            // first, then run the xUnit v3 assembly directly:
            //   .\Zametek.Maths.Graphs.Compilers.Tests.exe -explicit only -method "*MeasureArrowCriticalPathScaling*" -showLiveOutput
            // Sizes are far smaller than the vertex measurement uses, because an arrow
            // graph carries a dummy edge per dependency and one shape below is
            // pathological for the redundant-edge removal.
            //
            // Each row is written as it completes rather than buffered into one block at
            // the end, so a long run visibly makes progress instead of looking hung.
            m_Output.WriteLine(@"CalculateCriticalPath, split into the critical-path passes and the rest");
            m_Output.WriteLine(@"(the rest is dominated by RemoveRedundantEdges, which this engine does not touch):");
            m_Output.WriteLine(@"");
            m_Output.WriteLine(@"Scaling with activity count (12 layers):");
            m_Output.WriteLine($@"{"activities",12} {"total",12} {"cpm passes",12} {"the rest",12} {"allocated",14}");
            foreach (int size in new[] { 25, 50, 100, 150 })
            {
                WriteRow(size.ToString(), MeasureOne(size, layers: 12));
            }

            m_Output.WriteLine(@"");
            m_Output.WriteLine(@"Scaling with depth (150 activities):");
            m_Output.WriteLine($@"{"layers",12} {"total",12} {"cpm passes",12} {"the rest",12} {"allocated",14}");
            foreach (int layers in new[] { 10, 30, 60, 120 })
            {
                WriteRow(layers.ToString(), MeasureOne(150, layers));
            }
        }

        private void WriteRow(string label, (long totalMs, long cpmMs, long allocatedMb) measurement)
        {
            m_Output.WriteLine($@"{label,12} {measurement.totalMs + "ms",12} {measurement.cpmMs + "ms",12} {(measurement.totalMs - measurement.cpmMs) + "ms",12} {measurement.allocatedMb + " MB",14}");
        }

        private static (long totalMilliseconds, long cpmMilliseconds, long allocatedMb) MeasureOne(int size, int layers)
        {
            var timingEngine = new TimingCriticalPathEngine();
            var graphBuilder = new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(
                new ArrowGraphBuilderEngines<int, int, int, IActivity<int, int, int>>
                {
                    EdgeIdGenerator = new NextIdGenerator<int>(1_000_000),
                    NodeIdGenerator = new NextIdGenerator<int>(0),
                    CriticalPathEngine = timingEngine,
                });
            int perLayer = Math.Max(1, size / layers);
            for (int id = 1; id <= size; id++)
            {
                int layer = (id - 1) / perLayer;
                var dependencies = new HashSet<int>();
                if (layer > 0)
                {
                    int previousStart = ((layer - 1) * perLayer) + 1;
                    int previousEnd = Math.Min(layer * perLayer, size);
                    if (previousEnd >= previousStart)
                    {
                        dependencies.Add(previousStart + ((id * 7) % (previousEnd - previousStart + 1)));
                        dependencies.Add(previousStart + ((id * 13) % (previousEnd - previousStart + 1)));
                    }
                }
                graphBuilder.AddActivity(new Activity<int, int, int>(id, 1 + (id % 9)), dependencies);
            }
            graphBuilder.TransitiveReduction();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long allocatedBefore = GC.GetTotalAllocatedBytes(precise: false);
            timingEngine.Elapsed.Reset();
            var stopwatch = Stopwatch.StartNew();
            graphBuilder.CalculateCriticalPath();
            stopwatch.Stop();
            long allocatedAfter = GC.GetTotalAllocatedBytes(precise: false);

            return (
                stopwatch.ElapsedMilliseconds,
                timingEngine.Elapsed.ElapsedMilliseconds,
                (allocatedAfter - allocatedBefore) / (1024 * 1024));
        }
    }
}
