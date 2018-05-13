using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs.Tests
{
    [TestClass]
    public class ArrowGraphBuilderExtensionsTests
    {
        [TestMethod]
        public void ArrowGraphBuilderExtensions_CalculateCriticalPath_AsExpected()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int activityId6 = activityId5 + 1;
            int activityId7 = activityId6 + 1;
            int activityId8 = activityId7 + 1;
            int activityId9 = activityId8 + 1;
            int dummyActivityId = 100;
            var graphBuilder = new ArrowGraphBuilder<int, IActivity<int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            graphBuilder.AddActivity(new Activity<int>(activityId1, 6));
            graphBuilder.AddActivity(new Activity<int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int>(activityId4, 11), new HashSet<int>(new[] { 2 }));
            graphBuilder.AddActivity(new Activity<int>(activityId5, 8), new HashSet<int>(new[] { 1, 2, 3 }));
            graphBuilder.AddActivity(new Activity<int>(activityId6, 7), new HashSet<int>(new[] { 3 }));
            graphBuilder.AddActivity(new Activity<int>(activityId7, 4), new HashSet<int>(new[] { 4 }));
            graphBuilder.AddActivity(new Activity<int>(activityId8, 4), new HashSet<int>(new[] { 4, 6 }));
            graphBuilder.AddActivity(new Activity<int>(activityId9, 10), new HashSet<int>(new[] { 5 }));

            graphBuilder.CalculateCriticalPath();

            Assert.AreEqual(0, graphBuilder.Activity(activityId1).EarliestStartTime);
            Assert.AreEqual(6, graphBuilder.Activity(activityId1).EarliestFinishTime);
            Assert.AreEqual(2, graphBuilder.Activity(activityId1).FreeSlack);
            Assert.AreEqual(2, graphBuilder.Activity(activityId1).TotalSlack);
            Assert.AreEqual(2, graphBuilder.Activity(activityId1).LatestStartTime);
            Assert.AreEqual(8, graphBuilder.Activity(activityId1).LatestFinishTime);

            Assert.AreEqual(0, graphBuilder.Activity(activityId2).EarliestStartTime);
            Assert.AreEqual(7, graphBuilder.Activity(activityId2).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId2).FreeSlack);
            Assert.AreEqual(1, graphBuilder.Activity(activityId2).TotalSlack);
            Assert.AreEqual(1, graphBuilder.Activity(activityId2).LatestStartTime);
            Assert.AreEqual(8, graphBuilder.Activity(activityId2).LatestFinishTime);

            Assert.AreEqual(0, graphBuilder.Activity(activityId3).EarliestStartTime);
            Assert.AreEqual(8, graphBuilder.Activity(activityId3).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId3).FreeSlack);
            Assert.AreEqual(0, graphBuilder.Activity(activityId3).TotalSlack);
            Assert.AreEqual(0, graphBuilder.Activity(activityId3).LatestStartTime);
            Assert.AreEqual(8, graphBuilder.Activity(activityId3).LatestFinishTime);

            Assert.AreEqual(7, graphBuilder.Activity(activityId4).EarliestStartTime);
            Assert.AreEqual(18, graphBuilder.Activity(activityId4).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId4).FreeSlack);
            Assert.AreEqual(4, graphBuilder.Activity(activityId4).TotalSlack);
            Assert.AreEqual(11, graphBuilder.Activity(activityId4).LatestStartTime);
            Assert.AreEqual(22, graphBuilder.Activity(activityId4).LatestFinishTime);

            Assert.AreEqual(8, graphBuilder.Activity(activityId5).EarliestStartTime);
            Assert.AreEqual(16, graphBuilder.Activity(activityId5).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId5).FreeSlack);
            Assert.AreEqual(0, graphBuilder.Activity(activityId5).TotalSlack);
            Assert.AreEqual(8, graphBuilder.Activity(activityId5).LatestStartTime);
            Assert.AreEqual(16, graphBuilder.Activity(activityId5).LatestFinishTime);

            Assert.AreEqual(8, graphBuilder.Activity(activityId6).EarliestStartTime);
            Assert.AreEqual(15, graphBuilder.Activity(activityId6).EarliestFinishTime);
            Assert.AreEqual(3, graphBuilder.Activity(activityId6).FreeSlack);
            Assert.AreEqual(7, graphBuilder.Activity(activityId6).TotalSlack);
            Assert.AreEqual(15, graphBuilder.Activity(activityId6).LatestStartTime);
            Assert.AreEqual(22, graphBuilder.Activity(activityId6).LatestFinishTime);

            Assert.AreEqual(18, graphBuilder.Activity(activityId7).EarliestStartTime);
            Assert.AreEqual(22, graphBuilder.Activity(activityId7).EarliestFinishTime);
            Assert.AreEqual(4, graphBuilder.Activity(activityId7).FreeSlack);
            Assert.AreEqual(4, graphBuilder.Activity(activityId7).TotalSlack);
            Assert.AreEqual(22, graphBuilder.Activity(activityId7).LatestStartTime);
            Assert.AreEqual(26, graphBuilder.Activity(activityId7).LatestFinishTime);

            Assert.AreEqual(18, graphBuilder.Activity(activityId8).EarliestStartTime);
            Assert.AreEqual(22, graphBuilder.Activity(activityId8).EarliestFinishTime);
            Assert.AreEqual(4, graphBuilder.Activity(activityId8).FreeSlack);
            Assert.AreEqual(4, graphBuilder.Activity(activityId8).TotalSlack);
            Assert.AreEqual(22, graphBuilder.Activity(activityId8).LatestStartTime);
            Assert.AreEqual(26, graphBuilder.Activity(activityId8).LatestFinishTime);

            Assert.AreEqual(16, graphBuilder.Activity(activityId9).EarliestStartTime);
            Assert.AreEqual(26, graphBuilder.Activity(activityId9).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId9).FreeSlack);
            Assert.AreEqual(0, graphBuilder.Activity(activityId9).TotalSlack);
            Assert.AreEqual(16, graphBuilder.Activity(activityId9).LatestStartTime);
            Assert.AreEqual(26, graphBuilder.Activity(activityId9).LatestFinishTime);
        }

        [TestMethod]
        public void ArrowGraphBuilderExtensions_CalculateCriticalPathWithMinimumFreeSlackInStartActivity_AsExpected()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int activityId6 = activityId5 + 1;
            int activityId7 = activityId6 + 1;
            int activityId8 = activityId7 + 1;
            int activityId9 = activityId8 + 1;
            int dummyActivityId = 100;
            var graphBuilder = new ArrowGraphBuilder<int, IActivity<int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            graphBuilder.AddActivity(new Activity<int>(activityId1, 6) { MinimumFreeSlack = 10 });
            graphBuilder.AddActivity(new Activity<int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int>(activityId4, 11), new HashSet<int>(new[] { 2 }));
            graphBuilder.AddActivity(new Activity<int>(activityId5, 8), new HashSet<int>(new[] { 1, 2, 3 }));
            graphBuilder.AddActivity(new Activity<int>(activityId6, 7), new HashSet<int>(new[] { 3 }));
            graphBuilder.AddActivity(new Activity<int>(activityId7, 4), new HashSet<int>(new[] { 4 }));
            graphBuilder.AddActivity(new Activity<int>(activityId8, 4), new HashSet<int>(new[] { 4, 6 }));
            graphBuilder.AddActivity(new Activity<int>(activityId9, 10), new HashSet<int>(new[] { 5 }));

            graphBuilder.CalculateCriticalPath();

            Assert.AreEqual(0, graphBuilder.Activity(activityId1).EarliestStartTime);
            Assert.AreEqual(6, graphBuilder.Activity(activityId1).EarliestFinishTime);
            Assert.AreEqual(10, graphBuilder.Activity(activityId1).FreeSlack);
            Assert.AreEqual(10, graphBuilder.Activity(activityId1).TotalSlack);
            Assert.AreEqual(10, graphBuilder.Activity(activityId1).LatestStartTime);
            Assert.AreEqual(16, graphBuilder.Activity(activityId1).LatestFinishTime);

            Assert.AreEqual(0, graphBuilder.Activity(activityId2).EarliestStartTime);
            Assert.AreEqual(7, graphBuilder.Activity(activityId2).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId2).FreeSlack);
            Assert.AreEqual(9, graphBuilder.Activity(activityId2).TotalSlack);
            Assert.AreEqual(9, graphBuilder.Activity(activityId2).LatestStartTime);
            Assert.AreEqual(16, graphBuilder.Activity(activityId2).LatestFinishTime);

            Assert.AreEqual(0, graphBuilder.Activity(activityId3).EarliestStartTime);
            Assert.AreEqual(8, graphBuilder.Activity(activityId3).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId3).FreeSlack);
            Assert.AreEqual(8, graphBuilder.Activity(activityId3).TotalSlack);
            Assert.AreEqual(8, graphBuilder.Activity(activityId3).LatestStartTime);
            Assert.AreEqual(16, graphBuilder.Activity(activityId3).LatestFinishTime);

            Assert.AreEqual(7, graphBuilder.Activity(activityId4).EarliestStartTime);
            Assert.AreEqual(18, graphBuilder.Activity(activityId4).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId4).FreeSlack);
            Assert.AreEqual(12, graphBuilder.Activity(activityId4).TotalSlack);
            Assert.AreEqual(19, graphBuilder.Activity(activityId4).LatestStartTime);
            Assert.AreEqual(30, graphBuilder.Activity(activityId4).LatestFinishTime);

            Assert.AreEqual(16, graphBuilder.Activity(activityId5).EarliestStartTime);
            Assert.AreEqual(24, graphBuilder.Activity(activityId5).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId5).FreeSlack);
            Assert.AreEqual(0, graphBuilder.Activity(activityId5).TotalSlack);
            Assert.AreEqual(16, graphBuilder.Activity(activityId5).LatestStartTime);
            Assert.AreEqual(24, graphBuilder.Activity(activityId5).LatestFinishTime);

            Assert.AreEqual(8, graphBuilder.Activity(activityId6).EarliestStartTime);
            Assert.AreEqual(15, graphBuilder.Activity(activityId6).EarliestFinishTime);
            Assert.AreEqual(3, graphBuilder.Activity(activityId6).FreeSlack);
            Assert.AreEqual(15, graphBuilder.Activity(activityId6).TotalSlack);
            Assert.AreEqual(23, graphBuilder.Activity(activityId6).LatestStartTime);
            Assert.AreEqual(30, graphBuilder.Activity(activityId6).LatestFinishTime);

            Assert.AreEqual(18, graphBuilder.Activity(activityId7).EarliestStartTime);
            Assert.AreEqual(22, graphBuilder.Activity(activityId7).EarliestFinishTime);
            Assert.AreEqual(12, graphBuilder.Activity(activityId7).FreeSlack);
            Assert.AreEqual(12, graphBuilder.Activity(activityId7).TotalSlack);
            Assert.AreEqual(30, graphBuilder.Activity(activityId7).LatestStartTime);
            Assert.AreEqual(34, graphBuilder.Activity(activityId7).LatestFinishTime);

            Assert.AreEqual(18, graphBuilder.Activity(activityId8).EarliestStartTime);
            Assert.AreEqual(22, graphBuilder.Activity(activityId8).EarliestFinishTime);
            Assert.AreEqual(12, graphBuilder.Activity(activityId8).FreeSlack);
            Assert.AreEqual(12, graphBuilder.Activity(activityId8).TotalSlack);
            Assert.AreEqual(30, graphBuilder.Activity(activityId8).LatestStartTime);
            Assert.AreEqual(34, graphBuilder.Activity(activityId8).LatestFinishTime);

            Assert.AreEqual(24, graphBuilder.Activity(activityId9).EarliestStartTime);
            Assert.AreEqual(34, graphBuilder.Activity(activityId9).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId9).FreeSlack);
            Assert.AreEqual(0, graphBuilder.Activity(activityId9).TotalSlack);
            Assert.AreEqual(24, graphBuilder.Activity(activityId9).LatestStartTime);
            Assert.AreEqual(34, graphBuilder.Activity(activityId9).LatestFinishTime);
        }

        [TestMethod]
        public void ArrowGraphBuilderExtensions_CalculateCriticalPathWithMinimumFreeSlackInNormalActivity_AsExpected()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int activityId6 = activityId5 + 1;
            int activityId7 = activityId6 + 1;
            int activityId8 = activityId7 + 1;
            int activityId9 = activityId8 + 1;
            int dummyActivityId = 100;
            var graphBuilder = new ArrowGraphBuilder<int, IActivity<int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            graphBuilder.AddActivity(new Activity<int>(activityId1, 6));
            graphBuilder.AddActivity(new Activity<int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int>(activityId4, 11), new HashSet<int>(new[] { 2 }));
            graphBuilder.AddActivity(new Activity<int>(activityId5, 8) { MinimumFreeSlack = 15 }, new HashSet<int>(new[] { 1, 2, 3 }));
            graphBuilder.AddActivity(new Activity<int>(activityId6, 7), new HashSet<int>(new[] { 3 }));
            graphBuilder.AddActivity(new Activity<int>(activityId7, 4), new HashSet<int>(new[] { 4 }));
            graphBuilder.AddActivity(new Activity<int>(activityId8, 4), new HashSet<int>(new[] { 4, 6 }));
            graphBuilder.AddActivity(new Activity<int>(activityId9, 10), new HashSet<int>(new[] { 5 }));

            graphBuilder.CalculateCriticalPath();

            Assert.AreEqual(0, graphBuilder.Activity(activityId1).EarliestStartTime);
            Assert.AreEqual(6, graphBuilder.Activity(activityId1).EarliestFinishTime);
            Assert.AreEqual(2, graphBuilder.Activity(activityId1).FreeSlack);
            Assert.AreEqual(17, graphBuilder.Activity(activityId1).TotalSlack);
            Assert.AreEqual(17, graphBuilder.Activity(activityId1).LatestStartTime);
            Assert.AreEqual(23, graphBuilder.Activity(activityId1).LatestFinishTime);

            Assert.AreEqual(0, graphBuilder.Activity(activityId2).EarliestStartTime);
            Assert.AreEqual(7, graphBuilder.Activity(activityId2).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId2).FreeSlack);
            Assert.AreEqual(16, graphBuilder.Activity(activityId2).TotalSlack);
            Assert.AreEqual(16, graphBuilder.Activity(activityId2).LatestStartTime);
            Assert.AreEqual(23, graphBuilder.Activity(activityId2).LatestFinishTime);

            Assert.AreEqual(0, graphBuilder.Activity(activityId3).EarliestStartTime);
            Assert.AreEqual(8, graphBuilder.Activity(activityId3).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId3).FreeSlack);
            Assert.AreEqual(15, graphBuilder.Activity(activityId3).TotalSlack);
            Assert.AreEqual(15, graphBuilder.Activity(activityId3).LatestStartTime);
            Assert.AreEqual(23, graphBuilder.Activity(activityId3).LatestFinishTime);

            Assert.AreEqual(7, graphBuilder.Activity(activityId4).EarliestStartTime);
            Assert.AreEqual(18, graphBuilder.Activity(activityId4).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId4).FreeSlack);
            Assert.AreEqual(19, graphBuilder.Activity(activityId4).TotalSlack);
            Assert.AreEqual(26, graphBuilder.Activity(activityId4).LatestStartTime);
            Assert.AreEqual(37, graphBuilder.Activity(activityId4).LatestFinishTime);

            Assert.AreEqual(8, graphBuilder.Activity(activityId5).EarliestStartTime);
            Assert.AreEqual(16, graphBuilder.Activity(activityId5).EarliestFinishTime);
            Assert.AreEqual(15, graphBuilder.Activity(activityId5).FreeSlack);
            Assert.AreEqual(15, graphBuilder.Activity(activityId5).TotalSlack);
            Assert.AreEqual(23, graphBuilder.Activity(activityId5).LatestStartTime);
            Assert.AreEqual(31, graphBuilder.Activity(activityId5).LatestFinishTime);

            Assert.AreEqual(8, graphBuilder.Activity(activityId6).EarliestStartTime);
            Assert.AreEqual(15, graphBuilder.Activity(activityId6).EarliestFinishTime);
            Assert.AreEqual(3, graphBuilder.Activity(activityId6).FreeSlack);
            Assert.AreEqual(22, graphBuilder.Activity(activityId6).TotalSlack);
            Assert.AreEqual(30, graphBuilder.Activity(activityId6).LatestStartTime);
            Assert.AreEqual(37, graphBuilder.Activity(activityId6).LatestFinishTime);

            Assert.AreEqual(18, graphBuilder.Activity(activityId7).EarliestStartTime);
            Assert.AreEqual(22, graphBuilder.Activity(activityId7).EarliestFinishTime);
            Assert.AreEqual(19, graphBuilder.Activity(activityId7).FreeSlack);
            Assert.AreEqual(19, graphBuilder.Activity(activityId7).TotalSlack);
            Assert.AreEqual(37, graphBuilder.Activity(activityId7).LatestStartTime);
            Assert.AreEqual(41, graphBuilder.Activity(activityId7).LatestFinishTime);

            Assert.AreEqual(18, graphBuilder.Activity(activityId8).EarliestStartTime);
            Assert.AreEqual(22, graphBuilder.Activity(activityId8).EarliestFinishTime);
            Assert.AreEqual(19, graphBuilder.Activity(activityId8).FreeSlack);
            Assert.AreEqual(19, graphBuilder.Activity(activityId8).TotalSlack);
            Assert.AreEqual(37, graphBuilder.Activity(activityId8).LatestStartTime);
            Assert.AreEqual(41, graphBuilder.Activity(activityId8).LatestFinishTime);

            Assert.AreEqual(31, graphBuilder.Activity(activityId9).EarliestStartTime);
            Assert.AreEqual(41, graphBuilder.Activity(activityId9).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId9).FreeSlack);
            Assert.AreEqual(0, graphBuilder.Activity(activityId9).TotalSlack);
            Assert.AreEqual(31, graphBuilder.Activity(activityId9).LatestStartTime);
            Assert.AreEqual(41, graphBuilder.Activity(activityId9).LatestFinishTime);
        }

        [TestMethod]
        public void ArrowGraphBuilderExtensions_CalculateCriticalPathWithMinimumFreeSlackInEndActivity_AsExpected()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int activityId6 = activityId5 + 1;
            int activityId7 = activityId6 + 1;
            int activityId8 = activityId7 + 1;
            int activityId9 = activityId8 + 1;
            int dummyActivityId = 100;
            var graphBuilder = new ArrowGraphBuilder<int, IActivity<int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            graphBuilder.AddActivity(new Activity<int>(activityId1, 6));
            graphBuilder.AddActivity(new Activity<int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int>(activityId4, 11), new HashSet<int>(new[] { 2 }));
            graphBuilder.AddActivity(new Activity<int>(activityId5, 8), new HashSet<int>(new[] { 1, 2, 3 }));
            graphBuilder.AddActivity(new Activity<int>(activityId6, 7), new HashSet<int>(new[] { 3 }));
            graphBuilder.AddActivity(new Activity<int>(activityId7, 4), new HashSet<int>(new[] { 4 }));
            graphBuilder.AddActivity(new Activity<int>(activityId8, 4), new HashSet<int>(new[] { 4, 6 }));
            graphBuilder.AddActivity(new Activity<int>(activityId9, 10) { MinimumFreeSlack = 15 }, new HashSet<int>(new[] { 5 }));

            graphBuilder.CalculateCriticalPath();

            Assert.AreEqual(0, graphBuilder.Activity(activityId1).EarliestStartTime);
            Assert.AreEqual(6, graphBuilder.Activity(activityId1).EarliestFinishTime);
            Assert.AreEqual(2, graphBuilder.Activity(activityId1).FreeSlack);
            Assert.AreEqual(17, graphBuilder.Activity(activityId1).TotalSlack);
            Assert.AreEqual(17, graphBuilder.Activity(activityId1).LatestStartTime);
            Assert.AreEqual(23, graphBuilder.Activity(activityId1).LatestFinishTime);

            Assert.AreEqual(0, graphBuilder.Activity(activityId2).EarliestStartTime);
            Assert.AreEqual(7, graphBuilder.Activity(activityId2).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId2).FreeSlack);
            Assert.AreEqual(16, graphBuilder.Activity(activityId2).TotalSlack);
            Assert.AreEqual(16, graphBuilder.Activity(activityId2).LatestStartTime);
            Assert.AreEqual(23, graphBuilder.Activity(activityId2).LatestFinishTime);

            Assert.AreEqual(0, graphBuilder.Activity(activityId3).EarliestStartTime);
            Assert.AreEqual(8, graphBuilder.Activity(activityId3).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId3).FreeSlack);
            Assert.AreEqual(15, graphBuilder.Activity(activityId3).TotalSlack);
            Assert.AreEqual(15, graphBuilder.Activity(activityId3).LatestStartTime);
            Assert.AreEqual(23, graphBuilder.Activity(activityId3).LatestFinishTime);

            Assert.AreEqual(7, graphBuilder.Activity(activityId4).EarliestStartTime);
            Assert.AreEqual(18, graphBuilder.Activity(activityId4).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId4).FreeSlack);
            Assert.AreEqual(19, graphBuilder.Activity(activityId4).TotalSlack);
            Assert.AreEqual(26, graphBuilder.Activity(activityId4).LatestStartTime);
            Assert.AreEqual(37, graphBuilder.Activity(activityId4).LatestFinishTime);

            Assert.AreEqual(8, graphBuilder.Activity(activityId5).EarliestStartTime);
            Assert.AreEqual(16, graphBuilder.Activity(activityId5).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId5).FreeSlack);
            Assert.AreEqual(15, graphBuilder.Activity(activityId5).TotalSlack);
            Assert.AreEqual(23, graphBuilder.Activity(activityId5).LatestStartTime);
            Assert.AreEqual(31, graphBuilder.Activity(activityId5).LatestFinishTime);

            Assert.AreEqual(8, graphBuilder.Activity(activityId6).EarliestStartTime);
            Assert.AreEqual(15, graphBuilder.Activity(activityId6).EarliestFinishTime);
            Assert.AreEqual(3, graphBuilder.Activity(activityId6).FreeSlack);
            Assert.AreEqual(22, graphBuilder.Activity(activityId6).TotalSlack);
            Assert.AreEqual(30, graphBuilder.Activity(activityId6).LatestStartTime);
            Assert.AreEqual(37, graphBuilder.Activity(activityId6).LatestFinishTime);

            Assert.AreEqual(18, graphBuilder.Activity(activityId7).EarliestStartTime);
            Assert.AreEqual(22, graphBuilder.Activity(activityId7).EarliestFinishTime);
            Assert.AreEqual(19, graphBuilder.Activity(activityId7).FreeSlack);
            Assert.AreEqual(19, graphBuilder.Activity(activityId7).TotalSlack);
            Assert.AreEqual(37, graphBuilder.Activity(activityId7).LatestStartTime);
            Assert.AreEqual(41, graphBuilder.Activity(activityId7).LatestFinishTime);

            Assert.AreEqual(18, graphBuilder.Activity(activityId8).EarliestStartTime);
            Assert.AreEqual(22, graphBuilder.Activity(activityId8).EarliestFinishTime);
            Assert.AreEqual(19, graphBuilder.Activity(activityId8).FreeSlack);
            Assert.AreEqual(19, graphBuilder.Activity(activityId8).TotalSlack);
            Assert.AreEqual(37, graphBuilder.Activity(activityId8).LatestStartTime);
            Assert.AreEqual(41, graphBuilder.Activity(activityId8).LatestFinishTime);

            Assert.AreEqual(16, graphBuilder.Activity(activityId9).EarliestStartTime);
            Assert.AreEqual(26, graphBuilder.Activity(activityId9).EarliestFinishTime);
            Assert.AreEqual(15, graphBuilder.Activity(activityId9).FreeSlack);
            Assert.AreEqual(15, graphBuilder.Activity(activityId9).TotalSlack);
            Assert.AreEqual(31, graphBuilder.Activity(activityId9).LatestStartTime);
            Assert.AreEqual(41, graphBuilder.Activity(activityId9).LatestFinishTime);
        }

        [TestMethod]
        public void ArrowGraphBuilderExtensions_CalculateCriticalPathWithMinimumEarliestStartTimeInStartActivity_AsExpected()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int activityId6 = activityId5 + 1;
            int activityId7 = activityId6 + 1;
            int activityId8 = activityId7 + 1;
            int activityId9 = activityId8 + 1;
            int dummyActivityId = 100;
            var graphBuilder = new ArrowGraphBuilder<int, IActivity<int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            graphBuilder.AddActivity(new Activity<int>(activityId1, 6) { MinimumEarliestStartTime = 10 });
            graphBuilder.AddActivity(new Activity<int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int>(activityId4, 11), new HashSet<int>(new[] { 2 }));
            graphBuilder.AddActivity(new Activity<int>(activityId5, 8), new HashSet<int>(new[] { 1, 2, 3 }));
            graphBuilder.AddActivity(new Activity<int>(activityId6, 7), new HashSet<int>(new[] { 3 }));
            graphBuilder.AddActivity(new Activity<int>(activityId7, 4), new HashSet<int>(new[] { 4 }));
            graphBuilder.AddActivity(new Activity<int>(activityId8, 4), new HashSet<int>(new[] { 4, 6 }));
            graphBuilder.AddActivity(new Activity<int>(activityId9, 10), new HashSet<int>(new[] { 5 }));

            graphBuilder.CalculateCriticalPath();

            Assert.AreEqual(10, graphBuilder.Activity(activityId1).EarliestStartTime);
            Assert.AreEqual(16, graphBuilder.Activity(activityId1).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId1).FreeSlack);
            Assert.AreEqual(0, graphBuilder.Activity(activityId1).TotalSlack);
            Assert.AreEqual(10, graphBuilder.Activity(activityId1).LatestStartTime);
            Assert.AreEqual(16, graphBuilder.Activity(activityId1).LatestFinishTime);

            Assert.AreEqual(0, graphBuilder.Activity(activityId2).EarliestStartTime);
            Assert.AreEqual(7, graphBuilder.Activity(activityId2).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId2).FreeSlack);
            Assert.AreEqual(9, graphBuilder.Activity(activityId2).TotalSlack);
            Assert.AreEqual(9, graphBuilder.Activity(activityId2).LatestStartTime);
            Assert.AreEqual(16, graphBuilder.Activity(activityId2).LatestFinishTime);

            Assert.AreEqual(0, graphBuilder.Activity(activityId3).EarliestStartTime);
            Assert.AreEqual(8, graphBuilder.Activity(activityId3).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId3).FreeSlack);
            Assert.AreEqual(8, graphBuilder.Activity(activityId3).TotalSlack);
            Assert.AreEqual(8, graphBuilder.Activity(activityId3).LatestStartTime);
            Assert.AreEqual(16, graphBuilder.Activity(activityId3).LatestFinishTime);

            Assert.AreEqual(7, graphBuilder.Activity(activityId4).EarliestStartTime);
            Assert.AreEqual(18, graphBuilder.Activity(activityId4).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId4).FreeSlack);
            Assert.AreEqual(12, graphBuilder.Activity(activityId4).TotalSlack);
            Assert.AreEqual(19, graphBuilder.Activity(activityId4).LatestStartTime);
            Assert.AreEqual(30, graphBuilder.Activity(activityId4).LatestFinishTime);

            Assert.AreEqual(16, graphBuilder.Activity(activityId5).EarliestStartTime);
            Assert.AreEqual(24, graphBuilder.Activity(activityId5).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId5).FreeSlack);
            Assert.AreEqual(0, graphBuilder.Activity(activityId5).TotalSlack);
            Assert.AreEqual(16, graphBuilder.Activity(activityId5).LatestStartTime);
            Assert.AreEqual(24, graphBuilder.Activity(activityId5).LatestFinishTime);

            Assert.AreEqual(8, graphBuilder.Activity(activityId6).EarliestStartTime);
            Assert.AreEqual(15, graphBuilder.Activity(activityId6).EarliestFinishTime);
            Assert.AreEqual(3, graphBuilder.Activity(activityId6).FreeSlack);
            Assert.AreEqual(15, graphBuilder.Activity(activityId6).TotalSlack);
            Assert.AreEqual(23, graphBuilder.Activity(activityId6).LatestStartTime);
            Assert.AreEqual(30, graphBuilder.Activity(activityId6).LatestFinishTime);

            Assert.AreEqual(18, graphBuilder.Activity(activityId7).EarliestStartTime);
            Assert.AreEqual(22, graphBuilder.Activity(activityId7).EarliestFinishTime);
            Assert.AreEqual(12, graphBuilder.Activity(activityId7).FreeSlack);
            Assert.AreEqual(12, graphBuilder.Activity(activityId7).TotalSlack);
            Assert.AreEqual(30, graphBuilder.Activity(activityId7).LatestStartTime);
            Assert.AreEqual(34, graphBuilder.Activity(activityId7).LatestFinishTime);

            Assert.AreEqual(18, graphBuilder.Activity(activityId8).EarliestStartTime);
            Assert.AreEqual(22, graphBuilder.Activity(activityId8).EarliestFinishTime);
            Assert.AreEqual(12, graphBuilder.Activity(activityId8).FreeSlack);
            Assert.AreEqual(12, graphBuilder.Activity(activityId8).TotalSlack);
            Assert.AreEqual(30, graphBuilder.Activity(activityId8).LatestStartTime);
            Assert.AreEqual(34, graphBuilder.Activity(activityId8).LatestFinishTime);

            Assert.AreEqual(24, graphBuilder.Activity(activityId9).EarliestStartTime);
            Assert.AreEqual(34, graphBuilder.Activity(activityId9).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId9).FreeSlack);
            Assert.AreEqual(0, graphBuilder.Activity(activityId9).TotalSlack);
            Assert.AreEqual(24, graphBuilder.Activity(activityId9).LatestStartTime);
            Assert.AreEqual(34, graphBuilder.Activity(activityId9).LatestFinishTime);
        }

        [TestMethod]
        public void ArrowGraphBuilderExtensions_CalculateCriticalPathWithMinimumEarliestStartTimeInNormalActivity_AsExpected()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int activityId6 = activityId5 + 1;
            int activityId7 = activityId6 + 1;
            int activityId8 = activityId7 + 1;
            int activityId9 = activityId8 + 1;
            int dummyActivityId = 100;
            var graphBuilder = new ArrowGraphBuilder<int, IActivity<int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            graphBuilder.AddActivity(new Activity<int>(activityId1, 6));
            graphBuilder.AddActivity(new Activity<int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int>(activityId4, 11), new HashSet<int>(new[] { 2 }));
            graphBuilder.AddActivity(new Activity<int>(activityId5, 8) { MinimumEarliestStartTime = 10 }, new HashSet<int>(new[] { 1, 2, 3 }));
            graphBuilder.AddActivity(new Activity<int>(activityId6, 7), new HashSet<int>(new[] { 3 }));
            graphBuilder.AddActivity(new Activity<int>(activityId7, 4), new HashSet<int>(new[] { 4 }));
            graphBuilder.AddActivity(new Activity<int>(activityId8, 4), new HashSet<int>(new[] { 4, 6 }));
            graphBuilder.AddActivity(new Activity<int>(activityId9, 10), new HashSet<int>(new[] { 5 }));

            graphBuilder.TransitiveReduction();
            graphBuilder.CalculateCriticalPath();

            Assert.AreEqual(0, graphBuilder.Activity(activityId1).EarliestStartTime);
            Assert.AreEqual(6, graphBuilder.Activity(activityId1).EarliestFinishTime);
            Assert.AreEqual(4, graphBuilder.Activity(activityId1).FreeSlack);
            Assert.AreEqual(4, graphBuilder.Activity(activityId1).TotalSlack);
            Assert.AreEqual(4, graphBuilder.Activity(activityId1).LatestStartTime);
            Assert.AreEqual(10, graphBuilder.Activity(activityId1).LatestFinishTime);

            Assert.AreEqual(0, graphBuilder.Activity(activityId2).EarliestStartTime);
            Assert.AreEqual(7, graphBuilder.Activity(activityId2).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId2).FreeSlack);
            Assert.AreEqual(3, graphBuilder.Activity(activityId2).TotalSlack);
            Assert.AreEqual(3, graphBuilder.Activity(activityId2).LatestStartTime);
            Assert.AreEqual(10, graphBuilder.Activity(activityId2).LatestFinishTime);

            Assert.AreEqual(0, graphBuilder.Activity(activityId3).EarliestStartTime);
            Assert.AreEqual(8, graphBuilder.Activity(activityId3).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId3).FreeSlack);
            Assert.AreEqual(2, graphBuilder.Activity(activityId3).TotalSlack);
            Assert.AreEqual(2, graphBuilder.Activity(activityId3).LatestStartTime);
            Assert.AreEqual(10, graphBuilder.Activity(activityId3).LatestFinishTime);

            Assert.AreEqual(7, graphBuilder.Activity(activityId4).EarliestStartTime);
            Assert.AreEqual(18, graphBuilder.Activity(activityId4).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId4).FreeSlack);
            Assert.AreEqual(6, graphBuilder.Activity(activityId4).TotalSlack);
            Assert.AreEqual(13, graphBuilder.Activity(activityId4).LatestStartTime);
            Assert.AreEqual(24, graphBuilder.Activity(activityId4).LatestFinishTime);

            Assert.AreEqual(10, graphBuilder.Activity(activityId5).EarliestStartTime);
            Assert.AreEqual(18, graphBuilder.Activity(activityId5).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId5).FreeSlack);
            Assert.AreEqual(0, graphBuilder.Activity(activityId5).TotalSlack);
            Assert.AreEqual(10, graphBuilder.Activity(activityId5).LatestStartTime);
            Assert.AreEqual(18, graphBuilder.Activity(activityId5).LatestFinishTime);

            Assert.AreEqual(8, graphBuilder.Activity(activityId6).EarliestStartTime);
            Assert.AreEqual(15, graphBuilder.Activity(activityId6).EarliestFinishTime);
            Assert.AreEqual(3, graphBuilder.Activity(activityId6).FreeSlack);
            Assert.AreEqual(9, graphBuilder.Activity(activityId6).TotalSlack);
            Assert.AreEqual(17, graphBuilder.Activity(activityId6).LatestStartTime);
            Assert.AreEqual(24, graphBuilder.Activity(activityId6).LatestFinishTime);

            Assert.AreEqual(18, graphBuilder.Activity(activityId7).EarliestStartTime);
            Assert.AreEqual(22, graphBuilder.Activity(activityId7).EarliestFinishTime);
            Assert.AreEqual(6, graphBuilder.Activity(activityId7).FreeSlack);
            Assert.AreEqual(6, graphBuilder.Activity(activityId7).TotalSlack);
            Assert.AreEqual(24, graphBuilder.Activity(activityId7).LatestStartTime);
            Assert.AreEqual(28, graphBuilder.Activity(activityId7).LatestFinishTime);

            Assert.AreEqual(18, graphBuilder.Activity(activityId8).EarliestStartTime);
            Assert.AreEqual(22, graphBuilder.Activity(activityId8).EarliestFinishTime);
            Assert.AreEqual(6, graphBuilder.Activity(activityId8).FreeSlack);
            Assert.AreEqual(6, graphBuilder.Activity(activityId8).TotalSlack);
            Assert.AreEqual(24, graphBuilder.Activity(activityId8).LatestStartTime);
            Assert.AreEqual(28, graphBuilder.Activity(activityId8).LatestFinishTime);

            Assert.AreEqual(18, graphBuilder.Activity(activityId9).EarliestStartTime);
            Assert.AreEqual(28, graphBuilder.Activity(activityId9).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId9).FreeSlack);
            Assert.AreEqual(0, graphBuilder.Activity(activityId9).TotalSlack);
            Assert.AreEqual(18, graphBuilder.Activity(activityId9).LatestStartTime);
            Assert.AreEqual(28, graphBuilder.Activity(activityId9).LatestFinishTime);
        }

        [TestMethod]
        public void ArrowGraphBuilderExtensions_CalculateCriticalPathWithMinimumEarliestStartTimeInEndActivity_AsExpected()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int activityId6 = activityId5 + 1;
            int activityId7 = activityId6 + 1;
            int activityId8 = activityId7 + 1;
            int activityId9 = activityId8 + 1;
            int dummyActivityId = 100;
            var graphBuilder = new ArrowGraphBuilder<int, IActivity<int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            graphBuilder.AddActivity(new Activity<int>(activityId1, 6));
            graphBuilder.AddActivity(new Activity<int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int>(activityId4, 11), new HashSet<int>(new[] { 2 }));
            graphBuilder.AddActivity(new Activity<int>(activityId5, 8), new HashSet<int>(new[] { 1, 2, 3 }));
            graphBuilder.AddActivity(new Activity<int>(activityId6, 7), new HashSet<int>(new[] { 3 }));
            graphBuilder.AddActivity(new Activity<int>(activityId7, 4), new HashSet<int>(new[] { 4 }));
            graphBuilder.AddActivity(new Activity<int>(activityId8, 4), new HashSet<int>(new[] { 4, 6 }));
            graphBuilder.AddActivity(new Activity<int>(activityId9, 10) { MinimumEarliestStartTime = 20 }, new HashSet<int>(new[] { 5 }));

            graphBuilder.TransitiveReduction();
            graphBuilder.CalculateCriticalPath();

            Assert.AreEqual(0, graphBuilder.Activity(activityId1).EarliestStartTime);
            Assert.AreEqual(6, graphBuilder.Activity(activityId1).EarliestFinishTime);
            Assert.AreEqual(2, graphBuilder.Activity(activityId1).FreeSlack);
            Assert.AreEqual(6, graphBuilder.Activity(activityId1).TotalSlack);
            Assert.AreEqual(6, graphBuilder.Activity(activityId1).LatestStartTime);
            Assert.AreEqual(12, graphBuilder.Activity(activityId1).LatestFinishTime);

            Assert.AreEqual(0, graphBuilder.Activity(activityId2).EarliestStartTime);
            Assert.AreEqual(7, graphBuilder.Activity(activityId2).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId2).FreeSlack);
            Assert.AreEqual(5, graphBuilder.Activity(activityId2).TotalSlack);
            Assert.AreEqual(5, graphBuilder.Activity(activityId2).LatestStartTime);
            Assert.AreEqual(12, graphBuilder.Activity(activityId2).LatestFinishTime);

            Assert.AreEqual(0, graphBuilder.Activity(activityId3).EarliestStartTime);
            Assert.AreEqual(8, graphBuilder.Activity(activityId3).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId3).FreeSlack);
            Assert.AreEqual(4, graphBuilder.Activity(activityId3).TotalSlack);
            Assert.AreEqual(4, graphBuilder.Activity(activityId3).LatestStartTime);
            Assert.AreEqual(12, graphBuilder.Activity(activityId3).LatestFinishTime);

            Assert.AreEqual(7, graphBuilder.Activity(activityId4).EarliestStartTime);
            Assert.AreEqual(18, graphBuilder.Activity(activityId4).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId4).FreeSlack);
            Assert.AreEqual(8, graphBuilder.Activity(activityId4).TotalSlack);
            Assert.AreEqual(15, graphBuilder.Activity(activityId4).LatestStartTime);
            Assert.AreEqual(26, graphBuilder.Activity(activityId4).LatestFinishTime);

            Assert.AreEqual(8, graphBuilder.Activity(activityId5).EarliestStartTime);
            Assert.AreEqual(16, graphBuilder.Activity(activityId5).EarliestFinishTime);
            Assert.AreEqual(4, graphBuilder.Activity(activityId5).FreeSlack);
            Assert.AreEqual(4, graphBuilder.Activity(activityId5).TotalSlack);
            Assert.AreEqual(12, graphBuilder.Activity(activityId5).LatestStartTime);
            Assert.AreEqual(20, graphBuilder.Activity(activityId5).LatestFinishTime);

            Assert.AreEqual(8, graphBuilder.Activity(activityId6).EarliestStartTime);
            Assert.AreEqual(15, graphBuilder.Activity(activityId6).EarliestFinishTime);
            Assert.AreEqual(3, graphBuilder.Activity(activityId6).FreeSlack);
            Assert.AreEqual(11, graphBuilder.Activity(activityId6).TotalSlack);
            Assert.AreEqual(19, graphBuilder.Activity(activityId6).LatestStartTime);
            Assert.AreEqual(26, graphBuilder.Activity(activityId6).LatestFinishTime);

            Assert.AreEqual(18, graphBuilder.Activity(activityId7).EarliestStartTime);
            Assert.AreEqual(22, graphBuilder.Activity(activityId7).EarliestFinishTime);
            Assert.AreEqual(8, graphBuilder.Activity(activityId7).FreeSlack);
            Assert.AreEqual(8, graphBuilder.Activity(activityId7).TotalSlack);
            Assert.AreEqual(26, graphBuilder.Activity(activityId7).LatestStartTime);
            Assert.AreEqual(30, graphBuilder.Activity(activityId7).LatestFinishTime);

            Assert.AreEqual(18, graphBuilder.Activity(activityId8).EarliestStartTime);
            Assert.AreEqual(22, graphBuilder.Activity(activityId8).EarliestFinishTime);
            Assert.AreEqual(8, graphBuilder.Activity(activityId8).FreeSlack);
            Assert.AreEqual(8, graphBuilder.Activity(activityId8).TotalSlack);
            Assert.AreEqual(26, graphBuilder.Activity(activityId8).LatestStartTime);
            Assert.AreEqual(30, graphBuilder.Activity(activityId8).LatestFinishTime);

            Assert.AreEqual(20, graphBuilder.Activity(activityId9).EarliestStartTime);
            Assert.AreEqual(30, graphBuilder.Activity(activityId9).EarliestFinishTime);
            Assert.AreEqual(0, graphBuilder.Activity(activityId9).FreeSlack);
            Assert.AreEqual(0, graphBuilder.Activity(activityId9).TotalSlack);
            Assert.AreEqual(20, graphBuilder.Activity(activityId9).LatestStartTime);
            Assert.AreEqual(30, graphBuilder.Activity(activityId9).LatestFinishTime);
        }

        [TestMethod]
        public void ArrowGraphBuilderExtensions_CalculateCriticalPathPriorityList_CorrectOrder()
        {
            int eventId = 0;
            int dummyActivityId = 100;
            var graphBuilder = new ArrowGraphBuilder<int, IActivity<int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            graphBuilder.AddActivity(new Activity<int>(1, 6));
            graphBuilder.AddActivity(new Activity<int>(2, 7));
            graphBuilder.AddActivity(new Activity<int>(3, 8));
            graphBuilder.AddActivity(new Activity<int>(4, 11), new HashSet<int>(new[] { 2 }));
            graphBuilder.AddActivity(new Activity<int>(5, 8), new HashSet<int>(new[] { 1, 2, 3 }));
            graphBuilder.AddActivity(new Activity<int>(6, 7), new HashSet<int>(new[] { 3 }));
            graphBuilder.AddActivity(new Activity<int>(7, 4), new HashSet<int>(new[] { 4 }));
            graphBuilder.AddActivity(new Activity<int>(8, 4), new HashSet<int>(new[] { 4, 6 }));
            graphBuilder.AddActivity(new Activity<int>(9, 10), new HashSet<int>(new[] { 5 }));

            List<int> priorityList = graphBuilder.CalculateCriticalPathPriorityList().ToList();

            CollectionAssert.AreEqual(
                new List<int>(new[] { 3, 2, 1, 5, 4, 6, 9, 7, 8 }),
                priorityList);
        }

        [TestMethod]
        public void ArrowGraphBuilderExtensions_CalculateResourceSchedulesByPriorityListOneResource_CorrectOrder()
        {
            int eventId = 0;
            int dummyActivityId = 100;
            var graphBuilder = new ArrowGraphBuilder<int, IActivity<int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            graphBuilder.AddActivity(new Activity<int>(1, 6));
            graphBuilder.AddActivity(new Activity<int>(2, 7));
            graphBuilder.AddActivity(new Activity<int>(3, 8));
            graphBuilder.AddActivity(new Activity<int>(4, 11), new HashSet<int>(new[] { 2 }));
            graphBuilder.AddActivity(new Activity<int>(5, 8), new HashSet<int>(new[] { 1, 2, 3 }));
            graphBuilder.AddActivity(new Activity<int>(6, 7), new HashSet<int>(new[] { 3 }));
            graphBuilder.AddActivity(new Activity<int>(7, 4), new HashSet<int>(new[] { 4 }));
            graphBuilder.AddActivity(new Activity<int>(8, 4), new HashSet<int>(new[] { 4, 6 }));
            graphBuilder.AddActivity(new Activity<int>(9, 10), new HashSet<int>(new[] { 5 }));

            IList<IResourceSchedule<int>> resourceSchedule =
                graphBuilder.CalculateResourceSchedulesByPriorityList(
                    new List<IResource<int>>(new[]
                    {
                        new Resource<int>(1, string.Empty, false, InterActivityAllocationType.None, 1.0, 0)
                    })).ToList();
            Assert.AreEqual(1, resourceSchedule.Count);

            Assert.AreEqual(9, resourceSchedule[0].ScheduledActivities.Count);
            Assert.AreEqual(3, resourceSchedule[0].ScheduledActivities[0].Id);
            Assert.AreEqual(0, resourceSchedule[0].ScheduledActivities[0].StartTime);
            Assert.AreEqual(8, resourceSchedule[0].ScheduledActivities[0].FinishTime);

            Assert.AreEqual(2, resourceSchedule[0].ScheduledActivities[1].Id);
            Assert.AreEqual(8, resourceSchedule[0].ScheduledActivities[1].StartTime);
            Assert.AreEqual(15, resourceSchedule[0].ScheduledActivities[1].FinishTime);

            Assert.AreEqual(1, resourceSchedule[0].ScheduledActivities[2].Id);
            Assert.AreEqual(15, resourceSchedule[0].ScheduledActivities[2].StartTime);
            Assert.AreEqual(21, resourceSchedule[0].ScheduledActivities[2].FinishTime);

            Assert.AreEqual(5, resourceSchedule[0].ScheduledActivities[3].Id);
            Assert.AreEqual(21, resourceSchedule[0].ScheduledActivities[3].StartTime);
            Assert.AreEqual(29, resourceSchedule[0].ScheduledActivities[3].FinishTime);

            Assert.AreEqual(4, resourceSchedule[0].ScheduledActivities[4].Id);
            Assert.AreEqual(29, resourceSchedule[0].ScheduledActivities[4].StartTime);
            Assert.AreEqual(40, resourceSchedule[0].ScheduledActivities[4].FinishTime);

            Assert.AreEqual(6, resourceSchedule[0].ScheduledActivities[5].Id);
            Assert.AreEqual(40, resourceSchedule[0].ScheduledActivities[5].StartTime);
            Assert.AreEqual(47, resourceSchedule[0].ScheduledActivities[5].FinishTime);

            Assert.AreEqual(9, resourceSchedule[0].ScheduledActivities[6].Id);
            Assert.AreEqual(47, resourceSchedule[0].ScheduledActivities[6].StartTime);
            Assert.AreEqual(57, resourceSchedule[0].ScheduledActivities[6].FinishTime);

            Assert.AreEqual(7, resourceSchedule[0].ScheduledActivities[7].Id);
            Assert.AreEqual(57, resourceSchedule[0].ScheduledActivities[7].StartTime);
            Assert.AreEqual(61, resourceSchedule[0].ScheduledActivities[7].FinishTime);

            Assert.AreEqual(8, resourceSchedule[0].ScheduledActivities[8].Id);
            Assert.AreEqual(61, resourceSchedule[0].ScheduledActivities[8].StartTime);
            Assert.AreEqual(65, resourceSchedule[0].ScheduledActivities[8].FinishTime);

            Assert.AreEqual(65, resourceSchedule[0].ScheduledActivities.Last().FinishTime);
        }

        [TestMethod]
        public void ArrowGraphBuilderExtensions_CalculateResourceSchedulesByPriorityListTwoResources_CorrectOrder()
        {
            int eventId = 0;
            int dummyActivityId = 100;
            var graphBuilder = new ArrowGraphBuilder<int, IActivity<int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            graphBuilder.AddActivity(new Activity<int>(1, 6));
            graphBuilder.AddActivity(new Activity<int>(2, 7));
            graphBuilder.AddActivity(new Activity<int>(3, 8));
            graphBuilder.AddActivity(new Activity<int>(4, 11), new HashSet<int>(new[] { 2 }));
            graphBuilder.AddActivity(new Activity<int>(5, 8), new HashSet<int>(new[] { 1, 2, 3 }));
            graphBuilder.AddActivity(new Activity<int>(6, 7), new HashSet<int>(new[] { 3 }));
            graphBuilder.AddActivity(new Activity<int>(7, 4), new HashSet<int>(new[] { 4 }));
            graphBuilder.AddActivity(new Activity<int>(8, 4), new HashSet<int>(new[] { 4, 6 }));
            graphBuilder.AddActivity(new Activity<int>(9, 10), new HashSet<int>(new[] { 5 }));

            IList<IResourceSchedule<int>> resourceSchedules =
                graphBuilder.CalculateResourceSchedulesByPriorityList(
                    new List<IResource<int>>(new[]
                    {
                        new Resource<int>(1, string.Empty, false, InterActivityAllocationType.None, 1.0, 0),
                        new Resource<int>(2, string.Empty, false, InterActivityAllocationType.None, 1.0, 0)
                    })).ToList();
            Assert.AreEqual(2, resourceSchedules.Count);

            Assert.AreEqual(5, resourceSchedules[0].ScheduledActivities.Count);
            Assert.AreEqual(3, resourceSchedules[0].ScheduledActivities[0].Id);
            Assert.AreEqual(0, resourceSchedules[0].ScheduledActivities[0].StartTime);
            Assert.AreEqual(8, resourceSchedules[0].ScheduledActivities[0].FinishTime);

            Assert.AreEqual(4, resourceSchedules[0].ScheduledActivities[1].Id);
            Assert.AreEqual(8, resourceSchedules[0].ScheduledActivities[1].StartTime);
            Assert.AreEqual(19, resourceSchedules[0].ScheduledActivities[1].FinishTime);

            Assert.AreEqual(6, resourceSchedules[0].ScheduledActivities[2].Id);
            Assert.AreEqual(19, resourceSchedules[0].ScheduledActivities[2].StartTime);
            Assert.AreEqual(26, resourceSchedules[0].ScheduledActivities[2].FinishTime);

            Assert.AreEqual(7, resourceSchedules[0].ScheduledActivities[3].Id);
            Assert.AreEqual(26, resourceSchedules[0].ScheduledActivities[3].StartTime);
            Assert.AreEqual(30, resourceSchedules[0].ScheduledActivities[3].FinishTime);

            Assert.AreEqual(8, resourceSchedules[0].ScheduledActivities[4].Id);
            Assert.AreEqual(30, resourceSchedules[0].ScheduledActivities[4].StartTime);
            Assert.AreEqual(34, resourceSchedules[0].ScheduledActivities[4].FinishTime);

            Assert.AreEqual(34, resourceSchedules[0].ScheduledActivities.Last().FinishTime);

            Assert.AreEqual(4, resourceSchedules[1].ScheduledActivities.Count());

            Assert.AreEqual(2, resourceSchedules[1].ScheduledActivities[0].Id);
            Assert.AreEqual(0, resourceSchedules[1].ScheduledActivities[0].StartTime);
            Assert.AreEqual(7, resourceSchedules[1].ScheduledActivities[0].FinishTime);

            Assert.AreEqual(1, resourceSchedules[1].ScheduledActivities[1].Id);
            Assert.AreEqual(7, resourceSchedules[1].ScheduledActivities[1].StartTime);
            Assert.AreEqual(13, resourceSchedules[1].ScheduledActivities[1].FinishTime);

            Assert.AreEqual(5, resourceSchedules[1].ScheduledActivities[2].Id);
            Assert.AreEqual(13, resourceSchedules[1].ScheduledActivities[2].StartTime);
            Assert.AreEqual(21, resourceSchedules[1].ScheduledActivities[2].FinishTime);

            Assert.AreEqual(9, resourceSchedules[1].ScheduledActivities[3].Id);
            Assert.AreEqual(21, resourceSchedules[1].ScheduledActivities[3].StartTime);
            Assert.AreEqual(31, resourceSchedules[1].ScheduledActivities[3].FinishTime);

            Assert.AreEqual(31, resourceSchedules[1].ScheduledActivities.Last().FinishTime);
        }

        [TestMethod]
        public void ArrowGraphBuilderExtensions_CalculateResourceSchedulesByPriorityListThreeResources_CorrectOrder()
        {
            int eventId = 0;
            int dummyActivityId = 100;
            var graphBuilder = new ArrowGraphBuilder<int, IActivity<int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            graphBuilder.AddActivity(new Activity<int>(1, 6));
            graphBuilder.AddActivity(new Activity<int>(2, 7));
            graphBuilder.AddActivity(new Activity<int>(3, 8));
            graphBuilder.AddActivity(new Activity<int>(4, 11), new HashSet<int>(new[] { 2 }));
            graphBuilder.AddActivity(new Activity<int>(5, 8), new HashSet<int>(new[] { 1, 2, 3 }));
            graphBuilder.AddActivity(new Activity<int>(6, 7), new HashSet<int>(new[] { 3 }));
            graphBuilder.AddActivity(new Activity<int>(7, 4), new HashSet<int>(new[] { 4 }));
            graphBuilder.AddActivity(new Activity<int>(8, 4), new HashSet<int>(new[] { 4, 6 }));
            graphBuilder.AddActivity(new Activity<int>(9, 10), new HashSet<int>(new[] { 5 }));

            IList<IResourceSchedule<int>> resourceSchedules =
                graphBuilder.CalculateResourceSchedulesByPriorityList(
                    new List<IResource<int>>(new[]
                    {
                        new Resource<int>(1, string.Empty, false, InterActivityAllocationType.None, 1.0, 0),
                        new Resource<int>(2, string.Empty, false, InterActivityAllocationType.None, 1.0, 0),
                        new Resource<int>(3, string.Empty, false, InterActivityAllocationType.None, 1.0, 0)
                    })).ToList();
            Assert.AreEqual(3, resourceSchedules.Count);

            Assert.AreEqual(3, resourceSchedules[0].ScheduledActivities.Count);
            Assert.AreEqual(3, resourceSchedules[0].ScheduledActivities[0].Id);
            Assert.AreEqual(0, resourceSchedules[0].ScheduledActivities[0].StartTime);
            Assert.AreEqual(8, resourceSchedules[0].ScheduledActivities[0].FinishTime);

            Assert.AreEqual(5, resourceSchedules[0].ScheduledActivities[1].Id);
            Assert.AreEqual(8, resourceSchedules[0].ScheduledActivities[1].StartTime);
            Assert.AreEqual(16, resourceSchedules[0].ScheduledActivities[1].FinishTime);

            Assert.AreEqual(9, resourceSchedules[0].ScheduledActivities[2].Id);
            Assert.AreEqual(16, resourceSchedules[0].ScheduledActivities[2].StartTime);
            Assert.AreEqual(26, resourceSchedules[0].ScheduledActivities[2].FinishTime);

            Assert.AreEqual(26, resourceSchedules[0].ScheduledActivities.Last().FinishTime);

            Assert.AreEqual(3, resourceSchedules[1].ScheduledActivities.Count());
            Assert.AreEqual(2, resourceSchedules[1].ScheduledActivities[0].Id);
            Assert.AreEqual(0, resourceSchedules[1].ScheduledActivities[0].StartTime);
            Assert.AreEqual(7, resourceSchedules[1].ScheduledActivities[0].FinishTime);

            Assert.AreEqual(4, resourceSchedules[1].ScheduledActivities[1].Id);
            Assert.AreEqual(7, resourceSchedules[1].ScheduledActivities[1].StartTime);
            Assert.AreEqual(18, resourceSchedules[1].ScheduledActivities[1].FinishTime);

            Assert.AreEqual(7, resourceSchedules[1].ScheduledActivities[2].Id);
            Assert.AreEqual(18, resourceSchedules[1].ScheduledActivities[2].StartTime);
            Assert.AreEqual(22, resourceSchedules[1].ScheduledActivities[2].FinishTime);

            Assert.AreEqual(22, resourceSchedules[1].ScheduledActivities.Last().FinishTime);


            Assert.AreEqual(3, resourceSchedules[2].ScheduledActivities.Count());
            Assert.AreEqual(1, resourceSchedules[2].ScheduledActivities[0].Id);
            Assert.AreEqual(0, resourceSchedules[2].ScheduledActivities[0].StartTime);
            Assert.AreEqual(6, resourceSchedules[2].ScheduledActivities[0].FinishTime);

            Assert.AreEqual(6, resourceSchedules[2].ScheduledActivities[1].Id);
            Assert.AreEqual(8, resourceSchedules[2].ScheduledActivities[1].StartTime);
            Assert.AreEqual(15, resourceSchedules[2].ScheduledActivities[1].FinishTime);

            Assert.AreEqual(8, resourceSchedules[2].ScheduledActivities[2].Id);
            Assert.AreEqual(18, resourceSchedules[2].ScheduledActivities[2].StartTime);
            Assert.AreEqual(22, resourceSchedules[2].ScheduledActivities[2].FinishTime);

            Assert.AreEqual(22, resourceSchedules[2].ScheduledActivities.Last().FinishTime);
        }

        [TestMethod]
        public void ArrowGraphBuilderExtensions_CalculateResourceSchedulesByPriorityListFourResources_CorrectOrder()
        {
            int eventId = 0;
            int dummyActivityId = 100;
            var graphBuilder = new ArrowGraphBuilder<int, IActivity<int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            graphBuilder.AddActivity(new Activity<int>(1, 6));
            graphBuilder.AddActivity(new Activity<int>(2, 7));
            graphBuilder.AddActivity(new Activity<int>(3, 8));
            graphBuilder.AddActivity(new Activity<int>(4, 11), new HashSet<int>(new[] { 2 }));
            graphBuilder.AddActivity(new Activity<int>(5, 8), new HashSet<int>(new[] { 1, 2, 3 }));
            graphBuilder.AddActivity(new Activity<int>(6, 7), new HashSet<int>(new[] { 3 }));
            graphBuilder.AddActivity(new Activity<int>(7, 4), new HashSet<int>(new[] { 4 }));
            graphBuilder.AddActivity(new Activity<int>(8, 4), new HashSet<int>(new[] { 4, 6 }));
            graphBuilder.AddActivity(new Activity<int>(9, 10), new HashSet<int>(new[] { 5 }));

            IList<IResourceSchedule<int>> resourceSchedules =
                graphBuilder.CalculateResourceSchedulesByPriorityList(
                    new List<IResource<int>>(new[]
                    {
                        new Resource<int>(1, string.Empty, false, InterActivityAllocationType.None, 1.0, 0),
                        new Resource<int>(2, string.Empty, false, InterActivityAllocationType.None, 1.0, 0),
                        new Resource<int>(3, string.Empty, false, InterActivityAllocationType.None, 1.0, 0),
                        new Resource<int>(4, string.Empty, false, InterActivityAllocationType.None, 1.0, 0)
                    })).ToList();
            Assert.AreEqual(3, resourceSchedules.Count);

            Assert.AreEqual(3, resourceSchedules[0].ScheduledActivities.Count);
            Assert.AreEqual(3, resourceSchedules[0].ScheduledActivities[0].Id);
            Assert.AreEqual(0, resourceSchedules[0].ScheduledActivities[0].StartTime);
            Assert.AreEqual(8, resourceSchedules[0].ScheduledActivities[0].FinishTime);

            Assert.AreEqual(5, resourceSchedules[0].ScheduledActivities[1].Id);
            Assert.AreEqual(8, resourceSchedules[0].ScheduledActivities[1].StartTime);
            Assert.AreEqual(16, resourceSchedules[0].ScheduledActivities[1].FinishTime);

            Assert.AreEqual(9, resourceSchedules[0].ScheduledActivities[2].Id);
            Assert.AreEqual(16, resourceSchedules[0].ScheduledActivities[2].StartTime);
            Assert.AreEqual(26, resourceSchedules[0].ScheduledActivities[2].FinishTime);

            Assert.AreEqual(26, resourceSchedules[0].ScheduledActivities.Last().FinishTime);

            Assert.AreEqual(3, resourceSchedules[1].ScheduledActivities.Count());
            Assert.AreEqual(2, resourceSchedules[1].ScheduledActivities[0].Id);
            Assert.AreEqual(0, resourceSchedules[1].ScheduledActivities[0].StartTime);
            Assert.AreEqual(7, resourceSchedules[1].ScheduledActivities[0].FinishTime);

            Assert.AreEqual(4, resourceSchedules[1].ScheduledActivities[1].Id);
            Assert.AreEqual(7, resourceSchedules[1].ScheduledActivities[1].StartTime);
            Assert.AreEqual(18, resourceSchedules[1].ScheduledActivities[1].FinishTime);

            Assert.AreEqual(7, resourceSchedules[1].ScheduledActivities[2].Id);
            Assert.AreEqual(18, resourceSchedules[1].ScheduledActivities[2].StartTime);
            Assert.AreEqual(22, resourceSchedules[1].ScheduledActivities[2].FinishTime);

            Assert.AreEqual(22, resourceSchedules[1].ScheduledActivities.Last().FinishTime);


            Assert.AreEqual(3, resourceSchedules[2].ScheduledActivities.Count());
            Assert.AreEqual(1, resourceSchedules[2].ScheduledActivities[0].Id);
            Assert.AreEqual(0, resourceSchedules[2].ScheduledActivities[0].StartTime);
            Assert.AreEqual(6, resourceSchedules[2].ScheduledActivities[0].FinishTime);

            Assert.AreEqual(6, resourceSchedules[2].ScheduledActivities[1].Id);
            Assert.AreEqual(8, resourceSchedules[2].ScheduledActivities[1].StartTime);
            Assert.AreEqual(15, resourceSchedules[2].ScheduledActivities[1].FinishTime);

            Assert.AreEqual(8, resourceSchedules[2].ScheduledActivities[2].Id);
            Assert.AreEqual(18, resourceSchedules[2].ScheduledActivities[2].StartTime);
            Assert.AreEqual(22, resourceSchedules[2].ScheduledActivities[2].FinishTime);

            Assert.AreEqual(22, resourceSchedules[2].ScheduledActivities.Last().FinishTime);
        }

        [TestMethod]
        public void ArrowGraphBuilderExtensions_CalculateResourceSchedulesByPriorityListUnlimitedResources_CorrectOrder()
        {
            int eventId = 0;
            int dummyActivityId = 100;
            var graphBuilder = new ArrowGraphBuilder<int, IActivity<int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            graphBuilder.AddActivity(new Activity<int>(1, 6));
            graphBuilder.AddActivity(new Activity<int>(2, 7));
            graphBuilder.AddActivity(new Activity<int>(3, 8));
            graphBuilder.AddActivity(new Activity<int>(4, 11), new HashSet<int>(new[] { 2 }));
            graphBuilder.AddActivity(new Activity<int>(5, 8), new HashSet<int>(new[] { 1, 2, 3 }));
            graphBuilder.AddActivity(new Activity<int>(6, 7), new HashSet<int>(new[] { 3 }));
            graphBuilder.AddActivity(new Activity<int>(7, 4), new HashSet<int>(new[] { 4 }));
            graphBuilder.AddActivity(new Activity<int>(8, 4), new HashSet<int>(new[] { 4, 6 }));
            graphBuilder.AddActivity(new Activity<int>(9, 10), new HashSet<int>(new[] { 5 }));

            IList<IResourceSchedule<int>> resourceSchedules = graphBuilder.CalculateResourceSchedulesByPriorityList().ToList();
            Assert.AreEqual(3, resourceSchedules.Count);

            Assert.AreEqual(3, resourceSchedules[0].ScheduledActivities.Count());
            Assert.AreEqual(3, resourceSchedules[0].ScheduledActivities[0].Id);
            Assert.AreEqual(0, resourceSchedules[0].ScheduledActivities[0].StartTime);
            Assert.AreEqual(8, resourceSchedules[0].ScheduledActivities[0].FinishTime);

            Assert.AreEqual(5, resourceSchedules[0].ScheduledActivities[1].Id);
            Assert.AreEqual(8, resourceSchedules[0].ScheduledActivities[1].StartTime);
            Assert.AreEqual(16, resourceSchedules[0].ScheduledActivities[1].FinishTime);

            Assert.AreEqual(9, resourceSchedules[0].ScheduledActivities[2].Id);
            Assert.AreEqual(16, resourceSchedules[0].ScheduledActivities[2].StartTime);
            Assert.AreEqual(26, resourceSchedules[0].ScheduledActivities[2].FinishTime);

            Assert.AreEqual(26, resourceSchedules[0].ScheduledActivities.Last().FinishTime);

            Assert.AreEqual(3, resourceSchedules[1].ScheduledActivities.Count());
            Assert.AreEqual(2, resourceSchedules[1].ScheduledActivities[0].Id);
            Assert.AreEqual(0, resourceSchedules[1].ScheduledActivities[0].StartTime);
            Assert.AreEqual(7, resourceSchedules[1].ScheduledActivities[0].FinishTime);

            Assert.AreEqual(4, resourceSchedules[1].ScheduledActivities[1].Id);
            Assert.AreEqual(7, resourceSchedules[1].ScheduledActivities[1].StartTime);
            Assert.AreEqual(18, resourceSchedules[1].ScheduledActivities[1].FinishTime);

            Assert.AreEqual(7, resourceSchedules[1].ScheduledActivities[2].Id);
            Assert.AreEqual(18, resourceSchedules[1].ScheduledActivities[2].StartTime);
            Assert.AreEqual(22, resourceSchedules[1].ScheduledActivities[2].FinishTime);

            Assert.AreEqual(22, resourceSchedules[1].ScheduledActivities.Last().FinishTime);


            Assert.AreEqual(3, resourceSchedules[2].ScheduledActivities.Count());
            Assert.AreEqual(1, resourceSchedules[2].ScheduledActivities[0].Id);
            Assert.AreEqual(0, resourceSchedules[2].ScheduledActivities[0].StartTime);
            Assert.AreEqual(6, resourceSchedules[2].ScheduledActivities[0].FinishTime);

            Assert.AreEqual(6, resourceSchedules[2].ScheduledActivities[1].Id);
            Assert.AreEqual(8, resourceSchedules[2].ScheduledActivities[1].StartTime);
            Assert.AreEqual(15, resourceSchedules[2].ScheduledActivities[1].FinishTime);

            Assert.AreEqual(8, resourceSchedules[2].ScheduledActivities[2].Id);
            Assert.AreEqual(18, resourceSchedules[2].ScheduledActivities[2].StartTime);
            Assert.AreEqual(22, resourceSchedules[2].ScheduledActivities[2].FinishTime);

            Assert.AreEqual(22, resourceSchedules[2].ScheduledActivities.Last().FinishTime);
        }
    }
}
