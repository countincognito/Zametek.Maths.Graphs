using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    // End-to-end behaviour guards for the performance changes: critical-path
    // order-independence (the CPM allocation-cut kept the shuffle hook), scheduling
    // through a dummy chain (the cached strong-dependency set), and degenerate inputs
    // (the iterative walks' early-exits).
    public class CompileBehaviourTests
    {
        [Fact]
        public void VertexGraphBuilder_GivenRandomDags_ThenCriticalPathIsIndependentOfProcessingOrder()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var rng = new Random(seed);
                int activityCount = rng.Next(10, 40);

                // Build the specs once so both builders receive identical input; dependencies
                // only ever point at lower IDs, guaranteeing an acyclic graph.
                var specs = new List<(int Id, int Duration, int[] Deps)>();
                for (int id = 1; id <= activityCount; id++)
                {
                    var deps = new List<int>();
                    for (int dep = 1; dep < id; dep++)
                    {
                        if (rng.NextDouble() < 0.2)
                        {
                            deps.Add(dep);
                        }
                    }
                    specs.Add((id, rng.Next(1, 6), deps.ToArray()));
                }

                VertexGraphBuilder<int, int, int, IActivity<int, int, int>> ordered = BuildAndCalculate(specs, shuffle: false);
                VertexGraphBuilder<int, int, int, IActivity<int, int, int>> shuffled = BuildAndCalculate(specs, shuffle: true);

                foreach ((int id, int _, int[] _) in specs)
                {
                    IActivity<int, int, int> a = ordered.Activity(id);
                    IActivity<int, int, int> b = shuffled.Activity(id);
                    b.EarliestStartTime.ShouldBe(a.EarliestStartTime, $"seed {seed}, activity {id} EST");
                    b.LatestFinishTime.ShouldBe(a.LatestFinishTime, $"seed {seed}, activity {id} LFT");
                    b.FreeSlack.ShouldBe(a.FreeSlack, $"seed {seed}, activity {id} FreeSlack");
                    b.TotalSlack.ShouldBe(a.TotalSlack, $"seed {seed}, activity {id} TotalSlack");
                }
            }
        }

        private static VertexGraphBuilder<int, int, int, IActivity<int, int, int>> BuildAndCalculate(
            List<(int Id, int Duration, int[] Deps)> specs, bool shuffle)
        {
            var builder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(new NextIdGenerator<int>(0))
            {
                ShuffleProcessingOrder = shuffle
            };
            foreach ((int id, int duration, int[] deps) in specs)
            {
                builder.AddActivity(new Activity<int, int, int>(id, duration), [.. deps]);
            }
            builder.CalculateCriticalPath();
            return builder;
        }

        [Fact]
        public void VertexGraphCompiler_GivenDependencyThroughDummyChain_ThenScheduleRespectsIt()
        {
            var compiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            compiler.AddActivity(new DependentActivity<int, int, int>(1, 5));       // A
            compiler.AddActivity(new DependentActivity<int, int, int>(2, 0, [1]));  // B (zero-duration link) dep A
            compiler.AddActivity(new DependentActivity<int, int, int>(3, 5, [2]));  // C dep B (strong dependency: A)

            var resource = new Resource<int, int>(1, @"R1", false, false, InterActivityAllocationType.None, 0.0, 0.0, 0, []);
            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = compiler.Compile([resource]);

            compilation.CompilationErrors.ShouldBeEmpty();

            IDependentActivity<int, int, int> a = compilation.DependentActivities.Single(x => x.Id == 1);
            IDependentActivity<int, int, int> c = compilation.DependentActivities.Single(x => x.Id == 3);

            a.EarliestFinishTime.ShouldBe(5);
            // C's only real dependency is A, reached transitively through the dummy B; it must
            // not start before A finishes.
            c.EarliestStartTime.ShouldBe(5);
        }

        [Fact]
        public void VertexGraphCompiler_GivenEmptyGraph_ThenCompilesToEmptyResult()
        {
            var compiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = compiler.Compile();

            compilation.CompilationErrors.ShouldBeEmpty();
            compilation.DependentActivities.ShouldBeEmpty();
            compilation.ResourceSchedules.ShouldBeEmpty();
        }

        [Fact]
        public void VertexGraphCompiler_GivenSingleIsolatedActivity_ThenCompilesWithZeroStart()
        {
            var compiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            compiler.AddActivity(new DependentActivity<int, int, int>(1, 5));

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = compiler.Compile();

            compilation.CompilationErrors.ShouldBeEmpty();
            IDependentActivity<int, int, int> a = compilation.DependentActivities.Single();
            a.EarliestStartTime.ShouldBe(0);
            a.EarliestFinishTime.ShouldBe(5);
            a.LatestFinishTime.ShouldBe(5);
        }

        [Fact]
        public void VertexGraphCompiler_GivenAllZeroDurationChain_ThenCompilesWithoutError()
        {
            var compiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            compiler.AddActivity(new DependentActivity<int, int, int>(1, 0));
            compiler.AddActivity(new DependentActivity<int, int, int>(2, 0, [1]));
            compiler.AddActivity(new DependentActivity<int, int, int>(3, 0, [2]));

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = compiler.Compile();

            compilation.CompilationErrors.ShouldBeEmpty();
            foreach (IDependentActivity<int, int, int> activity in compilation.DependentActivities)
            {
                activity.EarliestStartTime.ShouldBe(0);
                activity.EarliestFinishTime.ShouldBe(0);
            }
        }
    }
}
