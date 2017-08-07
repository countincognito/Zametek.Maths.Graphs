using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs.Tests
{
    [TestClass]
    public class VertexGraphBuilderTests
    {
        [TestMethod]
        public void VertexGraphBuilder_Contructor_NoException()
        {
            int eventId = 0;
            int dummyActivityId = 0;
            var graphBuilder = new VertexGraphBuilder<int, IActivity<int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());

            Assert.IsFalse(graphBuilder.EdgeIds.Any());
            Assert.IsFalse(graphBuilder.NodeIds.Any());
            Assert.IsTrue(graphBuilder.AllDependenciesSatisfied);
            Assert.IsFalse(graphBuilder.StartNodes.Any());
            Assert.IsFalse(graphBuilder.EndNodes.Any());
        }

        [TestMethod]
        public void VertexGraphBuilder_CtorCalledWithNullEdgeIdGenerator_ShouldThrowArgumentNullException()
        {
            int dummyActivityId = 0;
            Assert.ThrowsException<ArgumentNullException>(
                () => new VertexGraphBuilder<int, IActivity<int>>(null, () => dummyActivityId = dummyActivityId.Next()));
        }

        [TestMethod]
        public void VertexGraphBuilder_CtorCalledWithNullNodeIdGenerator_ShouldThrowArgumentNullException()
        {
            int eventId = 0;
            Assert.ThrowsException<ArgumentNullException>(
                () => new VertexGraphBuilder<int, IActivity<int>>(() => eventId = eventId.Next(), null));
        }

        [TestMethod]
        public void VertexGraphBuilder_SingleActivityNoDependencies_NoStartOrEndNodes()
        {
            int eventId = 0;
            int activityId = 0;
            int activityId1 = activityId + 1;
            int dummyActivityId = activityId1 + 1;
            var graphBuilder = new VertexGraphBuilder<int, IActivity<int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());

            var activity = new Activity<int>(activityId1, 0);
            bool result = graphBuilder.AddActivity(activity);
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

        [TestMethod]
        public void VertexGraphBuilder_TwoActivitiesOneDependency_ActivitiesHookedUpByEdge()
        {
            int eventId = 0;
            int eventId1 = eventId + 1;
            int activityId = 0;
            int activityId1 = activityId + 1;
            int activityId2 = activityId1 + 1;
            int dummyActivityId = activityId2 + 1;
            var graphBuilder = new VertexGraphBuilder<int, IActivity<int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());

            var activity1 = new Activity<int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            Assert.IsTrue(result1);

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

            var activity2 = new Activity<int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2, new HashSet<int>(new[] { activityId1 }));
            Assert.IsTrue(result2);

            Assert.AreEqual(1, graphBuilder.EdgeIds.Count());
            Assert.AreEqual(2, graphBuilder.NodeIds.Count());
            Assert.IsTrue(graphBuilder.AllDependenciesSatisfied);

            Assert.AreEqual(1, graphBuilder.StartNodes.Count());
            Assert.AreEqual(1, graphBuilder.EndNodes.Count());

            // First activity.
            Assert.AreEqual(activityId1, graphBuilder.StartNodes.First().Id);
            Assert.AreEqual(activityId1, graphBuilder.EdgeTailNode(eventId1).Id);

            Assert.AreEqual(1, graphBuilder.StartNodes.First().OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.StartNodes.First().OutgoingEdges.Contains(eventId1));
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Count);
            Assert.AreEqual(activityId1, graphBuilder.EdgeTailNode(eventId1).Id);
            Assert.IsTrue(graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Contains(eventId1));

            // Second activity.

            Assert.AreEqual(activityId2, graphBuilder.EndNodes.First().Id);
            Assert.AreEqual(activityId2, graphBuilder.EdgeHeadNode(eventId1).Id);

            Assert.AreEqual(1, graphBuilder.EndNodes.First().IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EndNodes.First().IncomingEdges.Contains(eventId1));
            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Count);
            Assert.AreEqual(activityId2, graphBuilder.EdgeHeadNode(eventId1).Id);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Contains(eventId1));
        }

        [TestMethod]
        public void VertexGraphBuilder_TwoActivitiesOneDependencyReverseOrder_ActivitiesHookedUpByEdge()
        {
            int eventId = 0;
            int eventId1 = eventId + 1;
            int activityId = 0;
            int activityId1 = activityId + 1;
            int activityId2 = activityId1 + 1;
            int dummyActivityId = activityId2 + 1;
            var graphBuilder = new VertexGraphBuilder<int, IActivity<int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());

            var activity2 = new Activity<int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2, new HashSet<int>(new[] { activityId1 }));
            Assert.IsTrue(result2);

            Assert.IsFalse(graphBuilder.EdgeIds.Any());
            Assert.AreEqual(1, graphBuilder.NodeIds.Count());
            Assert.IsFalse(graphBuilder.AllDependenciesSatisfied);

            Assert.IsFalse(graphBuilder.StartNodes.Any());
            Assert.AreEqual(1, graphBuilder.EndNodes.Count());

            Assert.AreEqual(activityId2, graphBuilder.EndNodes.First().Id);

            Assert.IsFalse(graphBuilder.EndNodes.First().IncomingEdges.Any());

            var activity1 = new Activity<int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            Assert.IsTrue(result1);

            Assert.AreEqual(1, graphBuilder.EdgeIds.Count());
            Assert.AreEqual(2, graphBuilder.NodeIds.Count());
            Assert.IsTrue(graphBuilder.AllDependenciesSatisfied);

            Assert.AreEqual(1, graphBuilder.StartNodes.Count());
            Assert.AreEqual(1, graphBuilder.EndNodes.Count());

            // First Activity.
            Assert.AreEqual(activityId1, graphBuilder.StartNodes.First().Id);
            Assert.AreEqual(activityId1, graphBuilder.EdgeTailNode(eventId1).Id);

            Assert.AreEqual(1, graphBuilder.StartNodes.First().OutgoingEdges.Count());
            Assert.IsTrue(graphBuilder.StartNodes.First().OutgoingEdges.Contains(eventId1));
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Count);
            Assert.AreEqual(activityId1, graphBuilder.EdgeTailNode(eventId1).Id);
            Assert.IsNotNull(graphBuilder.EdgeTailNode(eventId1));
            Assert.IsTrue(graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Contains(eventId1));

            // Second Activity.
            Assert.AreEqual(activityId2, graphBuilder.EndNodes.First().Id);
            Assert.AreEqual(activityId2, graphBuilder.EdgeHeadNode(eventId1).Id);

            Assert.AreEqual(1, graphBuilder.EndNodes.First().IncomingEdges.Count());
            Assert.IsTrue(graphBuilder.EndNodes.First().IncomingEdges.Contains(eventId1));
            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Count);
            Assert.AreEqual(activityId2, graphBuilder.EdgeHeadNode(eventId1).Id);
            Assert.IsNotNull(graphBuilder.EdgeHeadNode(eventId1));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Contains(eventId1));
        }

        [TestMethod]
        public void VertexGraphBuilder_ThreeActivitiesOneDependentOnOtherTwo_DependentActivityHookedUpByTwoEdges()
        {
            int eventId = 0;
            int eventId1 = eventId + 1;
            int eventId2 = eventId1 + 1;
            int activityId = 0;
            int activityId1 = activityId + 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int dummyActivityId = activityId3 + 1;
            var graphBuilder = new VertexGraphBuilder<int, IActivity<int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());

            var activity1 = new Activity<int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            Assert.IsTrue(result1);

            Assert.IsFalse(graphBuilder.EdgeIds.Any());
            Assert.AreEqual(1, graphBuilder.NodeIds.Count());
            Assert.IsTrue(graphBuilder.AllDependenciesSatisfied);

            Assert.IsFalse(graphBuilder.StartNodes.Any());
            Assert.IsFalse(graphBuilder.EndNodes.Any());

            Assert.AreEqual(activityId1, graphBuilder.Node(activityId1).Id);
            Assert.AreEqual(NodeType.Isolated, graphBuilder.Node(activityId1).NodeType);
            Assert.AreEqual(activityId1, graphBuilder.Activity(activityId1).Id);
            Assert.AreEqual(1, graphBuilder.Activities.Count());
            Assert.AreEqual(1, graphBuilder.Nodes.Count());
            Assert.IsFalse(graphBuilder.Events.Any());
            Assert.IsFalse(graphBuilder.Edges.Any());

            var activity2 = new Activity<int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            Assert.IsTrue(result2);

            Assert.IsFalse(graphBuilder.EdgeIds.Any());
            Assert.AreEqual(2, graphBuilder.NodeIds.Count());
            Assert.IsTrue(graphBuilder.AllDependenciesSatisfied);

            Assert.IsFalse(graphBuilder.StartNodes.Any());
            Assert.IsFalse(graphBuilder.EndNodes.Any());

            Assert.AreEqual(activityId2, graphBuilder.Node(activityId2).Id);
            Assert.AreEqual(NodeType.Isolated, graphBuilder.Node(activityId2).NodeType);
            Assert.AreEqual(activityId2, graphBuilder.Activity(activityId2).Id);
            Assert.AreEqual(2, graphBuilder.Activities.Count());
            Assert.AreEqual(2, graphBuilder.Nodes.Count());
            Assert.IsFalse(graphBuilder.Events.Any());
            Assert.IsFalse(graphBuilder.Edges.Any());

            var activity3 = new Activity<int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int>(new[] { activityId1, activityId2 }));
            Assert.IsTrue(result3);

            Assert.AreEqual(2, graphBuilder.EdgeIds.Count());
            Assert.AreEqual(3, graphBuilder.NodeIds.Count());
            Assert.IsTrue(graphBuilder.AllDependenciesSatisfied);

            Assert.AreEqual(2, graphBuilder.StartNodes.Count());
            Assert.AreEqual(1, graphBuilder.EndNodes.Count());

            Assert.AreEqual(activityId3, graphBuilder.Node(activityId3).Id);
            Assert.AreEqual(NodeType.End, graphBuilder.Node(activityId3).NodeType);
            Assert.AreEqual(activityId3, graphBuilder.Activity(activityId3).Id);
            Assert.AreEqual(3, graphBuilder.Activities.Count());
            Assert.AreEqual(3, graphBuilder.Nodes.Count());
            Assert.AreEqual(2, graphBuilder.Events.Count());
            Assert.AreEqual(2, graphBuilder.Edges.Count());

            // First activity.
            Assert.AreEqual(activityId1, graphBuilder.Node(activityId1).Id);
            Assert.AreEqual(NodeType.Start, graphBuilder.Node(activityId1).NodeType);
            Assert.IsNotNull(graphBuilder.EdgeTailNode(eventId1));
            Assert.AreEqual(activityId1, graphBuilder.EdgeTailNode(eventId1).Id);
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Count());
            Assert.IsTrue(graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Contains(eventId1));

            // Second activity.
            Assert.AreEqual(activityId2, graphBuilder.Node(activityId2).Id);
            Assert.AreEqual(NodeType.Start, graphBuilder.Node(activityId2).NodeType);
            Assert.IsNotNull(graphBuilder.EdgeTailNode(eventId2));
            Assert.AreEqual(activityId2, graphBuilder.EdgeTailNode(eventId2).Id);
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(eventId2).OutgoingEdges.Count());
            Assert.IsTrue(graphBuilder.EdgeTailNode(eventId2).OutgoingEdges.Contains(eventId2));

            // Third activity.
            Assert.AreEqual(activityId3, graphBuilder.Node(activityId3).Id);
            Assert.AreEqual(NodeType.End, graphBuilder.Node(activityId3).NodeType);
            Assert.IsNotNull(graphBuilder.EdgeHeadNode(eventId1));
            Assert.IsNotNull(graphBuilder.EdgeHeadNode(eventId2));
            Assert.AreEqual(activityId3, graphBuilder.EdgeHeadNode(eventId1).Id);
            Assert.AreEqual(activityId3, graphBuilder.EdgeHeadNode(eventId2).Id);
            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Count());
            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(eventId2).IncomingEdges.Count());
            Assert.IsTrue(graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Contains(eventId1));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(eventId2).IncomingEdges.Contains(eventId2));
        }

        [TestMethod]
        public void VertexGraphBuilder_ThreeActivitiesOneDependentOnOtherTwoReverseOrder_DependentActivityHookedUpByTwoEdges()
        {
            int eventId = 0;
            int eventId1 = eventId + 1;
            int eventId2 = eventId1 + 1;
            int activityId = 0;
            int activityId1 = activityId + 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int dummyActivityId = activityId3 + 1;
            var graphBuilder = new VertexGraphBuilder<int, IActivity<int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());

            var activity3 = new Activity<int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int>(new[] { activityId1, activityId2 }));
            Assert.IsTrue(result3);

            Assert.AreEqual(0, graphBuilder.EdgeIds.Count());
            Assert.AreEqual(1, graphBuilder.NodeIds.Count());
            Assert.IsFalse(graphBuilder.AllDependenciesSatisfied);

            Assert.IsFalse(graphBuilder.StartNodes.Any());
            Assert.AreEqual(1, graphBuilder.EndNodes.Count());

            Assert.AreEqual(activityId3, graphBuilder.Node(activityId3).Id);
            Assert.AreEqual(NodeType.End, graphBuilder.Node(activityId3).NodeType);
            Assert.AreEqual(activityId3, graphBuilder.Activity(activityId3).Id);
            Assert.AreEqual(1, graphBuilder.Activities.Count());
            Assert.AreEqual(1, graphBuilder.Nodes.Count());
            Assert.IsFalse(graphBuilder.Events.Any());
            Assert.IsFalse(graphBuilder.Edges.Any());

            var activity2 = new Activity<int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            Assert.IsTrue(result2);

            Assert.AreEqual(1, graphBuilder.EdgeIds.Count());
            Assert.AreEqual(2, graphBuilder.NodeIds.Count());
            Assert.IsFalse(graphBuilder.AllDependenciesSatisfied);

            Assert.AreEqual(1, graphBuilder.StartNodes.Count());
            Assert.AreEqual(1, graphBuilder.EndNodes.Count());

            Assert.AreEqual(activityId2, graphBuilder.Node(activityId2).Id);
            Assert.AreEqual(NodeType.Start, graphBuilder.Node(activityId2).NodeType);
            Assert.AreEqual(activityId2, graphBuilder.Activity(activityId2).Id);
            Assert.AreEqual(2, graphBuilder.Activities.Count());
            Assert.AreEqual(2, graphBuilder.Nodes.Count());
            Assert.AreEqual(1, graphBuilder.Events.Count());
            Assert.AreEqual(1, graphBuilder.Edges.Count());

            var activity1 = new Activity<int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            Assert.IsTrue(result1);

            Assert.AreEqual(2, graphBuilder.EdgeIds.Count());
            Assert.AreEqual(3, graphBuilder.NodeIds.Count());
            Assert.IsTrue(graphBuilder.AllDependenciesSatisfied);

            Assert.AreEqual(2, graphBuilder.StartNodes.Count());
            Assert.AreEqual(1, graphBuilder.EndNodes.Count());

            Assert.AreEqual(activityId1, graphBuilder.Node(activityId1).Id);
            Assert.AreEqual(NodeType.Start, graphBuilder.Node(activityId1).NodeType);
            Assert.AreEqual(activityId1, graphBuilder.Activity(activityId1).Id);
            Assert.AreEqual(3, graphBuilder.Activities.Count());
            Assert.AreEqual(3, graphBuilder.Nodes.Count());
            Assert.AreEqual(2, graphBuilder.Events.Count());
            Assert.AreEqual(2, graphBuilder.Edges.Count());

            // First activity.
            Assert.AreEqual(activityId1, graphBuilder.Node(activityId1).Id);
            Assert.AreEqual(NodeType.Start, graphBuilder.Node(activityId1).NodeType);
            Assert.IsNotNull(graphBuilder.EdgeTailNode(eventId2));
            Assert.AreEqual(activityId1, graphBuilder.EdgeTailNode(eventId2).Id);
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(eventId2).OutgoingEdges.Count());
            Assert.IsTrue(graphBuilder.EdgeTailNode(eventId2).OutgoingEdges.Contains(eventId2));

            // Second activity.
            Assert.AreEqual(activityId2, graphBuilder.Node(activityId2).Id);
            Assert.AreEqual(NodeType.Start, graphBuilder.Node(activityId2).NodeType);
            Assert.IsNotNull(graphBuilder.EdgeTailNode(eventId1));
            Assert.AreEqual(activityId2, graphBuilder.EdgeTailNode(eventId1).Id);
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Count());
            Assert.IsTrue(graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Contains(eventId1));

            // Third activity.
            Assert.AreEqual(activityId3, graphBuilder.Node(activityId3).Id);
            Assert.AreEqual(NodeType.End, graphBuilder.Node(activityId3).NodeType);
            Assert.IsNotNull(graphBuilder.EdgeHeadNode(eventId1));
            Assert.IsNotNull(graphBuilder.EdgeHeadNode(eventId2));
            Assert.AreEqual(activityId3, graphBuilder.EdgeHeadNode(eventId1).Id);
            Assert.AreEqual(activityId3, graphBuilder.EdgeHeadNode(eventId2).Id);
            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Count());
            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(eventId2).IncomingEdges.Count());
            Assert.IsTrue(graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Contains(eventId1));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(eventId2).IncomingEdges.Contains(eventId2));
        }

        [TestMethod]
        public void VertexGraphBuilder_ThreeActivitiesOneDependentOnOtherTwoRemovedInStages_StructureAsExpected()
        {
            int eventId = 0;
            int eventId1 = eventId + 1;
            int eventId2 = eventId1 + 1;
            int activityId = 0;
            int activityId1 = activityId + 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int dummyActivityId = activityId3 + 1;
            var graphBuilder = new VertexGraphBuilder<int, IActivity<int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());

            var activity1 = new Activity<int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            Assert.IsTrue(result1);

            var activity2 = new Activity<int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            Assert.IsTrue(result2);

            var activity3 = new Activity<int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int>(new[] { activityId1, activityId2 }));
            Assert.IsTrue(result3);

            Assert.AreEqual(3, graphBuilder.Activities.Count());
            Assert.AreEqual(3, graphBuilder.Nodes.Count());
            Assert.AreEqual(2, graphBuilder.Events.Count());
            Assert.AreEqual(2, graphBuilder.Edges.Count());

            // First activity.
            Assert.AreEqual(activityId1, graphBuilder.Node(activityId1).Id);
            Assert.AreEqual(NodeType.Start, graphBuilder.Node(activityId1).NodeType);
            Assert.IsNotNull(graphBuilder.EdgeTailNode(eventId1));
            Assert.AreEqual(activityId1, graphBuilder.EdgeTailNode(eventId1).Id);
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Count());
            Assert.IsTrue(graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Contains(eventId1));

            // Second activity.
            Assert.AreEqual(activityId2, graphBuilder.Node(activityId2).Id);
            Assert.AreEqual(NodeType.Start, graphBuilder.Node(activityId2).NodeType);
            Assert.IsNotNull(graphBuilder.EdgeTailNode(eventId2));
            Assert.AreEqual(activityId2, graphBuilder.EdgeTailNode(eventId2).Id);
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(eventId2).OutgoingEdges.Count());
            Assert.IsTrue(graphBuilder.EdgeTailNode(eventId2).OutgoingEdges.Contains(eventId2));

            // Third activity.
            Assert.AreEqual(activityId3, graphBuilder.Node(activityId3).Id);
            Assert.AreEqual(NodeType.End, graphBuilder.Node(activityId3).NodeType);
            Assert.IsNotNull(graphBuilder.EdgeHeadNode(eventId1));
            Assert.IsNotNull(graphBuilder.EdgeHeadNode(eventId2));
            Assert.AreEqual(activityId3, graphBuilder.EdgeHeadNode(eventId1).Id);
            Assert.AreEqual(activityId3, graphBuilder.EdgeHeadNode(eventId2).Id);
            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Count());
            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(eventId2).IncomingEdges.Count());
            Assert.IsTrue(graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Contains(eventId1));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(eventId2).IncomingEdges.Contains(eventId2));



            bool result4 = graphBuilder.RemoveActivity(activityId3);
            Assert.IsFalse(result4);

            graphBuilder.Activity(activityId3).SetAsRemovable();

            result4 = graphBuilder.RemoveActivity(activityId3);
            Assert.IsTrue(result4);

            Assert.AreEqual(2, graphBuilder.Activities.Count());
            Assert.AreEqual(2, graphBuilder.Nodes.Count());
            Assert.IsFalse(graphBuilder.Events.Any());
            Assert.IsFalse(graphBuilder.Edges.Any());
            Assert.IsTrue(graphBuilder.AllDependenciesSatisfied);

            // First activity.
            Assert.AreEqual(activityId1, graphBuilder.Node(activityId1).Id);
            Assert.AreEqual(NodeType.Isolated, graphBuilder.Node(activityId1).NodeType);
            Assert.IsNull(graphBuilder.EdgeTailNode(eventId1));

            // Second activity.
            Assert.AreEqual(activityId2, graphBuilder.Node(activityId2).Id);
            Assert.AreEqual(NodeType.Isolated, graphBuilder.Node(activityId2).NodeType);
            Assert.IsNull(graphBuilder.EdgeTailNode(eventId2));

            // Third activity.
            Assert.IsFalse(graphBuilder.EdgeIds.Contains(activityId3));



            bool result5 = graphBuilder.RemoveActivity(activityId2);
            Assert.IsFalse(result5);

            graphBuilder.Activity(activityId2).SetAsRemovable();

            result5 = graphBuilder.RemoveActivity(activityId2);
            Assert.IsTrue(result5);

            Assert.AreEqual(1, graphBuilder.Activities.Count());
            Assert.AreEqual(1, graphBuilder.Nodes.Count());
            Assert.IsFalse(graphBuilder.Events.Any());
            Assert.IsFalse(graphBuilder.Edges.Any());
            Assert.IsTrue(graphBuilder.AllDependenciesSatisfied);

            // First activity.
            Assert.AreEqual(activityId1, graphBuilder.Node(activityId1).Id);
            Assert.AreEqual(NodeType.Isolated, graphBuilder.Node(activityId1).NodeType);
            Assert.IsNull(graphBuilder.EdgeTailNode(eventId1));

            // Second activity.
            Assert.IsFalse(graphBuilder.EdgeIds.Contains(activityId2));

            // Third activity.
            Assert.IsFalse(graphBuilder.EdgeIds.Contains(activityId3));



            bool result6 = graphBuilder.RemoveActivity(activityId1);
            Assert.IsFalse(result6);

            graphBuilder.Activity(activityId1).SetAsRemovable();

            result6 = graphBuilder.RemoveActivity(activityId1);
            Assert.IsTrue(result6);

            Assert.IsFalse(graphBuilder.Activities.Any());
            Assert.IsFalse(graphBuilder.Nodes.Any());
            Assert.IsFalse(graphBuilder.Events.Any());
            Assert.IsFalse(graphBuilder.Edges.Any());
            Assert.IsTrue(graphBuilder.AllDependenciesSatisfied);

            // First activity.
            Assert.IsFalse(graphBuilder.EdgeIds.Contains(activityId1));

            // Second activity.
            Assert.IsFalse(graphBuilder.EdgeIds.Contains(activityId2));

            // Third activity.
            Assert.IsFalse(graphBuilder.EdgeIds.Contains(activityId3));
        }

        [TestMethod]
        public void VertexGraphBuilder_FourActivitiesOneDependentOnOtherThreeGetAncestorNodesLookup_AncestorsAsExpected()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int dummyActivityId = activityId4 + 1;
            var graphBuilder = new VertexGraphBuilder<int, IActivity<int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());

            var activity1 = new Activity<int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            Assert.IsTrue(result1);

            var activity2 = new Activity<int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            Assert.IsTrue(result2);

            var activity3 = new Activity<int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int>(new[] { activityId2 }));
            Assert.IsTrue(result3);

            var activity4 = new Activity<int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int>(new[] { activityId1, activityId2, activityId3 }));
            Assert.IsTrue(result4);

            IDictionary<int, HashSet<int>> ancestorNodesLookup = graphBuilder.GetAncestorNodesLookup();

            // Start node (activity 1).
            Assert.AreEqual(0, ancestorNodesLookup[activityId1].Count);

            // Start node (activity 2).
            Assert.AreEqual(0, ancestorNodesLookup[activityId2].Count);

            // Activity 3.
            HashSet<int> nodeAncestors = ancestorNodesLookup[activityId3];
            Assert.AreEqual(1, nodeAncestors.Count);
            Assert.IsTrue(nodeAncestors.Contains(activityId2));

            // End node (activity 4).
            HashSet<int> endNodeAncestors = ancestorNodesLookup[activityId4];
            Assert.AreEqual(3, endNodeAncestors.Count);
            Assert.IsTrue(endNodeAncestors.Contains(activityId1));
            Assert.IsTrue(endNodeAncestors.Contains(activityId2));
            Assert.IsTrue(endNodeAncestors.Contains(activityId3));
        }

        [TestMethod]
        public void VertexGraphBuilder_FiveActivitiesWithTwoUnnecessaryDependencies_TransitiveReductionAsExpected()
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
            var graphBuilder = new VertexGraphBuilder<int, IActivity<int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());

            var activity1 = new Activity<int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            Assert.IsTrue(result1);

            var activity2 = new Activity<int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2, new HashSet<int>(new[] { activityId5 }));
            Assert.IsTrue(result2);

            var activity3 = new Activity<int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int>(new[] { activityId1, activityId2, activityId5 }));
            Assert.IsTrue(result3);

            var activity4 = new Activity<int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int>(new[] { activityId1, activityId2, activityId3 }));
            Assert.IsTrue(result4);

            var activity5 = new Activity<int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5);
            Assert.IsTrue(result5);

            Assert.AreEqual(7, graphBuilder.EdgeIds.Count());
            Assert.AreEqual(5, graphBuilder.NodeIds.Count());

            // First activity.
            Assert.AreEqual(activityId1, graphBuilder.Node(activityId1).Id);
            Assert.AreEqual(NodeType.Start, graphBuilder.Node(activityId1).NodeType);

            Assert.IsNotNull(graphBuilder.EdgeTailNode(eventId1));
            Assert.IsNotNull(graphBuilder.EdgeTailNode(eventId3));
            Assert.AreEqual(activityId1, graphBuilder.EdgeTailNode(eventId1).Id);
            Assert.AreEqual(activityId1, graphBuilder.EdgeTailNode(eventId3).Id);
            Assert.AreEqual(2, graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Count());
            Assert.AreEqual(2, graphBuilder.EdgeTailNode(eventId3).OutgoingEdges.Count());
            Assert.IsTrue(graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Contains(eventId1));
            Assert.IsTrue(graphBuilder.EdgeTailNode(eventId3).OutgoingEdges.Contains(eventId3));

            // Second activity.
            Assert.AreEqual(activityId2, graphBuilder.Node(activityId2).Id);
            Assert.AreEqual(NodeType.Normal, graphBuilder.Node(activityId2).NodeType);

            Assert.IsNotNull(graphBuilder.EdgeTailNode(eventId2));
            Assert.IsNotNull(graphBuilder.EdgeTailNode(eventId4));
            Assert.AreEqual(activityId2, graphBuilder.EdgeTailNode(eventId2).Id);
            Assert.AreEqual(activityId2, graphBuilder.EdgeTailNode(eventId4).Id);
            Assert.AreEqual(2, graphBuilder.EdgeTailNode(eventId2).OutgoingEdges.Count());
            Assert.AreEqual(2, graphBuilder.EdgeTailNode(eventId4).OutgoingEdges.Count());
            Assert.IsTrue(graphBuilder.EdgeTailNode(eventId2).OutgoingEdges.Contains(eventId2));
            Assert.IsTrue(graphBuilder.EdgeTailNode(eventId4).OutgoingEdges.Contains(eventId4));

            Assert.IsNotNull(graphBuilder.EdgeTailNode(eventId6));
            Assert.AreEqual(activityId5, graphBuilder.EdgeTailNode(eventId6).Id);
            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(eventId6).IncomingEdges.Count());
            Assert.IsTrue(graphBuilder.EdgeHeadNode(eventId6).IncomingEdges.Contains(eventId6));

            // Third activity.
            Assert.AreEqual(activityId3, graphBuilder.Node(activityId3).Id);
            Assert.AreEqual(NodeType.Normal, graphBuilder.Node(activityId3).NodeType);

            Assert.IsNotNull(graphBuilder.EdgeTailNode(eventId5));
            Assert.AreEqual(activityId3, graphBuilder.EdgeTailNode(eventId5).Id);
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(eventId5).OutgoingEdges.Count());
            Assert.IsTrue(graphBuilder.EdgeTailNode(eventId5).OutgoingEdges.Contains(eventId5));

            Assert.IsNotNull(graphBuilder.EdgeHeadNode(eventId1));
            Assert.IsNotNull(graphBuilder.EdgeHeadNode(eventId2));
            Assert.IsNotNull(graphBuilder.EdgeHeadNode(eventId7));
            Assert.AreEqual(activityId3, graphBuilder.EdgeHeadNode(eventId1).Id);
            Assert.AreEqual(activityId3, graphBuilder.EdgeHeadNode(eventId2).Id);
            Assert.AreEqual(activityId3, graphBuilder.EdgeHeadNode(eventId7).Id);
            Assert.AreEqual(3, graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Count());
            Assert.AreEqual(3, graphBuilder.EdgeHeadNode(eventId2).IncomingEdges.Count());
            Assert.AreEqual(3, graphBuilder.EdgeHeadNode(eventId7).IncomingEdges.Count());
            Assert.IsTrue(graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Contains(eventId1));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(eventId2).IncomingEdges.Contains(eventId2));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(eventId7).IncomingEdges.Contains(eventId7));

            // Forth activity.
            Assert.AreEqual(activityId4, graphBuilder.Node(activityId4).Id);
            Assert.AreEqual(NodeType.End, graphBuilder.Node(activityId4).NodeType);

            Assert.IsNotNull(graphBuilder.EdgeHeadNode(eventId3));
            Assert.IsNotNull(graphBuilder.EdgeHeadNode(eventId4));
            Assert.IsNotNull(graphBuilder.EdgeHeadNode(eventId5));
            Assert.AreEqual(activityId4, graphBuilder.EdgeHeadNode(eventId3).Id);
            Assert.AreEqual(activityId4, graphBuilder.EdgeHeadNode(eventId4).Id);
            Assert.AreEqual(activityId4, graphBuilder.EdgeHeadNode(eventId5).Id);
            Assert.AreEqual(3, graphBuilder.EdgeHeadNode(eventId3).IncomingEdges.Count());
            Assert.AreEqual(3, graphBuilder.EdgeHeadNode(eventId4).IncomingEdges.Count());
            Assert.AreEqual(3, graphBuilder.EdgeHeadNode(eventId5).IncomingEdges.Count());
            Assert.IsTrue(graphBuilder.EdgeHeadNode(eventId3).IncomingEdges.Contains(eventId3));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(eventId4).IncomingEdges.Contains(eventId4));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(eventId5).IncomingEdges.Contains(eventId5));

            // Fifth activity.
            Assert.AreEqual(activityId5, graphBuilder.Node(activityId5).Id);
            Assert.AreEqual(NodeType.Start, graphBuilder.Node(activityId5).NodeType);

            Assert.IsNotNull(graphBuilder.EdgeTailNode(eventId6));
            Assert.IsNotNull(graphBuilder.EdgeTailNode(eventId7));
            Assert.AreEqual(activityId5, graphBuilder.EdgeTailNode(eventId6).Id);
            Assert.AreEqual(activityId5, graphBuilder.EdgeTailNode(eventId7).Id);
            Assert.AreEqual(2, graphBuilder.EdgeTailNode(eventId6).OutgoingEdges.Count());
            Assert.AreEqual(2, graphBuilder.EdgeTailNode(eventId7).OutgoingEdges.Count());
            Assert.IsTrue(graphBuilder.EdgeTailNode(eventId6).OutgoingEdges.Contains(eventId6));
            Assert.IsTrue(graphBuilder.EdgeTailNode(eventId7).OutgoingEdges.Contains(eventId7));

            // Transitive Reduction.
            bool result6 = graphBuilder.TransitiveReduction();
            Assert.IsTrue(result6);

            Assert.AreEqual(4, graphBuilder.EdgeIds.Count());
            Assert.AreEqual(5, graphBuilder.NodeIds.Count());
            Assert.IsTrue(graphBuilder.AllDependenciesSatisfied);

            // First activity.
            Assert.AreEqual(activityId1, graphBuilder.Node(activityId1).Id);
            Assert.AreEqual(NodeType.Start, graphBuilder.Node(activityId1).NodeType);

            Assert.IsNotNull(graphBuilder.EdgeTailNode(eventId1));
            Assert.IsNull(graphBuilder.EdgeTailNode(eventId3));
            Assert.AreEqual(activityId1, graphBuilder.EdgeTailNode(eventId1).Id);
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Count());
            Assert.IsTrue(graphBuilder.EdgeTailNode(eventId1).OutgoingEdges.Contains(eventId1));

            // Second activity.
            Assert.AreEqual(activityId2, graphBuilder.Node(activityId2).Id);
            Assert.AreEqual(NodeType.Normal, graphBuilder.Node(activityId2).NodeType);

            Assert.IsNotNull(graphBuilder.EdgeTailNode(eventId2));
            Assert.IsNull(graphBuilder.EdgeTailNode(eventId4));
            Assert.AreEqual(activityId2, graphBuilder.EdgeTailNode(eventId2).Id);
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(eventId2).OutgoingEdges.Count());
            Assert.IsTrue(graphBuilder.EdgeTailNode(eventId2).OutgoingEdges.Contains(eventId2));

            Assert.IsNotNull(graphBuilder.EdgeTailNode(eventId6));
            Assert.AreEqual(activityId5, graphBuilder.EdgeTailNode(eventId6).Id);
            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(eventId6).IncomingEdges.Count());
            Assert.IsTrue(graphBuilder.EdgeHeadNode(eventId6).IncomingEdges.Contains(eventId6));

            // Third activity.
            Assert.AreEqual(activityId3, graphBuilder.Node(activityId3).Id);
            Assert.AreEqual(NodeType.Normal, graphBuilder.Node(activityId3).NodeType);

            Assert.IsNotNull(graphBuilder.EdgeTailNode(eventId5));
            Assert.AreEqual(activityId3, graphBuilder.EdgeTailNode(eventId5).Id);
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(eventId5).OutgoingEdges.Count());
            Assert.IsTrue(graphBuilder.EdgeTailNode(eventId5).OutgoingEdges.Contains(eventId5));

            Assert.IsNotNull(graphBuilder.EdgeHeadNode(eventId1));
            Assert.IsNotNull(graphBuilder.EdgeHeadNode(eventId2));
            Assert.IsNull(graphBuilder.EdgeHeadNode(eventId7));
            Assert.AreEqual(activityId3, graphBuilder.EdgeHeadNode(eventId1).Id);
            Assert.AreEqual(activityId3, graphBuilder.EdgeHeadNode(eventId2).Id);
            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Count());
            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(eventId2).IncomingEdges.Count());
            Assert.IsTrue(graphBuilder.EdgeHeadNode(eventId1).IncomingEdges.Contains(eventId1));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(eventId2).IncomingEdges.Contains(eventId2));

            // Forth activity.
            Assert.AreEqual(activityId4, graphBuilder.Node(activityId4).Id);
            Assert.AreEqual(NodeType.End, graphBuilder.Node(activityId4).NodeType);

            Assert.IsNull(graphBuilder.EdgeHeadNode(eventId3));
            Assert.IsNull(graphBuilder.EdgeHeadNode(eventId4));
            Assert.IsNotNull(graphBuilder.EdgeHeadNode(eventId5));
            Assert.AreEqual(activityId4, graphBuilder.EdgeHeadNode(eventId5).Id);
            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(eventId5).IncomingEdges.Count());
            Assert.IsTrue(graphBuilder.EdgeHeadNode(eventId5).IncomingEdges.Contains(eventId5));

            // Fifth activity.
            Assert.AreEqual(activityId5, graphBuilder.Node(activityId5).Id);
            Assert.AreEqual(NodeType.Start, graphBuilder.Node(activityId5).NodeType);

            Assert.IsNotNull(graphBuilder.EdgeTailNode(eventId6));
            Assert.IsNull(graphBuilder.EdgeTailNode(eventId7));
            Assert.AreEqual(activityId5, graphBuilder.EdgeTailNode(eventId6).Id);
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(eventId6).OutgoingEdges.Count());
            Assert.IsTrue(graphBuilder.EdgeTailNode(eventId6).OutgoingEdges.Contains(eventId6));
        }

        [TestMethod]
        public void VertexGraphBuilder_CtorCalledWithNullGraph_ShouldThrowArgumentNullException()
        {
            int eventId = 0;
            int activityId1 = 1;
            Assert.ThrowsException<ArgumentNullException>(
                () => new VertexGraphBuilder<int, IActivity<int>>(null, () => eventId = eventId.Next(), () => activityId1++));
        }

        [TestMethod]
        public void VertexGraphBuilder_CtorCalledWithGraph_GraphSuccessfullyAssimilated()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int dummyActivityId = activityId5 + 1;
            var graphBuilder = new VertexGraphBuilder<int, IActivity<int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());

            var activity1 = new Activity<int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            Assert.IsTrue(result1);

            var activity2 = new Activity<int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            Assert.IsTrue(result2);

            var activity3 = new Activity<int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int>(new[] { activityId1, activityId4 }));
            Assert.IsTrue(result3);

            var activity4 = new Activity<int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int>(new[] { activityId2 }));
            Assert.IsTrue(result4);

            var activity5 = new Activity<int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int>(new[] { activityId1 }));
            Assert.IsTrue(result5);

            var firstGraph = graphBuilder.ToGraph();

            var graphBuilder2 = new VertexGraphBuilder<int, IActivity<int>>(firstGraph, () => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());
            var secondGraph = graphBuilder2.ToGraph();
            Assert.AreEqual(firstGraph, secondGraph);
        }

        [TestMethod]
        public void VertexGraphBuilder_CtorCalledWithGraphWithMissingEdge_ShouldThrowArgumentException()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int dummyActivityId = activityId5 + 1;
            var graphBuilder = new VertexGraphBuilder<int, IActivity<int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());

            var activity1 = new Activity<int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            Assert.IsTrue(result1);

            var activity2 = new Activity<int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            Assert.IsTrue(result2);

            var activity3 = new Activity<int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int>(new[] { activityId1, activityId4 }));
            Assert.IsTrue(result3);

            var activity4 = new Activity<int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int>(new[] { activityId2 }));
            Assert.IsTrue(result4);

            var activity5 = new Activity<int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int>(new[] { activityId1 }));
            Assert.IsTrue(result5);

            var graph = graphBuilder.ToGraph();
            graph.Edges.RemoveAt(0);

            Assert.ThrowsException<ArgumentException>(
                () => new VertexGraphBuilder<int, IActivity<int>>(graph, () => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next()));
        }

        [TestMethod]
        public void VertexGraphBuilder_CtorCalledWithGraphWithTooManyEdges_ShouldThrowArgumentException()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int dummyActivityId = activityId5 + 1;
            var graphBuilder = new VertexGraphBuilder<int, IActivity<int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());

            var activity1 = new Activity<int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            Assert.IsTrue(result1);

            var activity2 = new Activity<int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            Assert.IsTrue(result2);

            var activity3 = new Activity<int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int>(new[] { activityId1, activityId4 }));
            Assert.IsTrue(result3);

            var activity4 = new Activity<int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int>(new[] { activityId2 }));
            Assert.IsTrue(result4);

            var activity5 = new Activity<int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int>(new[] { activityId1 }));
            Assert.IsTrue(result5);

            var graph = graphBuilder.ToGraph();
            graph.Edges.Add(new Edge<int, IEvent<int>>(new Event<int>(eventId = eventId.Next())));

            Assert.ThrowsException<ArgumentException>(
                () => new VertexGraphBuilder<int, IActivity<int>>(graph, () => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next()));
        }

        [TestMethod]
        public void VertexGraphBuilder_CtorCalledWithGraphWithMissingNode_ShouldThrowArgumentException()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int dummyActivityId = activityId5 + 1;
            var graphBuilder = new VertexGraphBuilder<int, IActivity<int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());

            var activity1 = new Activity<int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            Assert.IsTrue(result1);

            var activity2 = new Activity<int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            Assert.IsTrue(result2);

            var activity3 = new Activity<int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int>(new[] { activityId1, activityId4 }));
            Assert.IsTrue(result3);

            var activity4 = new Activity<int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int>(new[] { activityId2 }));
            Assert.IsTrue(result4);

            var activity5 = new Activity<int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int>(new[] { activityId1 }));
            Assert.IsTrue(result5);

            var graph = graphBuilder.ToGraph();
            Node<int, IActivity<int>> node = graph.Nodes.First(x => x.NodeType == NodeType.Normal);
            graph.Nodes.Remove(node);

            Assert.ThrowsException<ArgumentException>(
                () => new VertexGraphBuilder<int, IActivity<int>>(graph, () => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next()));
        }

        [TestMethod]
        public void VertexGraphBuilder_CtorCalledWithGraphWithTooManyNodes_ShouldThrowArgumentException()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int dummyActivityId = activityId5 + 1;
            var graphBuilder = new VertexGraphBuilder<int, IActivity<int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());

            var activity1 = new Activity<int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            Assert.IsTrue(result1);

            var activity2 = new Activity<int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            Assert.IsTrue(result2);

            var activity3 = new Activity<int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int>(new[] { activityId1, activityId4 }));
            Assert.IsTrue(result3);

            var activity4 = new Activity<int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int>(new[] { activityId2 }));
            Assert.IsTrue(result4);

            var activity5 = new Activity<int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int>(new[] { activityId1 }));
            Assert.IsTrue(result5);

            var graph = graphBuilder.ToGraph();
            graph.Nodes.Add(new Node<int, IActivity<int>>(new Activity<int>(dummyActivityId = dummyActivityId.Next(), 0)));

            Assert.ThrowsException<ArgumentException>(
                () => new VertexGraphBuilder<int, IActivity<int>>(graph, () => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next()));
        }

        [TestMethod]
        public void VertexGraphBuilder_CtorCalledWithGraphWithNoStartNode_ShouldThrowArgumentException()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int dummyActivityId = activityId5 + 1;
            var graphBuilder = new VertexGraphBuilder<int, IActivity<int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());

            var activity1 = new Activity<int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            Assert.IsTrue(result1);

            var activity2 = new Activity<int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            Assert.IsTrue(result2);

            var activity3 = new Activity<int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int>(new[] { activityId1, activityId4 }));
            Assert.IsTrue(result3);

            var activity4 = new Activity<int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int>(new[] { activityId2 }));
            Assert.IsTrue(result4);

            var activity5 = new Activity<int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int>(new[] { activityId1 }));
            Assert.IsTrue(result5);

            var graph = graphBuilder.ToGraph();
            foreach (Node<int, IActivity<int>> node in graph.Nodes.Where(x => x.NodeType == NodeType.Start))
            {
                node.SetNodeType(NodeType.Normal);
            }

            Assert.ThrowsException<ArgumentException>(
                () => new VertexGraphBuilder<int, IActivity<int>>(graph, () => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next()));
        }

        [TestMethod]
        public void VertexGraphBuilder_CtorCalledWithGraphWithNoEndNode_ShouldThrowArgumentException()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int dummyActivityId = activityId5 + 1;
            var graphBuilder = new VertexGraphBuilder<int, IActivity<int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());

            var activity1 = new Activity<int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            Assert.IsTrue(result1);

            var activity2 = new Activity<int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            Assert.IsTrue(result2);

            var activity3 = new Activity<int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int>(new[] { activityId1, activityId4 }));
            Assert.IsTrue(result3);

            var activity4 = new Activity<int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int>(new[] { activityId2 }));
            Assert.IsTrue(result4);

            var activity5 = new Activity<int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int>(new[] { activityId1 }));
            Assert.IsTrue(result5);

            var graph = graphBuilder.ToGraph();
            foreach (Node<int, IActivity<int>> node in graph.Nodes.Where(x => x.NodeType == NodeType.End))
            {
                node.SetNodeType(NodeType.Normal);
            }

            Assert.ThrowsException<ArgumentException>(
                () => new VertexGraphBuilder<int, IActivity<int>>(graph, () => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next()));
        }

        [TestMethod]
        public void VertexGraphBuilder_CtorCalledWithGraphWithOnlyIsolatedNodes_NoException()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int dummyActivityId = activityId2 + 1;
            var graphBuilder = new VertexGraphBuilder<int, IActivity<int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());

            var activity1 = new Activity<int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            Assert.IsTrue(result1);

            var activity2 = new Activity<int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            Assert.IsTrue(result2);

            var graph = graphBuilder.ToGraph();
            var graphBuilder2 = new VertexGraphBuilder<int, IActivity<int>>(graph, () => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());

            Assert.IsFalse(graphBuilder2.EdgeIds.Any());
            Assert.AreEqual(2, graphBuilder2.NodeIds.Count());
            Assert.IsTrue(graphBuilder2.AllDependenciesSatisfied);
            Assert.IsFalse(graphBuilder2.StartNodes.Any());
            Assert.IsFalse(graphBuilder2.EndNodes.Any());
            Assert.IsFalse(graphBuilder2.NormalNodes.Any());
            Assert.AreEqual(2, graphBuilder2.IsolatedNodes.Count());
        }

        [TestMethod]
        public void VertexGraphBuilder_CtorCalledWithGraphWithUnconnectedStartNode_ShouldThrowArgumentException()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int dummyActivityId = activityId5 + 1;
            var graphBuilder = new VertexGraphBuilder<int, IActivity<int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());

            var activity1 = new Activity<int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            Assert.IsTrue(result1);

            var activity2 = new Activity<int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            Assert.IsTrue(result2);

            var activity3 = new Activity<int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int>(new[] { activityId1, activityId4 }));
            Assert.IsTrue(result3);

            var activity4 = new Activity<int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int>(new[] { activityId2 }));
            Assert.IsTrue(result4);

            var activity5 = new Activity<int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int>(new[] { activityId1 }));
            Assert.IsTrue(result5);

            var graph = graphBuilder.ToGraph();

            Node<int, IActivity<int>> node = graph.Nodes.First(x => x.NodeType == NodeType.Normal);
            graph.Nodes.Remove(node);

            var newNode = new Node<int, IActivity<int>>(NodeType.Start, node.Content);
            foreach (int edgeId in node.OutgoingEdges)
            {
                newNode.OutgoingEdges.Add(edgeId);
            }
            graph.Nodes.Add(newNode);

            Assert.ThrowsException<ArgumentException>(
                () => new VertexGraphBuilder<int, IActivity<int>>(graph, () => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next()));
        }

        [TestMethod]
        public void VertexGraphBuilder_CtorCalledWithGraphWithUnconnectedOneEndNode_ShouldThrowArgumentException()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int dummyActivityId = activityId5 + 1;
            var graphBuilder = new VertexGraphBuilder<int, IActivity<int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());

            var activity1 = new Activity<int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            Assert.IsTrue(result1);

            var activity2 = new Activity<int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            Assert.IsTrue(result2);

            var activity3 = new Activity<int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int>(new[] { activityId1, activityId4 }));
            Assert.IsTrue(result3);

            var activity4 = new Activity<int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int>(new[] { activityId2 }));
            Assert.IsTrue(result4);

            var activity5 = new Activity<int>(activityId5, 0);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int>(new[] { activityId1 }));
            Assert.IsTrue(result5);

            var graph = graphBuilder.ToGraph();

            Node<int, IActivity<int>> node = graph.Nodes.First(x => x.NodeType == NodeType.Normal);
            graph.Nodes.Remove(node);

            var newNode = new Node<int, IActivity<int>>(NodeType.End, node.Content);
            foreach (int edgeId in node.IncomingEdges)
            {
                newNode.IncomingEdges.Add(edgeId);
            }
            graph.Nodes.Add(newNode);

            Assert.ThrowsException<ArgumentException>(
                () => new VertexGraphBuilder<int, IActivity<int>>(graph, () => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next()));
        }

        [TestMethod]
        public void VertexGraphBuilder_FindCircularDependencies_FindsCircularDependency()
        {
            int eventId = 0;
            int dummyActivityId = 100;
            var graphBuilder = new VertexGraphBuilder<int, IActivity<int>>(() => eventId = eventId.Next(), () => dummyActivityId = dummyActivityId.Next());
            graphBuilder.AddActivity(new Activity<int>(1, 10));
            graphBuilder.AddActivity(new Activity<int>(2, 10), new HashSet<int>(new[] { 7 }));
            graphBuilder.AddActivity(new Activity<int>(3, 10));
            graphBuilder.AddActivity(new Activity<int>(4, 10), new HashSet<int>(new[] { 2 }));
            graphBuilder.AddActivity(new Activity<int>(5, 10), new HashSet<int>(new[] { 1, 2, 3, 8 }));
            graphBuilder.AddActivity(new Activity<int>(6, 10), new HashSet<int>(new[] { 3 }));
            graphBuilder.AddActivity(new Activity<int>(7, 10), new HashSet<int>(new[] { 4 }));
            graphBuilder.AddActivity(new Activity<int>(8, 10), new HashSet<int>(new[] { 9, 6 }));
            graphBuilder.AddActivity(new Activity<int>(9, 10), new HashSet<int>(new[] { 5 }));
            IList<CircularDependency<int>> circularDependencies = graphBuilder.FindStrongCircularDependencies();

            Assert.AreEqual(2, circularDependencies.Count);
            CollectionAssert.AreEquivalent(
                new List<int>(new int[] { 2, 4, 7 }),
                circularDependencies[0].Dependencies.ToList());
            CollectionAssert.AreEquivalent(
                new List<int>(new int[] { 5, 8, 9 }),
                circularDependencies[1].Dependencies.ToList());
        }
    }
}
