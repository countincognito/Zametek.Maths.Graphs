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
            graphCompiler.AddActivity(new DependentActivity<int, int>(4, 11, new HashSet<int>(new[] { 2 })) { MinimumEarliestStartTime = 7, MaximumLatestFinishTime = 17 });
            graphCompiler.AddActivity(new DependentActivity<int, int>(5, 8, new HashSet<int>(new[] { 1, 2, 3 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(6, 7, new HashSet<int>(new[] { 3 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(7, 4, new HashSet<int>(new[] { 4 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(8, 4, new HashSet<int>(new[] { 4, 6 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(9, 10, new HashSet<int>(new[] { 5 })));

            IGraphCompilation<int, int, IDependentActivity<int, int>> compilation = graphCompiler.Compile();

            compilation.ResourceSchedules.Should().BeEmpty();
            compilation.Errors.Should().NotBeNull();
            compilation.Errors.AllResourcesExplicitTargetsButNotAllActivitiesTargeted.Should().BeFalse();
            compilation.Errors.InvalidConstraints.Should().BeEquivalentTo(new List<int>(new int[] { 4 }));
            compilation.Errors.MissingDependencies.Should().BeEmpty();
            compilation.Errors.CircularDependencies.Should().BeEmpty();
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithCircularDependencies_ThenFindsCircularDependencies()
        {
            var graphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();
            graphCompiler.AddActivity(new DependentActivity<int, int>(1, 10));
            graphCompiler.AddActivity(new DependentActivity<int, int>(2, 10, new HashSet<int>(new[] { 7 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(3, 10));
            graphCompiler.AddActivity(new DependentActivity<int, int>(4, 10, new HashSet<int>(new[] { 2 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(5, 10, new HashSet<int>(new[] { 1, 2, 3, 8 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(6, 10, new HashSet<int>(new[] { 3 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(7, 10, new HashSet<int>(new[] { 4 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(8, 10, new HashSet<int>(new[] { 9, 6 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(9, 10, new HashSet<int>(new[] { 5 })));

            IGraphCompilation<int, int, IDependentActivity<int, int>> compilation = graphCompiler.Compile();

            compilation.ResourceSchedules.Should().BeEmpty();
            compilation.Errors.Should().NotBeNull();
            compilation.Errors.AllResourcesExplicitTargetsButNotAllActivitiesTargeted.Should().BeFalse();
            compilation.Errors.InvalidConstraints.Should().BeEmpty();
            compilation.Errors.MissingDependencies.Should().BeEmpty();
            var circularDependencies = compilation.Errors.CircularDependencies.ToList();
            circularDependencies.Count().Should().Be(2);
            circularDependencies[0].Dependencies.Should().BeEquivalentTo(new List<int>(new int[] { 2, 4, 7 }));
            circularDependencies[1].Dependencies.Should().BeEquivalentTo(new List<int>(new int[] { 5, 8, 9 }));
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithMissingDependencies_ThenFindsMissingDependencies()
        {
            var graphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();
            graphCompiler.AddActivity(new DependentActivity<int, int>(1, 10));
            graphCompiler.AddActivity(new DependentActivity<int, int>(2, 10, new HashSet<int>(new[] { 7 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(3, 10, new HashSet<int>(new[] { 21 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(4, 10, new HashSet<int>(new[] { 2 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(5, 10, new HashSet<int>(new[] { 1, 2, 3, 8 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(6, 10, new HashSet<int>(new[] { 3 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(7, 10, new HashSet<int>(new[] { 22 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(8, 10, new HashSet<int>(new[] { 9, 6 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(9, 10));

            IGraphCompilation<int, int, IDependentActivity<int, int>> compilation = graphCompiler.Compile();

            compilation.ResourceSchedules.Should().BeEmpty();
            compilation.Errors.Should().NotBeNull();
            compilation.Errors.AllResourcesExplicitTargetsButNotAllActivitiesTargeted.Should().BeFalse();
            compilation.Errors.InvalidConstraints.Should().BeEmpty();
            compilation.Errors.MissingDependencies.Should().BeEquivalentTo(new List<int>(new int[] { 21, 22 }));
            compilation.Errors.CircularDependencies.Should().BeEmpty();
        }

        [Fact]
        public void VertexGraphCompiler_GivenCompileWithInvalidConstraintsAndCircularAndMissingDependencies_ThenFindsInvalidConstraintsAndCircularAndMissingDependencies()
        {
            var graphCompiler = new VertexGraphCompiler<int, int, IDependentActivity<int, int>>();
            graphCompiler.AddActivity(new DependentActivity<int, int>(1, 10));
            graphCompiler.AddActivity(new DependentActivity<int, int>(2, 10, new HashSet<int>(new[] { 7 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(3, 10, new HashSet<int>(new[] { 21 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(4, 10, new HashSet<int>(new[] { 2 })) { MinimumEarliestStartTime = 7, MaximumLatestFinishTime = 16 });
            graphCompiler.AddActivity(new DependentActivity<int, int>(5, 10, new HashSet<int>(new[] { 1, 2, 3, 8 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(6, 10, new HashSet<int>(new[] { 3 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(7, 10, new HashSet<int>(new[] { 4, 22 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(8, 10, new HashSet<int>(new[] { 9, 6 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(9, 10, new HashSet<int>(new[] { 5 })));

            IGraphCompilation<int, int, IDependentActivity<int, int>> compilation = graphCompiler.Compile();

            compilation.ResourceSchedules.Should().BeEmpty();
            compilation.Errors.Should().NotBeNull();
            compilation.Errors.AllResourcesExplicitTargetsButNotAllActivitiesTargeted.Should().BeFalse();
            compilation.Errors.InvalidConstraints.Should().BeEquivalentTo(new List<int>(new int[] { 4 }));
            var circularDependencies = compilation.Errors.CircularDependencies.ToList();
            circularDependencies.Count.Should().Be(2);
            circularDependencies[0].Dependencies.Should().BeEquivalentTo(new List<int>(new int[] { 2, 4, 7 }));
            circularDependencies[1].Dependencies.Should().BeEquivalentTo(new List<int>(new int[] { 5, 8, 9 }));
            compilation.Errors.MissingDependencies.Should().BeEquivalentTo(new List<int>(new int[] { 21, 22 }));
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
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId4, 11, new HashSet<int>(new[] { 2 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId5, 8, new HashSet<int>(new[] { 1, 2, 3 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId6, 7, new HashSet<int>(new[] { 3 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId7, 4, new HashSet<int>(new[] { 4 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId8, 4, new HashSet<int>(new[] { 4, 6 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId9, 10, new HashSet<int>(new[] { 5 })));

            IGraphCompilation<int, int, IDependentActivity<int, int>> compilation = graphCompiler.Compile();

            compilation.Errors.Should().BeNull();

            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.Should().Be(3);

            var scheduledActivities0 = resourceSchedules[0].ScheduledActivities.ToList();
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


            var scheduledActivities1 = resourceSchedules[1].ScheduledActivities.ToList();
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


            var scheduledActivities2 = resourceSchedules[2].ScheduledActivities.ToList();
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
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId4, 11, new HashSet<int>(new[] { 2 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId5, 8, new HashSet<int>(new[] { 1, 2, 3 })) { MinimumFreeSlack = 15 });
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId6, 7, new HashSet<int>(new[] { 3 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId7, 4, new HashSet<int>(new[] { 4 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId8, 4, new HashSet<int>(new[] { 4, 6 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId9, 10, new HashSet<int>(new[] { 5 })));

            IGraphCompilation<int, int, IDependentActivity<int, int>> compilation = graphCompiler.Compile();

            compilation.Errors.Should().BeNull();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count.Should().Be(3);

            var scheduledActivities0 = resourceSchedules[0].ScheduledActivities.ToList();
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


            var scheduledActivities1 = resourceSchedules[1].ScheduledActivities.ToList();
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


            var scheduledActivities2 = resourceSchedules[2].ScheduledActivities.ToList();
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
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId4, 11, new HashSet<int>(new[] { 2 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId5, 8, new HashSet<int>(new[] { 1, 2, 3 })) { HasNoCost = true });
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId6, 7, new HashSet<int>(new[] { 3 })) { HasNoCost = true });
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId7, 4, new HashSet<int>(new[] { 4 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId8, 4, new HashSet<int>(new[] { 4, 6 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId9, 10, new HashSet<int>(new[] { 5 })));

            int resourceId1 = 1;
            int resourceId2 = resourceId1 + 1;
            IGraphCompilation<int, int, IDependentActivity<int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int>>(new[]
                {
                    new Resource<int>(resourceId1, string.Empty, false, InterActivityAllocationType.None, 1.0, 0),
                    new Resource<int>(resourceId2, string.Empty, false, InterActivityAllocationType.Direct, 1.0, 0),
                }));

            compilation.Errors.Should().BeNull();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count().Should().Be(2);

            resourceSchedules[0].ActivityAllocation.Should().BeEquivalentTo(
                new bool[] {
                    false, false, false, false, false, false, false, false, true, true,
                    true, true, true, true, true, true, true, true, true, false,
                    false, false, false, false, false, false, true, true, true, true,
                    true, true, true, true,
                });

            var scheduledActivities0 = resourceSchedules[0].ScheduledActivities.ToList();
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

            var scheduledActivities1 = resourceSchedules[1].ScheduledActivities.ToList();
            scheduledActivities1.Count.Should().Be(4);

            scheduledActivities1[0].Id.Should().Be(activityId2);
            scheduledActivities0[0].HasNoCost.Should().BeTrue();
            scheduledActivities1[0].StartTime.Should().Be(0);
            scheduledActivities1[0].FinishTime.Should().Be(7);

            scheduledActivities1[1].Id.Should().Be(activityId1);
            scheduledActivities0[1].HasNoCost.Should().BeFalse();
            scheduledActivities1[1].StartTime.Should().Be(7);
            scheduledActivities1[1].FinishTime.Should().Be(13);

            scheduledActivities1[2].Id.Should().Be(activityId5);
            scheduledActivities0[2].HasNoCost.Should().BeTrue();
            scheduledActivities1[2].StartTime.Should().Be(13);
            scheduledActivities1[2].FinishTime.Should().Be(21);

            scheduledActivities1[3].Id.Should().Be(activityId9);
            scheduledActivities0[3].HasNoCost.Should().BeFalse();
            scheduledActivities1[3].StartTime.Should().Be(21);
            scheduledActivities1[3].FinishTime.Should().Be(31);

            scheduledActivities1.Last().FinishTime.Should().Be(31);



            graphBuilder.Activity(activityId1).EarliestStartTime.Should().Be(7);
            graphBuilder.Activity(activityId1).EarliestFinishTime.Should().Be(13);
            graphBuilder.Activity(activityId1).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId1).TotalSlack.Should().Be(3);
            graphBuilder.Activity(activityId1).LatestStartTime.Should().Be(10);
            graphBuilder.Activity(activityId1).LatestFinishTime.Should().Be(16);
            graphBuilder.Activity(activityId1).ResourceDependencies.Should().BeEquivalentTo(new List<int>(new int[] { 2 }));
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
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId4, 11, new HashSet<int>(new[] { 2 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId5, 8, new HashSet<int>(new[] { 1, 2, 3 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId6, 7, new HashSet<int>(new[] { 3 })) { HasNoCost = true });
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId7, 4, new HashSet<int>(new[] { 4 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId8, 4, new HashSet<int>(new[] { 4, 6 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId9, 10, new HashSet<int>(new[] { 5 })));

            int resourceId1 = 1;
            int resourceId2 = resourceId1 + 1;
            IGraphCompilation<int, int, IDependentActivity<int, int>> compilation = graphCompiler.Compile(
                new List<IResource<int>>(new[]
                {
                    new Resource<int>(resourceId1, string.Empty, false, InterActivityAllocationType.Indirect, 1.0, 0),
                    new Resource<int>(resourceId2, string.Empty, false, InterActivityAllocationType.Indirect, 1.0, 0),
                }));

            compilation.Errors.Should().BeNull();
            var resourceSchedules = compilation.ResourceSchedules.ToList();
            resourceSchedules.Count().Should().Be(2);

            resourceSchedules[0].ActivityAllocation.Should().BeEquivalentTo(
                new bool[] {
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true, true, true, true, true, true, true,
                    true, true, true, true,
                });

            var scheduledActivities0 = resourceSchedules[0].ScheduledActivities.ToList();
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

            var scheduledActivities1 = resourceSchedules[1].ScheduledActivities.ToList();
            scheduledActivities1.Count.Should().Be(4);

            scheduledActivities1[0].Id.Should().Be(activityId2);
            scheduledActivities0[0].HasNoCost.Should().BeTrue();
            scheduledActivities1[0].StartTime.Should().Be(0);
            scheduledActivities1[0].FinishTime.Should().Be(7);

            scheduledActivities1[1].Id.Should().Be(activityId1);
            scheduledActivities0[1].HasNoCost.Should().BeFalse();
            scheduledActivities1[1].StartTime.Should().Be(7);
            scheduledActivities1[1].FinishTime.Should().Be(13);

            scheduledActivities1[2].Id.Should().Be(activityId5);
            scheduledActivities0[2].HasNoCost.Should().BeTrue();
            scheduledActivities1[2].StartTime.Should().Be(13);
            scheduledActivities1[2].FinishTime.Should().Be(21);

            scheduledActivities1[3].Id.Should().Be(activityId9);
            scheduledActivities0[3].HasNoCost.Should().BeFalse();
            scheduledActivities1[3].StartTime.Should().Be(21);
            scheduledActivities1[3].FinishTime.Should().Be(31);

            scheduledActivities1.Last().FinishTime.Should().Be(31);



            graphBuilder.Activity(activityId1).EarliestStartTime.Should().Be(7);
            graphBuilder.Activity(activityId1).EarliestFinishTime.Should().Be(13);
            graphBuilder.Activity(activityId1).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId1).TotalSlack.Should().Be(3);
            graphBuilder.Activity(activityId1).LatestStartTime.Should().Be(10);
            graphBuilder.Activity(activityId1).LatestFinishTime.Should().Be(16);
            graphBuilder.Activity(activityId1).ResourceDependencies.Should().BeEquivalentTo(new List<int>(new int[] { 2 }));
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
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId4, 11, new HashSet<int>(new[] { 2 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId5, 8, new HashSet<int>(new[] { 1, 2, 3 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId6, 7, new HashSet<int>(new[] { 3 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId7, 4, new HashSet<int>(new[] { 4 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId8, 4, new HashSet<int>(new[] { 4, 6 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId9, 10, new HashSet<int>(new[] { 5 })));

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
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId4, 11, new HashSet<int>(new[] { 1 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId5, 8, new HashSet<int>(new[] { 2 })));
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId6, 7, new HashSet<int>(new[] { 3 })));

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
            graphCompiler.AddActivity(new DependentActivity<int, int>(activityId4, 11, new HashSet<int>(new[] { 1 })));

            graphCompiler.Compile();

            graphCompiler.CyclomaticComplexity.Should().Be(3);
        }
    }
}
