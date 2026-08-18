using Shouldly;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    // The domain limits in GraphLimits exist so that absurd or corrupted input is
    // rejected with a clear error rather than consuming unbounded time and memory.
    // Declared values and counts are checked before compilation (P0080); a schedule
    // whose computed finish time runs past the horizon - which per-value limits cannot
    // prevent, because durations sum - is reported after scheduling (C0020).
    public class GraphLimitTests
    {
        private static Resource<int, int> CreateResource(int id)
        {
            return new Resource<int, int>(id, $@"R{id}", false, false, InterActivityAllocationType.None, 0.0, 0.0, 0, []);
        }

        private static VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>> CreateCompiler()
        {
            return new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
        }

        [Fact]
        public void VertexGraphCompiler_GivenDurationAboveMaximum_ThenReportsP0080()
        {
            var compiler = CreateCompiler();
            compiler.AddActivity(new DependentActivity<int, int, int>(1, GraphLimits.MaximumTimeValue + 1));

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation =
                compiler.Compile([CreateResource(10)], TestContext.Current.CancellationToken);

            IGraphCompilationError error = compilation.CompilationErrors.ShouldHaveSingleItem();
            error.ErrorCode.ShouldBe(GraphCompilationErrorCode.P0080);
            error.ErrorMessage.ShouldContain(@"Activity 1 -> Duration");
            compilation.ResourceSchedules.ShouldBeEmpty();
        }

        [Fact]
        public void VertexGraphCompiler_GivenDurationAtMaximum_ThenCompilesWithoutLimitError()
        {
            var compiler = CreateCompiler();
            compiler.AddActivity(new DependentActivity<int, int, int>(1, GraphLimits.MaximumTimeValue));

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation =
                compiler.Compile([CreateResource(10)], TestContext.Current.CancellationToken);

            compilation.CompilationErrors.ShouldNotContain(x => x.ErrorCode == GraphCompilationErrorCode.P0080);
        }

        [Fact]
        public void VertexGraphCompiler_GivenNegativeMinimumEarliestStartTime_ThenReportsP0080()
        {
            var compiler = CreateCompiler();
            compiler.AddActivity(new DependentActivity<int, int, int>(1, 5) { MinimumEarliestStartTime = -1 });

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation =
                compiler.Compile([CreateResource(10)], TestContext.Current.CancellationToken);

            IGraphCompilationError error = compilation.CompilationErrors
                .ShouldHaveSingleItem();
            error.ErrorCode.ShouldBe(GraphCompilationErrorCode.P0080);
            error.ErrorMessage.ShouldContain(@"MinimumEarliestStartTime");
        }

        [Fact]
        public void VertexGraphCompiler_GivenMaximumLatestFinishTimeAboveMaximum_ThenReportsP0080()
        {
            var compiler = CreateCompiler();
            compiler.AddActivity(new DependentActivity<int, int, int>(1, 5)
            {
                MaximumLatestFinishTime = GraphLimits.MaximumTimeValue + 1,
            });

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation =
                compiler.Compile([CreateResource(10)], TestContext.Current.CancellationToken);

            compilation.CompilationErrors
                .ShouldContain(x => x.ErrorCode == GraphCompilationErrorCode.P0080
                    && x.ErrorMessage.Contains(@"MaximumLatestFinishTime"));
        }

        [Fact]
        public void VertexGraphCompiler_GivenNegativeMinimumFreeSlack_ThenReportsP0080()
        {
            var compiler = CreateCompiler();
            compiler.AddActivity(new DependentActivity<int, int, int>(1, 5) { MinimumFreeSlack = -5 });

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation =
                compiler.Compile([CreateResource(10)], TestContext.Current.CancellationToken);

            compilation.CompilationErrors
                .ShouldContain(x => x.ErrorCode == GraphCompilationErrorCode.P0080
                    && x.ErrorMessage.Contains(@"MinimumFreeSlack"));
        }

        [Fact]
        public void VertexGraphCompiler_GivenTooManyActivities_ThenReportsP0080()
        {
            var compiler = CreateCompiler();
            for (int id = 1; id <= GraphLimits.MaximumActivityCount + 1; id++)
            {
                compiler.AddActivity(new DependentActivity<int, int, int>(id, 1));
            }

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation =
                compiler.Compile([CreateResource(10)], TestContext.Current.CancellationToken);

            IGraphCompilationError error = compilation.CompilationErrors
                .ShouldHaveSingleItem();
            error.ErrorCode.ShouldBe(GraphCompilationErrorCode.P0080);
            error.ErrorMessage.ShouldContain(@"activities");
            error.ErrorMessage.ShouldContain(GraphLimits.MaximumActivityCount.ToString());
        }

        [Fact]
        public void VertexGraphCompiler_GivenTooManyResources_ThenReportsP0080()
        {
            var compiler = CreateCompiler();
            compiler.AddActivity(new DependentActivity<int, int, int>(1, 5));

            List<IResource<int, int>> resources = Enumerable
                .Range(1, GraphLimits.MaximumResourceCount + 1)
                .Select(x => (IResource<int, int>)CreateResource(x))
                .ToList();

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation =
                compiler.Compile(resources, TestContext.Current.CancellationToken);

            compilation.CompilationErrors
                .ShouldContain(x => x.ErrorCode == GraphCompilationErrorCode.P0080
                    && x.ErrorMessage.Contains(@"resources"));
        }

        [Fact]
        public void VertexGraphCompiler_GivenTooManyWorkStreams_ThenReportsP0080()
        {
            var compiler = CreateCompiler();
            compiler.AddActivity(new DependentActivity<int, int, int>(1, 5));

            List<IWorkStream<int>> workStreams = Enumerable
                .Range(1, GraphLimits.MaximumWorkStreamCount + 1)
                .Select(x => (IWorkStream<int>)new WorkStream<int>(x, $@"W{x}", false))
                .ToList();

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation =
                compiler.Compile([CreateResource(10)], workStreams, TestContext.Current.CancellationToken);

            compilation.CompilationErrors
                .ShouldContain(x => x.ErrorCode == GraphCompilationErrorCode.P0080
                    && x.ErrorMessage.Contains(@"work streams"));
        }

        [Fact]
        public void VertexGraphCompiler_GivenCountsAtTheirMaximums_ThenReportsNoLimitError()
        {
            var compiler = CreateCompiler();
            compiler.AddActivity(new DependentActivity<int, int, int>(1, 5));

            List<IResource<int, int>> resources = Enumerable
                .Range(1, GraphLimits.MaximumResourceCount)
                .Select(x => (IResource<int, int>)CreateResource(x))
                .ToList();
            List<IWorkStream<int>> workStreams = Enumerable
                .Range(1, GraphLimits.MaximumWorkStreamCount)
                .Select(x => (IWorkStream<int>)new WorkStream<int>(x, $@"W{x}", false))
                .ToList();

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation =
                compiler.Compile(resources, workStreams, TestContext.Current.CancellationToken);

            compilation.CompilationErrors.ShouldNotContain(x => x.ErrorCode == GraphCompilationErrorCode.P0080);
        }

        [Fact]
        public void VertexGraphCompiler_GivenDurationsThatSumBeyondTheHorizon_ThenReportsC0020()
        {
            // Every individual duration is legal, but chained together they push the
            // computed schedule past the horizon - the case per-value limits cannot catch.
            var compiler = CreateCompiler();
            int activityCount = 5;
            int duration = (GraphLimits.MaximumTimeValue / activityCount) + 1;
            for (int id = 1; id <= activityCount; id++)
            {
                compiler.AddActivity(id == 1
                    ? new DependentActivity<int, int, int>(id, duration)
                    : new DependentActivity<int, int, int>(id, duration, [id - 1]));
            }

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation =
                compiler.Compile([CreateResource(10)], TestContext.Current.CancellationToken);

            compilation.CompilationErrors
                .ShouldContain(x => x.ErrorCode == GraphCompilationErrorCode.C0020);
            compilation.ResourceSchedules.ShouldBeEmpty();
        }

        [Fact]
        public void VertexGraphCompiler_GivenScheduleEndingExactlyAtTheHorizon_ThenCompilesSuccessfully()
        {
            var compiler = CreateCompiler();
            compiler.AddActivity(new DependentActivity<int, int, int>(1, GraphLimits.MaximumTimeValue));

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation =
                compiler.Compile([CreateResource(10)], TestContext.Current.CancellationToken);

            compilation.CompilationErrors.ShouldBeEmpty();
            compilation.ResourceSchedules.ShouldNotBeEmpty();
        }
    }
}
