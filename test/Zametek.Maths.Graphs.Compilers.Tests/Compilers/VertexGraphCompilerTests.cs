using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class VertexGraphCompilerTests
    {
        [Fact]
        public void VertexGraphCompiler_Contructor_NoException()
        {
            var graphCompiler = VertexGraphCompiler<int, IDependentActivity<int>>.Create();
            var graphBuilder = graphCompiler.Builder;
            Assert.IsFalse(graphBuilder.EdgeIds.Any());
            Assert.IsFalse(graphBuilder.NodeIds.Any());
            Assert.IsTrue(graphBuilder.AllDependenciesSatisfied);
            Assert.IsFalse(graphBuilder.StartNodes.Any());
            Assert.IsFalse(graphBuilder.EndNodes.Any());
        }

        [Fact]
        public void VertexGraphCompiler_SingleActivityNoDependencies_NoStartOrEndNodes()
        {
            int activityId = 0;
            int activityId1 = activityId + 1;
            var graphCompiler = VertexGraphCompiler<int, IDependentActivity<int>>.Create();
            var graphBuilder = graphCompiler.Builder;

            var activity = new DependentActivity<int>(activityId1, 0);
            bool result = graphCompiler.AddActivity(activity);
            Assert.IsTrue(result);

            Assert.IsFalse(graphBuilder.EdgeIds.Any());
            Assert.AreEqual(1, graphBuilder.NodeIds.Count());
            Assert.IsTrue(graphBuilder.AllDependenciesSatisfied);

            Assert.IsFalse(graphBuilder.StartNodes.Any());
            Assert.IsFalse(graphBuilder.EndNodes.Any());

            Assert.AreEqual(activityId1, graphBuilder.Node(activityId1).Id);
            Assert.AreEqual(NodeType.Isolated, graphBuilder.Node(activityId1).NodeType);
            Assert.AreEqual(activityId1, graphBuilder.Activity(activityId1).Id);
            Assert.AreEqual(1, graphBuilder.Activities.Count());
            Assert.IsFalse(graphBuilder.Edges.Any());
        }

        [Fact]
        public void VertexGraphCompiler_CompileWithCircularDependencies_FindsCircularDependencies()
        {
            var graphCompiler = VertexGraphCompiler<int, IDependentActivity<int>>.Create();
            graphCompiler.AddActivity(new DependentActivity<int>(1, 10));
            graphCompiler.AddActivity(new DependentActivity<int>(2, 10, new HashSet<int>(new[] { 7 })));
            graphCompiler.AddActivity(new DependentActivity<int>(3, 10));
            graphCompiler.AddActivity(new DependentActivity<int>(4, 10, new HashSet<int>(new[] { 2 })));
            graphCompiler.AddActivity(new DependentActivity<int>(5, 10, new HashSet<int>(new[] { 1, 2, 3, 8 })));
            graphCompiler.AddActivity(new DependentActivity<int>(6, 10, new HashSet<int>(new[] { 3 })));
            graphCompiler.AddActivity(new DependentActivity<int>(7, 10, new HashSet<int>(new[] { 4 })));
            graphCompiler.AddActivity(new DependentActivity<int>(8, 10, new HashSet<int>(new[] { 9, 6 })));
            graphCompiler.AddActivity(new DependentActivity<int>(9, 10, new HashSet<int>(new[] { 5 })));

            GraphCompilation<int, IDependentActivity<int>> complication = graphCompiler.Compile();

            Assert.AreEqual(0, complication.ResourceSchedules.Count);
            Assert.AreEqual(0, complication.MissingDependencies.Count);
            Assert.AreEqual(2, complication.CircularDependencies.Count);
            CollectionAssert.AreEquivalent(
                new List<int>(new int[] { 2, 4, 7 }),
                complication.CircularDependencies[0].Dependencies.ToList());
            CollectionAssert.AreEquivalent(
                new List<int>(new int[] { 5, 8, 9 }),
                complication.CircularDependencies[1].Dependencies.ToList());
        }

        [Fact]
        public void VertexGraphCompiler_CompileWithMissingDependencies_FindsMissingDependencies()
        {
            var graphCompiler = VertexGraphCompiler<int, IDependentActivity<int>>.Create();
            graphCompiler.AddActivity(new DependentActivity<int>(1, 10));
            graphCompiler.AddActivity(new DependentActivity<int>(2, 10, new HashSet<int>(new[] { 7 })));
            graphCompiler.AddActivity(new DependentActivity<int>(3, 10, new HashSet<int>(new[] { 21 })));
            graphCompiler.AddActivity(new DependentActivity<int>(4, 10, new HashSet<int>(new[] { 2 })));
            graphCompiler.AddActivity(new DependentActivity<int>(5, 10, new HashSet<int>(new[] { 1, 2, 3, 8 })));
            graphCompiler.AddActivity(new DependentActivity<int>(6, 10, new HashSet<int>(new[] { 3 })));
            graphCompiler.AddActivity(new DependentActivity<int>(7, 10, new HashSet<int>(new[] { 22 })));
            graphCompiler.AddActivity(new DependentActivity<int>(8, 10, new HashSet<int>(new[] { 9, 6 })));
            graphCompiler.AddActivity(new DependentActivity<int>(9, 10));

            GraphCompilation<int, IDependentActivity<int>> complication = graphCompiler.Compile();

            Assert.AreEqual(0, complication.ResourceSchedules.Count);
            Assert.AreEqual(0, complication.CircularDependencies.Count);
            CollectionAssert.AreEquivalent(
                new List<int>(new int[] { 21, 22 }),
                complication.MissingDependencies.ToList());
        }

        [Fact]
        public void VertexGraphCompiler_CompileWithCircularAndMissingDependencies_FindsCircularAndMissingDependencies()
        {
            var graphCompiler = VertexGraphCompiler<int, IDependentActivity<int>>.Create();
            graphCompiler.AddActivity(new DependentActivity<int>(1, 10));
            graphCompiler.AddActivity(new DependentActivity<int>(2, 10, new HashSet<int>(new[] { 7 })));
            graphCompiler.AddActivity(new DependentActivity<int>(3, 10, new HashSet<int>(new[] { 21 })));
            graphCompiler.AddActivity(new DependentActivity<int>(4, 10, new HashSet<int>(new[] { 2 })));
            graphCompiler.AddActivity(new DependentActivity<int>(5, 10, new HashSet<int>(new[] { 1, 2, 3, 8 })));
            graphCompiler.AddActivity(new DependentActivity<int>(6, 10, new HashSet<int>(new[] { 3 })));
            graphCompiler.AddActivity(new DependentActivity<int>(7, 10, new HashSet<int>(new[] { 4, 22 })));
            graphCompiler.AddActivity(new DependentActivity<int>(8, 10, new HashSet<int>(new[] { 9, 6 })));
            graphCompiler.AddActivity(new DependentActivity<int>(9, 10, new HashSet<int>(new[] { 5 })));

            GraphCompilation<int, IDependentActivity<int>> complication = graphCompiler.Compile();

            Assert.AreEqual(0, complication.ResourceSchedules.Count);
            Assert.AreEqual(2, complication.CircularDependencies.Count);
            CollectionAssert.AreEquivalent(
                new List<int>(new int[] { 2, 4, 7 }),
                complication.CircularDependencies[0].Dependencies.ToList());
            CollectionAssert.AreEquivalent(
                new List<int>(new int[] { 5, 8, 9 }),
                complication.CircularDependencies[1].Dependencies.ToList());
            CollectionAssert.AreEquivalent(
                new List<int>(new int[] { 21, 22 }),
                complication.MissingDependencies.ToList());
        }

        [Fact]
        public void VertexGraphCompiler_CompileWithUnlimitedResources_ResourceSchedulesCorrectOrder()
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
            var graphCompiler = VertexGraphCompiler<int, IDependentActivity<int>>.Create();
            var graphBuilder = graphCompiler.Builder;
            graphCompiler.AddActivity(new DependentActivity<int>(activityId1, 6));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId2, 7));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId3, 8));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId4, 11, new HashSet<int>(new[] { 2 })));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId5, 8, new HashSet<int>(new[] { 1, 2, 3 })));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId6, 7, new HashSet<int>(new[] { 3 })));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId7, 4, new HashSet<int>(new[] { 4 })));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId8, 4, new HashSet<int>(new[] { 4, 6 })));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId9, 10, new HashSet<int>(new[] { 5 })));

            GraphCompilation<int, IDependentActivity<int>> complication = graphCompiler.Compile();

            Assert.AreEqual(0, complication.MissingDependencies.Count);
            Assert.AreEqual(0, complication.CircularDependencies.Count);
            Assert.AreEqual(3, complication.ResourceSchedules.Count);

            Assert.AreEqual(3, complication.ResourceSchedules[0].ScheduledActivities.Count());

            Assert.AreEqual(activityId3, complication.ResourceSchedules[0].ScheduledActivities[0].Id);
            Assert.AreEqual(0, complication.ResourceSchedules[0].ScheduledActivities[0].StartTime);
            Assert.AreEqual(8, complication.ResourceSchedules[0].ScheduledActivities[0].FinishTime);

            Assert.AreEqual(activityId5, complication.ResourceSchedules[0].ScheduledActivities[1].Id);
            Assert.AreEqual(8, complication.ResourceSchedules[0].ScheduledActivities[1].StartTime);
            Assert.AreEqual(16, complication.ResourceSchedules[0].ScheduledActivities[1].FinishTime);

            Assert.AreEqual(activityId9, complication.ResourceSchedules[0].ScheduledActivities[2].Id);
            Assert.AreEqual(16, complication.ResourceSchedules[0].ScheduledActivities[2].StartTime);
            Assert.AreEqual(26, complication.ResourceSchedules[0].ScheduledActivities[2].FinishTime);

            Assert.AreEqual(26, complication.ResourceSchedules[0].ScheduledActivities.Last().FinishTime);



            Assert.AreEqual(3, complication.ResourceSchedules[1].ScheduledActivities.Count());

            Assert.AreEqual(activityId2, complication.ResourceSchedules[1].ScheduledActivities[0].Id);
            Assert.AreEqual(0, complication.ResourceSchedules[1].ScheduledActivities[0].StartTime);
            Assert.AreEqual(7, complication.ResourceSchedules[1].ScheduledActivities[0].FinishTime);

            Assert.AreEqual(activityId4, complication.ResourceSchedules[1].ScheduledActivities[1].Id);
            Assert.AreEqual(7, complication.ResourceSchedules[1].ScheduledActivities[1].StartTime);
            Assert.AreEqual(18, complication.ResourceSchedules[1].ScheduledActivities[1].FinishTime);

            Assert.AreEqual(activityId7, complication.ResourceSchedules[1].ScheduledActivities[2].Id);
            Assert.AreEqual(18, complication.ResourceSchedules[1].ScheduledActivities[2].StartTime);
            Assert.AreEqual(22, complication.ResourceSchedules[1].ScheduledActivities[2].FinishTime);

            Assert.AreEqual(22, complication.ResourceSchedules[1].ScheduledActivities.Last().FinishTime);



            Assert.AreEqual(3, complication.ResourceSchedules[2].ScheduledActivities.Count());

            Assert.AreEqual(activityId1, complication.ResourceSchedules[2].ScheduledActivities[0].Id);
            Assert.AreEqual(0, complication.ResourceSchedules[2].ScheduledActivities[0].StartTime);
            Assert.AreEqual(6, complication.ResourceSchedules[2].ScheduledActivities[0].FinishTime);

            Assert.AreEqual(activityId6, complication.ResourceSchedules[2].ScheduledActivities[1].Id);
            Assert.AreEqual(8, complication.ResourceSchedules[2].ScheduledActivities[1].StartTime);
            Assert.AreEqual(15, complication.ResourceSchedules[2].ScheduledActivities[1].FinishTime);

            Assert.AreEqual(activityId8, complication.ResourceSchedules[2].ScheduledActivities[2].Id);
            Assert.AreEqual(18, complication.ResourceSchedules[2].ScheduledActivities[2].StartTime);
            Assert.AreEqual(22, complication.ResourceSchedules[2].ScheduledActivities[2].FinishTime);

            Assert.AreEqual(22, complication.ResourceSchedules[2].ScheduledActivities.Last().FinishTime);



            Assert.AreEqual(0, graphBuilder.Activity(activityId1).EarliestStartTime);
            Assert.AreEqual(6, graphBuilder.Activity(activityId1).EarliestFinishTime);
            Assert.AreEqual(2, graphBuilder.Activity(activityId1).FreeSlack);
            Assert.AreEqual(2, graphBuilder.Activity(activityId1).TotalSlack);
            Assert.AreEqual(2, graphBuilder.Activity(activityId1).LatestStartTime);
            Assert.AreEqual(8, graphBuilder.Activity(activityId1).LatestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId1).ResourceDependencies.Count());

            Assert.AreEqual(0, graphBuilder.Activity(activityId2).EarliestStartTime);
            Assert.AreEqual(7, graphBuilder.Activity(activityId2).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId2).FreeSlack);
            Assert.AreEqual(1, graphBuilder.Activity(activityId2).TotalSlack);
            Assert.AreEqual(1, graphBuilder.Activity(activityId2).LatestStartTime);
            Assert.AreEqual(8, graphBuilder.Activity(activityId2).LatestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId2).ResourceDependencies.Count());

            Assert.AreEqual(0, graphBuilder.Activity(activityId3).EarliestStartTime);
            Assert.AreEqual(8, graphBuilder.Activity(activityId3).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId3).FreeSlack);
            Assert.AreEqual(0, graphBuilder.Activity(activityId3).TotalSlack);
            Assert.AreEqual(0, graphBuilder.Activity(activityId3).LatestStartTime);
            Assert.AreEqual(8, graphBuilder.Activity(activityId3).LatestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId3).ResourceDependencies.Count());

            Assert.AreEqual(7, graphBuilder.Activity(activityId4).EarliestStartTime);
            Assert.AreEqual(18, graphBuilder.Activity(activityId4).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId4).FreeSlack);
            Assert.AreEqual(4, graphBuilder.Activity(activityId4).TotalSlack);
            Assert.AreEqual(11, graphBuilder.Activity(activityId4).LatestStartTime);
            Assert.AreEqual(22, graphBuilder.Activity(activityId4).LatestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId4).ResourceDependencies.Count());

            Assert.AreEqual(8, graphBuilder.Activity(activityId5).EarliestStartTime);
            Assert.AreEqual(16, graphBuilder.Activity(activityId5).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId5).FreeSlack);
            Assert.AreEqual(0, graphBuilder.Activity(activityId5).TotalSlack);
            Assert.AreEqual(8, graphBuilder.Activity(activityId5).LatestStartTime);
            Assert.AreEqual(16, graphBuilder.Activity(activityId5).LatestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId5).ResourceDependencies.Count());

            Assert.AreEqual(8, graphBuilder.Activity(activityId6).EarliestStartTime);
            Assert.AreEqual(15, graphBuilder.Activity(activityId6).EarliestFinishTime);
            Assert.AreEqual(3, graphBuilder.Activity(activityId6).FreeSlack);
            Assert.AreEqual(7, graphBuilder.Activity(activityId6).TotalSlack);
            Assert.AreEqual(15, graphBuilder.Activity(activityId6).LatestStartTime);
            Assert.AreEqual(22, graphBuilder.Activity(activityId6).LatestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId6).ResourceDependencies.Count());

            Assert.AreEqual(18, graphBuilder.Activity(activityId7).EarliestStartTime);
            Assert.AreEqual(22, graphBuilder.Activity(activityId7).EarliestFinishTime);
            Assert.AreEqual(4, graphBuilder.Activity(activityId7).FreeSlack);
            Assert.AreEqual(4, graphBuilder.Activity(activityId7).TotalSlack);
            Assert.AreEqual(22, graphBuilder.Activity(activityId7).LatestStartTime);
            Assert.AreEqual(26, graphBuilder.Activity(activityId7).LatestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId7).ResourceDependencies.Count());

            Assert.AreEqual(18, graphBuilder.Activity(activityId8).EarliestStartTime);
            Assert.AreEqual(22, graphBuilder.Activity(activityId8).EarliestFinishTime);
            Assert.AreEqual(4, graphBuilder.Activity(activityId8).FreeSlack);
            Assert.AreEqual(4, graphBuilder.Activity(activityId8).TotalSlack);
            Assert.AreEqual(22, graphBuilder.Activity(activityId8).LatestStartTime);
            Assert.AreEqual(26, graphBuilder.Activity(activityId8).LatestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId8).ResourceDependencies.Count());

            Assert.AreEqual(16, graphBuilder.Activity(activityId9).EarliestStartTime);
            Assert.AreEqual(26, graphBuilder.Activity(activityId9).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId9).FreeSlack);
            Assert.AreEqual(0, graphBuilder.Activity(activityId9).TotalSlack);
            Assert.AreEqual(16, graphBuilder.Activity(activityId9).LatestStartTime);
            Assert.AreEqual(26, graphBuilder.Activity(activityId9).LatestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId9).ResourceDependencies.Count());
        }

        [Fact]
        public void VertexGraphCompiler_CompileWithFreeSlackUnlimitedResources_ResourceSchedulesCorrectOrder()
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
            var graphCompiler = VertexGraphCompiler<int, IDependentActivity<int>>.Create();
            var graphBuilder = graphCompiler.Builder;
            graphCompiler.AddActivity(new DependentActivity<int>(activityId1, 6));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId2, 7));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId3, 8));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId4, 11, new HashSet<int>(new[] { 2 })));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId5, 8, new HashSet<int>(new[] { 1, 2, 3 })) { MinimumFreeSlack = 15 });
            graphCompiler.AddActivity(new DependentActivity<int>(activityId6, 7, new HashSet<int>(new[] { 3 })));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId7, 4, new HashSet<int>(new[] { 4 })));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId8, 4, new HashSet<int>(new[] { 4, 6 })));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId9, 10, new HashSet<int>(new[] { 5 })));

            GraphCompilation<int, IDependentActivity<int>> complication = graphCompiler.Compile();

            Assert.AreEqual(0, complication.MissingDependencies.Count);
            Assert.AreEqual(0, complication.CircularDependencies.Count);
            Assert.AreEqual(3, complication.ResourceSchedules.Count);

            Assert.AreEqual(4, complication.ResourceSchedules[0].ScheduledActivities.Count());

            Assert.AreEqual(activityId2, complication.ResourceSchedules[0].ScheduledActivities[0].Id);
            Assert.AreEqual(0, complication.ResourceSchedules[0].ScheduledActivities[0].StartTime);
            Assert.AreEqual(7, complication.ResourceSchedules[0].ScheduledActivities[0].FinishTime);

            Assert.AreEqual(activityId4, complication.ResourceSchedules[0].ScheduledActivities[1].Id);
            Assert.AreEqual(7, complication.ResourceSchedules[0].ScheduledActivities[1].StartTime);
            Assert.AreEqual(18, complication.ResourceSchedules[0].ScheduledActivities[1].FinishTime);

            Assert.AreEqual(activityId7, complication.ResourceSchedules[0].ScheduledActivities[2].Id);
            Assert.AreEqual(18, complication.ResourceSchedules[0].ScheduledActivities[2].StartTime);
            Assert.AreEqual(22, complication.ResourceSchedules[0].ScheduledActivities[2].FinishTime);

            Assert.AreEqual(activityId9, complication.ResourceSchedules[0].ScheduledActivities[3].Id);
            Assert.AreEqual(31, complication.ResourceSchedules[0].ScheduledActivities[3].StartTime);
            Assert.AreEqual(41, complication.ResourceSchedules[0].ScheduledActivities[3].FinishTime);

            Assert.AreEqual(41, complication.ResourceSchedules[0].ScheduledActivities.Last().FinishTime);



            Assert.AreEqual(3, complication.ResourceSchedules[1].ScheduledActivities.Count());

            Assert.AreEqual(activityId3, complication.ResourceSchedules[1].ScheduledActivities[0].Id);
            Assert.AreEqual(0, complication.ResourceSchedules[1].ScheduledActivities[0].StartTime);
            Assert.AreEqual(8, complication.ResourceSchedules[1].ScheduledActivities[0].FinishTime);

            Assert.AreEqual(activityId6, complication.ResourceSchedules[1].ScheduledActivities[1].Id);
            Assert.AreEqual(8, complication.ResourceSchedules[1].ScheduledActivities[1].StartTime);
            Assert.AreEqual(15, complication.ResourceSchedules[1].ScheduledActivities[1].FinishTime);

            Assert.AreEqual(activityId8, complication.ResourceSchedules[1].ScheduledActivities[2].Id);
            Assert.AreEqual(18, complication.ResourceSchedules[1].ScheduledActivities[2].StartTime);
            Assert.AreEqual(22, complication.ResourceSchedules[1].ScheduledActivities[2].FinishTime);

            Assert.AreEqual(22, complication.ResourceSchedules[1].ScheduledActivities.Last().FinishTime);



            Assert.AreEqual(2, complication.ResourceSchedules[2].ScheduledActivities.Count());

            Assert.AreEqual(activityId1, complication.ResourceSchedules[2].ScheduledActivities[0].Id);
            Assert.AreEqual(0, complication.ResourceSchedules[2].ScheduledActivities[0].StartTime);
            Assert.AreEqual(6, complication.ResourceSchedules[2].ScheduledActivities[0].FinishTime);

            Assert.AreEqual(activityId5, complication.ResourceSchedules[2].ScheduledActivities[1].Id);
            Assert.AreEqual(8, complication.ResourceSchedules[2].ScheduledActivities[1].StartTime);
            Assert.AreEqual(16, complication.ResourceSchedules[2].ScheduledActivities[1].FinishTime);

            Assert.AreEqual(16, complication.ResourceSchedules[2].ScheduledActivities.Last().FinishTime);



            Assert.AreEqual(0, graphBuilder.Activity(activityId1).EarliestStartTime);
            Assert.AreEqual(6, graphBuilder.Activity(activityId1).EarliestFinishTime);
            Assert.AreEqual(2, graphBuilder.Activity(activityId1).FreeSlack);
            Assert.AreEqual(17, graphBuilder.Activity(activityId1).TotalSlack);
            Assert.AreEqual(17, graphBuilder.Activity(activityId1).LatestStartTime);
            Assert.AreEqual(23, graphBuilder.Activity(activityId1).LatestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId1).ResourceDependencies.Count());

            Assert.AreEqual(0, graphBuilder.Activity(activityId2).EarliestStartTime);
            Assert.AreEqual(7, graphBuilder.Activity(activityId2).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId2).FreeSlack);
            Assert.AreEqual(16, graphBuilder.Activity(activityId2).TotalSlack);
            Assert.AreEqual(16, graphBuilder.Activity(activityId2).LatestStartTime);
            Assert.AreEqual(23, graphBuilder.Activity(activityId2).LatestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId2).ResourceDependencies.Count());

            Assert.AreEqual(0, graphBuilder.Activity(activityId3).EarliestStartTime);
            Assert.AreEqual(8, graphBuilder.Activity(activityId3).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId3).FreeSlack);
            Assert.AreEqual(15, graphBuilder.Activity(activityId3).TotalSlack);
            Assert.AreEqual(15, graphBuilder.Activity(activityId3).LatestStartTime);
            Assert.AreEqual(23, graphBuilder.Activity(activityId3).LatestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId3).ResourceDependencies.Count());

            Assert.AreEqual(7, graphBuilder.Activity(activityId4).EarliestStartTime);
            Assert.AreEqual(18, graphBuilder.Activity(activityId4).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId4).FreeSlack);
            Assert.AreEqual(19, graphBuilder.Activity(activityId4).TotalSlack);
            Assert.AreEqual(26, graphBuilder.Activity(activityId4).LatestStartTime);
            Assert.AreEqual(37, graphBuilder.Activity(activityId4).LatestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId4).ResourceDependencies.Count());

            Assert.AreEqual(8, graphBuilder.Activity(activityId5).EarliestStartTime);
            Assert.AreEqual(16, graphBuilder.Activity(activityId5).EarliestFinishTime);
            Assert.AreEqual(15, graphBuilder.Activity(activityId5).FreeSlack);
            Assert.AreEqual(15, graphBuilder.Activity(activityId5).TotalSlack);
            Assert.AreEqual(23, graphBuilder.Activity(activityId5).LatestStartTime);
            Assert.AreEqual(31, graphBuilder.Activity(activityId5).LatestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId5).ResourceDependencies.Count());

            Assert.AreEqual(8, graphBuilder.Activity(activityId6).EarliestStartTime);
            Assert.AreEqual(15, graphBuilder.Activity(activityId6).EarliestFinishTime);
            Assert.AreEqual(3, graphBuilder.Activity(activityId6).FreeSlack);
            Assert.AreEqual(22, graphBuilder.Activity(activityId6).TotalSlack);
            Assert.AreEqual(30, graphBuilder.Activity(activityId6).LatestStartTime);
            Assert.AreEqual(37, graphBuilder.Activity(activityId6).LatestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId6).ResourceDependencies.Count());

            Assert.AreEqual(18, graphBuilder.Activity(activityId7).EarliestStartTime);
            Assert.AreEqual(22, graphBuilder.Activity(activityId7).EarliestFinishTime);
            Assert.AreEqual(19, graphBuilder.Activity(activityId7).FreeSlack);
            Assert.AreEqual(19, graphBuilder.Activity(activityId7).TotalSlack);
            Assert.AreEqual(37, graphBuilder.Activity(activityId7).LatestStartTime);
            Assert.AreEqual(41, graphBuilder.Activity(activityId7).LatestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId7).ResourceDependencies.Count());

            Assert.AreEqual(18, graphBuilder.Activity(activityId8).EarliestStartTime);
            Assert.AreEqual(22, graphBuilder.Activity(activityId8).EarliestFinishTime);
            Assert.AreEqual(19, graphBuilder.Activity(activityId8).FreeSlack);
            Assert.AreEqual(19, graphBuilder.Activity(activityId8).TotalSlack);
            Assert.AreEqual(37, graphBuilder.Activity(activityId8).LatestStartTime);
            Assert.AreEqual(41, graphBuilder.Activity(activityId8).LatestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId8).ResourceDependencies.Count());

            Assert.AreEqual(31, graphBuilder.Activity(activityId9).EarliestStartTime);
            Assert.AreEqual(41, graphBuilder.Activity(activityId9).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId9).FreeSlack);
            Assert.AreEqual(0, graphBuilder.Activity(activityId9).TotalSlack);
            Assert.AreEqual(31, graphBuilder.Activity(activityId9).LatestStartTime);
            Assert.AreEqual(41, graphBuilder.Activity(activityId9).LatestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId9).ResourceDependencies.Count());
        }

        [Fact]
        public void VertexGraphCompiler_CompileWithTwoResources_ResourceSchedulesCorrectOrder()
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
            var graphCompiler = VertexGraphCompiler<int, IDependentActivity<int>>.Create();
            var graphBuilder = graphCompiler.Builder;
            graphCompiler.AddActivity(new DependentActivity<int>(activityId1, 6));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId2, 7));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId3, 8));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId4, 11, new HashSet<int>(new[] { 2 })));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId5, 8, new HashSet<int>(new[] { 1, 2, 3 })));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId6, 7, new HashSet<int>(new[] { 3 })));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId7, 4, new HashSet<int>(new[] { 4 })));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId8, 4, new HashSet<int>(new[] { 4, 6 })));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId9, 10, new HashSet<int>(new[] { 5 })));

            GraphCompilation<int, IDependentActivity<int>> complication = graphCompiler.Compile(
                new List<IResource<int>>(new[]
                {
                    new Resource<int>(1, string.Empty, false, InterActivityAllocationType.None, 1.0, 0),
                    new Resource<int>(2, string.Empty, false, InterActivityAllocationType.None, 1.0, 0),
                }));

            Assert.AreEqual(0, complication.MissingDependencies.Count);
            Assert.AreEqual(0, complication.CircularDependencies.Count);
            Assert.AreEqual(2, complication.ResourceSchedules.Count);

            Assert.AreEqual(5, complication.ResourceSchedules[0].ScheduledActivities.Count());

            Assert.AreEqual(activityId3, complication.ResourceSchedules[0].ScheduledActivities[0].Id);
            Assert.AreEqual(0, complication.ResourceSchedules[0].ScheduledActivities[0].StartTime);
            Assert.AreEqual(8, complication.ResourceSchedules[0].ScheduledActivities[0].FinishTime);

            Assert.AreEqual(activityId4, complication.ResourceSchedules[0].ScheduledActivities[1].Id);
            Assert.AreEqual(8, complication.ResourceSchedules[0].ScheduledActivities[1].StartTime);
            Assert.AreEqual(19, complication.ResourceSchedules[0].ScheduledActivities[1].FinishTime);

            Assert.AreEqual(activityId6, complication.ResourceSchedules[0].ScheduledActivities[2].Id);
            Assert.AreEqual(19, complication.ResourceSchedules[0].ScheduledActivities[2].StartTime);
            Assert.AreEqual(26, complication.ResourceSchedules[0].ScheduledActivities[2].FinishTime);

            Assert.AreEqual(activityId7, complication.ResourceSchedules[0].ScheduledActivities[3].Id);
            Assert.AreEqual(26, complication.ResourceSchedules[0].ScheduledActivities[3].StartTime);
            Assert.AreEqual(30, complication.ResourceSchedules[0].ScheduledActivities[3].FinishTime);

            Assert.AreEqual(activityId8, complication.ResourceSchedules[0].ScheduledActivities[4].Id);
            Assert.AreEqual(30, complication.ResourceSchedules[0].ScheduledActivities[4].StartTime);
            Assert.AreEqual(34, complication.ResourceSchedules[0].ScheduledActivities[4].FinishTime);

            Assert.AreEqual(34, complication.ResourceSchedules[0].ScheduledActivities.Last().FinishTime);



            Assert.AreEqual(4, complication.ResourceSchedules[1].ScheduledActivities.Count());

            Assert.AreEqual(activityId2, complication.ResourceSchedules[1].ScheduledActivities[0].Id);
            Assert.AreEqual(0, complication.ResourceSchedules[1].ScheduledActivities[0].StartTime);
            Assert.AreEqual(7, complication.ResourceSchedules[1].ScheduledActivities[0].FinishTime);

            Assert.AreEqual(activityId1, complication.ResourceSchedules[1].ScheduledActivities[1].Id);
            Assert.AreEqual(7, complication.ResourceSchedules[1].ScheduledActivities[1].StartTime);
            Assert.AreEqual(13, complication.ResourceSchedules[1].ScheduledActivities[1].FinishTime);

            Assert.AreEqual(activityId5, complication.ResourceSchedules[1].ScheduledActivities[2].Id);
            Assert.AreEqual(13, complication.ResourceSchedules[1].ScheduledActivities[2].StartTime);
            Assert.AreEqual(21, complication.ResourceSchedules[1].ScheduledActivities[2].FinishTime);

            Assert.AreEqual(activityId9, complication.ResourceSchedules[1].ScheduledActivities[3].Id);
            Assert.AreEqual(21, complication.ResourceSchedules[1].ScheduledActivities[3].StartTime);
            Assert.AreEqual(31, complication.ResourceSchedules[1].ScheduledActivities[3].FinishTime);

            Assert.AreEqual(31, complication.ResourceSchedules[1].ScheduledActivities.Last().FinishTime);



            Assert.AreEqual(7, graphBuilder.Activity(activityId1).EarliestStartTime);
            Assert.AreEqual(13, graphBuilder.Activity(activityId1).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId1).FreeSlack);
            Assert.AreEqual(3, graphBuilder.Activity(activityId1).TotalSlack);
            Assert.AreEqual(10, graphBuilder.Activity(activityId1).LatestStartTime);
            Assert.AreEqual(16, graphBuilder.Activity(activityId1).LatestFinishTime);
            CollectionAssert.AreEquivalent(
                new List<int>(new int[] { 2 }),
                graphBuilder.Activity(activityId1).ResourceDependencies.ToList());

            Assert.AreEqual(0, graphBuilder.Activity(activityId2).EarliestStartTime);
            Assert.AreEqual(7, graphBuilder.Activity(activityId2).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId2).FreeSlack);
            Assert.AreEqual(1, graphBuilder.Activity(activityId2).TotalSlack);
            Assert.AreEqual(1, graphBuilder.Activity(activityId2).LatestStartTime);
            Assert.AreEqual(8, graphBuilder.Activity(activityId2).LatestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId2).ResourceDependencies.Count());

            Assert.AreEqual(0, graphBuilder.Activity(activityId3).EarliestStartTime);
            Assert.AreEqual(8, graphBuilder.Activity(activityId3).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId3).FreeSlack);
            Assert.AreEqual(0, graphBuilder.Activity(activityId3).TotalSlack);
            Assert.AreEqual(0, graphBuilder.Activity(activityId3).LatestStartTime);
            Assert.AreEqual(8, graphBuilder.Activity(activityId3).LatestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId3).ResourceDependencies.Count());

            Assert.AreEqual(8, graphBuilder.Activity(activityId4).EarliestStartTime);
            Assert.AreEqual(19, graphBuilder.Activity(activityId4).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId4).FreeSlack);
            Assert.AreEqual(0, graphBuilder.Activity(activityId4).TotalSlack);
            Assert.AreEqual(8, graphBuilder.Activity(activityId4).LatestStartTime);
            Assert.AreEqual(19, graphBuilder.Activity(activityId4).LatestFinishTime);
            CollectionAssert.AreEquivalent(
                new List<int>(new int[] { activityId3 }),
                graphBuilder.Activity(activityId4).ResourceDependencies.ToList());

            Assert.AreEqual(13, graphBuilder.Activity(activityId5).EarliestStartTime);
            Assert.AreEqual(21, graphBuilder.Activity(activityId5).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId5).FreeSlack);
            Assert.AreEqual(3, graphBuilder.Activity(activityId5).TotalSlack);
            Assert.AreEqual(16, graphBuilder.Activity(activityId5).LatestStartTime);
            Assert.AreEqual(24, graphBuilder.Activity(activityId5).LatestFinishTime);
            CollectionAssert.AreEquivalent(
                new List<int>(new int[] { activityId1 }),
                graphBuilder.Activity(activityId5).ResourceDependencies.ToList());

            Assert.AreEqual(19, graphBuilder.Activity(activityId6).EarliestStartTime);
            Assert.AreEqual(26, graphBuilder.Activity(activityId6).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId6).FreeSlack);
            Assert.AreEqual(0, graphBuilder.Activity(activityId6).TotalSlack);
            Assert.AreEqual(19, graphBuilder.Activity(activityId6).LatestStartTime);
            Assert.AreEqual(26, graphBuilder.Activity(activityId6).LatestFinishTime);
            CollectionAssert.AreEquivalent(
                new List<int>(new int[] { activityId4 }),
                graphBuilder.Activity(activityId6).ResourceDependencies.ToList());

            Assert.AreEqual(26, graphBuilder.Activity(activityId7).EarliestStartTime);
            Assert.AreEqual(30, graphBuilder.Activity(activityId7).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId7).FreeSlack);
            Assert.AreEqual(0, graphBuilder.Activity(activityId7).TotalSlack);
            Assert.AreEqual(26, graphBuilder.Activity(activityId7).LatestStartTime);
            Assert.AreEqual(30, graphBuilder.Activity(activityId7).LatestFinishTime);
            CollectionAssert.AreEquivalent(
                new List<int>(new int[] { activityId6 }),
                graphBuilder.Activity(activityId7).ResourceDependencies.ToList());

            Assert.AreEqual(30, graphBuilder.Activity(activityId8).EarliestStartTime);
            Assert.AreEqual(34, graphBuilder.Activity(activityId8).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId8).FreeSlack);
            Assert.AreEqual(0, graphBuilder.Activity(activityId8).TotalSlack);
            Assert.AreEqual(30, graphBuilder.Activity(activityId8).LatestStartTime);
            Assert.AreEqual(34, graphBuilder.Activity(activityId8).LatestFinishTime);
            CollectionAssert.AreEquivalent(
                new List<int>(new int[] { activityId7 }),
                graphBuilder.Activity(activityId8).ResourceDependencies.ToList());

            Assert.AreEqual(21, graphBuilder.Activity(activityId9).EarliestStartTime);
            Assert.AreEqual(31, graphBuilder.Activity(activityId9).EarliestFinishTime);
            Assert.AreEqual(3, graphBuilder.Activity(activityId9).FreeSlack);
            Assert.AreEqual(3, graphBuilder.Activity(activityId9).TotalSlack);
            Assert.AreEqual(24, graphBuilder.Activity(activityId9).LatestStartTime);
            Assert.AreEqual(34, graphBuilder.Activity(activityId9).LatestFinishTime);
            CollectionAssert.AreEquivalent(
                new List<int>(new int[] { activityId5 }),
                graphBuilder.Activity(activityId9).ResourceDependencies.ToList());
        }

        [Fact]
        public void VertexGraphCompiler_CyclomaticComplexityWithNoNodes_FindsZero()
        {
            var graphCompiler = VertexGraphCompiler<int, IDependentActivity<int>>.Create();
            graphCompiler.Compile();
            Assert.AreEqual(0, graphCompiler.CyclomaticComplexity);
        }

        [Fact]
        public void VertexGraphCompiler_CyclomaticComplexityInOneNetwork_AsExpected()
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
            var graphCompiler = VertexGraphCompiler<int, IDependentActivity<int>>.Create();
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

            Assert.AreEqual(6, graphCompiler.CyclomaticComplexity);
        }

        [Fact]
        public void VertexGraphCompiler_CyclomaticComplexityInThreeNetworks_AsExpected()
        {
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int activityId6 = activityId5 + 1;
            var graphCompiler = VertexGraphCompiler<int, IDependentActivity<int>>.Create();
            graphCompiler.AddActivity(new DependentActivity<int>(activityId1, 6));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId2, 7));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId3, 8));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId4, 11, new HashSet<int>(new[] { 1 })));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId5, 8, new HashSet<int>(new[] { 2 })));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId6, 7, new HashSet<int>(new[] { 3 })));

            graphCompiler.Compile();

            Assert.AreEqual(3, graphCompiler.CyclomaticComplexity);
        }

        [Fact]
        public void VertexGraphCompiler_CyclomaticComplexityWithTwoLoneNodes_AsExpected()
        {
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int activityId6 = activityId5 + 1;
            var graphCompiler = VertexGraphCompiler<int, IDependentActivity<int>>.Create();
            graphCompiler.AddActivity(new DependentActivity<int>(activityId1, 6));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId2, 7));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId3, 8));
            graphCompiler.AddActivity(new DependentActivity<int>(activityId4, 11, new HashSet<int>(new[] { 1 })));

            graphCompiler.Compile();

            Assert.AreEqual(3, graphCompiler.CyclomaticComplexity);
        }
    }
}
