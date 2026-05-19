using Shouldly;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class GraphCompilationErrorFormatterTests
    {
        [Fact]
        public void GraphCompilationErrorFormatter_GivenBuildInvalidDependenciesErrorMessage_WithNullDependencies_ThenReturnsEmpty()
        {
            string output = GraphCompilationErrorFormatter<int, int, int, IDependentActivity<int, int, int>>
                .BuildInvalidDependenciesErrorMessage(null, []);
            output.ShouldBeEmpty();
        }

        [Fact]
        public void GraphCompilationErrorFormatter_GivenBuildInvalidDependenciesErrorMessage_WithEmptyDependencies_ThenReturnsEmpty()
        {
            string output = GraphCompilationErrorFormatter<int, int, int, IDependentActivity<int, int, int>>
                .BuildInvalidDependenciesErrorMessage([], []);
            output.ShouldBeEmpty();
        }

        [Fact]
        public void GraphCompilationErrorFormatter_GivenBuildInvalidDependenciesErrorMessage_WithNullActivities_ThenReturnsEmpty()
        {
            string output = GraphCompilationErrorFormatter<int, int, int, IDependentActivity<int, int, int>>
                .BuildInvalidDependenciesErrorMessage([99], null);
            output.ShouldBeEmpty();
        }

        [Fact]
        public void GraphCompilationErrorFormatter_GivenBuildInvalidDependenciesErrorMessage_WithActivitiesReferencingInvalidDep_ThenMessageContainsBothIds()
        {
            var activity = new DependentActivity<int, int, int>(1, 10, [ 99 ]);

            string output = GraphCompilationErrorFormatter<int, int, int, IDependentActivity<int, int, int>>
                .BuildInvalidDependenciesErrorMessage([99], [activity]);

            output.ShouldNotBeEmpty();
            output.ShouldContain("99");
            output.ShouldContain("1");
        }

        [Fact]
        public void GraphCompilationErrorFormatter_GivenBuildCircularDependenciesErrorMessage_WithNullInput_ThenReturnsEmpty()
        {
            string output = GraphCompilationErrorFormatter<int, int, int, IDependentActivity<int, int, int>>
                .BuildCircularDependenciesErrorMessage(null);
            output.ShouldBeEmpty();
        }

        [Fact]
        public void GraphCompilationErrorFormatter_GivenBuildCircularDependenciesErrorMessage_WithEmptyInput_ThenReturnsEmpty()
        {
            string output = GraphCompilationErrorFormatter<int, int, int, IDependentActivity<int, int, int>>
                .BuildCircularDependenciesErrorMessage([]);
            output.ShouldBeEmpty();
        }

        [Fact]
        public void GraphCompilationErrorFormatter_GivenBuildCircularDependenciesErrorMessage_WithCircularDeps_ThenMessageContainsArrowSeparatedIds()
        {
            var circular = new CircularDependency<int>([1, 2, 3]);

            string output = GraphCompilationErrorFormatter<int, int, int, IDependentActivity<int, int, int>>
                .BuildCircularDependenciesErrorMessage([circular]);

            output.ShouldNotBeEmpty();
            output.ShouldContain("->");
            output.ShouldContain("1");
            output.ShouldContain("2");
            output.ShouldContain("3");
        }

        [Fact]
        public void GraphCompilationErrorFormatter_GivenBuildInvalidConstraintsErrorMessage_WithNullInput_ThenReturnsEmpty()
        {
            string output = GraphCompilationErrorFormatter<int, int, int, IDependentActivity<int, int, int>>
                .BuildInvalidConstraintsErrorMessage(null);
            output.ShouldBeEmpty();
        }

        [Fact]
        public void GraphCompilationErrorFormatter_GivenBuildInvalidConstraintsErrorMessage_WithEmptyInput_ThenReturnsEmpty()
        {
            string output = GraphCompilationErrorFormatter<int, int, int, IDependentActivity<int, int, int>>
                .BuildInvalidConstraintsErrorMessage([]);
            output.ShouldBeEmpty();
        }

        [Fact]
        public void GraphCompilationErrorFormatter_GivenBuildInvalidConstraintsErrorMessage_WithConstraints_ThenMessageContainsIdAndMessage()
        {
            var constraint = new InvalidConstraint<int>(7, "some-constraint-message");

            string output = GraphCompilationErrorFormatter<int, int, int, IDependentActivity<int, int, int>>
                .BuildInvalidConstraintsErrorMessage([constraint]);

            output.ShouldNotBeEmpty();
            output.ShouldContain("7");
            output.ShouldContain("some-constraint-message");
        }

        [Fact]
        public void GraphCompilationErrorFormatter_GivenBuildUnavailableResourcesErrorMessage_WithNullInput_ThenReturnsEmpty()
        {
            string output = GraphCompilationErrorFormatter<int, int, int, IDependentActivity<int, int, int>>
                .BuildUnavailableResourcesErrorMessage(null);
            output.ShouldBeEmpty();
        }

        [Fact]
        public void GraphCompilationErrorFormatter_GivenBuildUnavailableResourcesErrorMessage_WithEmptyInput_ThenReturnsEmpty()
        {
            string output = GraphCompilationErrorFormatter<int, int, int, IDependentActivity<int, int, int>>
                .BuildUnavailableResourcesErrorMessage([]);
            output.ShouldBeEmpty();
        }

        [Fact]
        public void GraphCompilationErrorFormatter_GivenBuildUnavailableResourcesErrorMessage_WithEntries_ThenMessageContainsActivityIdAndResourceIds()
        {
            var unavailable = new UnavailableResources<int, int>(5, [11, 22]);

            string output = GraphCompilationErrorFormatter<int, int, int, IDependentActivity<int, int, int>>
                .BuildUnavailableResourcesErrorMessage([unavailable]);

            output.ShouldNotBeEmpty();
            output.ShouldContain("5");
            output.ShouldContain("11");
            output.ShouldContain("22");
        }

        [Fact]
        public void GraphCompilationErrorFormatter_GivenBuildInvalidDependenciesErrorMessage_WithPlanningDependencyReference_ThenMessageContainsBothIds()
        {
            var activity = new DependentActivity<int, int, int>(2, 10, dependencies: [], planningDependencies: [88]);

            string output = GraphCompilationErrorFormatter<int, int, int, IDependentActivity<int, int, int>>
                .BuildInvalidDependenciesErrorMessage([88], [activity]);

            output.ShouldNotBeEmpty();
            output.ShouldContain("88");
            output.ShouldContain("2");
        }
    }
}
