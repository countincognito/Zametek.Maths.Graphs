using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class ArrowGraphBuilderTests
    {
        [Fact]
        public void ArrowGraphBuilder_GivenGivenCtor_ThenThenNoException()
        {
            int eventId = 0;
            int dummyActivityId = 0;
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());

            graphBuilder.EdgeIds.Any().Should().BeFalse();
            graphBuilder.NodeIds.Count().Should().Be(2);
            graphBuilder.AllDependenciesSatisfied.Should().BeTrue();
            graphBuilder.StartNode.OutgoingEdges.Count.Should().Be(0);
            graphBuilder.EndNode.IncomingEdges.Count.Should().Be(0);
        }

        [Fact]
        public void ArrowGraphBuilder_GivenGivenCtorCalledWithNullEdgeIdGenerator_ThenThenShouldThrowArgumentNullException()
        {
            int eventId = 0;
            Action act = () => new ArrowGraphBuilder<int, int, IActivity<int, int>>(null, () => eventId = eventId.Next());
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void ArrowGraphBuilder_GivenGivenCtorCalledWithNullNodeIdGenerator_ThenThenShouldThrowArgumentNullException()
        {
            int dummyActivityId = 0;
            Action act = () => new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), null);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void ArrowGraphBuilder_GivenGivenAccessOutgoingEdgesOfEndNode_ThenThenShouldThrowInvalidOperationException()
        {
            int eventId = 0;
            int dummyActivityId = 0;
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            Action act = () => graphBuilder.EndNode.OutgoingEdges.Any();
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void ArrowGraphBuilder_GivenSingleActivityNoDependencies_ThenHooksUpToStartAndEndNodes()
        {
            int eventId = 0;
            int activityId = 1;
            int dummyActivityId = activityId;
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            int dummyActivityId1 = dummyActivityId + 1;

            var activity = new Activity<int, int>(activityId, 0);
            bool result = graphBuilder.AddActivity(activity);
            result.Should().BeTrue();

            graphBuilder.EdgeIds.Count().Should().Be(2);
            graphBuilder.NodeIds.Count().Should().Be(3);
            graphBuilder.AllDependenciesSatisfied.Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId).Id.Should().Be(graphBuilder.StartNode.Id);
            graphBuilder.EdgeTailNode(dummyActivityId1).Id.Should().Be(graphBuilder.EdgeHeadNode(activityId).Id);
            graphBuilder.EdgeHeadNode(dummyActivityId1).Id.Should().Be(graphBuilder.EndNode.Id);
            graphBuilder.StartNode.OutgoingEdges.Count.Should().Be(1);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId).Should().BeTrue();
            graphBuilder.EndNode.IncomingEdges.Count.Should().Be(1);
            graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId1).Should().BeTrue();
        }

        [Fact]
        public void ArrowGraphBuilder_GivenTwoActivitiesOneDependency_ThenActivitiesHookedUpByDummyEdge()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int dummyActivityId = activityId2 + 1;
            int dummyActivityId1 = dummyActivityId + 1;
            int dummyActivityId2 = dummyActivityId1 + 1;
            int dummyActivityId3 = dummyActivityId2 + 1;
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.Should().BeTrue();

            graphBuilder.EdgeIds.Count().Should().Be(2);
            graphBuilder.NodeIds.Count().Should().Be(3);
            graphBuilder.AllDependenciesSatisfied.Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId1).Id.Should().Be(graphBuilder.StartNode.Id);
            graphBuilder.EdgeTailNode(dummyActivityId1).Id.Should().Be(graphBuilder.EdgeHeadNode(activityId1).Id);
            graphBuilder.EdgeHeadNode(dummyActivityId1).Id.Should().Be(graphBuilder.EndNode.Id);
            graphBuilder.StartNode.OutgoingEdges.Count.Should().Be(1);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.EndNode.IncomingEdges.Count.Should().Be(1);
            graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId1).Should().BeTrue();

            var activity2 = new Activity<int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2, new HashSet<int>(new[] { activityId1 }));
            result2.Should().BeTrue();

            graphBuilder.EdgeIds.Count().Should().Be(5);
            graphBuilder.NodeIds.Count().Should().Be(5);
            graphBuilder.AllDependenciesSatisfied.Should().BeTrue();

            // First activity.
            graphBuilder.EdgeTailNode(activityId1).Id.Should().Be(graphBuilder.StartNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.Should().Be(1);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId1).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId1).Should().BeTrue();
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId2).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId1).Id.Should().Be(graphBuilder.EdgeHeadNode(activityId1).Id);
            graphBuilder.EdgeTailNode(dummyActivityId2).Id.Should().Be(graphBuilder.EdgeHeadNode(activityId1).Id);

            // Dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Contains(activityId1).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId2).Should().BeTrue();

            graphBuilder.Edge(dummyActivityId1).Content.IsDummy.Should().BeTrue();
            graphBuilder.Edge(dummyActivityId2).Content.IsDummy.Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId1).Should().BeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId3).Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId1).Id.Should().Be(graphBuilder.EndNode.Id);

            // Second activity.
            graphBuilder.EdgeTailNode(activityId2).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(activityId2).IncomingEdges.Contains(dummyActivityId2).Should().BeTrue();

            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId3).Should().BeTrue();

            // Dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Contains(activityId2).Should().BeTrue();

            graphBuilder.Edge(dummyActivityId3).Content.IsDummy.Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId1).Should().BeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId3).Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId3).Id.Should().Be(graphBuilder.EndNode.Id);

            graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId2).Should().BeTrue();

            graphBuilder.Edge(dummyActivityId2).Content.IsDummy.Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId2).Should().BeTrue();
        }

        [Fact]
        public void ArrowGraphBuilder_GivenTwoActivitiesOneDependencyReverseOrder_ThenActivitiesHookedUpByDummyEdge()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int dummyActivityId = activityId2 + 1;
            int dummyActivityId1 = dummyActivityId + 1;
            int dummyActivityId2 = dummyActivityId1 + 1;
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            var activity2 = new Activity<int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2, new HashSet<int>(new[] { activityId1 }));
            result2.Should().BeTrue();

            graphBuilder.EdgeIds.Count().Should().Be(2);
            graphBuilder.NodeIds.Count().Should().Be(4);
            graphBuilder.AllDependenciesSatisfied.Should().BeFalse();
            graphBuilder.EdgeTailNode(activityId2).Id.Should().NotBe(graphBuilder.StartNode.Id);
            graphBuilder.StartNode.OutgoingEdges.Count.Should().Be(0);

            graphBuilder.EdgeTailNode(activityId2).IncomingEdges.Count.Should().Be(0);

            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2).Should().BeTrue();

            var activity1 = new Activity<int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.Should().BeTrue();

            graphBuilder.EdgeIds.Count().Should().Be(4);
            graphBuilder.NodeIds.Count().Should().Be(5);
            graphBuilder.AllDependenciesSatisfied.Should().BeTrue();

            // First activity.
            graphBuilder.EdgeTailNode(activityId1).Id.Should().Be(graphBuilder.StartNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.Should().Be(1);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId1).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId2).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId2).Id.Should().Be(graphBuilder.EdgeHeadNode(activityId1).Id);

            // Dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Contains(activityId2).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId1).Should().BeTrue();

            graphBuilder.Edge(dummyActivityId1).Content.IsDummy.Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId1).Should().BeTrue();

            graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId1).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId2).Id.Should().Be(graphBuilder.EdgeTailNode(dummyActivityId1).Id);

            // Second activity.
            graphBuilder.EdgeTailNode(activityId2).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(activityId2).IncomingEdges.Contains(dummyActivityId2).Should().BeTrue();

            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2).Should().BeTrue();

            // Dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Contains(activityId2).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId1).Should().BeTrue();

            graphBuilder.Edge(dummyActivityId1).Content.IsDummy.Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId1).Should().BeTrue();

            graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId1).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId2).Id.Should().Be(graphBuilder.EdgeTailNode(dummyActivityId1).Id);
            graphBuilder.EndNode.IncomingEdges.Count.Should().Be(1);
            graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId1).Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId1).Id.Should().Be(graphBuilder.EndNode.Id);
        }

        [Fact]
        public void ArrowGraphBuilder_GivenThreeActivitiesOneDependentOnOtherTwo_ThenDependentActivityHookedUpByTwoDummyEdges()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int dummyActivityId = activityId3 + 1;
            int dummyActivityId1 = dummyActivityId + 1;
            int dummyActivityId2 = dummyActivityId1 + 1;
            int dummyActivityId3 = dummyActivityId2 + 1;
            int dummyActivityId4 = dummyActivityId3 + 1;
            int dummyActivityId5 = dummyActivityId4 + 1;
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.Should().BeTrue();

            graphBuilder.EdgeIds.Count().Should().Be(2);
            graphBuilder.NodeIds.Count().Should().Be(3);
            graphBuilder.AllDependenciesSatisfied.Should().BeTrue();

            // First activity.
            graphBuilder.EdgeTailNode(activityId1).Id.Should().Be(graphBuilder.StartNode.Id);
            graphBuilder.StartNode.OutgoingEdges.Count.Should().Be(1);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId1).Id.Should().Be(graphBuilder.EdgeHeadNode(activityId1).Id);

            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId1).Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId1).Id.Should().Be(graphBuilder.EndNode.Id);
            graphBuilder.EndNode.IncomingEdges.Count.Should().Be(1);
            graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId1).Should().BeTrue();

            var activity2 = new Activity<int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.Should().BeTrue();

            graphBuilder.EdgeIds.Count().Should().Be(4);
            graphBuilder.NodeIds.Count().Should().Be(4);
            graphBuilder.AllDependenciesSatisfied.Should().BeTrue();

            // Second activity.
            graphBuilder.EdgeTailNode(activityId2).Id.Should().Be(graphBuilder.StartNode.Id);
            graphBuilder.StartNode.OutgoingEdges.Count.Should().Be(2);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId2).Id.Should().Be(graphBuilder.EdgeHeadNode(activityId2).Id);

            graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId2).Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId2).Id.Should().Be(graphBuilder.EndNode.Id);
            graphBuilder.EndNode.IncomingEdges.Count.Should().Be(2);
            graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId2).Should().BeTrue();

            // Dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Contains(activityId1).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId1).Should().BeTrue();

            graphBuilder.Edge(dummyActivityId1).Content.IsDummy.Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId1).Should().BeTrue();

            graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId1).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId1).Id.Should().Be(graphBuilder.EdgeTailNode(dummyActivityId1).Id);

            var activity3 = new Activity<int, int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int>(new[] { activityId1, activityId2 }));
            result3.Should().BeTrue();

            graphBuilder.EdgeIds.Count().Should().Be(8);
            graphBuilder.NodeIds.Count().Should().Be(6);
            graphBuilder.AllDependenciesSatisfied.Should().BeTrue();

            // First activity.
            graphBuilder.EdgeTailNode(activityId1).Id.Should().Be(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId1).Id.Should().NotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.Should().Be(2);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId1).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId1).Should().BeTrue();
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId3).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId1).Id.Should().Be(graphBuilder.EdgeHeadNode(activityId1).Id);

            // Second activity.
            graphBuilder.EdgeTailNode(activityId2).Id.Should().Be(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId2).Id.Should().NotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.Should().Be(2);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId2).Should().BeTrue();
            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId4).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId2).Id.Should().Be(graphBuilder.EdgeHeadNode(activityId2).Id);

            // First dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Contains(activityId1).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Contains(dummyActivityId1).Should().BeTrue();

            graphBuilder.Edge(dummyActivityId1).Content.IsDummy.Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Count.Should().Be(3);
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId1).Should().BeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId2).Should().BeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId5).Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId1).Id.Should().Be(graphBuilder.EndNode.Id);

            // Second dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Contains(activityId2).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId2).Should().BeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId4).OutgoingEdges.Contains(dummyActivityId2).Should().BeTrue();

            graphBuilder.Edge(dummyActivityId2).Content.IsDummy.Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Count.Should().Be(3);
            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId1).Should().BeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId2).Should().BeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId5).Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId2).Id.Should().Be(graphBuilder.EndNode.Id);

            // Third dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Contains(activityId1).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Contains(dummyActivityId1).Should().BeTrue();

            graphBuilder.Edge(dummyActivityId3).Content.IsDummy.Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId3).Should().BeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId4).Should().BeTrue();

            // Forth dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId4).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(dummyActivityId4).IncomingEdges.Contains(activityId2).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId4).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId2).Should().BeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId4).OutgoingEdges.Contains(dummyActivityId2).Should().BeTrue();

            graphBuilder.Edge(dummyActivityId4).Content.IsDummy.Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId4).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeHeadNode(dummyActivityId4).IncomingEdges.Contains(dummyActivityId3).Should().BeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId4).IncomingEdges.Contains(dummyActivityId4).Should().BeTrue();

            // Fifth dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId5).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(dummyActivityId5).IncomingEdges.Contains(activityId3).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId5).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(dummyActivityId5).OutgoingEdges.Contains(dummyActivityId5).Should().BeTrue();

            graphBuilder.Edge(dummyActivityId5).Content.IsDummy.Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId5).IncomingEdges.Count.Should().Be(3);
            graphBuilder.EdgeHeadNode(dummyActivityId5).IncomingEdges.Contains(dummyActivityId1).Should().BeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId5).IncomingEdges.Contains(dummyActivityId2).Should().BeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId5).IncomingEdges.Contains(dummyActivityId5).Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId5).Id.Should().Be(graphBuilder.EndNode.Id);

            // Third activity.
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId3).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId4).Should().BeTrue();

            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Contains(activityId3).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Contains(activityId3).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Contains(dummyActivityId5).Should().BeTrue();
        }

        [Fact]
        public void ArrowGraphBuilder_GivenThreeActivitiesOneDependentOnOtherTwoReverseOrder_ThenDependentActivityHookedUpByTwoDummyEdges()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int dummyActivityId = activityId3 + 1;
            int dummyActivityId1 = dummyActivityId + 1;
            int dummyActivityId2 = dummyActivityId1 + 1;
            int dummyActivityId3 = dummyActivityId2 + 1;
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            var activity3 = new Activity<int, int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int>(new[] { activityId1, activityId2 }));
            result3.Should().BeTrue();

            graphBuilder.EdgeIds.Count().Should().Be(2);
            graphBuilder.NodeIds.Count().Should().Be(4);
            graphBuilder.AllDependenciesSatisfied.Should().BeFalse();
            graphBuilder.EdgeTailNode(activityId3).Id.Should().NotBe(graphBuilder.StartNode.Id);
            graphBuilder.StartNode.OutgoingEdges.Count.Should().Be(0);

            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Count.Should().Be(0);
            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Contains(activityId3).Should().BeTrue();

            var activity2 = new Activity<int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.Should().BeTrue();

            graphBuilder.EdgeIds.Count().Should().Be(4);
            graphBuilder.NodeIds.Count().Should().Be(5);
            graphBuilder.AllDependenciesSatisfied.Should().BeFalse();
            graphBuilder.EdgeTailNode(activityId3).Id.Should().NotBe(graphBuilder.StartNode.Id);

            // Second activity.
            graphBuilder.EdgeTailNode(activityId2).Id.Should().Be(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId2).Id.Should().NotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.Should().Be(1);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId2).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId2).Id.Should().Be(graphBuilder.EdgeHeadNode(activityId2).Id);

            // Second dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Contains(activityId2).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId2).Should().BeTrue();

            graphBuilder.Edge(dummyActivityId2).Content.IsDummy.Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId2).Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId2).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(dummyActivityId2).OutgoingEdges.Contains(activityId3).Should().BeTrue();

            graphBuilder.EdgeTailNode(activityId3).Id.Should().Be(graphBuilder.EdgeHeadNode(dummyActivityId2).Id);

            // Third activity.
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId2).Should().BeTrue();

            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Contains(activityId3).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Contains(activityId3).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId3).Id.Should().NotBe(graphBuilder.EndNode.Id);
            graphBuilder.EdgeTailNode(activityId3).Id.Should().NotBe(graphBuilder.StartNode.Id);

            var activity1 = new Activity<int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.Should().BeTrue();

            graphBuilder.EdgeIds.Count().Should().Be(6);
            graphBuilder.NodeIds.Count().Should().Be(6);
            graphBuilder.AllDependenciesSatisfied.Should().BeTrue();

            // First activity.
            graphBuilder.EdgeTailNode(activityId1).Id.Should().Be(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId1).Id.Should().NotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.Should().Be(2);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId2).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId3).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId3).Id.Should().Be(graphBuilder.EdgeHeadNode(activityId1).Id);

            // Second activity.
            graphBuilder.EdgeTailNode(activityId2).Id.Should().Be(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId2).Id.Should().NotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.Should().Be(2);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId2).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId2).Id.Should().Be(graphBuilder.EdgeHeadNode(activityId2).Id);

            // First dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Contains(activityId3).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId1).Should().BeTrue();

            graphBuilder.Edge(dummyActivityId1).Content.IsDummy.Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId1).Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId1).Id.Should().Be(graphBuilder.EndNode.Id);

            graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId1).Should().BeTrue();

            // Second dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Contains(activityId2).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId2).Should().BeTrue();

            graphBuilder.Edge(dummyActivityId2).Content.IsDummy.Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId2).Should().BeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId3).Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId2).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(dummyActivityId2).OutgoingEdges.Contains(activityId3).Should().BeTrue();

            graphBuilder.EdgeTailNode(activityId3).Id.Should().Be(graphBuilder.EdgeHeadNode(dummyActivityId2).Id);

            // Third dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Contains(activityId1).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Contains(dummyActivityId3).Should().BeTrue();

            graphBuilder.Edge(dummyActivityId3).Content.IsDummy.Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId2).Should().BeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId3).Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId3).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(dummyActivityId3).OutgoingEdges.Contains(activityId3).Should().BeTrue();

            graphBuilder.EdgeTailNode(activityId3).Id.Should().Be(graphBuilder.EdgeHeadNode(dummyActivityId3).Id);

            // Third activity.
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId2).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId3).Should().BeTrue();

            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Contains(activityId3).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Contains(activityId3).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Contains(dummyActivityId1).Should().BeTrue();
        }

        [Fact]
        public void ArrowGraphBuilder_GivenThreeActivitiesOneDependentOnOtherTwoRemovedInStages_ThenStructureAsExpected()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int dummyActivityId = activityId3 + 1;
            int dummyActivityId1 = dummyActivityId + 1;
            int dummyActivityId2 = dummyActivityId1 + 1;
            int dummyActivityId3 = dummyActivityId2 + 1;
            int dummyActivityId4 = dummyActivityId3 + 1;
            int dummyActivityId5 = dummyActivityId4 + 1;
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int>(activityId1, 0, canBeRemoved: true);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.Should().BeTrue();

            var activity2 = new Activity<int, int>(activityId2, 0, canBeRemoved: true);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.Should().BeTrue();

            var activity3 = new Activity<int, int>(activityId3, 0, canBeRemoved: true);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int>(new[] { activityId1, activityId2 }));
            result3.Should().BeTrue();

            graphBuilder.EdgeIds.Count().Should().Be(8);
            graphBuilder.NodeIds.Count().Should().Be(6);
            graphBuilder.AllDependenciesSatisfied.Should().BeTrue();

            bool result4 = graphBuilder.RemoveDummyActivity(dummyActivityId1);
            result4.Should().BeTrue();

            bool result5 = graphBuilder.RemoveDummyActivity(dummyActivityId2);
            result5.Should().BeTrue();

            bool result6 = graphBuilder.RemoveDummyActivity(dummyActivityId3);
            result6.Should().BeTrue();

            graphBuilder.EdgeIds.Count().Should().Be(5);
            graphBuilder.NodeIds.Count().Should().Be(5);
            graphBuilder.AllDependenciesSatisfied.Should().BeTrue();

            // First activity.
            graphBuilder.EdgeTailNode(activityId1).Id.Should().Be(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId1).Id.Should().NotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.Should().Be(2);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).Should().BeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId2).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId1).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(dummyActivityId4).Should().BeTrue();
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(activityId3).Should().BeTrue();

            // Second activity.
            graphBuilder.EdgeTailNode(activityId2).Id.Should().Be(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId2).Id.Should().NotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.Should().Be(2);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).Should().BeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId1).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId4).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId4).Id.Should().Be(graphBuilder.EdgeHeadNode(activityId2).Id);

            // Third activity.
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId4).Should().BeTrue();

            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Contains(activityId3).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Contains(activityId3).Should().BeTrue();
            graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Contains(dummyActivityId5).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId3).Id.Should().NotBe(graphBuilder.EndNode.Id);
            graphBuilder.EdgeTailNode(activityId3).Id.Should().NotBe(graphBuilder.StartNode.Id);



            bool result7 = graphBuilder.RemoveDummyActivity(activityId3);
            result7.Should().BeTrue();

            graphBuilder.EdgeIds.Count().Should().Be(4);
            graphBuilder.NodeIds.Count().Should().Be(4);
            graphBuilder.AllDependenciesSatisfied.Should().BeTrue();

            // First activity.
            graphBuilder.EdgeTailNode(activityId1).Id.Should().Be(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId1).Id.Should().NotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.Should().Be(2);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).Should().BeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId2).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId1).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(dummyActivityId4).Should().BeTrue();
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId5).Should().BeTrue();

            // Second activity.
            graphBuilder.EdgeTailNode(activityId2).Id.Should().Be(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId2).Id.Should().NotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.Should().Be(2);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).Should().BeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId1).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId4).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId4).Id.Should().Be(graphBuilder.EdgeHeadNode(activityId2).Id);

            // Third activity.
            graphBuilder.EdgeIds.Contains(activityId3).Should().BeFalse();



            bool result8 = graphBuilder.RemoveDummyActivity(dummyActivityId5);
            result8.Should().BeTrue();

            graphBuilder.EdgeIds.Count().Should().Be(3);
            graphBuilder.NodeIds.Count().Should().Be(3);
            graphBuilder.AllDependenciesSatisfied.Should().BeTrue();


            // First activity.
            graphBuilder.EdgeTailNode(activityId1).Id.Should().Be(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId1).Id.Should().Be(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.Should().Be(2);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).Should().BeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId2).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId1).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(dummyActivityId4).Should().BeTrue();

            // Second activity.
            graphBuilder.EdgeTailNode(activityId2).Id.Should().Be(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId2).Id.Should().NotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.Should().Be(2);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).Should().BeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId1).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId4).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId4).Id.Should().Be(graphBuilder.EdgeHeadNode(activityId2).Id);

            // Third activity.
            graphBuilder.EdgeIds.Contains(activityId3).Should().BeFalse();



            bool result9 = graphBuilder.RemoveDummyActivity(activityId1);
            result9.Should().BeFalse();
            bool result10 = graphBuilder.RemoveDummyActivity(activityId2);
            result10.Should().BeFalse();
            bool result11 = graphBuilder.RemoveDummyActivity(dummyActivityId4);
            result11.Should().BeFalse();
        }

        [Fact]
        public void ArrowGraphBuilder_GivenThreeActivitiesOneDependentOnOtherTwoRedirectDummyEdges_ThenDummiesRedirectedAsExpected()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int dummyActivityId = activityId3 + 1;
            int dummyActivityId1 = dummyActivityId + 1;
            int dummyActivityId2 = dummyActivityId1 + 1;
            int dummyActivityId3 = dummyActivityId2 + 1;
            int dummyActivityId4 = dummyActivityId3 + 1;
            int dummyActivityId5 = dummyActivityId4 + 1;
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.Should().BeTrue();

            var activity2 = new Activity<int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.Should().BeTrue();

            var activity3 = new Activity<int, int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int>(new[] { activityId1, activityId2 }));
            result3.Should().BeTrue();

            graphBuilder.EdgeIds.Count().Should().Be(8);
            graphBuilder.NodeIds.Count().Should().Be(6);
            graphBuilder.AllDependenciesSatisfied.Should().BeTrue();

            bool result4 = graphBuilder.RedirectEdges();
            result4.Should().BeTrue();

            graphBuilder.EdgeIds.Count().Should().Be(7);
            graphBuilder.NodeIds.Count().Should().Be(6);
            graphBuilder.AllDependenciesSatisfied.Should().BeTrue();

            // First activity.
            graphBuilder.EdgeTailNode(activityId1).Id.Should().Be(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId1).Id.Should().NotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.Should().Be(2);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId1).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count.Should().Be(2);

            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId1).Should().BeTrue();
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId3).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId1).Id.Should().Be(graphBuilder.EdgeHeadNode(activityId1).Id);
            graphBuilder.EdgeTailNode(dummyActivityId3).Id.Should().Be(graphBuilder.EdgeHeadNode(activityId1).Id);

            // Second activity.
            graphBuilder.EdgeTailNode(activityId2).Id.Should().Be(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId2).Id.Should().NotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.Should().Be(2);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId2).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId2).Id.Should().Be(graphBuilder.EdgeHeadNode(activityId2).Id);

            // First dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId2).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId3).Should().BeTrue();

            graphBuilder.Edge(dummyActivityId1).Content.IsDummy.Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId1).Should().BeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(activityId3).Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId1).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId5).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId5).Id.Should().Be(graphBuilder.EdgeHeadNode(dummyActivityId1).Id);

            // Second dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Contains(activityId2).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId2).Should().BeTrue();

            graphBuilder.Edge(dummyActivityId2).Content.IsDummy.Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId2).Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId2).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeHeadNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId1).Should().BeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId3).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId1).Id.Should().Be(graphBuilder.EdgeHeadNode(dummyActivityId2).Id);
            graphBuilder.EdgeTailNode(dummyActivityId3).Id.Should().Be(graphBuilder.EdgeHeadNode(dummyActivityId2).Id);

            // Third activity.
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId3).Should().BeTrue();

            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Contains(activityId3).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Contains(dummyActivityId1).Should().BeTrue();
            graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Contains(activityId3).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Contains(dummyActivityId5).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId5).Id.Should().Be(graphBuilder.EdgeHeadNode(activityId3).Id);

            // Third dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId2).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Contains(dummyActivityId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Contains(dummyActivityId3).Should().BeTrue();

            graphBuilder.Edge(dummyActivityId3).Content.IsDummy.Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId3).Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId3).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(dummyActivityId3).OutgoingEdges.Contains(activityId3).Should().BeTrue();

            graphBuilder.EdgeTailNode(activityId3).Id.Should().Be(graphBuilder.EdgeHeadNode(dummyActivityId3).Id);

            // Fourth dummy activity.
            graphBuilder.EdgeIds.Contains(dummyActivityId4).Should().BeFalse();

            // Fifth dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId5).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(dummyActivityId5).IncomingEdges.Contains(activityId3).Should().BeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId5).IncomingEdges.Contains(dummyActivityId1).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId5).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(dummyActivityId5).OutgoingEdges.Contains(dummyActivityId5).Should().BeTrue();

            graphBuilder.Edge(dummyActivityId5).Content.IsDummy.Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId5).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(dummyActivityId5).IncomingEdges.Contains(dummyActivityId5).Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId5).Id.Should().Be(graphBuilder.EndNode.Id);
        }

        [Fact]
        public void ArrowGraphBuilder_GivenFourActivitiesOneDependentOnOtherThreeRedirectDummyEdges_ThenDummiesRedirectedAsExpected()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int dummyActivityId = activityId4 + 1;
            int dummyActivityId1 = dummyActivityId + 1;
            int dummyActivityId2 = dummyActivityId1 + 1;
            int dummyActivityId3 = dummyActivityId2 + 1;
            int dummyActivityId4 = dummyActivityId3 + 1;
            int dummyActivityId5 = dummyActivityId4 + 1;
            int dummyActivityId6 = dummyActivityId5 + 1;
            int dummyActivityId7 = dummyActivityId6 + 1;
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.Should().BeTrue();

            var activity2 = new Activity<int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.Should().BeTrue();

            var activity3 = new Activity<int, int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3);
            result3.Should().BeTrue();

            var activity4 = new Activity<int, int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int>(new[] { activityId1, activityId2, activityId3 }));
            result4.Should().BeTrue();

            bool result5 = graphBuilder.RedirectEdges();
            result5.Should().BeTrue();

            graphBuilder.EdgeIds.Count().Should().Be(9);
            graphBuilder.NodeIds.Count().Should().Be(7);
            graphBuilder.AllDependenciesSatisfied.Should().BeTrue();

            // First activity.
            graphBuilder.EdgeTailNode(activityId1).Id.Should().Be(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId1).Id.Should().NotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.Should().Be(3);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).Should().BeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId3).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Count.Should().Be(3);
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId2).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId3).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(dummyActivityId2).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId1).Should().BeTrue();
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId4).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId1).Id.Should().Be(graphBuilder.EdgeHeadNode(activityId1).Id);
            graphBuilder.EdgeTailNode(dummyActivityId4).Id.Should().Be(graphBuilder.EdgeHeadNode(activityId1).Id);

            // Second activity.
            graphBuilder.EdgeTailNode(activityId2).Id.Should().Be(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId2).Id.Should().NotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.Should().Be(3);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).Should().BeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId3).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count.Should().Be(3);
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId3).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2).Should().BeTrue();
            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(dummyActivityId3).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId2).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId2).Id.Should().Be(graphBuilder.EdgeHeadNode(activityId2).Id);

            // Third activity.
            graphBuilder.EdgeTailNode(activityId3).Id.Should().Be(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId3).Id.Should().NotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.Should().Be(3);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).Should().BeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId3).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Count.Should().Be(3);
            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Contains(activityId2).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Contains(activityId3).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Contains(activityId3).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Contains(dummyActivityId3).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId3).Id.Should().Be(graphBuilder.EdgeHeadNode(activityId3).Id);

            // Fourth activity.
            graphBuilder.EdgeTailNode(activityId4).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(activityId4).IncomingEdges.Contains(dummyActivityId4).Should().BeTrue();

            graphBuilder.EdgeTailNode(activityId4).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(activityId4).OutgoingEdges.Contains(activityId4).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId4).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeHeadNode(activityId4).IncomingEdges.Contains(activityId4).Should().BeTrue();
            graphBuilder.EdgeHeadNode(activityId4).IncomingEdges.Contains(dummyActivityId1).Should().BeTrue();

            // First dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId2).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId4).Should().BeTrue();

            graphBuilder.Edge(dummyActivityId1).Content.IsDummy.Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId1).Should().BeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(activityId4).Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId1).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId7).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId7).Id.Should().Be(graphBuilder.EdgeHeadNode(dummyActivityId1).Id);

            // Second dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Contains(activityId2).Should().BeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId3).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId2).Should().BeTrue();

            graphBuilder.Edge(dummyActivityId2).Content.IsDummy.Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId2).Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId2).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeHeadNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId1).Should().BeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId4).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId1).Id.Should().Be(graphBuilder.EdgeHeadNode(dummyActivityId2).Id);

            // Third dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Contains(activityId3).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Contains(dummyActivityId3).Should().BeTrue();

            graphBuilder.Edge(dummyActivityId2).Content.IsDummy.Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(activityId2).Should().BeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId3).Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId3).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(dummyActivityId3).OutgoingEdges.Contains(dummyActivityId2).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId2).Id.Should().Be(graphBuilder.EdgeHeadNode(dummyActivityId3).Id);

            // Fourth dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId4).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(dummyActivityId4).IncomingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId4).IncomingEdges.Contains(dummyActivityId2).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId4).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(dummyActivityId4).OutgoingEdges.Contains(dummyActivityId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId4).OutgoingEdges.Contains(dummyActivityId4).Should().BeTrue();

            graphBuilder.Edge(dummyActivityId4).Content.IsDummy.Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId4).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(dummyActivityId4).IncomingEdges.Contains(dummyActivityId4).Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId4).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(dummyActivityId4).OutgoingEdges.Contains(activityId4).Should().BeTrue();

            graphBuilder.EdgeTailNode(activityId4).Id.Should().Be(graphBuilder.EdgeHeadNode(dummyActivityId4).Id);

            // Fifth dummy activity.
            graphBuilder.EdgeIds.Contains(dummyActivityId5).Should().BeFalse();

            // Sixth dummy activity.
            graphBuilder.EdgeIds.Contains(dummyActivityId6).Should().BeFalse();

            // Seventh dummy activity.
            graphBuilder.EndNode.IncomingEdges.Count.Should().Be(1);
            graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId7).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId7).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(dummyActivityId7).IncomingEdges.Contains(activityId4).Should().BeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId7).IncomingEdges.Contains(dummyActivityId1).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId7).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(dummyActivityId7).OutgoingEdges.Contains(dummyActivityId7).Should().BeTrue();

            graphBuilder.Edge(dummyActivityId7).Content.IsDummy.Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId7).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(dummyActivityId7).IncomingEdges.Contains(dummyActivityId7).Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId7).Id.Should().Be(graphBuilder.EndNode.Id);
            graphBuilder.EdgeTailNode(dummyActivityId7).Id.Should().NotBe(graphBuilder.StartNode.Id);
        }

        [Fact]
        public void ArrowGraphBuilder_GivenFourActivitiesOneDependentOnOtherThreeGetAncestorNodesLookup_ThenAncestorsAsExpected()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int dummyActivityId = activityId4 + 1;
            int dummyActivityId1 = dummyActivityId + 1;
            int dummyActivityId2 = dummyActivityId1 + 1;
            int dummyActivityId3 = dummyActivityId2 + 1;
            int eventId1 = eventId + 1;
            int eventId2 = eventId1 + 1;
            int eventId3 = eventId2 + 1;
            int eventId4 = eventId3 + 1;
            int eventId5 = eventId4 + 1;
            int eventId6 = eventId5 + 1;
            int eventId7 = eventId6 + 1;
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.Should().BeTrue();

            var activity2 = new Activity<int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.Should().BeTrue();

            var activity3 = new Activity<int, int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3);
            result3.Should().BeTrue();

            var activity4 = new Activity<int, int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int>(new[] { activityId1, activityId2, activityId3 }));
            result4.Should().BeTrue();

            IDictionary<int, HashSet<int>> ancestorNodesLookup = graphBuilder.GetAncestorNodesLookup();

            // Start node (event 1).
            ancestorNodesLookup[eventId1].Count.Should().Be(0);

            // End node (event 2).
            HashSet<int> endNodeAncestors = ancestorNodesLookup[eventId2];
            endNodeAncestors.Count.Should().Be(6);
            endNodeAncestors.Contains(eventId1).Should().BeTrue();
            endNodeAncestors.Contains(eventId3).Should().BeTrue();
            endNodeAncestors.Contains(eventId4).Should().BeTrue();
            endNodeAncestors.Contains(eventId5).Should().BeTrue();
            endNodeAncestors.Contains(eventId6).Should().BeTrue();
            endNodeAncestors.Contains(eventId7).Should().BeTrue();

            // Event 3.
            HashSet<int> event2NodeAncestors = ancestorNodesLookup[eventId3];
            event2NodeAncestors.Count.Should().Be(1);
            event2NodeAncestors.Contains(eventId1).Should().BeTrue();

            // Event 4.
            HashSet<int> event4NodeAncestors = ancestorNodesLookup[eventId4];
            event4NodeAncestors.Count.Should().Be(1);
            event4NodeAncestors.Contains(eventId1).Should().BeTrue();

            // Event 5.
            HashSet<int> event5NodeAncestors = ancestorNodesLookup[eventId5];
            event5NodeAncestors.Count.Should().Be(1);
            event5NodeAncestors.Contains(eventId1).Should().BeTrue();

            // Event 6.
            HashSet<int> event6NodeAncestors = ancestorNodesLookup[eventId6];
            event6NodeAncestors.Count.Should().Be(4);
            event6NodeAncestors.Contains(eventId1).Should().BeTrue();
            event6NodeAncestors.Contains(eventId3).Should().BeTrue();
            event6NodeAncestors.Contains(eventId4).Should().BeTrue();
            event6NodeAncestors.Contains(eventId5).Should().BeTrue();

            // Event 7.
            HashSet<int> event7NodeAncestors = ancestorNodesLookup[eventId7];
            event7NodeAncestors.Count.Should().Be(5);
            event7NodeAncestors.Contains(eventId1).Should().BeTrue();
            event7NodeAncestors.Contains(eventId3).Should().BeTrue();
            event7NodeAncestors.Contains(eventId4).Should().BeTrue();
            event7NodeAncestors.Contains(eventId5).Should().BeTrue();
            event7NodeAncestors.Contains(eventId6).Should().BeTrue();
        }

        [Fact]
        public void ArrowGraphBuilder_GivenThreeActivitiesOneDependentOnOtherTwoWithTwoUnnecessaryDummies_ThenTransitiveReductionAsExpected()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int activityId6 = activityId5 + 1;
            int dummyActivityId = activityId6 + 1;
            int dummyActivityId1 = dummyActivityId + 1;
            int dummyActivityId2 = dummyActivityId1 + 1;
            int dummyActivityId3 = dummyActivityId2 + 1;
            int dummyActivityId4 = dummyActivityId3 + 1;
            int dummyActivityId5 = dummyActivityId4 + 1;
            int dummyActivityId6 = dummyActivityId5 + 1;
            int dummyActivityId7 = dummyActivityId6 + 1;
            int dummyActivityId8 = dummyActivityId7 + 1;
            int dummyActivityId9 = dummyActivityId8 + 1;
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.Should().BeTrue();

            var activity2 = new Activity<int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.Should().BeTrue();

            var activity3 = new Activity<int, int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int>(new[] { activityId1, activityId2, activityId6 }));
            result3.Should().BeTrue();

            var activity4 = new Activity<int, int>(activityId4, 0, canBeRemoved: true);
            bool result4 = graphBuilder.AddActivity(activity4);
            result4.Should().BeTrue();

            var activity5 = new Activity<int, int>(activityId5, 0, canBeRemoved: true);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int>(new[] { activityId1 }));
            result5.Should().BeTrue();

            var activity6 = new Activity<int, int>(activityId6, 0, canBeRemoved: true);
            bool result6 = graphBuilder.AddActivity(activity6);
            result5.Should().BeTrue();

            graphBuilder.EdgeIds.Count().Should().Be(15);
            graphBuilder.NodeIds.Count().Should().Be(10);

            // First activity.
            graphBuilder.EdgeTailNode(activityId1).Id.Should().Be(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId1).Id.Should().NotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.Should().Be(4);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).Should().BeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId4).Should().BeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId6).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Count.Should().Be(4);
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId2).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId4).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId6).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Count.Should().Be(3);
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId1).Should().BeTrue();
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId3).Should().BeTrue();
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId7).Should().BeTrue();

            // Second activity.
            graphBuilder.EdgeTailNode(activityId2).Id.Should().Be(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId2).Id.Should().NotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.Should().Be(4);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).Should().BeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId4).Should().BeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId6).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count.Should().Be(4);
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId4).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId6).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId2).Should().BeTrue();
            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId4).Should().BeTrue();

            // Third activity.
            graphBuilder.EdgeTailNode(activityId3).Id.Should().NotBe(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId3).Id.Should().NotBe(graphBuilder.EndNode.Id);

            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Count.Should().Be(3);
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId3).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId4).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId9).Should().BeTrue();

            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Contains(activityId3).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Contains(activityId3).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Contains(dummyActivityId5).Should().BeTrue();

            // First dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId1).Id.Should().NotBe(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(dummyActivityId1).Id.Should().Be(graphBuilder.EndNode.Id);

            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Contains(activityId1).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Count.Should().Be(3);
            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId3).Should().BeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId7).Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Count.Should().Be(5);
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId1).Should().BeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId2).Should().BeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId5).Should().BeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId6).Should().BeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId8).Should().BeTrue();

            // Second dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId2).Id.Should().NotBe(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(dummyActivityId2).Id.Should().Be(graphBuilder.EndNode.Id);

            graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Contains(activityId2).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId2).Should().BeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId4).Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Count.Should().Be(5);
            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId1).Should().BeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId2).Should().BeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId5).Should().BeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId6).Should().BeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId8).Should().BeTrue();

            // Third dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId3).Id.Should().NotBe(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(dummyActivityId3).Id.Should().NotBe(graphBuilder.EndNode.Id);

            graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Contains(activityId1).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Count.Should().Be(3);
            graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Contains(dummyActivityId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Contains(dummyActivityId3).Should().BeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Contains(dummyActivityId7).Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Count.Should().Be(3);
            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId3).Should().BeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId4).Should().BeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId9).Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId3).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(dummyActivityId3).OutgoingEdges.Contains(activityId3).Should().BeTrue();

            // Transitive Reduction.
            bool result10 = graphBuilder.TransitiveReduction();
            result10.Should().BeTrue();

            graphBuilder.EdgeIds.Count().Should().Be(13);
            graphBuilder.NodeIds.Count().Should().Be(10);
            graphBuilder.AllDependenciesSatisfied.Should().BeTrue();

            // First activity.
            graphBuilder.EdgeTailNode(activityId1).Id.Should().Be(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId1).Id.Should().NotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.Should().Be(4);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).Should().BeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId4).Should().BeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId6).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Count.Should().Be(4);
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId2).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId4).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId6).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId3).Should().BeTrue();
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId7).Should().BeTrue();

            // Second activity.
            graphBuilder.EdgeTailNode(activityId2).Id.Should().Be(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId2).Id.Should().NotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.Should().Be(4);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).Should().BeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId4).Should().BeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId6).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count.Should().Be(4);
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId4).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId6).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId4).Should().BeTrue();

            // Third activity.
            graphBuilder.EdgeTailNode(activityId3).Id.Should().NotBe(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId3).Id.Should().NotBe(graphBuilder.EndNode.Id);

            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Count.Should().Be(3);
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId3).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId4).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId9).Should().BeTrue();

            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Contains(activityId3).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Contains(activityId3).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Contains(dummyActivityId5).Should().BeTrue();

            // First dummy activity.
            graphBuilder.EdgeIds.Contains(dummyActivityId1).Should().BeFalse();

            // Second dummy activity.
            graphBuilder.EdgeIds.Contains(dummyActivityId2).Should().BeFalse();

            // Third dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId3).Id.Should().NotBe(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(dummyActivityId3).Id.Should().NotBe(graphBuilder.EndNode.Id);

            graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Contains(activityId1).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Contains(dummyActivityId3).Should().BeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Contains(dummyActivityId7).Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Count.Should().Be(3);
            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId3).Should().BeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId4).Should().BeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId9).Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId3).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(dummyActivityId3).OutgoingEdges.Contains(activityId3).Should().BeTrue();
        }

        [Fact]
        public void ArrowGraphBuilder_GivenFiveActivitiesWithThreeUnnecessaryDummies_ThenRemoveRedundantDummyEdgesAsExpected()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int dummyActivityId = activityId5 + 1;
            int dummyActivityId1 = dummyActivityId + 1;
            int dummyActivityId2 = dummyActivityId1 + 1;
            int dummyActivityId3 = dummyActivityId2 + 1;
            int dummyActivityId4 = dummyActivityId3 + 1;
            int dummyActivityId5 = dummyActivityId4 + 1;
            int dummyActivityId6 = dummyActivityId5 + 1;
            int dummyActivityId7 = dummyActivityId6 + 1;
            int dummyActivityId8 = dummyActivityId7 + 1;
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.Should().BeTrue();

            var activity2 = new Activity<int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.Should().BeTrue();

            var activity3 = new Activity<int, int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int>(new[] { activityId1, activityId4 }));
            result3.Should().BeTrue();

            var activity4 = new Activity<int, int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int>(new[] { activityId2 }));
            result4.Should().BeTrue();

            var activity5 = new Activity<int, int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int>(new[] { activityId1 }));
            result5.Should().BeTrue();

            graphBuilder.EdgeIds.Count().Should().Be(13);
            graphBuilder.NodeIds.Count().Should().Be(10);

            // RemoveRedundantEdges.
            bool result6 = graphBuilder.RemoveRedundantEdges();
            result6.Should().BeTrue();

            graphBuilder.EdgeIds.Count().Should().Be(9);
            graphBuilder.NodeIds.Count().Should().Be(6);
            graphBuilder.AllDependenciesSatisfied.Should().BeTrue();

            // First activity.
            graphBuilder.EdgeTailNode(activityId1).Id.Should().Be(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId1).Id.Should().NotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.Should().Be(2);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId1).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Count.Should().Be(3);

            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId1).Should().BeTrue();
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId3).Should().BeTrue();
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(activityId5).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId1).Id.Should().Be(graphBuilder.EdgeHeadNode(activityId1).Id);

            // Second activity.
            graphBuilder.EdgeTailNode(activityId2).Id.Should().Be(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId2).Id.Should().NotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.Should().Be(2);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(activityId4).Should().BeTrue();
            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId2).Should().BeTrue();

            graphBuilder.EdgeTailNode(activityId4).Id.Should().Be(graphBuilder.EdgeHeadNode(activityId2).Id);

            // Fourth activity.
            graphBuilder.EdgeTailNode(activityId4).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(activityId4).IncomingEdges.Contains(activityId2).Should().BeTrue();

            graphBuilder.EdgeTailNode(activityId4).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(activityId4).OutgoingEdges.Contains(activityId4).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId4).OutgoingEdges.Contains(dummyActivityId2).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId4).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeHeadNode(activityId4).IncomingEdges.Contains(activityId4).Should().BeTrue();
            graphBuilder.EdgeHeadNode(activityId4).IncomingEdges.Contains(dummyActivityId3).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId4).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId4).OutgoingEdges.Contains(activityId3).Should().BeTrue();

            graphBuilder.EdgeTailNode(activityId3).Id.Should().Be(graphBuilder.EdgeHeadNode(activityId4).Id);

            // First dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Contains(activityId1).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Count.Should().Be(3);
            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId3).Should().BeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(activityId5).Should().BeTrue();

            graphBuilder.Edge(dummyActivityId1).Content.IsDummy.Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Count.Should().Be(4);
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId1).Should().BeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId2).Should().BeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId8).Should().BeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(activityId3).Should().BeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId1).Id.Should().Be(graphBuilder.EndNode.Id);
            graphBuilder.EdgeTailNode(dummyActivityId1).Id.Should().NotBe(graphBuilder.StartNode.Id);

            // Third activity.
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId3).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(activityId4).Should().BeTrue();

            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Contains(activityId3).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Count.Should().Be(4);
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId1).Should().BeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId2).Should().BeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId8).Should().BeTrue();
            graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Contains(activityId3).Should().BeTrue();
            graphBuilder.EndNode.IncomingEdges.Count.Should().Be(4);
            graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId1).Should().BeTrue();
            graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId2).Should().BeTrue();
            graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId8).Should().BeTrue();
            graphBuilder.EndNode.IncomingEdges.Contains(activityId3).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId3).Id.Should().Be(graphBuilder.EndNode.Id);
            graphBuilder.EdgeTailNode(activityId3).Id.Should().NotBe(graphBuilder.StartNode.Id);

            // Fifth activity.
            graphBuilder.EdgeTailNode(activityId5).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(activityId5).IncomingEdges.Contains(activityId1).Should().BeTrue();

            graphBuilder.EdgeTailNode(activityId5).OutgoingEdges.Count.Should().Be(3);
            graphBuilder.EdgeTailNode(activityId5).OutgoingEdges.Contains(activityId5).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId5).OutgoingEdges.Contains(dummyActivityId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(activityId5).OutgoingEdges.Contains(dummyActivityId3).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId5).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId5).IncomingEdges.Contains(activityId5).Should().BeTrue();

            graphBuilder.EdgeHeadNode(activityId5).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(activityId5).OutgoingEdges.Contains(dummyActivityId8).Should().BeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId8).Id.Should().Be(graphBuilder.EdgeHeadNode(activityId5).Id);
        }

        [Fact]
        public void ArrowGraphBuilder_GivenCtorCalledWithNullArrowGraph_ThenShouldThrowArgumentNullException()
        {
            int eventId = 0;
            int activityId1 = 1;
            Action act = () => new ArrowGraphBuilder<int, int, IActivity<int, int>>(null, () => activityId1++, () => eventId = eventId.Next());
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void ArrowGraphBuilder_GivenCtorCalledWithGraph_ThenGraphSuccessfullyAssimilated()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int dummyActivityId = activityId5 + 1;
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.Should().BeTrue();

            var activity2 = new Activity<int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.Should().BeTrue();

            var activity3 = new Activity<int, int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int>(new[] { activityId1, activityId4 }));
            result3.Should().BeTrue();

            var activity4 = new Activity<int, int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int>(new[] { activityId2 }));
            result4.Should().BeTrue();

            var activity5 = new Activity<int, int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int>(new[] { activityId1 }));
            result5.Should().BeTrue();

            var firstGraph = graphBuilder.ToGraph();

            var graphBuilder2 = new ArrowGraphBuilder<int, int, IActivity<int, int>>(firstGraph, () => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            var secondGraph = graphBuilder2.ToGraph();
            secondGraph.Should().Be(firstGraph);
        }

        [Fact]
        public void ArrowGraphBuilder_GivenCtorCalledWithGraphWithMissingEdge_ThenShouldThrowArgumentException()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int dummyActivityId = activityId5 + 1;
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.Should().BeTrue();

            var activity2 = new Activity<int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.Should().BeTrue();

            var activity3 = new Activity<int, int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int>(new[] { activityId1, activityId4 }));
            result3.Should().BeTrue();

            var activity4 = new Activity<int, int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int>(new[] { activityId2 }));
            result4.Should().BeTrue();

            var activity5 = new Activity<int, int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int>(new[] { activityId1 }));
            result5.Should().BeTrue();

            var graph = graphBuilder.ToGraph();
            graph.Edges.RemoveAt(0);

            Action act = () => new ArrowGraphBuilder<int, int, IActivity<int, int>>(graph, () => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void ArrowGraphBuilder_GivenCtorCalledWithGraphWithTooManyEdges_ThenShouldThrowArgumentException()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int dummyActivityId = activityId5 + 1;
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.Should().BeTrue();

            var activity2 = new Activity<int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.Should().BeTrue();

            var activity3 = new Activity<int, int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int>(new[] { activityId1, activityId4 }));
            result3.Should().BeTrue();

            var activity4 = new Activity<int, int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int>(new[] { activityId2 }));
            result4.Should().BeTrue();

            var activity5 = new Activity<int, int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int>(new[] { activityId1 }));
            result5.Should().BeTrue();

            var graph = graphBuilder.ToGraph();
            graph.Edges.Add(new Edge<int, IActivity<int, int>>(new Activity<int, int>(dummyActivityId = dummyActivityId.Next(), 0)));

            Action act = () => new ArrowGraphBuilder<int, int, IActivity<int, int>>(graph, () => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void ArrowGraphBuilder_GivenCtorCalledWithGraphWithMissingNode_ThenShouldThrowArgumentException()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int dummyActivityId = activityId5 + 1;
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.Should().BeTrue();

            var activity2 = new Activity<int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.Should().BeTrue();

            var activity3 = new Activity<int, int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int>(new[] { activityId1, activityId4 }));
            result3.Should().BeTrue();

            var activity4 = new Activity<int, int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int>(new[] { activityId2 }));
            result4.Should().BeTrue();

            var activity5 = new Activity<int, int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int>(new[] { activityId1 }));
            result5.Should().BeTrue();

            var graph = graphBuilder.ToGraph();
            Node<int, IEvent<int>> node = graph.Nodes.First(x => x.NodeType == NodeType.Normal);
            graph.Nodes.Remove(node);

            Action act = () => new ArrowGraphBuilder<int, int, IActivity<int, int>>(graph, () => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void ArrowGraphBuilder_GivenCtorCalledWithGraphWithTooManyNodes_ThenShouldThrowArgumentException()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int dummyActivityId = activityId5 + 1;
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.Should().BeTrue();

            var activity2 = new Activity<int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.Should().BeTrue();

            var activity3 = new Activity<int, int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int>(new[] { activityId1, activityId4 }));
            result3.Should().BeTrue();

            var activity4 = new Activity<int, int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int>(new[] { activityId2 }));
            result4.Should().BeTrue();

            var activity5 = new Activity<int, int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int>(new[] { activityId1 }));
            result5.Should().BeTrue();

            var graph = graphBuilder.ToGraph();
            graph.Nodes.Add(new Node<int, IEvent<int>>(new Event<int>(dummyActivityId = dummyActivityId.Next())));

            Action act = () => new ArrowGraphBuilder<int, int, IActivity<int, int>>(graph, () => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void ArrowGraphBuilder_GivenCtorCalledWithGraphWithNoStartNode_ThenShouldThrowArgumentException()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int dummyActivityId = activityId5 + 1;
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.Should().BeTrue();

            var activity2 = new Activity<int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.Should().BeTrue();

            var activity3 = new Activity<int, int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int>(new[] { activityId1, activityId4 }));
            result3.Should().BeTrue();

            var activity4 = new Activity<int, int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int>(new[] { activityId2 }));
            result4.Should().BeTrue();

            var activity5 = new Activity<int, int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int>(new[] { activityId1 }));
            result5.Should().BeTrue();

            var graph = graphBuilder.ToGraph();
            Node<int, IEvent<int>> node = graph.Nodes.First(x => x.NodeType == NodeType.Start);
            node.SetNodeType(NodeType.Normal);

            Action act = () => new ArrowGraphBuilder<int, int, IActivity<int, int>>(graph, () => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void ArrowGraphBuilder_GivenCtorCalledWithGraphWithNoEndNode_ThenShouldThrowArgumentException()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int dummyActivityId = activityId5 + 1;
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.Should().BeTrue();

            var activity2 = new Activity<int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.Should().BeTrue();

            var activity3 = new Activity<int, int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int>(new[] { activityId1, activityId4 }));
            result3.Should().BeTrue();

            var activity4 = new Activity<int, int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int>(new[] { activityId2 }));
            result4.Should().BeTrue();

            var activity5 = new Activity<int, int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int>(new[] { activityId1 }));
            result5.Should().BeTrue();

            var graph = graphBuilder.ToGraph();
            Node<int, IEvent<int>> node = graph.Nodes.First(x => x.NodeType == NodeType.End);
            node.SetNodeType(NodeType.Normal);

            Action act = () => new ArrowGraphBuilder<int, int, IActivity<int, int>>(graph, () => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void ArrowGraphBuilder_GivenCtorCalledWithGraphWithMoreThanOneStartNode_ThenShouldThrowArgumentException()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int dummyActivityId = activityId5 + 1;
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.Should().BeTrue();

            var activity2 = new Activity<int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.Should().BeTrue();

            var activity3 = new Activity<int, int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int>(new[] { activityId1, activityId4 }));
            result3.Should().BeTrue();

            var activity4 = new Activity<int, int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int>(new[] { activityId2 }));
            result4.Should().BeTrue();

            var activity5 = new Activity<int, int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int>(new[] { activityId1 }));
            result5.Should().BeTrue();

            var graph = graphBuilder.ToGraph();

            Node<int, IEvent<int>> node = graph.Nodes.First(x => x.NodeType == NodeType.Normal);
            graph.Nodes.Remove(node);

            var newNode = new Node<int, IEvent<int>>(NodeType.Start, node.Content);
            foreach (int edgeId in node.OutgoingEdges)
            {
                newNode.OutgoingEdges.Add(edgeId);
            }
            graph.Nodes.Add(newNode);

            Action act = () => new ArrowGraphBuilder<int, int, IActivity<int, int>>(graph, () => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void ArrowGraphBuilder_GivenCtorCalledWithGraphWithMoreThanOneEndNode_ThenShouldThrowArgumentException()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int dummyActivityId = activityId5 + 1;
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.Should().BeTrue();

            var activity2 = new Activity<int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.Should().BeTrue();

            var activity3 = new Activity<int, int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int>(new[] { activityId1, activityId4 }));
            result3.Should().BeTrue();

            var activity4 = new Activity<int, int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int>(new[] { activityId2 }));
            result4.Should().BeTrue();

            var activity5 = new Activity<int, int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int>(new[] { activityId1 }));
            result5.Should().BeTrue();

            var graph = graphBuilder.ToGraph();

            Node<int, IEvent<int>> node = graph.Nodes.First(x => x.NodeType == NodeType.Normal);
            graph.Nodes.Remove(node);

            var newNode = new Node<int, IEvent<int>>(NodeType.End, node.Content);
            foreach (int edgeId in node.IncomingEdges)
            {
                newNode.IncomingEdges.Add(edgeId);
            }
            graph.Nodes.Add(newNode);

            Action act = () => new ArrowGraphBuilder<int, int, IActivity<int, int>>(graph, () => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void ArrowGraphBuilder_GivenAllDummyActivitiesFindCircularDependencies_ThenFindsCircularDependency()
        {
            int eventId = 0;
            int dummyActivityId = 100;
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            graphBuilder.AddActivity(new Activity<int, int>(1, 0));
            graphBuilder.AddActivity(new Activity<int, int>(2, 0), new HashSet<int>(new[] { 7 }));
            graphBuilder.AddActivity(new Activity<int, int>(3, 0));
            graphBuilder.AddActivity(new Activity<int, int>(4, 0), new HashSet<int>(new[] { 2 }));
            graphBuilder.AddActivity(new Activity<int, int>(5, 0), new HashSet<int>(new[] { 1, 2, 3, 8 }));
            graphBuilder.AddActivity(new Activity<int, int>(6, 0), new HashSet<int>(new[] { 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(7, 0), new HashSet<int>(new[] { 4 }));
            graphBuilder.AddActivity(new Activity<int, int>(8, 0), new HashSet<int>(new[] { 9, 6 }));
            graphBuilder.AddActivity(new Activity<int, int>(9, 0), new HashSet<int>(new[] { 5 }));
            IList<ICircularDependency<int>> circularDependencies = graphBuilder.FindStrongCircularDependencies();

            circularDependencies.Count.Should().Be(2);
            circularDependencies[0].Dependencies.Should().BeEquivalentTo(new int[] { 2, 4, 7 });
            circularDependencies[1].Dependencies.Should().BeEquivalentTo(new int[] { 5, 8, 9 });
        }

        [Fact]
        public void ArrowGraphBuilder_GivenFindCircularDependencies_ThenFindsCircularDependency()
        {
            int eventId = 0;
            int dummyActivityId = 100;
            var graphBuilder = new ArrowGraphBuilder<int, int, IActivity<int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            graphBuilder.AddActivity(new Activity<int, int>(1, 10));
            graphBuilder.AddActivity(new Activity<int, int>(2, 10), new HashSet<int>(new[] { 7 }));
            graphBuilder.AddActivity(new Activity<int, int>(3, 10));
            graphBuilder.AddActivity(new Activity<int, int>(4, 10), new HashSet<int>(new[] { 2 }));
            graphBuilder.AddActivity(new Activity<int, int>(5, 10), new HashSet<int>(new[] { 1, 2, 3, 8 }));
            graphBuilder.AddActivity(new Activity<int, int>(6, 10), new HashSet<int>(new[] { 3 }));
            graphBuilder.AddActivity(new Activity<int, int>(7, 10), new HashSet<int>(new[] { 4 }));
            graphBuilder.AddActivity(new Activity<int, int>(8, 10), new HashSet<int>(new[] { 9, 6 }));
            graphBuilder.AddActivity(new Activity<int, int>(9, 10), new HashSet<int>(new[] { 5 }));
            IList<ICircularDependency<int>> circularDependencies = graphBuilder.FindStrongCircularDependencies();

            circularDependencies.Count.Should().Be(2);
            circularDependencies[0].Dependencies.Should().BeEquivalentTo(new int[] { 2, 4, 7 });
            circularDependencies[1].Dependencies.Should().BeEquivalentTo(new int[] { 5, 8, 9 });
        }
    }
}
