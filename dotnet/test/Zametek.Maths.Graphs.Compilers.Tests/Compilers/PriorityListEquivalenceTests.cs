using Shouldly;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    // The safety net for any future change to the priority-list calculation or to the
    // critical-path engine beneath it.
    //
    // The priority list decides the order in which activities are offered resources, so
    // it determines the final schedule. A faster calculation that produces a different
    // list is a behaviour change, not an optimisation - and one that would be easy to
    // miss, because most graphs still compile successfully afterwards, just to a
    // different schedule. These tests pin the current output exactly, over a corpus
    // chosen to stress depth, width, density and (above all) ties.
    //
    // If a change makes one of these fail, the change is not equivalent. Regenerate the
    // baseline only when the output is *intended* to change, using the explicit
    // RegenerateBaseline test below.
    public class PriorityListEquivalenceTests
    {
        private static readonly string s_BaselineRelativePath =
            Path.Combine(@"Compilers", @"TestFiles", @"PriorityListBaseline.txt");

        private readonly ITestOutputHelper m_Output;

        public PriorityListEquivalenceTests(ITestOutputHelper output)
        {
            m_Output = output;
        }

        #region Helpers

        private static string FormatCase(PriorityListCorpus.Case testCase)
        {
            List<int> priorityList = testCase.GraphBuilder.CalculateCriticalPathPriorityList();
            return $@"{testCase.Name}: {string.Join(@",", priorityList)}";
        }

        private static List<string> BuildCurrentLines()
        {
            return PriorityListCorpus.Generate().Select(FormatCase).ToList();
        }

        private static List<string> ReadBaselineLines()
        {
            string path = Path.Combine(AppContext.BaseDirectory, s_BaselineRelativePath);
            File.Exists(path).ShouldBeTrue(
                $@"The priority-list baseline is missing from the test output at {path}. Run the RegenerateBaseline test to create it.");
            return File.ReadAllLines(path).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        }

        // Walks up from the test output directory to the test project directory, so the
        // regeneration test can write the baseline back into source control rather than
        // into bin.
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
        public void PriorityList_GivenCorpus_ThenMatchesCommittedBaseline()
        {
            List<string> expected = ReadBaselineLines();
            List<string> actual = BuildCurrentLines();

            // Compare case by case so a failure names the shape that diverged rather
            // than just reporting that two large lists differ.
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
                $@"The priority list changed for {divergences.Count} of {expected.Count} corpus cases:{Environment.NewLine}{string.Join(Environment.NewLine, divergences.Take(5))}");
        }

        [Fact]
        public void PriorityList_GivenCorpus_ThenIsIndependentOfProcessingOrder()
        {
            // ShuffleProcessingOrder randomises the order the critical-path engine walks
            // its work lists. The priority list must not depend on it - if it does, any
            // rewrite of that engine is free to change the schedule silently.
            foreach (PriorityListCorpus.Case testCase in PriorityListCorpus.Generate())
            {
                List<int> ordered = testCase.GraphBuilder.CalculateCriticalPathPriorityList();

                testCase.GraphBuilder.ShuffleProcessingOrder = true;
                List<int> shuffled = testCase.GraphBuilder.CalculateCriticalPathPriorityList();
                testCase.GraphBuilder.ShuffleProcessingOrder = false;

                shuffled.ShouldBe(ordered, $@"case {testCase.Name}");
            }
        }

        [Fact]
        public void PriorityList_GivenCorpus_ThenEveryNonDummyActivityAppearsExactlyOnce()
        {
            // An invariant the calculation must preserve regardless of ordering: every
            // activity with a non-zero duration is scheduled, and none is scheduled twice.
            foreach (PriorityListCorpus.Case testCase in PriorityListCorpus.Generate())
            {
                List<int> expected = testCase.GraphBuilder.Activities
                    .Where(x => !x.IsDummy)
                    .Select(x => x.Id)
                    .OrderBy(x => x)
                    .ToList();

                List<int> priorityList = testCase.GraphBuilder.CalculateCriticalPathPriorityList();

                priorityList.Count.ShouldBe(priorityList.Distinct().Count(), $@"case {testCase.Name} has duplicates");
                priorityList.OrderBy(x => x).ToList().ShouldBe(expected, $@"case {testCase.Name}");
            }
        }

        [Fact(Explicit = true)]
        public void RegenerateBaseline()
        {
            // Explicit: run only when the priority-list output is intended to change.
            //   dotnet test --filter "FullyQualifiedName~RegenerateBaseline" -- xUnit.ExplicitTests=on
            string path = FindSourceBaselinePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllLines(path, BuildCurrentLines());
            File.Exists(path).ShouldBeTrue();
        }

        [Fact(Explicit = true)]
        public void MeasurePriorityListScaling()
        {
            // Explicit, because it takes far longer than a normal test. The xUnit v3 test
            // assembly is directly executable, which is the reliable way to run an
            // explicit test and see its output; build in Release first, then:
            //   .\Zametek.Maths.Graphs.Compilers.Tests.exe -explicit only -method "*MeasurePriorityListScaling*" -showLiveOutput
            // Compare the result against the tables in docs/TODO.md.
            //
            // Each row is written as it completes rather than buffered into one block at
            // the end, so a long run visibly makes progress instead of looking hung.
            m_Output.WriteLine(@"Scaling with activity count (12 layers):");
            m_Output.WriteLine($@"{"activities",12} {"time",12} {"allocated",14}");
            foreach (int size in new[] { 250, 500, 1_000, 2_000, 4_000, 8_000 })
            {
                (long milliseconds, long allocatedMb) = MeasureOne(size, layers: 12);
                m_Output.WriteLine($@"{size,12} {milliseconds + "ms",12} {allocatedMb + " MB",14}");
            }

            m_Output.WriteLine(@"");
            m_Output.WriteLine(@"Scaling with depth (1,500 activities):");
            m_Output.WriteLine($@"{"layers",12} {"time",12} {"allocated",14}");
            foreach (int layers in new[] { 10, 30, 60, 120, 240 })
            {
                (long milliseconds, long allocatedMb) = MeasureOne(1_500, layers);
                m_Output.WriteLine($@"{layers,12} {milliseconds + "ms",12} {allocatedMb + " MB",14}");
            }

            m_Output.WriteLine(@"");
            m_Output.WriteLine(@"On the shape the investigation started from, where depth grows with size:");
            m_Output.WriteLine($@"{"activities",12} {"layers",12} {"time",12} {"allocated",14}");
            foreach (int size in new[] { 1_000, 2_000, 4_000, 8_000 })
            {
                (long milliseconds, long allocatedMb) = MeasureOne(size, layers: size / 25);
                m_Output.WriteLine($@"{size,12} {size / 25,12} {milliseconds + "ms",12} {allocatedMb + " MB",14}");
            }
        }

        private static (long milliseconds, long allocatedMb) MeasureOne(int size, int layers)
        {
            var graphBuilder = new VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>>(
                new NextIdGenerator<int>(0));
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
                graphBuilder.AddActivity(
                    new DependentActivity<int, int, int>(id, 1 + (id % 9)),
                    dependencies);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long allocatedBefore = GC.GetTotalAllocatedBytes(precise: false);
            var stopwatch = Stopwatch.StartNew();
            graphBuilder.CalculateCriticalPathPriorityList();
            stopwatch.Stop();
            long allocatedAfter = GC.GetTotalAllocatedBytes(precise: false);

            return (stopwatch.ElapsedMilliseconds, (allocatedAfter - allocatedBefore) / (1024 * 1024));
        }
    }
}
