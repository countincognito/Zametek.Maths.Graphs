using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class VertexGraphBuilderExtensionsTests
    {
        [Fact]
        public void VertexGraphBuilderExtensions_GivenCalculateCriticalPathForwardFlow_ThenAsExpected()
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
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId1, 6));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId4, 11), new HashSet<int> { activityId2 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId5, 8), new HashSet<int> { activityId1, activityId2, activityId3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId6, 7), new HashSet<int> { activityId3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId7, 4), new HashSet<int> { activityId4 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId8, 4), new HashSet<int> { activityId4, activityId6 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId9, 10), new HashSet<int> { activityId5 });

            graphBuilder.CalculateCriticalPathForwardFlow();

            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(6);

            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(7);

            graphBuilder.Activity(activityId3).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.ShouldBe(8);

            graphBuilder.Activity(activityId4).EarliestStartTime.ShouldBe(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.ShouldBe(18);

            graphBuilder.Activity(activityId5).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId5).EarliestFinishTime.ShouldBe(16);

            graphBuilder.Activity(activityId6).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.ShouldBe(15);

            graphBuilder.Activity(activityId7).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.ShouldBe(22);

            graphBuilder.Activity(activityId8).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId8).EarliestFinishTime.ShouldBe(22);

            graphBuilder.Activity(activityId9).EarliestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId9).EarliestFinishTime.ShouldBe(26);
        }

        [Fact]
        public void VertexGraphBuilderExtensions_GivenCalculateCriticalPathBackwardFlow_ThenAsExpected()
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
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId1, 6));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId4, 11), new HashSet<int> { activityId2 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId5, 8), new HashSet<int> { activityId1, activityId2, activityId3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId6, 7), new HashSet<int> { activityId3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId7, 4), new HashSet<int> { activityId4 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId8, 4), new HashSet<int> { activityId4, activityId6 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId9, 10), new HashSet<int> { activityId5 });

            graphBuilder.CalculateCriticalPathForwardFlow();
            graphBuilder.CalculateCriticalPathBackwardFlow();

            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(2);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(8);

            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(1);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(8);

            graphBuilder.Activity(activityId3).LatestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestFinishTime.ShouldBe(8);

            graphBuilder.Activity(activityId4).LatestStartTime.ShouldBe(11);
            graphBuilder.Activity(activityId4).LatestFinishTime.ShouldBe(22);

            graphBuilder.Activity(activityId5).LatestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId5).LatestFinishTime.ShouldBe(16);

            graphBuilder.Activity(activityId6).LatestStartTime.ShouldBe(15);
            graphBuilder.Activity(activityId6).LatestFinishTime.ShouldBe(22);

            graphBuilder.Activity(activityId7).LatestStartTime.ShouldBe(22);
            graphBuilder.Activity(activityId7).LatestFinishTime.ShouldBe(26);

            graphBuilder.Activity(activityId8).LatestStartTime.ShouldBe(22);
            graphBuilder.Activity(activityId8).LatestFinishTime.ShouldBe(26);

            graphBuilder.Activity(activityId9).LatestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId9).LatestFinishTime.ShouldBe(26);
        }

        [Fact]
        public void VertexGraphBuilderExtensions_GivenCalculateCriticalPath_ThenAsExpected()
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
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId1, 6));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId4, 11), new HashSet<int> { activityId2 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId5, 8), new HashSet<int> { activityId1, activityId2, activityId3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId6, 7), new HashSet<int> { activityId3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId7, 4), new HashSet<int> { activityId4 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId8, 4), new HashSet<int> { activityId4, activityId6 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId9, 10), new HashSet<int> { activityId5 });

            graphBuilder.CalculateCriticalPath();

            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(6);
            graphBuilder.Activity(activityId1).FreeSlack.ShouldBe(2);
            graphBuilder.Activity(activityId1).TotalSlack.ShouldBe(2);
            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(2);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(8);

            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(7);
            graphBuilder.Activity(activityId2).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId2).TotalSlack.ShouldBe(1);
            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(1);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(8);

            graphBuilder.Activity(activityId3).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestFinishTime.ShouldBe(8);

            graphBuilder.Activity(activityId4).EarliestStartTime.ShouldBe(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.ShouldBe(18);
            graphBuilder.Activity(activityId4).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).TotalSlack.ShouldBe(4);
            graphBuilder.Activity(activityId4).LatestStartTime.ShouldBe(11);
            graphBuilder.Activity(activityId4).LatestFinishTime.ShouldBe(22);

            graphBuilder.Activity(activityId5).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId5).EarliestFinishTime.ShouldBe(16);
            graphBuilder.Activity(activityId5).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).LatestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId5).LatestFinishTime.ShouldBe(16);

            graphBuilder.Activity(activityId6).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.ShouldBe(15);
            graphBuilder.Activity(activityId6).FreeSlack.ShouldBe(3);
            graphBuilder.Activity(activityId6).TotalSlack.ShouldBe(7);
            graphBuilder.Activity(activityId6).LatestStartTime.ShouldBe(15);
            graphBuilder.Activity(activityId6).LatestFinishTime.ShouldBe(22);

            graphBuilder.Activity(activityId7).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId7).FreeSlack.ShouldBe(4);
            graphBuilder.Activity(activityId7).TotalSlack.ShouldBe(4);
            graphBuilder.Activity(activityId7).LatestStartTime.ShouldBe(22);
            graphBuilder.Activity(activityId7).LatestFinishTime.ShouldBe(26);

            graphBuilder.Activity(activityId8).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId8).EarliestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId8).FreeSlack.ShouldBe(4);
            graphBuilder.Activity(activityId8).TotalSlack.ShouldBe(4);
            graphBuilder.Activity(activityId8).LatestStartTime.ShouldBe(22);
            graphBuilder.Activity(activityId8).LatestFinishTime.ShouldBe(26);

            graphBuilder.Activity(activityId9).EarliestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId9).EarliestFinishTime.ShouldBe(26);
            graphBuilder.Activity(activityId9).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).LatestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId9).LatestFinishTime.ShouldBe(26);
        }

        [Fact]
        public void VertexGraphBuilderExtensions_GivenCalculateCriticalPath_WhenMinimumFreeSlackInStartActivity_ThenAsExpected()
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
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId1, 6) { MinimumFreeSlack = 10 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId4, 11), new HashSet<int> { activityId2 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId5, 8), new HashSet<int> { activityId1, activityId2, activityId3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId6, 7), new HashSet<int> { activityId3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId7, 4), new HashSet<int> { activityId4 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId8, 4), new HashSet<int> { activityId4, activityId6 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId9, 10), new HashSet<int> { activityId5 });

            graphBuilder.CalculateCriticalPath();

            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(6);
            graphBuilder.Activity(activityId1).FreeSlack.ShouldBe(10);
            graphBuilder.Activity(activityId1).TotalSlack.ShouldBe(10);
            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(10);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(16);

            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(7);
            graphBuilder.Activity(activityId2).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId2).TotalSlack.ShouldBe(9);
            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(9);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(16);

            graphBuilder.Activity(activityId3).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).TotalSlack.ShouldBe(8);
            graphBuilder.Activity(activityId3).LatestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).LatestFinishTime.ShouldBe(16);

            graphBuilder.Activity(activityId4).EarliestStartTime.ShouldBe(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.ShouldBe(18);
            graphBuilder.Activity(activityId4).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).TotalSlack.ShouldBe(12);
            graphBuilder.Activity(activityId4).LatestStartTime.ShouldBe(19);
            graphBuilder.Activity(activityId4).LatestFinishTime.ShouldBe(30);

            graphBuilder.Activity(activityId5).EarliestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId5).EarliestFinishTime.ShouldBe(24);
            graphBuilder.Activity(activityId5).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).LatestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId5).LatestFinishTime.ShouldBe(24);

            graphBuilder.Activity(activityId6).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.ShouldBe(15);
            graphBuilder.Activity(activityId6).FreeSlack.ShouldBe(3);
            graphBuilder.Activity(activityId6).TotalSlack.ShouldBe(15);
            graphBuilder.Activity(activityId6).LatestStartTime.ShouldBe(23);
            graphBuilder.Activity(activityId6).LatestFinishTime.ShouldBe(30);

            graphBuilder.Activity(activityId7).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId7).FreeSlack.ShouldBe(12);
            graphBuilder.Activity(activityId7).TotalSlack.ShouldBe(12);
            graphBuilder.Activity(activityId7).LatestStartTime.ShouldBe(30);
            graphBuilder.Activity(activityId7).LatestFinishTime.ShouldBe(34);

            graphBuilder.Activity(activityId8).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId8).EarliestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId8).FreeSlack.ShouldBe(12);
            graphBuilder.Activity(activityId8).TotalSlack.ShouldBe(12);
            graphBuilder.Activity(activityId8).LatestStartTime.ShouldBe(30);
            graphBuilder.Activity(activityId8).LatestFinishTime.ShouldBe(34);

            graphBuilder.Activity(activityId9).EarliestStartTime.ShouldBe(24);
            graphBuilder.Activity(activityId9).EarliestFinishTime.ShouldBe(34);
            graphBuilder.Activity(activityId9).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).LatestStartTime.ShouldBe(24);
            graphBuilder.Activity(activityId9).LatestFinishTime.ShouldBe(34);
        }

        [Fact]
        public void VertexGraphBuilderExtensions_GivenCalculateCriticalPath_WhenMinimumFreeSlackInNormalActivity_ThenAsExpected()
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
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId1, 6));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId4, 11), new HashSet<int> { activityId2 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId5, 8) { MinimumFreeSlack = 15 }, new HashSet<int> { activityId1, activityId2, activityId3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId6, 7), new HashSet<int> { activityId3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId7, 4), new HashSet<int> { activityId4 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId8, 4), new HashSet<int> { activityId4, activityId6 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId9, 10), new HashSet<int> { activityId5 });

            graphBuilder.CalculateCriticalPath();

            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(6);
            graphBuilder.Activity(activityId1).FreeSlack.ShouldBe(2);
            graphBuilder.Activity(activityId1).TotalSlack.ShouldBe(17);
            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(17);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(23);

            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(7);
            graphBuilder.Activity(activityId2).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId2).TotalSlack.ShouldBe(16);
            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(23);

            graphBuilder.Activity(activityId3).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).TotalSlack.ShouldBe(15);
            graphBuilder.Activity(activityId3).LatestStartTime.ShouldBe(15);
            graphBuilder.Activity(activityId3).LatestFinishTime.ShouldBe(23);

            graphBuilder.Activity(activityId4).EarliestStartTime.ShouldBe(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.ShouldBe(18);
            graphBuilder.Activity(activityId4).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).TotalSlack.ShouldBe(19);
            graphBuilder.Activity(activityId4).LatestStartTime.ShouldBe(26);
            graphBuilder.Activity(activityId4).LatestFinishTime.ShouldBe(37);

            graphBuilder.Activity(activityId5).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId5).EarliestFinishTime.ShouldBe(16);
            graphBuilder.Activity(activityId5).FreeSlack.ShouldBe(15);
            graphBuilder.Activity(activityId5).TotalSlack.ShouldBe(15);
            graphBuilder.Activity(activityId5).LatestStartTime.ShouldBe(23);
            graphBuilder.Activity(activityId5).LatestFinishTime.ShouldBe(31);

            graphBuilder.Activity(activityId6).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.ShouldBe(15);
            graphBuilder.Activity(activityId6).FreeSlack.ShouldBe(3);
            graphBuilder.Activity(activityId6).TotalSlack.ShouldBe(22);
            graphBuilder.Activity(activityId6).LatestStartTime.ShouldBe(30);
            graphBuilder.Activity(activityId6).LatestFinishTime.ShouldBe(37);

            graphBuilder.Activity(activityId7).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId7).FreeSlack.ShouldBe(19);
            graphBuilder.Activity(activityId7).TotalSlack.ShouldBe(19);
            graphBuilder.Activity(activityId7).LatestStartTime.ShouldBe(37);
            graphBuilder.Activity(activityId7).LatestFinishTime.ShouldBe(41);

            graphBuilder.Activity(activityId8).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId8).EarliestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId8).FreeSlack.ShouldBe(19);
            graphBuilder.Activity(activityId8).TotalSlack.ShouldBe(19);
            graphBuilder.Activity(activityId8).LatestStartTime.ShouldBe(37);
            graphBuilder.Activity(activityId8).LatestFinishTime.ShouldBe(41);

            graphBuilder.Activity(activityId9).EarliestStartTime.ShouldBe(31);
            graphBuilder.Activity(activityId9).EarliestFinishTime.ShouldBe(41);
            graphBuilder.Activity(activityId9).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).LatestStartTime.ShouldBe(31);
            graphBuilder.Activity(activityId9).LatestFinishTime.ShouldBe(41);
        }

        [Fact]
        public void VertexGraphBuilderExtensions_GivenCalculateCriticalPath_WhenMinimumFreeSlackInEndActivity_ThenAsExpected()
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
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId1, 6));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId4, 11), new HashSet<int> { activityId2 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId5, 8), new HashSet<int> { activityId1, activityId2, activityId3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId6, 7), new HashSet<int> { activityId3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId7, 4), new HashSet<int> { activityId4 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId8, 4), new HashSet<int> { activityId4, activityId6 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId9, 10) { MinimumFreeSlack = 15 }, new HashSet<int> { activityId5 });

            graphBuilder.CalculateCriticalPath();

            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(6);
            graphBuilder.Activity(activityId1).FreeSlack.ShouldBe(2);
            graphBuilder.Activity(activityId1).TotalSlack.ShouldBe(17);
            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(17);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(23);

            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(7);
            graphBuilder.Activity(activityId2).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId2).TotalSlack.ShouldBe(16);
            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(23);

            graphBuilder.Activity(activityId3).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).TotalSlack.ShouldBe(15);
            graphBuilder.Activity(activityId3).LatestStartTime.ShouldBe(15);
            graphBuilder.Activity(activityId3).LatestFinishTime.ShouldBe(23);

            graphBuilder.Activity(activityId4).EarliestStartTime.ShouldBe(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.ShouldBe(18);
            graphBuilder.Activity(activityId4).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).TotalSlack.ShouldBe(19);
            graphBuilder.Activity(activityId4).LatestStartTime.ShouldBe(26);
            graphBuilder.Activity(activityId4).LatestFinishTime.ShouldBe(37);

            graphBuilder.Activity(activityId5).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId5).EarliestFinishTime.ShouldBe(16);
            graphBuilder.Activity(activityId5).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).TotalSlack.ShouldBe(15);
            graphBuilder.Activity(activityId5).LatestStartTime.ShouldBe(23);
            graphBuilder.Activity(activityId5).LatestFinishTime.ShouldBe(31);

            graphBuilder.Activity(activityId6).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.ShouldBe(15);
            graphBuilder.Activity(activityId6).FreeSlack.ShouldBe(3);
            graphBuilder.Activity(activityId6).TotalSlack.ShouldBe(22);
            graphBuilder.Activity(activityId6).LatestStartTime.ShouldBe(30);
            graphBuilder.Activity(activityId6).LatestFinishTime.ShouldBe(37);

            graphBuilder.Activity(activityId7).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId7).FreeSlack.ShouldBe(19);
            graphBuilder.Activity(activityId7).TotalSlack.ShouldBe(19);
            graphBuilder.Activity(activityId7).LatestStartTime.ShouldBe(37);
            graphBuilder.Activity(activityId7).LatestFinishTime.ShouldBe(41);

            graphBuilder.Activity(activityId8).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId8).EarliestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId8).FreeSlack.ShouldBe(19);
            graphBuilder.Activity(activityId8).TotalSlack.ShouldBe(19);
            graphBuilder.Activity(activityId8).LatestStartTime.ShouldBe(37);
            graphBuilder.Activity(activityId8).LatestFinishTime.ShouldBe(41);

            graphBuilder.Activity(activityId9).EarliestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId9).EarliestFinishTime.ShouldBe(26);
            graphBuilder.Activity(activityId9).FreeSlack.ShouldBe(15);
            graphBuilder.Activity(activityId9).TotalSlack.ShouldBe(15);
            graphBuilder.Activity(activityId9).LatestStartTime.ShouldBe(31);
            graphBuilder.Activity(activityId9).LatestFinishTime.ShouldBe(41);
        }

        [Fact]
        public void VertexGraphBuilderExtensions_GivenCalculateCriticalPath_WhenMinimumEarliestStartTimeInStartActivity_ThenAsExpected()
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
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId1, 6) { MinimumEarliestStartTime = 10 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId4, 11), new HashSet<int> { activityId2 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId5, 8), new HashSet<int> { activityId1, activityId2, activityId3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId6, 7), new HashSet<int> { activityId3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId7, 4), new HashSet<int> { activityId4 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId8, 4), new HashSet<int> { activityId4, activityId6 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId9, 10), new HashSet<int> { activityId5 });

            graphBuilder.CalculateCriticalPath();

            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(10);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(16);
            graphBuilder.Activity(activityId1).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId1).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(10);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(16);

            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(7);
            graphBuilder.Activity(activityId2).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId2).TotalSlack.ShouldBe(9);
            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(9);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(16);

            graphBuilder.Activity(activityId3).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).TotalSlack.ShouldBe(8);
            graphBuilder.Activity(activityId3).LatestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).LatestFinishTime.ShouldBe(16);

            graphBuilder.Activity(activityId4).EarliestStartTime.ShouldBe(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.ShouldBe(18);
            graphBuilder.Activity(activityId4).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).TotalSlack.ShouldBe(12);
            graphBuilder.Activity(activityId4).LatestStartTime.ShouldBe(19);
            graphBuilder.Activity(activityId4).LatestFinishTime.ShouldBe(30);

            graphBuilder.Activity(activityId5).EarliestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId5).EarliestFinishTime.ShouldBe(24);
            graphBuilder.Activity(activityId5).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).LatestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId5).LatestFinishTime.ShouldBe(24);

            graphBuilder.Activity(activityId6).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.ShouldBe(15);
            graphBuilder.Activity(activityId6).FreeSlack.ShouldBe(3);
            graphBuilder.Activity(activityId6).TotalSlack.ShouldBe(15);
            graphBuilder.Activity(activityId6).LatestStartTime.ShouldBe(23);
            graphBuilder.Activity(activityId6).LatestFinishTime.ShouldBe(30);

            graphBuilder.Activity(activityId7).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId7).FreeSlack.ShouldBe(12);
            graphBuilder.Activity(activityId7).TotalSlack.ShouldBe(12);
            graphBuilder.Activity(activityId7).LatestStartTime.ShouldBe(30);
            graphBuilder.Activity(activityId7).LatestFinishTime.ShouldBe(34);

            graphBuilder.Activity(activityId8).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId8).EarliestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId8).FreeSlack.ShouldBe(12);
            graphBuilder.Activity(activityId8).TotalSlack.ShouldBe(12);
            graphBuilder.Activity(activityId8).LatestStartTime.ShouldBe(30);
            graphBuilder.Activity(activityId8).LatestFinishTime.ShouldBe(34);

            graphBuilder.Activity(activityId9).EarliestStartTime.ShouldBe(24);
            graphBuilder.Activity(activityId9).EarliestFinishTime.ShouldBe(34);
            graphBuilder.Activity(activityId9).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).LatestStartTime.ShouldBe(24);
            graphBuilder.Activity(activityId9).LatestFinishTime.ShouldBe(34);
        }

        [Fact]
        public void VertexGraphBuilderExtensions_GivenCalculateCriticalPath_WhenMinimumEarliestStartTimeInNormalActivity_ThenAsExpected()
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
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId1, 6));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId4, 11), new HashSet<int> { activityId2 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId5, 8) { MinimumEarliestStartTime = 10 }, new HashSet<int> { activityId1, activityId2, activityId3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId6, 7), new HashSet<int> { activityId3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId7, 4), new HashSet<int> { activityId4 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId8, 4), new HashSet<int> { activityId4, activityId6 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId9, 10), new HashSet<int> { activityId5 });

            graphBuilder.CalculateCriticalPath();

            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(6);
            graphBuilder.Activity(activityId1).FreeSlack.ShouldBe(4);
            graphBuilder.Activity(activityId1).TotalSlack.ShouldBe(4);
            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(4);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(10);

            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(7);
            graphBuilder.Activity(activityId2).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId2).TotalSlack.ShouldBe(3);
            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(3);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(10);

            graphBuilder.Activity(activityId3).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).TotalSlack.ShouldBe(2);
            graphBuilder.Activity(activityId3).LatestStartTime.ShouldBe(2);
            graphBuilder.Activity(activityId3).LatestFinishTime.ShouldBe(10);

            graphBuilder.Activity(activityId4).EarliestStartTime.ShouldBe(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.ShouldBe(18);
            graphBuilder.Activity(activityId4).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).TotalSlack.ShouldBe(6);
            graphBuilder.Activity(activityId4).LatestStartTime.ShouldBe(13);
            graphBuilder.Activity(activityId4).LatestFinishTime.ShouldBe(24);

            graphBuilder.Activity(activityId5).EarliestStartTime.ShouldBe(10);
            graphBuilder.Activity(activityId5).EarliestFinishTime.ShouldBe(18);
            graphBuilder.Activity(activityId5).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).LatestStartTime.ShouldBe(10);
            graphBuilder.Activity(activityId5).LatestFinishTime.ShouldBe(18);

            graphBuilder.Activity(activityId6).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.ShouldBe(15);
            graphBuilder.Activity(activityId6).FreeSlack.ShouldBe(3);
            graphBuilder.Activity(activityId6).TotalSlack.ShouldBe(9);
            graphBuilder.Activity(activityId6).LatestStartTime.ShouldBe(17);
            graphBuilder.Activity(activityId6).LatestFinishTime.ShouldBe(24);

            graphBuilder.Activity(activityId7).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId7).FreeSlack.ShouldBe(6);
            graphBuilder.Activity(activityId7).TotalSlack.ShouldBe(6);
            graphBuilder.Activity(activityId7).LatestStartTime.ShouldBe(24);
            graphBuilder.Activity(activityId7).LatestFinishTime.ShouldBe(28);

            graphBuilder.Activity(activityId8).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId8).EarliestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId8).FreeSlack.ShouldBe(6);
            graphBuilder.Activity(activityId8).TotalSlack.ShouldBe(6);
            graphBuilder.Activity(activityId8).LatestStartTime.ShouldBe(24);
            graphBuilder.Activity(activityId8).LatestFinishTime.ShouldBe(28);

            graphBuilder.Activity(activityId9).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId9).EarliestFinishTime.ShouldBe(28);
            graphBuilder.Activity(activityId9).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).LatestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId9).LatestFinishTime.ShouldBe(28);
        }

        [Fact]
        public void VertexGraphBuilderExtensions_GivenCalculateCriticalPath_WhenMinimumEarliestStartTimeInEndActivity_ThenAsExpected()
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
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId1, 6));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId4, 11), new HashSet<int> { activityId2 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId5, 8), new HashSet<int> { activityId1, activityId2, activityId3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId6, 7), new HashSet<int> { activityId3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId7, 4), new HashSet<int> { activityId4 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId8, 4), new HashSet<int> { activityId4, activityId6 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId9, 10) { MinimumEarliestStartTime = 20 }, new HashSet<int> { activityId5 });

            graphBuilder.CalculateCriticalPath();

            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(6);
            graphBuilder.Activity(activityId1).FreeSlack.ShouldBe(2);
            graphBuilder.Activity(activityId1).TotalSlack.ShouldBe(6);
            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(6);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(12);

            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(7);
            graphBuilder.Activity(activityId2).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId2).TotalSlack.ShouldBe(5);
            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(5);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(12);

            graphBuilder.Activity(activityId3).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).TotalSlack.ShouldBe(4);
            graphBuilder.Activity(activityId3).LatestStartTime.ShouldBe(4);
            graphBuilder.Activity(activityId3).LatestFinishTime.ShouldBe(12);

            graphBuilder.Activity(activityId4).EarliestStartTime.ShouldBe(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.ShouldBe(18);
            graphBuilder.Activity(activityId4).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).TotalSlack.ShouldBe(8);
            graphBuilder.Activity(activityId4).LatestStartTime.ShouldBe(15);
            graphBuilder.Activity(activityId4).LatestFinishTime.ShouldBe(26);

            graphBuilder.Activity(activityId5).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId5).EarliestFinishTime.ShouldBe(16);
            graphBuilder.Activity(activityId5).FreeSlack.ShouldBe(4);
            graphBuilder.Activity(activityId5).TotalSlack.ShouldBe(4);
            graphBuilder.Activity(activityId5).LatestStartTime.ShouldBe(12);
            graphBuilder.Activity(activityId5).LatestFinishTime.ShouldBe(20);

            graphBuilder.Activity(activityId6).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.ShouldBe(15);
            graphBuilder.Activity(activityId6).FreeSlack.ShouldBe(3);
            graphBuilder.Activity(activityId6).TotalSlack.ShouldBe(11);
            graphBuilder.Activity(activityId6).LatestStartTime.ShouldBe(19);
            graphBuilder.Activity(activityId6).LatestFinishTime.ShouldBe(26);

            graphBuilder.Activity(activityId7).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId7).FreeSlack.ShouldBe(8);
            graphBuilder.Activity(activityId7).TotalSlack.ShouldBe(8);
            graphBuilder.Activity(activityId7).LatestStartTime.ShouldBe(26);
            graphBuilder.Activity(activityId7).LatestFinishTime.ShouldBe(30);

            graphBuilder.Activity(activityId8).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId8).EarliestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId8).FreeSlack.ShouldBe(8);
            graphBuilder.Activity(activityId8).TotalSlack.ShouldBe(8);
            graphBuilder.Activity(activityId8).LatestStartTime.ShouldBe(26);
            graphBuilder.Activity(activityId8).LatestFinishTime.ShouldBe(30);

            graphBuilder.Activity(activityId9).EarliestStartTime.ShouldBe(20);
            graphBuilder.Activity(activityId9).EarliestFinishTime.ShouldBe(30);
            graphBuilder.Activity(activityId9).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).LatestStartTime.ShouldBe(20);
            graphBuilder.Activity(activityId9).LatestFinishTime.ShouldBe(30);
        }

        [Fact]
        public void VertexGraphBuilderExtensions_GivenCalculateCriticalPath_WhenMaximumLatestFinishTimeInStartActivity_ThenAsExpected()
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
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId1, 6) { MaximumLatestFinishTime = 7 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId4, 11), new HashSet<int> { activityId2 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId5, 8), new HashSet<int> { activityId1, activityId2, activityId3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId6, 7), new HashSet<int> { activityId3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId7, 4), new HashSet<int> { activityId4 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId8, 4), new HashSet<int> { activityId4, activityId6 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId9, 10), new HashSet<int> { activityId5 });

            graphBuilder.CalculateCriticalPath();

            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(6);
            graphBuilder.Activity(activityId1).FreeSlack.ShouldBe(1);
            graphBuilder.Activity(activityId1).TotalSlack.ShouldBe(1);
            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(1);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(7);

            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(7);
            graphBuilder.Activity(activityId2).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId2).TotalSlack.ShouldBe(1);
            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(1);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(8);

            graphBuilder.Activity(activityId3).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestFinishTime.ShouldBe(8);

            graphBuilder.Activity(activityId4).EarliestStartTime.ShouldBe(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.ShouldBe(18);
            graphBuilder.Activity(activityId4).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).TotalSlack.ShouldBe(4);
            graphBuilder.Activity(activityId4).LatestStartTime.ShouldBe(11);
            graphBuilder.Activity(activityId4).LatestFinishTime.ShouldBe(22);

            graphBuilder.Activity(activityId5).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId5).EarliestFinishTime.ShouldBe(16);
            graphBuilder.Activity(activityId5).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).LatestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId5).LatestFinishTime.ShouldBe(16);

            graphBuilder.Activity(activityId6).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.ShouldBe(15);
            graphBuilder.Activity(activityId6).FreeSlack.ShouldBe(3);
            graphBuilder.Activity(activityId6).TotalSlack.ShouldBe(7);
            graphBuilder.Activity(activityId6).LatestStartTime.ShouldBe(15);
            graphBuilder.Activity(activityId6).LatestFinishTime.ShouldBe(22);

            graphBuilder.Activity(activityId7).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId7).FreeSlack.ShouldBe(4);
            graphBuilder.Activity(activityId7).TotalSlack.ShouldBe(4);
            graphBuilder.Activity(activityId7).LatestStartTime.ShouldBe(22);
            graphBuilder.Activity(activityId7).LatestFinishTime.ShouldBe(26);

            graphBuilder.Activity(activityId8).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId8).EarliestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId8).FreeSlack.ShouldBe(4);
            graphBuilder.Activity(activityId8).TotalSlack.ShouldBe(4);
            graphBuilder.Activity(activityId8).LatestStartTime.ShouldBe(22);
            graphBuilder.Activity(activityId8).LatestFinishTime.ShouldBe(26);

            graphBuilder.Activity(activityId9).EarliestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId9).EarliestFinishTime.ShouldBe(26);
            graphBuilder.Activity(activityId9).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).LatestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId9).LatestFinishTime.ShouldBe(26);
        }

        [Fact]
        public void VertexGraphBuilderExtensions_GivenCalculateCriticalPath_WhenExtremeMaximumLatestFinishTimeInStartActivity_ThenAsExpected()
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
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId1, 6) { MaximumLatestFinishTime = 5 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId4, 11), new HashSet<int> { activityId2 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId5, 8), new HashSet<int> { activityId1, activityId2, activityId3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId6, 7), new HashSet<int> { activityId3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId7, 4), new HashSet<int> { activityId4 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId8, 4), new HashSet<int> { activityId4, activityId6 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId9, 10), new HashSet<int> { activityId5 });

            graphBuilder.CalculateCriticalPath();

            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(-1);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(5);
            graphBuilder.Activity(activityId1).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId1).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(-1);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(5);

            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(7);
            graphBuilder.Activity(activityId2).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId2).TotalSlack.ShouldBe(1);
            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(1);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(8);

            graphBuilder.Activity(activityId3).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestFinishTime.ShouldBe(8);

            graphBuilder.Activity(activityId4).EarliestStartTime.ShouldBe(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.ShouldBe(18);
            graphBuilder.Activity(activityId4).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).TotalSlack.ShouldBe(4);
            graphBuilder.Activity(activityId4).LatestStartTime.ShouldBe(11);
            graphBuilder.Activity(activityId4).LatestFinishTime.ShouldBe(22);

            graphBuilder.Activity(activityId5).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId5).EarliestFinishTime.ShouldBe(16);
            graphBuilder.Activity(activityId5).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).LatestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId5).LatestFinishTime.ShouldBe(16);

            graphBuilder.Activity(activityId6).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.ShouldBe(15);
            graphBuilder.Activity(activityId6).FreeSlack.ShouldBe(3);
            graphBuilder.Activity(activityId6).TotalSlack.ShouldBe(7);
            graphBuilder.Activity(activityId6).LatestStartTime.ShouldBe(15);
            graphBuilder.Activity(activityId6).LatestFinishTime.ShouldBe(22);

            graphBuilder.Activity(activityId7).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId7).FreeSlack.ShouldBe(4);
            graphBuilder.Activity(activityId7).TotalSlack.ShouldBe(4);
            graphBuilder.Activity(activityId7).LatestStartTime.ShouldBe(22);
            graphBuilder.Activity(activityId7).LatestFinishTime.ShouldBe(26);

            graphBuilder.Activity(activityId8).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId8).EarliestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId8).FreeSlack.ShouldBe(4);
            graphBuilder.Activity(activityId8).TotalSlack.ShouldBe(4);
            graphBuilder.Activity(activityId8).LatestStartTime.ShouldBe(22);
            graphBuilder.Activity(activityId8).LatestFinishTime.ShouldBe(26);

            graphBuilder.Activity(activityId9).EarliestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId9).EarliestFinishTime.ShouldBe(26);
            graphBuilder.Activity(activityId9).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).LatestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId9).LatestFinishTime.ShouldBe(26);
        }

        [Fact]
        public void VertexGraphBuilderExtensions_GivenCalculateCriticalPath_WhenMaximumLatestFinishTimeInNormalActivity_ThenAsExpected()
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
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId1, 6));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId4, 11) { MaximumLatestFinishTime = 18 }, new HashSet<int> { activityId2 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId5, 8), new HashSet<int> { activityId1, activityId2, activityId3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId6, 7), new HashSet<int> { activityId3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId7, 4), new HashSet<int> { activityId4 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId8, 4), new HashSet<int> { activityId4, activityId6 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId9, 10), new HashSet<int> { activityId5 });

            graphBuilder.CalculateCriticalPath();

            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(6);
            graphBuilder.Activity(activityId1).FreeSlack.ShouldBe(2);
            graphBuilder.Activity(activityId1).TotalSlack.ShouldBe(2);
            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(2);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(8);

            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(7);
            graphBuilder.Activity(activityId2).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId2).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(7);

            graphBuilder.Activity(activityId3).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestFinishTime.ShouldBe(8);

            graphBuilder.Activity(activityId4).EarliestStartTime.ShouldBe(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.ShouldBe(18);
            graphBuilder.Activity(activityId4).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).LatestStartTime.ShouldBe(7);
            graphBuilder.Activity(activityId4).LatestFinishTime.ShouldBe(18);

            graphBuilder.Activity(activityId5).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId5).EarliestFinishTime.ShouldBe(16);
            graphBuilder.Activity(activityId5).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).LatestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId5).LatestFinishTime.ShouldBe(16);

            graphBuilder.Activity(activityId6).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.ShouldBe(15);
            graphBuilder.Activity(activityId6).FreeSlack.ShouldBe(3);
            graphBuilder.Activity(activityId6).TotalSlack.ShouldBe(7);
            graphBuilder.Activity(activityId6).LatestStartTime.ShouldBe(15);
            graphBuilder.Activity(activityId6).LatestFinishTime.ShouldBe(22);

            graphBuilder.Activity(activityId7).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId7).FreeSlack.ShouldBe(4);
            graphBuilder.Activity(activityId7).TotalSlack.ShouldBe(4);
            graphBuilder.Activity(activityId7).LatestStartTime.ShouldBe(22);
            graphBuilder.Activity(activityId7).LatestFinishTime.ShouldBe(26);

            graphBuilder.Activity(activityId8).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId8).EarliestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId8).FreeSlack.ShouldBe(4);
            graphBuilder.Activity(activityId8).TotalSlack.ShouldBe(4);
            graphBuilder.Activity(activityId8).LatestStartTime.ShouldBe(22);
            graphBuilder.Activity(activityId8).LatestFinishTime.ShouldBe(26);

            graphBuilder.Activity(activityId9).EarliestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId9).EarliestFinishTime.ShouldBe(26);
            graphBuilder.Activity(activityId9).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).LatestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId9).LatestFinishTime.ShouldBe(26);
        }

        [Fact]
        public void VertexGraphBuilderExtensions_GivenCalculateCriticalPath_WhenExtremeMaximumLatestFinishTimeInNormalActivity_ThenAsExpected()
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
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId1, 6));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId4, 11) { MaximumLatestFinishTime = 16 }, new HashSet<int> { activityId2 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId5, 8), new HashSet<int> { activityId1, activityId2, activityId3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId6, 7), new HashSet<int> { activityId3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId7, 4), new HashSet<int> { activityId4 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId8, 4), new HashSet<int> { activityId4, activityId6 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId9, 10), new HashSet<int> { activityId5 });

            graphBuilder.CalculateCriticalPath();

            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(6);
            graphBuilder.Activity(activityId1).FreeSlack.ShouldBe(2);
            graphBuilder.Activity(activityId1).TotalSlack.ShouldBe(2);
            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(2);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(8);

            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(7);

            // MS Project would list this as 0, but ProjectPlan calculates slack based on its
            // downstream effect. So, in this case, the -2 total slack of activity 4 is transfered
            // to the the free slack of activity 2 instead.
            graphBuilder.Activity(activityId2).FreeSlack.ShouldBe(-2);

            graphBuilder.Activity(activityId2).TotalSlack.ShouldBe(-2);
            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(-2);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(5);

            graphBuilder.Activity(activityId3).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestFinishTime.ShouldBe(8);

            graphBuilder.Activity(activityId4).EarliestStartTime.ShouldBe(5);
            graphBuilder.Activity(activityId4).EarliestFinishTime.ShouldBe(16);
            graphBuilder.Activity(activityId4).FreeSlack.ShouldBe(0);

            // MS Project would list this as -2, but ProjectPlan calculates slack based on its
            // downstream effect. So, in this case, the -2 total slack of activity 4 is transfered
            // to the the free slack of activity 2 instead.
            graphBuilder.Activity(activityId4).TotalSlack.ShouldBe(0);

            graphBuilder.Activity(activityId4).LatestStartTime.ShouldBe(5);
            graphBuilder.Activity(activityId4).LatestFinishTime.ShouldBe(16);

            graphBuilder.Activity(activityId5).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId5).EarliestFinishTime.ShouldBe(16);
            graphBuilder.Activity(activityId5).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).LatestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId5).LatestFinishTime.ShouldBe(16);

            graphBuilder.Activity(activityId6).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.ShouldBe(15);
            graphBuilder.Activity(activityId6).FreeSlack.ShouldBe(1);
            graphBuilder.Activity(activityId6).TotalSlack.ShouldBe(7);
            graphBuilder.Activity(activityId6).LatestStartTime.ShouldBe(15);
            graphBuilder.Activity(activityId6).LatestFinishTime.ShouldBe(22);

            graphBuilder.Activity(activityId7).EarliestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId7).EarliestFinishTime.ShouldBe(20);
            graphBuilder.Activity(activityId7).FreeSlack.ShouldBe(6);
            graphBuilder.Activity(activityId7).TotalSlack.ShouldBe(6);
            graphBuilder.Activity(activityId7).LatestStartTime.ShouldBe(22);
            graphBuilder.Activity(activityId7).LatestFinishTime.ShouldBe(26);

            graphBuilder.Activity(activityId8).EarliestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId8).EarliestFinishTime.ShouldBe(20);
            graphBuilder.Activity(activityId8).FreeSlack.ShouldBe(6);
            graphBuilder.Activity(activityId8).TotalSlack.ShouldBe(6);
            graphBuilder.Activity(activityId8).LatestStartTime.ShouldBe(22);
            graphBuilder.Activity(activityId8).LatestFinishTime.ShouldBe(26);

            graphBuilder.Activity(activityId9).EarliestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId9).EarliestFinishTime.ShouldBe(26);
            graphBuilder.Activity(activityId9).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).LatestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId9).LatestFinishTime.ShouldBe(26);
        }

        [Fact]
        public void VertexGraphBuilderExtensions_GivenCalculateCriticalPath_WhenMaximumLatestFinishTimeInEndActivity_ThenAsExpected()
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
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId1, 6));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId4, 11), new HashSet<int> { activityId2 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId5, 8), new HashSet<int> { activityId1, activityId2, activityId3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId6, 7), new HashSet<int> { activityId3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId7, 4), new HashSet<int> { activityId4 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId8, 4) { MaximumLatestFinishTime = 22 }, new HashSet<int> { activityId4, activityId6 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId9, 10), new HashSet<int> { activityId5 });

            graphBuilder.CalculateCriticalPath();

            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(6);
            graphBuilder.Activity(activityId1).FreeSlack.ShouldBe(2);
            graphBuilder.Activity(activityId1).TotalSlack.ShouldBe(2);
            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(2);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(8);

            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(7);
            graphBuilder.Activity(activityId2).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId2).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(7);

            graphBuilder.Activity(activityId3).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestFinishTime.ShouldBe(8);

            graphBuilder.Activity(activityId4).EarliestStartTime.ShouldBe(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.ShouldBe(18);
            graphBuilder.Activity(activityId4).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId4).LatestStartTime.ShouldBe(7);
            graphBuilder.Activity(activityId4).LatestFinishTime.ShouldBe(18);

            graphBuilder.Activity(activityId5).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId5).EarliestFinishTime.ShouldBe(16);
            graphBuilder.Activity(activityId5).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).LatestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId5).LatestFinishTime.ShouldBe(16);

            graphBuilder.Activity(activityId6).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.ShouldBe(15);
            graphBuilder.Activity(activityId6).FreeSlack.ShouldBe(3);
            graphBuilder.Activity(activityId6).TotalSlack.ShouldBe(3);
            graphBuilder.Activity(activityId6).LatestStartTime.ShouldBe(11);
            graphBuilder.Activity(activityId6).LatestFinishTime.ShouldBe(18);

            graphBuilder.Activity(activityId7).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId7).FreeSlack.ShouldBe(4);
            graphBuilder.Activity(activityId7).TotalSlack.ShouldBe(4);
            graphBuilder.Activity(activityId7).LatestStartTime.ShouldBe(22);
            graphBuilder.Activity(activityId7).LatestFinishTime.ShouldBe(26);

            graphBuilder.Activity(activityId8).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId8).EarliestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId8).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId8).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId8).LatestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId8).LatestFinishTime.ShouldBe(22);

            graphBuilder.Activity(activityId9).EarliestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId9).EarliestFinishTime.ShouldBe(26);
            graphBuilder.Activity(activityId9).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).LatestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId9).LatestFinishTime.ShouldBe(26);
        }

        [Fact]
        public void VertexGraphBuilderExtensions_GivenCalculateCriticalPath_WhenExtremeMaximumLatestFinishTimeInEndActivity_ThenAsExpected()
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
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId1, 6));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId4, 11), new HashSet<int> { activityId2 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId5, 8), new HashSet<int> { activityId1, activityId2, activityId3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId6, 7), new HashSet<int> { activityId3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId7, 4), new HashSet<int> { activityId4 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId8, 4) { MaximumLatestFinishTime = 21 }, new HashSet<int> { activityId4, activityId6 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId9, 10), new HashSet<int> { activityId5 });

            graphBuilder.CalculateCriticalPath();

            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(6);
            graphBuilder.Activity(activityId1).FreeSlack.ShouldBe(2);
            graphBuilder.Activity(activityId1).TotalSlack.ShouldBe(2);
            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(2);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(8);

            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(7);

            // MS Project would list this as 0, but ProjectPlan calculates slack based on its
            // downstream effect. So, in this case, the -1 total slack of activity 8 is transfered
            // to the the free slack of activities 2 and 4 instead.
            graphBuilder.Activity(activityId2).FreeSlack.ShouldBe(-1);

            graphBuilder.Activity(activityId2).TotalSlack.ShouldBe(-1);
            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(-1);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(6);

            graphBuilder.Activity(activityId3).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.ShouldBe(8);
            graphBuilder.Activity(activityId3).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestFinishTime.ShouldBe(8);

            graphBuilder.Activity(activityId4).EarliestStartTime.ShouldBe(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.ShouldBe(18);

            // MS Project would list this as 0, but ProjectPlan calculates slack based on its
            // downstream effect. So, in this case, the -1 total slack of activity 8 is transfered
            // to the the free slack of activities 2 and 4 instead.
            graphBuilder.Activity(activityId4).FreeSlack.ShouldBe(-1);

            graphBuilder.Activity(activityId4).TotalSlack.ShouldBe(-1);
            graphBuilder.Activity(activityId4).LatestStartTime.ShouldBe(6);
            graphBuilder.Activity(activityId4).LatestFinishTime.ShouldBe(17);

            graphBuilder.Activity(activityId5).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId5).EarliestFinishTime.ShouldBe(16);
            graphBuilder.Activity(activityId5).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId5).LatestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId5).LatestFinishTime.ShouldBe(16);

            graphBuilder.Activity(activityId6).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.ShouldBe(15);
            graphBuilder.Activity(activityId6).FreeSlack.ShouldBe(2);
            graphBuilder.Activity(activityId6).TotalSlack.ShouldBe(2);
            graphBuilder.Activity(activityId6).LatestStartTime.ShouldBe(10);
            graphBuilder.Activity(activityId6).LatestFinishTime.ShouldBe(17);

            graphBuilder.Activity(activityId7).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.ShouldBe(22);
            graphBuilder.Activity(activityId7).FreeSlack.ShouldBe(4);
            graphBuilder.Activity(activityId7).TotalSlack.ShouldBe(4);
            graphBuilder.Activity(activityId7).LatestStartTime.ShouldBe(22);
            graphBuilder.Activity(activityId7).LatestFinishTime.ShouldBe(26);

            graphBuilder.Activity(activityId8).EarliestStartTime.ShouldBe(17);
            graphBuilder.Activity(activityId8).EarliestFinishTime.ShouldBe(21);
            graphBuilder.Activity(activityId8).FreeSlack.ShouldBe(0);

            // MS Project would list this as -1, but ProjectPlan calculates slack based on its
            // downstream effect. So, in this case, the -1 total slack of activity 8 is transfered
            // to the the free slack of activities 2 and 4 instead.
            graphBuilder.Activity(activityId8).TotalSlack.ShouldBe(0);

            graphBuilder.Activity(activityId8).LatestStartTime.ShouldBe(17);
            graphBuilder.Activity(activityId8).LatestFinishTime.ShouldBe(21);

            graphBuilder.Activity(activityId9).EarliestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId9).EarliestFinishTime.ShouldBe(26);
            graphBuilder.Activity(activityId9).FreeSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).TotalSlack.ShouldBe(0);
            graphBuilder.Activity(activityId9).LatestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId9).LatestFinishTime.ShouldBe(26);
        }

        [Fact]
        public void VertexGraphBuilderExtensions_GivenCalculateCriticalPath_WhenMinimumEarliestStartTimeAndMaximumLatestFinishTimeAreInvalid_ThenShouldThrowInvalidOperationException()
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
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId1, 6));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId4, 11) { MinimumEarliestStartTime = 7, MaximumLatestFinishTime = 17 }, new HashSet<int> { activityId2 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId5, 8), new HashSet<int> { activityId1, activityId2, activityId3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId6, 7), new HashSet<int> { activityId3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId7, 4), new HashSet<int> { activityId4 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId8, 4), new HashSet<int> { activityId4, activityId6 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId9, 10), new HashSet<int> { activityId5 });

            Action act = () => graphBuilder.CalculateCriticalPath();
            act.ShouldThrow<InvalidOperationException>();
        }

        [Fact]
        public void VertexGraphBuilderExtensions_GivenCalculateCriticalPathForwardFlow_WhenAddingAndRemovingDependencies_ThenAsExpected()
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
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId1, 6));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId4, 11), new HashSet<int> { activityId2 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId5, 8), new HashSet<int> { activityId1, activityId2, activityId3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId6, 7), new HashSet<int> { activityId3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId7, 4), new HashSet<int> { activityId4 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId8, 4), new HashSet<int> { activityId4, activityId6 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId9, 10), new HashSet<int> { activityId5 });

            graphBuilder.Nodes.Count().ShouldBe(9);
            graphBuilder.Edges.Count().ShouldBe(9);

            bool result1 = graphBuilder.AddActivityDependencies(activityId6, new HashSet<int> { activityId5 });
            result1.ShouldBeTrue();

            graphBuilder.Nodes.Count().ShouldBe(9);
            graphBuilder.Edges.Count().ShouldBe(10);

            graphBuilder.ClearCriticalPathVariables();
            graphBuilder.CalculateCriticalPathForwardFlow();

            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(6);

            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(7);

            graphBuilder.Activity(activityId3).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.ShouldBe(8);

            graphBuilder.Activity(activityId4).EarliestStartTime.ShouldBe(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.ShouldBe(18);

            graphBuilder.Activity(activityId5).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId5).EarliestFinishTime.ShouldBe(16);

            graphBuilder.Activity(activityId6).EarliestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId6).EarliestFinishTime.ShouldBe(23);

            graphBuilder.Activity(activityId7).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.ShouldBe(22);

            graphBuilder.Activity(activityId8).EarliestStartTime.ShouldBe(23);
            graphBuilder.Activity(activityId8).EarliestFinishTime.ShouldBe(27);

            graphBuilder.Activity(activityId9).EarliestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId9).EarliestFinishTime.ShouldBe(26);



            bool result2 = graphBuilder.AddActivityDependencies(activityId6, new HashSet<int> { 4 });
            result2.ShouldBeTrue();

            graphBuilder.Nodes.Count().ShouldBe(9);
            graphBuilder.Edges.Count().ShouldBe(11);

            graphBuilder.ClearCriticalPathVariables();
            graphBuilder.CalculateCriticalPathForwardFlow();

            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(6);

            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(7);

            graphBuilder.Activity(activityId3).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.ShouldBe(8);

            graphBuilder.Activity(activityId4).EarliestStartTime.ShouldBe(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.ShouldBe(18);

            graphBuilder.Activity(activityId5).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId5).EarliestFinishTime.ShouldBe(16);

            graphBuilder.Activity(activityId6).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId6).EarliestFinishTime.ShouldBe(25);

            graphBuilder.Activity(activityId7).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.ShouldBe(22);

            graphBuilder.Activity(activityId8).EarliestStartTime.ShouldBe(25);
            graphBuilder.Activity(activityId8).EarliestFinishTime.ShouldBe(29);

            graphBuilder.Activity(activityId9).EarliestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId9).EarliestFinishTime.ShouldBe(26);



            bool result3 = graphBuilder.RemoveActivityDependencies(activityId6, new HashSet<int> { 5 });
            result3.ShouldBeTrue();

            graphBuilder.Nodes.Count().ShouldBe(9);
            graphBuilder.Edges.Count().ShouldBe(10);

            graphBuilder.ClearCriticalPathVariables();
            graphBuilder.CalculateCriticalPathForwardFlow();

            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(6);

            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(7);

            graphBuilder.Activity(activityId3).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.ShouldBe(8);

            graphBuilder.Activity(activityId4).EarliestStartTime.ShouldBe(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.ShouldBe(18);

            graphBuilder.Activity(activityId5).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId5).EarliestFinishTime.ShouldBe(16);

            graphBuilder.Activity(activityId6).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId6).EarliestFinishTime.ShouldBe(25);

            graphBuilder.Activity(activityId7).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.ShouldBe(22);

            graphBuilder.Activity(activityId8).EarliestStartTime.ShouldBe(25);
            graphBuilder.Activity(activityId8).EarliestFinishTime.ShouldBe(29);

            graphBuilder.Activity(activityId9).EarliestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId9).EarliestFinishTime.ShouldBe(26);



            bool result4 = graphBuilder.RemoveActivityDependencies(activityId6, new HashSet<int> { 4 });
            result4.ShouldBeTrue();

            graphBuilder.Nodes.Count().ShouldBe(9);
            graphBuilder.Edges.Count().ShouldBe(9);

            graphBuilder.ClearCriticalPathVariables();
            graphBuilder.CalculateCriticalPathForwardFlow();

            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(6);

            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(7);

            graphBuilder.Activity(activityId3).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.ShouldBe(8);

            graphBuilder.Activity(activityId4).EarliestStartTime.ShouldBe(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.ShouldBe(18);

            graphBuilder.Activity(activityId5).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId5).EarliestFinishTime.ShouldBe(16);

            graphBuilder.Activity(activityId6).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.ShouldBe(15);

            graphBuilder.Activity(activityId7).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.ShouldBe(22);

            graphBuilder.Activity(activityId8).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId8).EarliestFinishTime.ShouldBe(22);

            graphBuilder.Activity(activityId9).EarliestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId9).EarliestFinishTime.ShouldBe(26);
        }

        [Fact]
        public void VertexGraphBuilderExtensions_GivenCalculateCriticalPathBackwardFlow_WhenAddingAndRemovingDependencies_ThenAsExpected()
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
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId1, 6));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId4, 11), new HashSet<int> { activityId2 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId5, 8), new HashSet<int> { activityId1, activityId2, activityId3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId6, 7), new HashSet<int> { activityId3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId7, 4), new HashSet<int> { activityId4 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId8, 4), new HashSet<int> { activityId4, activityId6 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId9, 10), new HashSet<int> { activityId5 });

            graphBuilder.Nodes.Count().ShouldBe(9);
            graphBuilder.Edges.Count().ShouldBe(9);

            bool result1 = graphBuilder.AddActivityDependencies(activityId6, new HashSet<int> { activityId5 });
            result1.ShouldBeTrue();

            graphBuilder.Nodes.Count().ShouldBe(9);
            graphBuilder.Edges.Count().ShouldBe(10);

            graphBuilder.ClearCriticalPathVariables();
            graphBuilder.CalculateCriticalPathForwardFlow();
            graphBuilder.CalculateCriticalPathBackwardFlow();

            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(2);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(8);

            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(1);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(8);

            graphBuilder.Activity(activityId3).LatestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestFinishTime.ShouldBe(8);

            graphBuilder.Activity(activityId4).LatestStartTime.ShouldBe(12);
            graphBuilder.Activity(activityId4).LatestFinishTime.ShouldBe(23);

            graphBuilder.Activity(activityId5).LatestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId5).LatestFinishTime.ShouldBe(16);

            graphBuilder.Activity(activityId6).LatestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId6).LatestFinishTime.ShouldBe(23);

            graphBuilder.Activity(activityId7).LatestStartTime.ShouldBe(23);
            graphBuilder.Activity(activityId7).LatestFinishTime.ShouldBe(27);

            graphBuilder.Activity(activityId8).LatestStartTime.ShouldBe(23);
            graphBuilder.Activity(activityId8).LatestFinishTime.ShouldBe(27);

            graphBuilder.Activity(activityId9).LatestStartTime.ShouldBe(17);
            graphBuilder.Activity(activityId9).LatestFinishTime.ShouldBe(27);



            bool result2 = graphBuilder.AddActivityDependencies(activityId6, new HashSet<int> { 4 });
            result2.ShouldBeTrue();

            graphBuilder.Nodes.Count().ShouldBe(9);
            graphBuilder.Edges.Count().ShouldBe(11);

            graphBuilder.ClearCriticalPathVariables();
            graphBuilder.CalculateCriticalPathForwardFlow();
            graphBuilder.CalculateCriticalPathBackwardFlow();

            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(4);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(10);

            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(7);

            graphBuilder.Activity(activityId3).LatestStartTime.ShouldBe(2);
            graphBuilder.Activity(activityId3).LatestFinishTime.ShouldBe(10);

            graphBuilder.Activity(activityId4).LatestStartTime.ShouldBe(7);
            graphBuilder.Activity(activityId4).LatestFinishTime.ShouldBe(18);

            graphBuilder.Activity(activityId5).LatestStartTime.ShouldBe(10);
            graphBuilder.Activity(activityId5).LatestFinishTime.ShouldBe(18);

            graphBuilder.Activity(activityId6).LatestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId6).LatestFinishTime.ShouldBe(25);

            graphBuilder.Activity(activityId7).LatestStartTime.ShouldBe(25);
            graphBuilder.Activity(activityId7).LatestFinishTime.ShouldBe(29);

            graphBuilder.Activity(activityId8).LatestStartTime.ShouldBe(25);
            graphBuilder.Activity(activityId8).LatestFinishTime.ShouldBe(29);

            graphBuilder.Activity(activityId9).LatestStartTime.ShouldBe(19);
            graphBuilder.Activity(activityId9).LatestFinishTime.ShouldBe(29);



            bool result3 = graphBuilder.RemoveActivityDependencies(activityId6, new HashSet<int> { 5 });
            result3.ShouldBeTrue();

            graphBuilder.Nodes.Count().ShouldBe(9);
            graphBuilder.Edges.Count().ShouldBe(10);

            graphBuilder.ClearCriticalPathVariables();
            graphBuilder.CalculateCriticalPathForwardFlow();
            graphBuilder.CalculateCriticalPathBackwardFlow();

            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(5);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(11);

            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(7);

            graphBuilder.Activity(activityId3).LatestStartTime.ShouldBe(3);
            graphBuilder.Activity(activityId3).LatestFinishTime.ShouldBe(11);

            graphBuilder.Activity(activityId4).LatestStartTime.ShouldBe(7);
            graphBuilder.Activity(activityId4).LatestFinishTime.ShouldBe(18);

            graphBuilder.Activity(activityId5).LatestStartTime.ShouldBe(11);
            graphBuilder.Activity(activityId5).LatestFinishTime.ShouldBe(19);

            graphBuilder.Activity(activityId6).LatestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId6).LatestFinishTime.ShouldBe(25);

            graphBuilder.Activity(activityId7).LatestStartTime.ShouldBe(25);
            graphBuilder.Activity(activityId7).LatestFinishTime.ShouldBe(29);

            graphBuilder.Activity(activityId8).LatestStartTime.ShouldBe(25);
            graphBuilder.Activity(activityId8).LatestFinishTime.ShouldBe(29);

            graphBuilder.Activity(activityId9).LatestStartTime.ShouldBe(19);
            graphBuilder.Activity(activityId9).LatestFinishTime.ShouldBe(29);



            bool result4 = graphBuilder.RemoveActivityDependencies(activityId6, new HashSet<int> { 4 });
            result4.ShouldBeTrue();

            graphBuilder.Nodes.Count().ShouldBe(9);
            graphBuilder.Edges.Count().ShouldBe(9);

            graphBuilder.ClearCriticalPathVariables();
            graphBuilder.CalculateCriticalPathForwardFlow();
            graphBuilder.CalculateCriticalPathBackwardFlow();

            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(6);

            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(7);

            graphBuilder.Activity(activityId3).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.ShouldBe(8);

            graphBuilder.Activity(activityId4).EarliestStartTime.ShouldBe(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.ShouldBe(18);

            graphBuilder.Activity(activityId5).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId5).EarliestFinishTime.ShouldBe(16);

            graphBuilder.Activity(activityId6).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.ShouldBe(15);

            graphBuilder.Activity(activityId7).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.ShouldBe(22);

            graphBuilder.Activity(activityId8).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId8).EarliestFinishTime.ShouldBe(22);

            graphBuilder.Activity(activityId9).EarliestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId9).EarliestFinishTime.ShouldBe(26);
        }

        [Fact]
        public void VertexGraphBuilderExtensions_GivenCalculateCriticalPath_WhenAddingAndRemovingDependencies_ThenAsExpected()
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
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId1, 6));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId4, 11), new HashSet<int> { activityId2 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId5, 8), new HashSet<int> { activityId1, activityId2, activityId3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId6, 7), new HashSet<int> { activityId3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId7, 4), new HashSet<int> { activityId4 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId8, 4), new HashSet<int> { activityId4, activityId6 });
            graphBuilder.AddActivity(new Activity<int, int, int>(activityId9, 10), new HashSet<int> { activityId5 });

            graphBuilder.Nodes.Count().ShouldBe(9);
            graphBuilder.Edges.Count().ShouldBe(9);

            bool result1 = graphBuilder.AddActivityDependencies(activityId6, new HashSet<int> { 5 });
            result1.ShouldBeTrue();

            graphBuilder.Nodes.Count().ShouldBe(9);
            graphBuilder.Edges.Count().ShouldBe(10);

            graphBuilder.CalculateCriticalPath();

            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(2);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(8);

            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(1);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(8);

            graphBuilder.Activity(activityId3).LatestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).LatestFinishTime.ShouldBe(8);

            graphBuilder.Activity(activityId4).LatestStartTime.ShouldBe(12);
            graphBuilder.Activity(activityId4).LatestFinishTime.ShouldBe(23);

            graphBuilder.Activity(activityId5).LatestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId5).LatestFinishTime.ShouldBe(16);

            graphBuilder.Activity(activityId6).LatestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId6).LatestFinishTime.ShouldBe(23);

            graphBuilder.Activity(activityId7).LatestStartTime.ShouldBe(23);
            graphBuilder.Activity(activityId7).LatestFinishTime.ShouldBe(27);

            graphBuilder.Activity(activityId8).LatestStartTime.ShouldBe(23);
            graphBuilder.Activity(activityId8).LatestFinishTime.ShouldBe(27);

            graphBuilder.Activity(activityId9).LatestStartTime.ShouldBe(17);
            graphBuilder.Activity(activityId9).LatestFinishTime.ShouldBe(27);



            bool result2 = graphBuilder.AddActivityDependencies(activityId6, new HashSet<int> { 4 });
            result2.ShouldBeTrue();

            graphBuilder.Nodes.Count().ShouldBe(9);
            graphBuilder.Edges.Count().ShouldBe(11);

            graphBuilder.CalculateCriticalPath();

            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(4);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(10);

            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(7);

            graphBuilder.Activity(activityId3).LatestStartTime.ShouldBe(2);
            graphBuilder.Activity(activityId3).LatestFinishTime.ShouldBe(10);

            graphBuilder.Activity(activityId4).LatestStartTime.ShouldBe(7);
            graphBuilder.Activity(activityId4).LatestFinishTime.ShouldBe(18);

            graphBuilder.Activity(activityId5).LatestStartTime.ShouldBe(10);
            graphBuilder.Activity(activityId5).LatestFinishTime.ShouldBe(18);

            graphBuilder.Activity(activityId6).LatestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId6).LatestFinishTime.ShouldBe(25);

            graphBuilder.Activity(activityId7).LatestStartTime.ShouldBe(25);
            graphBuilder.Activity(activityId7).LatestFinishTime.ShouldBe(29);

            graphBuilder.Activity(activityId8).LatestStartTime.ShouldBe(25);
            graphBuilder.Activity(activityId8).LatestFinishTime.ShouldBe(29);

            graphBuilder.Activity(activityId9).LatestStartTime.ShouldBe(19);
            graphBuilder.Activity(activityId9).LatestFinishTime.ShouldBe(29);



            bool result3 = graphBuilder.RemoveActivityDependencies(activityId6, new HashSet<int> { 5 });
            result3.ShouldBeTrue();

            graphBuilder.Nodes.Count().ShouldBe(9);
            graphBuilder.Edges.Count().ShouldBe(10);

            graphBuilder.CalculateCriticalPath();

            graphBuilder.Activity(activityId1).LatestStartTime.ShouldBe(5);
            graphBuilder.Activity(activityId1).LatestFinishTime.ShouldBe(11);

            graphBuilder.Activity(activityId2).LatestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).LatestFinishTime.ShouldBe(7);

            graphBuilder.Activity(activityId3).LatestStartTime.ShouldBe(3);
            graphBuilder.Activity(activityId3).LatestFinishTime.ShouldBe(11);

            graphBuilder.Activity(activityId4).LatestStartTime.ShouldBe(7);
            graphBuilder.Activity(activityId4).LatestFinishTime.ShouldBe(18);

            graphBuilder.Activity(activityId5).LatestStartTime.ShouldBe(11);
            graphBuilder.Activity(activityId5).LatestFinishTime.ShouldBe(19);

            graphBuilder.Activity(activityId6).LatestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId6).LatestFinishTime.ShouldBe(25);

            graphBuilder.Activity(activityId7).LatestStartTime.ShouldBe(25);
            graphBuilder.Activity(activityId7).LatestFinishTime.ShouldBe(29);

            graphBuilder.Activity(activityId8).LatestStartTime.ShouldBe(25);
            graphBuilder.Activity(activityId8).LatestFinishTime.ShouldBe(29);

            graphBuilder.Activity(activityId9).LatestStartTime.ShouldBe(19);
            graphBuilder.Activity(activityId9).LatestFinishTime.ShouldBe(29);



            bool result4 = graphBuilder.RemoveActivityDependencies(activityId6, new HashSet<int> { 4 });
            result4.ShouldBeTrue();

            graphBuilder.Nodes.Count().ShouldBe(9);
            graphBuilder.Edges.Count().ShouldBe(9);

            graphBuilder.CalculateCriticalPath();

            graphBuilder.Activity(activityId1).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.ShouldBe(6);

            graphBuilder.Activity(activityId2).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.ShouldBe(7);

            graphBuilder.Activity(activityId3).EarliestStartTime.ShouldBe(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.ShouldBe(8);

            graphBuilder.Activity(activityId4).EarliestStartTime.ShouldBe(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.ShouldBe(18);

            graphBuilder.Activity(activityId5).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId5).EarliestFinishTime.ShouldBe(16);

            graphBuilder.Activity(activityId6).EarliestStartTime.ShouldBe(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.ShouldBe(15);

            graphBuilder.Activity(activityId7).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.ShouldBe(22);

            graphBuilder.Activity(activityId8).EarliestStartTime.ShouldBe(18);
            graphBuilder.Activity(activityId8).EarliestFinishTime.ShouldBe(22);

            graphBuilder.Activity(activityId9).EarliestStartTime.ShouldBe(16);
            graphBuilder.Activity(activityId9).EarliestFinishTime.ShouldBe(26);
        }

        [Fact]
        public void VertexGraphBuilderExtensions_GivenCalculateCriticalPathPriorityList_ThenCorrectOrder()
        {
            int eventId = 0;
            int dummyActivityId = 100;
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int, int>(1, 6));
            graphBuilder.AddActivity(new Activity<int, int, int>(2, 7));
            graphBuilder.AddActivity(new Activity<int, int, int>(3, 8));
            graphBuilder.AddActivity(new Activity<int, int, int>(4, 11), new HashSet<int> { 2 });
            graphBuilder.AddActivity(new Activity<int, int, int>(5, 8), new HashSet<int> { 1, 2, 3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(6, 7), new HashSet<int> { 3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(7, 4), new HashSet<int> { 4 });
            graphBuilder.AddActivity(new Activity<int, int, int>(8, 4), new HashSet<int> { 4, 6 });
            graphBuilder.AddActivity(new Activity<int, int, int>(9, 10), new HashSet<int> { 5 });

            List<int> priorityList = graphBuilder.CalculateCriticalPathPriorityList().ToList();

            priorityList.ShouldBeEquivalentTo(new List<int>(new[] { 3, 2, 1, 5, 4, 6, 9, 7, 8 }));
        }

        [Fact]
        public void VertexGraphBuilderExtensions_GivenCalculateResourceSchedulesByPriorityList_WhenOneResource_ThenCorrectOrder()
        {
            int eventId = 0;
            int dummyActivityId = 100;
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int, int>(1, 6));
            graphBuilder.AddActivity(new Activity<int, int, int>(2, 7));
            graphBuilder.AddActivity(new Activity<int, int, int>(3, 8));
            graphBuilder.AddActivity(new Activity<int, int, int>(4, 11), new HashSet<int> { 2 });
            graphBuilder.AddActivity(new Activity<int, int, int>(5, 8), new HashSet<int> { 1, 2, 3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(6, 7), new HashSet<int> { 3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(7, 4), new HashSet<int> { 4 });
            graphBuilder.AddActivity(new Activity<int, int, int>(8, 4), new HashSet<int> { 4, 6 });
            graphBuilder.AddActivity(new Activity<int, int, int>(9, 10), new HashSet<int> { 5 });

            int resourceId1 = 1;

            IList<IResourceSchedule<int, int, int>> resourceSchedules =
                graphBuilder.CalculateResourceSchedulesByPriorityList(
                    new List<IResource<int, int>>(new[]
                    {
                        new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>())
                    })).ToList();
            resourceSchedules.Count.ShouldBe(1);

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(9);
            scheduledActivities0[0].Id.ShouldBe(3);
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(8);

            scheduledActivities0[1].Id.ShouldBe(2);
            scheduledActivities0[1].StartTime.ShouldBe(8);
            scheduledActivities0[1].FinishTime.ShouldBe(15);

            scheduledActivities0[2].Id.ShouldBe(1);
            scheduledActivities0[2].StartTime.ShouldBe(15);
            scheduledActivities0[2].FinishTime.ShouldBe(21);

            scheduledActivities0[3].Id.ShouldBe(5);
            scheduledActivities0[3].StartTime.ShouldBe(21);
            scheduledActivities0[3].FinishTime.ShouldBe(29);

            scheduledActivities0[4].Id.ShouldBe(4);
            scheduledActivities0[4].StartTime.ShouldBe(29);
            scheduledActivities0[4].FinishTime.ShouldBe(40);

            scheduledActivities0[5].Id.ShouldBe(6);
            scheduledActivities0[5].StartTime.ShouldBe(40);
            scheduledActivities0[5].FinishTime.ShouldBe(47);

            scheduledActivities0[6].Id.ShouldBe(9);
            scheduledActivities0[6].StartTime.ShouldBe(47);
            scheduledActivities0[6].FinishTime.ShouldBe(57);

            scheduledActivities0[7].Id.ShouldBe(7);
            scheduledActivities0[7].StartTime.ShouldBe(57);
            scheduledActivities0[7].FinishTime.ShouldBe(61);

            scheduledActivities0[8].Id.ShouldBe(8);
            scheduledActivities0[8].StartTime.ShouldBe(61);
            scheduledActivities0[8].FinishTime.ShouldBe(65);

            scheduledActivities0.Last().FinishTime.ShouldBe(65);
        }

        [Fact]
        public void VertexGraphBuilderExtensions_GivenCalculateResourceSchedulesByPriority_WhenListTwoResources_ThenCorrectOrder()
        {
            int eventId = 0;
            int dummyActivityId = 100;
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int, int>(1, 6));
            graphBuilder.AddActivity(new Activity<int, int, int>(2, 7));
            graphBuilder.AddActivity(new Activity<int, int, int>(3, 8));
            graphBuilder.AddActivity(new Activity<int, int, int>(4, 11), new HashSet<int> { 2 });
            graphBuilder.AddActivity(new Activity<int, int, int>(5, 8), new HashSet<int> { 1, 2, 3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(6, 7), new HashSet<int> { 3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(7, 4), new HashSet<int> { 4 });
            graphBuilder.AddActivity(new Activity<int, int, int>(8, 4), new HashSet<int> { 4, 6 });
            graphBuilder.AddActivity(new Activity<int, int, int>(9, 10), new HashSet<int> { 5 });

            int resourceId1 = 1;
            int resourceId2 = resourceId1 + 1;

            IList<IResourceSchedule<int, int, int>> resourceSchedules =
                graphBuilder.CalculateResourceSchedulesByPriorityList(
                    new List<IResource<int, int>>(new[]
                    {
                        new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>()),
                        new Resource<int, int>(resourceId2, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>())
                    })).ToList();
            resourceSchedules.Count.ShouldBe(2);

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(5);
            scheduledActivities0[0].Id.ShouldBe(3);
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(8);

            scheduledActivities0[1].Id.ShouldBe(4);
            scheduledActivities0[1].StartTime.ShouldBe(8);
            scheduledActivities0[1].FinishTime.ShouldBe(19);

            scheduledActivities0[2].Id.ShouldBe(6);
            scheduledActivities0[2].StartTime.ShouldBe(19);
            scheduledActivities0[2].FinishTime.ShouldBe(26);

            scheduledActivities0[3].Id.ShouldBe(7);
            scheduledActivities0[3].StartTime.ShouldBe(26);
            scheduledActivities0[3].FinishTime.ShouldBe(30);

            scheduledActivities0[4].Id.ShouldBe(8);
            scheduledActivities0[4].StartTime.ShouldBe(30);
            scheduledActivities0[4].FinishTime.ShouldBe(34);

            scheduledActivities0.Last().FinishTime.ShouldBe(34);


            var resourceSchedule1 = resourceSchedules[1];
            resourceSchedule1.Resource.Id.ShouldBe(resourceId2);
            var scheduledActivities1 = resourceSchedule1.ScheduledActivities.ToList();
            scheduledActivities1.Count.ShouldBe(4);

            scheduledActivities1[0].Id.ShouldBe(2);
            scheduledActivities1[0].StartTime.ShouldBe(0);
            scheduledActivities1[0].FinishTime.ShouldBe(7);

            scheduledActivities1[1].Id.ShouldBe(1);
            scheduledActivities1[1].StartTime.ShouldBe(7);
            scheduledActivities1[1].FinishTime.ShouldBe(13);

            scheduledActivities1[2].Id.ShouldBe(5);
            scheduledActivities1[2].StartTime.ShouldBe(13);
            scheduledActivities1[2].FinishTime.ShouldBe(21);

            scheduledActivities1[3].Id.ShouldBe(9);
            scheduledActivities1[3].StartTime.ShouldBe(21);
            scheduledActivities1[3].FinishTime.ShouldBe(31);

            scheduledActivities1.Last().FinishTime.ShouldBe(31);
        }

        [Fact]
        public void VertexGraphBuilderExtensions_GivenCalculateResourceSchedulesByPriorityList_WhenThreeResources_ThenCorrectOrder()
        {
            int eventId = 0;
            int dummyActivityId = 100;
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int, int>(1, 6));
            graphBuilder.AddActivity(new Activity<int, int, int>(2, 7));
            graphBuilder.AddActivity(new Activity<int, int, int>(3, 8));
            graphBuilder.AddActivity(new Activity<int, int, int>(4, 11), new HashSet<int> { 2 });
            graphBuilder.AddActivity(new Activity<int, int, int>(5, 8), new HashSet<int> { 1, 2, 3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(6, 7), new HashSet<int> { 3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(7, 4), new HashSet<int> { 4 });
            graphBuilder.AddActivity(new Activity<int, int, int>(8, 4), new HashSet<int> { 4, 6 });
            graphBuilder.AddActivity(new Activity<int, int, int>(9, 10), new HashSet<int> { 5 });

            int resourceId1 = 1;
            int resourceId2 = resourceId1 + 1;
            int resourceId3 = resourceId2 + 1;

            IList<IResourceSchedule<int, int, int>> resourceSchedules =
                graphBuilder.CalculateResourceSchedulesByPriorityList(
                    new List<IResource<int, int>>(new[]
                    {
                        new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>()),
                        new Resource<int, int>(resourceId2, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>()),
                        new Resource<int, int>(resourceId3, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>())
                    })).ToList();
            resourceSchedules.Count.ShouldBe(3);

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(3);
            scheduledActivities0[0].Id.ShouldBe(3);
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(8);

            scheduledActivities0[1].Id.ShouldBe(5);
            scheduledActivities0[1].StartTime.ShouldBe(8);
            scheduledActivities0[1].FinishTime.ShouldBe(16);

            scheduledActivities0[2].Id.ShouldBe(9);
            scheduledActivities0[2].StartTime.ShouldBe(16);
            scheduledActivities0[2].FinishTime.ShouldBe(26);

            scheduledActivities0.Last().FinishTime.ShouldBe(26);


            var resourceSchedule1 = resourceSchedules[1];
            resourceSchedule1.Resource.Id.ShouldBe(resourceId2);
            var scheduledActivities1 = resourceSchedule1.ScheduledActivities.ToList();
            scheduledActivities1.Count.ShouldBe(3);
            scheduledActivities1[0].Id.ShouldBe(2);
            scheduledActivities1[0].StartTime.ShouldBe(0);
            scheduledActivities1[0].FinishTime.ShouldBe(7);

            scheduledActivities1[1].Id.ShouldBe(4);
            scheduledActivities1[1].StartTime.ShouldBe(7);
            scheduledActivities1[1].FinishTime.ShouldBe(18);

            scheduledActivities1[2].Id.ShouldBe(7);
            scheduledActivities1[2].StartTime.ShouldBe(18);
            scheduledActivities1[2].FinishTime.ShouldBe(22);

            scheduledActivities1.Last().FinishTime.ShouldBe(22);


            var resourceSchedule2 = resourceSchedules[2];
            resourceSchedule2.Resource.Id.ShouldBe(resourceId3);
            var scheduledActivities2 = resourceSchedule2.ScheduledActivities.ToList();
            scheduledActivities2.Count.ShouldBe(3);
            scheduledActivities2[0].Id.ShouldBe(1);
            scheduledActivities2[0].StartTime.ShouldBe(0);
            scheduledActivities2[0].FinishTime.ShouldBe(6);

            scheduledActivities2[1].Id.ShouldBe(6);
            scheduledActivities2[1].StartTime.ShouldBe(8);
            scheduledActivities2[1].FinishTime.ShouldBe(15);

            scheduledActivities2[2].Id.ShouldBe(8);
            scheduledActivities2[2].StartTime.ShouldBe(18);
            scheduledActivities2[2].FinishTime.ShouldBe(22);

            scheduledActivities2.Last().FinishTime.ShouldBe(22);
        }

        [Fact]
        public void VertexGraphBuilderExtensions_GivenCalculateResourceSchedulesByPriorityList_WhenFourResources_ThenCorrectOrder()
        {
            int eventId = 0;
            int dummyActivityId = 100;
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int, int>(1, 6));
            graphBuilder.AddActivity(new Activity<int, int, int>(2, 7));
            graphBuilder.AddActivity(new Activity<int, int, int>(3, 8));
            graphBuilder.AddActivity(new Activity<int, int, int>(4, 11), new HashSet<int> { 2 });
            graphBuilder.AddActivity(new Activity<int, int, int>(5, 8), new HashSet<int> { 1, 2, 3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(6, 7), new HashSet<int> { 3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(7, 4), new HashSet<int> { 4 });
            graphBuilder.AddActivity(new Activity<int, int, int>(8, 4), new HashSet<int> { 4, 6 });
            graphBuilder.AddActivity(new Activity<int, int, int>(9, 10), new HashSet<int> { 5 });

            int resourceId1 = 1;
            int resourceId2 = resourceId1 + 1;
            int resourceId3 = resourceId2 + 1;
            int resourceId4 = resourceId3 + 1;

            IList<IResourceSchedule<int, int, int>> resourceSchedules =
                graphBuilder.CalculateResourceSchedulesByPriorityList(
                    new List<IResource<int, int>>(new[]
                    {
                        new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>()),
                        new Resource<int, int>(resourceId2, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>()),
                        new Resource<int, int>(resourceId3, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>()),
                        new Resource<int, int>(resourceId4, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0, Enumerable.Empty<int>())
                    })).ToList();
            resourceSchedules.Count.ShouldBe(3);

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId1);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(3);
            scheduledActivities0[0].Id.ShouldBe(3);
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(8);

            scheduledActivities0[1].Id.ShouldBe(5);
            scheduledActivities0[1].StartTime.ShouldBe(8);
            scheduledActivities0[1].FinishTime.ShouldBe(16);

            scheduledActivities0[2].Id.ShouldBe(9);
            scheduledActivities0[2].StartTime.ShouldBe(16);
            scheduledActivities0[2].FinishTime.ShouldBe(26);

            scheduledActivities0.Last().FinishTime.ShouldBe(26);


            var resourceSchedule1 = resourceSchedules[1];
            resourceSchedule1.Resource.Id.ShouldBe(resourceId2);
            var scheduledActivities1 = resourceSchedule1.ScheduledActivities.ToList();
            scheduledActivities1.Count.ShouldBe(3);
            scheduledActivities1[0].Id.ShouldBe(2);
            scheduledActivities1[0].StartTime.ShouldBe(0);
            scheduledActivities1[0].FinishTime.ShouldBe(7);

            scheduledActivities1[1].Id.ShouldBe(4);
            scheduledActivities1[1].StartTime.ShouldBe(7);
            scheduledActivities1[1].FinishTime.ShouldBe(18);

            scheduledActivities1[2].Id.ShouldBe(7);
            scheduledActivities1[2].StartTime.ShouldBe(18);
            scheduledActivities1[2].FinishTime.ShouldBe(22);

            scheduledActivities1.Last().FinishTime.ShouldBe(22);


            var resourceSchedule2 = resourceSchedules[2];
            resourceSchedule2.Resource.Id.ShouldBe(resourceId3);
            var scheduledActivities2 = resourceSchedule2.ScheduledActivities.ToList();
            scheduledActivities2.Count.ShouldBe(3);
            scheduledActivities2[0].Id.ShouldBe(1);
            scheduledActivities2[0].StartTime.ShouldBe(0);
            scheduledActivities2[0].FinishTime.ShouldBe(6);

            scheduledActivities2[1].Id.ShouldBe(6);
            scheduledActivities2[1].StartTime.ShouldBe(8);
            scheduledActivities2[1].FinishTime.ShouldBe(15);

            scheduledActivities2[2].Id.ShouldBe(8);
            scheduledActivities2[2].StartTime.ShouldBe(18);
            scheduledActivities2[2].FinishTime.ShouldBe(22);

            scheduledActivities2.Last().FinishTime.ShouldBe(22);
        }

        [Fact]
        public void VertexGraphBuilderExtensions_GivenCalculateResourceSchedulesByPriorityList_WhenFourOrderedResources_ThenCorrectOrder()
        {
            int eventId = 0;
            int dummyActivityId = 100;
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int, int>(1, 6));
            graphBuilder.AddActivity(new Activity<int, int, int>(2, 7));
            graphBuilder.AddActivity(new Activity<int, int, int>(3, 8));
            graphBuilder.AddActivity(new Activity<int, int, int>(4, 11), new HashSet<int> { 2 });
            graphBuilder.AddActivity(new Activity<int, int, int>(5, 8), new HashSet<int> { 1, 2, 3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(6, 7), new HashSet<int> { 3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(7, 4), new HashSet<int> { 4 });
            graphBuilder.AddActivity(new Activity<int, int, int>(8, 4), new HashSet<int> { 4, 6 });
            graphBuilder.AddActivity(new Activity<int, int, int>(9, 10), new HashSet<int> { 5 });

            int resourceId1 = 1;
            int resourceId2 = resourceId1 + 1;
            int resourceId3 = resourceId2 + 1;
            int resourceId4 = resourceId3 + 1;

            IList<IResourceSchedule<int, int, int>> resourceSchedules =
                graphBuilder.CalculateResourceSchedulesByPriorityList(
                    new List<IResource<int, int>>(new[]
                    {
                        new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 4, Enumerable.Empty<int>()),
                        new Resource<int, int>(resourceId2, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 3, Enumerable.Empty<int>()),
                        new Resource<int, int>(resourceId3, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 2, Enumerable.Empty<int>()),
                        new Resource<int, int>(resourceId4, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 1, Enumerable.Empty<int>())
                    })).ToList();
            resourceSchedules.Count.ShouldBe(3);

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.Id.ShouldBe(resourceId4);
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(3);
            scheduledActivities0[0].Id.ShouldBe(3);
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(8);

            scheduledActivities0[1].Id.ShouldBe(5);
            scheduledActivities0[1].StartTime.ShouldBe(8);
            scheduledActivities0[1].FinishTime.ShouldBe(16);

            scheduledActivities0[2].Id.ShouldBe(9);
            scheduledActivities0[2].StartTime.ShouldBe(16);
            scheduledActivities0[2].FinishTime.ShouldBe(26);

            scheduledActivities0.Last().FinishTime.ShouldBe(26);


            var resourceSchedule1 = resourceSchedules[1];
            resourceSchedule1.Resource.Id.ShouldBe(resourceId3);
            var scheduledActivities1 = resourceSchedule1.ScheduledActivities.ToList();
            scheduledActivities1.Count.ShouldBe(3);
            scheduledActivities1[0].Id.ShouldBe(2);
            scheduledActivities1[0].StartTime.ShouldBe(0);
            scheduledActivities1[0].FinishTime.ShouldBe(7);

            scheduledActivities1[1].Id.ShouldBe(4);
            scheduledActivities1[1].StartTime.ShouldBe(7);
            scheduledActivities1[1].FinishTime.ShouldBe(18);

            scheduledActivities1[2].Id.ShouldBe(7);
            scheduledActivities1[2].StartTime.ShouldBe(18);
            scheduledActivities1[2].FinishTime.ShouldBe(22);

            scheduledActivities1.Last().FinishTime.ShouldBe(22);


            var resourceSchedule2 = resourceSchedules[2];
            resourceSchedule2.Resource.Id.ShouldBe(resourceId2);
            var scheduledActivities2 = resourceSchedule2.ScheduledActivities.ToList();
            scheduledActivities2.Count.ShouldBe(3);
            scheduledActivities2[0].Id.ShouldBe(1);
            scheduledActivities2[0].StartTime.ShouldBe(0);
            scheduledActivities2[0].FinishTime.ShouldBe(6);

            scheduledActivities2[1].Id.ShouldBe(6);
            scheduledActivities2[1].StartTime.ShouldBe(8);
            scheduledActivities2[1].FinishTime.ShouldBe(15);

            scheduledActivities2[2].Id.ShouldBe(8);
            scheduledActivities2[2].StartTime.ShouldBe(18);
            scheduledActivities2[2].FinishTime.ShouldBe(22);

            scheduledActivities2.Last().FinishTime.ShouldBe(22);
        }

        [Fact]
        public void VertexGraphBuilderExtensions_GivenCalculateResourceSchedulesByPriorityList_WhenUnlimitedResources_ThenCorrectOrder()
        {
            int eventId = 0;
            int dummyActivityId = 100;
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int, int>(1, 6));
            graphBuilder.AddActivity(new Activity<int, int, int>(2, 7));
            graphBuilder.AddActivity(new Activity<int, int, int>(3, 8));
            graphBuilder.AddActivity(new Activity<int, int, int>(4, 11), new HashSet<int> { 2 });
            graphBuilder.AddActivity(new Activity<int, int, int>(5, 8), new HashSet<int> { 1, 2, 3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(6, 7), new HashSet<int> { 3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(7, 4), new HashSet<int> { 4 });
            graphBuilder.AddActivity(new Activity<int, int, int>(8, 4), new HashSet<int> { 4, 6 });
            graphBuilder.AddActivity(new Activity<int, int, int>(9, 10), new HashSet<int> { 5 });

            IList<IResourceSchedule<int, int, int>> resourceSchedules = graphBuilder.CalculateResourceSchedulesByPriorityList(new List<IResource<int, int>>()).ToList();
            resourceSchedules.Count.ShouldBe(3);

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.ShouldBeNull();
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(3);
            scheduledActivities0[0].Id.ShouldBe(3);
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(8);

            scheduledActivities0[1].Id.ShouldBe(5);
            scheduledActivities0[1].StartTime.ShouldBe(8);
            scheduledActivities0[1].FinishTime.ShouldBe(16);

            scheduledActivities0[2].Id.ShouldBe(9);
            scheduledActivities0[2].StartTime.ShouldBe(16);
            scheduledActivities0[2].FinishTime.ShouldBe(26);

            scheduledActivities0.Last().FinishTime.ShouldBe(26);


            var resourceSchedule1 = resourceSchedules[1];
            resourceSchedule1.Resource.ShouldBeNull();
            var scheduledActivities1 = resourceSchedule1.ScheduledActivities.ToList();
            scheduledActivities1.Count.ShouldBe(3);
            scheduledActivities1[0].Id.ShouldBe(2);
            scheduledActivities1[0].StartTime.ShouldBe(0);
            scheduledActivities1[0].FinishTime.ShouldBe(7);

            scheduledActivities1[1].Id.ShouldBe(4);
            scheduledActivities1[1].StartTime.ShouldBe(7);
            scheduledActivities1[1].FinishTime.ShouldBe(18);

            scheduledActivities1[2].Id.ShouldBe(7);
            scheduledActivities1[2].StartTime.ShouldBe(18);
            scheduledActivities1[2].FinishTime.ShouldBe(22);

            scheduledActivities1.Last().FinishTime.ShouldBe(22);


            var resourceSchedule2 = resourceSchedules[2];
            resourceSchedule2.Resource.ShouldBeNull();
            var scheduledActivities2 = resourceSchedule2.ScheduledActivities.ToList();
            scheduledActivities2.Count.ShouldBe(3);
            scheduledActivities2[0].Id.ShouldBe(1);
            scheduledActivities2[0].StartTime.ShouldBe(0);
            scheduledActivities2[0].FinishTime.ShouldBe(6);

            scheduledActivities2[1].Id.ShouldBe(6);
            scheduledActivities2[1].StartTime.ShouldBe(8);
            scheduledActivities2[1].FinishTime.ShouldBe(15);

            scheduledActivities2[2].Id.ShouldBe(8);
            scheduledActivities2[2].StartTime.ShouldBe(18);
            scheduledActivities2[2].FinishTime.ShouldBe(22);

            scheduledActivities2.Last().FinishTime.ShouldBe(22);
        }

        [Fact]
        public void VertexGraphBuilderExtensions_GivenCalculateResourceSchedulesByPriorityList_WhenUnlimitedResourcesWithPotentialOverlap_ThenCorrectOrder()
        {
            int eventId = 0;
            int dummyActivityId = 100;
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int, int>(1, 1));
            graphBuilder.AddActivity(new Activity<int, int, int>(2, 2) { MinimumEarliestStartTime = 1, MaximumLatestFinishTime = 4 }, new HashSet<int> { 1 });
            graphBuilder.AddActivity(new Activity<int, int, int>(3, 2), new HashSet<int> { 1 });
            graphBuilder.AddActivity(new Activity<int, int, int>(4, 2), new HashSet<int> { 1 });
            graphBuilder.AddActivity(new Activity<int, int, int>(5, 2));

            IList<IResourceSchedule<int, int, int>> resourceSchedules = graphBuilder.CalculateResourceSchedulesByPriorityList(new List<IResource<int, int>>()).ToList();
            resourceSchedules.Count.ShouldBe(3);

            var resourceSchedule0 = resourceSchedules[0];
            resourceSchedule0.Resource.ShouldBeNull();
            var scheduledActivities0 = resourceSchedule0.ScheduledActivities.ToList();
            scheduledActivities0.Count.ShouldBe(2);
            scheduledActivities0[0].Id.ShouldBe(1);
            scheduledActivities0[0].StartTime.ShouldBe(0);
            scheduledActivities0[0].FinishTime.ShouldBe(1);

            scheduledActivities0[1].Id.ShouldBe(3);
            scheduledActivities0[1].StartTime.ShouldBe(1);
            scheduledActivities0[1].FinishTime.ShouldBe(3);

            scheduledActivities0.Last().FinishTime.ShouldBe(3);


            var resourceSchedule1 = resourceSchedules[1];
            resourceSchedule1.Resource.ShouldBeNull();
            var scheduledActivities1 = resourceSchedule1.ScheduledActivities.ToList();
            scheduledActivities1.Count.ShouldBe(2);
            scheduledActivities1[0].Id.ShouldBe(5);
            scheduledActivities1[0].StartTime.ShouldBe(0);
            scheduledActivities1[0].FinishTime.ShouldBe(2);

            scheduledActivities1[1].Id.ShouldBe(2);
            scheduledActivities1[1].StartTime.ShouldBe(2);
            scheduledActivities1[1].FinishTime.ShouldBe(4);

            scheduledActivities1.Last().FinishTime.ShouldBe(4);


            var resourceSchedule2 = resourceSchedules[2];
            resourceSchedule2.Resource.ShouldBeNull();
            var scheduledActivities2 = resourceSchedule2.ScheduledActivities.ToList();
            scheduledActivities2.Count.ShouldBe(1);
            scheduledActivities2[0].Id.ShouldBe(4);
            scheduledActivities2[0].StartTime.ShouldBe(1);
            scheduledActivities2[0].FinishTime.ShouldBe(3);

            scheduledActivities2.Last().FinishTime.ShouldBe(3);
        }
    }
}
