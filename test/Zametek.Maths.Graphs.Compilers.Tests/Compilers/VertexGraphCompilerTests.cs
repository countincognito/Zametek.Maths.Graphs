using FluentAssertions;
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
            var graphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();
            var graphBuilder = graphCompiler.Builder;
            graphBuilder.EdgeIds.Any().Should().BeFalse();
            graphBuilder.NodeIds.Any().Should().BeFalse();
            graphBuilder.AllDependenciesSatisfied.Should().BeTrue();
            graphBuilder.StartNodes.Any().Should().BeFalse();
            graphBuilder.EndNodes.Any().Should().BeFalse();
        }

        [Fact]
        public void VertexGraphCompiler_GivenSingleActivityNoDependencies_ThenNoStartOrEndNodes()
        {
            int activityId = 0;
            int activityId1 = activityId + 1;
            var graphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();
            var graphBuilder = graphCompiler.Builder;

            var activity = new DependentActivity<int, int>(activityId1, 0);
            bool result = graphCompiler.AddActivity(activity);
            result.Should().BeTrue();

            graphBuilder.EdgeIds.Any().Should().BeFalse();
            graphBuilder.NodeIds.Count().Should().Be(1);
            graphBuilder.AllDependenciesSatisfied.Should().BeTrue();

            graphBuilder.StartNodes.Any().Should().BeFalse();
            graphBuilder.EndNodes.Any().Should().BeFalse();

            graphBuilder.Node(activityId1).Id.Should().Be(activityId1);
            graphBuilder.Node(activityId1).NodeType.Should().Be(NodeType.Isolated);
            graphBuilder.Activity(activityId1).Id.Should().Be(activityId1);
            graphBuilder.Activities.Count().Should().Be(1);
            graphBuilder.Edges.Any().Should().BeFalse();
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithInvalidConstraints_ThenFindsInvalidConstraints()
        {
            var graphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();
            graphCompiler.AddActivity(new DependentActivity<int, int>(1, 6));
            graphCompiler.AddActivity(new DependentActivity<int, int>(2, 7));
            graphCompiler.AddActivity(new DependentActivity<int, int>(3, 8));
            graphCompiler.AddActivity(new DependentActivity<int, int>(4, 11, new HashSet<int> { 2 }) { MinimumEarliestStartTime = 7, MaximumLatestFinishTime = 17 });
            graphCompiler.AddActivity(new DependentActivity<int, int>(5, 8, new HashSet<int> { 1, 2, 3 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(6, 7, new HashSet<int> { 3 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(7, 4, new HashSet<int> { 4 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(8, 4, new HashSet<int> { 4, 6 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(9, 10, new HashSet<int> { 5 }) { MinimumFreeSlack = 2, MaximumLatestFinishTime = 8 });

            IGraphCompilation<int, int, IDependentActivity<int, int>> compilation = graphCompiler.Compile();

            compilation.ResourceSchedules.Should().BeEmpty();
            compilation.CompilationErrors.Should().NotBeEmpty();

            var compilationErrors = compilation.CompilationErrors.ToList();

            compilationErrors.Count().Should().Be(1);
            compilationErrors[0].ErrorCode.Should().Be(GraphCompilationErrorCode.P0030);
            compilationErrors[0].ErrorMessage.Should().Be(
                $@"{Properties.Resources.Message_InvalidConstraints}
4 -> {Properties.Resources.Message_MinimumEarliestStartTimePlusDurationMustBeGreaterThanMaximumLatestFinishTime}
9 -> {Properties.Resources.Message_CannotSetMinimumFreeSlackAndMaximumLatestFinishTime}
");
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithCircularDependencies_ThenFindsCircularDependencies()
        {
            var graphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();
            graphCompiler.AddActivity(new DependentActivity<int, int>(1, 10));
            graphCompiler.AddActivity(new DependentActivity<int, int>(2, 10, new HashSet<int> { 7 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(3, 10));
            graphCompiler.AddActivity(new DependentActivity<int, int>(4, 10, new HashSet<int> { 2 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(5, 10, new HashSet<int> { 1, 2, 3, 8 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(6, 10, new HashSet<int> { 3 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(7, 10, new HashSet<int> { 4 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(8, 10, new HashSet<int> { 9, 6 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(9, 10, new HashSet<int> { 5 }));

            IGraphCompilation<int, int, IDependentActivity<int, int>> compilation = graphCompiler.Compile();

            compilation.ResourceSchedules.Should().BeEmpty();
            compilation.CompilationErrors.Should().NotBeEmpty();

            var compilationErrors = compilation.CompilationErrors.ToList();

            compilationErrors.Count().Should().Be(1);
            compilationErrors[0].ErrorCode.Should().Be(GraphCompilationErrorCode.P0020);
            compilationErrors[0].ErrorMessage.Should().Be(
                $@"{Properties.Resources.Message_CircularDependencies}
4 -> 7 -> 2
9 -> 8 -> 5
");
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithMissingDependencies_ThenFindsMissingDependencies()
        {
            var graphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();
            graphCompiler.AddActivity(new DependentActivity<int, int>(1, 10));
            graphCompiler.AddActivity(new DependentActivity<int, int>(2, 10, new HashSet<int> { 7 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(3, 10, new HashSet<int> { 21 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(4, 10, new HashSet<int> { 2 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(5, 10, new HashSet<int> { 1, 2, 3, 8 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(6, 10, new HashSet<int> { 3 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(7, 10, new HashSet<int> { 22 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(8, 10, new HashSet<int> { 9, 6 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(9, 10));

            IGraphCompilation<int, int, IDependentActivity<int, int>> compilation = graphCompiler.Compile();

            compilation.ResourceSchedules.Should().BeEmpty();
            compilation.CompilationErrors.Should().NotBeEmpty();

            var compilationErrors = compilation.CompilationErrors.ToList();
            compilationErrors.Count().Should().Be(1);
            compilationErrors[0].ErrorCode.Should().Be(GraphCompilationErrorCode.P0010);
            compilationErrors[0].ErrorMessage.Should().Be(
                $@"{Properties.Resources.Message_MissingDependencies}
21 {Properties.Resources.Message_IsMissingFrom} 3
22 {Properties.Resources.Message_IsMissingFrom} 7
");
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithInvalidConstraintsAndCircularAndMissingDependencies_ThenFindsInvalidConstraintsAndCircularAndMissingDependencies()
        {
            var graphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();
            graphCompiler.AddActivity(new DependentActivity<int, int>(1, 10));
            graphCompiler.AddActivity(new DependentActivity<int, int>(2, 10, new HashSet<int> { 7 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(3, 10, new HashSet<int> { 21 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(4, 10, new HashSet<int> { 2 }) { MinimumEarliestStartTime = 7, MaximumLatestFinishTime = 16 });
            graphCompiler.AddActivity(new DependentActivity<int, int>(5, 10, new HashSet<int> { 1, 2, 3, 8 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(6, 10, new HashSet<int> { 3 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(7, 10, new HashSet<int> { 4, 22 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(8, 10, new HashSet<int> { 9, 6 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(9, 10, new HashSet<int> { 5 }));

            IGraphCompilation<int, int, IDependentActivity<int, int>> compilation = graphCompiler.Compile();

            compilation.ResourceSchedules.Should().BeEmpty();
            compilation.CompilationErrors.Should().NotBeEmpty();

            var compilationErrors = compilation.CompilationErrors.ToList();

            compilationErrors.Count().Should().Be(3);

            compilationErrors[0].ErrorCode.Should().Be(GraphCompilationErrorCode.P0010);
            compilationErrors[0].ErrorMessage.Should().Be(
                $@"{Properties.Resources.Message_MissingDependencies}
21 {Properties.Resources.Message_IsMissingFrom} 3
22 {Properties.Resources.Message_IsMissingFrom} 7
");

            compilationErrors[1].ErrorCode.Should().Be(GraphCompilationErrorCode.P0020);
            compilationErrors[1].ErrorMessage.Should().Be(
                $@"{Properties.Resources.Message_CircularDependencies}
4 -> 7 -> 2
9 -> 8 -> 5
");

            compilationErrors[2].ErrorCode.Should().Be(GraphCompilationErrorCode.P0030);
            compilationErrors[2].ErrorMessage.Should().Be(
                $@"{Properties.Resources.Message_InvalidConstraints}
4 -> {Properties.Resources.Message_MinimumEarliestStartTimePlusDurationMustBeGreaterThanMaximumLatestFinishTime}
");
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithPostCompilationInvalidConstraints_ThenFindsInvalidConstraints()
        {
            var graphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();
            graphCompiler.AddActivity(new DependentActivity<int, int>(1, 6));
            graphCompiler.AddActivity(new DependentActivity<int, int>(2, 7));
            graphCompiler.AddActivity(new DependentActivity<int, int>(3, 8));
            graphCompiler.AddActivity(new DependentActivity<int, int>(4, 11, new HashSet<int> { 2 }) { MaximumLatestFinishTime = 5 });
            graphCompiler.AddActivity(new DependentActivity<int, int>(5, 8, new HashSet<int> { 1, 2, 3 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(6, 7, new HashSet<int> { 3 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(7, 4, new HashSet<int> { 4 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(8, 4, new HashSet<int> { 4, 6 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(9, 10, new HashSet<int> { 5 }));

            IGraphCompilation<int, int, IDependentActivity<int, int>> compilation = graphCompiler.Compile();

            compilation.ResourceSchedules.Should().BeEmpty();
            compilation.CompilationErrors.Should().NotBeEmpty();

            var compilationErrors = compilation.CompilationErrors.ToList();

            compilationErrors.Count().Should().Be(1);
            compilationErrors[0].ErrorCode.Should().Be(GraphCompilationErrorCode.C0010);
            compilationErrors[0].ErrorMessage.Should().Be(
                $@"{Properties.Resources.Message_InvalidConstraints}
2 -> {Properties.Resources.Message_LatestStartTimeLessThanZero}
2 -> {Properties.Resources.Message_LatestFinishTimeLessThanZero}
2 -> {Properties.Resources.Message_LatestStartTimeLessThanEarliestStartTime}
2 -> {Properties.Resources.Message_LatestFinishTimeLessThanEarliestFinishTime}
4 -> {Properties.Resources.Message_EarliestStartTimeLessThanZero}
4 -> {Properties.Resources.Message_LatestStartTimeLessThanZero}
");
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
            var graphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();

            var activity1 = new DependentActivity<int, int>(activityId1, 6);
            var activity2 = new DependentActivity<int, int>(activityId2, 7);
            var activity3 = new DependentActivity<int, int>(activityId3, 4);
            var activity4 = new DependentActivity<int, int>(activityId4, 8);
            var activity5 = new DependentActivity<int, int>(activityId5, 3);
            var activity6 = new DependentActivity<int, int>(activityId6, 2);
            var activity7 = new DependentActivity<int, int>(activityId7, 1);
            var activity8 = new DependentActivity<int, int>(activityId8, 6);
            var activity9 = new DependentActivity<int, int>(activityId9, 12);
            var activity10 = new DependentActivity<int, int>(activityId10, 11);
            var activity11 = new DependentActivity<int, int>(activityId11, 9);
            var activity12 = new DependentActivity<int, int>(activityId12, 3);
            var activity13 = new DependentActivity<int, int>(activityId13, 13);
            var activity14 = new DependentActivity<int, int>(activityId14, 8);

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

            IGraphCompilation<int, int, IDependentActivity<int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int>>(new[]
                {
                    new Resource<int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0),
                    new Resource<int>(resourceId2, string.Empty, false, true, InterActivityAllocationType.None, 1.0, 0),
                    new Resource<int>(resourceId3, string.Empty, false, true, InterActivityAllocationType.None, 1.0, 0),
                }));

            compilation.ResourceSchedules.Should().BeEmpty();
            compilation.CompilationErrors.Should().NotBeEmpty();

            var compilationErrors = compilation.CompilationErrors.ToList();

            compilationErrors.Count().Should().Be(1);
            compilationErrors[0].ErrorCode.Should().Be(GraphCompilationErrorCode.P0060);
            compilationErrors[0].ErrorMessage.Should().Be(
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
            var graphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();

            var activity1 = new DependentActivity<int, int>(activityId1, 6);
            var activity2 = new DependentActivity<int, int>(activityId2, 7, new HashSet<int> { activityId1 });
            var activity3 = new DependentActivity<int, int>(activityId3, 4, new HashSet<int> { activityId2 });

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

            IGraphCompilation<int, int, IDependentActivity<int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int>>(new[]
                {
                    new Resource<int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0),
                    new Resource<int>(resourceId2, string.Empty, false, true, InterActivityAllocationType.None, 1.0, 0),
                    new Resource<int>(resourceId3, string.Empty, false, true, InterActivityAllocationType.None, 1.0, 0),
                }));

            compilation.ResourceSchedules.Should().NotBeEmpty();
            compilation.CompilationErrors.Should().BeEmpty();

            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.Should().Be(1);

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.Should().Be(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.Should().Be(3);

            scheduledActivities0[0].Id.Should().Be(activityId1);
            scheduledActivities0[0].StartTime.Should().Be(0);
            scheduledActivities0[0].FinishTime.Should().Be(6);

            scheduledActivities0[1].Id.Should().Be(activityId2);
            scheduledActivities0[1].StartTime.Should().Be(6);
            scheduledActivities0[1].FinishTime.Should().Be(13);

            scheduledActivities0[2].Id.Should().Be(activityId3);
            scheduledActivities0[2].StartTime.Should().Be(13);
            scheduledActivities0[2].FinishTime.Should().Be(17);

            scheduledActivities0.Last().FinishTime.Should().Be(17);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithUnlimitedResources_ThenResourceSchedulesCorrectOrder()
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
            var graphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();
            var graphBuilder = graphCompiler.Builder;
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId1, 6));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId2, 7));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId3, 8));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId4, 11, new HashSet<int> { activityId2 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId5, 8, new HashSet<int> { activityId1, activityId2, activityId3 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId6, 7, new HashSet<int> { activityId3 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId7, 4, new HashSet<int> { activityId4 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId8, 4, new HashSet<int> { activityId4, activityId6 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId9, 10, new HashSet<int> { activityId5 }));

            IGraphCompilation<int, int, IDependentActivity<int, int>> compilation = graphCompiler.Compile();

            compilation.CompilationErrors.Should().BeEmpty();

            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.Should().Be(3);

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Should().BeNull();
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.Should().Be(3);

            scheduledActivities0[0].Id.Should().Be(activityId3);
            scheduledActivities0[0].StartTime.Should().Be(0);
            scheduledActivities0[0].FinishTime.Should().Be(8);

            scheduledActivities0[1].Id.Should().Be(activityId5);
            scheduledActivities0[1].StartTime.Should().Be(8);
            scheduledActivities0[1].FinishTime.Should().Be(16);

            scheduledActivities0[2].Id.Should().Be(activityId9);
            scheduledActivities0[2].StartTime.Should().Be(16);
            scheduledActivities0[2].FinishTime.Should().Be(26);

            scheduledActivities0.Last().FinishTime.Should().Be(26);


            var resourceSchedule1 = resourceSchedules[1];
            resourceSchedule1.Resource.Should().BeNull();
            var scheduledActivities1 = resourceSchedule1.ScheduledActivities.ToList();
            scheduledActivities1.Count.Should().Be(3);

            scheduledActivities1[0].Id.Should().Be(activityId2);
            scheduledActivities1[0].StartTime.Should().Be(0);
            scheduledActivities1[0].FinishTime.Should().Be(7);

            scheduledActivities1[1].Id.Should().Be(activityId4);
            scheduledActivities1[1].StartTime.Should().Be(7);
            scheduledActivities1[1].FinishTime.Should().Be(18);

            scheduledActivities1[2].Id.Should().Be(activityId7);
            scheduledActivities1[2].StartTime.Should().Be(18);
            scheduledActivities1[2].FinishTime.Should().Be(22);

            scheduledActivities1.Last().FinishTime.Should().Be(22);


            var resourceSchedule2 = resourceSchedules[2];
            resourceSchedule2.Resource.Should().BeNull();
            var scheduledActivities2 = resourceSchedule2.ScheduledActivities.ToList();
            scheduledActivities2.Count.Should().Be(3);

            scheduledActivities2[0].Id.Should().Be(activityId1);
            scheduledActivities2[0].StartTime.Should().Be(0);
            scheduledActivities2[0].FinishTime.Should().Be(6);

            scheduledActivities2[1].Id.Should().Be(activityId6);
            scheduledActivities2[1].StartTime.Should().Be(8);
            scheduledActivities2[1].FinishTime.Should().Be(15);

            scheduledActivities2[2].Id.Should().Be(activityId8);
            scheduledActivities2[2].StartTime.Should().Be(18);
            scheduledActivities2[2].FinishTime.Should().Be(22);

            scheduledActivities2.Last().FinishTime.Should().Be(22);



            graphBuilder.Activity(activityId1).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.Should().Be(6);
            graphBuilder.Activity(activityId1).FreeSlack.Should().Be(2);
            graphBuilder.Activity(activityId1).TotalSlack.Should().Be(2);
            graphBuilder.Activity(activityId1).LatestStartTime.Should().Be(2);
            graphBuilder.Activity(activityId1).LatestFinishTime.Should().Be(8);
            graphBuilder.Activity(activityId1).ResourceDependencies.Count.Should().Be(0);

            graphBuilder.Activity(activityId2).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.Should().Be(7);
            graphBuilder.Activity(activityId2).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId2).TotalSlack.Should().Be(1);
            graphBuilder.Activity(activityId2).LatestStartTime.Should().Be(1);
            graphBuilder.Activity(activityId2).LatestFinishTime.Should().Be(8);
            graphBuilder.Activity(activityId2).ResourceDependencies.Count.Should().Be(0);

            graphBuilder.Activity(activityId3).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.Should().Be(8);
            graphBuilder.Activity(activityId3).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId3).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId3).LatestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId3).LatestFinishTime.Should().Be(8);
            graphBuilder.Activity(activityId3).ResourceDependencies.Count.Should().Be(0);

            graphBuilder.Activity(activityId4).EarliestStartTime.Should().Be(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.Should().Be(18);
            graphBuilder.Activity(activityId4).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId4).TotalSlack.Should().Be(4);
            graphBuilder.Activity(activityId4).LatestStartTime.Should().Be(11);
            graphBuilder.Activity(activityId4).LatestFinishTime.Should().Be(22);
            graphBuilder.Activity(activityId4).ResourceDependencies.Count.Should().Be(0);

            graphBuilder.Activity(activityId5).EarliestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId5).EarliestFinishTime.Should().Be(16);
            graphBuilder.Activity(activityId5).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId5).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId5).LatestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId5).LatestFinishTime.Should().Be(16);
            graphBuilder.Activity(activityId5).ResourceDependencies.Count.Should().Be(0);

            graphBuilder.Activity(activityId6).EarliestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.Should().Be(15);
            graphBuilder.Activity(activityId6).FreeSlack.Should().Be(3);
            graphBuilder.Activity(activityId6).TotalSlack.Should().Be(7);
            graphBuilder.Activity(activityId6).LatestStartTime.Should().Be(15);
            graphBuilder.Activity(activityId6).LatestFinishTime.Should().Be(22);
            graphBuilder.Activity(activityId6).ResourceDependencies.Count.Should().Be(0);

            graphBuilder.Activity(activityId7).EarliestStartTime.Should().Be(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.Should().Be(22);
            graphBuilder.Activity(activityId7).FreeSlack.Should().Be(4);
            graphBuilder.Activity(activityId7).TotalSlack.Should().Be(4);
            graphBuilder.Activity(activityId7).LatestStartTime.Should().Be(22);
            graphBuilder.Activity(activityId7).LatestFinishTime.Should().Be(26);
            graphBuilder.Activity(activityId7).ResourceDependencies.Count.Should().Be(0);

            graphBuilder.Activity(activityId8).EarliestStartTime.Should().Be(18);
            graphBuilder.Activity(activityId8).EarliestFinishTime.Should().Be(22);
            graphBuilder.Activity(activityId8).FreeSlack.Should().Be(4);
            graphBuilder.Activity(activityId8).TotalSlack.Should().Be(4);
            graphBuilder.Activity(activityId8).LatestStartTime.Should().Be(22);
            graphBuilder.Activity(activityId8).LatestFinishTime.Should().Be(26);
            graphBuilder.Activity(activityId8).ResourceDependencies.Count.Should().Be(0);

            graphBuilder.Activity(activityId9).EarliestStartTime.Should().Be(16);
            graphBuilder.Activity(activityId9).EarliestFinishTime.Should().Be(26);
            graphBuilder.Activity(activityId9).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId9).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId9).LatestStartTime.Should().Be(16);
            graphBuilder.Activity(activityId9).LatestFinishTime.Should().Be(26);
            graphBuilder.Activity(activityId9).ResourceDependencies.Count.Should().Be(0);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithUnlimitedResourcesAndTargetResources_ThenResourceSchedulesCorrectOrder()
        {
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            var graphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();

            var activity1 = new DependentActivity<int, int>(activityId1, 6);
            var activity2 = new DependentActivity<int, int>(activityId2, 7, new HashSet<int> { activityId1 });
            var activity3 = new DependentActivity<int, int>(activityId3, 4, new HashSet<int> { activityId2 });

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

            IGraphCompilation<int, int, IDependentActivity<int, int>> compilation = graphCompiler.Compile();

            compilation.ResourceSchedules.Should().NotBeEmpty();
            compilation.CompilationErrors.Should().BeEmpty();

            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.Should().Be(1);

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Should().BeNull();
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.Should().Be(3);

            scheduledActivities0[0].Id.Should().Be(activityId1);
            scheduledActivities0[0].StartTime.Should().Be(0);
            scheduledActivities0[0].FinishTime.Should().Be(6);

            scheduledActivities0[1].Id.Should().Be(activityId2);
            scheduledActivities0[1].StartTime.Should().Be(6);
            scheduledActivities0[1].FinishTime.Should().Be(13);

            scheduledActivities0[2].Id.Should().Be(activityId3);
            scheduledActivities0[2].StartTime.Should().Be(13);
            scheduledActivities0[2].FinishTime.Should().Be(17);

            scheduledActivities0.Last().FinishTime.Should().Be(17);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithFreeSlackUnlimitedResources_ThenResourceSchedulesCorrectOrder()
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
            var graphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();
            var graphBuilder = graphCompiler.Builder;
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId1, 6));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId2, 7));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId3, 8));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId4, 11, new HashSet<int> { activityId2 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId5, 8, new HashSet<int> { activityId1, activityId2, activityId3 }) { MinimumFreeSlack = 15 });
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId6, 7, new HashSet<int> { activityId3 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId7, 4, new HashSet<int> { activityId4 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId8, 4, new HashSet<int> { activityId4, activityId6 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId9, 10, new HashSet<int> { activityId5 }));

            IGraphCompilation<int, int, IDependentActivity<int, int>> compilation = graphCompiler.Compile();

            compilation.CompilationErrors.Should().BeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.Should().Be(3);

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Should().BeNull();
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.Should().Be(4);

            scheduledActivities0[0].Id.Should().Be(activityId2);
            scheduledActivities0[0].StartTime.Should().Be(0);
            scheduledActivities0[0].FinishTime.Should().Be(7);

            scheduledActivities0[1].Id.Should().Be(activityId4);
            scheduledActivities0[1].StartTime.Should().Be(7);
            scheduledActivities0[1].FinishTime.Should().Be(18);

            scheduledActivities0[2].Id.Should().Be(activityId7);
            scheduledActivities0[2].StartTime.Should().Be(18);
            scheduledActivities0[2].FinishTime.Should().Be(22);

            scheduledActivities0[3].Id.Should().Be(activityId9);
            scheduledActivities0[3].StartTime.Should().Be(31);
            scheduledActivities0[3].FinishTime.Should().Be(41);

            scheduledActivities0.Last().FinishTime.Should().Be(41);


            var resourceSchedule1 = resourceSchedules[1];
            resourceSchedule1.Resource.Should().BeNull();
            var scheduledActivities1 = resourceSchedule1.ScheduledActivities.ToList();
            scheduledActivities1.Count.Should().Be(3);

            scheduledActivities1[0].Id.Should().Be(activityId3);
            scheduledActivities1[0].StartTime.Should().Be(0);
            scheduledActivities1[0].FinishTime.Should().Be(8);

            scheduledActivities1[1].Id.Should().Be(activityId6);
            scheduledActivities1[1].StartTime.Should().Be(8);
            scheduledActivities1[1].FinishTime.Should().Be(15);

            scheduledActivities1[2].Id.Should().Be(activityId8);
            scheduledActivities1[2].StartTime.Should().Be(18);
            scheduledActivities1[2].FinishTime.Should().Be(22);

            scheduledActivities1.Last().FinishTime.Should().Be(22);


            var resourceSchedule2 = resourceSchedules[2];
            resourceSchedule2.Resource.Should().BeNull();
            var scheduledActivities2 = resourceSchedule2.ScheduledActivities.ToList();
            scheduledActivities2.Count.Should().Be(2);

            scheduledActivities2[0].Id.Should().Be(activityId1);
            scheduledActivities2[0].StartTime.Should().Be(0);
            scheduledActivities2[0].FinishTime.Should().Be(6);

            scheduledActivities2[1].Id.Should().Be(activityId5);
            scheduledActivities2[1].StartTime.Should().Be(8);
            scheduledActivities2[1].FinishTime.Should().Be(16);

            scheduledActivities2.Last().FinishTime.Should().Be(16);



            graphBuilder.Activity(activityId1).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.Should().Be(6);
            graphBuilder.Activity(activityId1).FreeSlack.Should().Be(2);
            graphBuilder.Activity(activityId1).TotalSlack.Should().Be(17);
            graphBuilder.Activity(activityId1).LatestStartTime.Should().Be(17);
            graphBuilder.Activity(activityId1).LatestFinishTime.Should().Be(23);
            graphBuilder.Activity(activityId1).ResourceDependencies.Count.Should().Be(0);

            graphBuilder.Activity(activityId2).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.Should().Be(7);
            graphBuilder.Activity(activityId2).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId2).TotalSlack.Should().Be(16);
            graphBuilder.Activity(activityId2).LatestStartTime.Should().Be(16);
            graphBuilder.Activity(activityId2).LatestFinishTime.Should().Be(23);
            graphBuilder.Activity(activityId2).ResourceDependencies.Count.Should().Be(0);

            graphBuilder.Activity(activityId3).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.Should().Be(8);
            graphBuilder.Activity(activityId3).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId3).TotalSlack.Should().Be(15);
            graphBuilder.Activity(activityId3).LatestStartTime.Should().Be(15);
            graphBuilder.Activity(activityId3).LatestFinishTime.Should().Be(23);
            graphBuilder.Activity(activityId3).ResourceDependencies.Count.Should().Be(0);

            graphBuilder.Activity(activityId4).EarliestStartTime.Should().Be(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.Should().Be(18);
            graphBuilder.Activity(activityId4).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId4).TotalSlack.Should().Be(19);
            graphBuilder.Activity(activityId4).LatestStartTime.Should().Be(26);
            graphBuilder.Activity(activityId4).LatestFinishTime.Should().Be(37);
            graphBuilder.Activity(activityId4).ResourceDependencies.Count.Should().Be(0);

            graphBuilder.Activity(activityId5).EarliestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId5).EarliestFinishTime.Should().Be(16);
            graphBuilder.Activity(activityId5).FreeSlack.Should().Be(15);
            graphBuilder.Activity(activityId5).TotalSlack.Should().Be(15);
            graphBuilder.Activity(activityId5).LatestStartTime.Should().Be(23);
            graphBuilder.Activity(activityId5).LatestFinishTime.Should().Be(31);
            graphBuilder.Activity(activityId5).ResourceDependencies.Count.Should().Be(0);

            graphBuilder.Activity(activityId6).EarliestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.Should().Be(15);
            graphBuilder.Activity(activityId6).FreeSlack.Should().Be(3);
            graphBuilder.Activity(activityId6).TotalSlack.Should().Be(22);
            graphBuilder.Activity(activityId6).LatestStartTime.Should().Be(30);
            graphBuilder.Activity(activityId6).LatestFinishTime.Should().Be(37);
            graphBuilder.Activity(activityId6).ResourceDependencies.Count.Should().Be(0);

            graphBuilder.Activity(activityId7).EarliestStartTime.Should().Be(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.Should().Be(22);
            graphBuilder.Activity(activityId7).FreeSlack.Should().Be(19);
            graphBuilder.Activity(activityId7).TotalSlack.Should().Be(19);
            graphBuilder.Activity(activityId7).LatestStartTime.Should().Be(37);
            graphBuilder.Activity(activityId7).LatestFinishTime.Should().Be(41);
            graphBuilder.Activity(activityId7).ResourceDependencies.Count.Should().Be(0);

            graphBuilder.Activity(activityId8).EarliestStartTime.Should().Be(18);
            graphBuilder.Activity(activityId8).EarliestFinishTime.Should().Be(22);
            graphBuilder.Activity(activityId8).FreeSlack.Should().Be(19);
            graphBuilder.Activity(activityId8).TotalSlack.Should().Be(19);
            graphBuilder.Activity(activityId8).LatestStartTime.Should().Be(37);
            graphBuilder.Activity(activityId8).LatestFinishTime.Should().Be(41);
            graphBuilder.Activity(activityId8).ResourceDependencies.Count.Should().Be(0);

            graphBuilder.Activity(activityId9).EarliestStartTime.Should().Be(31);
            graphBuilder.Activity(activityId9).EarliestFinishTime.Should().Be(41);
            graphBuilder.Activity(activityId9).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId9).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId9).LatestStartTime.Should().Be(31);
            graphBuilder.Activity(activityId9).LatestFinishTime.Should().Be(41);
            graphBuilder.Activity(activityId9).ResourceDependencies.Count.Should().Be(0);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithTwoNoneAndDirectResources_ThenResourceSchedulesCorrectOrder()
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
            var graphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();
            var graphBuilder = graphCompiler.Builder;
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId1, 6));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId2, 7) { HasNoCost = true });
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId3, 8) { HasNoCost = true });
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId4, 11, new HashSet<int> { activityId2 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId5, 8, new HashSet<int> { activityId1, activityId2, activityId3 }) { HasNoCost = true });
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId6, 7, new HashSet<int> { activityId3 }) { HasNoCost = true });
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId7, 4, new HashSet<int> { activityId4 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId8, 4, new HashSet<int> { activityId4, activityId6 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId9, 10, new HashSet<int> { activityId5 }));

            int resourceId1 = 1;
            int resourceId2 = resourceId1 + 1;
            IGraphCompilation<int, int, IDependentActivity<int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int>>(new[]
                {
                    new Resource<int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0),
                    new Resource<int>(resourceId2, string.Empty, false, false, InterActivityAllocationType.Direct, 1.0, 0),
                }));

            compilation.CompilationErrors.Should().BeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count().Should().Be(2);

            resourceSchedules[0].ActivityAllocation.Should().BeEquivalentTo(
                new bool[] {
                    false, false, false, false, false, false, false, false, true, true,
                    true, true, true, true, true, true, true, true, true, false,
                    false, false, false, false, false, false, true, true, true, true,
                    true, true, true, true,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.Should().Be(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.Should().Be(5);

            scheduledActivities0[0].Id.Should().Be(activityId3);
            scheduledActivities0[0].HasNoCost.Should().BeTrue();
            scheduledActivities0[0].StartTime.Should().Be(0);
            scheduledActivities0[0].FinishTime.Should().Be(8);

            scheduledActivities0[1].Id.Should().Be(activityId4);
            scheduledActivities0[1].HasNoCost.Should().BeFalse();
            scheduledActivities0[1].StartTime.Should().Be(8);
            scheduledActivities0[1].FinishTime.Should().Be(19);

            scheduledActivities0[2].Id.Should().Be(activityId6);
            scheduledActivities0[2].HasNoCost.Should().BeTrue();
            scheduledActivities0[2].StartTime.Should().Be(19);
            scheduledActivities0[2].FinishTime.Should().Be(26);

            scheduledActivities0[3].Id.Should().Be(activityId7);
            scheduledActivities0[3].HasNoCost.Should().BeFalse();
            scheduledActivities0[3].StartTime.Should().Be(26);
            scheduledActivities0[3].FinishTime.Should().Be(30);

            scheduledActivities0[4].Id.Should().Be(activityId8);
            scheduledActivities0[4].HasNoCost.Should().BeFalse();
            scheduledActivities0[4].StartTime.Should().Be(30);
            scheduledActivities0[4].FinishTime.Should().Be(34);

            scheduledActivities0.Last().FinishTime.Should().Be(34);


            resourceSchedules[1].ActivityAllocation.Should().BeEquivalentTo(
                new bool[] {
                    false, false, false, false, false, false, false, true, true, true,
                    true, true, true, false, false, false, false, false, false, false,
                    false, true, true, true, true, true, true, true, true, true,
                    true, false, false, false,
                });

            var resourceSchedule1 = resourceSchedules[1];
            resourceSchedule1.Resource.Id.Should().Be(resourceId2);
            var scheduledActivities1 = resourceSchedule1.ScheduledActivities.ToList();
            scheduledActivities1.Count.Should().Be(4);

            scheduledActivities1[0].Id.Should().Be(activityId2);
            scheduledActivities1[0].HasNoCost.Should().BeTrue();
            scheduledActivities1[0].StartTime.Should().Be(0);
            scheduledActivities1[0].FinishTime.Should().Be(7);

            scheduledActivities1[1].Id.Should().Be(activityId1);
            scheduledActivities1[1].HasNoCost.Should().BeFalse();
            scheduledActivities1[1].StartTime.Should().Be(7);
            scheduledActivities1[1].FinishTime.Should().Be(13);

            scheduledActivities1[2].Id.Should().Be(activityId5);
            scheduledActivities1[2].HasNoCost.Should().BeTrue();
            scheduledActivities1[2].StartTime.Should().Be(13);
            scheduledActivities1[2].FinishTime.Should().Be(21);

            scheduledActivities1[3].Id.Should().Be(activityId9);
            scheduledActivities1[3].HasNoCost.Should().BeFalse();
            scheduledActivities1[3].StartTime.Should().Be(21);
            scheduledActivities1[3].FinishTime.Should().Be(31);

            scheduledActivities1.Last().FinishTime.Should().Be(31);



            graphBuilder.Activity(activityId1).EarliestStartTime.Should().Be(7);
            graphBuilder.Activity(activityId1).EarliestFinishTime.Should().Be(13);
            graphBuilder.Activity(activityId1).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId1).TotalSlack.Should().Be(3);
            graphBuilder.Activity(activityId1).LatestStartTime.Should().Be(10);
            graphBuilder.Activity(activityId1).LatestFinishTime.Should().Be(16);
            graphBuilder.Activity(activityId1).ResourceDependencies.Should().BeEquivalentTo(new List<int>(new int[] { activityId2 }));
            graphBuilder.Activity(activityId1).AllocatedToResources.Should().BeEquivalentTo(new List<int>(new int[] { resourceId2 }));

            graphBuilder.Activity(activityId2).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.Should().Be(7);
            graphBuilder.Activity(activityId2).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId2).TotalSlack.Should().Be(1);
            graphBuilder.Activity(activityId2).LatestStartTime.Should().Be(1);
            graphBuilder.Activity(activityId2).LatestFinishTime.Should().Be(8);
            graphBuilder.Activity(activityId2).ResourceDependencies.Count.Should().Be(0);
            graphBuilder.Activity(activityId2).AllocatedToResources.Should().BeEquivalentTo(new List<int>(new int[] { resourceId2 }));

            graphBuilder.Activity(activityId3).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.Should().Be(8);
            graphBuilder.Activity(activityId3).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId3).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId3).LatestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId3).LatestFinishTime.Should().Be(8);
            graphBuilder.Activity(activityId3).ResourceDependencies.Count.Should().Be(0);
            graphBuilder.Activity(activityId3).AllocatedToResources.Should().BeEquivalentTo(new List<int>(new int[] { resourceId1 }));

            graphBuilder.Activity(activityId4).EarliestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId4).EarliestFinishTime.Should().Be(19);
            graphBuilder.Activity(activityId4).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId4).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId4).LatestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId4).LatestFinishTime.Should().Be(19);
            graphBuilder.Activity(activityId4).ResourceDependencies.Should().BeEquivalentTo(new List<int>(new int[] { activityId3 }));
            graphBuilder.Activity(activityId4).AllocatedToResources.Should().BeEquivalentTo(new List<int>(new int[] { resourceId1 }));

            graphBuilder.Activity(activityId5).EarliestStartTime.Should().Be(13);
            graphBuilder.Activity(activityId5).EarliestFinishTime.Should().Be(21);
            graphBuilder.Activity(activityId5).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId5).TotalSlack.Should().Be(3);
            graphBuilder.Activity(activityId5).LatestStartTime.Should().Be(16);
            graphBuilder.Activity(activityId5).LatestFinishTime.Should().Be(24);
            graphBuilder.Activity(activityId5).ResourceDependencies.Should().BeEquivalentTo(new List<int>(new int[] { activityId1 }));
            graphBuilder.Activity(activityId5).AllocatedToResources.Should().BeEquivalentTo(new List<int>(new int[] { resourceId2 }));

            graphBuilder.Activity(activityId6).EarliestStartTime.Should().Be(19);
            graphBuilder.Activity(activityId6).EarliestFinishTime.Should().Be(26);
            graphBuilder.Activity(activityId6).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId6).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId6).LatestStartTime.Should().Be(19);
            graphBuilder.Activity(activityId6).LatestFinishTime.Should().Be(26);
            graphBuilder.Activity(activityId6).ResourceDependencies.Should().BeEquivalentTo(new List<int>(new int[] { activityId4 }));
            graphBuilder.Activity(activityId6).AllocatedToResources.Should().BeEquivalentTo(new List<int>(new int[] { resourceId1 }));

            graphBuilder.Activity(activityId7).EarliestStartTime.Should().Be(26);
            graphBuilder.Activity(activityId7).EarliestFinishTime.Should().Be(30);
            graphBuilder.Activity(activityId7).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId7).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId7).LatestStartTime.Should().Be(26);
            graphBuilder.Activity(activityId7).LatestFinishTime.Should().Be(30);
            graphBuilder.Activity(activityId7).ResourceDependencies.Should().BeEquivalentTo(new List<int>(new int[] { activityId6 }));
            graphBuilder.Activity(activityId7).AllocatedToResources.Should().BeEquivalentTo(new List<int>(new int[] { resourceId1 }));

            graphBuilder.Activity(activityId8).EarliestStartTime.Should().Be(30);
            graphBuilder.Activity(activityId8).EarliestFinishTime.Should().Be(34);
            graphBuilder.Activity(activityId8).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId8).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId8).LatestStartTime.Should().Be(30);
            graphBuilder.Activity(activityId8).LatestFinishTime.Should().Be(34);
            graphBuilder.Activity(activityId8).ResourceDependencies.Should().BeEquivalentTo(new List<int>(new int[] { activityId7 }));
            graphBuilder.Activity(activityId8).AllocatedToResources.Should().BeEquivalentTo(new List<int>(new int[] { resourceId1 }));

            graphBuilder.Activity(activityId9).EarliestStartTime.Should().Be(21);
            graphBuilder.Activity(activityId9).EarliestFinishTime.Should().Be(31);
            graphBuilder.Activity(activityId9).FreeSlack.Should().Be(3);
            graphBuilder.Activity(activityId9).TotalSlack.Should().Be(3);
            graphBuilder.Activity(activityId9).LatestStartTime.Should().Be(24);
            graphBuilder.Activity(activityId9).LatestFinishTime.Should().Be(34);
            graphBuilder.Activity(activityId9).ResourceDependencies.Should().BeEquivalentTo(new List<int>(new int[] { activityId5 }));
            graphBuilder.Activity(activityId9).AllocatedToResources.Should().BeEquivalentTo(new List<int>(new int[] { resourceId2 }));
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithOneActiveAndTwoInactiveResources_ThenResourceSchedulesCorrectOrder()
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
            var graphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();
            var graphBuilder = graphCompiler.Builder;
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId1, 6));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId2, 7) { HasNoCost = true });
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId3, 8) { HasNoCost = true });
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId4, 11, new HashSet<int> { activityId2 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId5, 8, new HashSet<int> { activityId1, activityId2, activityId3 }) { HasNoCost = true });
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId6, 7, new HashSet<int> { activityId3 }) { HasNoCost = true });
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId7, 4, new HashSet<int> { activityId4 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId8, 4, new HashSet<int> { activityId4, activityId6 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId9, 10, new HashSet<int> { activityId5 }));

            int resourceId1 = 1;
            int resourceId2 = resourceId1 + 1;
            int resourceId3 = resourceId2 + 1;

            IGraphCompilation<int, int, IDependentActivity<int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int>>(new[]
                {
                    new Resource<int>(resourceId1, string.Empty, false, true, InterActivityAllocationType.None, 1.0, 0),
                    new Resource<int>(resourceId2, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0),
                    new Resource<int>(resourceId3, string.Empty, false, true, InterActivityAllocationType.None, 1.0, 0),
                }));

            compilation.CompilationErrors.Should().BeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count().Should().Be(1);

            resourceSchedules[0].ActivityAllocation.Should().BeEquivalentTo(
                new bool[] {
                    false, false, false, false, false, false, false, false, false, false,
                    false, false, false, false, false, true, true, true, true, true,
                    true, false, false, false, false, false, false, false, false, true,
                    true, true, true, true, true, true, true, true, true, true,
                    false, false, false, false, false, false, false, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.Should().Be(resourceId2);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.Should().Be(9);

            scheduledActivities0[0].Id.Should().Be(activityId3);
            scheduledActivities0[0].HasNoCost.Should().BeTrue();
            scheduledActivities0[0].StartTime.Should().Be(0);
            scheduledActivities0[0].FinishTime.Should().Be(8);

            scheduledActivities0[1].Id.Should().Be(activityId2);
            scheduledActivities0[1].HasNoCost.Should().BeTrue();
            scheduledActivities0[1].StartTime.Should().Be(8);
            scheduledActivities0[1].FinishTime.Should().Be(15);

            scheduledActivities0[2].Id.Should().Be(activityId1);
            scheduledActivities0[2].HasNoCost.Should().BeFalse();
            scheduledActivities0[2].StartTime.Should().Be(15);
            scheduledActivities0[2].FinishTime.Should().Be(21);

            scheduledActivities0[3].Id.Should().Be(activityId5);
            scheduledActivities0[3].HasNoCost.Should().BeTrue();
            scheduledActivities0[3].StartTime.Should().Be(21);
            scheduledActivities0[3].FinishTime.Should().Be(29);

            scheduledActivities0[4].Id.Should().Be(activityId4);
            scheduledActivities0[4].HasNoCost.Should().BeFalse();
            scheduledActivities0[4].StartTime.Should().Be(29);
            scheduledActivities0[4].FinishTime.Should().Be(40);

            scheduledActivities0[5].Id.Should().Be(activityId6);
            scheduledActivities0[5].HasNoCost.Should().BeTrue();
            scheduledActivities0[5].StartTime.Should().Be(40);
            scheduledActivities0[5].FinishTime.Should().Be(47);

            scheduledActivities0[6].Id.Should().Be(activityId9);
            scheduledActivities0[6].HasNoCost.Should().BeFalse();
            scheduledActivities0[6].StartTime.Should().Be(47);
            scheduledActivities0[6].FinishTime.Should().Be(57);

            scheduledActivities0[7].Id.Should().Be(activityId7);
            scheduledActivities0[7].HasNoCost.Should().BeFalse();
            scheduledActivities0[7].StartTime.Should().Be(57);
            scheduledActivities0[7].FinishTime.Should().Be(61);

            scheduledActivities0[8].Id.Should().Be(activityId8);
            scheduledActivities0[8].HasNoCost.Should().BeFalse();
            scheduledActivities0[8].StartTime.Should().Be(61);
            scheduledActivities0[8].FinishTime.Should().Be(65);

            scheduledActivities0.Last().FinishTime.Should().Be(65);

            graphBuilder.Activity(activityId1).EarliestStartTime.Should().Be(15);
            graphBuilder.Activity(activityId1).EarliestFinishTime.Should().Be(21);
            graphBuilder.Activity(activityId1).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId1).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId1).LatestStartTime.Should().Be(15);
            graphBuilder.Activity(activityId1).LatestFinishTime.Should().Be(21);
            graphBuilder.Activity(activityId1).ResourceDependencies.Should().BeEquivalentTo(new List<int>(new int[] { activityId2 }));
            graphBuilder.Activity(activityId1).AllocatedToResources.Should().BeEquivalentTo(new List<int>(new int[] { resourceId2 }));

            graphBuilder.Activity(activityId2).EarliestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId2).EarliestFinishTime.Should().Be(15);
            graphBuilder.Activity(activityId2).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId2).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId2).LatestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId2).LatestFinishTime.Should().Be(15);
            graphBuilder.Activity(activityId2).ResourceDependencies.Should().BeEquivalentTo(new List<int>(new int[] { activityId3 }));
            graphBuilder.Activity(activityId2).AllocatedToResources.Should().BeEquivalentTo(new List<int>(new int[] { resourceId2 }));

            graphBuilder.Activity(activityId3).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.Should().Be(8);
            graphBuilder.Activity(activityId3).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId3).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId3).LatestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId3).LatestFinishTime.Should().Be(8);
            graphBuilder.Activity(activityId3).ResourceDependencies.Count.Should().Be(0);
            graphBuilder.Activity(activityId3).AllocatedToResources.Should().BeEquivalentTo(new List<int>(new int[] { resourceId2 }));

            graphBuilder.Activity(activityId4).EarliestStartTime.Should().Be(29);
            graphBuilder.Activity(activityId4).EarliestFinishTime.Should().Be(40);
            graphBuilder.Activity(activityId4).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId4).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId4).LatestStartTime.Should().Be(29);
            graphBuilder.Activity(activityId4).LatestFinishTime.Should().Be(40);
            graphBuilder.Activity(activityId4).ResourceDependencies.Should().BeEquivalentTo(new List<int>(new int[] { activityId5 }));
            graphBuilder.Activity(activityId4).AllocatedToResources.Should().BeEquivalentTo(new List<int>(new int[] { resourceId2 }));

            graphBuilder.Activity(activityId5).EarliestStartTime.Should().Be(21);
            graphBuilder.Activity(activityId5).EarliestFinishTime.Should().Be(29);
            graphBuilder.Activity(activityId5).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId5).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId5).LatestStartTime.Should().Be(21);
            graphBuilder.Activity(activityId5).LatestFinishTime.Should().Be(29);
            graphBuilder.Activity(activityId5).ResourceDependencies.Should().BeEquivalentTo(new List<int>(new int[] { activityId1 }));
            graphBuilder.Activity(activityId5).AllocatedToResources.Should().BeEquivalentTo(new List<int>(new int[] { resourceId2 }));

            graphBuilder.Activity(activityId6).EarliestStartTime.Should().Be(40);
            graphBuilder.Activity(activityId6).EarliestFinishTime.Should().Be(47);
            graphBuilder.Activity(activityId6).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId6).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId6).LatestStartTime.Should().Be(40);
            graphBuilder.Activity(activityId6).LatestFinishTime.Should().Be(47);
            graphBuilder.Activity(activityId6).ResourceDependencies.Should().BeEquivalentTo(new List<int>(new int[] { activityId4 }));
            graphBuilder.Activity(activityId6).AllocatedToResources.Should().BeEquivalentTo(new List<int>(new int[] { resourceId2 }));

            graphBuilder.Activity(activityId7).EarliestStartTime.Should().Be(57);
            graphBuilder.Activity(activityId7).EarliestFinishTime.Should().Be(61);
            graphBuilder.Activity(activityId7).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId7).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId7).LatestStartTime.Should().Be(57);
            graphBuilder.Activity(activityId7).LatestFinishTime.Should().Be(61);
            graphBuilder.Activity(activityId7).ResourceDependencies.Should().BeEquivalentTo(new List<int>(new int[] { activityId9 }));
            graphBuilder.Activity(activityId7).AllocatedToResources.Should().BeEquivalentTo(new List<int>(new int[] { resourceId2 }));

            graphBuilder.Activity(activityId8).EarliestStartTime.Should().Be(61);
            graphBuilder.Activity(activityId8).EarliestFinishTime.Should().Be(65);
            graphBuilder.Activity(activityId8).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId8).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId8).LatestStartTime.Should().Be(61);
            graphBuilder.Activity(activityId8).LatestFinishTime.Should().Be(65);
            graphBuilder.Activity(activityId8).ResourceDependencies.Should().BeEquivalentTo(new List<int>(new int[] { activityId7 }));
            graphBuilder.Activity(activityId8).AllocatedToResources.Should().BeEquivalentTo(new List<int>(new int[] { resourceId2 }));

            graphBuilder.Activity(activityId9).EarliestStartTime.Should().Be(47);
            graphBuilder.Activity(activityId9).EarliestFinishTime.Should().Be(57);
            graphBuilder.Activity(activityId9).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId9).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId9).LatestStartTime.Should().Be(47);
            graphBuilder.Activity(activityId9).LatestFinishTime.Should().Be(57);
            graphBuilder.Activity(activityId9).ResourceDependencies.Should().BeEquivalentTo(new List<int>(new int[] { activityId6 }));
            graphBuilder.Activity(activityId9).AllocatedToResources.Should().BeEquivalentTo(new List<int>(new int[] { resourceId2 }));
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithTwoIndirectResources_ThenResourceSchedulesCorrectOrder()
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
            var graphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();
            var graphBuilder = graphCompiler.Builder;
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId1, 6));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId2, 7));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId3, 8) { HasNoCost = true });
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId4, 11, new HashSet<int> { activityId2 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId5, 8, new HashSet<int> { activityId1, activityId2, activityId3 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId6, 7, new HashSet<int> { activityId3 }) { HasNoCost = true });
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId7, 4, new HashSet<int> { activityId4 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId8, 4, new HashSet<int> { activityId4, activityId6 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId9, 10, new HashSet<int> { activityId5 }));

            int resourceId1 = 1;
            int resourceId2 = resourceId1 + 1;
            IGraphCompilation<int, int, IDependentActivity<int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int>>(new[]
                {
                    new Resource<int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.Indirect, 1.0, 0),
                    new Resource<int>(resourceId2, string.Empty, false, false, InterActivityAllocationType.Indirect, 1.0, 0),
                }));

            compilation.CompilationErrors.Should().BeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count().Should().Be(2);

            resourceSchedules[0].ActivityAllocation.Should().BeEquivalentTo(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.Should().Be(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.Should().Be(5);

            scheduledActivities0[0].Id.Should().Be(activityId3);
            scheduledActivities0[0].HasNoCost.Should().BeTrue();
            scheduledActivities0[0].StartTime.Should().Be(0);
            scheduledActivities0[0].FinishTime.Should().Be(8);

            scheduledActivities0[1].Id.Should().Be(activityId4);
            scheduledActivities0[1].HasNoCost.Should().BeFalse();
            scheduledActivities0[1].StartTime.Should().Be(8);
            scheduledActivities0[1].FinishTime.Should().Be(19);

            scheduledActivities0[2].Id.Should().Be(activityId6);
            scheduledActivities0[2].HasNoCost.Should().BeTrue();
            scheduledActivities0[2].StartTime.Should().Be(19);
            scheduledActivities0[2].FinishTime.Should().Be(26);

            scheduledActivities0[3].Id.Should().Be(activityId7);
            scheduledActivities0[3].HasNoCost.Should().BeFalse();
            scheduledActivities0[3].StartTime.Should().Be(26);
            scheduledActivities0[3].FinishTime.Should().Be(30);

            scheduledActivities0[4].Id.Should().Be(activityId8);
            scheduledActivities0[4].HasNoCost.Should().BeFalse();
            scheduledActivities0[4].StartTime.Should().Be(30);
            scheduledActivities0[4].FinishTime.Should().Be(34);

            scheduledActivities0.Last().FinishTime.Should().Be(34);


            resourceSchedules[1].ActivityAllocation.Should().BeEquivalentTo(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true,
                });

            var resourceSchedule1 = resourceSchedules[1];
            resourceSchedule1.Resource.Id.Should().Be(resourceId2);
            var scheduledActivities1 = resourceSchedule1.ScheduledActivities.ToList();
            scheduledActivities1.Count.Should().Be(4);

            scheduledActivities1[0].Id.Should().Be(activityId2);
            scheduledActivities1[0].HasNoCost.Should().BeFalse();
            scheduledActivities1[0].StartTime.Should().Be(0);
            scheduledActivities1[0].FinishTime.Should().Be(7);

            scheduledActivities1[1].Id.Should().Be(activityId1);
            scheduledActivities1[1].HasNoCost.Should().BeFalse();
            scheduledActivities1[1].StartTime.Should().Be(7);
            scheduledActivities1[1].FinishTime.Should().Be(13);

            scheduledActivities1[2].Id.Should().Be(activityId5);
            scheduledActivities1[2].HasNoCost.Should().BeFalse();
            scheduledActivities1[2].StartTime.Should().Be(13);
            scheduledActivities1[2].FinishTime.Should().Be(21);

            scheduledActivities1[3].Id.Should().Be(activityId9);
            scheduledActivities1[3].HasNoCost.Should().BeFalse();
            scheduledActivities1[3].StartTime.Should().Be(21);
            scheduledActivities1[3].FinishTime.Should().Be(31);

            scheduledActivities1.Last().FinishTime.Should().Be(31);



            graphBuilder.Activity(activityId1).EarliestStartTime.Should().Be(7);
            graphBuilder.Activity(activityId1).EarliestFinishTime.Should().Be(13);
            graphBuilder.Activity(activityId1).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId1).TotalSlack.Should().Be(3);
            graphBuilder.Activity(activityId1).LatestStartTime.Should().Be(10);
            graphBuilder.Activity(activityId1).LatestFinishTime.Should().Be(16);
            graphBuilder.Activity(activityId1).ResourceDependencies.Should().BeEquivalentTo(new List<int>(new int[] { activityId2 }));
            graphBuilder.Activity(activityId1).AllocatedToResources.Should().BeEquivalentTo(new List<int>(new int[] { resourceId2 }));

            graphBuilder.Activity(activityId2).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.Should().Be(7);
            graphBuilder.Activity(activityId2).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId2).TotalSlack.Should().Be(1);
            graphBuilder.Activity(activityId2).LatestStartTime.Should().Be(1);
            graphBuilder.Activity(activityId2).LatestFinishTime.Should().Be(8);
            graphBuilder.Activity(activityId2).ResourceDependencies.Count.Should().Be(0);
            graphBuilder.Activity(activityId2).AllocatedToResources.Should().BeEquivalentTo(new List<int>(new int[] { resourceId2 }));

            graphBuilder.Activity(activityId3).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.Should().Be(8);
            graphBuilder.Activity(activityId3).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId3).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId3).LatestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId3).LatestFinishTime.Should().Be(8);
            graphBuilder.Activity(activityId3).ResourceDependencies.Count.Should().Be(0);
            graphBuilder.Activity(activityId3).AllocatedToResources.Should().BeEquivalentTo(new List<int>(new int[] { resourceId1 }));

            graphBuilder.Activity(activityId4).EarliestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId4).EarliestFinishTime.Should().Be(19);
            graphBuilder.Activity(activityId4).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId4).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId4).LatestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId4).LatestFinishTime.Should().Be(19);
            graphBuilder.Activity(activityId4).ResourceDependencies.Should().BeEquivalentTo(new List<int>(new int[] { activityId3 }));
            graphBuilder.Activity(activityId4).AllocatedToResources.Should().BeEquivalentTo(new List<int>(new int[] { resourceId1 }));

            graphBuilder.Activity(activityId5).EarliestStartTime.Should().Be(13);
            graphBuilder.Activity(activityId5).EarliestFinishTime.Should().Be(21);
            graphBuilder.Activity(activityId5).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId5).TotalSlack.Should().Be(3);
            graphBuilder.Activity(activityId5).LatestStartTime.Should().Be(16);
            graphBuilder.Activity(activityId5).LatestFinishTime.Should().Be(24);
            graphBuilder.Activity(activityId5).ResourceDependencies.Should().BeEquivalentTo(new List<int>(new int[] { activityId1 }));
            graphBuilder.Activity(activityId5).AllocatedToResources.Should().BeEquivalentTo(new List<int>(new int[] { resourceId2 }));

            graphBuilder.Activity(activityId6).EarliestStartTime.Should().Be(19);
            graphBuilder.Activity(activityId6).EarliestFinishTime.Should().Be(26);
            graphBuilder.Activity(activityId6).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId6).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId6).LatestStartTime.Should().Be(19);
            graphBuilder.Activity(activityId6).LatestFinishTime.Should().Be(26);
            graphBuilder.Activity(activityId6).ResourceDependencies.Should().BeEquivalentTo(new List<int>(new int[] { activityId4 }));
            graphBuilder.Activity(activityId6).AllocatedToResources.Should().BeEquivalentTo(new List<int>(new int[] { resourceId1 }));

            graphBuilder.Activity(activityId7).EarliestStartTime.Should().Be(26);
            graphBuilder.Activity(activityId7).EarliestFinishTime.Should().Be(30);
            graphBuilder.Activity(activityId7).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId7).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId7).LatestStartTime.Should().Be(26);
            graphBuilder.Activity(activityId7).LatestFinishTime.Should().Be(30);
            graphBuilder.Activity(activityId7).ResourceDependencies.Should().BeEquivalentTo(new List<int>(new int[] { activityId6 }));
            graphBuilder.Activity(activityId7).AllocatedToResources.Should().BeEquivalentTo(new List<int>(new int[] { resourceId1 }));

            graphBuilder.Activity(activityId8).EarliestStartTime.Should().Be(30);
            graphBuilder.Activity(activityId8).EarliestFinishTime.Should().Be(34);
            graphBuilder.Activity(activityId8).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId8).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId8).LatestStartTime.Should().Be(30);
            graphBuilder.Activity(activityId8).LatestFinishTime.Should().Be(34);
            graphBuilder.Activity(activityId8).ResourceDependencies.Should().BeEquivalentTo(new List<int>(new int[] { activityId7 }));
            graphBuilder.Activity(activityId8).AllocatedToResources.Should().BeEquivalentTo(new List<int>(new int[] { resourceId1 }));

            graphBuilder.Activity(activityId9).EarliestStartTime.Should().Be(21);
            graphBuilder.Activity(activityId9).EarliestFinishTime.Should().Be(31);
            graphBuilder.Activity(activityId9).FreeSlack.Should().Be(3);
            graphBuilder.Activity(activityId9).TotalSlack.Should().Be(3);
            graphBuilder.Activity(activityId9).LatestStartTime.Should().Be(24);
            graphBuilder.Activity(activityId9).LatestFinishTime.Should().Be(34);
            graphBuilder.Activity(activityId9).ResourceDependencies.Should().BeEquivalentTo(new List<int>(new int[] { activityId5 }));
            graphBuilder.Activity(activityId9).AllocatedToResources.Should().BeEquivalentTo(new List<int>(new int[] { resourceId2 }));
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithOneOfEachTypeResources_WithNoUncostedActivities_ThenOutputsAsExpected()
        {
            int resourceId1 = 1;
            int resourceId2 = resourceId1 + 1;
            int resourceId3 = resourceId2 + 1;

            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;

            var graphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();

            var resource1 = new Resource<int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.Direct, 1.0, 0);
            var resource2 = new Resource<int>(resourceId2, string.Empty, false, false, InterActivityAllocationType.Indirect, 1.0, 0);
            var resource3 = new Resource<int>(resourceId3, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0);

            var activity1 = new DependentActivity<int, int>(activityId1, 5);
            activity1.TargetResources.Add(resourceId1);
            var activity2 = new DependentActivity<int, int>(activityId2, 3);
            activity2.TargetResources.Add(resourceId2);
            var activity3 = new DependentActivity<int, int>(activityId3, 12);
            activity3.TargetResources.Add(resourceId3);


            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, IDependentActivity<int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int>>(new[] { resource1, resource2, resource3 }));

            compilation.CompilationErrors.Should().BeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count().Should().Be(3);

            resourceSchedules[0].ActivityAllocation.Should().BeEquivalentTo(
                new bool[] {
                    true, true, true, true, true, false, false, false, false, false,
                    false, false,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.Should().Be(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.Should().Be(1);

            scheduledActivities0[0].Id.Should().Be(activityId1);
            scheduledActivities0[0].HasNoCost.Should().BeFalse();
            scheduledActivities0[0].StartTime.Should().Be(0);
            scheduledActivities0[0].FinishTime.Should().Be(5);

            scheduledActivities0.Last().FinishTime.Should().Be(5);


            resourceSchedules[1].ActivityAllocation.Should().BeEquivalentTo(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            var resourceSchedule1 = resourceSchedules[1];
            resourceSchedule1.Resource.Id.Should().Be(resourceId2);
            var scheduledActivities1 = resourceSchedule1.ScheduledActivities.ToList();
            scheduledActivities1.Count.Should().Be(1);

            scheduledActivities1[0].Id.Should().Be(activityId2);
            scheduledActivities1[0].HasNoCost.Should().BeFalse();
            scheduledActivities1[0].StartTime.Should().Be(0);
            scheduledActivities1[0].FinishTime.Should().Be(3);

            scheduledActivities1.Last().FinishTime.Should().Be(3);


            resourceSchedules[2].ActivityAllocation.Should().BeEquivalentTo(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            var resourceSchedule2 = resourceSchedules[2];
            resourceSchedule2.Resource.Id.Should().Be(resourceId3);
            var scheduledActivities2 = resourceSchedule2.ScheduledActivities.ToList();
            scheduledActivities2.Count.Should().Be(1);

            scheduledActivities2[0].Id.Should().Be(activityId3);
            scheduledActivities2[0].HasNoCost.Should().BeFalse();
            scheduledActivities2[0].StartTime.Should().Be(0);
            scheduledActivities2[0].FinishTime.Should().Be(12);

            scheduledActivities2.Last().FinishTime.Should().Be(12);
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

            var graphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();

            var resource1 = new Resource<int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.Direct, 1.0, 0);
            var resource2 = new Resource<int>(resourceId2, string.Empty, false, false, InterActivityAllocationType.Indirect, 1.0, 0);
            var resource3 = new Resource<int>(resourceId3, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0);

            var activity1 = new DependentActivity<int, int>(activityId1, 5) { HasNoCost = true };
            activity1.TargetResources.Add(resourceId1);
            var activity2 = new DependentActivity<int, int>(activityId2, 3);
            activity2.TargetResources.Add(resourceId2);
            var activity3 = new DependentActivity<int, int>(activityId3, 12);
            activity3.TargetResources.Add(resourceId3);


            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, IDependentActivity<int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int>>(new[] { resource1, resource2, resource3 }));

            compilation.CompilationErrors.Should().BeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count().Should().Be(3);

            resourceSchedules[0].ActivityAllocation.Should().BeEquivalentTo(
                new bool[] {
                    false, false, false, false, false, false, false, false, false, false,
                    false, false,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.Should().Be(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.Should().Be(1);

            scheduledActivities0[0].Id.Should().Be(activityId1);
            scheduledActivities0[0].HasNoCost.Should().BeTrue();
            scheduledActivities0[0].StartTime.Should().Be(0);
            scheduledActivities0[0].FinishTime.Should().Be(5);

            scheduledActivities0.Last().FinishTime.Should().Be(5);


            resourceSchedules[1].ActivityAllocation.Should().BeEquivalentTo(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            var resourceSchedule1 = resourceSchedules[1];
            resourceSchedule1.Resource.Id.Should().Be(resourceId2);
            var scheduledActivities1 = resourceSchedule1.ScheduledActivities.ToList();
            scheduledActivities1.Count.Should().Be(1);

            scheduledActivities1[0].Id.Should().Be(activityId2);
            scheduledActivities1[0].HasNoCost.Should().BeFalse();
            scheduledActivities1[0].StartTime.Should().Be(0);
            scheduledActivities1[0].FinishTime.Should().Be(3);

            scheduledActivities1.Last().FinishTime.Should().Be(3);


            resourceSchedules[2].ActivityAllocation.Should().BeEquivalentTo(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            var resourceSchedule2 = resourceSchedules[2];
            resourceSchedule2.Resource.Id.Should().Be(resourceId3);
            var scheduledActivities2 = resourceSchedule2.ScheduledActivities.ToList();
            scheduledActivities2.Count.Should().Be(1);

            scheduledActivities2[0].Id.Should().Be(activityId3);
            scheduledActivities2[0].HasNoCost.Should().BeFalse();
            scheduledActivities2[0].StartTime.Should().Be(0);
            scheduledActivities2[0].FinishTime.Should().Be(12);

            scheduledActivities2.Last().FinishTime.Should().Be(12);
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

            var graphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();

            var resource1 = new Resource<int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.Direct, 1.0, 0);
            var resource2 = new Resource<int>(resourceId2, string.Empty, false, false, InterActivityAllocationType.Indirect, 1.0, 0);
            var resource3 = new Resource<int>(resourceId3, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0);

            var activity1 = new DependentActivity<int, int>(activityId1, 5);
            activity1.TargetResources.Add(resourceId1);
            var activity2 = new DependentActivity<int, int>(activityId2, 3) { HasNoCost = true };
            activity2.TargetResources.Add(resourceId2);
            var activity3 = new DependentActivity<int, int>(activityId3, 12);
            activity3.TargetResources.Add(resourceId3);


            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, IDependentActivity<int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int>>(new[] { resource1, resource2, resource3 }));

            compilation.CompilationErrors.Should().BeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count().Should().Be(3);

            resourceSchedules[0].ActivityAllocation.Should().BeEquivalentTo(
                new bool[] {
                    true, true, true, true, true, false, false, false, false, false,
                    false, false,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.Should().Be(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.Should().Be(1);

            scheduledActivities0[0].Id.Should().Be(activityId1);
            scheduledActivities0[0].HasNoCost.Should().BeFalse();
            scheduledActivities0[0].StartTime.Should().Be(0);
            scheduledActivities0[0].FinishTime.Should().Be(5);

            scheduledActivities0.Last().FinishTime.Should().Be(5);


            resourceSchedules[1].ActivityAllocation.Should().BeEquivalentTo(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            var resourceSchedule1 = resourceSchedules[1];
            resourceSchedule1.Resource.Id.Should().Be(resourceId2);
            var scheduledActivities1 = resourceSchedule1.ScheduledActivities.ToList();
            scheduledActivities1.Count.Should().Be(1);

            scheduledActivities1[0].Id.Should().Be(activityId2);
            scheduledActivities1[0].HasNoCost.Should().BeTrue();
            scheduledActivities1[0].StartTime.Should().Be(0);
            scheduledActivities1[0].FinishTime.Should().Be(3);

            scheduledActivities1.Last().FinishTime.Should().Be(3);


            resourceSchedules[2].ActivityAllocation.Should().BeEquivalentTo(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            var resourceSchedule2 = resourceSchedules[2];
            resourceSchedule2.Resource.Id.Should().Be(resourceId3);
            var scheduledActivities2 = resourceSchedule2.ScheduledActivities.ToList();
            scheduledActivities2.Count.Should().Be(1);

            scheduledActivities2[0].Id.Should().Be(activityId3);
            scheduledActivities2[0].HasNoCost.Should().BeFalse();
            scheduledActivities2[0].StartTime.Should().Be(0);
            scheduledActivities2[0].FinishTime.Should().Be(12);

            scheduledActivities2.Last().FinishTime.Should().Be(12);
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

            var graphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();

            var resource1 = new Resource<int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.Direct, 1.0, 0);
            var resource2 = new Resource<int>(resourceId2, string.Empty, false, false, InterActivityAllocationType.Indirect, 1.0, 0);
            var resource3 = new Resource<int>(resourceId3, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0);

            var activity1 = new DependentActivity<int, int>(activityId1, 5);
            activity1.TargetResources.Add(resourceId1);
            var activity2 = new DependentActivity<int, int>(activityId2, 3);
            activity2.TargetResources.Add(resourceId2);
            var activity3 = new DependentActivity<int, int>(activityId3, 12) { HasNoCost = true };
            activity3.TargetResources.Add(resourceId3);


            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, IDependentActivity<int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int>>(new[] { resource1, resource2, resource3 }));

            compilation.CompilationErrors.Should().BeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count().Should().Be(3);

            resourceSchedules[0].ActivityAllocation.Should().BeEquivalentTo(
                new bool[] {
                    true, true, true, true, true, false, false, false, false, false,
                    false, false,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.Should().Be(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.Should().Be(1);

            scheduledActivities0[0].Id.Should().Be(activityId1);
            scheduledActivities0[0].HasNoCost.Should().BeFalse();
            scheduledActivities0[0].StartTime.Should().Be(0);
            scheduledActivities0[0].FinishTime.Should().Be(5);

            scheduledActivities0.Last().FinishTime.Should().Be(5);


            resourceSchedules[1].ActivityAllocation.Should().BeEquivalentTo(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true,
                });

            var resourceSchedule1 = resourceSchedules[1];
            resourceSchedule1.Resource.Id.Should().Be(resourceId2);
            var scheduledActivities1 = resourceSchedule1.ScheduledActivities.ToList();
            scheduledActivities1.Count.Should().Be(1);

            scheduledActivities1[0].Id.Should().Be(activityId2);
            scheduledActivities1[0].HasNoCost.Should().BeFalse();
            scheduledActivities1[0].StartTime.Should().Be(0);
            scheduledActivities1[0].FinishTime.Should().Be(3);

            scheduledActivities1.Last().FinishTime.Should().Be(3);


            resourceSchedules[2].ActivityAllocation.Should().BeEquivalentTo(
                new bool[] {
                    false, false, false, false, false, false, false, false, false, false,
                    false, false,
                });

            var resourceSchedule2 = resourceSchedules[2];
            resourceSchedule2.Resource.Id.Should().Be(resourceId3);
            var scheduledActivities2 = resourceSchedule2.ScheduledActivities.ToList();
            scheduledActivities2.Count.Should().Be(1);

            scheduledActivities2[0].Id.Should().Be(activityId3);
            scheduledActivities2[0].HasNoCost.Should().BeTrue();
            scheduledActivities2[0].StartTime.Should().Be(0);
            scheduledActivities2[0].FinishTime.Should().Be(12);

            scheduledActivities2.Last().FinishTime.Should().Be(12);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithThreeDirectResources_WithUncostedFirstActivity_ThenFirstCostsRemoved()
        {
            int resourceId1 = 1;

            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;

            var graphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();

            var resource1 = new Resource<int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.Direct, 1.0, 0);

            var activity1 = new DependentActivity<int, int>(activityId1, 5) { HasNoCost = true };
            activity1.TargetResources.Add(resourceId1);
            var activity2 = new DependentActivity<int, int>(activityId2, 3);
            activity2.TargetResources.Add(resourceId1);
            var activity3 = new DependentActivity<int, int>(activityId3, 12);
            activity3.TargetResources.Add(resourceId1);

            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, IDependentActivity<int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int>>(new[] { resource1 }));

            compilation.CompilationErrors.Should().BeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count().Should().Be(1);

            resourceSchedules[0].ActivityAllocation.Should().BeEquivalentTo(
                new bool[] {
                    false, false, false, false, false, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.Should().Be(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.Should().Be(3);

            scheduledActivities0[0].Id.Should().Be(activityId1);
            scheduledActivities0[0].HasNoCost.Should().BeTrue();
            scheduledActivities0[0].StartTime.Should().Be(0);
            scheduledActivities0[0].FinishTime.Should().Be(5);

            scheduledActivities0[1].Id.Should().Be(activityId2);
            scheduledActivities0[1].HasNoCost.Should().BeFalse();
            scheduledActivities0[1].StartTime.Should().Be(5);
            scheduledActivities0[1].FinishTime.Should().Be(8);

            scheduledActivities0[2].Id.Should().Be(activityId3);
            scheduledActivities0[2].HasNoCost.Should().BeFalse();
            scheduledActivities0[2].StartTime.Should().Be(8);
            scheduledActivities0[2].FinishTime.Should().Be(20);

            scheduledActivities0.Last().FinishTime.Should().Be(20);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithThreeDirectResources_WithUncostedMiddleActivity_ThenMiddleCostsRemoved()
        {
            int resourceId1 = 1;

            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;

            var graphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();

            var resource1 = new Resource<int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.Direct, 1.0, 0);

            var activity1 = new DependentActivity<int, int>(activityId1, 5);
            activity1.TargetResources.Add(resourceId1);
            var activity2 = new DependentActivity<int, int>(activityId2, 3) { HasNoCost = true };
            activity2.TargetResources.Add(resourceId1);
            var activity3 = new DependentActivity<int, int>(activityId3, 12);
            activity3.TargetResources.Add(resourceId1);

            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, IDependentActivity<int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int>>(new[] { resource1 }));

            compilation.CompilationErrors.Should().BeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count().Should().Be(1);

            resourceSchedules[0].ActivityAllocation.Should().BeEquivalentTo(
                new bool[] {
                    true, true, true, true, true, false, false, false, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.Should().Be(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.Should().Be(3);

            scheduledActivities0[0].Id.Should().Be(activityId1);
            scheduledActivities0[0].HasNoCost.Should().BeFalse();
            scheduledActivities0[0].StartTime.Should().Be(0);
            scheduledActivities0[0].FinishTime.Should().Be(5);

            scheduledActivities0[1].Id.Should().Be(activityId2);
            scheduledActivities0[1].HasNoCost.Should().BeTrue();
            scheduledActivities0[1].StartTime.Should().Be(5);
            scheduledActivities0[1].FinishTime.Should().Be(8);

            scheduledActivities0[2].Id.Should().Be(activityId3);
            scheduledActivities0[2].HasNoCost.Should().BeFalse();
            scheduledActivities0[2].StartTime.Should().Be(8);
            scheduledActivities0[2].FinishTime.Should().Be(20);

            scheduledActivities0.Last().FinishTime.Should().Be(20);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithThreeDirectResources_WithUncostedLastActivity_ThenLastCostsRemoved()
        {
            int resourceId1 = 1;

            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;

            var graphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();

            var resource1 = new Resource<int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.Direct, 1.0, 0);

            var activity1 = new DependentActivity<int, int>(activityId1, 5);
            activity1.TargetResources.Add(resourceId1);
            var activity2 = new DependentActivity<int, int>(activityId2, 3);
            activity2.TargetResources.Add(resourceId1);
            var activity3 = new DependentActivity<int, int>(activityId3, 12) { HasNoCost = true };
            activity3.TargetResources.Add(resourceId1);

            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, IDependentActivity<int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int>>(new[] { resource1 }));

            compilation.CompilationErrors.Should().BeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count().Should().Be(1);

            resourceSchedules[0].ActivityAllocation.Should().BeEquivalentTo(
                new bool[] {
                    true, true, true, true, true, true, true, true, false, false,
                    false, false, false, false, false, false, false, false, false, false,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.Should().Be(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.Should().Be(3);

            scheduledActivities0[0].Id.Should().Be(activityId1);
            scheduledActivities0[0].HasNoCost.Should().BeFalse();
            scheduledActivities0[0].StartTime.Should().Be(0);
            scheduledActivities0[0].FinishTime.Should().Be(5);

            scheduledActivities0[1].Id.Should().Be(activityId2);
            scheduledActivities0[1].HasNoCost.Should().BeFalse();
            scheduledActivities0[1].StartTime.Should().Be(5);
            scheduledActivities0[1].FinishTime.Should().Be(8);

            scheduledActivities0[2].Id.Should().Be(activityId3);
            scheduledActivities0[2].HasNoCost.Should().BeTrue();
            scheduledActivities0[2].StartTime.Should().Be(8);
            scheduledActivities0[2].FinishTime.Should().Be(20);

            scheduledActivities0.Last().FinishTime.Should().Be(20);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithThreeIndirectResources_WithUncostedFirstActivity_ThenNoCostsRemoved()
        {
            int resourceId1 = 1;

            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;

            var graphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();

            var resource1 = new Resource<int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.Indirect, 1.0, 0);

            var activity1 = new DependentActivity<int, int>(activityId1, 5) { HasNoCost = true };
            activity1.TargetResources.Add(resourceId1);
            var activity2 = new DependentActivity<int, int>(activityId2, 3);
            activity2.TargetResources.Add(resourceId1);
            var activity3 = new DependentActivity<int, int>(activityId3, 12);
            activity3.TargetResources.Add(resourceId1);

            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, IDependentActivity<int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int>>(new[] { resource1 }));

            compilation.CompilationErrors.Should().BeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count().Should().Be(1);

            resourceSchedules[0].ActivityAllocation.Should().BeEquivalentTo(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.Should().Be(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.Should().Be(3);

            scheduledActivities0[0].Id.Should().Be(activityId1);
            scheduledActivities0[0].HasNoCost.Should().BeTrue();
            scheduledActivities0[0].StartTime.Should().Be(0);
            scheduledActivities0[0].FinishTime.Should().Be(5);

            scheduledActivities0[1].Id.Should().Be(activityId2);
            scheduledActivities0[1].HasNoCost.Should().BeFalse();
            scheduledActivities0[1].StartTime.Should().Be(5);
            scheduledActivities0[1].FinishTime.Should().Be(8);

            scheduledActivities0[2].Id.Should().Be(activityId3);
            scheduledActivities0[2].HasNoCost.Should().BeFalse();
            scheduledActivities0[2].StartTime.Should().Be(8);
            scheduledActivities0[2].FinishTime.Should().Be(20);

            scheduledActivities0.Last().FinishTime.Should().Be(20);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithThreeIndirectResources_WithUncostedMiddleActivity_ThenNoCostsRemoved()
        {
            int resourceId1 = 1;

            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;

            var graphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();

            var resource1 = new Resource<int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.Indirect, 1.0, 0);

            var activity1 = new DependentActivity<int, int>(activityId1, 5);
            activity1.TargetResources.Add(resourceId1);
            var activity2 = new DependentActivity<int, int>(activityId2, 3) { HasNoCost = true };
            activity2.TargetResources.Add(resourceId1);
            var activity3 = new DependentActivity<int, int>(activityId3, 12);
            activity3.TargetResources.Add(resourceId1);

            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, IDependentActivity<int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int>>(new[] { resource1 }));

            compilation.CompilationErrors.Should().BeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count().Should().Be(1);

            resourceSchedules[0].ActivityAllocation.Should().BeEquivalentTo(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.Should().Be(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.Should().Be(3);

            scheduledActivities0[0].Id.Should().Be(activityId1);
            scheduledActivities0[0].HasNoCost.Should().BeFalse();
            scheduledActivities0[0].StartTime.Should().Be(0);
            scheduledActivities0[0].FinishTime.Should().Be(5);

            scheduledActivities0[1].Id.Should().Be(activityId2);
            scheduledActivities0[1].HasNoCost.Should().BeTrue();
            scheduledActivities0[1].StartTime.Should().Be(5);
            scheduledActivities0[1].FinishTime.Should().Be(8);

            scheduledActivities0[2].Id.Should().Be(activityId3);
            scheduledActivities0[2].HasNoCost.Should().BeFalse();
            scheduledActivities0[2].StartTime.Should().Be(8);
            scheduledActivities0[2].FinishTime.Should().Be(20);

            scheduledActivities0.Last().FinishTime.Should().Be(20);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithThreeIndirectResources_WithUncostedLastActivity_ThenNoCostsRemoved()
        {
            int resourceId1 = 1;

            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;

            var graphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();

            var resource1 = new Resource<int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.Indirect, 1.0, 0);

            var activity1 = new DependentActivity<int, int>(activityId1, 5);
            activity1.TargetResources.Add(resourceId1);
            var activity2 = new DependentActivity<int, int>(activityId2, 3);
            activity2.TargetResources.Add(resourceId1);
            var activity3 = new DependentActivity<int, int>(activityId3, 12) { HasNoCost = true };
            activity3.TargetResources.Add(resourceId1);

            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, IDependentActivity<int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int>>(new[] { resource1 }));

            compilation.CompilationErrors.Should().BeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count().Should().Be(1);

            resourceSchedules[0].ActivityAllocation.Should().BeEquivalentTo(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.Should().Be(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.Should().Be(3);

            scheduledActivities0[0].Id.Should().Be(activityId1);
            scheduledActivities0[0].HasNoCost.Should().BeFalse();
            scheduledActivities0[0].StartTime.Should().Be(0);
            scheduledActivities0[0].FinishTime.Should().Be(5);

            scheduledActivities0[1].Id.Should().Be(activityId2);
            scheduledActivities0[1].HasNoCost.Should().BeFalse();
            scheduledActivities0[1].StartTime.Should().Be(5);
            scheduledActivities0[1].FinishTime.Should().Be(8);

            scheduledActivities0[2].Id.Should().Be(activityId3);
            scheduledActivities0[2].HasNoCost.Should().BeTrue();
            scheduledActivities0[2].StartTime.Should().Be(8);
            scheduledActivities0[2].FinishTime.Should().Be(20);

            scheduledActivities0.Last().FinishTime.Should().Be(20);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithThreeNoneResources_WithUncostedFirstActivity_ThenFirstCostsRemoved()
        {
            int resourceId1 = 1;

            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;

            var graphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();

            var resource1 = new Resource<int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0);

            var activity1 = new DependentActivity<int, int>(activityId1, 5) { HasNoCost = true };
            activity1.TargetResources.Add(resourceId1);
            var activity2 = new DependentActivity<int, int>(activityId2, 3);
            activity2.TargetResources.Add(resourceId1);
            var activity3 = new DependentActivity<int, int>(activityId3, 12);
            activity3.TargetResources.Add(resourceId1);

            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, IDependentActivity<int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int>>(new[] { resource1 }));

            compilation.CompilationErrors.Should().BeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count().Should().Be(1);

            resourceSchedules[0].ActivityAllocation.Should().BeEquivalentTo(
                new bool[] {
                    false, false, false, false, false, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.Should().Be(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.Should().Be(3);

            scheduledActivities0[0].Id.Should().Be(activityId1);
            scheduledActivities0[0].HasNoCost.Should().BeTrue();
            scheduledActivities0[0].StartTime.Should().Be(0);
            scheduledActivities0[0].FinishTime.Should().Be(5);

            scheduledActivities0[1].Id.Should().Be(activityId2);
            scheduledActivities0[1].HasNoCost.Should().BeFalse();
            scheduledActivities0[1].StartTime.Should().Be(5);
            scheduledActivities0[1].FinishTime.Should().Be(8);

            scheduledActivities0[2].Id.Should().Be(activityId3);
            scheduledActivities0[2].HasNoCost.Should().BeFalse();
            scheduledActivities0[2].StartTime.Should().Be(8);
            scheduledActivities0[2].FinishTime.Should().Be(20);

            scheduledActivities0.Last().FinishTime.Should().Be(20);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithThreeNoneResources_WithUncostedMiddleActivity_ThenMiddleCostsRemoved()
        {
            int resourceId1 = 1;

            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;

            var graphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();

            var resource1 = new Resource<int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0);

            var activity1 = new DependentActivity<int, int>(activityId1, 5);
            activity1.TargetResources.Add(resourceId1);
            var activity2 = new DependentActivity<int, int>(activityId2, 3) { HasNoCost = true };
            activity2.TargetResources.Add(resourceId1);
            var activity3 = new DependentActivity<int, int>(activityId3, 12);
            activity3.TargetResources.Add(resourceId1);

            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, IDependentActivity<int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int>>(new[] { resource1 }));

            compilation.CompilationErrors.Should().BeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count().Should().Be(1);

            resourceSchedules[0].ActivityAllocation.Should().BeEquivalentTo(
                new bool[] {
                    true, true, true, true, true, false, false, false, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.Should().Be(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.Should().Be(3);

            scheduledActivities0[0].Id.Should().Be(activityId1);
            scheduledActivities0[0].HasNoCost.Should().BeFalse();
            scheduledActivities0[0].StartTime.Should().Be(0);
            scheduledActivities0[0].FinishTime.Should().Be(5);

            scheduledActivities0[1].Id.Should().Be(activityId2);
            scheduledActivities0[1].HasNoCost.Should().BeTrue();
            scheduledActivities0[1].StartTime.Should().Be(5);
            scheduledActivities0[1].FinishTime.Should().Be(8);

            scheduledActivities0[2].Id.Should().Be(activityId3);
            scheduledActivities0[2].HasNoCost.Should().BeFalse();
            scheduledActivities0[2].StartTime.Should().Be(8);
            scheduledActivities0[2].FinishTime.Should().Be(20);

            scheduledActivities0.Last().FinishTime.Should().Be(20);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithThreeNoneResources_WithUncostedLastActivity_ThenLastCostsRemoved()
        {
            int resourceId1 = 1;

            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;

            var graphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();

            var resource1 = new Resource<int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0);

            var activity1 = new DependentActivity<int, int>(activityId1, 5);
            activity1.TargetResources.Add(resourceId1);
            var activity2 = new DependentActivity<int, int>(activityId2, 3);
            activity2.TargetResources.Add(resourceId1);
            var activity3 = new DependentActivity<int, int>(activityId3, 12) { HasNoCost = true };
            activity3.TargetResources.Add(resourceId1);

            graphCompiler.AddActivity(activity1);
            graphCompiler.AddActivity(activity2);
            graphCompiler.AddActivity(activity3);

            IGraphCompilation<int, int, IDependentActivity<int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int>>(new[] { resource1 }));

            compilation.CompilationErrors.Should().BeEmpty();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count().Should().Be(1);

            resourceSchedules[0].ActivityAllocation.Should().BeEquivalentTo(
                new bool[] {
                    true, true, true, true, true, true, true, true, false, false,
                    false, false, false, false, false, false, false, false, false, false,
                });

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.Should().Be(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.Should().Be(3);

            scheduledActivities0[0].Id.Should().Be(activityId1);
            scheduledActivities0[0].HasNoCost.Should().BeFalse();
            scheduledActivities0[0].StartTime.Should().Be(0);
            scheduledActivities0[0].FinishTime.Should().Be(5);

            scheduledActivities0[1].Id.Should().Be(activityId2);
            scheduledActivities0[1].HasNoCost.Should().BeFalse();
            scheduledActivities0[1].StartTime.Should().Be(5);
            scheduledActivities0[1].FinishTime.Should().Be(8);

            scheduledActivities0[2].Id.Should().Be(activityId3);
            scheduledActivities0[2].HasNoCost.Should().BeTrue();
            scheduledActivities0[2].StartTime.Should().Be(8);
            scheduledActivities0[2].FinishTime.Should().Be(20);

            scheduledActivities0.Last().FinishTime.Should().Be(20);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCyclomaticComplexityWithNoNodes_ThenFindsZero()
        {
            var graphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();
            graphCompiler.Compile();
            graphCompiler.CyclomaticComplexity.Should().Be(0);
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
            var graphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId1, 6));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId2, 7));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId3, 8));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId4, 11, new HashSet<int> { activityId2 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId5, 8, new HashSet<int> { activityId1, activityId2, activityId3 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId6, 7, new HashSet<int> { activityId3 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId7, 4, new HashSet<int> { activityId4 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId8, 4, new HashSet<int> { activityId4, activityId6 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId9, 10, new HashSet<int> { activityId5 }));

            graphCompiler.Compile();

            graphCompiler.CyclomaticComplexity.Should().Be(6);
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
            var graphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId1, 6));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId2, 7));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId3, 8));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId4, 11, new HashSet<int> { activityId1 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId5, 8, new HashSet<int> { activityId2 }));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId6, 7, new HashSet<int> { activityId3 }));

            graphCompiler.Compile();

            graphCompiler.CyclomaticComplexity.Should().Be(3);
        }

        [Fact]
        public void VertexGraphCompiler_GivenCyclomaticComplexityWithTwoLoneNodes_ThenAsExpected()
        {
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            var graphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId1, 6));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId2, 7));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId3, 8));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId4, 11, new HashSet<int> { activityId1 }));

            graphCompiler.Compile();

            graphCompiler.CyclomaticComplexity.Should().Be(3);
        }
    }
}
