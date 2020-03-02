using FluentAssertions;
using System.Collections.Generic;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class ArrowGraphCompilerTests
    {
        [Fact]
        public void ArrowGraphCompiler_GivenCyclomaticComplexityWithNoNodes_ThenFindsZero()
        {
            var graphCompiler = new ArrowGraphCompiler<int, IDependentActivity<int>>();
            graphCompiler.Compile();
            graphCompiler.CyclomaticComplexity.Should().Be(0);
        }

        [Fact]
        public void ArrowGraphCompiler_GivenCyclomaticComplexityInOneNetwork_ThenAsExpected()
        {
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int activityId6 = activityId5 + 1;
            int activityId7 = activityId6 + 1;
            int activityId8 = activityId7 + 1;
            int activityId9 = activityId8 + 1;
            var graphCompiler = new ArrowGraphCompiler<int, IDependentActivity<int>>();
            graphCompiler.AddActivity(new DependentActivity<int>(activityId1, 6));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId2, 7));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId3, 8));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId4, 11, new HashSet<int>(new[] { 2 })));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId5, 8, new HashSet<int>(new[] { 1, 2, 3 })));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId6, 7, new HashSet<int>(new[] { 3 })));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId7, 4, new HashSet<int>(new[] { 4 })));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId8, 4, new HashSet<int>(new[] { 4, 6 })));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId9, 10, new HashSet<int>(new[] { 5 })));

            graphCompiler.Compile();

            graphCompiler.CyclomaticComplexity.Should().Be(6);
        }

        [Fact]
        public void ArrowGraphCompiler_GivenCyclomaticComplexityInThreeNetworks_ThenAsExpected()
        {
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int activityId6 = activityId5 + 1;
            var graphCompiler = new ArrowGraphCompiler<int, IDependentActivity<int>>();
            graphCompiler.AddActivity(new DependentActivity<int>(activityId1, 6));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId2, 7));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId3, 8));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId4, 11, new HashSet<int>(new[] { 1 })));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId5, 8, new HashSet<int>(new[] { 2 })));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId6, 7, new HashSet<int>(new[] { 3 })));

            graphCompiler.Compile();

            graphCompiler.CyclomaticComplexity.Should().Be(3);
        }

        [Fact]
        public void ArrowGraphCompiler_GivenCyclomaticComplexityWithTwoLoneNodes_ThenAsExpected()
        {
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            var graphCompiler = new ArrowGraphCompiler<int, IDependentActivity<int>>();
            graphCompiler.AddActivity(new DependentActivity<int>(activityId1, 6));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId2, 7));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId3, 8));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId4, 11, new HashSet<int>(new[] { 1 })));

            graphCompiler.Compile();

            graphCompiler.CyclomaticComplexity.Should().Be(3);
        }
    }
}
