using Shouldly;
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
            var graphBuilder = new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());

            graphBuilder.EdgeIds.Any().ShouldBeFalse();
            graphBuilder.NodeIds.Count().ShouldBe(2);
            graphBuilder.AllDependenciesSatisfied.ShouldBeTrue();
            graphBuilder.StartNode.OutgoingEdges.Count.ShouldBe(0);
            graphBuilder.EndNode.IncomingEdges.Count.ShouldBe(0);
        }

        [Fact]
        public void ArrowGraphBuilder_GivenGivenCtorCalledWithNullEdgeIdGenerator_ThenThenShouldThrowArgumentNullException()
        {
            int eventId = 0;
            Action act = () => new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(null, () => eventId = eventId.Next());
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void ArrowGraphBuilder_GivenGivenCtorCalledWithNullNodeIdGenerator_ThenThenShouldThrowArgumentNullException()
        {
            int dummyActivityId = 0;
            Action act = () => new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(() => dummyActivityId = dummyActivityId.Next(), null);
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void ArrowGraphBuilder_GivenGivenAccessOutgoingEdgesOfEndNode_ThenThenShouldThrowInvalidOperationException()
        {
            int eventId = 0;
            int dummyActivityId = 0;
            var graphBuilder = new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            Action act = () => graphBuilder.EndNode.OutgoingEdges.Any();
            act.ShouldThrow<InvalidOperationException>();
        }

        [Fact]
        public void ArrowGraphBuilder_GivenSingleActivityNoDependencies_ThenHooksUpToStartAndEndNodes()
        {
            int eventId = 0;
            int activityId = 1;
            int dummyActivityId = activityId;
            var graphBuilder = new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            int dummyActivityId1 = dummyActivityId + 1;

            var activity = new Activity<int, int, int>(activityId, 0);
            bool result = graphBuilder.AddActivity(activity);
            result.ShouldBeTrue();

            graphBuilder.EdgeIds.Count().ShouldBe(2);
            graphBuilder.NodeIds.Count().ShouldBe(3);
            graphBuilder.AllDependenciesSatisfied.ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId).Id.ShouldBe(graphBuilder.StartNode.Id);
            graphBuilder.EdgeTailNode(dummyActivityId1).Id.ShouldBe(graphBuilder.EdgeHeadNode(activityId).Id);
            graphBuilder.EdgeHeadNode(dummyActivityId1).Id.ShouldBe(graphBuilder.EndNode.Id);
            graphBuilder.StartNode.OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId).ShouldBeTrue();
            graphBuilder.EndNode.IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId1).ShouldBeTrue();
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
            var graphBuilder = new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.ShouldBeTrue();

            graphBuilder.EdgeIds.Count().ShouldBe(2);
            graphBuilder.NodeIds.Count().ShouldBe(3);
            graphBuilder.AllDependenciesSatisfied.ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId1).Id.ShouldBe(graphBuilder.StartNode.Id);
            graphBuilder.EdgeTailNode(dummyActivityId1).Id.ShouldBe(graphBuilder.EdgeHeadNode(activityId1).Id);
            graphBuilder.EdgeHeadNode(dummyActivityId1).Id.ShouldBe(graphBuilder.EndNode.Id);
            graphBuilder.StartNode.OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.EndNode.IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId1).ShouldBeTrue();

            var activity2 = new Activity<int, int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2, new HashSet<int> { activityId1 });
            result2.ShouldBeTrue();

            graphBuilder.EdgeIds.Count().ShouldBe(5);
            graphBuilder.NodeIds.Count().ShouldBe(5);
            graphBuilder.AllDependenciesSatisfied.ShouldBeTrue();

            // First activity.
            graphBuilder.EdgeTailNode(activityId1).Id.ShouldBe(graphBuilder.StartNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId1).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId1).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId2).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId1).Id.ShouldBe(graphBuilder.EdgeHeadNode(activityId1).Id);
            graphBuilder.EdgeTailNode(dummyActivityId2).Id.ShouldBe(graphBuilder.EdgeHeadNode(activityId1).Id);

            // Dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Contains(activityId1).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId2).ShouldBeTrue();

            graphBuilder.Edge(dummyActivityId1).Content.IsDummy.ShouldBeTrue();
            graphBuilder.Edge(dummyActivityId2).Content.IsDummy.ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId1).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId3).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId1).Id.ShouldBe(graphBuilder.EndNode.Id);

            // Second activity.
            graphBuilder.EdgeTailNode(activityId2).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(activityId2).IncomingEdges.Contains(dummyActivityId2).ShouldBeTrue();

            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId3).ShouldBeTrue();

            // Dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Contains(activityId2).ShouldBeTrue();

            graphBuilder.Edge(dummyActivityId3).Content.IsDummy.ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId1).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId3).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId3).Id.ShouldBe(graphBuilder.EndNode.Id);

            graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId2).ShouldBeTrue();

            graphBuilder.Edge(dummyActivityId2).Content.IsDummy.ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId2).ShouldBeTrue();
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
            var graphBuilder = new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            var activity2 = new Activity<int, int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2, new HashSet<int> { activityId1 });
            result2.ShouldBeTrue();

            graphBuilder.EdgeIds.Count().ShouldBe(2);
            graphBuilder.NodeIds.Count().ShouldBe(4);
            graphBuilder.AllDependenciesSatisfied.ShouldBeFalse();
            graphBuilder.EdgeTailNode(activityId2).Id.ShouldNotBe(graphBuilder.StartNode.Id);
            graphBuilder.StartNode.OutgoingEdges.Count.ShouldBe(0);

            graphBuilder.EdgeTailNode(activityId2).IncomingEdges.Count.ShouldBe(0);

            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2).ShouldBeTrue();

            var activity1 = new Activity<int, int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.ShouldBeTrue();

            graphBuilder.EdgeIds.Count().ShouldBe(4);
            graphBuilder.NodeIds.Count().ShouldBe(5);
            graphBuilder.AllDependenciesSatisfied.ShouldBeTrue();

            // First activity.
            graphBuilder.EdgeTailNode(activityId1).Id.ShouldBe(graphBuilder.StartNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId1).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId2).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId2).Id.ShouldBe(graphBuilder.EdgeHeadNode(activityId1).Id);

            // Dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Contains(activityId2).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId1).ShouldBeTrue();

            graphBuilder.Edge(dummyActivityId1).Content.IsDummy.ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId1).ShouldBeTrue();

            graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId1).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId2).Id.ShouldBe(graphBuilder.EdgeTailNode(dummyActivityId1).Id);

            // Second activity.
            graphBuilder.EdgeTailNode(activityId2).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(activityId2).IncomingEdges.Contains(dummyActivityId2).ShouldBeTrue();

            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2).ShouldBeTrue();

            // Dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Contains(activityId2).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId1).ShouldBeTrue();

            graphBuilder.Edge(dummyActivityId1).Content.IsDummy.ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId1).ShouldBeTrue();

            graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId1).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId2).Id.ShouldBe(graphBuilder.EdgeTailNode(dummyActivityId1).Id);
            graphBuilder.EndNode.IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId1).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId1).Id.ShouldBe(graphBuilder.EndNode.Id);
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
            var graphBuilder = new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.ShouldBeTrue();

            graphBuilder.EdgeIds.Count().ShouldBe(2);
            graphBuilder.NodeIds.Count().ShouldBe(3);
            graphBuilder.AllDependenciesSatisfied.ShouldBeTrue();

            // First activity.
            graphBuilder.EdgeTailNode(activityId1).Id.ShouldBe(graphBuilder.StartNode.Id);
            graphBuilder.StartNode.OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId1).Id.ShouldBe(graphBuilder.EdgeHeadNode(activityId1).Id);

            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId1).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId1).Id.ShouldBe(graphBuilder.EndNode.Id);
            graphBuilder.EndNode.IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId1).ShouldBeTrue();

            var activity2 = new Activity<int, int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.ShouldBeTrue();

            graphBuilder.EdgeIds.Count().ShouldBe(4);
            graphBuilder.NodeIds.Count().ShouldBe(4);
            graphBuilder.AllDependenciesSatisfied.ShouldBeTrue();

            // Second activity.
            graphBuilder.EdgeTailNode(activityId2).Id.ShouldBe(graphBuilder.StartNode.Id);
            graphBuilder.StartNode.OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId2).Id.ShouldBe(graphBuilder.EdgeHeadNode(activityId2).Id);

            graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId2).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId2).Id.ShouldBe(graphBuilder.EndNode.Id);
            graphBuilder.EndNode.IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId2).ShouldBeTrue();

            // Dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Contains(activityId1).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId1).ShouldBeTrue();

            graphBuilder.Edge(dummyActivityId1).Content.IsDummy.ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId1).ShouldBeTrue();

            graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId1).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId1).Id.ShouldBe(graphBuilder.EdgeTailNode(dummyActivityId1).Id);

            var activity3 = new Activity<int, int, int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int> { activityId1, activityId2 });
            result3.ShouldBeTrue();

            graphBuilder.EdgeIds.Count().ShouldBe(8);
            graphBuilder.NodeIds.Count().ShouldBe(6);
            graphBuilder.AllDependenciesSatisfied.ShouldBeTrue();

            // First activity.
            graphBuilder.EdgeTailNode(activityId1).Id.ShouldBe(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId1).Id.ShouldNotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId1).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId1).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId3).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId1).Id.ShouldBe(graphBuilder.EdgeHeadNode(activityId1).Id);

            // Second activity.
            graphBuilder.EdgeTailNode(activityId2).Id.ShouldBe(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId2).Id.ShouldNotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId2).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId4).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId2).Id.ShouldBe(graphBuilder.EdgeHeadNode(activityId2).Id);

            // First dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Contains(activityId1).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Contains(dummyActivityId1).ShouldBeTrue();

            graphBuilder.Edge(dummyActivityId1).Content.IsDummy.ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Count.ShouldBe(3);
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId1).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId2).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId5).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId1).Id.ShouldBe(graphBuilder.EndNode.Id);

            // Second dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Contains(activityId2).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId2).ShouldBeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId4).OutgoingEdges.Contains(dummyActivityId2).ShouldBeTrue();

            graphBuilder.Edge(dummyActivityId2).Content.IsDummy.ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Count.ShouldBe(3);
            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId1).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId2).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId5).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId2).Id.ShouldBe(graphBuilder.EndNode.Id);

            // Third dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Contains(activityId1).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Contains(dummyActivityId1).ShouldBeTrue();

            graphBuilder.Edge(dummyActivityId3).Content.IsDummy.ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId3).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId4).ShouldBeTrue();

            // Forth dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId4).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(dummyActivityId4).IncomingEdges.Contains(activityId2).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId4).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId2).ShouldBeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId4).OutgoingEdges.Contains(dummyActivityId2).ShouldBeTrue();

            graphBuilder.Edge(dummyActivityId4).Content.IsDummy.ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId4).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeHeadNode(dummyActivityId4).IncomingEdges.Contains(dummyActivityId3).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId4).IncomingEdges.Contains(dummyActivityId4).ShouldBeTrue();

            // Fifth dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId5).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(dummyActivityId5).IncomingEdges.Contains(activityId3).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId5).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(dummyActivityId5).OutgoingEdges.Contains(dummyActivityId5).ShouldBeTrue();

            graphBuilder.Edge(dummyActivityId5).Content.IsDummy.ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId5).IncomingEdges.Count.ShouldBe(3);
            graphBuilder.EdgeHeadNode(dummyActivityId5).IncomingEdges.Contains(dummyActivityId1).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId5).IncomingEdges.Contains(dummyActivityId2).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId5).IncomingEdges.Contains(dummyActivityId5).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId5).Id.ShouldBe(graphBuilder.EndNode.Id);

            // Third activity.
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId3).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId4).ShouldBeTrue();

            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Contains(activityId3).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Contains(activityId3).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Contains(dummyActivityId5).ShouldBeTrue();
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
            var graphBuilder = new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            var activity3 = new Activity<int, int, int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int> { activityId1, activityId2 });
            result3.ShouldBeTrue();

            graphBuilder.EdgeIds.Count().ShouldBe(2);
            graphBuilder.NodeIds.Count().ShouldBe(4);
            graphBuilder.AllDependenciesSatisfied.ShouldBeFalse();
            graphBuilder.EdgeTailNode(activityId3).Id.ShouldNotBe(graphBuilder.StartNode.Id);
            graphBuilder.StartNode.OutgoingEdges.Count.ShouldBe(0);

            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Count.ShouldBe(0);
            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Contains(activityId3).ShouldBeTrue();

            var activity2 = new Activity<int, int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.ShouldBeTrue();

            graphBuilder.EdgeIds.Count().ShouldBe(4);
            graphBuilder.NodeIds.Count().ShouldBe(5);
            graphBuilder.AllDependenciesSatisfied.ShouldBeFalse();
            graphBuilder.EdgeTailNode(activityId3).Id.ShouldNotBe(graphBuilder.StartNode.Id);

            // Second activity.
            graphBuilder.EdgeTailNode(activityId2).Id.ShouldBe(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId2).Id.ShouldNotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId2).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId2).Id.ShouldBe(graphBuilder.EdgeHeadNode(activityId2).Id);

            // Second dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Contains(activityId2).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId2).ShouldBeTrue();

            graphBuilder.Edge(dummyActivityId2).Content.IsDummy.ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId2).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId2).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(dummyActivityId2).OutgoingEdges.Contains(activityId3).ShouldBeTrue();

            graphBuilder.EdgeTailNode(activityId3).Id.ShouldBe(graphBuilder.EdgeHeadNode(dummyActivityId2).Id);

            // Third activity.
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId2).ShouldBeTrue();

            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Contains(activityId3).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Contains(activityId3).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId3).Id.ShouldNotBe(graphBuilder.EndNode.Id);
            graphBuilder.EdgeTailNode(activityId3).Id.ShouldNotBe(graphBuilder.StartNode.Id);

            var activity1 = new Activity<int, int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.ShouldBeTrue();

            graphBuilder.EdgeIds.Count().ShouldBe(6);
            graphBuilder.NodeIds.Count().ShouldBe(6);
            graphBuilder.AllDependenciesSatisfied.ShouldBeTrue();

            // First activity.
            graphBuilder.EdgeTailNode(activityId1).Id.ShouldBe(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId1).Id.ShouldNotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId2).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId3).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId3).Id.ShouldBe(graphBuilder.EdgeHeadNode(activityId1).Id);

            // Second activity.
            graphBuilder.EdgeTailNode(activityId2).Id.ShouldBe(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId2).Id.ShouldNotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId2).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId2).Id.ShouldBe(graphBuilder.EdgeHeadNode(activityId2).Id);

            // First dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Contains(activityId3).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId1).ShouldBeTrue();

            graphBuilder.Edge(dummyActivityId1).Content.IsDummy.ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId1).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId1).Id.ShouldBe(graphBuilder.EndNode.Id);

            graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId1).ShouldBeTrue();

            // Second dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Contains(activityId2).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId2).ShouldBeTrue();

            graphBuilder.Edge(dummyActivityId2).Content.IsDummy.ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId2).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId3).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId2).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(dummyActivityId2).OutgoingEdges.Contains(activityId3).ShouldBeTrue();

            graphBuilder.EdgeTailNode(activityId3).Id.ShouldBe(graphBuilder.EdgeHeadNode(dummyActivityId2).Id);

            // Third dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Contains(activityId1).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Contains(dummyActivityId3).ShouldBeTrue();

            graphBuilder.Edge(dummyActivityId3).Content.IsDummy.ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId2).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId3).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId3).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(dummyActivityId3).OutgoingEdges.Contains(activityId3).ShouldBeTrue();

            graphBuilder.EdgeTailNode(activityId3).Id.ShouldBe(graphBuilder.EdgeHeadNode(dummyActivityId3).Id);

            // Third activity.
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId2).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId3).ShouldBeTrue();

            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Contains(activityId3).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Contains(activityId3).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Contains(dummyActivityId1).ShouldBeTrue();
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
            var graphBuilder = new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int, int>(activityId1, 0, canBeRemoved: true);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.ShouldBeTrue();

            var activity2 = new Activity<int, int, int>(activityId2, 0, canBeRemoved: true);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.ShouldBeTrue();

            var activity3 = new Activity<int, int, int>(activityId3, 0, canBeRemoved: true);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int> { activityId1, activityId2 });
            result3.ShouldBeTrue();

            graphBuilder.EdgeIds.Count().ShouldBe(8);
            graphBuilder.NodeIds.Count().ShouldBe(6);
            graphBuilder.AllDependenciesSatisfied.ShouldBeTrue();

            bool result4 = graphBuilder.RemoveDummyActivity(dummyActivityId1);
            result4.ShouldBeTrue();

            bool result5 = graphBuilder.RemoveDummyActivity(dummyActivityId2);
            result5.ShouldBeTrue();

            bool result6 = graphBuilder.RemoveDummyActivity(dummyActivityId3);
            result6.ShouldBeTrue();

            graphBuilder.EdgeIds.Count().ShouldBe(5);
            graphBuilder.NodeIds.Count().ShouldBe(5);
            graphBuilder.AllDependenciesSatisfied.ShouldBeTrue();

            // First activity.
            graphBuilder.EdgeTailNode(activityId1).Id.ShouldBe(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId1).Id.ShouldNotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).ShouldBeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId2).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId1).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(dummyActivityId4).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(activityId3).ShouldBeTrue();

            // Second activity.
            graphBuilder.EdgeTailNode(activityId2).Id.ShouldBe(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId2).Id.ShouldNotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).ShouldBeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId1).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId4).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId4).Id.ShouldBe(graphBuilder.EdgeHeadNode(activityId2).Id);

            // Third activity.
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId4).ShouldBeTrue();

            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Contains(activityId3).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Contains(activityId3).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Contains(dummyActivityId5).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId3).Id.ShouldNotBe(graphBuilder.EndNode.Id);
            graphBuilder.EdgeTailNode(activityId3).Id.ShouldNotBe(graphBuilder.StartNode.Id);



            bool result7 = graphBuilder.RemoveDummyActivity(activityId3);
            result7.ShouldBeTrue();

            graphBuilder.EdgeIds.Count().ShouldBe(4);
            graphBuilder.NodeIds.Count().ShouldBe(4);
            graphBuilder.AllDependenciesSatisfied.ShouldBeTrue();

            // First activity.
            graphBuilder.EdgeTailNode(activityId1).Id.ShouldBe(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId1).Id.ShouldNotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).ShouldBeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId2).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId1).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(dummyActivityId4).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId5).ShouldBeTrue();

            // Second activity.
            graphBuilder.EdgeTailNode(activityId2).Id.ShouldBe(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId2).Id.ShouldNotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).ShouldBeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId1).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId4).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId4).Id.ShouldBe(graphBuilder.EdgeHeadNode(activityId2).Id);

            // Third activity.
            graphBuilder.EdgeIds.Contains(activityId3).ShouldBeFalse();



            bool result8 = graphBuilder.RemoveDummyActivity(dummyActivityId5);
            result8.ShouldBeTrue();

            graphBuilder.EdgeIds.Count().ShouldBe(3);
            graphBuilder.NodeIds.Count().ShouldBe(3);
            graphBuilder.AllDependenciesSatisfied.ShouldBeTrue();


            // First activity.
            graphBuilder.EdgeTailNode(activityId1).Id.ShouldBe(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId1).Id.ShouldBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).ShouldBeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId2).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId1).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(dummyActivityId4).ShouldBeTrue();

            // Second activity.
            graphBuilder.EdgeTailNode(activityId2).Id.ShouldBe(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId2).Id.ShouldNotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).ShouldBeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId1).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId4).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId4).Id.ShouldBe(graphBuilder.EdgeHeadNode(activityId2).Id);

            // Third activity.
            graphBuilder.EdgeIds.Contains(activityId3).ShouldBeFalse();



            bool result9 = graphBuilder.RemoveDummyActivity(activityId1);
            result9.ShouldBeFalse();
            bool result10 = graphBuilder.RemoveDummyActivity(activityId2);
            result10.ShouldBeFalse();
            bool result11 = graphBuilder.RemoveDummyActivity(dummyActivityId4);
            result11.ShouldBeFalse();
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
            var graphBuilder = new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.ShouldBeTrue();

            var activity2 = new Activity<int, int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.ShouldBeTrue();

            var activity3 = new Activity<int, int, int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int> { activityId1, activityId2 });
            result3.ShouldBeTrue();

            graphBuilder.EdgeIds.Count().ShouldBe(8);
            graphBuilder.NodeIds.Count().ShouldBe(6);
            graphBuilder.AllDependenciesSatisfied.ShouldBeTrue();

            bool result4 = graphBuilder.RedirectEdges();
            result4.ShouldBeTrue();

            graphBuilder.EdgeIds.Count().ShouldBe(7);
            graphBuilder.NodeIds.Count().ShouldBe(6);
            graphBuilder.AllDependenciesSatisfied.ShouldBeTrue();

            // First activity.
            graphBuilder.EdgeTailNode(activityId1).Id.ShouldBe(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId1).Id.ShouldNotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId1).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count.ShouldBe(2);

            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId1).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId3).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId1).Id.ShouldBe(graphBuilder.EdgeHeadNode(activityId1).Id);
            graphBuilder.EdgeTailNode(dummyActivityId3).Id.ShouldBe(graphBuilder.EdgeHeadNode(activityId1).Id);

            // Second activity.
            graphBuilder.EdgeTailNode(activityId2).Id.ShouldBe(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId2).Id.ShouldNotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId2).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId2).Id.ShouldBe(graphBuilder.EdgeHeadNode(activityId2).Id);

            // First dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId2).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId3).ShouldBeTrue();

            graphBuilder.Edge(dummyActivityId1).Content.IsDummy.ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId1).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(activityId3).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId1).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId5).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId5).Id.ShouldBe(graphBuilder.EdgeHeadNode(dummyActivityId1).Id);

            // Second dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Contains(activityId2).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId2).ShouldBeTrue();

            graphBuilder.Edge(dummyActivityId2).Content.IsDummy.ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId2).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId2).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeHeadNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId1).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId3).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId1).Id.ShouldBe(graphBuilder.EdgeHeadNode(dummyActivityId2).Id);
            graphBuilder.EdgeTailNode(dummyActivityId3).Id.ShouldBe(graphBuilder.EdgeHeadNode(dummyActivityId2).Id);

            // Third activity.
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId3).ShouldBeTrue();

            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Contains(activityId3).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Contains(dummyActivityId1).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Contains(activityId3).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Contains(dummyActivityId5).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId5).Id.ShouldBe(graphBuilder.EdgeHeadNode(activityId3).Id);

            // Third dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId2).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Contains(dummyActivityId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Contains(dummyActivityId3).ShouldBeTrue();

            graphBuilder.Edge(dummyActivityId3).Content.IsDummy.ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId3).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId3).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(dummyActivityId3).OutgoingEdges.Contains(activityId3).ShouldBeTrue();

            graphBuilder.EdgeTailNode(activityId3).Id.ShouldBe(graphBuilder.EdgeHeadNode(dummyActivityId3).Id);

            // Fourth dummy activity.
            graphBuilder.EdgeIds.Contains(dummyActivityId4).ShouldBeFalse();

            // Fifth dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId5).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(dummyActivityId5).IncomingEdges.Contains(activityId3).ShouldBeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId5).IncomingEdges.Contains(dummyActivityId1).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId5).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(dummyActivityId5).OutgoingEdges.Contains(dummyActivityId5).ShouldBeTrue();

            graphBuilder.Edge(dummyActivityId5).Content.IsDummy.ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId5).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(dummyActivityId5).IncomingEdges.Contains(dummyActivityId5).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId5).Id.ShouldBe(graphBuilder.EndNode.Id);
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
            var graphBuilder = new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.ShouldBeTrue();

            var activity2 = new Activity<int, int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.ShouldBeTrue();

            var activity3 = new Activity<int, int, int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3);
            result3.ShouldBeTrue();

            var activity4 = new Activity<int, int, int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int> { activityId1, activityId2, activityId3 });
            result4.ShouldBeTrue();

            bool result5 = graphBuilder.RedirectEdges();
            result5.ShouldBeTrue();

            graphBuilder.EdgeIds.Count().ShouldBe(9);
            graphBuilder.NodeIds.Count().ShouldBe(7);
            graphBuilder.AllDependenciesSatisfied.ShouldBeTrue();

            // First activity.
            graphBuilder.EdgeTailNode(activityId1).Id.ShouldBe(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId1).Id.ShouldNotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.ShouldBe(3);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).ShouldBeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId3).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Count.ShouldBe(3);
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId2).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId3).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(dummyActivityId2).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId1).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId4).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId1).Id.ShouldBe(graphBuilder.EdgeHeadNode(activityId1).Id);
            graphBuilder.EdgeTailNode(dummyActivityId4).Id.ShouldBe(graphBuilder.EdgeHeadNode(activityId1).Id);

            // Second activity.
            graphBuilder.EdgeTailNode(activityId2).Id.ShouldBe(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId2).Id.ShouldNotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.ShouldBe(3);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).ShouldBeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId3).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count.ShouldBe(3);
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId3).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(dummyActivityId3).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId2).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId2).Id.ShouldBe(graphBuilder.EdgeHeadNode(activityId2).Id);

            // Third activity.
            graphBuilder.EdgeTailNode(activityId3).Id.ShouldBe(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId3).Id.ShouldNotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.ShouldBe(3);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).ShouldBeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId3).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Count.ShouldBe(3);
            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Contains(activityId2).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Contains(activityId3).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Contains(activityId3).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Contains(dummyActivityId3).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId3).Id.ShouldBe(graphBuilder.EdgeHeadNode(activityId3).Id);

            // Fourth activity.
            graphBuilder.EdgeTailNode(activityId4).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(activityId4).IncomingEdges.Contains(dummyActivityId4).ShouldBeTrue();

            graphBuilder.EdgeTailNode(activityId4).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(activityId4).OutgoingEdges.Contains(activityId4).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId4).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeHeadNode(activityId4).IncomingEdges.Contains(activityId4).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(activityId4).IncomingEdges.Contains(dummyActivityId1).ShouldBeTrue();

            // First dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId2).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId4).ShouldBeTrue();

            graphBuilder.Edge(dummyActivityId1).Content.IsDummy.ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId1).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(activityId4).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId1).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId7).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId7).Id.ShouldBe(graphBuilder.EdgeHeadNode(dummyActivityId1).Id);

            // Second dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Contains(activityId2).ShouldBeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId3).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId2).ShouldBeTrue();

            graphBuilder.Edge(dummyActivityId2).Content.IsDummy.ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId2).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId2).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeHeadNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId1).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId4).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId1).Id.ShouldBe(graphBuilder.EdgeHeadNode(dummyActivityId2).Id);

            // Third dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Contains(activityId3).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Contains(dummyActivityId3).ShouldBeTrue();

            graphBuilder.Edge(dummyActivityId2).Content.IsDummy.ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(activityId2).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId3).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId3).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(dummyActivityId3).OutgoingEdges.Contains(dummyActivityId2).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId2).Id.ShouldBe(graphBuilder.EdgeHeadNode(dummyActivityId3).Id);

            // Fourth dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId4).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(dummyActivityId4).IncomingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId4).IncomingEdges.Contains(dummyActivityId2).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId4).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(dummyActivityId4).OutgoingEdges.Contains(dummyActivityId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId4).OutgoingEdges.Contains(dummyActivityId4).ShouldBeTrue();

            graphBuilder.Edge(dummyActivityId4).Content.IsDummy.ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId4).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(dummyActivityId4).IncomingEdges.Contains(dummyActivityId4).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId4).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(dummyActivityId4).OutgoingEdges.Contains(activityId4).ShouldBeTrue();

            graphBuilder.EdgeTailNode(activityId4).Id.ShouldBe(graphBuilder.EdgeHeadNode(dummyActivityId4).Id);

            // Fifth dummy activity.
            graphBuilder.EdgeIds.Contains(dummyActivityId5).ShouldBeFalse();

            // Sixth dummy activity.
            graphBuilder.EdgeIds.Contains(dummyActivityId6).ShouldBeFalse();

            // Seventh dummy activity.
            graphBuilder.EndNode.IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId7).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId7).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(dummyActivityId7).IncomingEdges.Contains(activityId4).ShouldBeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId7).IncomingEdges.Contains(dummyActivityId1).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId7).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(dummyActivityId7).OutgoingEdges.Contains(dummyActivityId7).ShouldBeTrue();

            graphBuilder.Edge(dummyActivityId7).Content.IsDummy.ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId7).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(dummyActivityId7).IncomingEdges.Contains(dummyActivityId7).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId7).Id.ShouldBe(graphBuilder.EndNode.Id);
            graphBuilder.EdgeTailNode(dummyActivityId7).Id.ShouldNotBe(graphBuilder.StartNode.Id);
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
            var graphBuilder = new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.ShouldBeTrue();

            var activity2 = new Activity<int, int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.ShouldBeTrue();

            var activity3 = new Activity<int, int, int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3);
            result3.ShouldBeTrue();

            var activity4 = new Activity<int, int, int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int> { activityId1, activityId2, activityId3 });
            result4.ShouldBeTrue();

            IDictionary<int, HashSet<int>> ancestorNodesLookup = graphBuilder.GetAncestorNodesLookup();

            // Start node (event 1).
            ancestorNodesLookup[eventId1].Count.ShouldBe(0);

            // End node (event 2).
            HashSet<int> endNodeAncestors = ancestorNodesLookup[eventId2];
            endNodeAncestors.Count.ShouldBe(6);
            endNodeAncestors.Contains(eventId1).ShouldBeTrue();
            endNodeAncestors.Contains(eventId3).ShouldBeTrue();
            endNodeAncestors.Contains(eventId4).ShouldBeTrue();
            endNodeAncestors.Contains(eventId5).ShouldBeTrue();
            endNodeAncestors.Contains(eventId6).ShouldBeTrue();
            endNodeAncestors.Contains(eventId7).ShouldBeTrue();

            // Event 3.
            HashSet<int> event2NodeAncestors = ancestorNodesLookup[eventId3];
            event2NodeAncestors.Count.ShouldBe(1);
            event2NodeAncestors.Contains(eventId1).ShouldBeTrue();

            // Event 4.
            HashSet<int> event4NodeAncestors = ancestorNodesLookup[eventId4];
            event4NodeAncestors.Count.ShouldBe(1);
            event4NodeAncestors.Contains(eventId1).ShouldBeTrue();

            // Event 5.
            HashSet<int> event5NodeAncestors = ancestorNodesLookup[eventId5];
            event5NodeAncestors.Count.ShouldBe(1);
            event5NodeAncestors.Contains(eventId1).ShouldBeTrue();

            // Event 6.
            HashSet<int> event6NodeAncestors = ancestorNodesLookup[eventId6];
            event6NodeAncestors.Count.ShouldBe(4);
            event6NodeAncestors.Contains(eventId1).ShouldBeTrue();
            event6NodeAncestors.Contains(eventId3).ShouldBeTrue();
            event6NodeAncestors.Contains(eventId4).ShouldBeTrue();
            event6NodeAncestors.Contains(eventId5).ShouldBeTrue();

            // Event 7.
            HashSet<int> event7NodeAncestors = ancestorNodesLookup[eventId7];
            event7NodeAncestors.Count.ShouldBe(5);
            event7NodeAncestors.Contains(eventId1).ShouldBeTrue();
            event7NodeAncestors.Contains(eventId3).ShouldBeTrue();
            event7NodeAncestors.Contains(eventId4).ShouldBeTrue();
            event7NodeAncestors.Contains(eventId5).ShouldBeTrue();
            event7NodeAncestors.Contains(eventId6).ShouldBeTrue();
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
            var graphBuilder = new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.ShouldBeTrue();

            var activity2 = new Activity<int, int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.ShouldBeTrue();

            var activity3 = new Activity<int, int, int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int> { activityId1, activityId2, activityId6 });
            result3.ShouldBeTrue();

            var activity4 = new Activity<int, int, int>(activityId4, 0, canBeRemoved: true);
            bool result4 = graphBuilder.AddActivity(activity4);
            result4.ShouldBeTrue();

            var activity5 = new Activity<int, int, int>(activityId5, 0, canBeRemoved: true);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int> { activityId1 });
            result5.ShouldBeTrue();

            var activity6 = new Activity<int, int, int>(activityId6, 0, canBeRemoved: true);
            bool result6 = graphBuilder.AddActivity(activity6);
            result5.ShouldBeTrue();

            graphBuilder.EdgeIds.Count().ShouldBe(15);
            graphBuilder.NodeIds.Count().ShouldBe(10);

            // First activity.
            graphBuilder.EdgeTailNode(activityId1).Id.ShouldBe(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId1).Id.ShouldNotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.ShouldBe(4);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).ShouldBeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId4).ShouldBeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId6).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Count.ShouldBe(4);
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId2).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId4).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId6).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Count.ShouldBe(3);
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId1).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId3).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId7).ShouldBeTrue();

            // Second activity.
            graphBuilder.EdgeTailNode(activityId2).Id.ShouldBe(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId2).Id.ShouldNotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.ShouldBe(4);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).ShouldBeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId4).ShouldBeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId6).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count.ShouldBe(4);
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId4).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId6).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId2).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId4).ShouldBeTrue();

            // Third activity.
            graphBuilder.EdgeTailNode(activityId3).Id.ShouldNotBe(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId3).Id.ShouldNotBe(graphBuilder.EndNode.Id);

            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Count.ShouldBe(3);
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId3).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId4).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId9).ShouldBeTrue();

            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Contains(activityId3).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Contains(activityId3).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Contains(dummyActivityId5).ShouldBeTrue();

            // First dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId1).Id.ShouldNotBe(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(dummyActivityId1).Id.ShouldBe(graphBuilder.EndNode.Id);

            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Contains(activityId1).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Count.ShouldBe(3);
            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId3).ShouldBeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId7).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Count.ShouldBe(5);
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId1).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId2).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId5).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId6).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId8).ShouldBeTrue();

            // Second dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId2).Id.ShouldNotBe(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(dummyActivityId2).Id.ShouldBe(graphBuilder.EndNode.Id);

            graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Contains(activityId2).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId2).ShouldBeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId4).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Count.ShouldBe(5);
            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId1).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId2).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId5).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId6).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId8).ShouldBeTrue();

            // Third dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId3).Id.ShouldNotBe(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(dummyActivityId3).Id.ShouldNotBe(graphBuilder.EndNode.Id);

            graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Contains(activityId1).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Count.ShouldBe(3);
            graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Contains(dummyActivityId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Contains(dummyActivityId3).ShouldBeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Contains(dummyActivityId7).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Count.ShouldBe(3);
            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId3).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId4).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId9).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId3).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(dummyActivityId3).OutgoingEdges.Contains(activityId3).ShouldBeTrue();

            // Transitive Reduction.
            bool result10 = graphBuilder.TransitiveReduction();
            result10.ShouldBeTrue();

            graphBuilder.EdgeIds.Count().ShouldBe(13);
            graphBuilder.NodeIds.Count().ShouldBe(10);
            graphBuilder.AllDependenciesSatisfied.ShouldBeTrue();

            // First activity.
            graphBuilder.EdgeTailNode(activityId1).Id.ShouldBe(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId1).Id.ShouldNotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.ShouldBe(4);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).ShouldBeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId4).ShouldBeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId6).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Count.ShouldBe(4);
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId2).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId4).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId6).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId3).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId7).ShouldBeTrue();

            // Second activity.
            graphBuilder.EdgeTailNode(activityId2).Id.ShouldBe(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId2).Id.ShouldNotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.ShouldBe(4);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).ShouldBeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId4).ShouldBeTrue();
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId6).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count.ShouldBe(4);
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId4).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId6).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId4).ShouldBeTrue();

            // Third activity.
            graphBuilder.EdgeTailNode(activityId3).Id.ShouldNotBe(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId3).Id.ShouldNotBe(graphBuilder.EndNode.Id);

            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Count.ShouldBe(3);
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId3).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId4).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId9).ShouldBeTrue();

            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Contains(activityId3).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Contains(activityId3).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Contains(dummyActivityId5).ShouldBeTrue();

            // First dummy activity.
            graphBuilder.EdgeIds.Contains(dummyActivityId1).ShouldBeFalse();

            // Second dummy activity.
            graphBuilder.EdgeIds.Contains(dummyActivityId2).ShouldBeFalse();

            // Third dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId3).Id.ShouldNotBe(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(dummyActivityId3).Id.ShouldNotBe(graphBuilder.EndNode.Id);

            graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Contains(activityId1).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Contains(dummyActivityId3).ShouldBeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Contains(dummyActivityId7).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Count.ShouldBe(3);
            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId3).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId4).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId9).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId3).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(dummyActivityId3).OutgoingEdges.Contains(activityId3).ShouldBeTrue();
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
            var graphBuilder = new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.ShouldBeTrue();

            var activity2 = new Activity<int, int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.ShouldBeTrue();

            var activity3 = new Activity<int, int, int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int> { activityId1, activityId4 });
            result3.ShouldBeTrue();

            var activity4 = new Activity<int, int, int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int> { activityId2 });
            result4.ShouldBeTrue();

            var activity5 = new Activity<int, int, int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int> { activityId1 });
            result5.ShouldBeTrue();

            graphBuilder.EdgeIds.Count().ShouldBe(13);
            graphBuilder.NodeIds.Count().ShouldBe(10);

            // RemoveRedundantEdges.
            bool result6 = graphBuilder.RemoveRedundantEdges();
            result6.ShouldBeTrue();

            graphBuilder.EdgeIds.Count().ShouldBe(9);
            graphBuilder.NodeIds.Count().ShouldBe(6);
            graphBuilder.AllDependenciesSatisfied.ShouldBeTrue();

            // First activity.
            graphBuilder.EdgeTailNode(activityId1).Id.ShouldBe(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId1).Id.ShouldNotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId1).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Count.ShouldBe(3);

            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId1).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId3).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(activityId5).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId1).Id.ShouldBe(graphBuilder.EdgeHeadNode(activityId1).Id);

            // Second activity.
            graphBuilder.EdgeTailNode(activityId2).Id.ShouldBe(graphBuilder.StartNode.Id);
            graphBuilder.EdgeHeadNode(activityId2).Id.ShouldNotBe(graphBuilder.EndNode.Id);

            graphBuilder.StartNode.OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.StartNode.OutgoingEdges.Contains(activityId2).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(activityId4).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId2).ShouldBeTrue();

            graphBuilder.EdgeTailNode(activityId4).Id.ShouldBe(graphBuilder.EdgeHeadNode(activityId2).Id);

            // Fourth activity.
            graphBuilder.EdgeTailNode(activityId4).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(activityId4).IncomingEdges.Contains(activityId2).ShouldBeTrue();

            graphBuilder.EdgeTailNode(activityId4).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(activityId4).OutgoingEdges.Contains(activityId4).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId4).OutgoingEdges.Contains(dummyActivityId2).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId4).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeHeadNode(activityId4).IncomingEdges.Contains(activityId4).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(activityId4).IncomingEdges.Contains(dummyActivityId3).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId4).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId4).OutgoingEdges.Contains(activityId3).ShouldBeTrue();

            graphBuilder.EdgeTailNode(activityId3).Id.ShouldBe(graphBuilder.EdgeHeadNode(activityId4).Id);

            // First dummy activity.
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Contains(activityId1).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Count.ShouldBe(3);
            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId3).ShouldBeTrue();
            graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(activityId5).ShouldBeTrue();

            graphBuilder.Edge(dummyActivityId1).Content.IsDummy.ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Count.ShouldBe(4);
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId1).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId2).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId8).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(activityId3).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(dummyActivityId1).Id.ShouldBe(graphBuilder.EndNode.Id);
            graphBuilder.EdgeTailNode(dummyActivityId1).Id.ShouldNotBe(graphBuilder.StartNode.Id);

            // Third activity.
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId3).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(activityId4).ShouldBeTrue();

            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Contains(activityId3).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Count.ShouldBe(4);
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId1).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId2).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId8).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Contains(activityId3).ShouldBeTrue();
            graphBuilder.EndNode.IncomingEdges.Count.ShouldBe(4);
            graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId1).ShouldBeTrue();
            graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId2).ShouldBeTrue();
            graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId8).ShouldBeTrue();
            graphBuilder.EndNode.IncomingEdges.Contains(activityId3).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId3).Id.ShouldBe(graphBuilder.EndNode.Id);
            graphBuilder.EdgeTailNode(activityId3).Id.ShouldNotBe(graphBuilder.StartNode.Id);

            // Fifth activity.
            graphBuilder.EdgeTailNode(activityId5).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(activityId5).IncomingEdges.Contains(activityId1).ShouldBeTrue();

            graphBuilder.EdgeTailNode(activityId5).OutgoingEdges.Count.ShouldBe(3);
            graphBuilder.EdgeTailNode(activityId5).OutgoingEdges.Contains(activityId5).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId5).OutgoingEdges.Contains(dummyActivityId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(activityId5).OutgoingEdges.Contains(dummyActivityId3).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId5).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId5).IncomingEdges.Contains(activityId5).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(activityId5).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(activityId5).OutgoingEdges.Contains(dummyActivityId8).ShouldBeTrue();

            graphBuilder.EdgeTailNode(dummyActivityId8).Id.ShouldBe(graphBuilder.EdgeHeadNode(activityId5).Id);
        }

        [Fact]
        public void ArrowGraphBuilder_GivenCtorCalledWithNullArrowGraph_ThenShouldThrowArgumentNullException()
        {
            int eventId = 0;
            int activityId1 = 1;
            Action act = () => new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(null, () => activityId1++, () => eventId = eventId.Next());
            act.ShouldThrow<ArgumentNullException>();
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
            var graphBuilder = new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.ShouldBeTrue();

            var activity2 = new Activity<int, int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.ShouldBeTrue();

            var activity3 = new Activity<int, int, int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int> { activityId1, activityId4 });
            result3.ShouldBeTrue();

            var activity4 = new Activity<int, int, int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int> { activityId2 });
            result4.ShouldBeTrue();

            var activity5 = new Activity<int, int, int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int> { activityId1 });
            result5.ShouldBeTrue();

            var firstGraph = graphBuilder.ToGraph();

            var graphBuilder2 = new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(firstGraph, () => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            var secondGraph = graphBuilder2.ToGraph();
            secondGraph.ShouldBe(firstGraph);
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
            var graphBuilder = new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.ShouldBeTrue();

            var activity2 = new Activity<int, int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.ShouldBeTrue();

            var activity3 = new Activity<int, int, int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int> { activityId1, activityId4 });
            result3.ShouldBeTrue();

            var activity4 = new Activity<int, int, int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int> { activityId2 });
            result4.ShouldBeTrue();

            var activity5 = new Activity<int, int, int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int> { activityId1 });
            result5.ShouldBeTrue();

            var graph = graphBuilder.ToGraph();
            graph.Edges.RemoveAt(0);

            Action act = () => new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(graph, () => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            act.ShouldThrow<ArgumentException>();
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
            var graphBuilder = new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.ShouldBeTrue();

            var activity2 = new Activity<int, int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.ShouldBeTrue();

            var activity3 = new Activity<int, int, int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int> { activityId1, activityId4 });
            result3.ShouldBeTrue();

            var activity4 = new Activity<int, int, int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int> { activityId2 });
            result4.ShouldBeTrue();

            var activity5 = new Activity<int, int, int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int> { activityId1 });
            result5.ShouldBeTrue();

            var graph = graphBuilder.ToGraph();
            graph.Edges.Add(new Edge<int, IActivity<int, int, int>>(new Activity<int, int, int>(dummyActivityId = dummyActivityId.Next(), 0)));

            Action act = () => new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(graph, () => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            act.ShouldThrow<ArgumentException>();
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
            var graphBuilder = new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.ShouldBeTrue();

            var activity2 = new Activity<int, int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.ShouldBeTrue();

            var activity3 = new Activity<int, int, int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int> { activityId1, activityId4 });
            result3.ShouldBeTrue();

            var activity4 = new Activity<int, int, int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int> { activityId2 });
            result4.ShouldBeTrue();

            var activity5 = new Activity<int, int, int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int> { activityId1 });
            result5.ShouldBeTrue();

            var graph = graphBuilder.ToGraph();
            Node<int, IEvent<int>> node = graph.Nodes.First(x => x.NodeType == NodeType.Normal);
            graph.Nodes.Remove(node);

            Action act = () => new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(graph, () => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            act.ShouldThrow<ArgumentException>();
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
            var graphBuilder = new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.ShouldBeTrue();

            var activity2 = new Activity<int, int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.ShouldBeTrue();

            var activity3 = new Activity<int, int, int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int> { activityId1, activityId4 });
            result3.ShouldBeTrue();

            var activity4 = new Activity<int, int, int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int> { activityId2 });
            result4.ShouldBeTrue();

            var activity5 = new Activity<int, int, int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int> { activityId1 });
            result5.ShouldBeTrue();

            var graph = graphBuilder.ToGraph();
            graph.Nodes.Add(new Node<int, IEvent<int>>(new Event<int>(dummyActivityId = dummyActivityId.Next())));

            Action act = () => new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(graph, () => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            act.ShouldThrow<ArgumentException>();
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
            var graphBuilder = new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.ShouldBeTrue();

            var activity2 = new Activity<int, int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.ShouldBeTrue();

            var activity3 = new Activity<int, int, int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int> { activityId1, activityId4 });
            result3.ShouldBeTrue();

            var activity4 = new Activity<int, int, int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int> { activityId2 });
            result4.ShouldBeTrue();

            var activity5 = new Activity<int, int, int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int> { activityId1 });
            result5.ShouldBeTrue();

            var graph = graphBuilder.ToGraph();
            Node<int, IEvent<int>> node = graph.Nodes.First(x => x.NodeType == NodeType.Start);
            node.SetNodeType(NodeType.Normal);

            Action act = () => new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(graph, () => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            act.ShouldThrow<ArgumentException>();
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
            var graphBuilder = new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.ShouldBeTrue();

            var activity2 = new Activity<int, int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.ShouldBeTrue();

            var activity3 = new Activity<int, int, int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int> { activityId1, activityId4 });
            result3.ShouldBeTrue();

            var activity4 = new Activity<int, int, int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int> { activityId2 });
            result4.ShouldBeTrue();

            var activity5 = new Activity<int, int, int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int> { activityId1 });
            result5.ShouldBeTrue();

            var graph = graphBuilder.ToGraph();
            Node<int, IEvent<int>> node = graph.Nodes.First(x => x.NodeType == NodeType.End);
            node.SetNodeType(NodeType.Normal);

            Action act = () => new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(graph, () => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            act.ShouldThrow<ArgumentException>();
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
            var graphBuilder = new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.ShouldBeTrue();

            var activity2 = new Activity<int, int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.ShouldBeTrue();

            var activity3 = new Activity<int, int, int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int> { activityId1, activityId4 });
            result3.ShouldBeTrue();

            var activity4 = new Activity<int, int, int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int> { activityId2 });
            result4.ShouldBeTrue();

            var activity5 = new Activity<int, int, int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int> { activityId1 });
            result5.ShouldBeTrue();

            var graph = graphBuilder.ToGraph();

            Node<int, IEvent<int>> node = graph.Nodes.First(x => x.NodeType == NodeType.Normal);
            graph.Nodes.Remove(node);

            var newNode = new Node<int, IEvent<int>>(NodeType.Start, node.Content);
            foreach (int edgeId in node.OutgoingEdges)
            {
                newNode.OutgoingEdges.Add(edgeId);
            }
            graph.Nodes.Add(newNode);

            Action act = () => new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(graph, () => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            act.ShouldThrow<ArgumentException>();
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
            var graphBuilder = new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.ShouldBeTrue();

            var activity2 = new Activity<int, int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.ShouldBeTrue();

            var activity3 = new Activity<int, int, int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int> { activityId1, activityId4 });
            result3.ShouldBeTrue();

            var activity4 = new Activity<int, int, int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int> { activityId2 });
            result4.ShouldBeTrue();

            var activity5 = new Activity<int, int, int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int> { activityId1 });
            result5.ShouldBeTrue();

            var graph = graphBuilder.ToGraph();

            Node<int, IEvent<int>> node = graph.Nodes.First(x => x.NodeType == NodeType.Normal);
            graph.Nodes.Remove(node);

            var newNode = new Node<int, IEvent<int>>(NodeType.End, node.Content);
            foreach (int edgeId in node.IncomingEdges)
            {
                newNode.IncomingEdges.Add(edgeId);
            }
            graph.Nodes.Add(newNode);

            Action act = () => new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(graph, () => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            act.ShouldThrow<ArgumentException>();
        }

        [Fact]
        public void ArrowGraphBuilder_GivenAllDummyActivitiesFindCircularDependencies_ThenFindsCircularDependency()
        {
            int eventId = 0;
            int dummyActivityId = 100;
            var graphBuilder = new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            graphBuilder.AddActivity(new Activity<int, int, int>(1, 0));
            graphBuilder.AddActivity(new Activity<int, int, int>(2, 0), new HashSet<int> { 7 });
            graphBuilder.AddActivity(new Activity<int, int, int>(3, 0));
            graphBuilder.AddActivity(new Activity<int, int, int>(4, 0), new HashSet<int> { 2 });
            graphBuilder.AddActivity(new Activity<int, int, int>(5, 0), new HashSet<int> { 1, 2, 3, 8 });
            graphBuilder.AddActivity(new Activity<int, int, int>(6, 0), new HashSet<int> { 3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(7, 0), new HashSet<int> { 4 });
            graphBuilder.AddActivity(new Activity<int, int, int>(8, 0), new HashSet<int> { 9, 6 });
            graphBuilder.AddActivity(new Activity<int, int, int>(9, 0), new HashSet<int> { 5 });
            IList<ICircularDependency<int>> circularDependencies = graphBuilder.FindStrongCircularDependencies();

            circularDependencies.Count.ShouldBe(2);
            circularDependencies[0].Dependencies.ShouldBe(new int[] { 2, 4, 7 }, ignoreOrder: true);
            circularDependencies[1].Dependencies.ShouldBe(new int[] { 5, 8, 9 }, ignoreOrder: true);
        }

        [Fact]
        public void ArrowGraphBuilder_GivenFindCircularDependencies_ThenFindsCircularDependency()
        {
            int eventId = 0;
            int dummyActivityId = 100;
            var graphBuilder = new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            graphBuilder.AddActivity(new Activity<int, int, int>(1, 10));
            graphBuilder.AddActivity(new Activity<int, int, int>(2, 10), new HashSet<int> { 7 });
            graphBuilder.AddActivity(new Activity<int, int, int>(3, 10));
            graphBuilder.AddActivity(new Activity<int, int, int>(4, 10), new HashSet<int> { 2 });
            graphBuilder.AddActivity(new Activity<int, int, int>(5, 10), new HashSet<int> { 1, 2, 3, 8 });
            graphBuilder.AddActivity(new Activity<int, int, int>(6, 10), new HashSet<int> { 3 });
            graphBuilder.AddActivity(new Activity<int, int, int>(7, 10), new HashSet<int> { 4 });
            graphBuilder.AddActivity(new Activity<int, int, int>(8, 10), new HashSet<int> { 9, 6 });
            graphBuilder.AddActivity(new Activity<int, int, int>(9, 10), new HashSet<int> { 5 });
            IList<ICircularDependency<int>> circularDependencies = graphBuilder.FindStrongCircularDependencies();

            circularDependencies.Count.ShouldBe(2);
            circularDependencies[0].Dependencies.ShouldBe(new int[] { 2, 4, 7 }, ignoreOrder: true);
            circularDependencies[1].Dependencies.ShouldBe(new int[] { 5, 8, 9 }, ignoreOrder: true);
        }
    }
}
