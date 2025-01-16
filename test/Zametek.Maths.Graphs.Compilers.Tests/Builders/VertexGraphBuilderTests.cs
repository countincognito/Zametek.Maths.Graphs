using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class VertexGraphBuilderTests
    {
        [Fact]
        public void VertexGraphBuilder_GivenContructor_ThenNoException()
        {
            int eventId = 0;
            int dummyActivityId = 0;
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());

            graphBuilder.EdgeIds.Any().ShouldBeFalse();
            graphBuilder.NodeIds.Any().ShouldBeFalse();
            graphBuilder.AllDependenciesSatisfied.ShouldBeTrue();
            graphBuilder.StartNodes.Any().ShouldBeFalse();
            graphBuilder.EndNodes.Any().ShouldBeFalse();
        }

        [Fact]
        public void VertexGraphBuilder_GivenCtorCalledWithNullEdgeIdGenerator_ThenShouldThrowArgumentNullException()
        {
            int dummyActivityId = 0;
            Action act = () => new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(null, () => dummyActivityId = dummyActivityId.Next());
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void VertexGraphBuilder_GivenCtorCalledWithNullNodeIdGenerator_ThenShouldThrowArgumentNullException()
        {
            int eventId = 0;
            Action act = () => new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), null);
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void VertexGraphBuilder_GivenSingleActivityNoDependencies_ThenNoStartOrEndNodes()
        {
            int eventId = 0;
            int activityId = 0;
            int activityId1 = activityId + 1;
            int dummyActivityId = activityId1 + 1;
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };

            var activity = new Activity<int, int, int>(activityId1, 0);
            bool result = graphBuilder.AddActivity(activity);
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
        public void VertexGraphBuilder_GivenTwoActivitiesOneDependency_ThenActivitiesHookedUpByEdge()
        {
            int eventId = 0;
            int eventId1 = eventId + 1;
            int activityId = 0;
            int activityId1 = activityId + 1;
            int activityId2 = activityId1 + 1;
            int dummyActivityId = activityId2 + 1;
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.ShouldBeTrue();

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

            var activity2 = new Activity<int, int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2, new HashSet<int> { activityId1 });
            result2.ShouldBeTrue();

            graphBuilder.EdgeIds.Count().ShouldBe(1);
            graphBuilder.NodeIds.Count().ShouldBe(2);
            graphBuilder.AllDependenciesSatisfied.ShouldBeTrue();

            graphBuilder.StartNodes.Count().ShouldBe(1);
            graphBuilder.EndNodes.Count().ShouldBe(1);

            // First activity.
            graphBuilder.StartNodes.First().Id.ShouldBe(activityId1);
            graphBuilder.EdgeTailNode(eventId1).Id.ShouldBe(activityId1);

            graphBuilder.StartNodes.First().OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.StartNodes.First().OutgoingEdges.Contains(eventId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(eventId1).Id.ShouldBe(activityId1);
            graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Contains(eventId1).ShouldBeTrue();

            // Second activity.

            graphBuilder.EndNodes.First().Id.ShouldBe(activityId2);
            graphBuilder.EdgeHeadNode(eventId1).Id.ShouldBe(activityId2);

            graphBuilder.EndNodes.First().IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EndNodes.First().IncomingEdges.Contains(eventId1).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(eventId1).Id.ShouldBe(activityId2);
            graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Contains(eventId1).ShouldBeTrue();
        }

        [Fact]
        public void VertexGraphBuilder_GivenTwoActivitiesOneDependencyReverseOrder_ThenActivitiesHookedUpByEdge()
        {
            int eventId = 0;
            int eventId1 = eventId + 1;
            int activityId = 0;
            int activityId1 = activityId + 1;
            int activityId2 = activityId1 + 1;
            int dummyActivityId = activityId2 + 1;
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };

            var activity2 = new Activity<int, int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2, new HashSet<int> { activityId1 });
            result2.ShouldBeTrue();

            graphBuilder.EdgeIds.Any().ShouldBeFalse();
            graphBuilder.NodeIds.Count().ShouldBe(1);
            graphBuilder.AllDependenciesSatisfied.ShouldBeFalse();

            graphBuilder.StartNodes.Any().ShouldBeFalse();
            graphBuilder.EndNodes.Count().ShouldBe(1);

            graphBuilder.EndNodes.First().Id.ShouldBe(activityId2);

            graphBuilder.EndNodes.First().IncomingEdges.Any().ShouldBeFalse();

            var activity1 = new Activity<int, int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.ShouldBeTrue();

            graphBuilder.EdgeIds.Count().ShouldBe(1);
            graphBuilder.NodeIds.Count().ShouldBe(2);
            graphBuilder.AllDependenciesSatisfied.ShouldBeTrue();

            graphBuilder.StartNodes.Count().ShouldBe(1);
            graphBuilder.EndNodes.Count().ShouldBe(1);

            // First Activity.
            graphBuilder.StartNodes.First().Id.ShouldBe(activityId1);
            graphBuilder.EdgeTailNode(eventId1).Id.ShouldBe(activityId1);

            graphBuilder.StartNodes.First().OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.StartNodes.First().OutgoingEdges.Contains(eventId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(eventId1).Id.ShouldBe(activityId1);
            graphBuilder.EdgeTailNode(eventId1).ShouldNotBeNull();
            graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Contains(eventId1).ShouldBeTrue();

            // Second Activity.
            graphBuilder.EndNodes.First().Id.ShouldBe(activityId2);
            graphBuilder.EdgeHeadNode(eventId1).Id.ShouldBe(activityId2);

            graphBuilder.EndNodes.First().IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EndNodes.First().IncomingEdges.Contains(eventId1).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(eventId1).Id.ShouldBe(activityId2);
            graphBuilder.EdgeHeadNode(eventId1).ShouldNotBeNull();
            graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Contains(eventId1).ShouldBeTrue();
        }

        [Fact]
        public void VertexGraphBuilder_GivenThreeActivitiesOneDependentOnOtherTwo_ThenDependentActivityHookedUpByTwoEdges()
        {
            int eventId = 0;
            int eventId1 = eventId + 1;
            int eventId2 = eventId1 + 1;
            int activityId = 0;
            int activityId1 = activityId + 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int dummyActivityId = activityId3 + 1;
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.ShouldBeTrue();

            graphBuilder.EdgeIds.Any().ShouldBeFalse();
            graphBuilder.NodeIds.Count().ShouldBe(1);
            graphBuilder.AllDependenciesSatisfied.ShouldBeTrue();

            graphBuilder.StartNodes.Any().ShouldBeFalse();
            graphBuilder.EndNodes.Any().ShouldBeFalse();

            graphBuilder.Node(activityId1).Id.ShouldBe(activityId1);
            graphBuilder.Node(activityId1).NodeType.ShouldBe(NodeType.Isolated);
            graphBuilder.Activity(activityId1).Id.ShouldBe(activityId1);
            graphBuilder.Activities.Count().ShouldBe(1);
            graphBuilder.Nodes.Count().ShouldBe(1);
            graphBuilder.Events.Any().ShouldBeFalse();
            graphBuilder.Edges.Any().ShouldBeFalse();

            var activity2 = new Activity<int, int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.ShouldBeTrue();

            graphBuilder.EdgeIds.Any().ShouldBeFalse();
            graphBuilder.NodeIds.Count().ShouldBe(2);
            graphBuilder.AllDependenciesSatisfied.ShouldBeTrue();

            graphBuilder.StartNodes.Any().ShouldBeFalse();
            graphBuilder.EndNodes.Any().ShouldBeFalse();

            graphBuilder.Node(activityId2).Id.ShouldBe(activityId2);
            graphBuilder.Node(activityId2).NodeType.ShouldBe(NodeType.Isolated);
            graphBuilder.Activity(activityId2).Id.ShouldBe(activityId2);
            graphBuilder.Activities.Count().ShouldBe(2);
            graphBuilder.Nodes.Count().ShouldBe(2);
            graphBuilder.Events.Any().ShouldBeFalse();
            graphBuilder.Edges.Any().ShouldBeFalse();

            var activity3 = new Activity<int, int, int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int> { activityId1, activityId2 });
            result3.ShouldBeTrue();

            graphBuilder.EdgeIds.Count().ShouldBe(2);
            graphBuilder.NodeIds.Count().ShouldBe(3);
            graphBuilder.AllDependenciesSatisfied.ShouldBeTrue();

            graphBuilder.StartNodes.Count().ShouldBe(2);
            graphBuilder.EndNodes.Count().ShouldBe(1);

            graphBuilder.Node(activityId3).Id.ShouldBe(activityId3);
            graphBuilder.Node(activityId3).NodeType.ShouldBe(NodeType.End);
            graphBuilder.Activity(activityId3).Id.ShouldBe(activityId3);
            graphBuilder.Activities.Count().ShouldBe(3);
            graphBuilder.Nodes.Count().ShouldBe(3);
            graphBuilder.Events.Count().ShouldBe(2);
            graphBuilder.Edges.Count().ShouldBe(2);

            // First activity.
            graphBuilder.Node(activityId1).Id.ShouldBe(activityId1);
            graphBuilder.Node(activityId1).NodeType.ShouldBe(NodeType.Start);
            graphBuilder.EdgeTailNode(eventId1).ShouldNotBeNull();
            graphBuilder.EdgeTailNode(eventId1).Id.ShouldBe(activityId1);
            graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Contains(eventId1).ShouldBeTrue();

            // Second activity.
            graphBuilder.Node(activityId2).Id.ShouldBe(activityId2);
            graphBuilder.Node(activityId2).NodeType.ShouldBe(NodeType.Start);
            graphBuilder.EdgeTailNode(eventId2).ShouldNotBeNull();
            graphBuilder.EdgeTailNode(eventId2).Id.ShouldBe(activityId2);
            graphBuilder.EdgeTailNode(eventId2).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(eventId2).OutgoingEdges.Contains(eventId2).ShouldBeTrue();

            // Third activity.
            graphBuilder.Node(activityId3).Id.ShouldBe(activityId3);
            graphBuilder.Node(activityId3).NodeType.ShouldBe(NodeType.End);
            graphBuilder.EdgeHeadNode(eventId1).ShouldNotBeNull();
            graphBuilder.EdgeHeadNode(eventId2).ShouldNotBeNull();
            graphBuilder.EdgeHeadNode(eventId1).Id.ShouldBe(activityId3);
            graphBuilder.EdgeHeadNode(eventId2).Id.ShouldBe(activityId3);
            graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeHeadNode(eventId2).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Contains(eventId1).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(eventId2).IncomingEdges.Contains(eventId2).ShouldBeTrue();
        }

        [Fact]
        public void VertexGraphBuilder_GivenThreeActivitiesOneDependentOnOtherTwoReverseOrder_ThenDependentActivityHookedUpByTwoEdges()
        {
            int eventId = 0;
            int eventId1 = eventId + 1;
            int eventId2 = eventId1 + 1;
            int activityId = 0;
            int activityId1 = activityId + 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int dummyActivityId = activityId3 + 1;
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };

            var activity3 = new Activity<int, int, int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int> { activityId1, activityId2 });
            result3.ShouldBeTrue();

            graphBuilder.EdgeIds.Count().ShouldBe(0);
            graphBuilder.NodeIds.Count().ShouldBe(1);
            graphBuilder.AllDependenciesSatisfied.ShouldBeFalse();

            graphBuilder.StartNodes.Any().ShouldBeFalse();
            graphBuilder.EndNodes.Count().ShouldBe(1);

            graphBuilder.Node(activityId3).Id.ShouldBe(activityId3);
            graphBuilder.Node(activityId3).NodeType.ShouldBe(NodeType.End);
            graphBuilder.Activity(activityId3).Id.ShouldBe(activityId3);
            graphBuilder.Activities.Count().ShouldBe(1);
            graphBuilder.Nodes.Count().ShouldBe(1);
            graphBuilder.Events.Any().ShouldBeFalse();
            graphBuilder.Edges.Any().ShouldBeFalse();

            var activity2 = new Activity<int, int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.ShouldBeTrue();

            graphBuilder.EdgeIds.Count().ShouldBe(1);
            graphBuilder.NodeIds.Count().ShouldBe(2);
            graphBuilder.AllDependenciesSatisfied.ShouldBeFalse();

            graphBuilder.StartNodes.Count().ShouldBe(1);
            graphBuilder.EndNodes.Count().ShouldBe(1);

            graphBuilder.Node(activityId2).Id.ShouldBe(activityId2);
            graphBuilder.Node(activityId2).NodeType.ShouldBe(NodeType.Start);
            graphBuilder.Activity(activityId2).Id.ShouldBe(activityId2);
            graphBuilder.Activities.Count().ShouldBe(2);
            graphBuilder.Nodes.Count().ShouldBe(2);
            graphBuilder.Events.Count().ShouldBe(1);
            graphBuilder.Edges.Count().ShouldBe(1);

            var activity1 = new Activity<int, int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.ShouldBeTrue();

            graphBuilder.EdgeIds.Count().ShouldBe(2);
            graphBuilder.NodeIds.Count().ShouldBe(3);
            graphBuilder.AllDependenciesSatisfied.ShouldBeTrue();

            graphBuilder.StartNodes.Count().ShouldBe(2);
            graphBuilder.EndNodes.Count().ShouldBe(1);

            graphBuilder.Node(activityId1).Id.ShouldBe(activityId1);
            graphBuilder.Node(activityId1).NodeType.ShouldBe(NodeType.Start);
            graphBuilder.Activity(activityId1).Id.ShouldBe(activityId1);
            graphBuilder.Activities.Count().ShouldBe(3);
            graphBuilder.Nodes.Count().ShouldBe(3);
            graphBuilder.Events.Count().ShouldBe(2);
            graphBuilder.Edges.Count().ShouldBe(2);

            // First activity.
            graphBuilder.Node(activityId1).Id.ShouldBe(activityId1);
            graphBuilder.Node(activityId1).NodeType.ShouldBe(NodeType.Start);
            graphBuilder.EdgeTailNode(eventId2).ShouldNotBeNull();
            graphBuilder.EdgeTailNode(eventId2).Id.ShouldBe(activityId1);
            graphBuilder.EdgeTailNode(eventId2).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(eventId2).OutgoingEdges.Contains(eventId2).ShouldBeTrue();

            // Second activity.
            graphBuilder.Node(activityId2).Id.ShouldBe(activityId2);
            graphBuilder.Node(activityId2).NodeType.ShouldBe(NodeType.Start);
            graphBuilder.EdgeTailNode(eventId1).ShouldNotBeNull();
            graphBuilder.EdgeTailNode(eventId1).Id.ShouldBe(activityId2);
            graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Contains(eventId1).ShouldBeTrue();

            // Third activity.
            graphBuilder.Node(activityId3).Id.ShouldBe(activityId3);
            graphBuilder.Node(activityId3).NodeType.ShouldBe(NodeType.End);
            graphBuilder.EdgeHeadNode(eventId1).ShouldNotBeNull();
            graphBuilder.EdgeHeadNode(eventId2).ShouldNotBeNull();
            graphBuilder.EdgeHeadNode(eventId1).Id.ShouldBe(activityId3);
            graphBuilder.EdgeHeadNode(eventId2).Id.ShouldBe(activityId3);
            graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeHeadNode(eventId2).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Contains(eventId1).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(eventId2).IncomingEdges.Contains(eventId2).ShouldBeTrue();
        }

        [Fact]
        public void VertexGraphBuilder_GivenThreeActivitiesOneDependentOnOtherTwoRemovedInStages_ThenStructureAsExpected()
        {
            int eventId = 0;
            int eventId1 = eventId + 1;
            int eventId2 = eventId1 + 1;
            int activityId = 0;
            int activityId1 = activityId + 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int dummyActivityId = activityId3 + 1;
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
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

            graphBuilder.Activities.Count().ShouldBe(3);
            graphBuilder.Nodes.Count().ShouldBe(3);
            graphBuilder.Events.Count().ShouldBe(2);
            graphBuilder.Edges.Count().ShouldBe(2);

            // First activity.
            graphBuilder.Node(activityId1).Id.ShouldBe(activityId1);
            graphBuilder.Node(activityId1).NodeType.ShouldBe(NodeType.Start);
            graphBuilder.EdgeTailNode(eventId1).ShouldNotBeNull();
            graphBuilder.EdgeTailNode(eventId1).Id.ShouldBe(activityId1);
            graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Contains(eventId1).ShouldBeTrue();

            // Second activity.
            graphBuilder.Node(activityId2).Id.ShouldBe(activityId2);
            graphBuilder.Node(activityId2).NodeType.ShouldBe(NodeType.Start);
            graphBuilder.EdgeTailNode(eventId2).ShouldNotBeNull();
            graphBuilder.EdgeTailNode(eventId2).Id.ShouldBe(activityId2);
            graphBuilder.EdgeTailNode(eventId2).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(eventId2).OutgoingEdges.Contains(eventId2).ShouldBeTrue();

            // Third activity.
            graphBuilder.Node(activityId3).Id.ShouldBe(activityId3);
            graphBuilder.Node(activityId3).NodeType.ShouldBe(NodeType.End);
            graphBuilder.EdgeHeadNode(eventId1).ShouldNotBeNull();
            graphBuilder.EdgeHeadNode(eventId2).ShouldNotBeNull();
            graphBuilder.EdgeHeadNode(eventId1).Id.ShouldBe(activityId3);
            graphBuilder.EdgeHeadNode(eventId2).Id.ShouldBe(activityId3);
            graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeHeadNode(eventId2).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Contains(eventId1).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(eventId2).IncomingEdges.Contains(eventId2).ShouldBeTrue();



            bool result4 = graphBuilder.RemoveActivity(activityId3);
            result4.ShouldBeFalse();

            graphBuilder.Activity(activityId3).SetAsRemovable();

            result4 = graphBuilder.RemoveActivity(activityId3);
            result4.ShouldBeTrue();

            graphBuilder.Activities.Count().ShouldBe(2);
            graphBuilder.Nodes.Count().ShouldBe(2);
            graphBuilder.Events.Any().ShouldBeFalse();
            graphBuilder.Edges.Any().ShouldBeFalse();
            graphBuilder.AllDependenciesSatisfied.ShouldBeTrue();

            // First activity.
            graphBuilder.Node(activityId1).Id.ShouldBe(activityId1);
            graphBuilder.Node(activityId1).NodeType.ShouldBe(NodeType.Isolated);
            graphBuilder.EdgeTailNode(eventId1).ShouldBeNull();

            // Second activity.
            graphBuilder.Node(activityId2).Id.ShouldBe(activityId2);
            graphBuilder.Node(activityId2).NodeType.ShouldBe(NodeType.Isolated);
            graphBuilder.EdgeTailNode(eventId2).ShouldBeNull();

            // Third activity.
            graphBuilder.EdgeIds.Contains(activityId3).ShouldBeFalse();



            bool result5 = graphBuilder.RemoveActivity(activityId2);
            result5.ShouldBeFalse();

            graphBuilder.Activity(activityId2).SetAsRemovable();

            result5 = graphBuilder.RemoveActivity(activityId2);
            result5.ShouldBeTrue();

            graphBuilder.Activities.Count().ShouldBe(1);
            graphBuilder.Nodes.Count().ShouldBe(1);
            graphBuilder.Events.Any().ShouldBeFalse();
            graphBuilder.Edges.Any().ShouldBeFalse();
            graphBuilder.AllDependenciesSatisfied.ShouldBeTrue();

            // First activity.
            graphBuilder.Node(activityId1).Id.ShouldBe(activityId1);
            graphBuilder.Node(activityId1).NodeType.ShouldBe(NodeType.Isolated);
            graphBuilder.EdgeTailNode(eventId1).ShouldBeNull();

            // Second activity.
            graphBuilder.EdgeIds.Contains(activityId2).ShouldBeFalse();

            // Third activity.
            graphBuilder.EdgeIds.Contains(activityId3).ShouldBeFalse();



            bool result6 = graphBuilder.RemoveActivity(activityId1);
            result6.ShouldBeFalse();

            graphBuilder.Activity(activityId1).SetAsRemovable();

            result6 = graphBuilder.RemoveActivity(activityId1);
            result6.ShouldBeTrue();

            graphBuilder.Activities.Any().ShouldBeFalse();
            graphBuilder.Nodes.Any().ShouldBeFalse();
            graphBuilder.Events.Any().ShouldBeFalse();
            graphBuilder.Edges.Any().ShouldBeFalse();
            graphBuilder.AllDependenciesSatisfied.ShouldBeTrue();

            // First activity.
            graphBuilder.EdgeIds.Contains(activityId1).ShouldBeFalse();

            // Second activity.
            graphBuilder.EdgeIds.Contains(activityId2).ShouldBeFalse();

            // Third activity.
            graphBuilder.EdgeIds.Contains(activityId3).ShouldBeFalse();
        }

        [Fact]
        public void VertexGraphBuilder_GivenFourActivitiesOneDependentOnOtherThreeGetAncestorNodesLookup_ThenAncestorsAsExpected()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int dummyActivityId = activityId4 + 1;
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
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
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int> { activityId2 });
            result3.ShouldBeTrue();

            var activity4 = new Activity<int, int, int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int> { activityId1, activityId2, activityId3 });
            result4.ShouldBeTrue();

            IDictionary<int, HashSet<int>> ancestorNodesLookup = graphBuilder.GetAncestorNodesLookup();

            // Start node (activity 1).
            ancestorNodesLookup[activityId1].Count.ShouldBe(0);

            // Start node (activity 2).
            ancestorNodesLookup[activityId2].Count.ShouldBe(0);

            // Activity 3.
            HashSet<int> nodeAncestors = ancestorNodesLookup[activityId3];
            nodeAncestors.Count.ShouldBe(1);
            nodeAncestors.Contains(activityId2).ShouldBeTrue();

            // End node (activity 4).
            HashSet<int> endNodeAncestors = ancestorNodesLookup[activityId4];
            endNodeAncestors.Count.ShouldBe(3);
            endNodeAncestors.Contains(activityId1).ShouldBeTrue();
            endNodeAncestors.Contains(activityId2).ShouldBeTrue();
            endNodeAncestors.Contains(activityId3).ShouldBeTrue();
        }

        [Fact]
        public void VertexGraphBuilder_GivenFiveActivitiesWithTwoUnnecessaryDependencies_ThenTransitiveReductionAsExpected()
        {
            int eventId = 0;
            int eventId1 = eventId + 1;
            int eventId2 = eventId1 + 1;
            int eventId3 = eventId2 + 1;
            int eventId4 = eventId3 + 1;
            int eventId5 = eventId4 + 1;
            int eventId6 = eventId5 + 1;
            int eventId7 = eventId6 + 1;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int dummyActivityId = activityId5 + 1;
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.ShouldBeTrue();

            var activity2 = new Activity<int, int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2, new HashSet<int> { activityId5 });
            result2.ShouldBeTrue();

            var activity3 = new Activity<int, int, int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int> { activityId1, activityId2, activityId5 });
            result3.ShouldBeTrue();

            var activity4 = new Activity<int, int, int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int> { activityId1, activityId2, activityId3 });
            result4.ShouldBeTrue();

            var activity5 = new Activity<int, int, int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5);
            result5.ShouldBeTrue();

            graphBuilder.EdgeIds.Count().ShouldBe(7);
            graphBuilder.NodeIds.Count().ShouldBe(5);

            // First activity.
            graphBuilder.Node(activityId1).Id.ShouldBe(activityId1);
            graphBuilder.Node(activityId1).NodeType.ShouldBe(NodeType.Start);

            graphBuilder.EdgeTailNode(eventId1).ShouldNotBeNull();
            graphBuilder.EdgeTailNode(eventId3).ShouldNotBeNull();
            graphBuilder.EdgeTailNode(eventId1).Id.ShouldBe(activityId1);
            graphBuilder.EdgeTailNode(eventId3).Id.ShouldBe(activityId1);
            graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(eventId3).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Contains(eventId1).ShouldBeTrue();
            graphBuilder.EdgeTailNode(eventId3).OutgoingEdges.Contains(eventId3).ShouldBeTrue();

            // Second activity.
            graphBuilder.Node(activityId2).Id.ShouldBe(activityId2);
            graphBuilder.Node(activityId2).NodeType.ShouldBe(NodeType.Normal);

            graphBuilder.EdgeTailNode(eventId2).ShouldNotBeNull();
            graphBuilder.EdgeTailNode(eventId4).ShouldNotBeNull();
            graphBuilder.EdgeTailNode(eventId2).Id.ShouldBe(activityId2);
            graphBuilder.EdgeTailNode(eventId4).Id.ShouldBe(activityId2);
            graphBuilder.EdgeTailNode(eventId2).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(eventId4).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(eventId2).OutgoingEdges.Contains(eventId2).ShouldBeTrue();
            graphBuilder.EdgeTailNode(eventId4).OutgoingEdges.Contains(eventId4).ShouldBeTrue();

            graphBuilder.EdgeTailNode(eventId6).ShouldNotBeNull();
            graphBuilder.EdgeTailNode(eventId6).Id.ShouldBe(activityId5);
            graphBuilder.EdgeHeadNode(eventId6).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(eventId6).IncomingEdges.Contains(eventId6).ShouldBeTrue();

            // Third activity.
            graphBuilder.Node(activityId3).Id.ShouldBe(activityId3);
            graphBuilder.Node(activityId3).NodeType.ShouldBe(NodeType.Normal);

            graphBuilder.EdgeTailNode(eventId5).ShouldNotBeNull();
            graphBuilder.EdgeTailNode(eventId5).Id.ShouldBe(activityId3);
            graphBuilder.EdgeTailNode(eventId5).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(eventId5).OutgoingEdges.Contains(eventId5).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(eventId1).ShouldNotBeNull();
            graphBuilder.EdgeHeadNode(eventId2).ShouldNotBeNull();
            graphBuilder.EdgeHeadNode(eventId7).ShouldNotBeNull();
            graphBuilder.EdgeHeadNode(eventId1).Id.ShouldBe(activityId3);
            graphBuilder.EdgeHeadNode(eventId2).Id.ShouldBe(activityId3);
            graphBuilder.EdgeHeadNode(eventId7).Id.ShouldBe(activityId3);
            graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Count.ShouldBe(3);
            graphBuilder.EdgeHeadNode(eventId2).IncomingEdges.Count.ShouldBe(3);
            graphBuilder.EdgeHeadNode(eventId7).IncomingEdges.Count.ShouldBe(3);
            graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Contains(eventId1).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(eventId2).IncomingEdges.Contains(eventId2).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(eventId7).IncomingEdges.Contains(eventId7).ShouldBeTrue();

            // Forth activity.
            graphBuilder.Node(activityId4).Id.ShouldBe(activityId4);
            graphBuilder.Node(activityId4).NodeType.ShouldBe(NodeType.End);

            graphBuilder.EdgeHeadNode(eventId3).ShouldNotBeNull();
            graphBuilder.EdgeHeadNode(eventId4).ShouldNotBeNull();
            graphBuilder.EdgeHeadNode(eventId5).ShouldNotBeNull();
            graphBuilder.EdgeHeadNode(eventId3).Id.ShouldBe(activityId4);
            graphBuilder.EdgeHeadNode(eventId4).Id.ShouldBe(activityId4);
            graphBuilder.EdgeHeadNode(eventId5).Id.ShouldBe(activityId4);
            graphBuilder.EdgeHeadNode(eventId3).IncomingEdges.Count.ShouldBe(3);
            graphBuilder.EdgeHeadNode(eventId4).IncomingEdges.Count.ShouldBe(3);
            graphBuilder.EdgeHeadNode(eventId5).IncomingEdges.Count.ShouldBe(3);
            graphBuilder.EdgeHeadNode(eventId3).IncomingEdges.Contains(eventId3).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(eventId4).IncomingEdges.Contains(eventId4).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(eventId5).IncomingEdges.Contains(eventId5).ShouldBeTrue();

            // Fifth activity.
            graphBuilder.Node(activityId5).Id.ShouldBe(activityId5);
            graphBuilder.Node(activityId5).NodeType.ShouldBe(NodeType.Start);

            graphBuilder.EdgeTailNode(eventId6).ShouldNotBeNull();
            graphBuilder.EdgeTailNode(eventId7).ShouldNotBeNull();
            graphBuilder.EdgeTailNode(eventId6).Id.ShouldBe(activityId5);
            graphBuilder.EdgeTailNode(eventId7).Id.ShouldBe(activityId5);
            graphBuilder.EdgeTailNode(eventId6).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(eventId7).OutgoingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeTailNode(eventId6).OutgoingEdges.Contains(eventId6).ShouldBeTrue();
            graphBuilder.EdgeTailNode(eventId7).OutgoingEdges.Contains(eventId7).ShouldBeTrue();

            // Transitive Reduction.
            bool result6 = graphBuilder.TransitiveReduction();
            result6.ShouldBeTrue();

            graphBuilder.EdgeIds.Count().ShouldBe(4);
            graphBuilder.NodeIds.Count().ShouldBe(5);
            graphBuilder.AllDependenciesSatisfied.ShouldBeTrue();

            // First activity.
            graphBuilder.Node(activityId1).Id.ShouldBe(activityId1);
            graphBuilder.Node(activityId1).NodeType.ShouldBe(NodeType.Start);

            graphBuilder.EdgeTailNode(eventId1).ShouldNotBeNull();
            graphBuilder.EdgeTailNode(eventId3).ShouldBeNull();
            graphBuilder.EdgeTailNode(eventId1).Id.ShouldBe(activityId1);
            graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Contains(eventId1).ShouldBeTrue();

            // Second activity.
            graphBuilder.Node(activityId2).Id.ShouldBe(activityId2);
            graphBuilder.Node(activityId2).NodeType.ShouldBe(NodeType.Normal);

            graphBuilder.EdgeTailNode(eventId2).ShouldNotBeNull();
            graphBuilder.EdgeTailNode(eventId4).ShouldBeNull();
            graphBuilder.EdgeTailNode(eventId2).Id.ShouldBe(activityId2);
            graphBuilder.EdgeTailNode(eventId2).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(eventId2).OutgoingEdges.Contains(eventId2).ShouldBeTrue();

            graphBuilder.EdgeTailNode(eventId6).ShouldNotBeNull();
            graphBuilder.EdgeTailNode(eventId6).Id.ShouldBe(activityId5);
            graphBuilder.EdgeHeadNode(eventId6).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(eventId6).IncomingEdges.Contains(eventId6).ShouldBeTrue();

            // Third activity.
            graphBuilder.Node(activityId3).Id.ShouldBe(activityId3);
            graphBuilder.Node(activityId3).NodeType.ShouldBe(NodeType.Normal);

            graphBuilder.EdgeTailNode(eventId5).ShouldNotBeNull();
            graphBuilder.EdgeTailNode(eventId5).Id.ShouldBe(activityId3);
            graphBuilder.EdgeTailNode(eventId5).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(eventId5).OutgoingEdges.Contains(eventId5).ShouldBeTrue();

            graphBuilder.EdgeHeadNode(eventId1).ShouldNotBeNull();
            graphBuilder.EdgeHeadNode(eventId2).ShouldNotBeNull();
            graphBuilder.EdgeHeadNode(eventId7).ShouldBeNull();
            graphBuilder.EdgeHeadNode(eventId1).Id.ShouldBe(activityId3);
            graphBuilder.EdgeHeadNode(eventId2).Id.ShouldBe(activityId3);
            graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeHeadNode(eventId2).IncomingEdges.Count.ShouldBe(2);
            graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Contains(eventId1).ShouldBeTrue();
            graphBuilder.EdgeHeadNode(eventId2).IncomingEdges.Contains(eventId2).ShouldBeTrue();

            // Forth activity.
            graphBuilder.Node(activityId4).Id.ShouldBe(activityId4);
            graphBuilder.Node(activityId4).NodeType.ShouldBe(NodeType.End);

            graphBuilder.EdgeHeadNode(eventId3).ShouldBeNull();
            graphBuilder.EdgeHeadNode(eventId4).ShouldBeNull();
            graphBuilder.EdgeHeadNode(eventId5).ShouldNotBeNull();
            graphBuilder.EdgeHeadNode(eventId5).Id.ShouldBe(activityId4);
            graphBuilder.EdgeHeadNode(eventId5).IncomingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeHeadNode(eventId5).IncomingEdges.Contains(eventId5).ShouldBeTrue();

            // Fifth activity.
            graphBuilder.Node(activityId5).Id.ShouldBe(activityId5);
            graphBuilder.Node(activityId5).NodeType.ShouldBe(NodeType.Start);

            graphBuilder.EdgeTailNode(eventId6).ShouldNotBeNull();
            graphBuilder.EdgeTailNode(eventId7).ShouldBeNull();
            graphBuilder.EdgeTailNode(eventId6).Id.ShouldBe(activityId5);
            graphBuilder.EdgeTailNode(eventId6).OutgoingEdges.Count.ShouldBe(1);
            graphBuilder.EdgeTailNode(eventId6).OutgoingEdges.Contains(eventId6).ShouldBeTrue();
        }

        [Fact]
        public void VertexGraphBuilder_GivenCtorCalledWithNullGraph_ThenShouldThrowArgumentNullException()
        {
            int eventId = 0;
            int activityId1 = 1;
            Action act = () => new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(null, () => eventId = eventId.Next(), () => activityId1++);
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void VertexGraphBuilder_GivenCtorCalledWithGraph_ThenGraphSuccessfullyAssimilated()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int dummyActivityId = activityId5 + 1;
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
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

            var graphBuilder2 = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(firstGraph, () => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());
            var secondGraph = graphBuilder2.ToGraph();
            secondGraph.ShouldBe(firstGraph);
        }

        [Fact]
        public void VertexGraphBuilder_GivenCtorCalledWithGraphWithMissingEdge_ThenShouldThrowArgumentException()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int dummyActivityId = activityId5 + 1;
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
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

            Action act = () => new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(graph, () => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());
            act.ShouldThrow<ArgumentException>();
        }

        [Fact]
        public void VertexGraphBuilder_GivenCtorCalledWithGraphWithTooManyEdges_ThenShouldThrowArgumentException()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int dummyActivityId = activityId5 + 1;
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
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
            graph.Edges.Add(new Edge<int, IEvent<int>>(new Event<int>(eventId = eventId.Next())));

            Action act = () => new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(graph, () => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());
            act.ShouldThrow<ArgumentException>();
        }

        [Fact]
        public void VertexGraphBuilder_GivenCtorCalledWithGraphWithMissingNode_ThenShouldThrowArgumentException()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int dummyActivityId = activityId5 + 1;
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
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
            Node<int, IActivity<int, int, int>> node = graph.Nodes.First(x => x.NodeType == NodeType.Normal);
            graph.Nodes.Remove(node);

            Action act = () => new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(graph, () => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());
            act.ShouldThrow<ArgumentException>();
        }

        [Fact]
        public void VertexGraphBuilder_GivenCtorCalledWithGraphWithTooManyNodes_ThenShouldThrowArgumentException()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int dummyActivityId = activityId5 + 1;
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
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
            graph.Nodes.Add(new Node<int, IActivity<int, int, int>>(new Activity<int, int, int>(dummyActivityId = dummyActivityId.Next(), 0)));

            Action act = () => new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(graph, () => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());
            act.ShouldThrow<ArgumentException>();
        }

        [Fact]
        public void VertexGraphBuilder_GivenCtorCalledWithGraphWithNoStartNode_ThenShouldThrowArgumentException()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int dummyActivityId = activityId5 + 1;
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
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
            foreach (Node<int, IActivity<int, int, int>> node in graph.Nodes.Where(x => x.NodeType == NodeType.Start))
            {
                node.SetNodeType(NodeType.Normal);
            }

            Action act = () => new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(graph, () => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());
            act.ShouldThrow<ArgumentException>();
        }

        [Fact]
        public void VertexGraphBuilder_GivenCtorCalledWithGraphWithNoEndNode_ThenShouldThrowArgumentException()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int dummyActivityId = activityId5 + 1;
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
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
            foreach (Node<int, IActivity<int, int, int>> node in graph.Nodes.Where(x => x.NodeType == NodeType.End))
            {
                node.SetNodeType(NodeType.Normal);
            }

            Action act = () => new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(graph, () => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());
            act.ShouldThrow<ArgumentException>();
        }

        [Fact]
        public void VertexGraphBuilder_GivenCtorCalledWithGraphWithOnlyIsolatedNodes_ThenNoException()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int dummyActivityId = activityId2 + 1;
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.ShouldBeTrue();

            var activity2 = new Activity<int, int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.ShouldBeTrue();

            var graph = graphBuilder.ToGraph();
            var graphBuilder2 = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(graph, () => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());

            graphBuilder2.EdgeIds.Any().ShouldBeFalse();
            graphBuilder2.NodeIds.Count().ShouldBe(2);
            graphBuilder2.AllDependenciesSatisfied.ShouldBeTrue();
            graphBuilder2.StartNodes.Any().ShouldBeFalse();
            graphBuilder2.EndNodes.Any().ShouldBeFalse();
            graphBuilder2.NormalNodes.Any().ShouldBeFalse();
            graphBuilder2.IsolatedNodes.Count().ShouldBe(2);
        }

        [Fact]
        public void VertexGraphBuilder_GivenCtorCalledWithGraphWithUnconnectedStartNode_ThenShouldThrowArgumentException()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int dummyActivityId = activityId5 + 1;
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
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

            Node<int, IActivity<int, int, int>> node = graph.Nodes.First(x => x.NodeType == NodeType.Normal);
            graph.Nodes.Remove(node);

            var newNode = new Node<int, IActivity<int, int, int>>(NodeType.Start, node.Content);
            foreach (int edgeId in node.OutgoingEdges)
            {
                newNode.OutgoingEdges.Add(edgeId);
            }
            graph.Nodes.Add(newNode);

            Action act = () => new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(graph, () => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());
            act.ShouldThrow<ArgumentException>();
        }

        [Fact]
        public void VertexGraphBuilder_GivenCtorCalledWithGraphWithUnconnectedOneEndNode_ThenShouldThrowArgumentException()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int dummyActivityId = activityId5 + 1;
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
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

            Node<int, IActivity<int, int, int>> node = graph.Nodes.First(x => x.NodeType == NodeType.Normal);
            graph.Nodes.Remove(node);

            var newNode = new Node<int, IActivity<int, int, int>>(NodeType.End, node.Content);
            foreach (int edgeId in node.IncomingEdges)
            {
                newNode.IncomingEdges.Add(edgeId);
            }
            graph.Nodes.Add(newNode);

            Action act = () => new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(graph, () => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());
            act.ShouldThrow<ArgumentException>();
        }

        [Fact]
        public void VertexGraphBuilder_GivenAllDummyActivitiesFindCircularDependencies_ThenFindsCircularDependency()
        {
            int eventId = 0;
            int dummyActivityId = 100;
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());
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
        public void VertexGraphBuilder_GivenFindCircularDependencies_ThenFindsCircularDependency()
        {
            int eventId = 0;
            int dummyActivityId = 100;
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());
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
