using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class ArrowGraphBuilderExtensionsTests
    {
        [Fact]
        public void ArrowGraphBuilderExtensions_GivenCalculateCriticalPath_ThenAsExpected()
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
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int>(activityId1, 6));
            graphBuilder.AddActivity(new Activity<int, int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int, int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int, int>(activityId4, 11), new HashSet<int>(new[] { 2 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId5, 8), new HashSet<int>(new[] { 1, 2, 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId6, 7), new HashSet<int>(new[] { 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId7, 4), new HashSet<int>(new[] { 4 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId8, 4), new HashSet<int>(new[] { 4, 6 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId9, 10), new HashSet<int>(new[] { 5 }));

            graphBuilder.CalculateCriticalPath();

            graphBuilder.Activity(activityId1).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.Should().Be(6);
            graphBuilder.Activity(activityId1).FreeSlack.Should().Be(2);
            graphBuilder.Activity(activityId1).TotalSlack.Should().Be(2);
            graphBuilder.Activity(activityId1).LatestStartTime.Should().Be(2);
            graphBuilder.Activity(activityId1).LatestFinishTime.Should().Be(8);

            graphBuilder.Activity(activityId2).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.Should().Be(7);
            graphBuilder.Activity(activityId2).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId2).TotalSlack.Should().Be(1);
            graphBuilder.Activity(activityId2).LatestStartTime.Should().Be(1);
            graphBuilder.Activity(activityId2).LatestFinishTime.Should().Be(8);

            graphBuilder.Activity(activityId3).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.Should().Be(8);
            graphBuilder.Activity(activityId3).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId3).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId3).LatestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId3).LatestFinishTime.Should().Be(8);

            graphBuilder.Activity(activityId4).EarliestStartTime.Should().Be(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.Should().Be(18);
            graphBuilder.Activity(activityId4).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId4).TotalSlack.Should().Be(4);
            graphBuilder.Activity(activityId4).LatestStartTime.Should().Be(11);
            graphBuilder.Activity(activityId4).LatestFinishTime.Should().Be(22);

            graphBuilder.Activity(activityId5).EarliestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId5).EarliestFinishTime.Should().Be(16);
            graphBuilder.Activity(activityId5).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId5).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId5).LatestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId5).LatestFinishTime.Should().Be(16);

            graphBuilder.Activity(activityId6).EarliestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.Should().Be(15);
            graphBuilder.Activity(activityId6).FreeSlack.Should().Be(3);
            graphBuilder.Activity(activityId6).TotalSlack.Should().Be(7);
            graphBuilder.Activity(activityId6).LatestStartTime.Should().Be(15);
            graphBuilder.Activity(activityId6).LatestFinishTime.Should().Be(22);

            graphBuilder.Activity(activityId7).EarliestStartTime.Should().Be(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.Should().Be(22);
            graphBuilder.Activity(activityId7).FreeSlack.Should().Be(4);
            graphBuilder.Activity(activityId7).TotalSlack.Should().Be(4);
            graphBuilder.Activity(activityId7).LatestStartTime.Should().Be(22);
            graphBuilder.Activity(activityId7).LatestFinishTime.Should().Be(26);

            graphBuilder.Activity(activityId8).EarliestStartTime.Should().Be(18);
            graphBuilder.Activity(activityId8).EarliestFinishTime.Should().Be(22);
            graphBuilder.Activity(activityId8).FreeSlack.Should().Be(4);
            graphBuilder.Activity(activityId8).TotalSlack.Should().Be(4);
            graphBuilder.Activity(activityId8).LatestStartTime.Should().Be(22);
            graphBuilder.Activity(activityId8).LatestFinishTime.Should().Be(26);

            graphBuilder.Activity(activityId9).EarliestStartTime.Should().Be(16);
            graphBuilder.Activity(activityId9).EarliestFinishTime.Should().Be(26);
            graphBuilder.Activity(activityId9).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId9).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId9).LatestStartTime.Should().Be(16);
            graphBuilder.Activity(activityId9).LatestFinishTime.Should().Be(26);
        }

        [Fact]
        public void ArrowGraphBuilderExtensions_GivenCalculateCriticalPath_WhenMinimumFreeSlackInStartActivity_ThenAsExpected()
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
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int>(activityId1, 6) { MinimumFreeSlack = 10 });
            graphBuilder.AddActivity(new Activity<int, int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int, int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int, int>(activityId4, 11), new HashSet<int>(new[] { 2 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId5, 8), new HashSet<int>(new[] { 1, 2, 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId6, 7), new HashSet<int>(new[] { 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId7, 4), new HashSet<int>(new[] { 4 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId8, 4), new HashSet<int>(new[] { 4, 6 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId9, 10), new HashSet<int>(new[] { 5 }));

            graphBuilder.CalculateCriticalPath();

            graphBuilder.Activity(activityId1).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.Should().Be(6);
            graphBuilder.Activity(activityId1).FreeSlack.Should().Be(10);
            graphBuilder.Activity(activityId1).TotalSlack.Should().Be(10);
            graphBuilder.Activity(activityId1).LatestStartTime.Should().Be(10);
            graphBuilder.Activity(activityId1).LatestFinishTime.Should().Be(16);

            graphBuilder.Activity(activityId2).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.Should().Be(7);
            graphBuilder.Activity(activityId2).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId2).TotalSlack.Should().Be(9);
            graphBuilder.Activity(activityId2).LatestStartTime.Should().Be(9);
            graphBuilder.Activity(activityId2).LatestFinishTime.Should().Be(16);

            graphBuilder.Activity(activityId3).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.Should().Be(8);
            graphBuilder.Activity(activityId3).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId3).TotalSlack.Should().Be(8);
            graphBuilder.Activity(activityId3).LatestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId3).LatestFinishTime.Should().Be(16);

            graphBuilder.Activity(activityId4).EarliestStartTime.Should().Be(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.Should().Be(18);
            graphBuilder.Activity(activityId4).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId4).TotalSlack.Should().Be(12);
            graphBuilder.Activity(activityId4).LatestStartTime.Should().Be(19);
            graphBuilder.Activity(activityId4).LatestFinishTime.Should().Be(30);

            graphBuilder.Activity(activityId5).EarliestStartTime.Should().Be(16);
            graphBuilder.Activity(activityId5).EarliestFinishTime.Should().Be(24);
            graphBuilder.Activity(activityId5).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId5).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId5).LatestStartTime.Should().Be(16);
            graphBuilder.Activity(activityId5).LatestFinishTime.Should().Be(24);

            graphBuilder.Activity(activityId6).EarliestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.Should().Be(15);
            graphBuilder.Activity(activityId6).FreeSlack.Should().Be(3);
            graphBuilder.Activity(activityId6).TotalSlack.Should().Be(15);
            graphBuilder.Activity(activityId6).LatestStartTime.Should().Be(23);
            graphBuilder.Activity(activityId6).LatestFinishTime.Should().Be(30);

            graphBuilder.Activity(activityId7).EarliestStartTime.Should().Be(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.Should().Be(22);
            graphBuilder.Activity(activityId7).FreeSlack.Should().Be(12);
            graphBuilder.Activity(activityId7).TotalSlack.Should().Be(12);
            graphBuilder.Activity(activityId7).LatestStartTime.Should().Be(30);
            graphBuilder.Activity(activityId7).LatestFinishTime.Should().Be(34);

            graphBuilder.Activity(activityId8).EarliestStartTime.Should().Be(18);
            graphBuilder.Activity(activityId8).EarliestFinishTime.Should().Be(22);
            graphBuilder.Activity(activityId8).FreeSlack.Should().Be(12);
            graphBuilder.Activity(activityId8).TotalSlack.Should().Be(12);
            graphBuilder.Activity(activityId8).LatestStartTime.Should().Be(30);
            graphBuilder.Activity(activityId8).LatestFinishTime.Should().Be(34);

            graphBuilder.Activity(activityId9).EarliestStartTime.Should().Be(24);
            graphBuilder.Activity(activityId9).EarliestFinishTime.Should().Be(34);
            graphBuilder.Activity(activityId9).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId9).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId9).LatestStartTime.Should().Be(24);
            graphBuilder.Activity(activityId9).LatestFinishTime.Should().Be(34);
        }

        [Fact]
        public void ArrowGraphBuilderExtensions_GivenCalculateCriticalPath_WhenMinimumFreeSlackInNormalActivity_ThenAsExpected()
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
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int>(activityId1, 6));
            graphBuilder.AddActivity(new Activity<int, int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int, int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int, int>(activityId4, 11), new HashSet<int>(new[] { 2 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId5, 8) { MinimumFreeSlack = 15 }, new HashSet<int>(new[] { 1, 2, 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId6, 7), new HashSet<int>(new[] { 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId7, 4), new HashSet<int>(new[] { 4 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId8, 4), new HashSet<int>(new[] { 4, 6 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId9, 10), new HashSet<int>(new[] { 5 }));

            graphBuilder.CalculateCriticalPath();

            graphBuilder.Activity(activityId1).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.Should().Be(6);
            graphBuilder.Activity(activityId1).FreeSlack.Should().Be(2);
            graphBuilder.Activity(activityId1).TotalSlack.Should().Be(17);
            graphBuilder.Activity(activityId1).LatestStartTime.Should().Be(17);
            graphBuilder.Activity(activityId1).LatestFinishTime.Should().Be(23);

            graphBuilder.Activity(activityId2).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.Should().Be(7);
            graphBuilder.Activity(activityId2).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId2).TotalSlack.Should().Be(16);
            graphBuilder.Activity(activityId2).LatestStartTime.Should().Be(16);
            graphBuilder.Activity(activityId2).LatestFinishTime.Should().Be(23);

            graphBuilder.Activity(activityId3).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.Should().Be(8);
            graphBuilder.Activity(activityId3).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId3).TotalSlack.Should().Be(15);
            graphBuilder.Activity(activityId3).LatestStartTime.Should().Be(15);
            graphBuilder.Activity(activityId3).LatestFinishTime.Should().Be(23);

            graphBuilder.Activity(activityId4).EarliestStartTime.Should().Be(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.Should().Be(18);
            graphBuilder.Activity(activityId4).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId4).TotalSlack.Should().Be(19);
            graphBuilder.Activity(activityId4).LatestStartTime.Should().Be(26);
            graphBuilder.Activity(activityId4).LatestFinishTime.Should().Be(37);

            graphBuilder.Activity(activityId5).EarliestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId5).EarliestFinishTime.Should().Be(16);
            graphBuilder.Activity(activityId5).FreeSlack.Should().Be(15);
            graphBuilder.Activity(activityId5).TotalSlack.Should().Be(15);
            graphBuilder.Activity(activityId5).LatestStartTime.Should().Be(23);
            graphBuilder.Activity(activityId5).LatestFinishTime.Should().Be(31);

            graphBuilder.Activity(activityId6).EarliestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.Should().Be(15);
            graphBuilder.Activity(activityId6).FreeSlack.Should().Be(3);
            graphBuilder.Activity(activityId6).TotalSlack.Should().Be(22);
            graphBuilder.Activity(activityId6).LatestStartTime.Should().Be(30);
            graphBuilder.Activity(activityId6).LatestFinishTime.Should().Be(37);

            graphBuilder.Activity(activityId7).EarliestStartTime.Should().Be(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.Should().Be(22);
            graphBuilder.Activity(activityId7).FreeSlack.Should().Be(19);
            graphBuilder.Activity(activityId7).TotalSlack.Should().Be(19);
            graphBuilder.Activity(activityId7).LatestStartTime.Should().Be(37);
            graphBuilder.Activity(activityId7).LatestFinishTime.Should().Be(41);

            graphBuilder.Activity(activityId8).EarliestStartTime.Should().Be(18);
            graphBuilder.Activity(activityId8).EarliestFinishTime.Should().Be(22);
            graphBuilder.Activity(activityId8).FreeSlack.Should().Be(19);
            graphBuilder.Activity(activityId8).TotalSlack.Should().Be(19);
            graphBuilder.Activity(activityId8).LatestStartTime.Should().Be(37);
            graphBuilder.Activity(activityId8).LatestFinishTime.Should().Be(41);

            graphBuilder.Activity(activityId9).EarliestStartTime.Should().Be(31);
            graphBuilder.Activity(activityId9).EarliestFinishTime.Should().Be(41);
            graphBuilder.Activity(activityId9).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId9).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId9).LatestStartTime.Should().Be(31);
            graphBuilder.Activity(activityId9).LatestFinishTime.Should().Be(41);
        }

        [Fact]
        public void ArrowGraphBuilderExtensions_GivenCalculateCriticalPath_WhenMinimumFreeSlackInEndActivity_ThenAsExpected()
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
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int>(activityId1, 6));
            graphBuilder.AddActivity(new Activity<int, int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int, int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int, int>(activityId4, 11), new HashSet<int>(new[] { 2 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId5, 8), new HashSet<int>(new[] { 1, 2, 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId6, 7), new HashSet<int>(new[] { 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId7, 4), new HashSet<int>(new[] { 4 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId8, 4), new HashSet<int>(new[] { 4, 6 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId9, 10) { MinimumFreeSlack = 15 }, new HashSet<int>(new[] { 5 }));

            graphBuilder.CalculateCriticalPath();

            graphBuilder.Activity(activityId1).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.Should().Be(6);
            graphBuilder.Activity(activityId1).FreeSlack.Should().Be(2);
            graphBuilder.Activity(activityId1).TotalSlack.Should().Be(17);
            graphBuilder.Activity(activityId1).LatestStartTime.Should().Be(17);
            graphBuilder.Activity(activityId1).LatestFinishTime.Should().Be(23);

            graphBuilder.Activity(activityId2).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.Should().Be(7);
            graphBuilder.Activity(activityId2).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId2).TotalSlack.Should().Be(16);
            graphBuilder.Activity(activityId2).LatestStartTime.Should().Be(16);
            graphBuilder.Activity(activityId2).LatestFinishTime.Should().Be(23);

            graphBuilder.Activity(activityId3).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.Should().Be(8);
            graphBuilder.Activity(activityId3).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId3).TotalSlack.Should().Be(15);
            graphBuilder.Activity(activityId3).LatestStartTime.Should().Be(15);
            graphBuilder.Activity(activityId3).LatestFinishTime.Should().Be(23);

            graphBuilder.Activity(activityId4).EarliestStartTime.Should().Be(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.Should().Be(18);
            graphBuilder.Activity(activityId4).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId4).TotalSlack.Should().Be(19);
            graphBuilder.Activity(activityId4).LatestStartTime.Should().Be(26);
            graphBuilder.Activity(activityId4).LatestFinishTime.Should().Be(37);

            graphBuilder.Activity(activityId5).EarliestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId5).EarliestFinishTime.Should().Be(16);
            graphBuilder.Activity(activityId5).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId5).TotalSlack.Should().Be(15);
            graphBuilder.Activity(activityId5).LatestStartTime.Should().Be(23);
            graphBuilder.Activity(activityId5).LatestFinishTime.Should().Be(31);

            graphBuilder.Activity(activityId6).EarliestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.Should().Be(15);
            graphBuilder.Activity(activityId6).FreeSlack.Should().Be(3);
            graphBuilder.Activity(activityId6).TotalSlack.Should().Be(22);
            graphBuilder.Activity(activityId6).LatestStartTime.Should().Be(30);
            graphBuilder.Activity(activityId6).LatestFinishTime.Should().Be(37);

            graphBuilder.Activity(activityId7).EarliestStartTime.Should().Be(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.Should().Be(22);
            graphBuilder.Activity(activityId7).FreeSlack.Should().Be(19);
            graphBuilder.Activity(activityId7).TotalSlack.Should().Be(19);
            graphBuilder.Activity(activityId7).LatestStartTime.Should().Be(37);
            graphBuilder.Activity(activityId7).LatestFinishTime.Should().Be(41);

            graphBuilder.Activity(activityId8).EarliestStartTime.Should().Be(18);
            graphBuilder.Activity(activityId8).EarliestFinishTime.Should().Be(22);
            graphBuilder.Activity(activityId8).FreeSlack.Should().Be(19);
            graphBuilder.Activity(activityId8).TotalSlack.Should().Be(19);
            graphBuilder.Activity(activityId8).LatestStartTime.Should().Be(37);
            graphBuilder.Activity(activityId8).LatestFinishTime.Should().Be(41);

            graphBuilder.Activity(activityId9).EarliestStartTime.Should().Be(16);
            graphBuilder.Activity(activityId9).EarliestFinishTime.Should().Be(26);
            graphBuilder.Activity(activityId9).FreeSlack.Should().Be(15);
            graphBuilder.Activity(activityId9).TotalSlack.Should().Be(15);
            graphBuilder.Activity(activityId9).LatestStartTime.Should().Be(31);
            graphBuilder.Activity(activityId9).LatestFinishTime.Should().Be(41);
        }

        [Fact]
        public void ArrowGraphBuilderExtensions_GivenCalculateCriticalPath_WhenMinimumEarliestStartTimeInStartActivity_ThenAsExpected()
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
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int>(activityId1, 6) { MinimumEarliestStartTime = 10 });
            graphBuilder.AddActivity(new Activity<int, int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int, int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int, int>(activityId4, 11), new HashSet<int>(new[] { 2 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId5, 8), new HashSet<int>(new[] { 1, 2, 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId6, 7), new HashSet<int>(new[] { 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId7, 4), new HashSet<int>(new[] { 4 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId8, 4), new HashSet<int>(new[] { 4, 6 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId9, 10), new HashSet<int>(new[] { 5 }));

            graphBuilder.CalculateCriticalPath();

            graphBuilder.Activity(activityId1).EarliestStartTime.Should().Be(10);
            graphBuilder.Activity(activityId1).EarliestFinishTime.Should().Be(16);
            graphBuilder.Activity(activityId1).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId1).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId1).LatestStartTime.Should().Be(10);
            graphBuilder.Activity(activityId1).LatestFinishTime.Should().Be(16);

            graphBuilder.Activity(activityId2).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.Should().Be(7);
            graphBuilder.Activity(activityId2).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId2).TotalSlack.Should().Be(9);
            graphBuilder.Activity(activityId2).LatestStartTime.Should().Be(9);
            graphBuilder.Activity(activityId2).LatestFinishTime.Should().Be(16);

            graphBuilder.Activity(activityId3).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.Should().Be(8);
            graphBuilder.Activity(activityId3).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId3).TotalSlack.Should().Be(8);
            graphBuilder.Activity(activityId3).LatestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId3).LatestFinishTime.Should().Be(16);

            graphBuilder.Activity(activityId4).EarliestStartTime.Should().Be(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.Should().Be(18);
            graphBuilder.Activity(activityId4).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId4).TotalSlack.Should().Be(12);
            graphBuilder.Activity(activityId4).LatestStartTime.Should().Be(19);
            graphBuilder.Activity(activityId4).LatestFinishTime.Should().Be(30);

            graphBuilder.Activity(activityId5).EarliestStartTime.Should().Be(16);
            graphBuilder.Activity(activityId5).EarliestFinishTime.Should().Be(24);
            graphBuilder.Activity(activityId5).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId5).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId5).LatestStartTime.Should().Be(16);
            graphBuilder.Activity(activityId5).LatestFinishTime.Should().Be(24);

            graphBuilder.Activity(activityId6).EarliestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.Should().Be(15);
            graphBuilder.Activity(activityId6).FreeSlack.Should().Be(3);
            graphBuilder.Activity(activityId6).TotalSlack.Should().Be(15);
            graphBuilder.Activity(activityId6).LatestStartTime.Should().Be(23);
            graphBuilder.Activity(activityId6).LatestFinishTime.Should().Be(30);

            graphBuilder.Activity(activityId7).EarliestStartTime.Should().Be(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.Should().Be(22);
            graphBuilder.Activity(activityId7).FreeSlack.Should().Be(12);
            graphBuilder.Activity(activityId7).TotalSlack.Should().Be(12);
            graphBuilder.Activity(activityId7).LatestStartTime.Should().Be(30);
            graphBuilder.Activity(activityId7).LatestFinishTime.Should().Be(34);

            graphBuilder.Activity(activityId8).EarliestStartTime.Should().Be(18);
            graphBuilder.Activity(activityId8).EarliestFinishTime.Should().Be(22);
            graphBuilder.Activity(activityId8).FreeSlack.Should().Be(12);
            graphBuilder.Activity(activityId8).TotalSlack.Should().Be(12);
            graphBuilder.Activity(activityId8).LatestStartTime.Should().Be(30);
            graphBuilder.Activity(activityId8).LatestFinishTime.Should().Be(34);

            graphBuilder.Activity(activityId9).EarliestStartTime.Should().Be(24);
            graphBuilder.Activity(activityId9).EarliestFinishTime.Should().Be(34);
            graphBuilder.Activity(activityId9).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId9).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId9).LatestStartTime.Should().Be(24);
            graphBuilder.Activity(activityId9).LatestFinishTime.Should().Be(34);
        }

        [Fact]
        public void ArrowGraphBuilderExtensions_GivenCalculateCriticalPath_WhenMinimumEarliestStartTimeInNormalActivity_ThenAsExpected()
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
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int>(activityId1, 6));
            graphBuilder.AddActivity(new Activity<int, int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int, int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int, int>(activityId4, 11), new HashSet<int>(new[] { 2 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId5, 8) { MinimumEarliestStartTime = 10 }, new HashSet<int>(new[] { 1, 2, 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId6, 7), new HashSet<int>(new[] { 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId7, 4), new HashSet<int>(new[] { 4 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId8, 4), new HashSet<int>(new[] { 4, 6 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId9, 10), new HashSet<int>(new[] { 5 }));

            graphBuilder.TransitiveReduction();
            graphBuilder.CalculateCriticalPath();

            graphBuilder.Activity(activityId1).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.Should().Be(6);
            graphBuilder.Activity(activityId1).FreeSlack.Should().Be(4);
            graphBuilder.Activity(activityId1).TotalSlack.Should().Be(4);
            graphBuilder.Activity(activityId1).LatestStartTime.Should().Be(4);
            graphBuilder.Activity(activityId1).LatestFinishTime.Should().Be(10);

            graphBuilder.Activity(activityId2).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.Should().Be(7);
            graphBuilder.Activity(activityId2).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId2).TotalSlack.Should().Be(3);
            graphBuilder.Activity(activityId2).LatestStartTime.Should().Be(3);
            graphBuilder.Activity(activityId2).LatestFinishTime.Should().Be(10);

            graphBuilder.Activity(activityId3).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.Should().Be(8);
            graphBuilder.Activity(activityId3).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId3).TotalSlack.Should().Be(2);
            graphBuilder.Activity(activityId3).LatestStartTime.Should().Be(2);
            graphBuilder.Activity(activityId3).LatestFinishTime.Should().Be(10);

            graphBuilder.Activity(activityId4).EarliestStartTime.Should().Be(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.Should().Be(18);
            graphBuilder.Activity(activityId4).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId4).TotalSlack.Should().Be(6);
            graphBuilder.Activity(activityId4).LatestStartTime.Should().Be(13);
            graphBuilder.Activity(activityId4).LatestFinishTime.Should().Be(24);

            graphBuilder.Activity(activityId5).EarliestStartTime.Should().Be(10);
            graphBuilder.Activity(activityId5).EarliestFinishTime.Should().Be(18);
            graphBuilder.Activity(activityId5).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId5).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId5).LatestStartTime.Should().Be(10);
            graphBuilder.Activity(activityId5).LatestFinishTime.Should().Be(18);

            graphBuilder.Activity(activityId6).EarliestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.Should().Be(15);
            graphBuilder.Activity(activityId6).FreeSlack.Should().Be(3);
            graphBuilder.Activity(activityId6).TotalSlack.Should().Be(9);
            graphBuilder.Activity(activityId6).LatestStartTime.Should().Be(17);
            graphBuilder.Activity(activityId6).LatestFinishTime.Should().Be(24);

            graphBuilder.Activity(activityId7).EarliestStartTime.Should().Be(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.Should().Be(22);
            graphBuilder.Activity(activityId7).FreeSlack.Should().Be(6);
            graphBuilder.Activity(activityId7).TotalSlack.Should().Be(6);
            graphBuilder.Activity(activityId7).LatestStartTime.Should().Be(24);
            graphBuilder.Activity(activityId7).LatestFinishTime.Should().Be(28);

            graphBuilder.Activity(activityId8).EarliestStartTime.Should().Be(18);
            graphBuilder.Activity(activityId8).EarliestFinishTime.Should().Be(22);
            graphBuilder.Activity(activityId8).FreeSlack.Should().Be(6);
            graphBuilder.Activity(activityId8).TotalSlack.Should().Be(6);
            graphBuilder.Activity(activityId8).LatestStartTime.Should().Be(24);
            graphBuilder.Activity(activityId8).LatestFinishTime.Should().Be(28);

            graphBuilder.Activity(activityId9).EarliestStartTime.Should().Be(18);
            graphBuilder.Activity(activityId9).EarliestFinishTime.Should().Be(28);
            graphBuilder.Activity(activityId9).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId9).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId9).LatestStartTime.Should().Be(18);
            graphBuilder.Activity(activityId9).LatestFinishTime.Should().Be(28);
        }

        [Fact]
        public void ArrowGraphBuilderExtensions_GivenCalculateCriticalPath_WhenMinimumEarliestStartTimeInEndActivity_ThenAsExpected()
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
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int>(activityId1, 6));
            graphBuilder.AddActivity(new Activity<int, int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int, int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int, int>(activityId4, 11), new HashSet<int>(new[] { 2 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId5, 8), new HashSet<int>(new[] { 1, 2, 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId6, 7), new HashSet<int>(new[] { 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId7, 4), new HashSet<int>(new[] { 4 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId8, 4), new HashSet<int>(new[] { 4, 6 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId9, 10) { MinimumEarliestStartTime = 20 }, new HashSet<int>(new[] { 5 }));

            graphBuilder.TransitiveReduction();
            graphBuilder.CalculateCriticalPath();

            graphBuilder.Activity(activityId1).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.Should().Be(6);
            graphBuilder.Activity(activityId1).FreeSlack.Should().Be(2);
            graphBuilder.Activity(activityId1).TotalSlack.Should().Be(6);
            graphBuilder.Activity(activityId1).LatestStartTime.Should().Be(6);
            graphBuilder.Activity(activityId1).LatestFinishTime.Should().Be(12);

            graphBuilder.Activity(activityId2).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.Should().Be(7);
            graphBuilder.Activity(activityId2).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId2).TotalSlack.Should().Be(5);
            graphBuilder.Activity(activityId2).LatestStartTime.Should().Be(5);
            graphBuilder.Activity(activityId2).LatestFinishTime.Should().Be(12);

            graphBuilder.Activity(activityId3).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.Should().Be(8);
            graphBuilder.Activity(activityId3).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId3).TotalSlack.Should().Be(4);
            graphBuilder.Activity(activityId3).LatestStartTime.Should().Be(4);
            graphBuilder.Activity(activityId3).LatestFinishTime.Should().Be(12);

            graphBuilder.Activity(activityId4).EarliestStartTime.Should().Be(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.Should().Be(18);
            graphBuilder.Activity(activityId4).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId4).TotalSlack.Should().Be(8);
            graphBuilder.Activity(activityId4).LatestStartTime.Should().Be(15);
            graphBuilder.Activity(activityId4).LatestFinishTime.Should().Be(26);

            graphBuilder.Activity(activityId5).EarliestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId5).EarliestFinishTime.Should().Be(16);
            graphBuilder.Activity(activityId5).FreeSlack.Should().Be(4);
            graphBuilder.Activity(activityId5).TotalSlack.Should().Be(4);
            graphBuilder.Activity(activityId5).LatestStartTime.Should().Be(12);
            graphBuilder.Activity(activityId5).LatestFinishTime.Should().Be(20);

            graphBuilder.Activity(activityId6).EarliestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.Should().Be(15);
            graphBuilder.Activity(activityId6).FreeSlack.Should().Be(3);
            graphBuilder.Activity(activityId6).TotalSlack.Should().Be(11);
            graphBuilder.Activity(activityId6).LatestStartTime.Should().Be(19);
            graphBuilder.Activity(activityId6).LatestFinishTime.Should().Be(26);

            graphBuilder.Activity(activityId7).EarliestStartTime.Should().Be(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.Should().Be(22);
            graphBuilder.Activity(activityId7).FreeSlack.Should().Be(8);
            graphBuilder.Activity(activityId7).TotalSlack.Should().Be(8);
            graphBuilder.Activity(activityId7).LatestStartTime.Should().Be(26);
            graphBuilder.Activity(activityId7).LatestFinishTime.Should().Be(30);

            graphBuilder.Activity(activityId8).EarliestStartTime.Should().Be(18);
            graphBuilder.Activity(activityId8).EarliestFinishTime.Should().Be(22);
            graphBuilder.Activity(activityId8).FreeSlack.Should().Be(8);
            graphBuilder.Activity(activityId8).TotalSlack.Should().Be(8);
            graphBuilder.Activity(activityId8).LatestStartTime.Should().Be(26);
            graphBuilder.Activity(activityId8).LatestFinishTime.Should().Be(30);

            graphBuilder.Activity(activityId9).EarliestStartTime.Should().Be(20);
            graphBuilder.Activity(activityId9).EarliestFinishTime.Should().Be(30);
            graphBuilder.Activity(activityId9).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId9).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId9).LatestStartTime.Should().Be(20);
            graphBuilder.Activity(activityId9).LatestFinishTime.Should().Be(30);
        }

        [Fact]
        public void ArrowGraphBuilderExtensions_GivenCalculateCriticalPath_WhenMaximumLatestFinishTimeInStartActivity_ThenAsExpected()
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
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.WhenTesting = true;
            graphBuilder.AddActivity(new Activity<int, int>(activityId1, 6) { MaximumLatestFinishTime = 7 });
            graphBuilder.AddActivity(new Activity<int, int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int, int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int, int>(activityId4, 11), new HashSet<int>(new[] { 2 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId5, 8), new HashSet<int>(new[] { 1, 2, 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId6, 7), new HashSet<int>(new[] { 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId7, 4), new HashSet<int>(new[] { 4 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId8, 4), new HashSet<int>(new[] { 4, 6 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId9, 10), new HashSet<int>(new[] { 5 }));

            graphBuilder.TransitiveReduction();
            graphBuilder.CalculateCriticalPath();

            graphBuilder.Activity(activityId1).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.Should().Be(6);
            graphBuilder.Activity(activityId1).FreeSlack.Should().Be(1);
            graphBuilder.Activity(activityId1).TotalSlack.Should().Be(1);
            graphBuilder.Activity(activityId1).LatestStartTime.Should().Be(1);
            graphBuilder.Activity(activityId1).LatestFinishTime.Should().Be(7);

            graphBuilder.Activity(activityId2).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.Should().Be(7);
            graphBuilder.Activity(activityId2).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId2).TotalSlack.Should().Be(1);
            graphBuilder.Activity(activityId2).LatestStartTime.Should().Be(1);
            graphBuilder.Activity(activityId2).LatestFinishTime.Should().Be(8);

            graphBuilder.Activity(activityId3).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.Should().Be(8);
            graphBuilder.Activity(activityId3).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId3).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId3).LatestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId3).LatestFinishTime.Should().Be(8);

            graphBuilder.Activity(activityId4).EarliestStartTime.Should().Be(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.Should().Be(18);
            graphBuilder.Activity(activityId4).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId4).TotalSlack.Should().Be(4);
            graphBuilder.Activity(activityId4).LatestStartTime.Should().Be(11);
            graphBuilder.Activity(activityId4).LatestFinishTime.Should().Be(22);

            graphBuilder.Activity(activityId5).EarliestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId5).EarliestFinishTime.Should().Be(16);
            graphBuilder.Activity(activityId5).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId5).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId5).LatestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId5).LatestFinishTime.Should().Be(16);

            graphBuilder.Activity(activityId6).EarliestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.Should().Be(15);
            graphBuilder.Activity(activityId6).FreeSlack.Should().Be(3);
            graphBuilder.Activity(activityId6).TotalSlack.Should().Be(7);
            graphBuilder.Activity(activityId6).LatestStartTime.Should().Be(15);
            graphBuilder.Activity(activityId6).LatestFinishTime.Should().Be(22);

            graphBuilder.Activity(activityId7).EarliestStartTime.Should().Be(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.Should().Be(22);
            graphBuilder.Activity(activityId7).FreeSlack.Should().Be(4);
            graphBuilder.Activity(activityId7).TotalSlack.Should().Be(4);
            graphBuilder.Activity(activityId7).LatestStartTime.Should().Be(22);
            graphBuilder.Activity(activityId7).LatestFinishTime.Should().Be(26);

            graphBuilder.Activity(activityId8).EarliestStartTime.Should().Be(18);
            graphBuilder.Activity(activityId8).EarliestFinishTime.Should().Be(22);
            graphBuilder.Activity(activityId8).FreeSlack.Should().Be(4);
            graphBuilder.Activity(activityId8).TotalSlack.Should().Be(4);
            graphBuilder.Activity(activityId8).LatestStartTime.Should().Be(22);
            graphBuilder.Activity(activityId8).LatestFinishTime.Should().Be(26);

            graphBuilder.Activity(activityId9).EarliestStartTime.Should().Be(16);
            graphBuilder.Activity(activityId9).EarliestFinishTime.Should().Be(26);
            graphBuilder.Activity(activityId9).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId9).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId9).LatestStartTime.Should().Be(16);
            graphBuilder.Activity(activityId9).LatestFinishTime.Should().Be(26);
        }

        [Fact]
        public void ArrowGraphBuilderExtensions_GivenCalculateCriticalPath_WhenExtremeMaximumLatestFinishTimeInStartActivity_ThenAsExpected()
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
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int>(activityId1, 6) { MaximumLatestFinishTime = 5 });
            graphBuilder.AddActivity(new Activity<int, int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int, int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int, int>(activityId4, 11), new HashSet<int>(new[] { 2 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId5, 8), new HashSet<int>(new[] { 1, 2, 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId6, 7), new HashSet<int>(new[] { 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId7, 4), new HashSet<int>(new[] { 4 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId8, 4), new HashSet<int>(new[] { 4, 6 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId9, 10), new HashSet<int>(new[] { 5 }));

            graphBuilder.TransitiveReduction();
            graphBuilder.CalculateCriticalPath();

            graphBuilder.Activity(activityId1).EarliestStartTime.Should().Be(-1);
            graphBuilder.Activity(activityId1).EarliestFinishTime.Should().Be(5);
            graphBuilder.Activity(activityId1).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId1).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId1).LatestStartTime.Should().Be(-1);
            graphBuilder.Activity(activityId1).LatestFinishTime.Should().Be(5);

            graphBuilder.Activity(activityId2).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.Should().Be(7);
            graphBuilder.Activity(activityId2).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId2).TotalSlack.Should().Be(1);
            graphBuilder.Activity(activityId2).LatestStartTime.Should().Be(1);
            graphBuilder.Activity(activityId2).LatestFinishTime.Should().Be(8);

            graphBuilder.Activity(activityId3).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.Should().Be(8);
            graphBuilder.Activity(activityId3).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId3).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId3).LatestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId3).LatestFinishTime.Should().Be(8);

            graphBuilder.Activity(activityId4).EarliestStartTime.Should().Be(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.Should().Be(18);
            graphBuilder.Activity(activityId4).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId4).TotalSlack.Should().Be(4);
            graphBuilder.Activity(activityId4).LatestStartTime.Should().Be(11);
            graphBuilder.Activity(activityId4).LatestFinishTime.Should().Be(22);

            graphBuilder.Activity(activityId5).EarliestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId5).EarliestFinishTime.Should().Be(16);
            graphBuilder.Activity(activityId5).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId5).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId5).LatestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId5).LatestFinishTime.Should().Be(16);

            graphBuilder.Activity(activityId6).EarliestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.Should().Be(15);
            graphBuilder.Activity(activityId6).FreeSlack.Should().Be(3);
            graphBuilder.Activity(activityId6).TotalSlack.Should().Be(7);
            graphBuilder.Activity(activityId6).LatestStartTime.Should().Be(15);
            graphBuilder.Activity(activityId6).LatestFinishTime.Should().Be(22);

            graphBuilder.Activity(activityId7).EarliestStartTime.Should().Be(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.Should().Be(22);
            graphBuilder.Activity(activityId7).FreeSlack.Should().Be(4);
            graphBuilder.Activity(activityId7).TotalSlack.Should().Be(4);
            graphBuilder.Activity(activityId7).LatestStartTime.Should().Be(22);
            graphBuilder.Activity(activityId7).LatestFinishTime.Should().Be(26);

            graphBuilder.Activity(activityId8).EarliestStartTime.Should().Be(18);
            graphBuilder.Activity(activityId8).EarliestFinishTime.Should().Be(22);
            graphBuilder.Activity(activityId8).FreeSlack.Should().Be(4);
            graphBuilder.Activity(activityId8).TotalSlack.Should().Be(4);
            graphBuilder.Activity(activityId8).LatestStartTime.Should().Be(22);
            graphBuilder.Activity(activityId8).LatestFinishTime.Should().Be(26);

            graphBuilder.Activity(activityId9).EarliestStartTime.Should().Be(16);
            graphBuilder.Activity(activityId9).EarliestFinishTime.Should().Be(26);
            graphBuilder.Activity(activityId9).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId9).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId9).LatestStartTime.Should().Be(16);
            graphBuilder.Activity(activityId9).LatestFinishTime.Should().Be(26);
        }

        [Fact]
        public void ArrowGraphBuilderExtensions_GivenCalculateCriticalPath_WhenMaximumLatestFinishTimeInNormalActivity_ThenAsExpected()
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
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int>(activityId1, 6));
            graphBuilder.AddActivity(new Activity<int, int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int, int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int, int>(activityId4, 11) { MaximumLatestFinishTime = 18 }, new HashSet<int>(new[] { 2 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId5, 8), new HashSet<int>(new[] { 1, 2, 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId6, 7), new HashSet<int>(new[] { 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId7, 4), new HashSet<int>(new[] { 4 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId8, 4), new HashSet<int>(new[] { 4, 6 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId9, 10), new HashSet<int>(new[] { 5 }));

            graphBuilder.TransitiveReduction();
            graphBuilder.CalculateCriticalPath();

            graphBuilder.Activity(activityId1).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.Should().Be(6);
            graphBuilder.Activity(activityId1).FreeSlack.Should().Be(2);
            graphBuilder.Activity(activityId1).TotalSlack.Should().Be(2);
            graphBuilder.Activity(activityId1).LatestStartTime.Should().Be(2);
            graphBuilder.Activity(activityId1).LatestFinishTime.Should().Be(8);

            graphBuilder.Activity(activityId2).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.Should().Be(7);
            graphBuilder.Activity(activityId2).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId2).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId2).LatestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId2).LatestFinishTime.Should().Be(7);

            graphBuilder.Activity(activityId3).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.Should().Be(8);
            graphBuilder.Activity(activityId3).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId3).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId3).LatestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId3).LatestFinishTime.Should().Be(8);

            graphBuilder.Activity(activityId4).EarliestStartTime.Should().Be(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.Should().Be(18);
            graphBuilder.Activity(activityId4).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId4).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId4).LatestStartTime.Should().Be(7);
            graphBuilder.Activity(activityId4).LatestFinishTime.Should().Be(18);

            graphBuilder.Activity(activityId5).EarliestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId5).EarliestFinishTime.Should().Be(16);
            graphBuilder.Activity(activityId5).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId5).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId5).LatestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId5).LatestFinishTime.Should().Be(16);

            graphBuilder.Activity(activityId6).EarliestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.Should().Be(15);
            graphBuilder.Activity(activityId6).FreeSlack.Should().Be(3);
            graphBuilder.Activity(activityId6).TotalSlack.Should().Be(7);
            graphBuilder.Activity(activityId6).LatestStartTime.Should().Be(15);
            graphBuilder.Activity(activityId6).LatestFinishTime.Should().Be(22);

            graphBuilder.Activity(activityId7).EarliestStartTime.Should().Be(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.Should().Be(22);
            graphBuilder.Activity(activityId7).FreeSlack.Should().Be(4);
            graphBuilder.Activity(activityId7).TotalSlack.Should().Be(4);
            graphBuilder.Activity(activityId7).LatestStartTime.Should().Be(22);
            graphBuilder.Activity(activityId7).LatestFinishTime.Should().Be(26);

            graphBuilder.Activity(activityId8).EarliestStartTime.Should().Be(18);
            graphBuilder.Activity(activityId8).EarliestFinishTime.Should().Be(22);
            graphBuilder.Activity(activityId8).FreeSlack.Should().Be(4);
            graphBuilder.Activity(activityId8).TotalSlack.Should().Be(4);
            graphBuilder.Activity(activityId8).LatestStartTime.Should().Be(22);
            graphBuilder.Activity(activityId8).LatestFinishTime.Should().Be(26);

            graphBuilder.Activity(activityId9).EarliestStartTime.Should().Be(16);
            graphBuilder.Activity(activityId9).EarliestFinishTime.Should().Be(26);
            graphBuilder.Activity(activityId9).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId9).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId9).LatestStartTime.Should().Be(16);
            graphBuilder.Activity(activityId9).LatestFinishTime.Should().Be(26);
        }

        [Fact]
        public void ArrowGraphBuilderExtensions_GivenCalculateCriticalPath_WhenExtremeMaximumLatestFinishTimeInNormalActivity_ThenAsExpected()
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
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int>(activityId1, 6));
            graphBuilder.AddActivity(new Activity<int, int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int, int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int, int>(activityId4, 11) { MaximumLatestFinishTime = 16 }, new HashSet<int>(new[] { 2 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId5, 8), new HashSet<int>(new[] { 1, 2, 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId6, 7), new HashSet<int>(new[] { 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId7, 4), new HashSet<int>(new[] { 4 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId8, 4), new HashSet<int>(new[] { 4, 6 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId9, 10), new HashSet<int>(new[] { 5 }));

            graphBuilder.TransitiveReduction();
            graphBuilder.CalculateCriticalPath();

            graphBuilder.Activity(activityId1).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.Should().Be(6);
            graphBuilder.Activity(activityId1).FreeSlack.Should().Be(2);
            graphBuilder.Activity(activityId1).TotalSlack.Should().Be(2);
            graphBuilder.Activity(activityId1).LatestStartTime.Should().Be(2);
            graphBuilder.Activity(activityId1).LatestFinishTime.Should().Be(8);

            graphBuilder.Activity(activityId2).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.Should().Be(7);

            // MS Project would list this as 0, but ProjectPlan calculates slack based on its
            // downstream effect. So, in this case, the -2 total slack of activity 4 is transfered
            // to the the free slack of activity 2 instead.
            graphBuilder.Activity(activityId2).FreeSlack.Should().Be(-2);

            graphBuilder.Activity(activityId2).TotalSlack.Should().Be(-2);
            graphBuilder.Activity(activityId2).LatestStartTime.Should().Be(-2);
            graphBuilder.Activity(activityId2).LatestFinishTime.Should().Be(5);

            graphBuilder.Activity(activityId3).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.Should().Be(8);
            graphBuilder.Activity(activityId3).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId3).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId3).LatestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId3).LatestFinishTime.Should().Be(8);

            graphBuilder.Activity(activityId4).EarliestStartTime.Should().Be(5);
            graphBuilder.Activity(activityId4).EarliestFinishTime.Should().Be(16);
            graphBuilder.Activity(activityId4).FreeSlack.Should().Be(0);

            // MS Project would list this as -2, but ProjectPlan calculates slack based on its
            // downstream effect. So, in this case, the -2 total slack of activity 4 is transfered
            // to the the free slack of activity 2 instead.
            graphBuilder.Activity(activityId4).TotalSlack.Should().Be(0);

            graphBuilder.Activity(activityId4).LatestStartTime.Should().Be(5);
            graphBuilder.Activity(activityId4).LatestFinishTime.Should().Be(16);

            graphBuilder.Activity(activityId5).EarliestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId5).EarliestFinishTime.Should().Be(16);
            graphBuilder.Activity(activityId5).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId5).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId5).LatestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId5).LatestFinishTime.Should().Be(16);

            graphBuilder.Activity(activityId6).EarliestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.Should().Be(15);
            graphBuilder.Activity(activityId6).FreeSlack.Should().Be(1);
            graphBuilder.Activity(activityId6).TotalSlack.Should().Be(7);
            graphBuilder.Activity(activityId6).LatestStartTime.Should().Be(15);
            graphBuilder.Activity(activityId6).LatestFinishTime.Should().Be(22);

            graphBuilder.Activity(activityId7).EarliestStartTime.Should().Be(16);
            graphBuilder.Activity(activityId7).EarliestFinishTime.Should().Be(20);
            graphBuilder.Activity(activityId7).FreeSlack.Should().Be(6);
            graphBuilder.Activity(activityId7).TotalSlack.Should().Be(6);
            graphBuilder.Activity(activityId7).LatestStartTime.Should().Be(22);
            graphBuilder.Activity(activityId7).LatestFinishTime.Should().Be(26);

            graphBuilder.Activity(activityId8).EarliestStartTime.Should().Be(16);
            graphBuilder.Activity(activityId8).EarliestFinishTime.Should().Be(20);
            graphBuilder.Activity(activityId8).FreeSlack.Should().Be(6);
            graphBuilder.Activity(activityId8).TotalSlack.Should().Be(6);
            graphBuilder.Activity(activityId8).LatestStartTime.Should().Be(22);
            graphBuilder.Activity(activityId8).LatestFinishTime.Should().Be(26);

            graphBuilder.Activity(activityId9).EarliestStartTime.Should().Be(16);
            graphBuilder.Activity(activityId9).EarliestFinishTime.Should().Be(26);
            graphBuilder.Activity(activityId9).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId9).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId9).LatestStartTime.Should().Be(16);
            graphBuilder.Activity(activityId9).LatestFinishTime.Should().Be(26);
        }

        [Fact]
        public void ArrowGraphBuilderExtensions_GivenCalculateCriticalPath_WhenMaximumLatestFinishTimeInEndActivity_ThenAsExpected()
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
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int>(activityId1, 6));
            graphBuilder.AddActivity(new Activity<int, int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int, int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int, int>(activityId4, 11), new HashSet<int>(new[] { 2 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId5, 8), new HashSet<int>(new[] { 1, 2, 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId6, 7), new HashSet<int>(new[] { 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId7, 4), new HashSet<int>(new[] { 4 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId8, 4) { MaximumLatestFinishTime = 22 }, new HashSet<int>(new[] { 4, 6 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId9, 10), new HashSet<int>(new[] { 5 }));

            graphBuilder.TransitiveReduction();
            graphBuilder.CalculateCriticalPath();

            graphBuilder.Activity(activityId1).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.Should().Be(6);
            graphBuilder.Activity(activityId1).FreeSlack.Should().Be(2);
            graphBuilder.Activity(activityId1).TotalSlack.Should().Be(2);
            graphBuilder.Activity(activityId1).LatestStartTime.Should().Be(2);
            graphBuilder.Activity(activityId1).LatestFinishTime.Should().Be(8);

            graphBuilder.Activity(activityId2).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.Should().Be(7);
            graphBuilder.Activity(activityId2).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId2).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId2).LatestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId2).LatestFinishTime.Should().Be(7);

            graphBuilder.Activity(activityId3).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.Should().Be(8);
            graphBuilder.Activity(activityId3).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId3).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId3).LatestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId3).LatestFinishTime.Should().Be(8);

            graphBuilder.Activity(activityId4).EarliestStartTime.Should().Be(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.Should().Be(18);
            graphBuilder.Activity(activityId4).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId4).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId4).LatestStartTime.Should().Be(7);
            graphBuilder.Activity(activityId4).LatestFinishTime.Should().Be(18);

            graphBuilder.Activity(activityId5).EarliestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId5).EarliestFinishTime.Should().Be(16);
            graphBuilder.Activity(activityId5).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId5).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId5).LatestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId5).LatestFinishTime.Should().Be(16);

            graphBuilder.Activity(activityId6).EarliestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.Should().Be(15);
            graphBuilder.Activity(activityId6).FreeSlack.Should().Be(3);
            graphBuilder.Activity(activityId6).TotalSlack.Should().Be(3);
            graphBuilder.Activity(activityId6).LatestStartTime.Should().Be(11);
            graphBuilder.Activity(activityId6).LatestFinishTime.Should().Be(18);

            graphBuilder.Activity(activityId7).EarliestStartTime.Should().Be(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.Should().Be(22);
            graphBuilder.Activity(activityId7).FreeSlack.Should().Be(4);
            graphBuilder.Activity(activityId7).TotalSlack.Should().Be(4);
            graphBuilder.Activity(activityId7).LatestStartTime.Should().Be(22);
            graphBuilder.Activity(activityId7).LatestFinishTime.Should().Be(26);

            graphBuilder.Activity(activityId8).EarliestStartTime.Should().Be(18);
            graphBuilder.Activity(activityId8).EarliestFinishTime.Should().Be(22);
            graphBuilder.Activity(activityId8).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId8).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId8).LatestStartTime.Should().Be(18);
            graphBuilder.Activity(activityId8).LatestFinishTime.Should().Be(22);

            graphBuilder.Activity(activityId9).EarliestStartTime.Should().Be(16);
            graphBuilder.Activity(activityId9).EarliestFinishTime.Should().Be(26);
            graphBuilder.Activity(activityId9).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId9).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId9).LatestStartTime.Should().Be(16);
            graphBuilder.Activity(activityId9).LatestFinishTime.Should().Be(26);
        }

        [Fact]
        public void ArrowGraphBuilderExtensions_GivenCalculateCriticalPath_WhenExtremeMaximumLatestFinishTimeInEndActivity_ThenAsExpected()
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
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int>(activityId1, 6));
            graphBuilder.AddActivity(new Activity<int, int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int, int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int, int>(activityId4, 11), new HashSet<int>(new[] { 2 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId5, 8), new HashSet<int>(new[] { 1, 2, 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId6, 7), new HashSet<int>(new[] { 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId7, 4), new HashSet<int>(new[] { 4 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId8, 4) { MaximumLatestFinishTime = 21 }, new HashSet<int>(new[] { 4, 6 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId9, 10), new HashSet<int>(new[] { 5 }));

            graphBuilder.TransitiveReduction();
            graphBuilder.CalculateCriticalPath();

            graphBuilder.Activity(activityId1).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId1).EarliestFinishTime.Should().Be(6);
            graphBuilder.Activity(activityId1).FreeSlack.Should().Be(2);
            graphBuilder.Activity(activityId1).TotalSlack.Should().Be(2);
            graphBuilder.Activity(activityId1).LatestStartTime.Should().Be(2);
            graphBuilder.Activity(activityId1).LatestFinishTime.Should().Be(8);

            graphBuilder.Activity(activityId2).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId2).EarliestFinishTime.Should().Be(7);

            // MS Project would list this as 0, but ProjectPlan calculates slack based on its
            // downstream effect. So, in this case, the -1 total slack of activity 8 is transfered
            // to the the free slack of activities 2 and 4 instead.
            graphBuilder.Activity(activityId2).FreeSlack.Should().Be(-1);

            graphBuilder.Activity(activityId2).TotalSlack.Should().Be(-1);
            graphBuilder.Activity(activityId2).LatestStartTime.Should().Be(-1);
            graphBuilder.Activity(activityId2).LatestFinishTime.Should().Be(6);

            graphBuilder.Activity(activityId3).EarliestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId3).EarliestFinishTime.Should().Be(8);
            graphBuilder.Activity(activityId3).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId3).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId3).LatestStartTime.Should().Be(0);
            graphBuilder.Activity(activityId3).LatestFinishTime.Should().Be(8);

            graphBuilder.Activity(activityId4).EarliestStartTime.Should().Be(7);
            graphBuilder.Activity(activityId4).EarliestFinishTime.Should().Be(18);

            // MS Project would list this as 0, but ProjectPlan calculates slack based on its
            // downstream effect. So, in this case, the -1 total slack of activity 8 is transfered
            // to the the free slack of activities 2 and 4 instead.
            graphBuilder.Activity(activityId4).FreeSlack.Should().Be(-1);

            graphBuilder.Activity(activityId4).TotalSlack.Should().Be(-1);
            graphBuilder.Activity(activityId4).LatestStartTime.Should().Be(6);
            graphBuilder.Activity(activityId4).LatestFinishTime.Should().Be(17);

            graphBuilder.Activity(activityId5).EarliestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId5).EarliestFinishTime.Should().Be(16);
            graphBuilder.Activity(activityId5).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId5).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId5).LatestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId5).LatestFinishTime.Should().Be(16);

            graphBuilder.Activity(activityId6).EarliestStartTime.Should().Be(8);
            graphBuilder.Activity(activityId6).EarliestFinishTime.Should().Be(15);
            graphBuilder.Activity(activityId6).FreeSlack.Should().Be(2);
            graphBuilder.Activity(activityId6).TotalSlack.Should().Be(2);
            graphBuilder.Activity(activityId6).LatestStartTime.Should().Be(10);
            graphBuilder.Activity(activityId6).LatestFinishTime.Should().Be(17);

            graphBuilder.Activity(activityId7).EarliestStartTime.Should().Be(18);
            graphBuilder.Activity(activityId7).EarliestFinishTime.Should().Be(22);
            graphBuilder.Activity(activityId7).FreeSlack.Should().Be(4);
            graphBuilder.Activity(activityId7).TotalSlack.Should().Be(4);
            graphBuilder.Activity(activityId7).LatestStartTime.Should().Be(22);
            graphBuilder.Activity(activityId7).LatestFinishTime.Should().Be(26);

            graphBuilder.Activity(activityId8).EarliestStartTime.Should().Be(17);
            graphBuilder.Activity(activityId8).EarliestFinishTime.Should().Be(21);
            graphBuilder.Activity(activityId8).FreeSlack.Should().Be(0);

            // MS Project would list this as -1, but ProjectPlan calculates slack based on its
            // downstream effect. So, in this case, the -1 total slack of activity 8 is transfered
            // to the the free slack of activities 2 and 4 instead.
            graphBuilder.Activity(activityId8).TotalSlack.Should().Be(0);

            graphBuilder.Activity(activityId8).LatestStartTime.Should().Be(17);
            graphBuilder.Activity(activityId8).LatestFinishTime.Should().Be(21);

            graphBuilder.Activity(activityId9).EarliestStartTime.Should().Be(16);
            graphBuilder.Activity(activityId9).EarliestFinishTime.Should().Be(26);
            graphBuilder.Activity(activityId9).FreeSlack.Should().Be(0);
            graphBuilder.Activity(activityId9).TotalSlack.Should().Be(0);
            graphBuilder.Activity(activityId9).LatestStartTime.Should().Be(16);
            graphBuilder.Activity(activityId9).LatestFinishTime.Should().Be(26);
        }

        [Fact]
        public void ArrowGraphBuilderExtensions_GivenCalculateCriticalPath_WhenMinimumEarliestStartTimeAndMaximumLatestFinishTimeAreInvalid_ThenShouldThrowInvalidOperationException()
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
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int>(activityId1, 6));
            graphBuilder.AddActivity(new Activity<int, int>(activityId2, 7));
            graphBuilder.AddActivity(new Activity<int, int>(activityId3, 8));
            graphBuilder.AddActivity(new Activity<int, int>(activityId4, 11) { MinimumEarliestStartTime = 7, MaximumLatestFinishTime = 17 }, new HashSet<int>(new[] { 2 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId5, 8), new HashSet<int>(new[] { 1, 2, 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId6, 7), new HashSet<int>(new[] { 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId7, 4), new HashSet<int>(new[] { 4 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId8, 4), new HashSet<int>(new[] { 4, 6 }));
            graphBuilder.AddActivity(new Activity<int, int>(activityId9, 10), new HashSet<int>(new[] { 5 }));

            graphBuilder.TransitiveReduction();

            Action act = () => graphBuilder.CalculateCriticalPath();
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void ArrowGraphBuilderExtensions_GivenCalculateCriticalPathPriorityList_ThenCorrectOrder()
        {
            int eventId = 0;
            int dummyActivityId = 100;
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int>(1, 6));
            graphBuilder.AddActivity(new Activity<int, int>(2, 7));
            graphBuilder.AddActivity(new Activity<int, int>(3, 8));
            graphBuilder.AddActivity(new Activity<int, int>(4, 11), new HashSet<int>(new[] { 2 }));
            graphBuilder.AddActivity(new Activity<int, int>(5, 8), new HashSet<int>(new[] { 1, 2, 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(6, 7), new HashSet<int>(new[] { 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(7, 4), new HashSet<int>(new[] { 4 }));
            graphBuilder.AddActivity(new Activity<int, int>(8, 4), new HashSet<int>(new[] { 4, 6 }));
            graphBuilder.AddActivity(new Activity<int, int>(9, 10), new HashSet<int>(new[] { 5 }));

            List<int> priorityList = graphBuilder.CalculateCriticalPathPriorityList().ToList();

            priorityList.Should().BeEquivalentTo(new List<int>(new[] { 3, 2, 1, 5, 4, 6, 9, 7, 8 }));
        }

        [Fact]
        public void ArrowGraphBuilderExtensions_GivenCalculateResourceSchedulesByPriorityList_WhenOneResource_ThenCorrectOrder()
        {
            int eventId = 0;
            int dummyActivityId = 100;
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int>(1, 6));
            graphBuilder.AddActivity(new Activity<int, int>(2, 7));
            graphBuilder.AddActivity(new Activity<int, int>(3, 8));
            graphBuilder.AddActivity(new Activity<int, int>(4, 11), new HashSet<int>(new[] { 2 }));
            graphBuilder.AddActivity(new Activity<int, int>(5, 8), new HashSet<int>(new[] { 1, 2, 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(6, 7), new HashSet<int>(new[] { 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(7, 4), new HashSet<int>(new[] { 4 }));
            graphBuilder.AddActivity(new Activity<int, int>(8, 4), new HashSet<int>(new[] { 4, 6 }));
            graphBuilder.AddActivity(new Activity<int, int>(9, 10), new HashSet<int>(new[] { 5 }));

            IList<IResourceSchedule<int, int>> resourceSchedules =
                graphBuilder.CalculateResourceSchedulesByPriorityList(
                    new List<IResource<int>>(new[]
                    {
                        new Resource<int>(1, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0)
                    })).ToList();
            resourceSchedules.Count.Should().Be(1);

            var scheduledActivities0 = resourceSchedules[0].ScheduledActivities.ToList();
            scheduledActivities0.Count.Should().Be(9);
            scheduledActivities0[0].Id.Should().Be(3);
            scheduledActivities0[0].StartTime.Should().Be(0);
            scheduledActivities0[0].FinishTime.Should().Be(8);

            scheduledActivities0[1].Id.Should().Be(2);
            scheduledActivities0[1].StartTime.Should().Be(8);
            scheduledActivities0[1].FinishTime.Should().Be(15);

            scheduledActivities0[2].Id.Should().Be(1);
            scheduledActivities0[2].StartTime.Should().Be(15);
            scheduledActivities0[2].FinishTime.Should().Be(21);

            scheduledActivities0[3].Id.Should().Be(5);
            scheduledActivities0[3].StartTime.Should().Be(21);
            scheduledActivities0[3].FinishTime.Should().Be(29);

            scheduledActivities0[4].Id.Should().Be(4);
            scheduledActivities0[4].StartTime.Should().Be(29);
            scheduledActivities0[4].FinishTime.Should().Be(40);

            scheduledActivities0[5].Id.Should().Be(6);
            scheduledActivities0[5].StartTime.Should().Be(40);
            scheduledActivities0[5].FinishTime.Should().Be(47);

            scheduledActivities0[6].Id.Should().Be(9);
            scheduledActivities0[6].StartTime.Should().Be(47);
            scheduledActivities0[6].FinishTime.Should().Be(57);

            scheduledActivities0[7].Id.Should().Be(7);
            scheduledActivities0[7].StartTime.Should().Be(57);
            scheduledActivities0[7].FinishTime.Should().Be(61);

            scheduledActivities0[8].Id.Should().Be(8);
            scheduledActivities0[8].StartTime.Should().Be(61);
            scheduledActivities0[8].FinishTime.Should().Be(65);

            scheduledActivities0.Last().FinishTime.Should().Be(65);
        }

        [Fact]
        public void ArrowGraphBuilderExtensions_GivenCalculateResourceSchedulesByPriorityList_WhenTwoResources_ThenCorrectOrder()
        {
            int eventId = 0;
            int dummyActivityId = 100;
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int>(1, 6));
            graphBuilder.AddActivity(new Activity<int, int>(2, 7));
            graphBuilder.AddActivity(new Activity<int, int>(3, 8));
            graphBuilder.AddActivity(new Activity<int, int>(4, 11), new HashSet<int>(new[] { 2 }));
            graphBuilder.AddActivity(new Activity<int, int>(5, 8), new HashSet<int>(new[] { 1, 2, 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(6, 7), new HashSet<int>(new[] { 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(7, 4), new HashSet<int>(new[] { 4 }));
            graphBuilder.AddActivity(new Activity<int, int>(8, 4), new HashSet<int>(new[] { 4, 6 }));
            graphBuilder.AddActivity(new Activity<int, int>(9, 10), new HashSet<int>(new[] { 5 }));

            IList<IResourceSchedule<int, int>> resourceSchedules =
                graphBuilder.CalculateResourceSchedulesByPriorityList(
                    new List<IResource<int>>(new[]
                    {
                        new Resource<int>(1, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0),
                        new Resource<int>(2, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0)
                    })).ToList();
            resourceSchedules.Count.Should().Be(2);

            var scheduledActivities0 = resourceSchedules[0].ScheduledActivities.ToList();
            scheduledActivities0.Count.Should().Be(5);
            scheduledActivities0[0].Id.Should().Be(3);
            scheduledActivities0[0].StartTime.Should().Be(0);
            scheduledActivities0[0].FinishTime.Should().Be(8);

            scheduledActivities0[1].Id.Should().Be(4);
            scheduledActivities0[1].StartTime.Should().Be(8);
            scheduledActivities0[1].FinishTime.Should().Be(19);

            scheduledActivities0[2].Id.Should().Be(6);
            scheduledActivities0[2].StartTime.Should().Be(19);
            scheduledActivities0[2].FinishTime.Should().Be(26);

            scheduledActivities0[3].Id.Should().Be(7);
            scheduledActivities0[3].StartTime.Should().Be(26);
            scheduledActivities0[3].FinishTime.Should().Be(30);

            scheduledActivities0[4].Id.Should().Be(8);
            scheduledActivities0[4].StartTime.Should().Be(30);
            scheduledActivities0[4].FinishTime.Should().Be(34);

            scheduledActivities0.Last().FinishTime.Should().Be(34);


            var scheduledActivities1 = resourceSchedules[1].ScheduledActivities.ToList();
            scheduledActivities1.Count.Should().Be(4);

            scheduledActivities1[0].Id.Should().Be(2);
            scheduledActivities1[0].StartTime.Should().Be(0);
            scheduledActivities1[0].FinishTime.Should().Be(7);

            scheduledActivities1[1].Id.Should().Be(1);
            scheduledActivities1[1].StartTime.Should().Be(7);
            scheduledActivities1[1].FinishTime.Should().Be(13);

            scheduledActivities1[2].Id.Should().Be(5);
            scheduledActivities1[2].StartTime.Should().Be(13);
            scheduledActivities1[2].FinishTime.Should().Be(21);

            scheduledActivities1[3].Id.Should().Be(9);
            scheduledActivities1[3].StartTime.Should().Be(21);
            scheduledActivities1[3].FinishTime.Should().Be(31);

            scheduledActivities1.Last().FinishTime.Should().Be(31);
        }

        [Fact]
        public void ArrowGraphBuilderExtensions_GivenCalculateResourceSchedulesByPriorityList_WhenThreeResources_ThenCorrectOrder()
        {
            int eventId = 0;
            int dummyActivityId = 100;
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int>(1, 6));
            graphBuilder.AddActivity(new Activity<int, int>(2, 7));
            graphBuilder.AddActivity(new Activity<int, int>(3, 8));
            graphBuilder.AddActivity(new Activity<int, int>(4, 11), new HashSet<int>(new[] { 2 }));
            graphBuilder.AddActivity(new Activity<int, int>(5, 8), new HashSet<int>(new[] { 1, 2, 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(6, 7), new HashSet<int>(new[] { 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(7, 4), new HashSet<int>(new[] { 4 }));
            graphBuilder.AddActivity(new Activity<int, int>(8, 4), new HashSet<int>(new[] { 4, 6 }));
            graphBuilder.AddActivity(new Activity<int, int>(9, 10), new HashSet<int>(new[] { 5 }));

            IList<IResourceSchedule<int, int>> resourceSchedules =
                graphBuilder.CalculateResourceSchedulesByPriorityList(
                    new List<IResource<int>>(new[]
                    {
                        new Resource<int>(1, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0),
                        new Resource<int>(2, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0),
                        new Resource<int>(3, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0)
                    })).ToList();
            resourceSchedules.Count.Should().Be(3);

            var scheduledActivities0 = resourceSchedules[0].ScheduledActivities.ToList();
            scheduledActivities0.Count.Should().Be(3);
            scheduledActivities0[0].Id.Should().Be(3);
            scheduledActivities0[0].StartTime.Should().Be(0);
            scheduledActivities0[0].FinishTime.Should().Be(8);

            scheduledActivities0[1].Id.Should().Be(5);
            scheduledActivities0[1].StartTime.Should().Be(8);
            scheduledActivities0[1].FinishTime.Should().Be(16);

            scheduledActivities0[2].Id.Should().Be(9);
            scheduledActivities0[2].StartTime.Should().Be(16);
            scheduledActivities0[2].FinishTime.Should().Be(26);

            scheduledActivities0.Last().FinishTime.Should().Be(26);


            var scheduledActivities1 = resourceSchedules[1].ScheduledActivities.ToList();
            scheduledActivities1.Count.Should().Be(3);
            scheduledActivities1[0].Id.Should().Be(2);
            scheduledActivities1[0].StartTime.Should().Be(0);
            scheduledActivities1[0].FinishTime.Should().Be(7);

            scheduledActivities1[1].Id.Should().Be(4);
            scheduledActivities1[1].StartTime.Should().Be(7);
            scheduledActivities1[1].FinishTime.Should().Be(18);

            scheduledActivities1[2].Id.Should().Be(7);
            scheduledActivities1[2].StartTime.Should().Be(18);
            scheduledActivities1[2].FinishTime.Should().Be(22);

            scheduledActivities1.Last().FinishTime.Should().Be(22);


            var scheduledActivities2 = resourceSchedules[2].ScheduledActivities.ToList();
            scheduledActivities2.Count.Should().Be(3);
            scheduledActivities2[0].Id.Should().Be(1);
            scheduledActivities2[0].StartTime.Should().Be(0);
            scheduledActivities2[0].FinishTime.Should().Be(6);

            scheduledActivities2[1].Id.Should().Be(6);
            scheduledActivities2[1].StartTime.Should().Be(8);
            scheduledActivities2[1].FinishTime.Should().Be(15);

            scheduledActivities2[2].Id.Should().Be(8);
            scheduledActivities2[2].StartTime.Should().Be(18);
            scheduledActivities2[2].FinishTime.Should().Be(22);

            scheduledActivities2.Last().FinishTime.Should().Be(22);
        }

        [Fact]
        public void ArrowGraphBuilderExtensions_GivenCalculateResourceSchedulesByPriorityList_WhenFourResources_ThenCorrectOrder()
        {
            int eventId = 0;
            int dummyActivityId = 100;
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int>(1, 6));
            graphBuilder.AddActivity(new Activity<int, int>(2, 7));
            graphBuilder.AddActivity(new Activity<int, int>(3, 8));
            graphBuilder.AddActivity(new Activity<int, int>(4, 11), new HashSet<int>(new[] { 2 }));
            graphBuilder.AddActivity(new Activity<int, int>(5, 8), new HashSet<int>(new[] { 1, 2, 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(6, 7), new HashSet<int>(new[] { 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(7, 4), new HashSet<int>(new[] { 4 }));
            graphBuilder.AddActivity(new Activity<int, int>(8, 4), new HashSet<int>(new[] { 4, 6 }));
            graphBuilder.AddActivity(new Activity<int, int>(9, 10), new HashSet<int>(new[] { 5 }));

            IList<IResourceSchedule<int, int>> resourceSchedules =
                graphBuilder.CalculateResourceSchedulesByPriorityList(
                    new List<IResource<int>>(new[]
                    {
                        new Resource<int>(1, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0),
                        new Resource<int>(2, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0),
                        new Resource<int>(3, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0),
                        new Resource<int>(4, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 0)
                    })).ToList();
            resourceSchedules.Count.Should().Be(3);

            var scheduledActivities0 = resourceSchedules[0].ScheduledActivities.ToList();
            scheduledActivities0.Count.Should().Be(3);
            scheduledActivities0[0].Id.Should().Be(3);
            scheduledActivities0[0].StartTime.Should().Be(0);
            scheduledActivities0[0].FinishTime.Should().Be(8);

            scheduledActivities0[1].Id.Should().Be(5);
            scheduledActivities0[1].StartTime.Should().Be(8);
            scheduledActivities0[1].FinishTime.Should().Be(16);

            scheduledActivities0[2].Id.Should().Be(9);
            scheduledActivities0[2].StartTime.Should().Be(16);
            scheduledActivities0[2].FinishTime.Should().Be(26);

            scheduledActivities0.Last().FinishTime.Should().Be(26);


            var scheduledActivities1 = resourceSchedules[1].ScheduledActivities.ToList();
            scheduledActivities1.Count.Should().Be(3);
            scheduledActivities1[0].Id.Should().Be(2);
            scheduledActivities1[0].StartTime.Should().Be(0);
            scheduledActivities1[0].FinishTime.Should().Be(7);

            scheduledActivities1[1].Id.Should().Be(4);
            scheduledActivities1[1].StartTime.Should().Be(7);
            scheduledActivities1[1].FinishTime.Should().Be(18);

            scheduledActivities1[2].Id.Should().Be(7);
            scheduledActivities1[2].StartTime.Should().Be(18);
            scheduledActivities1[2].FinishTime.Should().Be(22);

            scheduledActivities1.Last().FinishTime.Should().Be(22);


            var scheduledActivities2 = resourceSchedules[2].ScheduledActivities.ToList();
            scheduledActivities2.Count.Should().Be(3);
            scheduledActivities2[0].Id.Should().Be(1);
            scheduledActivities2[0].StartTime.Should().Be(0);
            scheduledActivities2[0].FinishTime.Should().Be(6);

            scheduledActivities2[1].Id.Should().Be(6);
            scheduledActivities2[1].StartTime.Should().Be(8);
            scheduledActivities2[1].FinishTime.Should().Be(15);

            scheduledActivities2[2].Id.Should().Be(8);
            scheduledActivities2[2].StartTime.Should().Be(18);
            scheduledActivities2[2].FinishTime.Should().Be(22);

            scheduledActivities2.Last().FinishTime.Should().Be(22);
        }

        [Fact]
        public void ArrowGraphBuilderExtensions_GivenCalculateResourceSchedulesByPriorityList_WhenFourOrderedResources_ThenCorrectOrder()
        {
            int eventId = 0;
            int dummyActivityId = 100;
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int>(1, 6));
            graphBuilder.AddActivity(new Activity<int, int>(2, 7));
            graphBuilder.AddActivity(new Activity<int, int>(3, 8));
            graphBuilder.AddActivity(new Activity<int, int>(4, 11), new HashSet<int>(new[] { 2 }));
            graphBuilder.AddActivity(new Activity<int, int>(5, 8), new HashSet<int>(new[] { 1, 2, 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(6, 7), new HashSet<int>(new[] { 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(7, 4), new HashSet<int>(new[] { 4 }));
            graphBuilder.AddActivity(new Activity<int, int>(8, 4), new HashSet<int>(new[] { 4, 6 }));
            graphBuilder.AddActivity(new Activity<int, int>(9, 10), new HashSet<int>(new[] { 5 }));

            IList<IResourceSchedule<int, int>> resourceSchedules =
                graphBuilder.CalculateResourceSchedulesByPriorityList(
                    new List<IResource<int>>(new[]
                    {
                        new Resource<int>(1, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 4),
                        new Resource<int>(2, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 3),
                        new Resource<int>(3, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 2),
                        new Resource<int>(4, string.Empty, false, false, InterActivityAllocationType.None, 1.0, 1)
                    })).ToList();
            resourceSchedules.Count.Should().Be(3);

            resourceSchedules[0].Resource.Id.Should().Be(4);
            var scheduledActivities0 = resourceSchedules[0].ScheduledActivities.ToList();
            scheduledActivities0.Count.Should().Be(3);
            scheduledActivities0[0].Id.Should().Be(3);
            scheduledActivities0[0].StartTime.Should().Be(0);
            scheduledActivities0[0].FinishTime.Should().Be(8);

            scheduledActivities0[1].Id.Should().Be(5);
            scheduledActivities0[1].StartTime.Should().Be(8);
            scheduledActivities0[1].FinishTime.Should().Be(16);

            scheduledActivities0[2].Id.Should().Be(9);
            scheduledActivities0[2].StartTime.Should().Be(16);
            scheduledActivities0[2].FinishTime.Should().Be(26);

            scheduledActivities0.Last().FinishTime.Should().Be(26);


            resourceSchedules[1].Resource.Id.Should().Be(3);
            var scheduledActivities1 = resourceSchedules[1].ScheduledActivities.ToList();
            scheduledActivities1.Count.Should().Be(3);
            scheduledActivities1[0].Id.Should().Be(2);
            scheduledActivities1[0].StartTime.Should().Be(0);
            scheduledActivities1[0].FinishTime.Should().Be(7);

            scheduledActivities1[1].Id.Should().Be(4);
            scheduledActivities1[1].StartTime.Should().Be(7);
            scheduledActivities1[1].FinishTime.Should().Be(18);

            scheduledActivities1[2].Id.Should().Be(7);
            scheduledActivities1[2].StartTime.Should().Be(18);
            scheduledActivities1[2].FinishTime.Should().Be(22);

            scheduledActivities1.Last().FinishTime.Should().Be(22);


            resourceSchedules[2].Resource.Id.Should().Be(2);
            var scheduledActivities2 = resourceSchedules[2].ScheduledActivities.ToList();
            scheduledActivities2.Count.Should().Be(3);
            scheduledActivities2[0].Id.Should().Be(1);
            scheduledActivities2[0].StartTime.Should().Be(0);
            scheduledActivities2[0].FinishTime.Should().Be(6);

            scheduledActivities2[1].Id.Should().Be(6);
            scheduledActivities2[1].StartTime.Should().Be(8);
            scheduledActivities2[1].FinishTime.Should().Be(15);

            scheduledActivities2[2].Id.Should().Be(8);
            scheduledActivities2[2].StartTime.Should().Be(18);
            scheduledActivities2[2].FinishTime.Should().Be(22);

            scheduledActivities2.Last().FinishTime.Should().Be(22);
        }

        [Fact]
        public void ArrowGraphBuilderExtensions_GivenCalculateResourceSchedulesByPriorityList_WhenUnlimitedResources_ThenCorrectOrder()
        {
            int eventId = 0;
            int dummyActivityId = 100;
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };
            graphBuilder.AddActivity(new Activity<int, int>(1, 6));
            graphBuilder.AddActivity(new Activity<int, int>(2, 7));
            graphBuilder.AddActivity(new Activity<int, int>(3, 8));
            graphBuilder.AddActivity(new Activity<int, int>(4, 11), new HashSet<int>(new[] { 2 }));
            graphBuilder.AddActivity(new Activity<int, int>(5, 8), new HashSet<int>(new[] { 1, 2, 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(6, 7), new HashSet<int>(new[] { 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(7, 4), new HashSet<int>(new[] { 4 }));
            graphBuilder.AddActivity(new Activity<int, int>(8, 4), new HashSet<int>(new[] { 4, 6 }));
            graphBuilder.AddActivity(new Activity<int, int>(9, 10), new HashSet<int>(new[] { 5 }));

            IList<IResourceSchedule<int, int>> resourceSchedules = graphBuilder.CalculateResourceSchedulesByPriorityList(new List<IResource<int>>()).ToList();
            resourceSchedules.Count.Should().Be(3);

            var scheduledActivities0 = resourceSchedules[0].ScheduledActivities.ToList();
            scheduledActivities0.Count.Should().Be(3);
            scheduledActivities0[0].Id.Should().Be(3);
            scheduledActivities0[0].StartTime.Should().Be(0);
            scheduledActivities0[0].FinishTime.Should().Be(8);

            scheduledActivities0[1].Id.Should().Be(5);
            scheduledActivities0[1].StartTime.Should().Be(8);
            scheduledActivities0[1].FinishTime.Should().Be(16);

            scheduledActivities0[2].Id.Should().Be(9);
            scheduledActivities0[2].StartTime.Should().Be(16);
            scheduledActivities0[2].FinishTime.Should().Be(26);

            scheduledActivities0.Last().FinishTime.Should().Be(26);


            var scheduledActivities1 = resourceSchedules[1].ScheduledActivities.ToList();
            scheduledActivities1.Count.Should().Be(3);
            scheduledActivities1[0].Id.Should().Be(2);
            scheduledActivities1[0].StartTime.Should().Be(0);
            scheduledActivities1[0].FinishTime.Should().Be(7);

            scheduledActivities1[1].Id.Should().Be(4);
            scheduledActivities1[1].StartTime.Should().Be(7);
            scheduledActivities1[1].FinishTime.Should().Be(18);

            scheduledActivities1[2].Id.Should().Be(7);
            scheduledActivities1[2].StartTime.Should().Be(18);
            scheduledActivities1[2].FinishTime.Should().Be(22);

            scheduledActivities1.Last().FinishTime.Should().Be(22);


            var scheduledActivities2 = resourceSchedules[2].ScheduledActivities.ToList();
            scheduledActivities2.Count.Should().Be(3);
            scheduledActivities2[0].Id.Should().Be(1);
            scheduledActivities2[0].StartTime.Should().Be(0);
            scheduledActivities2[0].FinishTime.Should().Be(6);

            scheduledActivities2[1].Id.Should().Be(6);
            scheduledActivities2[1].StartTime.Should().Be(8);
            scheduledActivities2[1].FinishTime.Should().Be(15);

            scheduledActivities2[2].Id.Should().Be(8);
            scheduledActivities2[2].StartTime.Should().Be(18);
            scheduledActivities2[2].FinishTime.Should().Be(22);

            scheduledActivities2.Last().FinishTime.Should().Be(22);
        }
    }
}
