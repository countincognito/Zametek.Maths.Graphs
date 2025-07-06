using Shouldly;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class VertexGraphCompilerTests
    {
        [Fact]
        public void VertexGraphCompiler_GivenContructor_ThenNoException()
        {
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            var graphBuilder = graphCompiler.Builder;
            graphBuilder.EdgeIds.Any().ShouldBeFalse();
            graphBuilder.NodeIds.Any().ShouldBeFalse();
            graphBuilder.AllDependenciesSatisfied.ShouldBeTrue();
            graphBuilder.StartNodes.Any().ShouldBeFalse();
            graphBuilder.EndNodes.Any().ShouldBeFalse();
        }

        [Fact]
        public void VertexGraphCompiler_GivenSingleActivityNoDependencies_ThenNoStartOrEndNodes()
        {
            int activityId = 0;
            int activityId1 = activityId + 1;
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            var graphBuilder = graphCompiler.Builder;

            var activity = new DependentActivity<int, int, int>(activityId1, 0);
            bool result = graphCompiler.AddActivity(activity);
            result.ShouldBeTrue();

            graphBuilder.EdgeIds.Any().ShouldBeFalse();
            graphBuilder.NodeIds.Count().ShouldBe(1);
            graphBuilder.AllDependenciesSatisfied.ShouldBeTrue();

            graphBuilder.StartNodes.Any().ShouldBeFalse();
            graphBuilder.EndNodes.Any().ShouldBeFalse();

            graphBuilder.Node(activityId1).Id.ShouldBe(activityId1);
            graphBuilder.Node(activityId1).NodeType.ShouldBe(NodeType.Isolated);
            graphBuilder.Activity(activityId1).Id.ShouldBe(activityId1);
            graphBuilder.Activities.Count().ShouldBe(1);
            graphBuilder.Edges.Any().ShouldBeFalse();
        }

        [Fact]
        public void VertexGraphCompiler_GivenTwoActivitiesNoDependencies_ThenNodesAreIsolatedWithSameFinishTimes()
        {
            int activityId = 0;
            int activityId1 = activityId + 1;
            int activityId2 = activityId1 + 1;
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            var graphBuilder = graphCompiler.Builder;

            var activity1 = new DependentActivity<int, int, int>(activityId1, 3);
            bool result1 = graphCompiler.AddActivity(activity1);
            result1.ShouldBeTrue();

            var activity2 = new DependentActivity<int, int, int>(activityId2, 5);
            bool result2 = graphCompiler.AddActivity(activity2);
            result2.ShouldBeTrue();

            var output = graphCompiler.Compile();

            graphBuilder.EdgeIds.Any().ShouldBeFalse();
            graphBuilder.NodeIds.Count().ShouldBe(2);
            graphBuilder.AllDependenciesSatisfied.ShouldBeTrue();

            graphBuilder.StartNodes.Any().ShouldBeFalse();
            graphBuilder.EndNodes.Any().ShouldBeFalse();

            graphBuilder.Node(activityId1).Id.ShouldBe(activityId1);
            graphBuilder.Node(activityId1).NodeType.ShouldBe(NodeType.Isolated);
            graphBuilder.Activity(activityId1).Id.ShouldBe(activityId1);
            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(2);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(3);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(5);
            graphBuilder.Activity(activityId1).FreeSlack.ShouldBe(2);
            graphBuilder.Activity(activityId1).TotalSlack.ShouldBe(2);

            graphBuilder.Node(activityId2).Id.ShouldBe(activityId2);
            graphBuilder.Node(activityId2).NodeType.ShouldBe(NodeType.Isolated);
            graphBuilder.Activity(activityId2).Id.ShouldBe(activityId2);
            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(5);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(5);
            graphBuilder.Activity(activityId2).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId2).TotalSlack.ShouldBe(0);

            graphBuilder.Activities.Count().ShouldBe(2);
            graphBuilder.Edges.Any().ShouldBeFalse();
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithInvalidConstraints_ThenFindsInvalidConstraints()
        {
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(1, 6));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(2, 7));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(3, 8));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(4, 11, new HashSet<int> { 2 }) { MinimumEarliestStartTime = 7, MaximumLatestFinishTime = 17 });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(5, 8, new HashSet<int> { 1, 2, 3 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(6, 7, new HashSet<int> { 3 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(7, 4, new HashSet<int> { 4 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(8, 4, new HashSet<int> { 4, 6 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(9, 10, new HashSet<int> { 5 }) { MinimumFreeSlack = 2, MaximumLatestFinishTime = 8 });

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile();

            compilation.ResourceSchedules.ShouldBeEmpty();
            compilation.CompilationErrors.ShouldNotBeEmpty();

            var compilationErrors = compilation.CompilationErrors.ToList();

            compilationErrors.Count.ShouldBe(1);
            compilationErrors[0].ErrorCode.ShouldBe(GraphCompilationErrorCode.P0030);
            compilationErrors[0].ErrorMessage.ShouldBe(
                $@"{Properties.Resources.Message_InvalidConstraints}
4 -> {Properties.Resources.Message_MinimumEarliestStartTimePlusDurationMustBeGreaterThanMaximumLatestFinishTime}
9 -> {Properties.Resources.Message_CannotSetMinimumFreeSlackAndMaximumLatestFinishTime}
");
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithCircularDependencies_ThenFindsCircularDependencies()
        {
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(1, 10));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(2, 10, new HashSet<int> { 7 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(3, 10));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(4, 10, new HashSet<int> { 2 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(5, 10, new HashSet<int> { 1, 2, 3, 8 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(6, 10, new HashSet<int> { 3 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(7, 10, new HashSet<int> { 4 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(8, 10, new HashSet<int> { 9, 6 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(9, 10, new HashSet<int> { 5 }));

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile();

            compilation.ResourceSchedules.ShouldBeEmpty();
            compilation.CompilationErrors.ShouldNotBeEmpty();

            var compilationErrors = compilation.CompilationErrors.ToList();

            compilationErrors.Count.ShouldBe(1);
            compilationErrors[0].ErrorCode.ShouldBe(GraphCompilationErrorCode.P0020);
            compilationErrors[0].ErrorMessage.ShouldBe(
                $@"{Properties.Resources.Message_CircularDependencies}
4 -> 7 -> 2
9 -> 8 -> 5
");
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithInvalidDependencies_ThenFindsInvalidDependencies()
        {
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(1, 10));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(2, 10, new HashSet<int> { 7 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(3, 10, new HashSet<int> { 21 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(4, 10, new HashSet<int> { 2 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(5, 10, new HashSet<int> { 1, 2, 3, 8 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(6, 10, new HashSet<int> { 3 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(7, 10, new HashSet<int> { 22 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(8, 10, new HashSet<int> { 9, 6 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(9, 10));

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile();

            compilation.ResourceSchedules.ShouldBeEmpty();
            compilation.CompilationErrors.ShouldNotBeEmpty();

            var compilationErrors = compilation.CompilationErrors.ToList();
            compilationErrors.Count.ShouldBe(1);
            compilationErrors[0].ErrorCode.ShouldBe(GraphCompilationErrorCode.P0010);
            compilationErrors[0].ErrorMessage.ShouldBe(
                $@"{Properties.Resources.Message_InvalidDependencies}
21 {Properties.Resources.Message_IsInvalidButReferencedBy} 3
22 {Properties.Resources.Message_IsInvalidButReferencedBy} 7
");
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithInvalidConstraintsAndCircularAndInvalidDependencies_ThenFindsInvalidConstraintsAndCircularAndInvalidDependencies()
        {
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(1, 10));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(2, 10, new HashSet<int> { 7 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(3, 10, new HashSet<int> { 21 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(4, 10, new HashSet<int> { 2 }) { MinimumEarliestStartTime = 7, MaximumLatestFinishTime = 16 });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(5, 10, new HashSet<int> { 1, 2, 3, 8 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(6, 10, new HashSet<int> { 3 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(7, 10, new HashSet<int> { 4, 22 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(8, 10, new HashSet<int> { 9, 6, 22 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(9, 10, new HashSet<int> { 5 }));

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile();

            compilation.ResourceSchedules.ShouldBeEmpty();
            compilation.CompilationErrors.ShouldNotBeEmpty();

            var compilationErrors = compilation.CompilationErrors.ToList();

            compilationErrors.Count.ShouldBe(3);

            compilationErrors[0].ErrorCode.ShouldBe(GraphCompilationErrorCode.P0010);
            compilationErrors[0].ErrorMessage.ShouldBe(
                $@"{Properties.Resources.Message_InvalidDependencies}
21 {Properties.Resources.Message_IsInvalidButReferencedBy} 3
22 {Properties.Resources.Message_IsInvalidButReferencedBy} 7, 8
");

            compilationErrors[1].ErrorCode.ShouldBe(GraphCompilationErrorCode.P0020);
            compilationErrors[1].ErrorMessage.ShouldBe(
                $@"{Properties.Resources.Message_CircularDependencies}
4 -> 7 -> 2
9 -> 8 -> 5
");

            compilationErrors[2].ErrorCode.ShouldBe(GraphCompilationErrorCode.P0030);
            compilationErrors[2].ErrorMessage.ShouldBe(
                $@"{Properties.Resources.Message_InvalidConstraints}
4 -> {Properties.Resources.Message_MinimumEarliestStartTimePlusDurationMustBeGreaterThanMaximumLatestFinishTime}
");
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithPostCompilationInvalidConstraints_ThenFindsInvalidConstraints()
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
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            var graphBuilder = graphCompiler.Builder;
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId1, 6));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId2, 7));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId3, 8));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId4, 11, new HashSet<int> { 2 }) { MaximumLatestFinishTime = 5 });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId5, 8, new HashSet<int> { 1, 2, 3 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId6, 7, new HashSet<int> { 3 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId7, 4, new HashSet<int> { 4 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId8, 4, new HashSet<int> { 4, 6 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId9, 10, new HashSet<int> { 5 }));

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile();

            compilation.ResourceSchedules.ShouldNotBeEmpty();
            compilation.CompilationErrors.ShouldNotBeEmpty();

            var compilationErrors = compilation.CompilationErrors.ToList();

            compilationErrors.Count.ShouldBe(1);
            compilationErrors[0].ErrorCode.ShouldBe(GraphCompilationErrorCode.C0010);
            compilationErrors[0].ErrorMessage.ShouldBe(
                $@"{Properties.Resources.Message_InvalidConstraints}
2 -> {Properties.Resources.Message_LatestStartTimeLessThanZero}
2 -> {Properties.Resources.Message_LatestFinishTimeLessThanZero}
2 -> {Properties.Resources.Message_LatestStartTimeLessThanEarliestStartTime}
2 -> {Properties.Resources.Message_LatestFinishTimeLessThanEarliestFinishTime}
4 -> {Properties.Resources.Message_EarliestStartTimeLessThanZero}
4 -> {Properties.Resources.Message_LatestStartTimeLessThanZero}
");

            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(3);

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.ShouldBeNull();
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(3);

            scheduledActivities0[0].Id.ShouldBe(activityId2);
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(7);

            scheduledActivities0[1].Id.ShouldBe(activityId4);
            scheduledActivities0[1].StartTime.ShouldBe(-6);
            scheduledActivities0[1].FinishTime.ShouldBe(5);

            scheduledActivities0[2].Id.ShouldBe(activityId7);
            scheduledActivities0[2].StartTime.ShouldBe(5);
            scheduledActivities0[2].FinishTime.ShouldBe(9);

            scheduledActivities0.Last().FinishTime.ShouldBe(9);


            var resourceSchedule1 = resourceSchedules[1];
            resourceSchedule1.Resource.ShouldBeNull();
            var scheduledActivities1 = resourceSchedule1.ScheduledActivities.ToList();
            scheduledActivities1.Count.ShouldBe(3);

            scheduledActivities1[0].Id.ShouldBe(activityId3);
            scheduledActivities1[0].StartTime.ShouldBe(0);
            scheduledActivities1[0].FinishTime.ShouldBe(8);

            scheduledActivities1[1].Id.ShouldBe(activityId5);
            scheduledActivities1[1].StartTime.ShouldBe(8);
            scheduledActivities1[1].FinishTime.ShouldBe(16);

            scheduledActivities1[2].Id.ShouldBe(activityId9);
            scheduledActivities1[2].StartTime.ShouldBe(16);
            scheduledActivities1[2].FinishTime.ShouldBe(26);

            scheduledActivities1.Last().FinishTime.ShouldBe(26);


            var resourceSchedule2 = resourceSchedules[2];
            resourceSchedule2.Resource.ShouldBeNull();
            var scheduledActivities2 = resourceSchedule2.ScheduledActivities.ToList();
            scheduledActivities2.Count.ShouldBe(3);

            scheduledActivities2[0].Id.ShouldBe(activityId1);
            scheduledActivities2[0].StartTime.ShouldBe(0);
            scheduledActivities2[0].FinishTime.ShouldBe(6);

            scheduledActivities2[1].Id.ShouldBe(activityId6);
            scheduledActivities2[1].StartTime.ShouldBe(8);
            scheduledActivities2[1].FinishTime.ShouldBe(15);

            scheduledActivities2[2].Id.ShouldBe(activityId8);
            scheduledActivities2[2].StartTime.ShouldBe(15);
            scheduledActivities2[2].FinishTime.ShouldBe(19);

            scheduledActivities2.Last().FinishTime.ShouldBe(19);



            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(6);
            graphBuilder.Activity(activityId1).FreeSlack.ShouldBe(2);
            graphBuilder.Activity(activityId1).TotalSlack.ShouldBe(2);
            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(2);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId1).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId1).Successors.Contains(activityId5).ShouldBeTrue();


            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(7);
            graphBuilder.Activity(activityId2).FreeSlack.ShouldBe(-13);
            graphBuilder.Activity(activityId2).TotalSlack.ShouldBe(-13);
            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(-13);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(-6);
            graphBuilder.Activity(activityId2).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId2).Successors.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId2).Successors.Contains(activityId5).ShouldBeTrue();

            graphBuilder.Activity(activityId3).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId3).Successors.Contains(activityId5).ShouldBeTrue();
            graphBuilder.Activity(activityId3).Successors.Contains(activityId6).ShouldBeTrue();

            graphBuilder.Activity(activityId4).EarliestStartTime.ShouldBe(-6);
            graphBuilder.Activity(activityId4).EarliestFinishTime.ShouldBe(5);
            graphBuilder.Activity(activityId4).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).LatestStartTime.ShouldBe(-6);
            graphBuilder.Activity(activityId4).LatestFinishTime.ShouldBe(5);
            graphBuilder.Activity(activityId4).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId4).Dependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId4).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId4).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId4).ResourceDependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId4).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId4).Successors.Contains(activityId7).ShouldBeTrue();
            graphBuilder.Activity(activityId4).Successors.Contains(activityId8).ShouldBeTrue();

            graphBuilder.Activity(activityId5).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId5).EarliestFinishTime.ShouldBe(16);
            graphBuilder.Activity(activityId5).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).LatestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId5).LatestFinishTime.ShouldBe(16);
            graphBuilder.Activity(activityId5).Dependencies.Count.ShouldBe(3);
            graphBuilder.Activity(activityId5).Dependencies.Contains(activityId1).ShouldBeTrue();
            graphBuilder.Activity(activityId5).Dependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId5).Dependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId5).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId5).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId5).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId5).ResourceDependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId5).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId5).Successors.Contains(activityId9).ShouldBeTrue();

            graphBuilder.Activity(activityId6).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.ShouldBe(15);
            graphBuilder.Activity(activityId6).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId6).TotalSlack.ShouldBe(7);
            graphBuilder.Activity(activityId6).LatestStartTime.ShouldBe(15);
            graphBuilder.Activity(activityId6).LatestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId6).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).Dependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId6).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId6).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).ResourceDependencies.Contains(activityId1).ShouldBeTrue();
            graphBuilder.Activity(activityId6).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).Successors.Contains(activityId8).ShouldBeTrue();

            graphBuilder.Activity(activityId7).EarliestStartTime.ShouldBe(5);
            graphBuilder.Activity(activityId7).EarliestFinishTime.ShouldBe(9);
            graphBuilder.Activity(activityId7).FreeSlack.ShouldBe(17);
            graphBuilder.Activity(activityId7).TotalSlack.ShouldBe(17);
            graphBuilder.Activity(activityId7).LatestStartTime.ShouldBe(22);
            graphBuilder.Activity(activityId7).LatestFinishTime.ShouldBe(26);
            graphBuilder.Activity(activityId7).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId7).Dependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId7).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId7).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId7).ResourceDependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId7).Successors.Count.ShouldBe(0);

            graphBuilder.Activity(activityId8).EarliestStartTime.ShouldBe(15);
            graphBuilder.Activity(activityId8).EarliestFinishTime.ShouldBe(19);
            graphBuilder.Activity(activityId8).FreeSlack.ShouldBe(7);
            graphBuilder.Activity(activityId8).TotalSlack.ShouldBe(7);
            graphBuilder.Activity(activityId8).LatestStartTime.ShouldBe(22);
            graphBuilder.Activity(activityId8).LatestFinishTime.ShouldBe(26);
            graphBuilder.Activity(activityId8).Dependencies.Count.ShouldBe(2);
            graphBuilder.Activity(activityId8).Dependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId8).Dependencies.Contains(activityId6).ShouldBeTrue();
            graphBuilder.Activity(activityId8).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId8).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId8).ResourceDependencies.Contains(activityId6).ShouldBeTrue();
            graphBuilder.Activity(activityId8).Successors.Count.ShouldBe(0);

            graphBuilder.Activity(activityId9).EarliestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId9).EarliestFinishTime.ShouldBe(26);
            graphBuilder.Activity(activityId9).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).LatestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId9).LatestFinishTime.ShouldBe(26);
            graphBuilder.Activity(activityId9).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId9).Dependencies.Contains(activityId5).ShouldBeTrue();
            graphBuilder.Activity(activityId9).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId9).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId9).ResourceDependencies.Contains(activityId5).ShouldBeTrue();
            graphBuilder.Activity(activityId9).Successors.Count.ShouldBe(0);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithMinimumFreeSlackPostCompilationInvalidConstraints_ThenFindsInvalidConstraints()
        {
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            var graphBuilder = graphCompiler.Builder;
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId1, 10) { MinimumFreeSlack = 10 });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId2, 10, new HashSet<int> { activityId1 }) { MaximumLatestFinishTime = 20 });

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile();

            compilation.ResourceSchedules.ShouldNotBeEmpty();
            compilation.CompilationErrors.ShouldNotBeEmpty();

            var compilationErrors = compilation.CompilationErrors.ToList();

            compilationErrors.Count.ShouldBe(1);
            compilationErrors[0].ErrorCode.ShouldBe(GraphCompilationErrorCode.C0010);
            compilationErrors[0].ErrorMessage.ShouldBe(
                $@"{Properties.Resources.Message_InvalidConstraints}
1 -> {Properties.Resources.Message_FreeSlackLessThanMinimumFreeSlack}
");

            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(10);
            graphBuilder.Activity(activityId1).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId1).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(10);
            graphBuilder.Activity(activityId1).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId1).Successors.Contains(activityId2).ShouldBeTrue();


            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(10);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(20);
            graphBuilder.Activity(activityId2).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId2).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(10);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(20);
            graphBuilder.Activity(activityId2).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId2).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId2).Successors.Count.ShouldBe(0);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithUnavailableResources_ThenFindsUnavailableResources()
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
            int activityId10 = activityId9 + 1;
            int activityId11 = activityId10 + 1;
            int activityId12 = activityId11 + 1;
            int activityId13 = activityId12 + 1;
            int activityId14 = activityId13 + 1;
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();

            var activity1 = new DependentActivity<int, int, int>(activityId1, 6);
            var activity2 = new DependentActivity<int, int, int>(activityId2, 7);
            var activity3 = new DependentActivity<int, int, int>(activityId3, 4);
            var activity4 = new DependentActivity<int, int, int>(activityId4, 8);
            var activity5 = new DependentActivity<int, int, int>(activityId5, 3);
            var activity6 = new DependentActivity<int, int, int>(activityId6, 2);
            var activity7 = new DependentActivity<int, int, int>(activityId7, 1);
            var activity8 = new DependentActivity<int, int, int>(activityId8, 6);
            var activity9 = new DependentActivity<int, int, int>(activityId9, 12);
            var activity10 = new DependentActivity<int, int, int>(activityId10, 11);
            var activity11 = new DependentActivity<int, int, int>(activityId11, 9);
            var activity12 = new DependentActivity<int, int, int>(activityId12, 3);
            var activity13 = new DependentActivity<int, int, int>(activityId13, 13);
            var activity14 = new DependentActivity<int, int, int>(activityId14, 8);

            int resourceId1 = 1;
            int resourceId2 = resourceId1 + 1;
            int resourceId3 = resourceId2 + 1;

            activity1.TargetResources.Add(resourceId1);
            activity1.TargetResourceOperator = LogicalOperator.AND;

            activity2.TargetResources.Add(resourceId2);
            activity2.TargetResourceOperator = LogicalOperator.AND;

            activity3.TargetResources.Add(resourceId3);
            activity3.TargetResourceOperator = LogicalOperator.AND;

            activity4.TargetResources.Add(resourceId1);
            activity4.TargetResources.Add(resourceId2);
            activity4.TargetResourceOperator = LogicalOperator.AND;

            activity5.TargetResources.Add(resourceId2);
            activity5.TargetResources.Add(resourceId3);
            activity5.TargetResourceOperator = LogicalOperator.AND;

            activity6.TargetResources.Add(resourceId1);
            activity6.TargetResources.Add(resourceId3);
            activity6.TargetResourceOperator = LogicalOperator.AND;

            activity7.TargetResources.Add(resourceId1);
            activity7.TargetResources.Add(resourceId2);
            activity7.TargetResources.Add(resourceId3);
            activity7.TargetResourceOperator = LogicalOperator.AND;

            activity8.TargetResources.Add(resourceId1);
            activity8.TargetResourceOperator = LogicalOperator.OR;

            activity9.TargetResources.Add(resourceId2);
            activity9.TargetResourceOperator = LogicalOperator.OR;

            activity10.TargetResources.Add(resourceId3);
            activity10.TargetResourceOperator = LogicalOperator.OR;

            activity11.TargetResources.Add(resourceId1);
            activity11.TargetResources.Add(resourceId2);
            activity11.TargetResourceOperator = LogicalOperator.OR;

            activity12.TargetResources.Add(resourceId2);
            activity12.TargetResources.Add(resourceId3);
            activity12.TargetResourceOperator = LogicalOperator.OR;

            activity13.TargetResources.Add(resourceId1);
            activity13.TargetResources.Add(resourceId3);
            activity13.TargetResourceOperator = LogicalOperator.OR;

            activity14.TargetResources.Add(resourceId1);
            activity14.TargetResources.Add(resourceId2);
            activity14.TargetResources.Add(resourceId3);
            activity14.TargetResourceOperator = LogicalOperator.OR;

            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);
            graphCompiler.AddActivity(activity4);
            graphCompiler.AddActivity(activity5);
            graphCompiler.AddActivity(activity6);
            graphCompiler.AddActivity(activity7);
            graphCompiler.AddActivity(activity8);
            graphCompiler.AddActivity(activity9);
            graphCompiler.AddActivity(activity10);
            graphCompiler.AddActivity(activity11);
            graphCompiler.AddActivity(activity12);
            graphCompiler.AddActivity(activity13);
            graphCompiler.AddActivity(activity14);

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int, int>>(new[]
                {
                    new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>()),
                    new Resource<int, int>(resourceId2, string.Empty, false, true, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>()),
                    new Resource<int, int>(resourceId3, string.Empty, false, true, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>()),
                }));

            compilation.ResourceSchedules.ShouldBeEmpty();
            compilation.CompilationErrors.ShouldNotBeEmpty();

            var compilationErrors = compilation.CompilationErrors.ToList();

            compilationErrors.Count.ShouldBe(1);
            compilationErrors[0].ErrorCode.ShouldBe(GraphCompilationErrorCode.P0060);
            compilationErrors[0].ErrorMessage.ShouldBe(
                $@"{Properties.Resources.Message_UnavailableResources}
{activityId2} -> {resourceId2}
{activityId3} -> {resourceId3}
{activityId4} -> {resourceId2}
{activityId5} -> {resourceId2}, {resourceId3}
{activityId6} -> {resourceId3}
{activityId7} -> {resourceId2}, {resourceId3}
{activityId9} -> {resourceId2}
{activityId10} -> {resourceId3}
{activityId12} -> {resourceId2}, {resourceId3}
");
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithAvailableResources_ThenResourceSchedulesCorrectOrder()
        {
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();

            var activity1 = new DependentActivity<int, int, int>(activityId1, 6);
            var activity2 = new DependentActivity<int, int, int>(activityId2, 7, new HashSet<int> { activityId1 });
            var activity3 = new DependentActivity<int, int, int>(activityId3, 4, new HashSet<int> { activityId2 });

            int resourceId1 = 1;
            int resourceId2 = resourceId1 + 1;
            int resourceId3 = resourceId2 + 1;

            activity1.TargetResources.Add(resourceId1);
            activity1.TargetResourceOperator = LogicalOperator.AND;

            activity2.TargetResources.Add(resourceId1);
            activity2.TargetResources.Add(resourceId2);
            activity2.TargetResourceOperator = LogicalOperator.OR;

            activity3.TargetResources.Add(resourceId1);
            activity3.TargetResources.Add(resourceId3);
            activity3.TargetResourceOperator = LogicalOperator.OR;

            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int, int>>(new[]
                {
                    new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>()),
                    new Resource<int, int>(resourceId2, string.Empty, false, true, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>()),
                    new Resource<int, int>(resourceId3, string.Empty, false, true, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>()),
                }));

            compilation.ResourceSchedules.ShouldNotBeEmpty();
            compilation.CompilationErrors.ShouldBeEmpty();

            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(1);

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(3);

            scheduledActivities0[0].Id.ShouldBe(activityId1);
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(6);

            scheduledActivities0[1].Id.ShouldBe(activityId2);
            scheduledActivities0[1].StartTime.ShouldBe(6);
            scheduledActivities0[1].FinishTime.ShouldBe(13);

            scheduledActivities0[2].Id.ShouldBe(activityId3);
            scheduledActivities0[2].StartTime.ShouldBe(13);
            scheduledActivities0[2].FinishTime.ShouldBe(17);

            scheduledActivities0.Last().FinishTime.ShouldBe(17);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithCompiledDependenciesAndUnlimitedResources_ThenResourceSchedulesCorrectOrder()
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
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            var graphBuilder = graphCompiler.Builder;
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId1, 6));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId2, 7));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId3, 8));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId4, 11, new HashSet<int> { activityId2 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId5, 8, new HashSet<int> { activityId1, activityId2, activityId3 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId6, 7, new HashSet<int> { activityId3 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId7, 4, new HashSet<int> { activityId4 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId8, 4, new HashSet<int> { activityId4, activityId6 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId9, 10, new HashSet<int> { activityId5 }));

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile();

            compilation.CompilationErrors.ShouldBeEmpty();

            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(3);

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.ShouldBeNull();
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(3);

            scheduledActivities0[0].Id.ShouldBe(activityId3);
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(8);

            scheduledActivities0[1].Id.ShouldBe(activityId5);
            scheduledActivities0[1].StartTime.ShouldBe(8);
            scheduledActivities0[1].FinishTime.ShouldBe(16);

            scheduledActivities0[2].Id.ShouldBe(activityId9);
            scheduledActivities0[2].StartTime.ShouldBe(16);
            scheduledActivities0[2].FinishTime.ShouldBe(26);

            scheduledActivities0.Last().FinishTime.ShouldBe(26);


            var resourceSchedule1 = resourceSchedules[1];
            resourceSchedule1.Resource.ShouldBeNull();
            var scheduledActivities1 = resourceSchedule1.ScheduledActivities.ToList();
            scheduledActivities1.Count.ShouldBe(3);

            scheduledActivities1[0].Id.ShouldBe(activityId2);
            scheduledActivities1[0].StartTime.ShouldBe(0);
            scheduledActivities1[0].FinishTime.ShouldBe(7);

            scheduledActivities1[1].Id.ShouldBe(activityId4);
            scheduledActivities1[1].StartTime.ShouldBe(7);
            scheduledActivities1[1].FinishTime.ShouldBe(18);

            scheduledActivities1[2].Id.ShouldBe(activityId7);
            scheduledActivities1[2].StartTime.ShouldBe(18);
            scheduledActivities1[2].FinishTime.ShouldBe(22);

            scheduledActivities1.Last().FinishTime.ShouldBe(22);


            var resourceSchedule2 = resourceSchedules[2];
            resourceSchedule2.Resource.ShouldBeNull();
            var scheduledActivities2 = resourceSchedule2.ScheduledActivities.ToList();
            scheduledActivities2.Count.ShouldBe(3);

            scheduledActivities2[0].Id.ShouldBe(activityId1);
            scheduledActivities2[0].StartTime.ShouldBe(0);
            scheduledActivities2[0].FinishTime.ShouldBe(6);

            scheduledActivities2[1].Id.ShouldBe(activityId6);
            scheduledActivities2[1].StartTime.ShouldBe(8);
            scheduledActivities2[1].FinishTime.ShouldBe(15);

            scheduledActivities2[2].Id.ShouldBe(activityId8);
            scheduledActivities2[2].StartTime.ShouldBe(18);
            scheduledActivities2[2].FinishTime.ShouldBe(22);

            scheduledActivities2.Last().FinishTime.ShouldBe(22);



            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(6);
            graphBuilder.Activity(activityId1).FreeSlack.ShouldBe(2);
            graphBuilder.Activity(activityId1).TotalSlack.ShouldBe(2);
            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(2);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId1).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId1).Successors.Contains(activityId5).ShouldBeTrue();


            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(7);
            graphBuilder.Activity(activityId2).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId2).TotalSlack.ShouldBe(1);
            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(1);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId2).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId2).Successors.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId2).Successors.Contains(activityId5).ShouldBeTrue();

            graphBuilder.Activity(activityId3).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId3).Successors.Contains(activityId5).ShouldBeTrue();
            graphBuilder.Activity(activityId3).Successors.Contains(activityId6).ShouldBeTrue();

            graphBuilder.Activity(activityId4).EarliestStartTime.ShouldBe(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.ShouldBe(18);
            graphBuilder.Activity(activityId4).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).TotalSlack.ShouldBe(4);
            graphBuilder.Activity(activityId4).LatestStartTime.ShouldBe(11);
            graphBuilder.Activity(activityId4).LatestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId4).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId4).Dependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId4).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId4).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId4).ResourceDependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId4).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId4).Successors.Contains(activityId7).ShouldBeTrue();
            graphBuilder.Activity(activityId4).Successors.Contains(activityId8).ShouldBeTrue();

            graphBuilder.Activity(activityId5).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId5).EarliestFinishTime.ShouldBe(16);
            graphBuilder.Activity(activityId5).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).LatestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId5).LatestFinishTime.ShouldBe(16);
            graphBuilder.Activity(activityId5).Dependencies.Count.ShouldBe(3);
            graphBuilder.Activity(activityId5).Dependencies.Contains(activityId1).ShouldBeTrue();
            graphBuilder.Activity(activityId5).Dependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId5).Dependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId5).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId5).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId5).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId5).ResourceDependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId5).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId5).Successors.Contains(activityId9).ShouldBeTrue();

            graphBuilder.Activity(activityId6).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.ShouldBe(15);
            graphBuilder.Activity(activityId6).FreeSlack.ShouldBe(3);
            graphBuilder.Activity(activityId6).TotalSlack.ShouldBe(7);
            graphBuilder.Activity(activityId6).LatestStartTime.ShouldBe(15);
            graphBuilder.Activity(activityId6).LatestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId6).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).Dependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId6).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId6).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).ResourceDependencies.Contains(activityId1).ShouldBeTrue();
            graphBuilder.Activity(activityId6).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).Successors.Contains(activityId8).ShouldBeTrue();

            graphBuilder.Activity(activityId7).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId7).FreeSlack.ShouldBe(4);
            graphBuilder.Activity(activityId7).TotalSlack.ShouldBe(4);
            graphBuilder.Activity(activityId7).LatestStartTime.ShouldBe(22);
            graphBuilder.Activity(activityId7).LatestFinishTime.ShouldBe(26);
            graphBuilder.Activity(activityId7).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId7).Dependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId7).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId7).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId7).ResourceDependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId7).Successors.Count.ShouldBe(0);

            graphBuilder.Activity(activityId8).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId8).EarliestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId8).FreeSlack.ShouldBe(4);
            graphBuilder.Activity(activityId8).TotalSlack.ShouldBe(4);
            graphBuilder.Activity(activityId8).LatestStartTime.ShouldBe(22);
            graphBuilder.Activity(activityId8).LatestFinishTime.ShouldBe(26);
            graphBuilder.Activity(activityId8).Dependencies.Count.ShouldBe(2);
            graphBuilder.Activity(activityId8).Dependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId8).Dependencies.Contains(activityId6).ShouldBeTrue();
            graphBuilder.Activity(activityId8).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId8).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId8).ResourceDependencies.Contains(activityId6).ShouldBeTrue();
            graphBuilder.Activity(activityId8).Successors.Count.ShouldBe(0);

            graphBuilder.Activity(activityId9).EarliestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId9).EarliestFinishTime.ShouldBe(26);
            graphBuilder.Activity(activityId9).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).LatestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId9).LatestFinishTime.ShouldBe(26);
            graphBuilder.Activity(activityId9).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId9).Dependencies.Contains(activityId5).ShouldBeTrue();
            graphBuilder.Activity(activityId9).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId9).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId9).ResourceDependencies.Contains(activityId5).ShouldBeTrue();
            graphBuilder.Activity(activityId9).Successors.Count.ShouldBe(0);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithCompiledDependenciesAndUnlimitedResourcesAndTargetResources_ThenResourceSchedulesCorrectOrder()
        {
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();

            var activity1 = new DependentActivity<int, int, int>(activityId1, 6);
            var activity2 = new DependentActivity<int, int, int>(activityId2, 7, new HashSet<int> { activityId1 });
            var activity3 = new DependentActivity<int, int, int>(activityId3, 4, new HashSet<int> { activityId2 });

            int resourceId1 = 1;
            int resourceId2 = resourceId1 + 1;
            int resourceId3 = resourceId2 + 1;

            activity1.TargetResources.Add(resourceId1);
            activity1.TargetResourceOperator = LogicalOperator.AND;

            activity2.TargetResources.Add(resourceId1);
            activity2.TargetResources.Add(resourceId2);
            activity2.TargetResourceOperator = LogicalOperator.OR;

            activity3.TargetResources.Add(resourceId1);
            activity3.TargetResources.Add(resourceId3);
            activity3.TargetResourceOperator = LogicalOperator.OR;

            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile();

            compilation.ResourceSchedules.ShouldNotBeEmpty();
            compilation.CompilationErrors.ShouldBeEmpty();

            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(1);

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.ShouldBeNull();
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(3);

            scheduledActivities0[0].Id.ShouldBe(activityId1);
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(6);

            scheduledActivities0[1].Id.ShouldBe(activityId2);
            scheduledActivities0[1].StartTime.ShouldBe(6);
            scheduledActivities0[1].FinishTime.ShouldBe(13);

            scheduledActivities0[2].Id.ShouldBe(activityId3);
            scheduledActivities0[2].StartTime.ShouldBe(13);
            scheduledActivities0[2].FinishTime.ShouldBe(17);

            scheduledActivities0.Last().FinishTime.ShouldBe(17);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithCompiledDependenciesAndFreeSlackUnlimitedResources_ThenResourceSchedulesCorrectOrder()
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
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            var graphBuilder = graphCompiler.Builder;
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId1, 6));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId2, 7));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId3, 8));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId4, 11, new HashSet<int> { activityId2 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId5, 8, new HashSet<int> { activityId1, activityId2, activityId3 }) { MinimumFreeSlack = 15 });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId6, 7, new HashSet<int> { activityId3 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId7, 4, new HashSet<int> { activityId4 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId8, 4, new HashSet<int> { activityId4, activityId6 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId9, 10, new HashSet<int> { activityId5 }));

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile();

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(3);

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.ShouldBeNull();
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(4);

            scheduledActivities0[0].Id.ShouldBe(activityId2);
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(7);

            scheduledActivities0[1].Id.ShouldBe(activityId4);
            scheduledActivities0[1].StartTime.ShouldBe(7);
            scheduledActivities0[1].FinishTime.ShouldBe(18);

            scheduledActivities0[2].Id.ShouldBe(activityId7);
            scheduledActivities0[2].StartTime.ShouldBe(18);
            scheduledActivities0[2].FinishTime.ShouldBe(22);

            scheduledActivities0[3].Id.ShouldBe(activityId9);
            scheduledActivities0[3].StartTime.ShouldBe(31);
            scheduledActivities0[3].FinishTime.ShouldBe(41);

            scheduledActivities0.Last().FinishTime.ShouldBe(41);


            var resourceSchedule1 = resourceSchedules[1];
            resourceSchedule1.Resource.ShouldBeNull();
            var scheduledActivities1 = resourceSchedule1.ScheduledActivities.ToList();
            scheduledActivities1.Count.ShouldBe(3);

            scheduledActivities1[0].Id.ShouldBe(activityId3);
            scheduledActivities1[0].StartTime.ShouldBe(0);
            scheduledActivities1[0].FinishTime.ShouldBe(8);

            scheduledActivities1[1].Id.ShouldBe(activityId6);
            scheduledActivities1[1].StartTime.ShouldBe(8);
            scheduledActivities1[1].FinishTime.ShouldBe(15);

            scheduledActivities1[2].Id.ShouldBe(activityId8);
            scheduledActivities1[2].StartTime.ShouldBe(18);
            scheduledActivities1[2].FinishTime.ShouldBe(22);

            scheduledActivities1.Last().FinishTime.ShouldBe(22);


            var resourceSchedule2 = resourceSchedules[2];
            resourceSchedule2.Resource.ShouldBeNull();
            var scheduledActivities2 = resourceSchedule2.ScheduledActivities.ToList();
            scheduledActivities2.Count.ShouldBe(2);

            scheduledActivities2[0].Id.ShouldBe(activityId1);
            scheduledActivities2[0].StartTime.ShouldBe(0);
            scheduledActivities2[0].FinishTime.ShouldBe(6);

            scheduledActivities2[1].Id.ShouldBe(activityId5);
            scheduledActivities2[1].StartTime.ShouldBe(8);
            scheduledActivities2[1].FinishTime.ShouldBe(16);

            scheduledActivities2.Last().FinishTime.ShouldBe(16);



            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(6);
            graphBuilder.Activity(activityId1).FreeSlack.ShouldBe(2);
            graphBuilder.Activity(activityId1).TotalSlack.ShouldBe(17);
            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(17);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(23);
            graphBuilder.Activity(activityId1).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId1).Successors.Contains(activityId5).ShouldBeTrue();

            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(7);
            graphBuilder.Activity(activityId2).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId2).TotalSlack.ShouldBe(9);
            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(9);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(16);
            graphBuilder.Activity(activityId2).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId2).Successors.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId2).Successors.Contains(activityId5).ShouldBeTrue();

            graphBuilder.Activity(activityId3).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).TotalSlack.ShouldBe(15);
            graphBuilder.Activity(activityId3).LatestStartTime.ShouldBe(15);
            graphBuilder.Activity(activityId3).LatestFinishTime.ShouldBe(23);
            graphBuilder.Activity(activityId3).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId3).Successors.Contains(activityId5).ShouldBeTrue();
            graphBuilder.Activity(activityId3).Successors.Contains(activityId6).ShouldBeTrue();

            graphBuilder.Activity(activityId4).EarliestStartTime.ShouldBe(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.ShouldBe(18);
            graphBuilder.Activity(activityId4).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).TotalSlack.ShouldBe(9);
            graphBuilder.Activity(activityId4).LatestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId4).LatestFinishTime.ShouldBe(27);
            graphBuilder.Activity(activityId4).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId4).Dependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId4).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId4).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId4).ResourceDependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId4).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId4).Successors.Contains(activityId7).ShouldBeTrue();
            graphBuilder.Activity(activityId4).Successors.Contains(activityId8).ShouldBeTrue();

            graphBuilder.Activity(activityId5).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId5).EarliestFinishTime.ShouldBe(16);
            graphBuilder.Activity(activityId5).FreeSlack.ShouldBe(15);
            graphBuilder.Activity(activityId5).TotalSlack.ShouldBe(15);
            graphBuilder.Activity(activityId5).LatestStartTime.ShouldBe(23);
            graphBuilder.Activity(activityId5).LatestFinishTime.ShouldBe(31);
            graphBuilder.Activity(activityId5).Dependencies.Count.ShouldBe(3);
            graphBuilder.Activity(activityId5).Dependencies.Contains(activityId1).ShouldBeTrue();
            graphBuilder.Activity(activityId5).Dependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId5).Dependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId5).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId5).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId5).ResourceDependencies.Contains(activityId1).ShouldBeTrue();
            graphBuilder.Activity(activityId5).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId5).Successors.Contains(activityId9).ShouldBeTrue();

            graphBuilder.Activity(activityId6).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.ShouldBe(15);
            graphBuilder.Activity(activityId6).FreeSlack.ShouldBe(3);
            graphBuilder.Activity(activityId6).TotalSlack.ShouldBe(22);
            graphBuilder.Activity(activityId6).LatestStartTime.ShouldBe(30);
            graphBuilder.Activity(activityId6).LatestFinishTime.ShouldBe(37);
            graphBuilder.Activity(activityId6).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).Dependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId6).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId6).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).ResourceDependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId6).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).Successors.Contains(activityId8).ShouldBeTrue();

            graphBuilder.Activity(activityId7).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId7).FreeSlack.ShouldBe(9);
            graphBuilder.Activity(activityId7).TotalSlack.ShouldBe(9);
            graphBuilder.Activity(activityId7).LatestStartTime.ShouldBe(27);
            graphBuilder.Activity(activityId7).LatestFinishTime.ShouldBe(31);
            graphBuilder.Activity(activityId7).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId7).Dependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId7).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId7).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId7).ResourceDependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId7).Successors.Count.ShouldBe(0);

            graphBuilder.Activity(activityId8).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId8).EarliestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId8).FreeSlack.ShouldBe(19);
            graphBuilder.Activity(activityId8).TotalSlack.ShouldBe(19);
            graphBuilder.Activity(activityId8).LatestStartTime.ShouldBe(37);
            graphBuilder.Activity(activityId8).LatestFinishTime.ShouldBe(41);
            graphBuilder.Activity(activityId8).Dependencies.Count.ShouldBe(2);
            graphBuilder.Activity(activityId8).Dependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId8).Dependencies.Contains(activityId6).ShouldBeTrue();
            graphBuilder.Activity(activityId8).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId8).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId8).ResourceDependencies.Contains(activityId6).ShouldBeTrue();
            graphBuilder.Activity(activityId8).Successors.Count.ShouldBe(0);

            graphBuilder.Activity(activityId9).EarliestStartTime.ShouldBe(31);
            graphBuilder.Activity(activityId9).EarliestFinishTime.ShouldBe(41);
            graphBuilder.Activity(activityId9).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).LatestStartTime.ShouldBe(31);
            graphBuilder.Activity(activityId9).LatestFinishTime.ShouldBe(41);
            graphBuilder.Activity(activityId9).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId9).Dependencies.Contains(activityId5).ShouldBeTrue();
            graphBuilder.Activity(activityId9).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId9).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId9).ResourceDependencies.Contains(activityId7).ShouldBeTrue();
            graphBuilder.Activity(activityId9).Successors.Count.ShouldBe(0);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithCompiledDependenciesAndTwoNoneAndDirectResources_ThenResourceSchedulesCorrectOrder()
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
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            var graphBuilder = graphCompiler.Builder;
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId1, 6));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId2, 7) { HasNoCost = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId3, 8) { HasNoCost = true, HasNoEffort = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId4, 11, new HashSet<int> { activityId2 }) { HasNoEffort = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId5, 8, new HashSet<int> { activityId1, activityId2, activityId3 }) { HasNoCost = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId6, 7, new HashSet<int> { activityId3 }) { HasNoCost = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId7, 4, new HashSet<int> { activityId4 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId8, 4, new HashSet<int> { activityId4, activityId6 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId9, 10, new HashSet<int> { activityId5 }));

            int resourceId1 = 1;
            int resourceId2 = resourceId1 + 1;
            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int, int>>(new[]
                {
                    new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>()),
                    new Resource<int, int>(resourceId2, string.Empty, false, false, InterActivityAllocationType.Direct, 1.0, 0, Enumerable.Empty<int>()),
                }));

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(2);

            resourceSchedules[0].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true,
                });

            resourceSchedules[0].CostAllocation.ShouldBe(
                new bool[] {
                    false, false, false, false, false, false, false, false, true, true,
                    true, true, true, true, true, true, true, true, true, false,
                    false, false, false, false, false, false, true, true, true, true,
                    true, true, true, true,
                });

            resourceSchedules[0].EffortAllocation.ShouldBe(
                new bool[] {
                    false, false, false, false, false, false, false, false, false, false,
                    false, false, false, false, false, false, false, false, false, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(5);

            scheduledActivities0[0].Id.ShouldBe(activityId3);
            scheduledActivities0[0].HasNoCost.ShouldBeTrue();
            scheduledActivities0[0].HasNoEffort.ShouldBeTrue();
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(8);

            scheduledActivities0[1].Id.ShouldBe(activityId4);
            scheduledActivities0[1].HasNoCost.ShouldBeFalse();
            scheduledActivities0[1].HasNoEffort.ShouldBeTrue();
            scheduledActivities0[1].StartTime.ShouldBe(8);
            scheduledActivities0[1].FinishTime.ShouldBe(19);

            scheduledActivities0[2].Id.ShouldBe(activityId6);
            scheduledActivities0[2].HasNoCost.ShouldBeTrue();
            scheduledActivities0[2].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[2].StartTime.ShouldBe(19);
            scheduledActivities0[2].FinishTime.ShouldBe(26);

            scheduledActivities0[3].Id.ShouldBe(activityId7);
            scheduledActivities0[3].HasNoCost.ShouldBeFalse();
            scheduledActivities0[3].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[3].StartTime.ShouldBe(26);
            scheduledActivities0[3].FinishTime.ShouldBe(30);

            scheduledActivities0[4].Id.ShouldBe(activityId8);
            scheduledActivities0[4].HasNoCost.ShouldBeFalse();
            scheduledActivities0[4].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[4].StartTime.ShouldBe(30);
            scheduledActivities0[4].FinishTime.ShouldBe(34);

            scheduledActivities0.Last().FinishTime.ShouldBe(34);


            resourceSchedules[1].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, false, false, false,
                });

            resourceSchedules[1].CostAllocation.ShouldBe(
                new bool[] {
                    false, false, false, false, false, false, false, true, true, true,
                    true, true, true, false, false, false, false, false, false, false,
                    false, true, true, true, true, true, true, true, true, true,
                    true, false, false, false,
                });

            resourceSchedules[1].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, false, false, false,
                });

            var resourceSchedule1 = resourceSchedules[1];
            resourceSchedule1.Resource.Id.ShouldBe(resourceId2);
            var scheduledActivities1 = resourceSchedule1.ScheduledActivities.ToList();
            scheduledActivities1.Count.ShouldBe(4);

            scheduledActivities1[0].Id.ShouldBe(activityId2);
            scheduledActivities1[0].HasNoCost.ShouldBeTrue();
            scheduledActivities1[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities1[0].StartTime.ShouldBe(0);
            scheduledActivities1[0].FinishTime.ShouldBe(7);

            scheduledActivities1[1].Id.ShouldBe(activityId1);
            scheduledActivities1[1].HasNoCost.ShouldBeFalse();
            scheduledActivities1[1].HasNoEffort.ShouldBeFalse();
            scheduledActivities1[1].StartTime.ShouldBe(7);
            scheduledActivities1[1].FinishTime.ShouldBe(13);

            scheduledActivities1[2].Id.ShouldBe(activityId5);
            scheduledActivities1[2].HasNoCost.ShouldBeTrue();
            scheduledActivities1[2].HasNoEffort.ShouldBeFalse();
            scheduledActivities1[2].StartTime.ShouldBe(13);
            scheduledActivities1[2].FinishTime.ShouldBe(21);

            scheduledActivities1[3].Id.ShouldBe(activityId9);
            scheduledActivities1[3].HasNoCost.ShouldBeFalse();
            scheduledActivities1[3].HasNoEffort.ShouldBeFalse();
            scheduledActivities1[3].StartTime.ShouldBe(21);
            scheduledActivities1[3].FinishTime.ShouldBe(31);

            scheduledActivities1.Last().FinishTime.ShouldBe(31);



            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(7);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(13);
            graphBuilder.Activity(activityId1).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId1).TotalSlack.ShouldBe(3);
            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(10);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(16);
            graphBuilder.Activity(activityId1).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId1).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId1).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId1).Successors.Contains(activityId5).ShouldBeTrue();

            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(7);
            graphBuilder.Activity(activityId2).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId2).TotalSlack.ShouldBe(1);
            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(1);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId2).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId2).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId2).Successors.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId2).Successors.Contains(activityId5).ShouldBeTrue();

            graphBuilder.Activity(activityId3).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId3).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId3).Successors.Contains(activityId5).ShouldBeTrue();
            graphBuilder.Activity(activityId3).Successors.Contains(activityId6).ShouldBeTrue();

            graphBuilder.Activity(activityId4).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId4).EarliestFinishTime.ShouldBe(19);
            graphBuilder.Activity(activityId4).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).LatestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId4).LatestFinishTime.ShouldBe(19);
            graphBuilder.Activity(activityId4).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId4).Dependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId4).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId4).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId3 }), ignoreOrder: true);
            graphBuilder.Activity(activityId4).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId4).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId4).Successors.Contains(activityId7).ShouldBeTrue();
            graphBuilder.Activity(activityId4).Successors.Contains(activityId8).ShouldBeTrue();

            graphBuilder.Activity(activityId5).EarliestStartTime.ShouldBe(13);
            graphBuilder.Activity(activityId5).EarliestFinishTime.ShouldBe(21);
            graphBuilder.Activity(activityId5).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).TotalSlack.ShouldBe(3);
            graphBuilder.Activity(activityId5).LatestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId5).LatestFinishTime.ShouldBe(24);
            graphBuilder.Activity(activityId5).Dependencies.Count.ShouldBe(3);
            graphBuilder.Activity(activityId5).Dependencies.Contains(activityId1).ShouldBeTrue();
            graphBuilder.Activity(activityId5).Dependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId5).Dependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId5).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId5).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId5).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId5).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId5).Successors.Contains(activityId9).ShouldBeTrue();

            graphBuilder.Activity(activityId6).EarliestStartTime.ShouldBe(19);
            graphBuilder.Activity(activityId6).EarliestFinishTime.ShouldBe(26);
            graphBuilder.Activity(activityId6).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId6).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId6).LatestStartTime.ShouldBe(19);
            graphBuilder.Activity(activityId6).LatestFinishTime.ShouldBe(26);
            graphBuilder.Activity(activityId6).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).Dependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId6).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId6).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId4 }), ignoreOrder: true);
            graphBuilder.Activity(activityId6).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId6).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).Successors.Contains(activityId8).ShouldBeTrue();

            graphBuilder.Activity(activityId7).EarliestStartTime.ShouldBe(26);
            graphBuilder.Activity(activityId7).EarliestFinishTime.ShouldBe(30);
            graphBuilder.Activity(activityId7).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId7).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId7).LatestStartTime.ShouldBe(26);
            graphBuilder.Activity(activityId7).LatestFinishTime.ShouldBe(30);
            graphBuilder.Activity(activityId7).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId7).Dependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId7).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId7).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId6 }), ignoreOrder: true);
            graphBuilder.Activity(activityId7).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId7).Successors.Count.ShouldBe(0);

            graphBuilder.Activity(activityId8).EarliestStartTime.ShouldBe(30);
            graphBuilder.Activity(activityId8).EarliestFinishTime.ShouldBe(34);
            graphBuilder.Activity(activityId8).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId8).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId8).LatestStartTime.ShouldBe(30);
            graphBuilder.Activity(activityId8).LatestFinishTime.ShouldBe(34);
            graphBuilder.Activity(activityId8).Dependencies.Count.ShouldBe(2);
            graphBuilder.Activity(activityId8).Dependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId8).Dependencies.Contains(activityId6).ShouldBeTrue();
            graphBuilder.Activity(activityId8).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId8).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId7 }), ignoreOrder: true);
            graphBuilder.Activity(activityId8).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId8).Successors.Count.ShouldBe(0);

            graphBuilder.Activity(activityId9).EarliestStartTime.ShouldBe(21);
            graphBuilder.Activity(activityId9).EarliestFinishTime.ShouldBe(31);
            graphBuilder.Activity(activityId9).FreeSlack.ShouldBe(3);
            graphBuilder.Activity(activityId9).TotalSlack.ShouldBe(3);
            graphBuilder.Activity(activityId9).LatestStartTime.ShouldBe(24);
            graphBuilder.Activity(activityId9).LatestFinishTime.ShouldBe(34);
            graphBuilder.Activity(activityId9).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId9).Dependencies.Contains(activityId5).ShouldBeTrue();
            graphBuilder.Activity(activityId9).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId9).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId5 }), ignoreOrder: true);
            graphBuilder.Activity(activityId9).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId9).Successors.Count.ShouldBe(0);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithCompiledDependenciesAndOneActiveAndTwoInactiveResources_ThenResourceSchedulesCorrectOrder()
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
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            var graphBuilder = graphCompiler.Builder;
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId1, 6));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId2, 7) { HasNoCost = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId3, 8) { HasNoCost = true, HasNoEffort = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId4, 11, new HashSet<int> { activityId2 }) { HasNoEffort = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId5, 8, new HashSet<int> { activityId1, activityId2, activityId3 }) { HasNoCost = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId6, 7, new HashSet<int> { activityId3 }) { HasNoCost = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId7, 4, new HashSet<int> { activityId4 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId8, 4, new HashSet<int> { activityId4, activityId6 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId9, 10, new HashSet<int> { activityId5 }));

            int resourceId1 = 1;
            int resourceId2 = resourceId1 + 1;
            int resourceId3 = resourceId2 + 1;

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int, int>>(new[]
                {
                    new Resource<int, int>(resourceId1, string.Empty, false, true, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>()),
                    new Resource<int, int>(resourceId2, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>()),
                    new Resource<int, int>(resourceId3, string.Empty, false, true, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>()),
                }));

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(1);

            resourceSchedules[0].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true,
                });

            resourceSchedules[0].CostAllocation.ShouldBe(
                new bool[] {
                    false, false, false, false, false, false, false, false, false, false,
                    false, false, false, false, false, true, true, true, true, true,
                    true, false, false, false, false, false, false, false, false, true,
                    true, true, true, true, true, true, true, true, true, true,
                    false, false, false, false, false, false, false, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true,
                });

            resourceSchedules[0].EffortAllocation.ShouldBe(
                new bool[] {
                    false, false, false, false, false, false, false, false, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, false,
                    false, false, false, false, false, false, false, false, false, false,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId2);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(9);

            scheduledActivities0[0].Id.ShouldBe(activityId3);
            scheduledActivities0[0].HasNoCost.ShouldBeTrue();
            scheduledActivities0[0].HasNoEffort.ShouldBeTrue();
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(8);

            scheduledActivities0[1].Id.ShouldBe(activityId2);
            scheduledActivities0[1].HasNoCost.ShouldBeTrue();
            scheduledActivities0[1].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[1].StartTime.ShouldBe(8);
            scheduledActivities0[1].FinishTime.ShouldBe(15);

            scheduledActivities0[2].Id.ShouldBe(activityId1);
            scheduledActivities0[2].HasNoCost.ShouldBeFalse();
            scheduledActivities0[2].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[2].StartTime.ShouldBe(15);
            scheduledActivities0[2].FinishTime.ShouldBe(21);

            scheduledActivities0[3].Id.ShouldBe(activityId5);
            scheduledActivities0[3].HasNoCost.ShouldBeTrue();
            scheduledActivities0[3].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[3].StartTime.ShouldBe(21);
            scheduledActivities0[3].FinishTime.ShouldBe(29);

            scheduledActivities0[4].Id.ShouldBe(activityId4);
            scheduledActivities0[4].HasNoCost.ShouldBeFalse();
            scheduledActivities0[4].HasNoEffort.ShouldBeTrue();
            scheduledActivities0[4].StartTime.ShouldBe(29);
            scheduledActivities0[4].FinishTime.ShouldBe(40);

            scheduledActivities0[5].Id.ShouldBe(activityId6);
            scheduledActivities0[5].HasNoCost.ShouldBeTrue();
            scheduledActivities0[5].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[5].StartTime.ShouldBe(40);
            scheduledActivities0[5].FinishTime.ShouldBe(47);

            scheduledActivities0[6].Id.ShouldBe(activityId9);
            scheduledActivities0[6].HasNoCost.ShouldBeFalse();
            scheduledActivities0[6].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[6].StartTime.ShouldBe(47);
            scheduledActivities0[6].FinishTime.ShouldBe(57);

            scheduledActivities0[7].Id.ShouldBe(activityId7);
            scheduledActivities0[7].HasNoCost.ShouldBeFalse();
            scheduledActivities0[7].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[7].StartTime.ShouldBe(57);
            scheduledActivities0[7].FinishTime.ShouldBe(61);

            scheduledActivities0[8].Id.ShouldBe(activityId8);
            scheduledActivities0[8].HasNoCost.ShouldBeFalse();
            scheduledActivities0[8].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[8].StartTime.ShouldBe(61);
            scheduledActivities0[8].FinishTime.ShouldBe(65);

            scheduledActivities0.Last().FinishTime.ShouldBe(65);

            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(15);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(21);
            graphBuilder.Activity(activityId1).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId1).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(15);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(21);
            graphBuilder.Activity(activityId1).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId1).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId1).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId1).Successors.Contains(activityId5).ShouldBeTrue();

            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(15);
            graphBuilder.Activity(activityId2).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId2).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(15);
            graphBuilder.Activity(activityId2).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId3 }), ignoreOrder: true);
            graphBuilder.Activity(activityId2).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId2).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId2).Successors.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId2).Successors.Contains(activityId5).ShouldBeTrue();

            graphBuilder.Activity(activityId3).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId3).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId3).Successors.Contains(activityId5).ShouldBeTrue();
            graphBuilder.Activity(activityId3).Successors.Contains(activityId6).ShouldBeTrue();

            graphBuilder.Activity(activityId4).EarliestStartTime.ShouldBe(29);
            graphBuilder.Activity(activityId4).EarliestFinishTime.ShouldBe(40);
            graphBuilder.Activity(activityId4).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).LatestStartTime.ShouldBe(29);
            graphBuilder.Activity(activityId4).LatestFinishTime.ShouldBe(40);
            graphBuilder.Activity(activityId4).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId4).Dependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId4).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId4).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId5 }), ignoreOrder: true);
            graphBuilder.Activity(activityId4).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId4).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId4).Successors.Contains(activityId7).ShouldBeTrue();
            graphBuilder.Activity(activityId4).Successors.Contains(activityId8).ShouldBeTrue();

            graphBuilder.Activity(activityId5).EarliestStartTime.ShouldBe(21);
            graphBuilder.Activity(activityId5).EarliestFinishTime.ShouldBe(29);
            graphBuilder.Activity(activityId5).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).LatestStartTime.ShouldBe(21);
            graphBuilder.Activity(activityId5).LatestFinishTime.ShouldBe(29);
            graphBuilder.Activity(activityId5).Dependencies.Count.ShouldBe(3);
            graphBuilder.Activity(activityId5).Dependencies.Contains(activityId1).ShouldBeTrue();
            graphBuilder.Activity(activityId5).Dependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId5).Dependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId5).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId5).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId5).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId5).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId5).Successors.Contains(activityId9).ShouldBeTrue();

            graphBuilder.Activity(activityId6).EarliestStartTime.ShouldBe(40);
            graphBuilder.Activity(activityId6).EarliestFinishTime.ShouldBe(47);
            graphBuilder.Activity(activityId6).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId6).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId6).LatestStartTime.ShouldBe(40);
            graphBuilder.Activity(activityId6).LatestFinishTime.ShouldBe(47);
            graphBuilder.Activity(activityId6).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).Dependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId6).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId6).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId4 }), ignoreOrder: true);
            graphBuilder.Activity(activityId6).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId6).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).Successors.Contains(activityId8).ShouldBeTrue();

            graphBuilder.Activity(activityId7).EarliestStartTime.ShouldBe(57);
            graphBuilder.Activity(activityId7).EarliestFinishTime.ShouldBe(61);
            graphBuilder.Activity(activityId7).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId7).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId7).LatestStartTime.ShouldBe(57);
            graphBuilder.Activity(activityId7).LatestFinishTime.ShouldBe(61);
            graphBuilder.Activity(activityId7).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId7).Dependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId7).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId7).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId9 }), ignoreOrder: true);
            graphBuilder.Activity(activityId7).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId7).Successors.Count.ShouldBe(0);

            graphBuilder.Activity(activityId8).EarliestStartTime.ShouldBe(61);
            graphBuilder.Activity(activityId8).EarliestFinishTime.ShouldBe(65);
            graphBuilder.Activity(activityId8).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId8).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId8).LatestStartTime.ShouldBe(61);
            graphBuilder.Activity(activityId8).LatestFinishTime.ShouldBe(65);
            graphBuilder.Activity(activityId8).Dependencies.Count.ShouldBe(2);
            graphBuilder.Activity(activityId8).Dependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId8).Dependencies.Contains(activityId6).ShouldBeTrue();
            graphBuilder.Activity(activityId8).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId8).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId7 }), ignoreOrder: true);
            graphBuilder.Activity(activityId8).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId8).Successors.Count.ShouldBe(0);

            graphBuilder.Activity(activityId9).EarliestStartTime.ShouldBe(47);
            graphBuilder.Activity(activityId9).EarliestFinishTime.ShouldBe(57);
            graphBuilder.Activity(activityId9).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).LatestStartTime.ShouldBe(47);
            graphBuilder.Activity(activityId9).LatestFinishTime.ShouldBe(57);
            graphBuilder.Activity(activityId9).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId9).Dependencies.Contains(activityId5).ShouldBeTrue();
            graphBuilder.Activity(activityId9).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId9).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId6 }), ignoreOrder: true);
            graphBuilder.Activity(activityId9).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId9).Successors.Count.ShouldBe(0);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithCompiledDependenciesAndTwoIndirectResources_ThenResourceSchedulesCorrectOrder()
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
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            var graphBuilder = graphCompiler.Builder;
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId1, 6));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId2, 7));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId3, 8) { HasNoCost = true, HasNoEffort = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId4, 11, new HashSet<int> { activityId2 }) { HasNoEffort = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId5, 8, new HashSet<int> { activityId1, activityId2, activityId3 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId6, 7, new HashSet<int> { activityId3 }) { HasNoCost = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId7, 4, new HashSet<int> { activityId4 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId8, 4, new HashSet<int> { activityId4, activityId6 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId9, 10, new HashSet<int> { activityId5 }));

            int resourceId1 = 1;
            int resourceId2 = resourceId1 + 1;
            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int, int>>(new[]
                {
                    new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.Indirect, 1.0, 0, Enumerable.Empty<int>()),
                    new Resource<int, int>(resourceId2, string.Empty, false, false, InterActivityAllocationType.Indirect, 1.0, 0, Enumerable.Empty<int>()),
                }));

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(2);

            resourceSchedules[0].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true,
                });

            resourceSchedules[0].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true,
                });

            resourceSchedules[0].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(5);

            scheduledActivities0[0].Id.ShouldBe(activityId3);
            scheduledActivities0[0].HasNoCost.ShouldBeTrue();
            scheduledActivities0[0].HasNoEffort.ShouldBeTrue();
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(8);

            scheduledActivities0[1].Id.ShouldBe(activityId4);
            scheduledActivities0[1].HasNoCost.ShouldBeFalse();
            scheduledActivities0[1].HasNoEffort.ShouldBeTrue();
            scheduledActivities0[1].StartTime.ShouldBe(8);
            scheduledActivities0[1].FinishTime.ShouldBe(19);

            scheduledActivities0[2].Id.ShouldBe(activityId6);
            scheduledActivities0[2].HasNoCost.ShouldBeTrue();
            scheduledActivities0[2].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[2].StartTime.ShouldBe(19);
            scheduledActivities0[2].FinishTime.ShouldBe(26);

            scheduledActivities0[3].Id.ShouldBe(activityId7);
            scheduledActivities0[3].HasNoCost.ShouldBeFalse();
            scheduledActivities0[3].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[3].StartTime.ShouldBe(26);
            scheduledActivities0[3].FinishTime.ShouldBe(30);

            scheduledActivities0[4].Id.ShouldBe(activityId8);
            scheduledActivities0[4].HasNoCost.ShouldBeFalse();
            scheduledActivities0[4].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[4].StartTime.ShouldBe(30);
            scheduledActivities0[4].FinishTime.ShouldBe(34);

            scheduledActivities0.Last().FinishTime.ShouldBe(34);


            resourceSchedules[1].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true,
                });

            resourceSchedules[1].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true,
                });

            resourceSchedules[1].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true,
                });

            var resourceSchedule1 = resourceSchedules[1];
            resourceSchedule1.Resource.Id.ShouldBe(resourceId2);
            var scheduledActivities1 = resourceSchedule1.ScheduledActivities.ToList();
            scheduledActivities1.Count.ShouldBe(4);

            scheduledActivities1[0].Id.ShouldBe(activityId2);
            scheduledActivities1[0].HasNoCost.ShouldBeFalse();
            scheduledActivities1[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities1[0].StartTime.ShouldBe(0);
            scheduledActivities1[0].FinishTime.ShouldBe(7);

            scheduledActivities1[1].Id.ShouldBe(activityId1);
            scheduledActivities1[1].HasNoCost.ShouldBeFalse();
            scheduledActivities1[1].HasNoEffort.ShouldBeFalse();
            scheduledActivities1[1].StartTime.ShouldBe(7);
            scheduledActivities1[1].FinishTime.ShouldBe(13);

            scheduledActivities1[2].Id.ShouldBe(activityId5);
            scheduledActivities1[2].HasNoCost.ShouldBeFalse();
            scheduledActivities1[2].HasNoEffort.ShouldBeFalse();
            scheduledActivities1[2].StartTime.ShouldBe(13);
            scheduledActivities1[2].FinishTime.ShouldBe(21);

            scheduledActivities1[3].Id.ShouldBe(activityId9);
            scheduledActivities1[3].HasNoCost.ShouldBeFalse();
            scheduledActivities1[3].HasNoEffort.ShouldBeFalse();
            scheduledActivities1[3].StartTime.ShouldBe(21);
            scheduledActivities1[3].FinishTime.ShouldBe(31);

            scheduledActivities1.Last().FinishTime.ShouldBe(31);



            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(7);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(13);
            graphBuilder.Activity(activityId1).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId1).TotalSlack.ShouldBe(3);
            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(10);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(16);
            graphBuilder.Activity(activityId1).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId1).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId1).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId1).Successors.Contains(activityId5).ShouldBeTrue();

            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(7);
            graphBuilder.Activity(activityId2).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId2).TotalSlack.ShouldBe(1);
            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(1);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId2).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId2).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId2).Successors.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId2).Successors.Contains(activityId5).ShouldBeTrue();

            graphBuilder.Activity(activityId3).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId3).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId3).Successors.Contains(activityId5).ShouldBeTrue();
            graphBuilder.Activity(activityId3).Successors.Contains(activityId6).ShouldBeTrue();

            graphBuilder.Activity(activityId4).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId4).EarliestFinishTime.ShouldBe(19);
            graphBuilder.Activity(activityId4).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).LatestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId4).LatestFinishTime.ShouldBe(19);
            graphBuilder.Activity(activityId4).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId4).Dependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId4).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId4).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId3 }), ignoreOrder: true);
            graphBuilder.Activity(activityId4).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId4).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId4).Successors.Contains(activityId7).ShouldBeTrue();
            graphBuilder.Activity(activityId4).Successors.Contains(activityId8).ShouldBeTrue();

            graphBuilder.Activity(activityId5).EarliestStartTime.ShouldBe(13);
            graphBuilder.Activity(activityId5).EarliestFinishTime.ShouldBe(21);
            graphBuilder.Activity(activityId5).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).TotalSlack.ShouldBe(3);
            graphBuilder.Activity(activityId5).LatestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId5).LatestFinishTime.ShouldBe(24);
            graphBuilder.Activity(activityId5).Dependencies.Count.ShouldBe(3);
            graphBuilder.Activity(activityId5).Dependencies.Contains(activityId1).ShouldBeTrue();
            graphBuilder.Activity(activityId5).Dependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId5).Dependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId5).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId5).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId5).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId5).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId5).Successors.Contains(activityId9).ShouldBeTrue();

            graphBuilder.Activity(activityId6).EarliestStartTime.ShouldBe(19);
            graphBuilder.Activity(activityId6).EarliestFinishTime.ShouldBe(26);
            graphBuilder.Activity(activityId6).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId6).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId6).LatestStartTime.ShouldBe(19);
            graphBuilder.Activity(activityId6).LatestFinishTime.ShouldBe(26);
            graphBuilder.Activity(activityId6).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).Dependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId6).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId6).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId4 }), ignoreOrder: true);
            graphBuilder.Activity(activityId6).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId6).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).Successors.Contains(activityId8).ShouldBeTrue();

            graphBuilder.Activity(activityId7).EarliestStartTime.ShouldBe(26);
            graphBuilder.Activity(activityId7).EarliestFinishTime.ShouldBe(30);
            graphBuilder.Activity(activityId7).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId7).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId7).LatestStartTime.ShouldBe(26);
            graphBuilder.Activity(activityId7).LatestFinishTime.ShouldBe(30);
            graphBuilder.Activity(activityId7).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId7).Dependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId7).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId7).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId6 }), ignoreOrder: true);
            graphBuilder.Activity(activityId7).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId7).Successors.Count.ShouldBe(0);

            graphBuilder.Activity(activityId8).EarliestStartTime.ShouldBe(30);
            graphBuilder.Activity(activityId8).EarliestFinishTime.ShouldBe(34);
            graphBuilder.Activity(activityId8).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId8).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId8).LatestStartTime.ShouldBe(30);
            graphBuilder.Activity(activityId8).LatestFinishTime.ShouldBe(34);
            graphBuilder.Activity(activityId8).Dependencies.Count.ShouldBe(2);
            graphBuilder.Activity(activityId8).Dependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId8).Dependencies.Contains(activityId6).ShouldBeTrue();
            graphBuilder.Activity(activityId8).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId8).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId7 }), ignoreOrder: true);
            graphBuilder.Activity(activityId8).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId8).Successors.Count.ShouldBe(0);

            graphBuilder.Activity(activityId9).EarliestStartTime.ShouldBe(21);
            graphBuilder.Activity(activityId9).EarliestFinishTime.ShouldBe(31);
            graphBuilder.Activity(activityId9).FreeSlack.ShouldBe(3);
            graphBuilder.Activity(activityId9).TotalSlack.ShouldBe(3);
            graphBuilder.Activity(activityId9).LatestStartTime.ShouldBe(24);
            graphBuilder.Activity(activityId9).LatestFinishTime.ShouldBe(34);
            graphBuilder.Activity(activityId9).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId9).Dependencies.Contains(activityId5).ShouldBeTrue();
            graphBuilder.Activity(activityId9).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId9).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId5 }), ignoreOrder: true);
            graphBuilder.Activity(activityId9).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId9).Successors.Count.ShouldBe(0);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithPlanningDependenciesAndUnlimitedResources_ThenResourceSchedulesCorrectOrder()
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
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            var graphBuilder = graphCompiler.Builder;
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId1, 6));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId2, 7));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId3, 8));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId4, 11, new HashSet<int>(), new HashSet<int> { activityId2 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId5, 8, new HashSet<int>(), new HashSet<int> { activityId1, activityId2, activityId3 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId6, 7, new HashSet<int>(), new HashSet<int> { activityId3 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId7, 4, new HashSet<int>(), new HashSet<int> { activityId4 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId8, 4, new HashSet<int>(), new HashSet<int> { activityId4, activityId6 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId9, 10, new HashSet<int>(), new HashSet<int> { activityId5 }));

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile();

            compilation.CompilationErrors.ShouldBeEmpty();

            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(3);

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.ShouldBeNull();
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(3);

            scheduledActivities0[0].Id.ShouldBe(activityId3);
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(8);

            scheduledActivities0[1].Id.ShouldBe(activityId5);
            scheduledActivities0[1].StartTime.ShouldBe(8);
            scheduledActivities0[1].FinishTime.ShouldBe(16);

            scheduledActivities0[2].Id.ShouldBe(activityId9);
            scheduledActivities0[2].StartTime.ShouldBe(16);
            scheduledActivities0[2].FinishTime.ShouldBe(26);

            scheduledActivities0.Last().FinishTime.ShouldBe(26);


            var resourceSchedule1 = resourceSchedules[1];
            resourceSchedule1.Resource.ShouldBeNull();
            var scheduledActivities1 = resourceSchedule1.ScheduledActivities.ToList();
            scheduledActivities1.Count.ShouldBe(3);

            scheduledActivities1[0].Id.ShouldBe(activityId2);
            scheduledActivities1[0].StartTime.ShouldBe(0);
            scheduledActivities1[0].FinishTime.ShouldBe(7);

            scheduledActivities1[1].Id.ShouldBe(activityId4);
            scheduledActivities1[1].StartTime.ShouldBe(7);
            scheduledActivities1[1].FinishTime.ShouldBe(18);

            scheduledActivities1[2].Id.ShouldBe(activityId7);
            scheduledActivities1[2].StartTime.ShouldBe(18);
            scheduledActivities1[2].FinishTime.ShouldBe(22);

            scheduledActivities1.Last().FinishTime.ShouldBe(22);


            var resourceSchedule2 = resourceSchedules[2];
            resourceSchedule2.Resource.ShouldBeNull();
            var scheduledActivities2 = resourceSchedule2.ScheduledActivities.ToList();
            scheduledActivities2.Count.ShouldBe(3);

            scheduledActivities2[0].Id.ShouldBe(activityId1);
            scheduledActivities2[0].StartTime.ShouldBe(0);
            scheduledActivities2[0].FinishTime.ShouldBe(6);

            scheduledActivities2[1].Id.ShouldBe(activityId6);
            scheduledActivities2[1].StartTime.ShouldBe(8);
            scheduledActivities2[1].FinishTime.ShouldBe(15);

            scheduledActivities2[2].Id.ShouldBe(activityId8);
            scheduledActivities2[2].StartTime.ShouldBe(18);
            scheduledActivities2[2].FinishTime.ShouldBe(22);

            scheduledActivities2.Last().FinishTime.ShouldBe(22);



            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(6);
            graphBuilder.Activity(activityId1).FreeSlack.ShouldBe(2);
            graphBuilder.Activity(activityId1).TotalSlack.ShouldBe(2);
            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(2);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId1).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId1).Successors.Contains(activityId5).ShouldBeTrue();

            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(7);
            graphBuilder.Activity(activityId2).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId2).TotalSlack.ShouldBe(1);
            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(1);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId2).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId2).Successors.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId2).Successors.Contains(activityId5).ShouldBeTrue();

            graphBuilder.Activity(activityId3).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId3).Successors.Contains(activityId5).ShouldBeTrue();
            graphBuilder.Activity(activityId3).Successors.Contains(activityId6).ShouldBeTrue();

            graphBuilder.Activity(activityId4).EarliestStartTime.ShouldBe(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.ShouldBe(18);
            graphBuilder.Activity(activityId4).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).TotalSlack.ShouldBe(4);
            graphBuilder.Activity(activityId4).LatestStartTime.ShouldBe(11);
            graphBuilder.Activity(activityId4).LatestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId4).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId4).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId4).PlanningDependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId4).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId4).ResourceDependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId4).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId4).Successors.Contains(activityId7).ShouldBeTrue();
            graphBuilder.Activity(activityId4).Successors.Contains(activityId8).ShouldBeTrue();

            graphBuilder.Activity(activityId5).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId5).EarliestFinishTime.ShouldBe(16);
            graphBuilder.Activity(activityId5).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).LatestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId5).LatestFinishTime.ShouldBe(16);
            graphBuilder.Activity(activityId5).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId5).PlanningDependencies.Count.ShouldBe(3);
            graphBuilder.Activity(activityId5).PlanningDependencies.Contains(activityId1).ShouldBeTrue();
            graphBuilder.Activity(activityId5).PlanningDependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId5).PlanningDependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId5).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId5).ResourceDependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId5).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId5).Successors.Contains(activityId9).ShouldBeTrue();

            graphBuilder.Activity(activityId6).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.ShouldBe(15);
            graphBuilder.Activity(activityId6).FreeSlack.ShouldBe(3);
            graphBuilder.Activity(activityId6).TotalSlack.ShouldBe(7);
            graphBuilder.Activity(activityId6).LatestStartTime.ShouldBe(15);
            graphBuilder.Activity(activityId6).LatestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId6).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId6).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).PlanningDependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId6).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).ResourceDependencies.Contains(activityId1).ShouldBeTrue();
            graphBuilder.Activity(activityId6).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).Successors.Contains(activityId8).ShouldBeTrue();

            graphBuilder.Activity(activityId7).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId7).FreeSlack.ShouldBe(4);
            graphBuilder.Activity(activityId7).TotalSlack.ShouldBe(4);
            graphBuilder.Activity(activityId7).LatestStartTime.ShouldBe(22);
            graphBuilder.Activity(activityId7).LatestFinishTime.ShouldBe(26);
            graphBuilder.Activity(activityId7).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId7).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId7).PlanningDependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId7).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId7).ResourceDependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId7).Successors.Count.ShouldBe(0);

            graphBuilder.Activity(activityId8).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId8).EarliestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId8).FreeSlack.ShouldBe(4);
            graphBuilder.Activity(activityId8).TotalSlack.ShouldBe(4);
            graphBuilder.Activity(activityId8).LatestStartTime.ShouldBe(22);
            graphBuilder.Activity(activityId8).LatestFinishTime.ShouldBe(26);
            graphBuilder.Activity(activityId8).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId8).PlanningDependencies.Count.ShouldBe(2);
            graphBuilder.Activity(activityId8).PlanningDependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId8).PlanningDependencies.Contains(activityId6).ShouldBeTrue();
            graphBuilder.Activity(activityId8).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId8).ResourceDependencies.Contains(activityId6).ShouldBeTrue();
            graphBuilder.Activity(activityId8).Successors.Count.ShouldBe(0);

            graphBuilder.Activity(activityId9).EarliestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId9).EarliestFinishTime.ShouldBe(26);
            graphBuilder.Activity(activityId9).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).LatestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId9).LatestFinishTime.ShouldBe(26);
            graphBuilder.Activity(activityId9).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId9).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId9).PlanningDependencies.Contains(activityId5).ShouldBeTrue();
            graphBuilder.Activity(activityId9).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId9).ResourceDependencies.Contains(activityId5).ShouldBeTrue();
            graphBuilder.Activity(activityId9).Successors.Count.ShouldBe(0);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithPlanningDependenciesAndUnlimitedResourcesAndTargetResources_ThenResourceSchedulesCorrectOrder()
        {
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();

            var activity1 = new DependentActivity<int, int, int>(activityId1, 6);
            var activity2 = new DependentActivity<int, int, int>(activityId2, 7, new HashSet<int>(), new HashSet<int> { activityId1 });
            var activity3 = new DependentActivity<int, int, int>(activityId3, 4, new HashSet<int>(), new HashSet<int> { activityId2 });

            int resourceId1 = 1;
            int resourceId2 = resourceId1 + 1;
            int resourceId3 = resourceId2 + 1;

            activity1.TargetResources.Add(resourceId1);
            activity1.TargetResourceOperator = LogicalOperator.AND;

            activity2.TargetResources.Add(resourceId1);
            activity2.TargetResources.Add(resourceId2);
            activity2.TargetResourceOperator = LogicalOperator.OR;

            activity3.TargetResources.Add(resourceId1);
            activity3.TargetResources.Add(resourceId3);
            activity3.TargetResourceOperator = LogicalOperator.OR;

            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile();

            compilation.ResourceSchedules.ShouldNotBeEmpty();
            compilation.CompilationErrors.ShouldBeEmpty();

            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(1);

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.ShouldBeNull();
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(3);

            scheduledActivities0[0].Id.ShouldBe(activityId1);
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(6);

            scheduledActivities0[1].Id.ShouldBe(activityId2);
            scheduledActivities0[1].StartTime.ShouldBe(6);
            scheduledActivities0[1].FinishTime.ShouldBe(13);

            scheduledActivities0[2].Id.ShouldBe(activityId3);
            scheduledActivities0[2].StartTime.ShouldBe(13);
            scheduledActivities0[2].FinishTime.ShouldBe(17);

            scheduledActivities0.Last().FinishTime.ShouldBe(17);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithPlanningDependenciesAndFreeSlackUnlimitedResources_ThenResourceSchedulesCorrectOrder()
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
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            var graphBuilder = graphCompiler.Builder;
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId1, 6));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId2, 7));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId3, 8));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId4, 11, new HashSet<int>(), new HashSet<int> { activityId2 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId5, 8, new HashSet<int>(), new HashSet<int> { activityId1, activityId2, activityId3 }) { MinimumFreeSlack = 15 });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId6, 7, new HashSet<int>(), new HashSet<int> { activityId3 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId7, 4, new HashSet<int>(), new HashSet<int> { activityId4 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId8, 4, new HashSet<int>(), new HashSet<int> { activityId4, activityId6 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId9, 10, new HashSet<int>(), new HashSet<int> { activityId5 }));

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile();

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(3);

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.ShouldBeNull();
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(4);

            scheduledActivities0[0].Id.ShouldBe(activityId2);
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(7);

            scheduledActivities0[1].Id.ShouldBe(activityId4);
            scheduledActivities0[1].StartTime.ShouldBe(7);
            scheduledActivities0[1].FinishTime.ShouldBe(18);

            scheduledActivities0[2].Id.ShouldBe(activityId7);
            scheduledActivities0[2].StartTime.ShouldBe(18);
            scheduledActivities0[2].FinishTime.ShouldBe(22);

            scheduledActivities0[3].Id.ShouldBe(activityId9);
            scheduledActivities0[3].StartTime.ShouldBe(31);
            scheduledActivities0[3].FinishTime.ShouldBe(41);

            scheduledActivities0.Last().FinishTime.ShouldBe(41);


            var resourceSchedule1 = resourceSchedules[1];
            resourceSchedule1.Resource.ShouldBeNull();
            var scheduledActivities1 = resourceSchedule1.ScheduledActivities.ToList();
            scheduledActivities1.Count.ShouldBe(3);

            scheduledActivities1[0].Id.ShouldBe(activityId3);
            scheduledActivities1[0].StartTime.ShouldBe(0);
            scheduledActivities1[0].FinishTime.ShouldBe(8);

            scheduledActivities1[1].Id.ShouldBe(activityId6);
            scheduledActivities1[1].StartTime.ShouldBe(8);
            scheduledActivities1[1].FinishTime.ShouldBe(15);

            scheduledActivities1[2].Id.ShouldBe(activityId8);
            scheduledActivities1[2].StartTime.ShouldBe(18);
            scheduledActivities1[2].FinishTime.ShouldBe(22);

            scheduledActivities1.Last().FinishTime.ShouldBe(22);


            var resourceSchedule2 = resourceSchedules[2];
            resourceSchedule2.Resource.ShouldBeNull();
            var scheduledActivities2 = resourceSchedule2.ScheduledActivities.ToList();
            scheduledActivities2.Count.ShouldBe(2);

            scheduledActivities2[0].Id.ShouldBe(activityId1);
            scheduledActivities2[0].StartTime.ShouldBe(0);
            scheduledActivities2[0].FinishTime.ShouldBe(6);

            scheduledActivities2[1].Id.ShouldBe(activityId5);
            scheduledActivities2[1].StartTime.ShouldBe(8);
            scheduledActivities2[1].FinishTime.ShouldBe(16);

            scheduledActivities2.Last().FinishTime.ShouldBe(16);



            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(6);
            graphBuilder.Activity(activityId1).FreeSlack.ShouldBe(2);
            graphBuilder.Activity(activityId1).TotalSlack.ShouldBe(17);
            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(17);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(23);
            graphBuilder.Activity(activityId1).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId1).Successors.Contains(activityId5).ShouldBeTrue();

            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(7);
            graphBuilder.Activity(activityId2).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId2).TotalSlack.ShouldBe(9);
            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(9);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(16);
            graphBuilder.Activity(activityId2).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId2).Successors.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId2).Successors.Contains(activityId5).ShouldBeTrue();

            graphBuilder.Activity(activityId3).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).TotalSlack.ShouldBe(15);
            graphBuilder.Activity(activityId3).LatestStartTime.ShouldBe(15);
            graphBuilder.Activity(activityId3).LatestFinishTime.ShouldBe(23);
            graphBuilder.Activity(activityId3).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId3).Successors.Contains(activityId5).ShouldBeTrue();
            graphBuilder.Activity(activityId3).Successors.Contains(activityId6).ShouldBeTrue();

            graphBuilder.Activity(activityId4).EarliestStartTime.ShouldBe(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.ShouldBe(18);
            graphBuilder.Activity(activityId4).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).TotalSlack.ShouldBe(9);
            graphBuilder.Activity(activityId4).LatestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId4).LatestFinishTime.ShouldBe(27);
            graphBuilder.Activity(activityId4).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId4).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId4).PlanningDependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId4).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId4).ResourceDependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId4).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId4).Successors.Contains(activityId7).ShouldBeTrue();
            graphBuilder.Activity(activityId4).Successors.Contains(activityId8).ShouldBeTrue();

            graphBuilder.Activity(activityId5).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId5).EarliestFinishTime.ShouldBe(16);
            graphBuilder.Activity(activityId5).FreeSlack.ShouldBe(15);
            graphBuilder.Activity(activityId5).TotalSlack.ShouldBe(15);
            graphBuilder.Activity(activityId5).LatestStartTime.ShouldBe(23);
            graphBuilder.Activity(activityId5).LatestFinishTime.ShouldBe(31);
            graphBuilder.Activity(activityId5).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId5).PlanningDependencies.Count.ShouldBe(3);
            graphBuilder.Activity(activityId5).PlanningDependencies.Contains(activityId1).ShouldBeTrue();
            graphBuilder.Activity(activityId5).PlanningDependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId5).PlanningDependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId5).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId5).ResourceDependencies.Contains(activityId1).ShouldBeTrue();
            graphBuilder.Activity(activityId5).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId5).Successors.Contains(activityId9).ShouldBeTrue();

            graphBuilder.Activity(activityId6).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.ShouldBe(15);
            graphBuilder.Activity(activityId6).FreeSlack.ShouldBe(3);
            graphBuilder.Activity(activityId6).TotalSlack.ShouldBe(22);
            graphBuilder.Activity(activityId6).LatestStartTime.ShouldBe(30);
            graphBuilder.Activity(activityId6).LatestFinishTime.ShouldBe(37);
            graphBuilder.Activity(activityId6).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId6).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).PlanningDependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId6).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).ResourceDependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId6).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).Successors.Contains(activityId8).ShouldBeTrue();

            graphBuilder.Activity(activityId7).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId7).FreeSlack.ShouldBe(9);
            graphBuilder.Activity(activityId7).TotalSlack.ShouldBe(9);
            graphBuilder.Activity(activityId7).LatestStartTime.ShouldBe(27);
            graphBuilder.Activity(activityId7).LatestFinishTime.ShouldBe(31);
            graphBuilder.Activity(activityId7).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId7).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId7).PlanningDependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId7).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId7).ResourceDependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId7).Successors.Count.ShouldBe(0);

            graphBuilder.Activity(activityId8).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId8).EarliestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId8).FreeSlack.ShouldBe(19);
            graphBuilder.Activity(activityId8).TotalSlack.ShouldBe(19);
            graphBuilder.Activity(activityId8).LatestStartTime.ShouldBe(37);
            graphBuilder.Activity(activityId8).LatestFinishTime.ShouldBe(41);
            graphBuilder.Activity(activityId8).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId8).PlanningDependencies.Count.ShouldBe(2);
            graphBuilder.Activity(activityId8).PlanningDependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId8).PlanningDependencies.Contains(activityId6).ShouldBeTrue();
            graphBuilder.Activity(activityId8).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId8).ResourceDependencies.Contains(activityId6).ShouldBeTrue();
            graphBuilder.Activity(activityId8).Successors.Count.ShouldBe(0);

            graphBuilder.Activity(activityId9).EarliestStartTime.ShouldBe(31);
            graphBuilder.Activity(activityId9).EarliestFinishTime.ShouldBe(41);
            graphBuilder.Activity(activityId9).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).LatestStartTime.ShouldBe(31);
            graphBuilder.Activity(activityId9).LatestFinishTime.ShouldBe(41);
            graphBuilder.Activity(activityId9).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId9).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId9).PlanningDependencies.Contains(activityId5).ShouldBeTrue();
            graphBuilder.Activity(activityId9).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId9).ResourceDependencies.Contains(activityId7).ShouldBeTrue();
            graphBuilder.Activity(activityId9).Successors.Count.ShouldBe(0);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithPlanningDependenciesAndTwoNoneAndDirectResources_ThenResourceSchedulesCorrectOrder()
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
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            var graphBuilder = graphCompiler.Builder;
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId1, 6));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId2, 7) { HasNoCost = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId3, 8) { HasNoCost = true, HasNoEffort = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId4, 11, new HashSet<int>(), new HashSet<int> { activityId2 }) { HasNoEffort = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId5, 8, new HashSet<int>(), new HashSet<int> { activityId1, activityId2, activityId3 }) { HasNoCost = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId6, 7, new HashSet<int>(), new HashSet<int> { activityId3 }) { HasNoCost = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId7, 4, new HashSet<int>(), new HashSet<int> { activityId4 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId8, 4, new HashSet<int>(), new HashSet<int> { activityId4, activityId6 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId9, 10, new HashSet<int>(), new HashSet<int> { activityId5 }));

            int resourceId1 = 1;
            int resourceId2 = resourceId1 + 1;
            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int, int>>(new[]
                {
                    new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>()),
                    new Resource<int, int>(resourceId2, string.Empty, false, false, InterActivityAllocationType.Direct, 1.0, 0, Enumerable.Empty<int>()),
                }));

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(2);

            resourceSchedules[0].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true,
                });

            resourceSchedules[0].CostAllocation.ShouldBe(
                new bool[] {
                    false, false, false, false, false, false, false, false, true, true,
                    true, true, true, true, true, true, true, true, true, false,
                    false, false, false, false, false, false, true, true, true, true,
                    true, true, true, true,
                });

            resourceSchedules[0].EffortAllocation.ShouldBe(
                new bool[] {
                    false, false, false, false, false, false, false, false, false, false,
                    false, false, false, false, false, false, false, false, false, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(5);

            scheduledActivities0[0].Id.ShouldBe(activityId3);
            scheduledActivities0[0].HasNoCost.ShouldBeTrue();
            scheduledActivities0[0].HasNoEffort.ShouldBeTrue();
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(8);

            scheduledActivities0[1].Id.ShouldBe(activityId4);
            scheduledActivities0[1].HasNoCost.ShouldBeFalse();
            scheduledActivities0[1].HasNoEffort.ShouldBeTrue();
            scheduledActivities0[1].StartTime.ShouldBe(8);
            scheduledActivities0[1].FinishTime.ShouldBe(19);

            scheduledActivities0[2].Id.ShouldBe(activityId6);
            scheduledActivities0[2].HasNoCost.ShouldBeTrue();
            scheduledActivities0[2].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[2].StartTime.ShouldBe(19);
            scheduledActivities0[2].FinishTime.ShouldBe(26);

            scheduledActivities0[3].Id.ShouldBe(activityId7);
            scheduledActivities0[3].HasNoCost.ShouldBeFalse();
            scheduledActivities0[3].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[3].StartTime.ShouldBe(26);
            scheduledActivities0[3].FinishTime.ShouldBe(30);

            scheduledActivities0[4].Id.ShouldBe(activityId8);
            scheduledActivities0[4].HasNoCost.ShouldBeFalse();
            scheduledActivities0[4].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[4].StartTime.ShouldBe(30);
            scheduledActivities0[4].FinishTime.ShouldBe(34);

            scheduledActivities0.Last().FinishTime.ShouldBe(34);


            resourceSchedules[1].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, false, false, false,
                });

            resourceSchedules[1].CostAllocation.ShouldBe(
                new bool[] {
                    false, false, false, false, false, false, false, true, true, true,
                    true, true, true, false, false, false, false, false, false, false,
                    false, true, true, true, true, true, true, true, true, true,
                    true, false, false, false,
                });

            resourceSchedules[1].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, false, false, false,
                });

            var resourceSchedule1 = resourceSchedules[1];
            resourceSchedule1.Resource.Id.ShouldBe(resourceId2);
            var scheduledActivities1 = resourceSchedule1.ScheduledActivities.ToList();
            scheduledActivities1.Count.ShouldBe(4);

            scheduledActivities1[0].Id.ShouldBe(activityId2);
            scheduledActivities1[0].HasNoCost.ShouldBeTrue();
            scheduledActivities1[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities1[0].StartTime.ShouldBe(0);
            scheduledActivities1[0].FinishTime.ShouldBe(7);

            scheduledActivities1[1].Id.ShouldBe(activityId1);
            scheduledActivities1[1].HasNoCost.ShouldBeFalse();
            scheduledActivities1[1].HasNoEffort.ShouldBeFalse();
            scheduledActivities1[1].StartTime.ShouldBe(7);
            scheduledActivities1[1].FinishTime.ShouldBe(13);

            scheduledActivities1[2].Id.ShouldBe(activityId5);
            scheduledActivities1[2].HasNoCost.ShouldBeTrue();
            scheduledActivities1[2].HasNoEffort.ShouldBeFalse();
            scheduledActivities1[2].StartTime.ShouldBe(13);
            scheduledActivities1[2].FinishTime.ShouldBe(21);

            scheduledActivities1[3].Id.ShouldBe(activityId9);
            scheduledActivities1[3].HasNoCost.ShouldBeFalse();
            scheduledActivities1[3].HasNoEffort.ShouldBeFalse();
            scheduledActivities1[3].StartTime.ShouldBe(21);
            scheduledActivities1[3].FinishTime.ShouldBe(31);

            scheduledActivities1.Last().FinishTime.ShouldBe(31);



            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(7);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(13);
            graphBuilder.Activity(activityId1).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId1).TotalSlack.ShouldBe(3);
            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(10);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(16);
            graphBuilder.Activity(activityId1).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId1).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId1).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId1).Successors.Contains(activityId5).ShouldBeTrue();

            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(7);
            graphBuilder.Activity(activityId2).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId2).TotalSlack.ShouldBe(1);
            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(1);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId2).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId2).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId2).Successors.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId2).Successors.Contains(activityId5).ShouldBeTrue();

            graphBuilder.Activity(activityId3).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId3).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId3).Successors.Contains(activityId5).ShouldBeTrue();
            graphBuilder.Activity(activityId3).Successors.Contains(activityId6).ShouldBeTrue();

            graphBuilder.Activity(activityId4).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId4).EarliestFinishTime.ShouldBe(19);
            graphBuilder.Activity(activityId4).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).LatestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId4).LatestFinishTime.ShouldBe(19);
            graphBuilder.Activity(activityId4).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId4).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId4).PlanningDependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId4).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId3 }), ignoreOrder: true);
            graphBuilder.Activity(activityId4).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId4).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId4).Successors.Contains(activityId7).ShouldBeTrue();
            graphBuilder.Activity(activityId4).Successors.Contains(activityId8).ShouldBeTrue();

            graphBuilder.Activity(activityId5).EarliestStartTime.ShouldBe(13);
            graphBuilder.Activity(activityId5).EarliestFinishTime.ShouldBe(21);
            graphBuilder.Activity(activityId5).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).TotalSlack.ShouldBe(3);
            graphBuilder.Activity(activityId5).LatestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId5).LatestFinishTime.ShouldBe(24);
            graphBuilder.Activity(activityId5).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId5).PlanningDependencies.Count.ShouldBe(3);
            graphBuilder.Activity(activityId5).PlanningDependencies.Contains(activityId1).ShouldBeTrue();
            graphBuilder.Activity(activityId5).PlanningDependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId5).PlanningDependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId5).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId5).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId5).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId5).Successors.Contains(activityId9).ShouldBeTrue();

            graphBuilder.Activity(activityId6).EarliestStartTime.ShouldBe(19);
            graphBuilder.Activity(activityId6).EarliestFinishTime.ShouldBe(26);
            graphBuilder.Activity(activityId6).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId6).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId6).LatestStartTime.ShouldBe(19);
            graphBuilder.Activity(activityId6).LatestFinishTime.ShouldBe(26);
            graphBuilder.Activity(activityId6).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId6).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).PlanningDependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId6).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId4 }), ignoreOrder: true);
            graphBuilder.Activity(activityId6).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId6).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).Successors.Contains(activityId8).ShouldBeTrue();

            graphBuilder.Activity(activityId7).EarliestStartTime.ShouldBe(26);
            graphBuilder.Activity(activityId7).EarliestFinishTime.ShouldBe(30);
            graphBuilder.Activity(activityId7).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId7).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId7).LatestStartTime.ShouldBe(26);
            graphBuilder.Activity(activityId7).LatestFinishTime.ShouldBe(30);
            graphBuilder.Activity(activityId7).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId7).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId7).PlanningDependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId7).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId6 }), ignoreOrder: true);
            graphBuilder.Activity(activityId7).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId7).Successors.Count.ShouldBe(0);

            graphBuilder.Activity(activityId8).EarliestStartTime.ShouldBe(30);
            graphBuilder.Activity(activityId8).EarliestFinishTime.ShouldBe(34);
            graphBuilder.Activity(activityId8).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId8).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId8).LatestStartTime.ShouldBe(30);
            graphBuilder.Activity(activityId8).LatestFinishTime.ShouldBe(34);
            graphBuilder.Activity(activityId8).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId8).PlanningDependencies.Count.ShouldBe(2);
            graphBuilder.Activity(activityId8).PlanningDependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId8).PlanningDependencies.Contains(activityId6).ShouldBeTrue();
            graphBuilder.Activity(activityId8).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId7 }), ignoreOrder: true);
            graphBuilder.Activity(activityId8).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId8).Successors.Count.ShouldBe(0);

            graphBuilder.Activity(activityId9).EarliestStartTime.ShouldBe(21);
            graphBuilder.Activity(activityId9).EarliestFinishTime.ShouldBe(31);
            graphBuilder.Activity(activityId9).FreeSlack.ShouldBe(3);
            graphBuilder.Activity(activityId9).TotalSlack.ShouldBe(3);
            graphBuilder.Activity(activityId9).LatestStartTime.ShouldBe(24);
            graphBuilder.Activity(activityId9).LatestFinishTime.ShouldBe(34);
            graphBuilder.Activity(activityId9).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId9).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId9).PlanningDependencies.Contains(activityId5).ShouldBeTrue();
            graphBuilder.Activity(activityId9).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId5 }), ignoreOrder: true);
            graphBuilder.Activity(activityId9).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId9).Successors.Count.ShouldBe(0);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithPlanningDependenciesAndOneActiveAndTwoInactiveResources_ThenResourceSchedulesCorrectOrder()
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
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            var graphBuilder = graphCompiler.Builder;
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId1, 6));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId2, 7) { HasNoCost = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId3, 8) { HasNoCost = true, HasNoEffort = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId4, 11, new HashSet<int>(), new HashSet<int> { activityId2 }) { HasNoEffort = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId5, 8, new HashSet<int>(), new HashSet<int> { activityId1, activityId2, activityId3 }) { HasNoCost = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId6, 7, new HashSet<int>(), new HashSet<int> { activityId3 }) { HasNoCost = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId7, 4, new HashSet<int>(), new HashSet<int> { activityId4 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId8, 4, new HashSet<int>(), new HashSet<int> { activityId4, activityId6 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId9, 10, new HashSet<int>(), new HashSet<int> { activityId5 }));

            int resourceId1 = 1;
            int resourceId2 = resourceId1 + 1;
            int resourceId3 = resourceId2 + 1;

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int, int>>(new[]
                {
                    new Resource<int, int>(resourceId1, string.Empty, false, true, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>()),
                    new Resource<int, int>(resourceId2, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>()),
                    new Resource<int, int>(resourceId3, string.Empty, false, true, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>()),
                }));

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(1);

            resourceSchedules[0].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true,
                });

            resourceSchedules[0].CostAllocation.ShouldBe(
                new bool[] {
                    false, false, false, false, false, false, false, false, false, false,
                    false, false, false, false, false, true, true, true, true, true,
                    true, false, false, false, false, false, false, false, false, true,
                    true, true, true, true, true, true, true, true, true, true,
                    false, false, false, false, false, false, false, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true,
                });

            resourceSchedules[0].EffortAllocation.ShouldBe(
                new bool[] {
                    false, false, false, false, false, false, false, false, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, false,
                    false, false, false, false, false, false, false, false, false, false,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId2);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(9);

            scheduledActivities0[0].Id.ShouldBe(activityId3);
            scheduledActivities0[0].HasNoCost.ShouldBeTrue();
            scheduledActivities0[0].HasNoEffort.ShouldBeTrue();
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(8);

            scheduledActivities0[1].Id.ShouldBe(activityId2);
            scheduledActivities0[1].HasNoCost.ShouldBeTrue();
            scheduledActivities0[1].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[1].StartTime.ShouldBe(8);
            scheduledActivities0[1].FinishTime.ShouldBe(15);

            scheduledActivities0[2].Id.ShouldBe(activityId1);
            scheduledActivities0[2].HasNoCost.ShouldBeFalse();
            scheduledActivities0[2].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[2].StartTime.ShouldBe(15);
            scheduledActivities0[2].FinishTime.ShouldBe(21);

            scheduledActivities0[3].Id.ShouldBe(activityId5);
            scheduledActivities0[3].HasNoCost.ShouldBeTrue();
            scheduledActivities0[3].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[3].StartTime.ShouldBe(21);
            scheduledActivities0[3].FinishTime.ShouldBe(29);

            scheduledActivities0[4].Id.ShouldBe(activityId4);
            scheduledActivities0[4].HasNoCost.ShouldBeFalse();
            scheduledActivities0[4].HasNoEffort.ShouldBeTrue();
            scheduledActivities0[4].StartTime.ShouldBe(29);
            scheduledActivities0[4].FinishTime.ShouldBe(40);

            scheduledActivities0[5].Id.ShouldBe(activityId6);
            scheduledActivities0[5].HasNoCost.ShouldBeTrue();
            scheduledActivities0[5].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[5].StartTime.ShouldBe(40);
            scheduledActivities0[5].FinishTime.ShouldBe(47);

            scheduledActivities0[6].Id.ShouldBe(activityId9);
            scheduledActivities0[6].HasNoCost.ShouldBeFalse();
            scheduledActivities0[6].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[6].StartTime.ShouldBe(47);
            scheduledActivities0[6].FinishTime.ShouldBe(57);

            scheduledActivities0[7].Id.ShouldBe(activityId7);
            scheduledActivities0[7].HasNoCost.ShouldBeFalse();
            scheduledActivities0[7].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[7].StartTime.ShouldBe(57);
            scheduledActivities0[7].FinishTime.ShouldBe(61);

            scheduledActivities0[8].Id.ShouldBe(activityId8);
            scheduledActivities0[8].HasNoCost.ShouldBeFalse();
            scheduledActivities0[8].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[8].StartTime.ShouldBe(61);
            scheduledActivities0[8].FinishTime.ShouldBe(65);

            scheduledActivities0.Last().FinishTime.ShouldBe(65);

            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(15);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(21);
            graphBuilder.Activity(activityId1).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId1).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(15);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(21);
            graphBuilder.Activity(activityId1).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId1).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId1).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId1).Successors.Contains(activityId5).ShouldBeTrue();

            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(15);
            graphBuilder.Activity(activityId2).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId2).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(15);
            graphBuilder.Activity(activityId2).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId3 }), ignoreOrder: true);
            graphBuilder.Activity(activityId2).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId2).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId2).Successors.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId2).Successors.Contains(activityId5).ShouldBeTrue();

            graphBuilder.Activity(activityId3).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId3).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId3).Successors.Contains(activityId5).ShouldBeTrue();
            graphBuilder.Activity(activityId3).Successors.Contains(activityId6).ShouldBeTrue();

            graphBuilder.Activity(activityId4).EarliestStartTime.ShouldBe(29);
            graphBuilder.Activity(activityId4).EarliestFinishTime.ShouldBe(40);
            graphBuilder.Activity(activityId4).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).LatestStartTime.ShouldBe(29);
            graphBuilder.Activity(activityId4).LatestFinishTime.ShouldBe(40);
            graphBuilder.Activity(activityId4).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId4).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId4).PlanningDependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId4).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId5 }), ignoreOrder: true);
            graphBuilder.Activity(activityId4).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId4).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId4).Successors.Contains(activityId7).ShouldBeTrue();
            graphBuilder.Activity(activityId4).Successors.Contains(activityId8).ShouldBeTrue();

            graphBuilder.Activity(activityId5).EarliestStartTime.ShouldBe(21);
            graphBuilder.Activity(activityId5).EarliestFinishTime.ShouldBe(29);
            graphBuilder.Activity(activityId5).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).LatestStartTime.ShouldBe(21);
            graphBuilder.Activity(activityId5).LatestFinishTime.ShouldBe(29);
            graphBuilder.Activity(activityId5).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId5).PlanningDependencies.Count.ShouldBe(3);
            graphBuilder.Activity(activityId5).PlanningDependencies.Contains(activityId1).ShouldBeTrue();
            graphBuilder.Activity(activityId5).PlanningDependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId5).PlanningDependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId5).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId5).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId5).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId5).Successors.Contains(activityId9).ShouldBeTrue();

            graphBuilder.Activity(activityId6).EarliestStartTime.ShouldBe(40);
            graphBuilder.Activity(activityId6).EarliestFinishTime.ShouldBe(47);
            graphBuilder.Activity(activityId6).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId6).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId6).LatestStartTime.ShouldBe(40);
            graphBuilder.Activity(activityId6).LatestFinishTime.ShouldBe(47);
            graphBuilder.Activity(activityId6).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId6).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).PlanningDependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId6).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId4 }), ignoreOrder: true);
            graphBuilder.Activity(activityId6).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId6).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).Successors.Contains(activityId8).ShouldBeTrue();

            graphBuilder.Activity(activityId7).EarliestStartTime.ShouldBe(57);
            graphBuilder.Activity(activityId7).EarliestFinishTime.ShouldBe(61);
            graphBuilder.Activity(activityId7).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId7).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId7).LatestStartTime.ShouldBe(57);
            graphBuilder.Activity(activityId7).LatestFinishTime.ShouldBe(61);
            graphBuilder.Activity(activityId7).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId7).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId7).PlanningDependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId7).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId9 }), ignoreOrder: true);
            graphBuilder.Activity(activityId7).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId7).Successors.Count.ShouldBe(0);

            graphBuilder.Activity(activityId8).EarliestStartTime.ShouldBe(61);
            graphBuilder.Activity(activityId8).EarliestFinishTime.ShouldBe(65);
            graphBuilder.Activity(activityId8).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId8).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId8).LatestStartTime.ShouldBe(61);
            graphBuilder.Activity(activityId8).LatestFinishTime.ShouldBe(65);
            graphBuilder.Activity(activityId8).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId8).PlanningDependencies.Count.ShouldBe(2);
            graphBuilder.Activity(activityId8).PlanningDependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId8).PlanningDependencies.Contains(activityId6).ShouldBeTrue();
            graphBuilder.Activity(activityId8).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId7 }), ignoreOrder: true);
            graphBuilder.Activity(activityId8).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId8).Successors.Count.ShouldBe(0);

            graphBuilder.Activity(activityId9).EarliestStartTime.ShouldBe(47);
            graphBuilder.Activity(activityId9).EarliestFinishTime.ShouldBe(57);
            graphBuilder.Activity(activityId9).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).LatestStartTime.ShouldBe(47);
            graphBuilder.Activity(activityId9).LatestFinishTime.ShouldBe(57);
            graphBuilder.Activity(activityId9).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId9).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId9).PlanningDependencies.Contains(activityId5).ShouldBeTrue();
            graphBuilder.Activity(activityId9).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId6 }), ignoreOrder: true);
            graphBuilder.Activity(activityId9).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId9).Successors.Count.ShouldBe(0);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithPlanningDependenciesAndTwoIndirectResources_ThenResourceSchedulesCorrectOrder()
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
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            var graphBuilder = graphCompiler.Builder;
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId1, 6));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId2, 7));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId3, 8) { HasNoCost = true, HasNoEffort = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId4, 11, new HashSet<int>(), new HashSet<int> { activityId2 }) { HasNoEffort = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId5, 8, new HashSet<int>(), new HashSet<int> { activityId1, activityId2, activityId3 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId6, 7, new HashSet<int>(), new HashSet<int> { activityId3 }) { HasNoCost = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId7, 4, new HashSet<int>(), new HashSet<int> { activityId4 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId8, 4, new HashSet<int>(), new HashSet<int> { activityId4, activityId6 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId9, 10, new HashSet<int>(), new HashSet<int> { activityId5 }));

            int resourceId1 = 1;
            int resourceId2 = resourceId1 + 1;
            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int, int>>(new[]
                {
                    new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.Indirect, 1.0, 0, Enumerable.Empty<int>()),
                    new Resource<int, int>(resourceId2, string.Empty, false, false, InterActivityAllocationType.Indirect, 1.0, 0, Enumerable.Empty<int>()),
                }));

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(2);

            resourceSchedules[0].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true,
                });

            resourceSchedules[0].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true,
                });

            resourceSchedules[0].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(5);

            scheduledActivities0[0].Id.ShouldBe(activityId3);
            scheduledActivities0[0].HasNoCost.ShouldBeTrue();
            scheduledActivities0[0].HasNoEffort.ShouldBeTrue();
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(8);

            scheduledActivities0[1].Id.ShouldBe(activityId4);
            scheduledActivities0[1].HasNoCost.ShouldBeFalse();
            scheduledActivities0[1].HasNoEffort.ShouldBeTrue();
            scheduledActivities0[1].StartTime.ShouldBe(8);
            scheduledActivities0[1].FinishTime.ShouldBe(19);

            scheduledActivities0[2].Id.ShouldBe(activityId6);
            scheduledActivities0[2].HasNoCost.ShouldBeTrue();
            scheduledActivities0[2].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[2].StartTime.ShouldBe(19);
            scheduledActivities0[2].FinishTime.ShouldBe(26);

            scheduledActivities0[3].Id.ShouldBe(activityId7);
            scheduledActivities0[3].HasNoCost.ShouldBeFalse();
            scheduledActivities0[3].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[3].StartTime.ShouldBe(26);
            scheduledActivities0[3].FinishTime.ShouldBe(30);

            scheduledActivities0[4].Id.ShouldBe(activityId8);
            scheduledActivities0[4].HasNoCost.ShouldBeFalse();
            scheduledActivities0[4].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[4].StartTime.ShouldBe(30);
            scheduledActivities0[4].FinishTime.ShouldBe(34);

            scheduledActivities0.Last().FinishTime.ShouldBe(34);


            resourceSchedules[1].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true,
                });

            resourceSchedules[1].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true,
                });

            resourceSchedules[1].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true,
                });

            var resourceSchedule1 = resourceSchedules[1];
            resourceSchedule1.Resource.Id.ShouldBe(resourceId2);
            var scheduledActivities1 = resourceSchedule1.ScheduledActivities.ToList();
            scheduledActivities1.Count.ShouldBe(4);

            scheduledActivities1[0].Id.ShouldBe(activityId2);
            scheduledActivities1[0].HasNoCost.ShouldBeFalse();
            scheduledActivities1[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities1[0].StartTime.ShouldBe(0);
            scheduledActivities1[0].FinishTime.ShouldBe(7);

            scheduledActivities1[1].Id.ShouldBe(activityId1);
            scheduledActivities1[1].HasNoCost.ShouldBeFalse();
            scheduledActivities1[1].HasNoEffort.ShouldBeFalse();
            scheduledActivities1[1].StartTime.ShouldBe(7);
            scheduledActivities1[1].FinishTime.ShouldBe(13);

            scheduledActivities1[2].Id.ShouldBe(activityId5);
            scheduledActivities1[2].HasNoCost.ShouldBeFalse();
            scheduledActivities1[2].HasNoEffort.ShouldBeFalse();
            scheduledActivities1[2].StartTime.ShouldBe(13);
            scheduledActivities1[2].FinishTime.ShouldBe(21);

            scheduledActivities1[3].Id.ShouldBe(activityId9);
            scheduledActivities1[3].HasNoCost.ShouldBeFalse();
            scheduledActivities1[3].HasNoEffort.ShouldBeFalse();
            scheduledActivities1[3].StartTime.ShouldBe(21);
            scheduledActivities1[3].FinishTime.ShouldBe(31);

            scheduledActivities1.Last().FinishTime.ShouldBe(31);



            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(7);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(13);
            graphBuilder.Activity(activityId1).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId1).TotalSlack.ShouldBe(3);
            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(10);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(16);
            graphBuilder.Activity(activityId1).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId1).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId1).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId1).Successors.Contains(activityId5).ShouldBeTrue();

            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(7);
            graphBuilder.Activity(activityId2).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId2).TotalSlack.ShouldBe(1);
            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(1);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId2).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId2).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId2).Successors.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId2).Successors.Contains(activityId5).ShouldBeTrue();

            graphBuilder.Activity(activityId3).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId3).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId3).Successors.Contains(activityId5).ShouldBeTrue();
            graphBuilder.Activity(activityId3).Successors.Contains(activityId6).ShouldBeTrue();

            graphBuilder.Activity(activityId4).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId4).EarliestFinishTime.ShouldBe(19);
            graphBuilder.Activity(activityId4).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).LatestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId4).LatestFinishTime.ShouldBe(19);
            graphBuilder.Activity(activityId4).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId4).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId4).PlanningDependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId4).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId3 }), ignoreOrder: true);
            graphBuilder.Activity(activityId4).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId4).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId4).Successors.Contains(activityId7).ShouldBeTrue();
            graphBuilder.Activity(activityId4).Successors.Contains(activityId8).ShouldBeTrue();

            graphBuilder.Activity(activityId5).EarliestStartTime.ShouldBe(13);
            graphBuilder.Activity(activityId5).EarliestFinishTime.ShouldBe(21);
            graphBuilder.Activity(activityId5).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).TotalSlack.ShouldBe(3);
            graphBuilder.Activity(activityId5).LatestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId5).LatestFinishTime.ShouldBe(24);
            graphBuilder.Activity(activityId5).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId5).PlanningDependencies.Count.ShouldBe(3);
            graphBuilder.Activity(activityId5).PlanningDependencies.Contains(activityId1).ShouldBeTrue();
            graphBuilder.Activity(activityId5).PlanningDependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId5).PlanningDependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId5).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId5).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId5).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId5).Successors.Contains(activityId9).ShouldBeTrue();

            graphBuilder.Activity(activityId6).EarliestStartTime.ShouldBe(19);
            graphBuilder.Activity(activityId6).EarliestFinishTime.ShouldBe(26);
            graphBuilder.Activity(activityId6).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId6).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId6).LatestStartTime.ShouldBe(19);
            graphBuilder.Activity(activityId6).LatestFinishTime.ShouldBe(26);
            graphBuilder.Activity(activityId6).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId6).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).PlanningDependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId6).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId4 }), ignoreOrder: true);
            graphBuilder.Activity(activityId6).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId6).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).Successors.Contains(activityId8).ShouldBeTrue();

            graphBuilder.Activity(activityId7).EarliestStartTime.ShouldBe(26);
            graphBuilder.Activity(activityId7).EarliestFinishTime.ShouldBe(30);
            graphBuilder.Activity(activityId7).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId7).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId7).LatestStartTime.ShouldBe(26);
            graphBuilder.Activity(activityId7).LatestFinishTime.ShouldBe(30);
            graphBuilder.Activity(activityId7).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId7).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId7).PlanningDependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId7).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId6 }), ignoreOrder: true);
            graphBuilder.Activity(activityId7).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId7).Successors.Count.ShouldBe(0);

            graphBuilder.Activity(activityId8).EarliestStartTime.ShouldBe(30);
            graphBuilder.Activity(activityId8).EarliestFinishTime.ShouldBe(34);
            graphBuilder.Activity(activityId8).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId8).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId8).LatestStartTime.ShouldBe(30);
            graphBuilder.Activity(activityId8).LatestFinishTime.ShouldBe(34);
            graphBuilder.Activity(activityId8).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId8).PlanningDependencies.Count.ShouldBe(2);
            graphBuilder.Activity(activityId8).PlanningDependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId8).PlanningDependencies.Contains(activityId6).ShouldBeTrue();
            graphBuilder.Activity(activityId8).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId7 }), ignoreOrder: true);
            graphBuilder.Activity(activityId8).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId8).Successors.Count.ShouldBe(0);

            graphBuilder.Activity(activityId9).EarliestStartTime.ShouldBe(21);
            graphBuilder.Activity(activityId9).EarliestFinishTime.ShouldBe(31);
            graphBuilder.Activity(activityId9).FreeSlack.ShouldBe(3);
            graphBuilder.Activity(activityId9).TotalSlack.ShouldBe(3);
            graphBuilder.Activity(activityId9).LatestStartTime.ShouldBe(24);
            graphBuilder.Activity(activityId9).LatestFinishTime.ShouldBe(34);
            graphBuilder.Activity(activityId9).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId9).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId9).PlanningDependencies.Contains(activityId5).ShouldBeTrue();
            graphBuilder.Activity(activityId9).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId5 }), ignoreOrder: true);
            graphBuilder.Activity(activityId9).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId9).Successors.Count.ShouldBe(0);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithCompiledAndPlanningDependenciesAndUnlimitedResources_ThenResourceSchedulesCorrectOrder()
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
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            var graphBuilder = graphCompiler.Builder;
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId1, 6));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId2, 7));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId3, 8));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId4, 11, new HashSet<int>(), new HashSet<int> { activityId2 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId5, 8, new HashSet<int> { activityId1, activityId2 }, new HashSet<int> { activityId3 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId6, 7, new HashSet<int>(), new HashSet<int> { activityId3 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId7, 4, new HashSet<int> { activityId4 }, new HashSet<int>()));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId8, 4, new HashSet<int> { activityId4 }, new HashSet<int> { activityId6 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId9, 10, new HashSet<int>(), new HashSet<int> { activityId5 }));

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile();

            compilation.CompilationErrors.ShouldBeEmpty();

            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(3);

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.ShouldBeNull();
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(3);

            scheduledActivities0[0].Id.ShouldBe(activityId3);
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(8);

            scheduledActivities0[1].Id.ShouldBe(activityId5);
            scheduledActivities0[1].StartTime.ShouldBe(8);
            scheduledActivities0[1].FinishTime.ShouldBe(16);

            scheduledActivities0[2].Id.ShouldBe(activityId9);
            scheduledActivities0[2].StartTime.ShouldBe(16);
            scheduledActivities0[2].FinishTime.ShouldBe(26);

            scheduledActivities0.Last().FinishTime.ShouldBe(26);


            var resourceSchedule1 = resourceSchedules[1];
            resourceSchedule1.Resource.ShouldBeNull();
            var scheduledActivities1 = resourceSchedule1.ScheduledActivities.ToList();
            scheduledActivities1.Count.ShouldBe(3);

            scheduledActivities1[0].Id.ShouldBe(activityId2);
            scheduledActivities1[0].StartTime.ShouldBe(0);
            scheduledActivities1[0].FinishTime.ShouldBe(7);

            scheduledActivities1[1].Id.ShouldBe(activityId4);
            scheduledActivities1[1].StartTime.ShouldBe(7);
            scheduledActivities1[1].FinishTime.ShouldBe(18);

            scheduledActivities1[2].Id.ShouldBe(activityId7);
            scheduledActivities1[2].StartTime.ShouldBe(18);
            scheduledActivities1[2].FinishTime.ShouldBe(22);

            scheduledActivities1.Last().FinishTime.ShouldBe(22);


            var resourceSchedule2 = resourceSchedules[2];
            resourceSchedule2.Resource.ShouldBeNull();
            var scheduledActivities2 = resourceSchedule2.ScheduledActivities.ToList();
            scheduledActivities2.Count.ShouldBe(3);

            scheduledActivities2[0].Id.ShouldBe(activityId1);
            scheduledActivities2[0].StartTime.ShouldBe(0);
            scheduledActivities2[0].FinishTime.ShouldBe(6);

            scheduledActivities2[1].Id.ShouldBe(activityId6);
            scheduledActivities2[1].StartTime.ShouldBe(8);
            scheduledActivities2[1].FinishTime.ShouldBe(15);

            scheduledActivities2[2].Id.ShouldBe(activityId8);
            scheduledActivities2[2].StartTime.ShouldBe(18);
            scheduledActivities2[2].FinishTime.ShouldBe(22);

            scheduledActivities2.Last().FinishTime.ShouldBe(22);



            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(6);
            graphBuilder.Activity(activityId1).FreeSlack.ShouldBe(2);
            graphBuilder.Activity(activityId1).TotalSlack.ShouldBe(2);
            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(2);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId1).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId1).Successors.Contains(activityId5).ShouldBeTrue();

            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(7);
            graphBuilder.Activity(activityId2).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId2).TotalSlack.ShouldBe(1);
            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(1);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId2).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId2).Successors.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId2).Successors.Contains(activityId5).ShouldBeTrue();

            graphBuilder.Activity(activityId3).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId3).Successors.Contains(activityId5).ShouldBeTrue();
            graphBuilder.Activity(activityId3).Successors.Contains(activityId6).ShouldBeTrue();

            graphBuilder.Activity(activityId4).EarliestStartTime.ShouldBe(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.ShouldBe(18);
            graphBuilder.Activity(activityId4).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).TotalSlack.ShouldBe(4);
            graphBuilder.Activity(activityId4).LatestStartTime.ShouldBe(11);
            graphBuilder.Activity(activityId4).LatestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId4).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId4).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId4).PlanningDependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId4).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId4).ResourceDependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId4).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId4).Successors.Contains(activityId7).ShouldBeTrue();
            graphBuilder.Activity(activityId4).Successors.Contains(activityId8).ShouldBeTrue();

            graphBuilder.Activity(activityId5).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId5).EarliestFinishTime.ShouldBe(16);
            graphBuilder.Activity(activityId5).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).LatestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId5).LatestFinishTime.ShouldBe(16);
            graphBuilder.Activity(activityId5).Dependencies.Count.ShouldBe(2);
            graphBuilder.Activity(activityId5).Dependencies.Contains(activityId1).ShouldBeTrue();
            graphBuilder.Activity(activityId5).Dependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId5).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId5).PlanningDependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId5).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId5).ResourceDependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId5).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId5).Successors.Contains(activityId9).ShouldBeTrue();

            graphBuilder.Activity(activityId6).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.ShouldBe(15);
            graphBuilder.Activity(activityId6).FreeSlack.ShouldBe(3);
            graphBuilder.Activity(activityId6).TotalSlack.ShouldBe(7);
            graphBuilder.Activity(activityId6).LatestStartTime.ShouldBe(15);
            graphBuilder.Activity(activityId6).LatestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId6).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId6).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).PlanningDependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId6).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).ResourceDependencies.Contains(activityId1).ShouldBeTrue();
            graphBuilder.Activity(activityId6).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).Successors.Contains(activityId8).ShouldBeTrue();

            graphBuilder.Activity(activityId7).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId7).FreeSlack.ShouldBe(4);
            graphBuilder.Activity(activityId7).TotalSlack.ShouldBe(4);
            graphBuilder.Activity(activityId7).LatestStartTime.ShouldBe(22);
            graphBuilder.Activity(activityId7).LatestFinishTime.ShouldBe(26);
            graphBuilder.Activity(activityId7).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId7).Dependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId7).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId7).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId7).ResourceDependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId7).Successors.Count.ShouldBe(0);

            graphBuilder.Activity(activityId8).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId8).EarliestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId8).FreeSlack.ShouldBe(4);
            graphBuilder.Activity(activityId8).TotalSlack.ShouldBe(4);
            graphBuilder.Activity(activityId8).LatestStartTime.ShouldBe(22);
            graphBuilder.Activity(activityId8).LatestFinishTime.ShouldBe(26);
            graphBuilder.Activity(activityId8).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId8).Dependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId8).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId8).PlanningDependencies.Contains(activityId6).ShouldBeTrue();
            graphBuilder.Activity(activityId8).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId8).ResourceDependencies.Contains(activityId6).ShouldBeTrue();
            graphBuilder.Activity(activityId8).Successors.Count.ShouldBe(0);

            graphBuilder.Activity(activityId9).EarliestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId9).EarliestFinishTime.ShouldBe(26);
            graphBuilder.Activity(activityId9).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).LatestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId9).LatestFinishTime.ShouldBe(26);
            graphBuilder.Activity(activityId9).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId9).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId9).PlanningDependencies.Contains(activityId5).ShouldBeTrue();
            graphBuilder.Activity(activityId9).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId9).ResourceDependencies.Contains(activityId5).ShouldBeTrue();
            graphBuilder.Activity(activityId9).Successors.Count.ShouldBe(0);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithCompiledAndPlanningDependenciesAndUnlimitedResourcesAndTargetResources_ThenResourceSchedulesCorrectOrder()
        {
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();

            var activity1 = new DependentActivity<int, int, int>(activityId1, 6);
            var activity2 = new DependentActivity<int, int, int>(activityId2, 7, new HashSet<int>(), new HashSet<int> { activityId1 });
            var activity3 = new DependentActivity<int, int, int>(activityId3, 4, new HashSet<int> { activityId2 }, new HashSet<int>());

            int resourceId1 = 1;
            int resourceId2 = resourceId1 + 1;
            int resourceId3 = resourceId2 + 1;

            activity1.TargetResources.Add(resourceId1);
            activity1.TargetResourceOperator = LogicalOperator.AND;

            activity2.TargetResources.Add(resourceId1);
            activity2.TargetResources.Add(resourceId2);
            activity2.TargetResourceOperator = LogicalOperator.OR;

            activity3.TargetResources.Add(resourceId1);
            activity3.TargetResources.Add(resourceId3);
            activity3.TargetResourceOperator = LogicalOperator.OR;

            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile();

            compilation.ResourceSchedules.ShouldNotBeEmpty();
            compilation.CompilationErrors.ShouldBeEmpty();

            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(1);

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.ShouldBeNull();
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(3);

            scheduledActivities0[0].Id.ShouldBe(activityId1);
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(6);

            scheduledActivities0[1].Id.ShouldBe(activityId2);
            scheduledActivities0[1].StartTime.ShouldBe(6);
            scheduledActivities0[1].FinishTime.ShouldBe(13);

            scheduledActivities0[2].Id.ShouldBe(activityId3);
            scheduledActivities0[2].StartTime.ShouldBe(13);
            scheduledActivities0[2].FinishTime.ShouldBe(17);

            scheduledActivities0.Last().FinishTime.ShouldBe(17);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithCompiledAndPlanningDependenciesAndFreeSlackUnlimitedResources_ThenResourceSchedulesCorrectOrder()
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
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            var graphBuilder = graphCompiler.Builder;
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId1, 6));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId2, 7));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId3, 8));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId4, 11, new HashSet<int>(), new HashSet<int> { activityId2 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId5, 8, new HashSet<int> { activityId1, activityId2 }, new HashSet<int> { activityId3 }) { MinimumFreeSlack = 15 });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId6, 7, new HashSet<int>(), new HashSet<int> { activityId3 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId7, 4, new HashSet<int> { activityId4 }, new HashSet<int>()));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId8, 4, new HashSet<int> { activityId4 }, new HashSet<int> { activityId6 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId9, 10, new HashSet<int>(), new HashSet<int> { activityId5 }));

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile();

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(3);

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.ShouldBeNull();
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(4);

            scheduledActivities0[0].Id.ShouldBe(activityId2);
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(7);

            scheduledActivities0[1].Id.ShouldBe(activityId4);
            scheduledActivities0[1].StartTime.ShouldBe(7);
            scheduledActivities0[1].FinishTime.ShouldBe(18);

            scheduledActivities0[2].Id.ShouldBe(activityId7);
            scheduledActivities0[2].StartTime.ShouldBe(18);
            scheduledActivities0[2].FinishTime.ShouldBe(22);

            scheduledActivities0[3].Id.ShouldBe(activityId9);
            scheduledActivities0[3].StartTime.ShouldBe(31);
            scheduledActivities0[3].FinishTime.ShouldBe(41);

            scheduledActivities0.Last().FinishTime.ShouldBe(41);


            var resourceSchedule1 = resourceSchedules[1];
            resourceSchedule1.Resource.ShouldBeNull();
            var scheduledActivities1 = resourceSchedule1.ScheduledActivities.ToList();
            scheduledActivities1.Count.ShouldBe(3);

            scheduledActivities1[0].Id.ShouldBe(activityId3);
            scheduledActivities1[0].StartTime.ShouldBe(0);
            scheduledActivities1[0].FinishTime.ShouldBe(8);

            scheduledActivities1[1].Id.ShouldBe(activityId6);
            scheduledActivities1[1].StartTime.ShouldBe(8);
            scheduledActivities1[1].FinishTime.ShouldBe(15);

            scheduledActivities1[2].Id.ShouldBe(activityId8);
            scheduledActivities1[2].StartTime.ShouldBe(18);
            scheduledActivities1[2].FinishTime.ShouldBe(22);

            scheduledActivities1.Last().FinishTime.ShouldBe(22);


            var resourceSchedule2 = resourceSchedules[2];
            resourceSchedule2.Resource.ShouldBeNull();
            var scheduledActivities2 = resourceSchedule2.ScheduledActivities.ToList();
            scheduledActivities2.Count.ShouldBe(2);

            scheduledActivities2[0].Id.ShouldBe(activityId1);
            scheduledActivities2[0].StartTime.ShouldBe(0);
            scheduledActivities2[0].FinishTime.ShouldBe(6);

            scheduledActivities2[1].Id.ShouldBe(activityId5);
            scheduledActivities2[1].StartTime.ShouldBe(8);
            scheduledActivities2[1].FinishTime.ShouldBe(16);

            scheduledActivities2.Last().FinishTime.ShouldBe(16);



            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(6);
            graphBuilder.Activity(activityId1).FreeSlack.ShouldBe(2);
            graphBuilder.Activity(activityId1).TotalSlack.ShouldBe(17);
            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(17);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(23);
            graphBuilder.Activity(activityId1).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId1).Successors.Contains(activityId5).ShouldBeTrue();

            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(7);
            graphBuilder.Activity(activityId2).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId2).TotalSlack.ShouldBe(9);
            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(9);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(16);
            graphBuilder.Activity(activityId2).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId2).Successors.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId2).Successors.Contains(activityId5).ShouldBeTrue();

            graphBuilder.Activity(activityId3).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).TotalSlack.ShouldBe(15);
            graphBuilder.Activity(activityId3).LatestStartTime.ShouldBe(15);
            graphBuilder.Activity(activityId3).LatestFinishTime.ShouldBe(23);
            graphBuilder.Activity(activityId3).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId3).Successors.Contains(activityId5).ShouldBeTrue();
            graphBuilder.Activity(activityId3).Successors.Contains(activityId6).ShouldBeTrue();

            graphBuilder.Activity(activityId4).EarliestStartTime.ShouldBe(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.ShouldBe(18);
            graphBuilder.Activity(activityId4).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).TotalSlack.ShouldBe(9);
            graphBuilder.Activity(activityId4).LatestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId4).LatestFinishTime.ShouldBe(27);
            graphBuilder.Activity(activityId4).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId4).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId4).PlanningDependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId4).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId4).ResourceDependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId4).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId4).Successors.Contains(activityId7).ShouldBeTrue();
            graphBuilder.Activity(activityId4).Successors.Contains(activityId8).ShouldBeTrue();

            graphBuilder.Activity(activityId5).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId5).EarliestFinishTime.ShouldBe(16);
            graphBuilder.Activity(activityId5).FreeSlack.ShouldBe(15);
            graphBuilder.Activity(activityId5).TotalSlack.ShouldBe(15);
            graphBuilder.Activity(activityId5).LatestStartTime.ShouldBe(23);
            graphBuilder.Activity(activityId5).LatestFinishTime.ShouldBe(31);
            graphBuilder.Activity(activityId5).Dependencies.Count.ShouldBe(2);
            graphBuilder.Activity(activityId5).Dependencies.Contains(activityId1).ShouldBeTrue();
            graphBuilder.Activity(activityId5).Dependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId5).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId5).PlanningDependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId5).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId5).ResourceDependencies.Contains(activityId1).ShouldBeTrue();
            graphBuilder.Activity(activityId5).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId5).Successors.Contains(activityId9).ShouldBeTrue();

            graphBuilder.Activity(activityId6).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.ShouldBe(15);
            graphBuilder.Activity(activityId6).FreeSlack.ShouldBe(3);
            graphBuilder.Activity(activityId6).TotalSlack.ShouldBe(22);
            graphBuilder.Activity(activityId6).LatestStartTime.ShouldBe(30);
            graphBuilder.Activity(activityId6).LatestFinishTime.ShouldBe(37);
            graphBuilder.Activity(activityId6).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId6).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).PlanningDependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId6).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).ResourceDependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId6).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).Successors.Contains(activityId8).ShouldBeTrue();

            graphBuilder.Activity(activityId7).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId7).FreeSlack.ShouldBe(9);
            graphBuilder.Activity(activityId7).TotalSlack.ShouldBe(9);
            graphBuilder.Activity(activityId7).LatestStartTime.ShouldBe(27);
            graphBuilder.Activity(activityId7).LatestFinishTime.ShouldBe(31);
            graphBuilder.Activity(activityId7).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId7).Dependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId7).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId7).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId7).ResourceDependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId7).Successors.Count.ShouldBe(0);

            graphBuilder.Activity(activityId8).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId8).EarliestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId8).FreeSlack.ShouldBe(19);
            graphBuilder.Activity(activityId8).TotalSlack.ShouldBe(19);
            graphBuilder.Activity(activityId8).LatestStartTime.ShouldBe(37);
            graphBuilder.Activity(activityId8).LatestFinishTime.ShouldBe(41);
            graphBuilder.Activity(activityId8).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId8).Dependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId8).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId8).PlanningDependencies.Contains(activityId6).ShouldBeTrue();
            graphBuilder.Activity(activityId8).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId8).ResourceDependencies.Contains(activityId6).ShouldBeTrue();
            graphBuilder.Activity(activityId8).Successors.Count.ShouldBe(0);

            graphBuilder.Activity(activityId9).EarliestStartTime.ShouldBe(31);
            graphBuilder.Activity(activityId9).EarliestFinishTime.ShouldBe(41);
            graphBuilder.Activity(activityId9).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).LatestStartTime.ShouldBe(31);
            graphBuilder.Activity(activityId9).LatestFinishTime.ShouldBe(41);
            graphBuilder.Activity(activityId9).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId9).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId9).PlanningDependencies.Contains(activityId5).ShouldBeTrue();
            graphBuilder.Activity(activityId9).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId9).ResourceDependencies.Contains(activityId7).ShouldBeTrue();
            graphBuilder.Activity(activityId9).Successors.Count.ShouldBe(0);

        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithCompiledAndPlanningDependenciesAndTwoNoneAndDirectResources_ThenResourceSchedulesCorrectOrder()
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
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            var graphBuilder = graphCompiler.Builder;
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId1, 6));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId2, 7) { HasNoCost = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId3, 8) { HasNoCost = true, HasNoEffort = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId4, 11, new HashSet<int>(), new HashSet<int> { activityId2 }) { HasNoEffort = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId5, 8, new HashSet<int> { activityId1, activityId2 }, new HashSet<int> { activityId3 }) { HasNoCost = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId6, 7, new HashSet<int>(), new HashSet<int> { activityId3 }) { HasNoCost = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId7, 4, new HashSet<int> { activityId4 }, new HashSet<int>()));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId8, 4, new HashSet<int> { activityId4 }, new HashSet<int> { activityId6 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId9, 10, new HashSet<int>(), new HashSet<int> { activityId5 }));

            int resourceId1 = 1;
            int resourceId2 = resourceId1 + 1;
            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int, int>>(new[]
                {
                    new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>()),
                    new Resource<int, int>(resourceId2, string.Empty, false, false, InterActivityAllocationType.Direct, 1.0, 0, Enumerable.Empty<int>()),
                }));

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(2);

            resourceSchedules[0].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true,
                });

            resourceSchedules[0].CostAllocation.ShouldBe(
                new bool[] {
                    false, false, false, false, false, false, false, false, true, true,
                    true, true, true, true, true, true, true, true, true, false,
                    false, false, false, false, false, false, true, true, true, true,
                    true, true, true, true,
                });

            resourceSchedules[0].EffortAllocation.ShouldBe(
                new bool[] {
                    false, false, false, false, false, false, false, false, false, false,
                    false, false, false, false, false, false, false, false, false, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(5);

            scheduledActivities0[0].Id.ShouldBe(activityId3);
            scheduledActivities0[0].HasNoCost.ShouldBeTrue();
            scheduledActivities0[0].HasNoEffort.ShouldBeTrue();
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(8);

            scheduledActivities0[1].Id.ShouldBe(activityId4);
            scheduledActivities0[1].HasNoCost.ShouldBeFalse();
            scheduledActivities0[1].HasNoEffort.ShouldBeTrue();
            scheduledActivities0[1].StartTime.ShouldBe(8);
            scheduledActivities0[1].FinishTime.ShouldBe(19);

            scheduledActivities0[2].Id.ShouldBe(activityId6);
            scheduledActivities0[2].HasNoCost.ShouldBeTrue();
            scheduledActivities0[2].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[2].StartTime.ShouldBe(19);
            scheduledActivities0[2].FinishTime.ShouldBe(26);

            scheduledActivities0[3].Id.ShouldBe(activityId7);
            scheduledActivities0[3].HasNoCost.ShouldBeFalse();
            scheduledActivities0[3].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[3].StartTime.ShouldBe(26);
            scheduledActivities0[3].FinishTime.ShouldBe(30);

            scheduledActivities0[4].Id.ShouldBe(activityId8);
            scheduledActivities0[4].HasNoCost.ShouldBeFalse();
            scheduledActivities0[4].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[4].StartTime.ShouldBe(30);
            scheduledActivities0[4].FinishTime.ShouldBe(34);

            scheduledActivities0.Last().FinishTime.ShouldBe(34);


            resourceSchedules[1].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, false, false, false,
                });

            resourceSchedules[1].CostAllocation.ShouldBe(
                new bool[] {
                    false, false, false, false, false, false, false, true, true, true,
                    true, true, true, false, false, false, false, false, false, false,
                    false, true, true, true, true, true, true, true, true, true,
                    true, false, false, false,
                });

            resourceSchedules[1].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, false, false, false,
                });

            var resourceSchedule1 = resourceSchedules[1];
            resourceSchedule1.Resource.Id.ShouldBe(resourceId2);
            var scheduledActivities1 = resourceSchedule1.ScheduledActivities.ToList();
            scheduledActivities1.Count.ShouldBe(4);

            scheduledActivities1[0].Id.ShouldBe(activityId2);
            scheduledActivities1[0].HasNoCost.ShouldBeTrue();
            scheduledActivities1[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities1[0].StartTime.ShouldBe(0);
            scheduledActivities1[0].FinishTime.ShouldBe(7);

            scheduledActivities1[1].Id.ShouldBe(activityId1);
            scheduledActivities1[1].HasNoCost.ShouldBeFalse();
            scheduledActivities1[1].HasNoEffort.ShouldBeFalse();
            scheduledActivities1[1].StartTime.ShouldBe(7);
            scheduledActivities1[1].FinishTime.ShouldBe(13);

            scheduledActivities1[2].Id.ShouldBe(activityId5);
            scheduledActivities1[2].HasNoCost.ShouldBeTrue();
            scheduledActivities1[2].HasNoEffort.ShouldBeFalse();
            scheduledActivities1[2].StartTime.ShouldBe(13);
            scheduledActivities1[2].FinishTime.ShouldBe(21);

            scheduledActivities1[3].Id.ShouldBe(activityId9);
            scheduledActivities1[3].HasNoCost.ShouldBeFalse();
            scheduledActivities1[3].HasNoEffort.ShouldBeFalse();
            scheduledActivities1[3].StartTime.ShouldBe(21);
            scheduledActivities1[3].FinishTime.ShouldBe(31);

            scheduledActivities1.Last().FinishTime.ShouldBe(31);



            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(7);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(13);
            graphBuilder.Activity(activityId1).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId1).TotalSlack.ShouldBe(3);
            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(10);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(16);
            graphBuilder.Activity(activityId1).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId1).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId1).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId1).Successors.Contains(activityId5).ShouldBeTrue();

            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(7);
            graphBuilder.Activity(activityId2).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId2).TotalSlack.ShouldBe(1);
            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(1);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId2).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId2).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId2).Successors.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId2).Successors.Contains(activityId5).ShouldBeTrue();

            graphBuilder.Activity(activityId3).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId3).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId3).Successors.Contains(activityId5).ShouldBeTrue();
            graphBuilder.Activity(activityId3).Successors.Contains(activityId6).ShouldBeTrue();

            graphBuilder.Activity(activityId4).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId4).EarliestFinishTime.ShouldBe(19);
            graphBuilder.Activity(activityId4).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).LatestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId4).LatestFinishTime.ShouldBe(19);
            graphBuilder.Activity(activityId4).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId4).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId4).PlanningDependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId4).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId3 }), ignoreOrder: true);
            graphBuilder.Activity(activityId4).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId4).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId4).Successors.Contains(activityId7).ShouldBeTrue();
            graphBuilder.Activity(activityId4).Successors.Contains(activityId8).ShouldBeTrue();

            graphBuilder.Activity(activityId5).EarliestStartTime.ShouldBe(13);
            graphBuilder.Activity(activityId5).EarliestFinishTime.ShouldBe(21);
            graphBuilder.Activity(activityId5).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).TotalSlack.ShouldBe(3);
            graphBuilder.Activity(activityId5).LatestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId5).LatestFinishTime.ShouldBe(24);
            graphBuilder.Activity(activityId5).Dependencies.Count.ShouldBe(2);
            graphBuilder.Activity(activityId5).Dependencies.Contains(activityId1).ShouldBeTrue();
            graphBuilder.Activity(activityId5).Dependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId5).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId5).PlanningDependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId5).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId5).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId5).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId5).Successors.Contains(activityId9).ShouldBeTrue();

            graphBuilder.Activity(activityId6).EarliestStartTime.ShouldBe(19);
            graphBuilder.Activity(activityId6).EarliestFinishTime.ShouldBe(26);
            graphBuilder.Activity(activityId6).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId6).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId6).LatestStartTime.ShouldBe(19);
            graphBuilder.Activity(activityId6).LatestFinishTime.ShouldBe(26);
            graphBuilder.Activity(activityId6).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId6).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).PlanningDependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId6).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId4 }), ignoreOrder: true);
            graphBuilder.Activity(activityId6).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId6).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).Successors.Contains(activityId8).ShouldBeTrue();

            graphBuilder.Activity(activityId7).EarliestStartTime.ShouldBe(26);
            graphBuilder.Activity(activityId7).EarliestFinishTime.ShouldBe(30);
            graphBuilder.Activity(activityId7).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId7).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId7).LatestStartTime.ShouldBe(26);
            graphBuilder.Activity(activityId7).LatestFinishTime.ShouldBe(30);
            graphBuilder.Activity(activityId7).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId7).Dependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId7).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId7).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId6 }), ignoreOrder: true);
            graphBuilder.Activity(activityId7).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId7).Successors.Count.ShouldBe(0);

            graphBuilder.Activity(activityId8).EarliestStartTime.ShouldBe(30);
            graphBuilder.Activity(activityId8).EarliestFinishTime.ShouldBe(34);
            graphBuilder.Activity(activityId8).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId8).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId8).LatestStartTime.ShouldBe(30);
            graphBuilder.Activity(activityId8).LatestFinishTime.ShouldBe(34);
            graphBuilder.Activity(activityId8).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId8).Dependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId8).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId8).PlanningDependencies.Contains(activityId6).ShouldBeTrue();
            graphBuilder.Activity(activityId8).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId7 }), ignoreOrder: true);
            graphBuilder.Activity(activityId8).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId8).Successors.Count.ShouldBe(0);

            graphBuilder.Activity(activityId9).EarliestStartTime.ShouldBe(21);
            graphBuilder.Activity(activityId9).EarliestFinishTime.ShouldBe(31);
            graphBuilder.Activity(activityId9).FreeSlack.ShouldBe(3);
            graphBuilder.Activity(activityId9).TotalSlack.ShouldBe(3);
            graphBuilder.Activity(activityId9).LatestStartTime.ShouldBe(24);
            graphBuilder.Activity(activityId9).LatestFinishTime.ShouldBe(34);
            graphBuilder.Activity(activityId9).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId9).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId9).PlanningDependencies.Contains(activityId5).ShouldBeTrue();
            graphBuilder.Activity(activityId9).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId5 }), ignoreOrder: true);
            graphBuilder.Activity(activityId9).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId9).Successors.Count.ShouldBe(0);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithCompiledAndPlanningDependenciesAndOneActiveAndTwoInactiveResources_ThenResourceSchedulesCorrectOrder()
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
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            var graphBuilder = graphCompiler.Builder;
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId1, 6));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId2, 7) { HasNoCost = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId3, 8) { HasNoCost = true, HasNoEffort = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId4, 11, new HashSet<int>(), new HashSet<int> { activityId2 }) { HasNoEffort = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId5, 8, new HashSet<int> { activityId1, activityId2 }, new HashSet<int> { activityId3 }) { HasNoCost = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId6, 7, new HashSet<int>(), new HashSet<int> { activityId3 }) { HasNoCost = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId7, 4, new HashSet<int> { activityId4 }, new HashSet<int>()));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId8, 4, new HashSet<int> { activityId4 }, new HashSet<int> { activityId6 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId9, 10, new HashSet<int>(), new HashSet<int> { activityId5 }));

            int resourceId1 = 1;
            int resourceId2 = resourceId1 + 1;
            int resourceId3 = resourceId2 + 1;

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int, int>>(new[]
                {
                    new Resource<int, int>(resourceId1, string.Empty, false, true, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>()),
                    new Resource<int, int>(resourceId2, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>()),
                    new Resource<int, int>(resourceId3, string.Empty, false, true, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>()),
                }));

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(1);

            resourceSchedules[0].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true,
                });

            resourceSchedules[0].CostAllocation.ShouldBe(
                new bool[] {
                    false, false, false, false, false, false, false, false, false, false,
                    false, false, false, false, false, true, true, true, true, true,
                    true, false, false, false, false, false, false, false, false, true,
                    true, true, true, true, true, true, true, true, true, true,
                    false, false, false, false, false, false, false, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true,
                });

            resourceSchedules[0].EffortAllocation.ShouldBe(
                new bool[] {
                    false, false, false, false, false, false, false, false, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, false,
                    false, false, false, false, false, false, false, false, false, false,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId2);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(9);

            scheduledActivities0[0].Id.ShouldBe(activityId3);
            scheduledActivities0[0].HasNoCost.ShouldBeTrue();
            scheduledActivities0[0].HasNoEffort.ShouldBeTrue();
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(8);

            scheduledActivities0[1].Id.ShouldBe(activityId2);
            scheduledActivities0[1].HasNoCost.ShouldBeTrue();
            scheduledActivities0[1].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[1].StartTime.ShouldBe(8);
            scheduledActivities0[1].FinishTime.ShouldBe(15);

            scheduledActivities0[2].Id.ShouldBe(activityId1);
            scheduledActivities0[2].HasNoCost.ShouldBeFalse();
            scheduledActivities0[2].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[2].StartTime.ShouldBe(15);
            scheduledActivities0[2].FinishTime.ShouldBe(21);

            scheduledActivities0[3].Id.ShouldBe(activityId5);
            scheduledActivities0[3].HasNoCost.ShouldBeTrue();
            scheduledActivities0[3].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[3].StartTime.ShouldBe(21);
            scheduledActivities0[3].FinishTime.ShouldBe(29);

            scheduledActivities0[4].Id.ShouldBe(activityId4);
            scheduledActivities0[4].HasNoCost.ShouldBeFalse();
            scheduledActivities0[4].HasNoEffort.ShouldBeTrue();
            scheduledActivities0[4].StartTime.ShouldBe(29);
            scheduledActivities0[4].FinishTime.ShouldBe(40);

            scheduledActivities0[5].Id.ShouldBe(activityId6);
            scheduledActivities0[5].HasNoCost.ShouldBeTrue();
            scheduledActivities0[5].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[5].StartTime.ShouldBe(40);
            scheduledActivities0[5].FinishTime.ShouldBe(47);

            scheduledActivities0[6].Id.ShouldBe(activityId9);
            scheduledActivities0[6].HasNoCost.ShouldBeFalse();
            scheduledActivities0[6].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[6].StartTime.ShouldBe(47);
            scheduledActivities0[6].FinishTime.ShouldBe(57);

            scheduledActivities0[7].Id.ShouldBe(activityId7);
            scheduledActivities0[7].HasNoCost.ShouldBeFalse();
            scheduledActivities0[7].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[7].StartTime.ShouldBe(57);
            scheduledActivities0[7].FinishTime.ShouldBe(61);

            scheduledActivities0[8].Id.ShouldBe(activityId8);
            scheduledActivities0[8].HasNoCost.ShouldBeFalse();
            scheduledActivities0[8].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[8].StartTime.ShouldBe(61);
            scheduledActivities0[8].FinishTime.ShouldBe(65);

            scheduledActivities0.Last().FinishTime.ShouldBe(65);

            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(15);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(21);
            graphBuilder.Activity(activityId1).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId1).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(15);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(21);
            graphBuilder.Activity(activityId1).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId1).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId1).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId1).Successors.Contains(activityId5).ShouldBeTrue();

            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(15);
            graphBuilder.Activity(activityId2).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId2).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(15);
            graphBuilder.Activity(activityId2).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId3 }), ignoreOrder: true);
            graphBuilder.Activity(activityId2).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId2).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId2).Successors.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId2).Successors.Contains(activityId5).ShouldBeTrue();

            graphBuilder.Activity(activityId3).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId3).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId3).Successors.Contains(activityId5).ShouldBeTrue();
            graphBuilder.Activity(activityId3).Successors.Contains(activityId6).ShouldBeTrue();

            graphBuilder.Activity(activityId4).EarliestStartTime.ShouldBe(29);
            graphBuilder.Activity(activityId4).EarliestFinishTime.ShouldBe(40);
            graphBuilder.Activity(activityId4).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).LatestStartTime.ShouldBe(29);
            graphBuilder.Activity(activityId4).LatestFinishTime.ShouldBe(40);
            graphBuilder.Activity(activityId4).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId4).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId4).PlanningDependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId4).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId5 }), ignoreOrder: true);
            graphBuilder.Activity(activityId4).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId4).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId4).Successors.Contains(activityId7).ShouldBeTrue();
            graphBuilder.Activity(activityId4).Successors.Contains(activityId8).ShouldBeTrue();

            graphBuilder.Activity(activityId5).EarliestStartTime.ShouldBe(21);
            graphBuilder.Activity(activityId5).EarliestFinishTime.ShouldBe(29);
            graphBuilder.Activity(activityId5).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).LatestStartTime.ShouldBe(21);
            graphBuilder.Activity(activityId5).LatestFinishTime.ShouldBe(29);
            graphBuilder.Activity(activityId5).Dependencies.Count.ShouldBe(2);
            graphBuilder.Activity(activityId5).Dependencies.Contains(activityId1).ShouldBeTrue();
            graphBuilder.Activity(activityId5).Dependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId5).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId5).PlanningDependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId5).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId5).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId5).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId5).Successors.Contains(activityId9).ShouldBeTrue();

            graphBuilder.Activity(activityId6).EarliestStartTime.ShouldBe(40);
            graphBuilder.Activity(activityId6).EarliestFinishTime.ShouldBe(47);
            graphBuilder.Activity(activityId6).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId6).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId6).LatestStartTime.ShouldBe(40);
            graphBuilder.Activity(activityId6).LatestFinishTime.ShouldBe(47);
            graphBuilder.Activity(activityId6).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId6).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).PlanningDependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId6).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId4 }), ignoreOrder: true);
            graphBuilder.Activity(activityId6).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId6).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).Successors.Contains(activityId8).ShouldBeTrue();

            graphBuilder.Activity(activityId7).EarliestStartTime.ShouldBe(57);
            graphBuilder.Activity(activityId7).EarliestFinishTime.ShouldBe(61);
            graphBuilder.Activity(activityId7).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId7).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId7).LatestStartTime.ShouldBe(57);
            graphBuilder.Activity(activityId7).LatestFinishTime.ShouldBe(61);
            graphBuilder.Activity(activityId7).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId7).Dependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId7).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId7).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId9 }), ignoreOrder: true);
            graphBuilder.Activity(activityId7).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId7).Successors.Count.ShouldBe(0);

            graphBuilder.Activity(activityId8).EarliestStartTime.ShouldBe(61);
            graphBuilder.Activity(activityId8).EarliestFinishTime.ShouldBe(65);
            graphBuilder.Activity(activityId8).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId8).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId8).LatestStartTime.ShouldBe(61);
            graphBuilder.Activity(activityId8).LatestFinishTime.ShouldBe(65);
            graphBuilder.Activity(activityId8).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId8).Dependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId8).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId8).PlanningDependencies.Contains(activityId6).ShouldBeTrue();
            graphBuilder.Activity(activityId8).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId7 }), ignoreOrder: true);
            graphBuilder.Activity(activityId8).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId8).Successors.Count.ShouldBe(0);

            graphBuilder.Activity(activityId9).EarliestStartTime.ShouldBe(47);
            graphBuilder.Activity(activityId9).EarliestFinishTime.ShouldBe(57);
            graphBuilder.Activity(activityId9).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).LatestStartTime.ShouldBe(47);
            graphBuilder.Activity(activityId9).LatestFinishTime.ShouldBe(57);
            graphBuilder.Activity(activityId9).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId9).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId9).PlanningDependencies.Contains(activityId5).ShouldBeTrue();
            graphBuilder.Activity(activityId9).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId6 }), ignoreOrder: true);
            graphBuilder.Activity(activityId9).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId9).Successors.Count.ShouldBe(0);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithCompiledAndPlanningDependenciesAndTwoIndirectResources_ThenResourceSchedulesCorrectOrder()
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
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            var graphBuilder = graphCompiler.Builder;
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId1, 6));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId2, 7));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId3, 8) { HasNoCost = true, HasNoEffort = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId4, 11, new HashSet<int>(), new HashSet<int> { activityId2 }) { HasNoEffort = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId5, 8, new HashSet<int> { activityId1, activityId2 }, new HashSet<int> { activityId3 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId6, 7, new HashSet<int>(), new HashSet<int> { activityId3 }) { HasNoCost = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId7, 4, new HashSet<int> { activityId4 }, new HashSet<int>()));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId8, 4, new HashSet<int> { activityId4 }, new HashSet<int> { activityId6 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId9, 10, new HashSet<int>(), new HashSet<int> { activityId5 }));

            int resourceId1 = 1;
            int resourceId2 = resourceId1 + 1;
            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int, int>>(new[]
                {
                    new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.Indirect, 1.0, 0, Enumerable.Empty<int>()),
                    new Resource<int, int>(resourceId2, string.Empty, false, false, InterActivityAllocationType.Indirect, 1.0, 0, Enumerable.Empty<int>()),
                }));

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(2);

            resourceSchedules[0].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true,
                });

            resourceSchedules[0].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true,
                });

            resourceSchedules[0].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(5);

            scheduledActivities0[0].Id.ShouldBe(activityId3);
            scheduledActivities0[0].HasNoCost.ShouldBeTrue();
            scheduledActivities0[0].HasNoEffort.ShouldBeTrue();
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(8);

            scheduledActivities0[1].Id.ShouldBe(activityId4);
            scheduledActivities0[1].HasNoCost.ShouldBeFalse();
            scheduledActivities0[1].HasNoEffort.ShouldBeTrue();
            scheduledActivities0[1].StartTime.ShouldBe(8);
            scheduledActivities0[1].FinishTime.ShouldBe(19);

            scheduledActivities0[2].Id.ShouldBe(activityId6);
            scheduledActivities0[2].HasNoCost.ShouldBeTrue();
            scheduledActivities0[2].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[2].StartTime.ShouldBe(19);
            scheduledActivities0[2].FinishTime.ShouldBe(26);

            scheduledActivities0[3].Id.ShouldBe(activityId7);
            scheduledActivities0[3].HasNoCost.ShouldBeFalse();
            scheduledActivities0[3].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[3].StartTime.ShouldBe(26);
            scheduledActivities0[3].FinishTime.ShouldBe(30);

            scheduledActivities0[4].Id.ShouldBe(activityId8);
            scheduledActivities0[4].HasNoCost.ShouldBeFalse();
            scheduledActivities0[4].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[4].StartTime.ShouldBe(30);
            scheduledActivities0[4].FinishTime.ShouldBe(34);

            scheduledActivities0.Last().FinishTime.ShouldBe(34);


            resourceSchedules[1].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true,
                });

            resourceSchedules[1].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true,
                });

            resourceSchedules[1].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true,
                });

            var resourceSchedule1 = resourceSchedules[1];
            resourceSchedule1.Resource.Id.ShouldBe(resourceId2);
            var scheduledActivities1 = resourceSchedule1.ScheduledActivities.ToList();
            scheduledActivities1.Count.ShouldBe(4);

            scheduledActivities1[0].Id.ShouldBe(activityId2);
            scheduledActivities1[0].HasNoCost.ShouldBeFalse();
            scheduledActivities1[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities1[0].StartTime.ShouldBe(0);
            scheduledActivities1[0].FinishTime.ShouldBe(7);

            scheduledActivities1[1].Id.ShouldBe(activityId1);
            scheduledActivities1[1].HasNoCost.ShouldBeFalse();
            scheduledActivities1[1].HasNoEffort.ShouldBeFalse();
            scheduledActivities1[1].StartTime.ShouldBe(7);
            scheduledActivities1[1].FinishTime.ShouldBe(13);

            scheduledActivities1[2].Id.ShouldBe(activityId5);
            scheduledActivities1[2].HasNoCost.ShouldBeFalse();
            scheduledActivities1[2].HasNoEffort.ShouldBeFalse();
            scheduledActivities1[2].StartTime.ShouldBe(13);
            scheduledActivities1[2].FinishTime.ShouldBe(21);

            scheduledActivities1[3].Id.ShouldBe(activityId9);
            scheduledActivities1[3].HasNoCost.ShouldBeFalse();
            scheduledActivities1[3].HasNoEffort.ShouldBeFalse();
            scheduledActivities1[3].StartTime.ShouldBe(21);
            scheduledActivities1[3].FinishTime.ShouldBe(31);

            scheduledActivities1.Last().FinishTime.ShouldBe(31);



            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(7);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(13);
            graphBuilder.Activity(activityId1).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId1).TotalSlack.ShouldBe(3);
            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(10);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(16);
            graphBuilder.Activity(activityId1).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId1).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId1).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId1).Successors.Contains(activityId5).ShouldBeTrue();

            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(7);
            graphBuilder.Activity(activityId2).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId2).TotalSlack.ShouldBe(1);
            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(1);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId2).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId2).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId2).Successors.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId2).Successors.Contains(activityId5).ShouldBeTrue();

            graphBuilder.Activity(activityId3).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId3).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId3).Successors.Contains(activityId5).ShouldBeTrue();
            graphBuilder.Activity(activityId3).Successors.Contains(activityId6).ShouldBeTrue();

            graphBuilder.Activity(activityId4).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId4).EarliestFinishTime.ShouldBe(19);
            graphBuilder.Activity(activityId4).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).LatestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId4).LatestFinishTime.ShouldBe(19);
            graphBuilder.Activity(activityId4).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId4).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId4).PlanningDependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId4).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId3 }), ignoreOrder: true);
            graphBuilder.Activity(activityId4).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId4).Successors.Count.ShouldBe(2);
            graphBuilder.Activity(activityId4).Successors.Contains(activityId7).ShouldBeTrue();
            graphBuilder.Activity(activityId4).Successors.Contains(activityId8).ShouldBeTrue();

            graphBuilder.Activity(activityId5).EarliestStartTime.ShouldBe(13);
            graphBuilder.Activity(activityId5).EarliestFinishTime.ShouldBe(21);
            graphBuilder.Activity(activityId5).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).TotalSlack.ShouldBe(3);
            graphBuilder.Activity(activityId5).LatestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId5).LatestFinishTime.ShouldBe(24);
            graphBuilder.Activity(activityId5).Dependencies.Count.ShouldBe(2);
            graphBuilder.Activity(activityId5).Dependencies.Contains(activityId1).ShouldBeTrue();
            graphBuilder.Activity(activityId5).Dependencies.Contains(activityId2).ShouldBeTrue();
            graphBuilder.Activity(activityId5).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId5).PlanningDependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId5).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId5).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId5).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId5).Successors.Contains(activityId9).ShouldBeTrue();

            graphBuilder.Activity(activityId6).EarliestStartTime.ShouldBe(19);
            graphBuilder.Activity(activityId6).EarliestFinishTime.ShouldBe(26);
            graphBuilder.Activity(activityId6).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId6).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId6).LatestStartTime.ShouldBe(19);
            graphBuilder.Activity(activityId6).LatestFinishTime.ShouldBe(26);
            graphBuilder.Activity(activityId6).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId6).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).PlanningDependencies.Contains(activityId3).ShouldBeTrue();
            graphBuilder.Activity(activityId6).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId4 }), ignoreOrder: true);
            graphBuilder.Activity(activityId6).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId6).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId6).Successors.Contains(activityId8).ShouldBeTrue();

            graphBuilder.Activity(activityId7).EarliestStartTime.ShouldBe(26);
            graphBuilder.Activity(activityId7).EarliestFinishTime.ShouldBe(30);
            graphBuilder.Activity(activityId7).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId7).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId7).LatestStartTime.ShouldBe(26);
            graphBuilder.Activity(activityId7).LatestFinishTime.ShouldBe(30);
            graphBuilder.Activity(activityId7).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId7).Dependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId7).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId7).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId6 }), ignoreOrder: true);
            graphBuilder.Activity(activityId7).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId7).Successors.Count.ShouldBe(0);

            graphBuilder.Activity(activityId8).EarliestStartTime.ShouldBe(30);
            graphBuilder.Activity(activityId8).EarliestFinishTime.ShouldBe(34);
            graphBuilder.Activity(activityId8).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId8).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId8).LatestStartTime.ShouldBe(30);
            graphBuilder.Activity(activityId8).LatestFinishTime.ShouldBe(34);
            graphBuilder.Activity(activityId8).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId8).Dependencies.Contains(activityId4).ShouldBeTrue();
            graphBuilder.Activity(activityId8).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId8).PlanningDependencies.Contains(activityId6).ShouldBeTrue();
            graphBuilder.Activity(activityId8).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId7 }), ignoreOrder: true);
            graphBuilder.Activity(activityId8).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId8).Successors.Count.ShouldBe(0);

            graphBuilder.Activity(activityId9).EarliestStartTime.ShouldBe(21);
            graphBuilder.Activity(activityId9).EarliestFinishTime.ShouldBe(31);
            graphBuilder.Activity(activityId9).FreeSlack.ShouldBe(3);
            graphBuilder.Activity(activityId9).TotalSlack.ShouldBe(3);
            graphBuilder.Activity(activityId9).LatestStartTime.ShouldBe(24);
            graphBuilder.Activity(activityId9).LatestFinishTime.ShouldBe(34);
            graphBuilder.Activity(activityId9).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId9).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId9).PlanningDependencies.Contains(activityId5).ShouldBeTrue();
            graphBuilder.Activity(activityId9).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId5 }), ignoreOrder: true);
            graphBuilder.Activity(activityId9).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId9).Successors.Count.ShouldBe(0);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithNoActivitiesAndOneIndirectResource_ThenResourceSchedulesCorrectOrder()
        {
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();

            int resourceId1 = 1;
            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int, int>>(new[]
                {
                    new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.Indirect, 1.0, 0, Enumerable.Empty<int>()),
                }));

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(1);
            resourceSchedules[0].ActivityAllocation.ShouldBeEmpty();
            resourceSchedules[0].CostAllocation.ShouldBeEmpty();
            resourceSchedules[0].EffortAllocation.ShouldBeEmpty();
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithOneOfEachTypeResources_WithNoUncostedAndUneffortedActivities_ThenOutputsAsExpected()
        {
            int resourceId1 = 1;
            int resourceId2 = resourceId1 + 1;
            int resourceId3 = resourceId2 + 1;

            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;

            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();

            var resource1 = new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.Direct, 1.0, 0, Enumerable.Empty<int>());
            var resource2 = new Resource<int, int>(resourceId2, string.Empty, false, false, InterActivityAllocationType.Indirect, 1.0, 0, Enumerable.Empty<int>());
            var resource3 = new Resource<int, int>(resourceId3, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>());

            var activity1 = new DependentActivity<int, int, int>(activityId1, 5);
            activity1.TargetResources.Add(resourceId1);
            var activity2 = new DependentActivity<int, int, int>(activityId2, 3);
            activity2.TargetResources.Add(resourceId2);
            var activity3 = new DependentActivity<int, int, int>(activityId3, 12);
            activity3.TargetResources.Add(resourceId3);


            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int, int>>(new[] { resource1, resource2, resource3 }));

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(3);

            resourceSchedules[0].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, false, false, false, false, false,
                    false, false,
                });

            resourceSchedules[0].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, false, false, false, false, false,
                    false, false,
                });

            resourceSchedules[0].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, false, false, false, false, false,
                    false, false,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(1);

            scheduledActivities0[0].Id.ShouldBe(activityId1);
            scheduledActivities0[0].HasNoCost.ShouldBeFalse();
            scheduledActivities0[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(5);

            scheduledActivities0.Last().FinishTime.ShouldBe(5);


            resourceSchedules[1].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            resourceSchedules[1].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            resourceSchedules[1].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            var resourceSchedule1 = resourceSchedules[1];
            resourceSchedule1.Resource.Id.ShouldBe(resourceId2);
            var scheduledActivities1 = resourceSchedule1.ScheduledActivities.ToList();
            scheduledActivities1.Count.ShouldBe(1);

            scheduledActivities1[0].Id.ShouldBe(activityId2);
            scheduledActivities1[0].HasNoCost.ShouldBeFalse();
            scheduledActivities1[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities1[0].StartTime.ShouldBe(0);
            scheduledActivities1[0].FinishTime.ShouldBe(3);

            scheduledActivities1.Last().FinishTime.ShouldBe(3);


            resourceSchedules[2].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            resourceSchedules[2].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            resourceSchedules[2].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            var resourceSchedule2 = resourceSchedules[2];
            resourceSchedule2.Resource.Id.ShouldBe(resourceId3);
            var scheduledActivities2 = resourceSchedule2.ScheduledActivities.ToList();
            scheduledActivities2.Count.ShouldBe(1);

            scheduledActivities2[0].Id.ShouldBe(activityId3);
            scheduledActivities2[0].HasNoCost.ShouldBeFalse();
            scheduledActivities2[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities2[0].StartTime.ShouldBe(0);
            scheduledActivities2[0].FinishTime.ShouldBe(12);

            scheduledActivities2.Last().FinishTime.ShouldBe(12);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithOneOfEachTypeResources_WithUncostedDirectActivity_ThenDirectCostsRemoved()
        {
            int resourceId1 = 1;
            int resourceId2 = resourceId1 + 1;
            int resourceId3 = resourceId2 + 1;

            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;

            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();

            var resource1 = new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.Direct, 1.0, 0, Enumerable.Empty<int>());
            var resource2 = new Resource<int, int>(resourceId2, string.Empty, false, false, InterActivityAllocationType.Indirect, 1.0, 0, Enumerable.Empty<int>());
            var resource3 = new Resource<int, int>(resourceId3, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>());

            var activity1 = new DependentActivity<int, int, int>(activityId1, 5) { HasNoCost = true };
            activity1.TargetResources.Add(resourceId1);
            var activity2 = new DependentActivity<int, int, int>(activityId2, 3);
            activity2.TargetResources.Add(resourceId2);
            var activity3 = new DependentActivity<int, int, int>(activityId3, 12);
            activity3.TargetResources.Add(resourceId3);


            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int, int>>(new[] { resource1, resource2, resource3 }));

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(3);

            resourceSchedules[0].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, false, false, false, false, false,
                    false, false,
                });

            resourceSchedules[0].CostAllocation.ShouldBe(
                new bool[] {
                    false, false, false, false, false, false, false, false, false, false,
                    false, false,
                });

            resourceSchedules[0].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, false, false, false, false, false,
                    false, false,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(1);

            scheduledActivities0[0].Id.ShouldBe(activityId1);
            scheduledActivities0[0].HasNoCost.ShouldBeTrue();
            scheduledActivities0[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(5);

            scheduledActivities0.Last().FinishTime.ShouldBe(5);


            resourceSchedules[1].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            resourceSchedules[1].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            resourceSchedules[1].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            var resourceSchedule1 = resourceSchedules[1];
            resourceSchedule1.Resource.Id.ShouldBe(resourceId2);
            var scheduledActivities1 = resourceSchedule1.ScheduledActivities.ToList();
            scheduledActivities1.Count.ShouldBe(1);

            scheduledActivities1[0].Id.ShouldBe(activityId2);
            scheduledActivities1[0].HasNoCost.ShouldBeFalse();
            scheduledActivities1[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities1[0].StartTime.ShouldBe(0);
            scheduledActivities1[0].FinishTime.ShouldBe(3);

            scheduledActivities1.Last().FinishTime.ShouldBe(3);


            resourceSchedules[2].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            resourceSchedules[2].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            resourceSchedules[2].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            var resourceSchedule2 = resourceSchedules[2];
            resourceSchedule2.Resource.Id.ShouldBe(resourceId3);
            var scheduledActivities2 = resourceSchedule2.ScheduledActivities.ToList();
            scheduledActivities2.Count.ShouldBe(1);

            scheduledActivities2[0].Id.ShouldBe(activityId3);
            scheduledActivities2[0].HasNoCost.ShouldBeFalse();
            scheduledActivities2[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities2[0].StartTime.ShouldBe(0);
            scheduledActivities2[0].FinishTime.ShouldBe(12);

            scheduledActivities2.Last().FinishTime.ShouldBe(12);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithOneOfEachTypeResources_WithUneffortedDirectActivity_ThenDirectEffortsRemoved()
        {
            int resourceId1 = 1;
            int resourceId2 = resourceId1 + 1;
            int resourceId3 = resourceId2 + 1;

            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;

            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();

            var resource1 = new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.Direct, 1.0, 0, Enumerable.Empty<int>());
            var resource2 = new Resource<int, int>(resourceId2, string.Empty, false, false, InterActivityAllocationType.Indirect, 1.0, 0, Enumerable.Empty<int>());
            var resource3 = new Resource<int, int>(resourceId3, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>());

            var activity1 = new DependentActivity<int, int, int>(activityId1, 5) { HasNoEffort = true };
            activity1.TargetResources.Add(resourceId1);
            var activity2 = new DependentActivity<int, int, int>(activityId2, 3);
            activity2.TargetResources.Add(resourceId2);
            var activity3 = new DependentActivity<int, int, int>(activityId3, 12);
            activity3.TargetResources.Add(resourceId3);


            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int, int>>(new[] { resource1, resource2, resource3 }));

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(3);

            resourceSchedules[0].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, false, false, false, false, false,
                    false, false,
                });

            resourceSchedules[0].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, false, false, false, false, false,
                    false, false,
                });

            resourceSchedules[0].EffortAllocation.ShouldBe(
                new bool[] {
                    false, false, false, false, false, false, false, false, false, false,
                    false, false,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(1);

            scheduledActivities0[0].Id.ShouldBe(activityId1);
            scheduledActivities0[0].HasNoCost.ShouldBeFalse();
            scheduledActivities0[0].HasNoEffort.ShouldBeTrue();
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(5);

            scheduledActivities0.Last().FinishTime.ShouldBe(5);


            resourceSchedules[1].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            resourceSchedules[1].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            resourceSchedules[1].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            var resourceSchedule1 = resourceSchedules[1];
            resourceSchedule1.Resource.Id.ShouldBe(resourceId2);
            var scheduledActivities1 = resourceSchedule1.ScheduledActivities.ToList();
            scheduledActivities1.Count.ShouldBe(1);

            scheduledActivities1[0].Id.ShouldBe(activityId2);
            scheduledActivities1[0].HasNoCost.ShouldBeFalse();
            scheduledActivities1[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities1[0].StartTime.ShouldBe(0);
            scheduledActivities1[0].FinishTime.ShouldBe(3);

            scheduledActivities1.Last().FinishTime.ShouldBe(3);


            resourceSchedules[2].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            resourceSchedules[2].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            resourceSchedules[2].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            var resourceSchedule2 = resourceSchedules[2];
            resourceSchedule2.Resource.Id.ShouldBe(resourceId3);
            var scheduledActivities2 = resourceSchedule2.ScheduledActivities.ToList();
            scheduledActivities2.Count.ShouldBe(1);

            scheduledActivities2[0].Id.ShouldBe(activityId3);
            scheduledActivities2[0].HasNoCost.ShouldBeFalse();
            scheduledActivities2[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities2[0].StartTime.ShouldBe(0);
            scheduledActivities2[0].FinishTime.ShouldBe(12);

            scheduledActivities2.Last().FinishTime.ShouldBe(12);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithOneOfEachTypeResources_WithUncostedIndirectActivity_ThenIndirectCostsUnaffected()
        {
            int resourceId1 = 1;
            int resourceId2 = resourceId1 + 1;
            int resourceId3 = resourceId2 + 1;

            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;

            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();

            var resource1 = new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.Direct, 1.0, 0, Enumerable.Empty<int>());
            var resource2 = new Resource<int, int>(resourceId2, string.Empty, false, false, InterActivityAllocationType.Indirect, 1.0, 0, Enumerable.Empty<int>());
            var resource3 = new Resource<int, int>(resourceId3, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>());

            var activity1 = new DependentActivity<int, int, int>(activityId1, 5);
            activity1.TargetResources.Add(resourceId1);
            var activity2 = new DependentActivity<int, int, int>(activityId2, 3) { HasNoCost = true };
            activity2.TargetResources.Add(resourceId2);
            var activity3 = new DependentActivity<int, int, int>(activityId3, 12);
            activity3.TargetResources.Add(resourceId3);


            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int, int>>(new[] { resource1, resource2, resource3 }));

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(3);

            resourceSchedules[0].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, false, false, false, false, false,
                    false, false,
                });

            resourceSchedules[0].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, false, false, false, false, false,
                    false, false,
                });

            resourceSchedules[0].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, false, false, false, false, false,
                    false, false,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(1);

            scheduledActivities0[0].Id.ShouldBe(activityId1);
            scheduledActivities0[0].HasNoCost.ShouldBeFalse();
            scheduledActivities0[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(5);

            scheduledActivities0.Last().FinishTime.ShouldBe(5);


            resourceSchedules[1].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            resourceSchedules[1].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            resourceSchedules[1].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            var resourceSchedule1 = resourceSchedules[1];
            resourceSchedule1.Resource.Id.ShouldBe(resourceId2);
            var scheduledActivities1 = resourceSchedule1.ScheduledActivities.ToList();
            scheduledActivities1.Count.ShouldBe(1);

            scheduledActivities1[0].Id.ShouldBe(activityId2);
            scheduledActivities1[0].HasNoCost.ShouldBeTrue();
            scheduledActivities1[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities1[0].StartTime.ShouldBe(0);
            scheduledActivities1[0].FinishTime.ShouldBe(3);

            scheduledActivities1.Last().FinishTime.ShouldBe(3);


            resourceSchedules[2].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            resourceSchedules[2].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            resourceSchedules[2].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            var resourceSchedule2 = resourceSchedules[2];
            resourceSchedule2.Resource.Id.ShouldBe(resourceId3);
            var scheduledActivities2 = resourceSchedule2.ScheduledActivities.ToList();
            scheduledActivities2.Count.ShouldBe(1);

            scheduledActivities2[0].Id.ShouldBe(activityId3);
            scheduledActivities2[0].HasNoCost.ShouldBeFalse();
            scheduledActivities2[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities2[0].StartTime.ShouldBe(0);
            scheduledActivities2[0].FinishTime.ShouldBe(12);

            scheduledActivities2.Last().FinishTime.ShouldBe(12);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithOneOfEachTypeResources_WithUneffortedIndirectActivity_ThenIndirectEffortsUnaffected()
        {
            int resourceId1 = 1;
            int resourceId2 = resourceId1 + 1;
            int resourceId3 = resourceId2 + 1;

            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;

            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();

            var resource1 = new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.Direct, 1.0, 0, Enumerable.Empty<int>());
            var resource2 = new Resource<int, int>(resourceId2, string.Empty, false, false, InterActivityAllocationType.Indirect, 1.0, 0, Enumerable.Empty<int>());
            var resource3 = new Resource<int, int>(resourceId3, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>());

            var activity1 = new DependentActivity<int, int, int>(activityId1, 5);
            activity1.TargetResources.Add(resourceId1);
            var activity2 = new DependentActivity<int, int, int>(activityId2, 3) { HasNoEffort = true };
            activity2.TargetResources.Add(resourceId2);
            var activity3 = new DependentActivity<int, int, int>(activityId3, 12);
            activity3.TargetResources.Add(resourceId3);


            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int, int>>(new[] { resource1, resource2, resource3 }));

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(3);

            resourceSchedules[0].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, false, false, false, false, false,
                    false, false,
                });

            resourceSchedules[0].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, false, false, false, false, false,
                    false, false,
                });

            resourceSchedules[0].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, false, false, false, false, false,
                    false, false,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(1);

            scheduledActivities0[0].Id.ShouldBe(activityId1);
            scheduledActivities0[0].HasNoCost.ShouldBeFalse();
            scheduledActivities0[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(5);

            scheduledActivities0.Last().FinishTime.ShouldBe(5);


            resourceSchedules[1].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            resourceSchedules[1].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            resourceSchedules[1].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            var resourceSchedule1 = resourceSchedules[1];
            resourceSchedule1.Resource.Id.ShouldBe(resourceId2);
            var scheduledActivities1 = resourceSchedule1.ScheduledActivities.ToList();
            scheduledActivities1.Count.ShouldBe(1);

            scheduledActivities1[0].Id.ShouldBe(activityId2);
            scheduledActivities1[0].HasNoCost.ShouldBeFalse();
            scheduledActivities1[0].HasNoEffort.ShouldBeTrue();
            scheduledActivities1[0].StartTime.ShouldBe(0);
            scheduledActivities1[0].FinishTime.ShouldBe(3);

            scheduledActivities1.Last().FinishTime.ShouldBe(3);


            resourceSchedules[2].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            resourceSchedules[2].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            resourceSchedules[2].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            var resourceSchedule2 = resourceSchedules[2];
            resourceSchedule2.Resource.Id.ShouldBe(resourceId3);
            var scheduledActivities2 = resourceSchedule2.ScheduledActivities.ToList();
            scheduledActivities2.Count.ShouldBe(1);

            scheduledActivities2[0].Id.ShouldBe(activityId3);
            scheduledActivities2[0].HasNoCost.ShouldBeFalse();
            scheduledActivities2[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities2[0].StartTime.ShouldBe(0);
            scheduledActivities2[0].FinishTime.ShouldBe(12);

            scheduledActivities2.Last().FinishTime.ShouldBe(12);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithOneOfEachTypeResources_WithUncostedNoneActivity_ThenNoneCostsRemoved()
        {
            int resourceId1 = 1;
            int resourceId2 = resourceId1 + 1;
            int resourceId3 = resourceId2 + 1;

            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;

            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();

            var resource1 = new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.Direct, 1.0, 0, Enumerable.Empty<int>());
            var resource2 = new Resource<int, int>(resourceId2, string.Empty, false, false, InterActivityAllocationType.Indirect, 1.0, 0, Enumerable.Empty<int>());
            var resource3 = new Resource<int, int>(resourceId3, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>());

            var activity1 = new DependentActivity<int, int, int>(activityId1, 5);
            activity1.TargetResources.Add(resourceId1);
            var activity2 = new DependentActivity<int, int, int>(activityId2, 3);
            activity2.TargetResources.Add(resourceId2);
            var activity3 = new DependentActivity<int, int, int>(activityId3, 12) { HasNoCost = true };
            activity3.TargetResources.Add(resourceId3);


            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int, int>>(new[] { resource1, resource2, resource3 }));

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(3);

            resourceSchedules[0].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, false, false, false, false, false,
                    false, false,
                });

            resourceSchedules[0].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, false, false, false, false, false,
                    false, false,
                });

            resourceSchedules[0].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, false, false, false, false, false,
                    false, false,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(1);

            scheduledActivities0[0].Id.ShouldBe(activityId1);
            scheduledActivities0[0].HasNoCost.ShouldBeFalse();
            scheduledActivities0[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(5);

            scheduledActivities0.Last().FinishTime.ShouldBe(5);


            resourceSchedules[1].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            resourceSchedules[1].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            resourceSchedules[1].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            var resourceSchedule1 = resourceSchedules[1];
            resourceSchedule1.Resource.Id.ShouldBe(resourceId2);
            var scheduledActivities1 = resourceSchedule1.ScheduledActivities.ToList();
            scheduledActivities1.Count.ShouldBe(1);

            scheduledActivities1[0].Id.ShouldBe(activityId2);
            scheduledActivities1[0].HasNoCost.ShouldBeFalse();
            scheduledActivities1[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities1[0].StartTime.ShouldBe(0);
            scheduledActivities1[0].FinishTime.ShouldBe(3);

            scheduledActivities1.Last().FinishTime.ShouldBe(3);


            resourceSchedules[2].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            resourceSchedules[2].CostAllocation.ShouldBe(
                new bool[] {
                    false, false, false, false, false, false, false, false, false, false,
                    false, false,
                });

            resourceSchedules[2].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            var resourceSchedule2 = resourceSchedules[2];
            resourceSchedule2.Resource.Id.ShouldBe(resourceId3);
            var scheduledActivities2 = resourceSchedule2.ScheduledActivities.ToList();
            scheduledActivities2.Count.ShouldBe(1);

            scheduledActivities2[0].Id.ShouldBe(activityId3);
            scheduledActivities2[0].HasNoCost.ShouldBeTrue();
            scheduledActivities2[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities2[0].StartTime.ShouldBe(0);
            scheduledActivities2[0].FinishTime.ShouldBe(12);

            scheduledActivities2.Last().FinishTime.ShouldBe(12);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithOneOfEachTypeResources_WithUneffortedNoneActivity_ThenNoneEffortsRemoved()
        {
            int resourceId1 = 1;
            int resourceId2 = resourceId1 + 1;
            int resourceId3 = resourceId2 + 1;

            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;

            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();

            var resource1 = new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.Direct, 1.0, 0, Enumerable.Empty<int>());
            var resource2 = new Resource<int, int>(resourceId2, string.Empty, false, false, InterActivityAllocationType.Indirect, 1.0, 0, Enumerable.Empty<int>());
            var resource3 = new Resource<int, int>(resourceId3, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>());

            var activity1 = new DependentActivity<int, int, int>(activityId1, 5);
            activity1.TargetResources.Add(resourceId1);
            var activity2 = new DependentActivity<int, int, int>(activityId2, 3);
            activity2.TargetResources.Add(resourceId2);
            var activity3 = new DependentActivity<int, int, int>(activityId3, 12) { HasNoEffort = true };
            activity3.TargetResources.Add(resourceId3);


            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int, int>>(new[] { resource1, resource2, resource3 }));

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(3);

            resourceSchedules[0].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, false, false, false, false, false,
                    false, false,
                });

            resourceSchedules[0].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, false, false, false, false, false,
                    false, false,
                });

            resourceSchedules[0].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, false, false, false, false, false,
                    false, false,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(1);

            scheduledActivities0[0].Id.ShouldBe(activityId1);
            scheduledActivities0[0].HasNoCost.ShouldBeFalse();
            scheduledActivities0[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(5);

            scheduledActivities0.Last().FinishTime.ShouldBe(5);


            resourceSchedules[1].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            resourceSchedules[1].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            resourceSchedules[1].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            var resourceSchedule1 = resourceSchedules[1];
            resourceSchedule1.Resource.Id.ShouldBe(resourceId2);
            var scheduledActivities1 = resourceSchedule1.ScheduledActivities.ToList();
            scheduledActivities1.Count.ShouldBe(1);

            scheduledActivities1[0].Id.ShouldBe(activityId2);
            scheduledActivities1[0].HasNoCost.ShouldBeFalse();
            scheduledActivities1[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities1[0].StartTime.ShouldBe(0);
            scheduledActivities1[0].FinishTime.ShouldBe(3);

            scheduledActivities1.Last().FinishTime.ShouldBe(3);


            resourceSchedules[2].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            resourceSchedules[2].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            resourceSchedules[2].EffortAllocation.ShouldBe(
                new bool[] {
                    false, false, false, false, false, false, false, false, false, false,
                    false, false,
                });

            var resourceSchedule2 = resourceSchedules[2];
            resourceSchedule2.Resource.Id.ShouldBe(resourceId3);
            var scheduledActivities2 = resourceSchedule2.ScheduledActivities.ToList();
            scheduledActivities2.Count.ShouldBe(1);

            scheduledActivities2[0].Id.ShouldBe(activityId3);
            scheduledActivities2[0].HasNoCost.ShouldBeFalse();
            scheduledActivities2[0].HasNoEffort.ShouldBeTrue();
            scheduledActivities2[0].StartTime.ShouldBe(0);
            scheduledActivities2[0].FinishTime.ShouldBe(12);

            scheduledActivities2.Last().FinishTime.ShouldBe(12);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithThreeDirectResources_WithUncostedFirstActivity_ThenFirstCostsRemoved()
        {
            int resourceId1 = 1;

            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;

            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();

            var resource1 = new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.Direct, 1.0, 0, Enumerable.Empty<int>());

            var activity1 = new DependentActivity<int, int, int>(activityId1, 5) { HasNoCost = true };
            activity1.TargetResources.Add(resourceId1);
            var activity2 = new DependentActivity<int, int, int>(activityId2, 3);
            activity2.TargetResources.Add(resourceId1);
            var activity3 = new DependentActivity<int, int, int>(activityId3, 12);
            activity3.TargetResources.Add(resourceId1);

            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int, int>>(new[] { resource1 }));

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(1);

            resourceSchedules[0].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            resourceSchedules[0].CostAllocation.ShouldBe(
                new bool[] {
                    false, false, false, false, false, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            resourceSchedules[0].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(3);

            scheduledActivities0[0].Id.ShouldBe(activityId1);
            scheduledActivities0[0].HasNoCost.ShouldBeTrue();
            scheduledActivities0[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(5);

            scheduledActivities0[1].Id.ShouldBe(activityId2);
            scheduledActivities0[1].HasNoCost.ShouldBeFalse();
            scheduledActivities0[1].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[1].StartTime.ShouldBe(5);
            scheduledActivities0[1].FinishTime.ShouldBe(8);

            scheduledActivities0[2].Id.ShouldBe(activityId3);
            scheduledActivities0[2].HasNoCost.ShouldBeFalse();
            scheduledActivities0[2].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[2].StartTime.ShouldBe(8);
            scheduledActivities0[2].FinishTime.ShouldBe(20);

            scheduledActivities0.Last().FinishTime.ShouldBe(20);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithThreeDirectResources_WithUneffortedFirstActivity_ThenFirstEffortsRemoved()
        {
            int resourceId1 = 1;

            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;

            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();

            var resource1 = new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.Direct, 1.0, 0, Enumerable.Empty<int>());

            var activity1 = new DependentActivity<int, int, int>(activityId1, 5) { HasNoEffort = true };
            activity1.TargetResources.Add(resourceId1);
            var activity2 = new DependentActivity<int, int, int>(activityId2, 3);
            activity2.TargetResources.Add(resourceId1);
            var activity3 = new DependentActivity<int, int, int>(activityId3, 12);
            activity3.TargetResources.Add(resourceId1);

            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int, int>>(new[] { resource1 }));

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(1);

            resourceSchedules[0].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            resourceSchedules[0].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            resourceSchedules[0].EffortAllocation.ShouldBe(
                new bool[] {
                    false, false, false, false, false, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(3);

            scheduledActivities0[0].Id.ShouldBe(activityId1);
            scheduledActivities0[0].HasNoCost.ShouldBeFalse();
            scheduledActivities0[0].HasNoEffort.ShouldBeTrue();
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(5);

            scheduledActivities0[1].Id.ShouldBe(activityId2);
            scheduledActivities0[1].HasNoCost.ShouldBeFalse();
            scheduledActivities0[1].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[1].StartTime.ShouldBe(5);
            scheduledActivities0[1].FinishTime.ShouldBe(8);

            scheduledActivities0[2].Id.ShouldBe(activityId3);
            scheduledActivities0[2].HasNoCost.ShouldBeFalse();
            scheduledActivities0[2].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[2].StartTime.ShouldBe(8);
            scheduledActivities0[2].FinishTime.ShouldBe(20);

            scheduledActivities0.Last().FinishTime.ShouldBe(20);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithThreeDirectResources_WithUncostedMiddleActivity_ThenMiddleCostsRemoved()
        {
            int resourceId1 = 1;

            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;

            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();

            var resource1 = new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.Direct, 1.0, 0, Enumerable.Empty<int>());

            var activity1 = new DependentActivity<int, int, int>(activityId1, 5);
            activity1.TargetResources.Add(resourceId1);
            var activity2 = new DependentActivity<int, int, int>(activityId2, 3) { HasNoCost = true };
            activity2.TargetResources.Add(resourceId1);
            var activity3 = new DependentActivity<int, int, int>(activityId3, 12);
            activity3.TargetResources.Add(resourceId1);

            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int, int>>(new[] { resource1 }));

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(1);

            resourceSchedules[0].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            resourceSchedules[0].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, false, false, false, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            resourceSchedules[0].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(3);

            scheduledActivities0[0].Id.ShouldBe(activityId1);
            scheduledActivities0[0].HasNoCost.ShouldBeFalse();
            scheduledActivities0[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(5);

            scheduledActivities0[1].Id.ShouldBe(activityId2);
            scheduledActivities0[1].HasNoCost.ShouldBeTrue();
            scheduledActivities0[1].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[1].StartTime.ShouldBe(5);
            scheduledActivities0[1].FinishTime.ShouldBe(8);

            scheduledActivities0[2].Id.ShouldBe(activityId3);
            scheduledActivities0[2].HasNoCost.ShouldBeFalse();
            scheduledActivities0[2].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[2].StartTime.ShouldBe(8);
            scheduledActivities0[2].FinishTime.ShouldBe(20);

            scheduledActivities0.Last().FinishTime.ShouldBe(20);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithThreeDirectResources_WithUneffortedMiddleActivity_ThenMiddleEffortsRemoved()
        {
            int resourceId1 = 1;

            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;

            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();

            var resource1 = new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.Direct, 1.0, 0, Enumerable.Empty<int>());

            var activity1 = new DependentActivity<int, int, int>(activityId1, 5);
            activity1.TargetResources.Add(resourceId1);
            var activity2 = new DependentActivity<int, int, int>(activityId2, 3) { HasNoEffort = true };
            activity2.TargetResources.Add(resourceId1);
            var activity3 = new DependentActivity<int, int, int>(activityId3, 12);
            activity3.TargetResources.Add(resourceId1);

            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int, int>>(new[] { resource1 }));

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(1);

            resourceSchedules[0].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            resourceSchedules[0].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            resourceSchedules[0].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, false, false, false, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(3);

            scheduledActivities0[0].Id.ShouldBe(activityId1);
            scheduledActivities0[0].HasNoCost.ShouldBeFalse();
            scheduledActivities0[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(5);

            scheduledActivities0[1].Id.ShouldBe(activityId2);
            scheduledActivities0[1].HasNoCost.ShouldBeFalse();
            scheduledActivities0[1].HasNoEffort.ShouldBeTrue();
            scheduledActivities0[1].StartTime.ShouldBe(5);
            scheduledActivities0[1].FinishTime.ShouldBe(8);

            scheduledActivities0[2].Id.ShouldBe(activityId3);
            scheduledActivities0[2].HasNoCost.ShouldBeFalse();
            scheduledActivities0[2].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[2].StartTime.ShouldBe(8);
            scheduledActivities0[2].FinishTime.ShouldBe(20);

            scheduledActivities0.Last().FinishTime.ShouldBe(20);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithThreeDirectResources_WithUncostedLastActivity_ThenLastCostsRemoved()
        {
            int resourceId1 = 1;

            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;

            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();

            var resource1 = new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.Direct, 1.0, 0, Enumerable.Empty<int>());

            var activity1 = new DependentActivity<int, int, int>(activityId1, 5);
            activity1.TargetResources.Add(resourceId1);
            var activity2 = new DependentActivity<int, int, int>(activityId2, 3);
            activity2.TargetResources.Add(resourceId1);
            var activity3 = new DependentActivity<int, int, int>(activityId3, 12) { HasNoCost = true };
            activity3.TargetResources.Add(resourceId1);

            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int, int>>(new[] { resource1 }));

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(1);

            resourceSchedules[0].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            resourceSchedules[0].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, false, false,
                    false, false, false, false, false, false, false, false, false, false,
                });

            resourceSchedules[0].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(3);

            scheduledActivities0[0].Id.ShouldBe(activityId1);
            scheduledActivities0[0].HasNoCost.ShouldBeFalse();
            scheduledActivities0[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(5);

            scheduledActivities0[1].Id.ShouldBe(activityId2);
            scheduledActivities0[1].HasNoCost.ShouldBeFalse();
            scheduledActivities0[1].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[1].StartTime.ShouldBe(5);
            scheduledActivities0[1].FinishTime.ShouldBe(8);

            scheduledActivities0[2].Id.ShouldBe(activityId3);
            scheduledActivities0[2].HasNoCost.ShouldBeTrue();
            scheduledActivities0[2].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[2].StartTime.ShouldBe(8);
            scheduledActivities0[2].FinishTime.ShouldBe(20);

            scheduledActivities0.Last().FinishTime.ShouldBe(20);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithThreeDirectResources_WithUneffortedLastActivity_ThenLastEffortsRemoved()
        {
            int resourceId1 = 1;

            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;

            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();

            var resource1 = new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.Direct, 1.0, 0, Enumerable.Empty<int>());

            var activity1 = new DependentActivity<int, int, int>(activityId1, 5);
            activity1.TargetResources.Add(resourceId1);
            var activity2 = new DependentActivity<int, int, int>(activityId2, 3);
            activity2.TargetResources.Add(resourceId1);
            var activity3 = new DependentActivity<int, int, int>(activityId3, 12) { HasNoEffort = true };
            activity3.TargetResources.Add(resourceId1);

            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int, int>>(new[] { resource1 }));

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(1);

            resourceSchedules[0].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            resourceSchedules[0].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            resourceSchedules[0].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, false, false,
                    false, false, false, false, false, false, false, false, false, false,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(3);

            scheduledActivities0[0].Id.ShouldBe(activityId1);
            scheduledActivities0[0].HasNoCost.ShouldBeFalse();
            scheduledActivities0[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(5);

            scheduledActivities0[1].Id.ShouldBe(activityId2);
            scheduledActivities0[1].HasNoCost.ShouldBeFalse();
            scheduledActivities0[1].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[1].StartTime.ShouldBe(5);
            scheduledActivities0[1].FinishTime.ShouldBe(8);

            scheduledActivities0[2].Id.ShouldBe(activityId3);
            scheduledActivities0[2].HasNoCost.ShouldBeFalse();
            scheduledActivities0[2].HasNoEffort.ShouldBeTrue();
            scheduledActivities0[2].StartTime.ShouldBe(8);
            scheduledActivities0[2].FinishTime.ShouldBe(20);

            scheduledActivities0.Last().FinishTime.ShouldBe(20);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithThreeIndirectResources_WithUncostedFirstActivity_ThenNoCostsRemoved()
        {
            int resourceId1 = 1;

            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;

            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();

            var resource1 = new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.Indirect, 1.0, 0, Enumerable.Empty<int>());

            var activity1 = new DependentActivity<int, int, int>(activityId1, 5) { HasNoCost = true };
            activity1.TargetResources.Add(resourceId1);
            var activity2 = new DependentActivity<int, int, int>(activityId2, 3);
            activity2.TargetResources.Add(resourceId1);
            var activity3 = new DependentActivity<int, int, int>(activityId3, 12);
            activity3.TargetResources.Add(resourceId1);

            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int, int>>(new[] { resource1 }));

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(1);

            resourceSchedules[0].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            resourceSchedules[0].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            resourceSchedules[0].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(3);

            scheduledActivities0[0].Id.ShouldBe(activityId1);
            scheduledActivities0[0].HasNoCost.ShouldBeTrue();
            scheduledActivities0[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(5);

            scheduledActivities0[1].Id.ShouldBe(activityId2);
            scheduledActivities0[1].HasNoCost.ShouldBeFalse();
            scheduledActivities0[1].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[1].StartTime.ShouldBe(5);
            scheduledActivities0[1].FinishTime.ShouldBe(8);

            scheduledActivities0[2].Id.ShouldBe(activityId3);
            scheduledActivities0[2].HasNoCost.ShouldBeFalse();
            scheduledActivities0[2].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[2].StartTime.ShouldBe(8);
            scheduledActivities0[2].FinishTime.ShouldBe(20);

            scheduledActivities0.Last().FinishTime.ShouldBe(20);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithThreeIndirectResources_WithUneffortedFirstActivity_ThenNoEffortsRemoved()
        {
            int resourceId1 = 1;

            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;

            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();

            var resource1 = new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.Indirect, 1.0, 0, Enumerable.Empty<int>());

            var activity1 = new DependentActivity<int, int, int>(activityId1, 5) { HasNoEffort = true };
            activity1.TargetResources.Add(resourceId1);
            var activity2 = new DependentActivity<int, int, int>(activityId2, 3);
            activity2.TargetResources.Add(resourceId1);
            var activity3 = new DependentActivity<int, int, int>(activityId3, 12);
            activity3.TargetResources.Add(resourceId1);

            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int, int>>(new[] { resource1 }));

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(1);

            resourceSchedules[0].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            resourceSchedules[0].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            resourceSchedules[0].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(3);

            scheduledActivities0[0].Id.ShouldBe(activityId1);
            scheduledActivities0[0].HasNoCost.ShouldBeFalse();
            scheduledActivities0[0].HasNoEffort.ShouldBeTrue();
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(5);

            scheduledActivities0[1].Id.ShouldBe(activityId2);
            scheduledActivities0[1].HasNoCost.ShouldBeFalse();
            scheduledActivities0[1].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[1].StartTime.ShouldBe(5);
            scheduledActivities0[1].FinishTime.ShouldBe(8);

            scheduledActivities0[2].Id.ShouldBe(activityId3);
            scheduledActivities0[2].HasNoCost.ShouldBeFalse();
            scheduledActivities0[2].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[2].StartTime.ShouldBe(8);
            scheduledActivities0[2].FinishTime.ShouldBe(20);

            scheduledActivities0.Last().FinishTime.ShouldBe(20);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithThreeIndirectResources_WithUncostedMiddleActivity_ThenNoCostsRemoved()
        {
            int resourceId1 = 1;

            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;

            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();

            var resource1 = new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.Indirect, 1.0, 0, Enumerable.Empty<int>());

            var activity1 = new DependentActivity<int, int, int>(activityId1, 5);
            activity1.TargetResources.Add(resourceId1);
            var activity2 = new DependentActivity<int, int, int>(activityId2, 3) { HasNoCost = true };
            activity2.TargetResources.Add(resourceId1);
            var activity3 = new DependentActivity<int, int, int>(activityId3, 12);
            activity3.TargetResources.Add(resourceId1);

            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int, int>>(new[] { resource1 }));

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(1);

            resourceSchedules[0].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            resourceSchedules[0].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            resourceSchedules[0].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(3);

            scheduledActivities0[0].Id.ShouldBe(activityId1);
            scheduledActivities0[0].HasNoCost.ShouldBeFalse();
            scheduledActivities0[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(5);

            scheduledActivities0[1].Id.ShouldBe(activityId2);
            scheduledActivities0[1].HasNoCost.ShouldBeTrue();
            scheduledActivities0[1].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[1].StartTime.ShouldBe(5);
            scheduledActivities0[1].FinishTime.ShouldBe(8);

            scheduledActivities0[2].Id.ShouldBe(activityId3);
            scheduledActivities0[2].HasNoCost.ShouldBeFalse();
            scheduledActivities0[2].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[2].StartTime.ShouldBe(8);
            scheduledActivities0[2].FinishTime.ShouldBe(20);

            scheduledActivities0.Last().FinishTime.ShouldBe(20);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithThreeIndirectResources_WithUneffortedMiddleActivity_ThenNoEffortsRemoved()
        {
            int resourceId1 = 1;

            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;

            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();

            var resource1 = new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.Indirect, 1.0, 0, Enumerable.Empty<int>());

            var activity1 = new DependentActivity<int, int, int>(activityId1, 5);
            activity1.TargetResources.Add(resourceId1);
            var activity2 = new DependentActivity<int, int, int>(activityId2, 3) { HasNoEffort = true };
            activity2.TargetResources.Add(resourceId1);
            var activity3 = new DependentActivity<int, int, int>(activityId3, 12);
            activity3.TargetResources.Add(resourceId1);

            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int, int>>(new[] { resource1 }));

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(1);

            resourceSchedules[0].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            resourceSchedules[0].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            resourceSchedules[0].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(3);

            scheduledActivities0[0].Id.ShouldBe(activityId1);
            scheduledActivities0[0].HasNoCost.ShouldBeFalse();
            scheduledActivities0[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(5);

            scheduledActivities0[1].Id.ShouldBe(activityId2);
            scheduledActivities0[1].HasNoCost.ShouldBeFalse();
            scheduledActivities0[1].HasNoEffort.ShouldBeTrue();
            scheduledActivities0[1].StartTime.ShouldBe(5);
            scheduledActivities0[1].FinishTime.ShouldBe(8);

            scheduledActivities0[2].Id.ShouldBe(activityId3);
            scheduledActivities0[2].HasNoCost.ShouldBeFalse();
            scheduledActivities0[2].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[2].StartTime.ShouldBe(8);
            scheduledActivities0[2].FinishTime.ShouldBe(20);

            scheduledActivities0.Last().FinishTime.ShouldBe(20);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithThreeIndirectResources_WithUncostedLastActivity_ThenNoCostsRemoved()
        {
            int resourceId1 = 1;

            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;

            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();

            var resource1 = new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.Indirect, 1.0, 0, Enumerable.Empty<int>());

            var activity1 = new DependentActivity<int, int, int>(activityId1, 5);
            activity1.TargetResources.Add(resourceId1);
            var activity2 = new DependentActivity<int, int, int>(activityId2, 3);
            activity2.TargetResources.Add(resourceId1);
            var activity3 = new DependentActivity<int, int, int>(activityId3, 12) { HasNoCost = true };
            activity3.TargetResources.Add(resourceId1);

            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int, int>>(new[] { resource1 }));

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(1);

            resourceSchedules[0].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            resourceSchedules[0].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            resourceSchedules[0].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(3);

            scheduledActivities0[0].Id.ShouldBe(activityId1);
            scheduledActivities0[0].HasNoCost.ShouldBeFalse();
            scheduledActivities0[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(5);

            scheduledActivities0[1].Id.ShouldBe(activityId2);
            scheduledActivities0[1].HasNoCost.ShouldBeFalse();
            scheduledActivities0[1].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[1].StartTime.ShouldBe(5);
            scheduledActivities0[1].FinishTime.ShouldBe(8);

            scheduledActivities0[2].Id.ShouldBe(activityId3);
            scheduledActivities0[2].HasNoCost.ShouldBeTrue();
            scheduledActivities0[2].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[2].StartTime.ShouldBe(8);
            scheduledActivities0[2].FinishTime.ShouldBe(20);

            scheduledActivities0.Last().FinishTime.ShouldBe(20);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithThreeIndirectResources_WithUneffortedLastActivity_ThenNoEffortsRemoved()
        {
            int resourceId1 = 1;

            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;

            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();

            var resource1 = new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.Indirect, 1.0, 0, Enumerable.Empty<int>());

            var activity1 = new DependentActivity<int, int, int>(activityId1, 5);
            activity1.TargetResources.Add(resourceId1);
            var activity2 = new DependentActivity<int, int, int>(activityId2, 3);
            activity2.TargetResources.Add(resourceId1);
            var activity3 = new DependentActivity<int, int, int>(activityId3, 12) { HasNoEffort = true };
            activity3.TargetResources.Add(resourceId1);

            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int, int>>(new[] { resource1 }));

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(1);

            resourceSchedules[0].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            resourceSchedules[0].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            resourceSchedules[0].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(3);

            scheduledActivities0[0].Id.ShouldBe(activityId1);
            scheduledActivities0[0].HasNoCost.ShouldBeFalse();
            scheduledActivities0[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(5);

            scheduledActivities0[1].Id.ShouldBe(activityId2);
            scheduledActivities0[1].HasNoCost.ShouldBeFalse();
            scheduledActivities0[1].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[1].StartTime.ShouldBe(5);
            scheduledActivities0[1].FinishTime.ShouldBe(8);

            scheduledActivities0[2].Id.ShouldBe(activityId3);
            scheduledActivities0[2].HasNoCost.ShouldBeFalse();
            scheduledActivities0[2].HasNoEffort.ShouldBeTrue();
            scheduledActivities0[2].StartTime.ShouldBe(8);
            scheduledActivities0[2].FinishTime.ShouldBe(20);

            scheduledActivities0.Last().FinishTime.ShouldBe(20);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithThreeNoneResources_WithUncostedFirstActivity_ThenFirstCostsRemoved()
        {
            int resourceId1 = 1;

            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;

            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();

            var resource1 = new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>());

            var activity1 = new DependentActivity<int, int, int>(activityId1, 5) { HasNoCost = true };
            activity1.TargetResources.Add(resourceId1);
            var activity2 = new DependentActivity<int, int, int>(activityId2, 3);
            activity2.TargetResources.Add(resourceId1);
            var activity3 = new DependentActivity<int, int, int>(activityId3, 12);
            activity3.TargetResources.Add(resourceId1);

            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int, int>>(new[] { resource1 }));

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(1);

            resourceSchedules[0].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            resourceSchedules[0].CostAllocation.ShouldBe(
                new bool[] {
                    false, false, false, false, false, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            resourceSchedules[0].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(3);

            scheduledActivities0[0].Id.ShouldBe(activityId1);
            scheduledActivities0[0].HasNoCost.ShouldBeTrue();
            scheduledActivities0[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(5);

            scheduledActivities0[1].Id.ShouldBe(activityId2);
            scheduledActivities0[1].HasNoCost.ShouldBeFalse();
            scheduledActivities0[1].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[1].StartTime.ShouldBe(5);
            scheduledActivities0[1].FinishTime.ShouldBe(8);

            scheduledActivities0[2].Id.ShouldBe(activityId3);
            scheduledActivities0[2].HasNoCost.ShouldBeFalse();
            scheduledActivities0[2].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[2].StartTime.ShouldBe(8);
            scheduledActivities0[2].FinishTime.ShouldBe(20);

            scheduledActivities0.Last().FinishTime.ShouldBe(20);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithThreeNoneResources_WithUneffortedFirstActivity_ThenFirstEffortsRemoved()
        {
            int resourceId1 = 1;

            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;

            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();

            var resource1 = new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>());

            var activity1 = new DependentActivity<int, int, int>(activityId1, 5) { HasNoEffort = true };
            activity1.TargetResources.Add(resourceId1);
            var activity2 = new DependentActivity<int, int, int>(activityId2, 3);
            activity2.TargetResources.Add(resourceId1);
            var activity3 = new DependentActivity<int, int, int>(activityId3, 12);
            activity3.TargetResources.Add(resourceId1);

            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int, int>>(new[] { resource1 }));

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(1);

            resourceSchedules[0].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            resourceSchedules[0].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            resourceSchedules[0].EffortAllocation.ShouldBe(
                new bool[] {
                    false, false, false, false, false, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(3);

            scheduledActivities0[0].Id.ShouldBe(activityId1);
            scheduledActivities0[0].HasNoCost.ShouldBeFalse();
            scheduledActivities0[0].HasNoEffort.ShouldBeTrue();
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(5);

            scheduledActivities0[1].Id.ShouldBe(activityId2);
            scheduledActivities0[1].HasNoCost.ShouldBeFalse();
            scheduledActivities0[1].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[1].StartTime.ShouldBe(5);
            scheduledActivities0[1].FinishTime.ShouldBe(8);

            scheduledActivities0[2].Id.ShouldBe(activityId3);
            scheduledActivities0[2].HasNoCost.ShouldBeFalse();
            scheduledActivities0[2].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[2].StartTime.ShouldBe(8);
            scheduledActivities0[2].FinishTime.ShouldBe(20);

            scheduledActivities0.Last().FinishTime.ShouldBe(20);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithThreeNoneResources_WithUncostedMiddleActivity_ThenMiddleCostsRemoved()
        {
            int resourceId1 = 1;

            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;

            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();

            var resource1 = new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>());

            var activity1 = new DependentActivity<int, int, int>(activityId1, 5);
            activity1.TargetResources.Add(resourceId1);
            var activity2 = new DependentActivity<int, int, int>(activityId2, 3) { HasNoCost = true };
            activity2.TargetResources.Add(resourceId1);
            var activity3 = new DependentActivity<int, int, int>(activityId3, 12);
            activity3.TargetResources.Add(resourceId1);

            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int, int>>(new[] { resource1 }));

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(1);

            resourceSchedules[0].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            resourceSchedules[0].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, false, false, false, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            resourceSchedules[0].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(3);

            scheduledActivities0[0].Id.ShouldBe(activityId1);
            scheduledActivities0[0].HasNoCost.ShouldBeFalse();
            scheduledActivities0[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(5);

            scheduledActivities0[1].Id.ShouldBe(activityId2);
            scheduledActivities0[1].HasNoCost.ShouldBeTrue();
            scheduledActivities0[1].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[1].StartTime.ShouldBe(5);
            scheduledActivities0[1].FinishTime.ShouldBe(8);

            scheduledActivities0[2].Id.ShouldBe(activityId3);
            scheduledActivities0[2].HasNoCost.ShouldBeFalse();
            scheduledActivities0[2].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[2].StartTime.ShouldBe(8);
            scheduledActivities0[2].FinishTime.ShouldBe(20);

            scheduledActivities0.Last().FinishTime.ShouldBe(20);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithThreeNoneResources_WithUneffortedMiddleActivity_ThenMiddleEffortsRemoved()
        {
            int resourceId1 = 1;

            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;

            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();

            var resource1 = new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>());

            var activity1 = new DependentActivity<int, int, int>(activityId1, 5);
            activity1.TargetResources.Add(resourceId1);
            var activity2 = new DependentActivity<int, int, int>(activityId2, 3) { HasNoEffort = true };
            activity2.TargetResources.Add(resourceId1);
            var activity3 = new DependentActivity<int, int, int>(activityId3, 12);
            activity3.TargetResources.Add(resourceId1);

            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int, int>>(new[] { resource1 }));

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(1);

            resourceSchedules[0].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            resourceSchedules[0].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            resourceSchedules[0].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, false, false, false, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(3);

            scheduledActivities0[0].Id.ShouldBe(activityId1);
            scheduledActivities0[0].HasNoCost.ShouldBeFalse();
            scheduledActivities0[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(5);

            scheduledActivities0[1].Id.ShouldBe(activityId2);
            scheduledActivities0[1].HasNoCost.ShouldBeFalse();
            scheduledActivities0[1].HasNoEffort.ShouldBeTrue();
            scheduledActivities0[1].StartTime.ShouldBe(5);
            scheduledActivities0[1].FinishTime.ShouldBe(8);

            scheduledActivities0[2].Id.ShouldBe(activityId3);
            scheduledActivities0[2].HasNoCost.ShouldBeFalse();
            scheduledActivities0[2].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[2].StartTime.ShouldBe(8);
            scheduledActivities0[2].FinishTime.ShouldBe(20);

            scheduledActivities0.Last().FinishTime.ShouldBe(20);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithThreeNoneResources_WithUncostedLastActivity_ThenLastCostsRemoved()
        {
            int resourceId1 = 1;

            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;

            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();

            var resource1 = new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>());

            var activity1 = new DependentActivity<int, int, int>(activityId1, 5);
            activity1.TargetResources.Add(resourceId1);
            var activity2 = new DependentActivity<int, int, int>(activityId2, 3);
            activity2.TargetResources.Add(resourceId1);
            var activity3 = new DependentActivity<int, int, int>(activityId3, 12) { HasNoCost = true };
            activity3.TargetResources.Add(resourceId1);

            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int, int>>(new[] { resource1 }));

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(1);

            resourceSchedules[0].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            resourceSchedules[0].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, false, false,
                    false, false, false, false, false, false, false, false, false, false,
                });

            resourceSchedules[0].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(3);

            scheduledActivities0[0].Id.ShouldBe(activityId1);
            scheduledActivities0[0].HasNoCost.ShouldBeFalse();
            scheduledActivities0[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(5);

            scheduledActivities0[1].Id.ShouldBe(activityId2);
            scheduledActivities0[1].HasNoCost.ShouldBeFalse();
            scheduledActivities0[1].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[1].StartTime.ShouldBe(5);
            scheduledActivities0[1].FinishTime.ShouldBe(8);

            scheduledActivities0[2].Id.ShouldBe(activityId3);
            scheduledActivities0[2].HasNoCost.ShouldBeTrue();
            scheduledActivities0[2].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[2].StartTime.ShouldBe(8);
            scheduledActivities0[2].FinishTime.ShouldBe(20);

            scheduledActivities0.Last().FinishTime.ShouldBe(20);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithThreeNoneResources_WithUneffortedLastActivity_ThenLastEffortsRemoved()
        {
            int resourceId1 = 1;

            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;

            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();

            var resource1 = new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>());

            var activity1 = new DependentActivity<int, int, int>(activityId1, 5);
            activity1.TargetResources.Add(resourceId1);
            var activity2 = new DependentActivity<int, int, int>(activityId2, 3);
            activity2.TargetResources.Add(resourceId1);
            var activity3 = new DependentActivity<int, int, int>(activityId3, 12) { HasNoEffort = true };
            activity3.TargetResources.Add(resourceId1);

            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int, int>>(new[] { resource1 }));

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(1);

            resourceSchedules[0].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            resourceSchedules[0].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            resourceSchedules[0].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, false, false,
                    false, false, false, false, false, false, false, false, false, false,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(3);

            scheduledActivities0[0].Id.ShouldBe(activityId1);
            scheduledActivities0[0].HasNoCost.ShouldBeFalse();
            scheduledActivities0[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(5);

            scheduledActivities0[1].Id.ShouldBe(activityId2);
            scheduledActivities0[1].HasNoCost.ShouldBeFalse();
            scheduledActivities0[1].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[1].StartTime.ShouldBe(5);
            scheduledActivities0[1].FinishTime.ShouldBe(8);

            scheduledActivities0[2].Id.ShouldBe(activityId3);
            scheduledActivities0[2].HasNoCost.ShouldBeFalse();
            scheduledActivities0[2].HasNoEffort.ShouldBeTrue();
            scheduledActivities0[2].StartTime.ShouldBe(8);
            scheduledActivities0[2].FinishTime.ShouldBe(20);

            scheduledActivities0.Last().FinishTime.ShouldBe(20);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCyclomaticComplexityWithNoNodes_ThenFindsZero()
        {
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            graphCompiler.Compile();
            graphCompiler.CyclomaticComplexity.ShouldBe(0);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCyclomaticComplexityInOneNetwork_ThenAsExpected()
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
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId1, 6));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId2, 7));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId3, 8));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId4, 11, new HashSet<int> { activityId2 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId5, 8, new HashSet<int> { activityId1, activityId2, activityId3 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId6, 7, new HashSet<int> { activityId3 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId7, 4, new HashSet<int> { activityId4 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId8, 4, new HashSet<int> { activityId4, activityId6 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId9, 10, new HashSet<int> { activityId5 }));

            graphCompiler.Compile();

            graphCompiler.CyclomaticComplexity.ShouldBe(6);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCyclomaticComplexityInThreeNetworks_ThenAsExpected()
        {
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int activityId6 = activityId5 + 1;
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId1, 6));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId2, 7));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId3, 8));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId4, 11, new HashSet<int> { activityId1 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId5, 8, new HashSet<int> { activityId2 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId6, 7, new HashSet<int> { activityId3 }));

            graphCompiler.Compile();

            graphCompiler.CyclomaticComplexity.ShouldBe(3);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCyclomaticComplexityWithTwoLoneNodes_ThenAsExpected()
        {
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId1, 6));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId2, 7));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId3, 8));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId4, 11, new HashSet<int> { activityId1 }));

            graphCompiler.Compile();

            graphCompiler.CyclomaticComplexity.ShouldBe(3);
        }

        [Fact]
        public void VertexGraphCompiler_GivenBasicVanillaTest_ThenAsExpected()
        {
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            var graphBuilder = graphCompiler.Builder;
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId1, 5));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId2, 10));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId3, 15, new HashSet<int> { activityId1 }) { HasNoEffort = true });
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId4, 10, new HashSet<int> { activityId3 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId5, 10) { HasNoEffort = true, HasNoCost = true });

            int resourceId1 = 1;
            int resourceId2 = resourceId1 + 1;
            int resourceId3 = resourceId2 + 1;
            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int, int>>(new[]
                {
                    new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>()),
                    new Resource<int, int>(resourceId2, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>()),
                    new Resource<int, int>(resourceId3, string.Empty, false, false, InterActivityAllocationType.Indirect, 1.0, 0, Enumerable.Empty<int>()),
                }));

            compilation.CompilationErrors.ShouldBeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.ShouldBe(3);

            resourceSchedules[0].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            resourceSchedules[0].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            resourceSchedules[0].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, false, false, false, false, false,
                    false, false, false, false, false, false, false, false, false, false,
                    true, true, true, true, true, true, true, true, true, true,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(3);

            scheduledActivities0[0].Id.ShouldBe(activityId1);
            scheduledActivities0[0].HasNoCost.ShouldBeFalse();
            scheduledActivities0[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(5);

            scheduledActivities0[1].Id.ShouldBe(activityId3);
            scheduledActivities0[1].HasNoCost.ShouldBeFalse();
            scheduledActivities0[1].HasNoEffort.ShouldBeTrue();
            scheduledActivities0[1].StartTime.ShouldBe(5);
            scheduledActivities0[1].FinishTime.ShouldBe(20);

            scheduledActivities0[2].Id.ShouldBe(activityId4);
            scheduledActivities0[2].HasNoCost.ShouldBeFalse();
            scheduledActivities0[2].HasNoEffort.ShouldBeFalse();
            scheduledActivities0[2].StartTime.ShouldBe(20);
            scheduledActivities0[2].FinishTime.ShouldBe(30);

            scheduledActivities0.Last().FinishTime.ShouldBe(30);

            resourceSchedules[1].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    false, false, false, false, false, false, false, false, false, false,
                    false, false, false, false, false, false, false, false, false, false,
                });

            resourceSchedules[1].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    false, false, false, false, false, false, false, false, false, false,
                    false, false, false, false, false, false, false, false, false, false,
                });

            resourceSchedules[1].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    false, false, false, false, false, false, false, false, false, false,
                    false, false, false, false, false, false, false, false, false, false,
                });

            var resourceSchedule1 = resourceSchedules[1];
            resourceSchedule1.Resource.Id.ShouldBe(resourceId2);
            var scheduledActivities1 = resourceSchedule1.ScheduledActivities.ToList();
            scheduledActivities1.Count.ShouldBe(1);

            scheduledActivities1[0].Id.ShouldBe(activityId2);
            scheduledActivities1[0].HasNoCost.ShouldBeFalse();
            scheduledActivities1[0].HasNoEffort.ShouldBeFalse();
            scheduledActivities1[0].StartTime.ShouldBe(0);
            scheduledActivities1[0].FinishTime.ShouldBe(10);

            scheduledActivities1.Last().FinishTime.ShouldBe(10);

            resourceSchedules[2].ActivityAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            resourceSchedules[2].CostAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            resourceSchedules[2].EffortAllocation.ShouldBe(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            var resourceSchedule2 = resourceSchedules[2];
            resourceSchedule2.Resource.Id.ShouldBe(resourceId3);
            var scheduledActivities2 = resourceSchedule2.ScheduledActivities.ToList();
            scheduledActivities2.Count.ShouldBe(1);

            scheduledActivities2[0].Id.ShouldBe(activityId5);
            scheduledActivities2[0].HasNoCost.ShouldBeTrue();
            scheduledActivities2[0].HasNoEffort.ShouldBeTrue();
            scheduledActivities2[0].StartTime.ShouldBe(0);
            scheduledActivities2[0].FinishTime.ShouldBe(10);

            scheduledActivities2.Last().FinishTime.ShouldBe(10);



            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(5);
            graphBuilder.Activity(activityId1).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId1).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(5);
            graphBuilder.Activity(activityId1).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId1 }), ignoreOrder: true);

            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(10);
            graphBuilder.Activity(activityId2).FreeSlack.ShouldBe(20);
            graphBuilder.Activity(activityId2).TotalSlack.ShouldBe(20);
            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(20);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(30);
            graphBuilder.Activity(activityId2).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId2 }), ignoreOrder: true);

            graphBuilder.Activity(activityId3).EarliestStartTime.ShouldBe(5);
            graphBuilder.Activity(activityId3).EarliestFinishTime.ShouldBe(20);
            graphBuilder.Activity(activityId3).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestStartTime.ShouldBe(5);
            graphBuilder.Activity(activityId3).LatestFinishTime.ShouldBe(20);
            graphBuilder.Activity(activityId3).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId3).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId1 }), ignoreOrder: true);

            graphBuilder.Activity(activityId4).EarliestStartTime.ShouldBe(20);
            graphBuilder.Activity(activityId4).EarliestFinishTime.ShouldBe(30);
            graphBuilder.Activity(activityId4).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).LatestStartTime.ShouldBe(20);
            graphBuilder.Activity(activityId4).LatestFinishTime.ShouldBe(30);
            graphBuilder.Activity(activityId4).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId3 }), ignoreOrder: true);
            graphBuilder.Activity(activityId4).AllocatedToResources.ShouldBe(new List<int>(new int[] { resourceId1 }), ignoreOrder: true);
        }

        [Fact]
        public void VertexGraphCompiler_GivenTransitiveReduction_WhenRedundantDependencies_ThenRedundantDependenciesRemoved()
        {
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            var graphBuilder = graphCompiler.Builder;
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId1, 1));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId2, 2, new HashSet<int> { activityId1 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId3, 2, new HashSet<int> { activityId1, activityId2 }));

            graphCompiler.TransitiveReduction();
            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile();

            compilation.CompilationErrors.ShouldBeEmpty();

            graphBuilder.Activity(activityId1).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId1).Successors.ShouldBe(new List<int>(new int[] { activityId2 }), ignoreOrder: true);

            graphBuilder.Activity(activityId2).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId2).Dependencies.ShouldBe(new List<int>(new int[] { activityId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId2).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId2).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId2).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId2).Successors.ShouldBe(new List<int>(new int[] { activityId3 }), ignoreOrder: true);

            graphBuilder.Activity(activityId3).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId3).Dependencies.ShouldBe(new List<int>(new int[] { activityId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId3).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId3).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId3).Successors.Count.ShouldBe(0);
        }

        [Fact]
        public void VertexGraphCompiler_GivenTransitiveReduction_WhenRedundantPlanningDependencies_ThenRedundantPlanningDependenciesRemoved()
        {
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            var graphBuilder = graphCompiler.Builder;
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId1, 1));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId2, 2, new HashSet<int>(), new HashSet<int> { activityId1 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId3, 2, new HashSet<int>(), new HashSet<int> { activityId1, activityId2 }));

            graphCompiler.TransitiveReduction();
            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile();

            compilation.CompilationErrors.ShouldBeEmpty();

            graphBuilder.Activity(activityId1).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId1).Successors.ShouldBe(new List<int>(new int[] { activityId2 }), ignoreOrder: true);

            graphBuilder.Activity(activityId2).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId2).PlanningDependencies.ShouldBe(new List<int>(new int[] { activityId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId2).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId2).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId2).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId2).Successors.ShouldBe(new List<int>(new int[] { activityId3 }), ignoreOrder: true);

            graphBuilder.Activity(activityId3).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId3).PlanningDependencies.ShouldBe(new List<int>(new int[] { activityId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId3).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId3).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId3).Successors.Count.ShouldBe(0);
        }

        [Fact]
        public void VertexGraphCompiler_GivenTransitiveReduction_WhenDependenciesAreRedundantAcrossPlanningDependencies_ThenDependenciesRemoved()
        {
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            var graphBuilder = graphCompiler.Builder;
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId1, 1));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId2, 2, new HashSet<int>(), new HashSet<int> { activityId1 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId3, 2, new HashSet<int> { activityId1, activityId2 }));

            graphCompiler.TransitiveReduction();
            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile();

            compilation.CompilationErrors.ShouldBeEmpty();

            graphBuilder.Activity(activityId1).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId1).Successors.ShouldBe(new List<int>(new int[] { activityId2 }), ignoreOrder: true);

            graphBuilder.Activity(activityId2).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId2).PlanningDependencies.ShouldBe(new List<int>(new int[] { activityId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId2).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId2).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId2).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId2).Successors.ShouldBe(new List<int>(new int[] { activityId3 }), ignoreOrder: true);

            graphBuilder.Activity(activityId3).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId3).Dependencies.ShouldBe(new List<int>(new int[] { activityId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId3).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId3).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId3).Successors.Count.ShouldBe(0);
        }

        [Fact]
        public void VertexGraphCompiler_GivenTransitiveReduction_WhenPlanningDependenciesAreRedundantAcrossDependencies_ThenPlanningDependenciesRemoved()
        {
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            var graphBuilder = graphCompiler.Builder;
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId1, 1));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId2, 2, new HashSet<int> { activityId1 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId3, 2, new HashSet<int>(), new HashSet<int> { activityId1, activityId2 }));

            graphCompiler.TransitiveReduction();
            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile();

            compilation.CompilationErrors.ShouldBeEmpty();

            graphBuilder.Activity(activityId1).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId1).Successors.ShouldBe(new List<int>(new int[] { activityId2 }), ignoreOrder: true);

            graphBuilder.Activity(activityId2).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId2).Dependencies.ShouldBe(new List<int>(new int[] { activityId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId2).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId2).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId2).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId2).Successors.ShouldBe(new List<int>(new int[] { activityId3 }), ignoreOrder: true);

            graphBuilder.Activity(activityId3).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId3).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId3).PlanningDependencies.ShouldBe(new List<int>(new int[] { activityId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId3).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId3).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId3).Successors.Count.ShouldBe(0);
        }

        [Fact]
        public void VertexGraphCompiler_GivenTransitiveReduction_WhenDependenciesAndPlanningDependenciesAreRedundantAcrossPlanningDependencies_ThenBothDependenciesRemoved()
        {
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            var graphBuilder = graphCompiler.Builder;
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId1, 1));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId2, 2, new HashSet<int>(), new HashSet<int> { activityId1 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId3, 2, new HashSet<int> { activityId1, activityId2 }, new HashSet<int> { activityId1, activityId2 }));

            graphCompiler.TransitiveReduction();
            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile();

            compilation.CompilationErrors.ShouldBeEmpty();

            graphBuilder.Activity(activityId1).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId1).Successors.ShouldBe(new List<int>(new int[] { activityId2 }), ignoreOrder: true);

            graphBuilder.Activity(activityId2).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId2).PlanningDependencies.ShouldBe(new List<int>(new int[] { activityId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId2).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId2).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId2).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId2).Successors.ShouldBe(new List<int>(new int[] { activityId3 }), ignoreOrder: true);

            graphBuilder.Activity(activityId3).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId3).Dependencies.ShouldBe(new List<int>(new int[] { activityId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId3).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId3).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId3).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId3).Successors.Count.ShouldBe(0);
        }

        [Fact]
        public void VertexGraphCompiler_GivenTransitiveReduction_WhenDependenciesAndPlanningDependenciesAreRedundantAcrossDependencies_ThenBothDependenciesRemoved()
        {
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            var graphBuilder = graphCompiler.Builder;
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId1, 1));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId2, 2, new HashSet<int> { activityId1 }));
            graphCompiler.AddActivity(new DependentActivity<int, int, int>(activityId3, 2, new HashSet<int> { activityId1, activityId2 }, new HashSet<int> { activityId1, activityId2 }));

            graphCompiler.TransitiveReduction();
            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = graphCompiler.Compile();

            compilation.CompilationErrors.ShouldBeEmpty();

            graphBuilder.Activity(activityId1).Dependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).ResourceDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId1).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId1).Successors.ShouldBe(new List<int>(new int[] { activityId2 }), ignoreOrder: true);

            graphBuilder.Activity(activityId2).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId2).Dependencies.ShouldBe(new List<int>(new int[] { activityId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId2).PlanningDependencies.Count.ShouldBe(0);
            graphBuilder.Activity(activityId2).ResourceDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId2).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId1 }), ignoreOrder: true);
            graphBuilder.Activity(activityId2).Successors.Count.ShouldBe(1);
            graphBuilder.Activity(activityId2).Successors.ShouldBe(new List<int>(new int[] { activityId3 }), ignoreOrder: true);

            graphBuilder.Activity(activityId3).Dependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId3).Dependencies.ShouldBe(new List<int>(new int[] { activityId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId3).PlanningDependencies.Count.ShouldBe(1);
            graphBuilder.Activity(activityId3).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId3).ResourceDependencies.ShouldBe(new List<int>(new int[] { activityId2 }), ignoreOrder: true);
            graphBuilder.Activity(activityId3).Successors.Count.ShouldBe(0);
        }
    }
}
