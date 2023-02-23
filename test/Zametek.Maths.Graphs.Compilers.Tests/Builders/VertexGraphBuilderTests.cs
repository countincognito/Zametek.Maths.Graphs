using FluentAssertions;
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
            var graphBuilder = new VertexGraphBuilder<int, int, IActivity<int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());

            graphBuilder.EdgeIds.Any().Should().BeFalse();
            graphBuilder.NodeIds.Any().Should().BeFalse();
            graphBuilder.AllDependenciesSatisfied.Should().BeTrue();
            graphBuilder.StartNodes.Any().Should().BeFalse();
            graphBuilder.EndNodes.Any().Should().BeFalse();
        }

        [Fact]
        public void VertexGraphBuilder_GivenCtorCalledWithNullEdgeIdGenerator_ThenShouldThrowArgumentNullException()
        {
            int dummyActivityId = 0;
            Action act = () => new VertexGraphBuilder<int, int, IActivity<int, int>>(null, () => dummyActivityId = dummyActivityId.Next());
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void VertexGraphBuilder_GivenCtorCalledWithNullNodeIdGenerator_ThenShouldThrowArgumentNullException()
        {
            int eventId = 0;
            Action act = () => new VertexGraphBuilder<int, int, IActivity<int, int>>(() => eventId = eventId.Next(), null);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void VertexGraphBuilder_GivenSingleActivityNoDependencies_ThenNoStartOrEndNodes()
        {
            int eventId = 0;
            int activityId = 0;
            int activityId1 = activityId + 1;
            int dummyActivityId = activityId1 + 1;
            var graphBuilder = new VertexGraphBuilder<int, int, IActivity<int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };

            var activity = new Activity<int, int>(activityId1, 0);
            bool result = graphBuilder.AddActivity(activity);
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
        public void VertexGraphBuilder_GivenTwoActivitiesOneDependency_ThenActivitiesHookedUpByEdge()
        {
            int eventId = 0;
            int eventId1 = eventId + 1;
            int activityId = 0;
            int activityId1 = activityId + 1;
            int activityId2 = activityId1 + 1;
            int dummyActivityId = activityId2 + 1;
            var graphBuilder = new VertexGraphBuilder<int, int, IActivity<int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.Should().BeTrue();

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

            var activity2 = new Activity<int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2, new HashSet<int> { activityId1 });
            result2.Should().BeTrue();

            graphBuilder.EdgeIds.Count().Should().Be(1);
            graphBuilder.NodeIds.Count().Should().Be(2);
            graphBuilder.AllDependenciesSatisfied.Should().BeTrue();

            graphBuilder.StartNodes.Count().Should().Be(1);
            graphBuilder.EndNodes.Count().Should().Be(1);

            // First activity.
            graphBuilder.StartNodes.First().Id.Should().Be(activityId1);
            graphBuilder.EdgeTailNode(eventId1).Id.Should().Be(activityId1);

            graphBuilder.StartNodes.First().OutgoingEdges.Count.Should().Be(1);
            graphBuilder.StartNodes.First().OutgoingEdges.Contains(eventId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(eventId1).Id.Should().Be(activityId1);
            graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Contains(eventId1).Should().BeTrue();

            // Second activity.

            graphBuilder.EndNodes.First().Id.Should().Be(activityId2);
            graphBuilder.EdgeHeadNode(eventId1).Id.Should().Be(activityId2);

            graphBuilder.EndNodes.First().IncomingEdges.Count.Should().Be(1);
            graphBuilder.EndNodes.First().IncomingEdges.Contains(eventId1).Should().BeTrue();
            graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(eventId1).Id.Should().Be(activityId2);
            graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Contains(eventId1).Should().BeTrue();
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
            var graphBuilder = new VertexGraphBuilder<int, int, IActivity<int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };

            var activity2 = new Activity<int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2, new HashSet<int> { activityId1 });
            result2.Should().BeTrue();

            graphBuilder.EdgeIds.Any().Should().BeFalse();
            graphBuilder.NodeIds.Count().Should().Be(1);
            graphBuilder.AllDependenciesSatisfied.Should().BeFalse();

            graphBuilder.StartNodes.Any().Should().BeFalse();
            graphBuilder.EndNodes.Count().Should().Be(1);

            graphBuilder.EndNodes.First().Id.Should().Be(activityId2);

            graphBuilder.EndNodes.First().IncomingEdges.Any().Should().BeFalse();

            var activity1 = new Activity<int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.Should().BeTrue();

            graphBuilder.EdgeIds.Count().Should().Be(1);
            graphBuilder.NodeIds.Count().Should().Be(2);
            graphBuilder.AllDependenciesSatisfied.Should().BeTrue();

            graphBuilder.StartNodes.Count().Should().Be(1);
            graphBuilder.EndNodes.Count().Should().Be(1);

            // First Activity.
            graphBuilder.StartNodes.First().Id.Should().Be(activityId1);
            graphBuilder.EdgeTailNode(eventId1).Id.Should().Be(activityId1);

            graphBuilder.StartNodes.First().OutgoingEdges.Count.Should().Be(1);
            graphBuilder.StartNodes.First().OutgoingEdges.Contains(eventId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(eventId1).Id.Should().Be(activityId1);
            graphBuilder.EdgeTailNode(eventId1).Should().NotBeNull();
            graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Contains(eventId1).Should().BeTrue();

            // Second Activity.
            graphBuilder.EndNodes.First().Id.Should().Be(activityId2);
            graphBuilder.EdgeHeadNode(eventId1).Id.Should().Be(activityId2);

            graphBuilder.EndNodes.First().IncomingEdges.Count.Should().Be(1);
            graphBuilder.EndNodes.First().IncomingEdges.Contains(eventId1).Should().BeTrue();
            graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(eventId1).Id.Should().Be(activityId2);
            graphBuilder.EdgeHeadNode(eventId1).Should().NotBeNull();
            graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Contains(eventId1).Should().BeTrue();
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
            var graphBuilder = new VertexGraphBuilder<int, int, IActivity<int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.Should().BeTrue();

            graphBuilder.EdgeIds.Any().Should().BeFalse();
            graphBuilder.NodeIds.Count().Should().Be(1);
            graphBuilder.AllDependenciesSatisfied.Should().BeTrue();

            graphBuilder.StartNodes.Any().Should().BeFalse();
            graphBuilder.EndNodes.Any().Should().BeFalse();

            graphBuilder.Node(activityId1).Id.Should().Be(activityId1);
            graphBuilder.Node(activityId1).NodeType.Should().Be(NodeType.Isolated);
            graphBuilder.Activity(activityId1).Id.Should().Be(activityId1);
            graphBuilder.Activities.Count().Should().Be(1);
            graphBuilder.Nodes.Count().Should().Be(1);
            graphBuilder.Events.Any().Should().BeFalse();
            graphBuilder.Edges.Any().Should().BeFalse();

            var activity2 = new Activity<int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.Should().BeTrue();

            graphBuilder.EdgeIds.Any().Should().BeFalse();
            graphBuilder.NodeIds.Count().Should().Be(2);
            graphBuilder.AllDependenciesSatisfied.Should().BeTrue();

            graphBuilder.StartNodes.Any().Should().BeFalse();
            graphBuilder.EndNodes.Any().Should().BeFalse();

            graphBuilder.Node(activityId2).Id.Should().Be(activityId2);
            graphBuilder.Node(activityId2).NodeType.Should().Be(NodeType.Isolated);
            graphBuilder.Activity(activityId2).Id.Should().Be(activityId2);
            graphBuilder.Activities.Count().Should().Be(2);
            graphBuilder.Nodes.Count().Should().Be(2);
            graphBuilder.Events.Any().Should().BeFalse();
            graphBuilder.Edges.Any().Should().BeFalse();

            var activity3 = new Activity<int, int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int> { activityId1, activityId2 });
            result3.Should().BeTrue();

            graphBuilder.EdgeIds.Count().Should().Be(2);
            graphBuilder.NodeIds.Count().Should().Be(3);
            graphBuilder.AllDependenciesSatisfied.Should().BeTrue();

            graphBuilder.StartNodes.Count().Should().Be(2);
            graphBuilder.EndNodes.Count().Should().Be(1);

            graphBuilder.Node(activityId3).Id.Should().Be(activityId3);
            graphBuilder.Node(activityId3).NodeType.Should().Be(NodeType.End);
            graphBuilder.Activity(activityId3).Id.Should().Be(activityId3);
            graphBuilder.Activities.Count().Should().Be(3);
            graphBuilder.Nodes.Count().Should().Be(3);
            graphBuilder.Events.Count().Should().Be(2);
            graphBuilder.Edges.Count().Should().Be(2);

            // First activity.
            graphBuilder.Node(activityId1).Id.Should().Be(activityId1);
            graphBuilder.Node(activityId1).NodeType.Should().Be(NodeType.Start);
            graphBuilder.EdgeTailNode(eventId1).Should().NotBeNull();
            graphBuilder.EdgeTailNode(eventId1).Id.Should().Be(activityId1);
            graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Contains(eventId1).Should().BeTrue();

            // Second activity.
            graphBuilder.Node(activityId2).Id.Should().Be(activityId2);
            graphBuilder.Node(activityId2).NodeType.Should().Be(NodeType.Start);
            graphBuilder.EdgeTailNode(eventId2).Should().NotBeNull();
            graphBuilder.EdgeTailNode(eventId2).Id.Should().Be(activityId2);
            graphBuilder.EdgeTailNode(eventId2).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(eventId2).OutgoingEdges.Contains(eventId2).Should().BeTrue();

            // Third activity.
            graphBuilder.Node(activityId3).Id.Should().Be(activityId3);
            graphBuilder.Node(activityId3).NodeType.Should().Be(NodeType.End);
            graphBuilder.EdgeHeadNode(eventId1).Should().NotBeNull();
            graphBuilder.EdgeHeadNode(eventId2).Should().NotBeNull();
            graphBuilder.EdgeHeadNode(eventId1).Id.Should().Be(activityId3);
            graphBuilder.EdgeHeadNode(eventId2).Id.Should().Be(activityId3);
            graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeHeadNode(eventId2).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Contains(eventId1).Should().BeTrue();
            graphBuilder.EdgeHeadNode(eventId2).IncomingEdges.Contains(eventId2).Should().BeTrue();
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
            var graphBuilder = new VertexGraphBuilder<int, int, IActivity<int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };

            var activity3 = new Activity<int, int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int> { activityId1, activityId2 });
            result3.Should().BeTrue();

            graphBuilder.EdgeIds.Count().Should().Be(0);
            graphBuilder.NodeIds.Count().Should().Be(1);
            graphBuilder.AllDependenciesSatisfied.Should().BeFalse();

            graphBuilder.StartNodes.Any().Should().BeFalse();
            graphBuilder.EndNodes.Count().Should().Be(1);

            graphBuilder.Node(activityId3).Id.Should().Be(activityId3);
            graphBuilder.Node(activityId3).NodeType.Should().Be(NodeType.End);
            graphBuilder.Activity(activityId3).Id.Should().Be(activityId3);
            graphBuilder.Activities.Count().Should().Be(1);
            graphBuilder.Nodes.Count().Should().Be(1);
            graphBuilder.Events.Any().Should().BeFalse();
            graphBuilder.Edges.Any().Should().BeFalse();

            var activity2 = new Activity<int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.Should().BeTrue();

            graphBuilder.EdgeIds.Count().Should().Be(1);
            graphBuilder.NodeIds.Count().Should().Be(2);
            graphBuilder.AllDependenciesSatisfied.Should().BeFalse();

            graphBuilder.StartNodes.Count().Should().Be(1);
            graphBuilder.EndNodes.Count().Should().Be(1);

            graphBuilder.Node(activityId2).Id.Should().Be(activityId2);
            graphBuilder.Node(activityId2).NodeType.Should().Be(NodeType.Start);
            graphBuilder.Activity(activityId2).Id.Should().Be(activityId2);
            graphBuilder.Activities.Count().Should().Be(2);
            graphBuilder.Nodes.Count().Should().Be(2);
            graphBuilder.Events.Count().Should().Be(1);
            graphBuilder.Edges.Count().Should().Be(1);

            var activity1 = new Activity<int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.Should().BeTrue();

            graphBuilder.EdgeIds.Count().Should().Be(2);
            graphBuilder.NodeIds.Count().Should().Be(3);
            graphBuilder.AllDependenciesSatisfied.Should().BeTrue();

            graphBuilder.StartNodes.Count().Should().Be(2);
            graphBuilder.EndNodes.Count().Should().Be(1);

            graphBuilder.Node(activityId1).Id.Should().Be(activityId1);
            graphBuilder.Node(activityId1).NodeType.Should().Be(NodeType.Start);
            graphBuilder.Activity(activityId1).Id.Should().Be(activityId1);
            graphBuilder.Activities.Count().Should().Be(3);
            graphBuilder.Nodes.Count().Should().Be(3);
            graphBuilder.Events.Count().Should().Be(2);
            graphBuilder.Edges.Count().Should().Be(2);

            // First activity.
            graphBuilder.Node(activityId1).Id.Should().Be(activityId1);
            graphBuilder.Node(activityId1).NodeType.Should().Be(NodeType.Start);
            graphBuilder.EdgeTailNode(eventId2).Should().NotBeNull();
            graphBuilder.EdgeTailNode(eventId2).Id.Should().Be(activityId1);
            graphBuilder.EdgeTailNode(eventId2).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(eventId2).OutgoingEdges.Contains(eventId2).Should().BeTrue();

            // Second activity.
            graphBuilder.Node(activityId2).Id.Should().Be(activityId2);
            graphBuilder.Node(activityId2).NodeType.Should().Be(NodeType.Start);
            graphBuilder.EdgeTailNode(eventId1).Should().NotBeNull();
            graphBuilder.EdgeTailNode(eventId1).Id.Should().Be(activityId2);
            graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Contains(eventId1).Should().BeTrue();

            // Third activity.
            graphBuilder.Node(activityId3).Id.Should().Be(activityId3);
            graphBuilder.Node(activityId3).NodeType.Should().Be(NodeType.End);
            graphBuilder.EdgeHeadNode(eventId1).Should().NotBeNull();
            graphBuilder.EdgeHeadNode(eventId2).Should().NotBeNull();
            graphBuilder.EdgeHeadNode(eventId1).Id.Should().Be(activityId3);
            graphBuilder.EdgeHeadNode(eventId2).Id.Should().Be(activityId3);
            graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeHeadNode(eventId2).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Contains(eventId1).Should().BeTrue();
            graphBuilder.EdgeHeadNode(eventId2).IncomingEdges.Contains(eventId2).Should().BeTrue();
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
            var graphBuilder = new VertexGraphBuilder<int, int, IActivity<int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
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
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int> { activityId1, activityId2 });
            result3.Should().BeTrue();

            graphBuilder.Activities.Count().Should().Be(3);
            graphBuilder.Nodes.Count().Should().Be(3);
            graphBuilder.Events.Count().Should().Be(2);
            graphBuilder.Edges.Count().Should().Be(2);

            // First activity.
            graphBuilder.Node(activityId1).Id.Should().Be(activityId1);
            graphBuilder.Node(activityId1).NodeType.Should().Be(NodeType.Start);
            graphBuilder.EdgeTailNode(eventId1).Should().NotBeNull();
            graphBuilder.EdgeTailNode(eventId1).Id.Should().Be(activityId1);
            graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Contains(eventId1).Should().BeTrue();

            // Second activity.
            graphBuilder.Node(activityId2).Id.Should().Be(activityId2);
            graphBuilder.Node(activityId2).NodeType.Should().Be(NodeType.Start);
            graphBuilder.EdgeTailNode(eventId2).Should().NotBeNull();
            graphBuilder.EdgeTailNode(eventId2).Id.Should().Be(activityId2);
            graphBuilder.EdgeTailNode(eventId2).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(eventId2).OutgoingEdges.Contains(eventId2).Should().BeTrue();

            // Third activity.
            graphBuilder.Node(activityId3).Id.Should().Be(activityId3);
            graphBuilder.Node(activityId3).NodeType.Should().Be(NodeType.End);
            graphBuilder.EdgeHeadNode(eventId1).Should().NotBeNull();
            graphBuilder.EdgeHeadNode(eventId2).Should().NotBeNull();
            graphBuilder.EdgeHeadNode(eventId1).Id.Should().Be(activityId3);
            graphBuilder.EdgeHeadNode(eventId2).Id.Should().Be(activityId3);
            graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeHeadNode(eventId2).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Contains(eventId1).Should().BeTrue();
            graphBuilder.EdgeHeadNode(eventId2).IncomingEdges.Contains(eventId2).Should().BeTrue();



            bool result4 = graphBuilder.RemoveActivity(activityId3);
            result4.Should().BeFalse();

            graphBuilder.Activity(activityId3).SetAsRemovable();

            result4 = graphBuilder.RemoveActivity(activityId3);
            result4.Should().BeTrue();

            graphBuilder.Activities.Count().Should().Be(2);
            graphBuilder.Nodes.Count().Should().Be(2);
            graphBuilder.Events.Any().Should().BeFalse();
            graphBuilder.Edges.Any().Should().BeFalse();
            graphBuilder.AllDependenciesSatisfied.Should().BeTrue();

            // First activity.
            graphBuilder.Node(activityId1).Id.Should().Be(activityId1);
            graphBuilder.Node(activityId1).NodeType.Should().Be(NodeType.Isolated);
            graphBuilder.EdgeTailNode(eventId1).Should().BeNull();

            // Second activity.
            graphBuilder.Node(activityId2).Id.Should().Be(activityId2);
            graphBuilder.Node(activityId2).NodeType.Should().Be(NodeType.Isolated);
            graphBuilder.EdgeTailNode(eventId2).Should().BeNull();

            // Third activity.
            graphBuilder.EdgeIds.Contains(activityId3).Should().BeFalse();



            bool result5 = graphBuilder.RemoveActivity(activityId2);
            result5.Should().BeFalse();

            graphBuilder.Activity(activityId2).SetAsRemovable();

            result5 = graphBuilder.RemoveActivity(activityId2);
            result5.Should().BeTrue();

            graphBuilder.Activities.Count().Should().Be(1);
            graphBuilder.Nodes.Count().Should().Be(1);
            graphBuilder.Events.Any().Should().BeFalse();
            graphBuilder.Edges.Any().Should().BeFalse();
            graphBuilder.AllDependenciesSatisfied.Should().BeTrue();

            // First activity.
            graphBuilder.Node(activityId1).Id.Should().Be(activityId1);
            graphBuilder.Node(activityId1).NodeType.Should().Be(NodeType.Isolated);
            graphBuilder.EdgeTailNode(eventId1).Should().BeNull();

            // Second activity.
            graphBuilder.EdgeIds.Contains(activityId2).Should().BeFalse();

            // Third activity.
            graphBuilder.EdgeIds.Contains(activityId3).Should().BeFalse();



            bool result6 = graphBuilder.RemoveActivity(activityId1);
            result6.Should().BeFalse();

            graphBuilder.Activity(activityId1).SetAsRemovable();

            result6 = graphBuilder.RemoveActivity(activityId1);
            result6.Should().BeTrue();

            graphBuilder.Activities.Any().Should().BeFalse();
            graphBuilder.Nodes.Any().Should().BeFalse();
            graphBuilder.Events.Any().Should().BeFalse();
            graphBuilder.Edges.Any().Should().BeFalse();
            graphBuilder.AllDependenciesSatisfied.Should().BeTrue();

            // First activity.
            graphBuilder.EdgeIds.Contains(activityId1).Should().BeFalse();

            // Second activity.
            graphBuilder.EdgeIds.Contains(activityId2).Should().BeFalse();

            // Third activity.
            graphBuilder.EdgeIds.Contains(activityId3).Should().BeFalse();
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
            var graphBuilder = new VertexGraphBuilder<int, int, IActivity<int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
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
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int> { activityId2 });
            result3.Should().BeTrue();

            var activity4 = new Activity<int, int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int> { activityId1, activityId2, activityId3 });
            result4.Should().BeTrue();

            IDictionary<int, HashSet<int>> ancestorNodesLookup = graphBuilder.GetAncestorNodesLookup();

            // Start node (activity 1).
            ancestorNodesLookup[activityId1].Count.Should().Be(0);

            // Start node (activity 2).
            ancestorNodesLookup[activityId2].Count.Should().Be(0);

            // Activity 3.
            HashSet<int> nodeAncestors = ancestorNodesLookup[activityId3];
            nodeAncestors.Count.Should().Be(1);
            nodeAncestors.Contains(activityId2).Should().BeTrue();

            // End node (activity 4).
            HashSet<int> endNodeAncestors = ancestorNodesLookup[activityId4];
            endNodeAncestors.Count.Should().Be(3);
            endNodeAncestors.Contains(activityId1).Should().BeTrue();
            endNodeAncestors.Contains(activityId2).Should().BeTrue();
            endNodeAncestors.Contains(activityId3).Should().BeTrue();
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
            var graphBuilder = new VertexGraphBuilder<int, int, IActivity<int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.Should().BeTrue();

            var activity2 = new Activity<int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2, new HashSet<int> { activityId5 });
            result2.Should().BeTrue();

            var activity3 = new Activity<int, int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int> { activityId1, activityId2, activityId5 });
            result3.Should().BeTrue();

            var activity4 = new Activity<int, int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int> { activityId1, activityId2, activityId3 });
            result4.Should().BeTrue();

            var activity5 = new Activity<int, int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5);
            result5.Should().BeTrue();

            graphBuilder.EdgeIds.Count().Should().Be(7);
            graphBuilder.NodeIds.Count().Should().Be(5);

            // First activity.
            graphBuilder.Node(activityId1).Id.Should().Be(activityId1);
            graphBuilder.Node(activityId1).NodeType.Should().Be(NodeType.Start);

            graphBuilder.EdgeTailNode(eventId1).Should().NotBeNull();
            graphBuilder.EdgeTailNode(eventId3).Should().NotBeNull();
            graphBuilder.EdgeTailNode(eventId1).Id.Should().Be(activityId1);
            graphBuilder.EdgeTailNode(eventId3).Id.Should().Be(activityId1);
            graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(eventId3).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Contains(eventId1).Should().BeTrue();
            graphBuilder.EdgeTailNode(eventId3).OutgoingEdges.Contains(eventId3).Should().BeTrue();

            // Second activity.
            graphBuilder.Node(activityId2).Id.Should().Be(activityId2);
            graphBuilder.Node(activityId2).NodeType.Should().Be(NodeType.Normal);

            graphBuilder.EdgeTailNode(eventId2).Should().NotBeNull();
            graphBuilder.EdgeTailNode(eventId4).Should().NotBeNull();
            graphBuilder.EdgeTailNode(eventId2).Id.Should().Be(activityId2);
            graphBuilder.EdgeTailNode(eventId4).Id.Should().Be(activityId2);
            graphBuilder.EdgeTailNode(eventId2).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(eventId4).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(eventId2).OutgoingEdges.Contains(eventId2).Should().BeTrue();
            graphBuilder.EdgeTailNode(eventId4).OutgoingEdges.Contains(eventId4).Should().BeTrue();

            graphBuilder.EdgeTailNode(eventId6).Should().NotBeNull();
            graphBuilder.EdgeTailNode(eventId6).Id.Should().Be(activityId5);
            graphBuilder.EdgeHeadNode(eventId6).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(eventId6).IncomingEdges.Contains(eventId6).Should().BeTrue();

            // Third activity.
            graphBuilder.Node(activityId3).Id.Should().Be(activityId3);
            graphBuilder.Node(activityId3).NodeType.Should().Be(NodeType.Normal);

            graphBuilder.EdgeTailNode(eventId5).Should().NotBeNull();
            graphBuilder.EdgeTailNode(eventId5).Id.Should().Be(activityId3);
            graphBuilder.EdgeTailNode(eventId5).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(eventId5).OutgoingEdges.Contains(eventId5).Should().BeTrue();

            graphBuilder.EdgeHeadNode(eventId1).Should().NotBeNull();
            graphBuilder.EdgeHeadNode(eventId2).Should().NotBeNull();
            graphBuilder.EdgeHeadNode(eventId7).Should().NotBeNull();
            graphBuilder.EdgeHeadNode(eventId1).Id.Should().Be(activityId3);
            graphBuilder.EdgeHeadNode(eventId2).Id.Should().Be(activityId3);
            graphBuilder.EdgeHeadNode(eventId7).Id.Should().Be(activityId3);
            graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Count.Should().Be(3);
            graphBuilder.EdgeHeadNode(eventId2).IncomingEdges.Count.Should().Be(3);
            graphBuilder.EdgeHeadNode(eventId7).IncomingEdges.Count.Should().Be(3);
            graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Contains(eventId1).Should().BeTrue();
            graphBuilder.EdgeHeadNode(eventId2).IncomingEdges.Contains(eventId2).Should().BeTrue();
            graphBuilder.EdgeHeadNode(eventId7).IncomingEdges.Contains(eventId7).Should().BeTrue();

            // Forth activity.
            graphBuilder.Node(activityId4).Id.Should().Be(activityId4);
            graphBuilder.Node(activityId4).NodeType.Should().Be(NodeType.End);

            graphBuilder.EdgeHeadNode(eventId3).Should().NotBeNull();
            graphBuilder.EdgeHeadNode(eventId4).Should().NotBeNull();
            graphBuilder.EdgeHeadNode(eventId5).Should().NotBeNull();
            graphBuilder.EdgeHeadNode(eventId3).Id.Should().Be(activityId4);
            graphBuilder.EdgeHeadNode(eventId4).Id.Should().Be(activityId4);
            graphBuilder.EdgeHeadNode(eventId5).Id.Should().Be(activityId4);
            graphBuilder.EdgeHeadNode(eventId3).IncomingEdges.Count.Should().Be(3);
            graphBuilder.EdgeHeadNode(eventId4).IncomingEdges.Count.Should().Be(3);
            graphBuilder.EdgeHeadNode(eventId5).IncomingEdges.Count.Should().Be(3);
            graphBuilder.EdgeHeadNode(eventId3).IncomingEdges.Contains(eventId3).Should().BeTrue();
            graphBuilder.EdgeHeadNode(eventId4).IncomingEdges.Contains(eventId4).Should().BeTrue();
            graphBuilder.EdgeHeadNode(eventId5).IncomingEdges.Contains(eventId5).Should().BeTrue();

            // Fifth activity.
            graphBuilder.Node(activityId5).Id.Should().Be(activityId5);
            graphBuilder.Node(activityId5).NodeType.Should().Be(NodeType.Start);

            graphBuilder.EdgeTailNode(eventId6).Should().NotBeNull();
            graphBuilder.EdgeTailNode(eventId7).Should().NotBeNull();
            graphBuilder.EdgeTailNode(eventId6).Id.Should().Be(activityId5);
            graphBuilder.EdgeTailNode(eventId7).Id.Should().Be(activityId5);
            graphBuilder.EdgeTailNode(eventId6).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(eventId7).OutgoingEdges.Count.Should().Be(2);
            graphBuilder.EdgeTailNode(eventId6).OutgoingEdges.Contains(eventId6).Should().BeTrue();
            graphBuilder.EdgeTailNode(eventId7).OutgoingEdges.Contains(eventId7).Should().BeTrue();

            // Transitive Reduction.
            bool result6 = graphBuilder.TransitiveReduction();
            result6.Should().BeTrue();

            graphBuilder.EdgeIds.Count().Should().Be(4);
            graphBuilder.NodeIds.Count().Should().Be(5);
            graphBuilder.AllDependenciesSatisfied.Should().BeTrue();

            // First activity.
            graphBuilder.Node(activityId1).Id.Should().Be(activityId1);
            graphBuilder.Node(activityId1).NodeType.Should().Be(NodeType.Start);

            graphBuilder.EdgeTailNode(eventId1).Should().NotBeNull();
            graphBuilder.EdgeTailNode(eventId3).Should().BeNull();
            graphBuilder.EdgeTailNode(eventId1).Id.Should().Be(activityId1);
            graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Contains(eventId1).Should().BeTrue();

            // Second activity.
            graphBuilder.Node(activityId2).Id.Should().Be(activityId2);
            graphBuilder.Node(activityId2).NodeType.Should().Be(NodeType.Normal);

            graphBuilder.EdgeTailNode(eventId2).Should().NotBeNull();
            graphBuilder.EdgeTailNode(eventId4).Should().BeNull();
            graphBuilder.EdgeTailNode(eventId2).Id.Should().Be(activityId2);
            graphBuilder.EdgeTailNode(eventId2).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(eventId2).OutgoingEdges.Contains(eventId2).Should().BeTrue();

            graphBuilder.EdgeTailNode(eventId6).Should().NotBeNull();
            graphBuilder.EdgeTailNode(eventId6).Id.Should().Be(activityId5);
            graphBuilder.EdgeHeadNode(eventId6).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(eventId6).IncomingEdges.Contains(eventId6).Should().BeTrue();

            // Third activity.
            graphBuilder.Node(activityId3).Id.Should().Be(activityId3);
            graphBuilder.Node(activityId3).NodeType.Should().Be(NodeType.Normal);

            graphBuilder.EdgeTailNode(eventId5).Should().NotBeNull();
            graphBuilder.EdgeTailNode(eventId5).Id.Should().Be(activityId3);
            graphBuilder.EdgeTailNode(eventId5).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(eventId5).OutgoingEdges.Contains(eventId5).Should().BeTrue();

            graphBuilder.EdgeHeadNode(eventId1).Should().NotBeNull();
            graphBuilder.EdgeHeadNode(eventId2).Should().NotBeNull();
            graphBuilder.EdgeHeadNode(eventId7).Should().BeNull();
            graphBuilder.EdgeHeadNode(eventId1).Id.Should().Be(activityId3);
            graphBuilder.EdgeHeadNode(eventId2).Id.Should().Be(activityId3);
            graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeHeadNode(eventId2).IncomingEdges.Count.Should().Be(2);
            graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Contains(eventId1).Should().BeTrue();
            graphBuilder.EdgeHeadNode(eventId2).IncomingEdges.Contains(eventId2).Should().BeTrue();

            // Forth activity.
            graphBuilder.Node(activityId4).Id.Should().Be(activityId4);
            graphBuilder.Node(activityId4).NodeType.Should().Be(NodeType.End);

            graphBuilder.EdgeHeadNode(eventId3).Should().BeNull();
            graphBuilder.EdgeHeadNode(eventId4).Should().BeNull();
            graphBuilder.EdgeHeadNode(eventId5).Should().NotBeNull();
            graphBuilder.EdgeHeadNode(eventId5).Id.Should().Be(activityId4);
            graphBuilder.EdgeHeadNode(eventId5).IncomingEdges.Count.Should().Be(1);
            graphBuilder.EdgeHeadNode(eventId5).IncomingEdges.Contains(eventId5).Should().BeTrue();

            // Fifth activity.
            graphBuilder.Node(activityId5).Id.Should().Be(activityId5);
            graphBuilder.Node(activityId5).NodeType.Should().Be(NodeType.Start);

            graphBuilder.EdgeTailNode(eventId6).Should().NotBeNull();
            graphBuilder.EdgeTailNode(eventId7).Should().BeNull();
            graphBuilder.EdgeTailNode(eventId6).Id.Should().Be(activityId5);
            graphBuilder.EdgeTailNode(eventId6).OutgoingEdges.Count.Should().Be(1);
            graphBuilder.EdgeTailNode(eventId6).OutgoingEdges.Contains(eventId6).Should().BeTrue();
        }

        [Fact]
        public void VertexGraphBuilder_GivenCtorCalledWithNullGraph_ThenShouldThrowArgumentNullException()
        {
            int eventId = 0;
            int activityId1 = 1;
            Action act = () => new VertexGraphBuilder<int, int, IActivity<int, int>>(null, () => eventId = eventId.Next(), () => activityId1++);
            act.Should().Throw<ArgumentNullException>();
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
            var graphBuilder = new VertexGraphBuilder<int, int, IActivity<int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
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
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int> { activityId1, activityId4 });
            result3.Should().BeTrue();

            var activity4 = new Activity<int, int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int> { activityId2 });
            result4.Should().BeTrue();

            var activity5 = new Activity<int, int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int> { activityId1 });
            result5.Should().BeTrue();

            var firstGraph = graphBuilder.ToGraph();

            var graphBuilder2 = new VertexGraphBuilder<int, int, IActivity<int, int>>(firstGraph, () => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());
            var secondGraph = graphBuilder2.ToGraph();
            secondGraph.Should().Be(firstGraph);
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
            var graphBuilder = new VertexGraphBuilder<int, int, IActivity<int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
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
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int> { activityId1, activityId4 });
            result3.Should().BeTrue();

            var activity4 = new Activity<int, int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int> { activityId2 });
            result4.Should().BeTrue();

            var activity5 = new Activity<int, int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int> { activityId1 });
            result5.Should().BeTrue();

            var graph = graphBuilder.ToGraph();
            graph.Edges.RemoveAt(0);

            Action act = () => new VertexGraphBuilder<int, int, IActivity<int, int>>(graph, () => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());
            act.Should().Throw<ArgumentException>();
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
            var graphBuilder = new VertexGraphBuilder<int, int, IActivity<int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
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
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int> { activityId1, activityId4 });
            result3.Should().BeTrue();

            var activity4 = new Activity<int, int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int> { activityId2 });
            result4.Should().BeTrue();

            var activity5 = new Activity<int, int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int> { activityId1 });
            result5.Should().BeTrue();

            var graph = graphBuilder.ToGraph();
            graph.Edges.Add(new Edge<int, IEvent<int>>(new Event<int>(eventId = eventId.Next())));

            Action act = () => new VertexGraphBuilder<int, int, IActivity<int, int>>(graph, () => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());
            act.Should().Throw<ArgumentException>();
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
            var graphBuilder = new VertexGraphBuilder<int, int, IActivity<int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
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
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int> { activityId1, activityId4 });
            result3.Should().BeTrue();

            var activity4 = new Activity<int, int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int> { activityId2 });
            result4.Should().BeTrue();

            var activity5 = new Activity<int, int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int> { activityId1 });
            result5.Should().BeTrue();

            var graph = graphBuilder.ToGraph();
            Node<int, IActivity<int, int>> node = graph.Nodes.First(x => x.NodeType == NodeType.Normal);
            graph.Nodes.Remove(node);

            Action act = () => new VertexGraphBuilder<int, int, IActivity<int, int>>(graph, () => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());
            act.Should().Throw<ArgumentException>();
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
            var graphBuilder = new VertexGraphBuilder<int, int, IActivity<int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
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
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int> { activityId1, activityId4 });
            result3.Should().BeTrue();

            var activity4 = new Activity<int, int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int> { activityId2 });
            result4.Should().BeTrue();

            var activity5 = new Activity<int, int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int> { activityId1 });
            result5.Should().BeTrue();

            var graph = graphBuilder.ToGraph();
            graph.Nodes.Add(new Node<int, IActivity<int, int>>(new Activity<int, int>(dummyActivityId = dummyActivityId.Next(), 0)));

            Action act = () => new VertexGraphBuilder<int, int, IActivity<int, int>>(graph, () => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());
            act.Should().Throw<ArgumentException>();
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
            var graphBuilder = new VertexGraphBuilder<int, int, IActivity<int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
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
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int> { activityId1, activityId4 });
            result3.Should().BeTrue();

            var activity4 = new Activity<int, int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int> { activityId2 });
            result4.Should().BeTrue();

            var activity5 = new Activity<int, int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int> { activityId1 });
            result5.Should().BeTrue();

            var graph = graphBuilder.ToGraph();
            foreach (Node<int, IActivity<int, int>> node in graph.Nodes.Where(x => x.NodeType == NodeType.Start))
            {
                node.SetNodeType(NodeType.Normal);
            }

            Action act = () => new VertexGraphBuilder<int, int, IActivity<int, int>>(graph, () => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());
            act.Should().Throw<ArgumentException>();
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
            var graphBuilder = new VertexGraphBuilder<int, int, IActivity<int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
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
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int> { activityId1, activityId4 });
            result3.Should().BeTrue();

            var activity4 = new Activity<int, int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int> { activityId2 });
            result4.Should().BeTrue();

            var activity5 = new Activity<int, int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int> { activityId1 });
            result5.Should().BeTrue();

            var graph = graphBuilder.ToGraph();
            foreach (Node<int, IActivity<int, int>> node in graph.Nodes.Where(x => x.NodeType == NodeType.End))
            {
                node.SetNodeType(NodeType.Normal);
            }

            Action act = () => new VertexGraphBuilder<int, int, IActivity<int, int>>(graph, () => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void VertexGraphBuilder_GivenCtorCalledWithGraphWithOnlyIsolatedNodes_ThenNoException()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int dummyActivityId = activityId2 + 1;
            var graphBuilder = new VertexGraphBuilder<int, int, IActivity<int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
            {
                WhenTesting = true
            };

            var activity1 = new Activity<int, int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            result1.Should().BeTrue();

            var activity2 = new Activity<int, int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            result2.Should().BeTrue();

            var graph = graphBuilder.ToGraph();
            var graphBuilder2 = new VertexGraphBuilder<int, int, IActivity<int, int>>(graph, () => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());

            graphBuilder2.EdgeIds.Any().Should().BeFalse();
            graphBuilder2.NodeIds.Count().Should().Be(2);
            graphBuilder2.AllDependenciesSatisfied.Should().BeTrue();
            graphBuilder2.StartNodes.Any().Should().BeFalse();
            graphBuilder2.EndNodes.Any().Should().BeFalse();
            graphBuilder2.NormalNodes.Any().Should().BeFalse();
            graphBuilder2.IsolatedNodes.Count().Should().Be(2);
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
            var graphBuilder = new VertexGraphBuilder<int, int, IActivity<int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
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
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int> { activityId1, activityId4 });
            result3.Should().BeTrue();

            var activity4 = new Activity<int, int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int> { activityId2 });
            result4.Should().BeTrue();

            var activity5 = new Activity<int, int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int> { activityId1 });
            result5.Should().BeTrue();

            var graph = graphBuilder.ToGraph();

            Node<int, IActivity<int, int>> node = graph.Nodes.First(x => x.NodeType == NodeType.Normal);
            graph.Nodes.Remove(node);

            var newNode = new Node<int, IActivity<int, int>>(NodeType.Start, node.Content);
            foreach (int edgeId in node.OutgoingEdges)
            {
                newNode.OutgoingEdges.Add(edgeId);
            }
            graph.Nodes.Add(newNode);

            Action act = () => new VertexGraphBuilder<int, int, IActivity<int, int>>(graph, () => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());
            act.Should().Throw<ArgumentException>();
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
            var graphBuilder = new VertexGraphBuilder<int, int, IActivity<int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next())
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
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int> { activityId1, activityId4 });
            result3.Should().BeTrue();

            var activity4 = new Activity<int, int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int> { activityId2 });
            result4.Should().BeTrue();

            var activity5 = new Activity<int, int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int> { activityId1 });
            result5.Should().BeTrue();

            var graph = graphBuilder.ToGraph();

            Node<int, IActivity<int, int>> node = graph.Nodes.First(x => x.NodeType == NodeType.Normal);
            graph.Nodes.Remove(node);

            var newNode = new Node<int, IActivity<int, int>>(NodeType.End, node.Content);
            foreach (int edgeId in node.IncomingEdges)
            {
                newNode.IncomingEdges.Add(edgeId);
            }
            graph.Nodes.Add(newNode);

            Action act = () => new VertexGraphBuilder<int, int, IActivity<int, int>>(graph, () => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void VertexGraphBuilder_GivenAllDummyActivitiesFindCircularDependencies_ThenFindsCircularDependency()
        {
            int eventId = 0;
            int dummyActivityId = 100;
            var graphBuilder = new VertexGraphBuilder<int, int, IActivity<int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());
            graphBuilder.AddActivity(new Activity<int, int>(1, 0));
            graphBuilder.AddActivity(new Activity<int, int>(2, 0), new HashSet<int> { 7 });
            graphBuilder.AddActivity(new Activity<int, int>(3, 0));
            graphBuilder.AddActivity(new Activity<int, int>(4, 0), new HashSet<int> { 2 });
            graphBuilder.AddActivity(new Activity<int, int>(5, 0), new HashSet<int> { 1, 2, 3, 8 });
            graphBuilder.AddActivity(new Activity<int, int>(6, 0), new HashSet<int> { 3 });
            graphBuilder.AddActivity(new Activity<int, int>(7, 0), new HashSet<int> { 4 });
            graphBuilder.AddActivity(new Activity<int, int>(8, 0), new HashSet<int> { 9, 6 });
            graphBuilder.AddActivity(new Activity<int, int>(9, 0), new HashSet<int> { 5 });
            IList<ICircularDependency<int>> circularDependencies = graphBuilder.FindStrongCircularDependencies();

            circularDependencies.Count.Should().Be(2);
            circularDependencies[0].Dependencies.Should().BeEquivalentTo(new int[] { 2, 4, 7 });
            circularDependencies[1].Dependencies.Should().BeEquivalentTo(new int[] { 5, 8, 9 });
        }

        [Fact]
        public void VertexGraphBuilder_GivenFindCircularDependencies_ThenFindsCircularDependency()
        {
            int eventId = 0;
            int dummyActivityId = 100;
            var graphBuilder = new VertexGraphBuilder<int, int, IActivity<int, int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());
            graphBuilder.AddActivity(new Activity<int, int>(1, 10));
            graphBuilder.AddActivity(new Activity<int, int>(2, 10), new HashSet<int> { 7 });
            graphBuilder.AddActivity(new Activity<int, int>(3, 10));
            graphBuilder.AddActivity(new Activity<int, int>(4, 10), new HashSet<int> { 2 });
            graphBuilder.AddActivity(new Activity<int, int>(5, 10), new HashSet<int> { 1, 2, 3, 8 });
            graphBuilder.AddActivity(new Activity<int, int>(6, 10), new HashSet<int> { 3 });
            graphBuilder.AddActivity(new Activity<int, int>(7, 10), new HashSet<int> { 4 });
            graphBuilder.AddActivity(new Activity<int, int>(8, 10), new HashSet<int> { 9, 6 });
            graphBuilder.AddActivity(new Activity<int, int>(9, 10), new HashSet<int> { 5 });
            IList<ICircularDependency<int>> circularDependencies = graphBuilder.FindStrongCircularDependencies();

            circularDependencies.Count.Should().Be(2);
            circularDependencies[0].Dependencies.Should().BeEquivalentTo(new int[] { 2, 4, 7 });
            circularDependencies[1].Dependencies.Should().BeEquivalentTo(new int[] { 5, 8, 9 });
        }
    }
}
