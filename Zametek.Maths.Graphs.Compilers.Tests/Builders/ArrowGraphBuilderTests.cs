using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs.Tests
{
    [TestClass]
    public class ArrowGraphBuilderTests
    {
        [TestMethod]
        public void ArrowGraphBuilder_Contructor_NoException()
        {
            int eventId = 0;
            int dummyActivityId = 0;
            var graphBuilder = new ArrowGraphBuilder<int, IActivity<int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());

            Assert.IsFalse(graphBuilder.EdgeIds.Any());
            Assert.AreEqual(2, graphBuilder.NodeIds.Count());
            Assert.IsTrue(graphBuilder.AllDependenciesSatisfied);
            Assert.AreEqual(0, graphBuilder.StartNode.OutgoingEdges.Count);
            Assert.AreEqual(0, graphBuilder.EndNode.IncomingEdges.Count);
        }

        [TestMethod]
        public void ArrowGraphBuilder_CtorCalledWithNullEdgeIdGenerator_ShouldThrowArgumentNullException()
        {
            int eventId = 0;
            Assert.ThrowsException<ArgumentNullException>(
                () => new ArrowGraphBuilder<int, IActivity<int>>(null, () => eventId = eventId.Next()));
        }

        [TestMethod]
        public void ArrowGraphBuilder_CtorCalledWithNullNodeIdGenerator_ShouldThrowArgumentNullException()
        {
            int dummyActivityId = 0;
            Assert.ThrowsException<ArgumentNullException>(
                () => new ArrowGraphBuilder<int, IActivity<int>>(() => dummyActivityId = dummyActivityId.Next(), null));
        }

        [TestMethod]
        public void ArrowGraphBuilder_AccessOutgoingEdgesOfEndNode_ShouldThrowInvalidOperationException()
        {
            int eventId = 0;
            int dummyActivityId = 0;
            var graphBuilder = new ArrowGraphBuilder<int, IActivity<int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            Assert.ThrowsException<InvalidOperationException>(
                () => graphBuilder.EndNode.OutgoingEdges.Any());
        }

        [TestMethod]
        public void ArrowGraphBuilder_SingleActivityNoDependencies_HooksUpToStartAndEndNodes()
        {
            int eventId = 0;
            int activityId = 1;
            int dummyActivityId = activityId;
            var graphBuilder = new ArrowGraphBuilder<int, IActivity<int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());

            int dummyActivityId1 = dummyActivityId + 1;

            var activity = new Activity<int>(activityId, 0);
            bool result = graphBuilder.AddActivity(activity);
            Assert.IsTrue(result);

            Assert.AreEqual(2, graphBuilder.EdgeIds.Count());
            Assert.AreEqual(3, graphBuilder.NodeIds.Count());
            Assert.IsTrue(graphBuilder.AllDependenciesSatisfied);
            Assert.AreEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(activityId).Id);
            Assert.AreEqual(graphBuilder.EdgeHeadNode(activityId).Id, graphBuilder.EdgeTailNode(dummyActivityId1).Id);
            Assert.AreEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(dummyActivityId1).Id);
            Assert.AreEqual(1, graphBuilder.StartNode.OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId));
            Assert.AreEqual(1, graphBuilder.EndNode.IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId1));
        }

        [TestMethod]
        public void ArrowGraphBuilder_TwoActivitiesOneDependency_ActivitiesHookedUpByDummyEdge()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int dummyActivityId = activityId2 + 1;
            int dummyActivityId1 = dummyActivityId + 1;
            int dummyActivityId2 = dummyActivityId1 + 1;
            int dummyActivityId3 = dummyActivityId2 + 1;
            var graphBuilder = new ArrowGraphBuilder<int, IActivity<int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());

            var activity1 = new Activity<int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            Assert.IsTrue(result1);

            Assert.AreEqual(2, graphBuilder.EdgeIds.Count());
            Assert.AreEqual(3, graphBuilder.NodeIds.Count());
            Assert.IsTrue(graphBuilder.AllDependenciesSatisfied);
            Assert.AreEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(activityId1).Id);
            Assert.AreEqual(graphBuilder.EdgeHeadNode(activityId1).Id, graphBuilder.EdgeTailNode(dummyActivityId1).Id);
            Assert.AreEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(dummyActivityId1).Id);
            Assert.AreEqual(1, graphBuilder.StartNode.OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId1));
            Assert.AreEqual(1, graphBuilder.EndNode.IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId1));

            var activity2 = new Activity<int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2, new HashSet<int>(new[] { activityId1 }));
            Assert.IsTrue(result2);

            Assert.AreEqual(5, graphBuilder.EdgeIds.Count());
            Assert.AreEqual(5, graphBuilder.NodeIds.Count());
            Assert.IsTrue(graphBuilder.AllDependenciesSatisfied);

            // First activity.
            Assert.AreEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(activityId1).Id);

            Assert.AreEqual(1, graphBuilder.StartNode.OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId1));
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId1));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1));

            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId1));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId2));

            Assert.AreEqual(graphBuilder.EdgeHeadNode(activityId1).Id, graphBuilder.EdgeTailNode(dummyActivityId1).Id);
            Assert.AreEqual(graphBuilder.EdgeHeadNode(activityId1).Id, graphBuilder.EdgeTailNode(dummyActivityId2).Id);

            // Dummy activity.
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Contains(activityId1));

            Assert.AreEqual(2, graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId1));
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId2));

            Assert.IsTrue(graphBuilder.Edge(dummyActivityId1).Content.IsDummy);
            Assert.IsTrue(graphBuilder.Edge(dummyActivityId2).Content.IsDummy);

            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId1));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId3));

            Assert.AreEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(dummyActivityId1).Id);

            // Second activity.
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(activityId2).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId2).IncomingEdges.Contains(dummyActivityId2));

            Assert.AreEqual(1, graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId3));

            // Dummy activity.
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Contains(activityId2));

            Assert.IsTrue(graphBuilder.Edge(dummyActivityId3).Content.IsDummy);

            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId1));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId3));

            Assert.AreEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(dummyActivityId3).Id);

            Assert.AreEqual(2, graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId1));
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId2));

            Assert.IsTrue(graphBuilder.Edge(dummyActivityId2).Content.IsDummy);

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId2));
        }

        [TestMethod]
        public void ArrowGraphBuilder_TwoActivitiesOneDependencyReverseOrder_ActivitiesHookedUpByDummyEdge()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int dummyActivityId = activityId2 + 1;
            int dummyActivityId1 = dummyActivityId + 1;
            int dummyActivityId2 = dummyActivityId1 + 1;
            var graphBuilder = new ArrowGraphBuilder<int, IActivity<int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());

            var activity2 = new Activity<int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2, new HashSet<int>(new[] { activityId1 }));
            Assert.IsTrue(result2);

            Assert.AreEqual(2, graphBuilder.EdgeIds.Count());
            Assert.AreEqual(4, graphBuilder.NodeIds.Count());
            Assert.IsFalse(graphBuilder.AllDependenciesSatisfied);
            Assert.AreNotEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(activityId2).Id);
            Assert.AreEqual(0, graphBuilder.StartNode.OutgoingEdges.Count);

            Assert.AreEqual(0, graphBuilder.EdgeTailNode(activityId2).IncomingEdges.Count);

            Assert.AreEqual(1, graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2));

            var activity1 = new Activity<int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            Assert.IsTrue(result1);

            Assert.AreEqual(4, graphBuilder.EdgeIds.Count());
            Assert.AreEqual(5, graphBuilder.NodeIds.Count());
            Assert.IsTrue(graphBuilder.AllDependenciesSatisfied);

            // First activity.
            Assert.AreEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(activityId1).Id);

            Assert.AreEqual(1, graphBuilder.StartNode.OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId1));
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId1));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId2));

            Assert.AreEqual(graphBuilder.EdgeHeadNode(activityId1).Id, graphBuilder.EdgeTailNode(dummyActivityId2).Id);

            // Dummy activity.
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Contains(activityId2));

            Assert.AreEqual(1, graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId1));

            Assert.IsTrue(graphBuilder.Edge(dummyActivityId1).Content.IsDummy);

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId1));

            Assert.IsTrue(graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId1));

            Assert.AreEqual(graphBuilder.EdgeTailNode(dummyActivityId1).Id, graphBuilder.EdgeHeadNode(activityId2).Id);

            // Second activity.
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(activityId2).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId2).IncomingEdges.Contains(dummyActivityId2));

            Assert.AreEqual(1, graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2));

            // Dummy activity.
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Contains(activityId2));

            Assert.AreEqual(1, graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId1));

            Assert.IsTrue(graphBuilder.Edge(dummyActivityId1).Content.IsDummy);

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId1));

            Assert.IsTrue(graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId1));

            Assert.AreEqual(graphBuilder.EdgeTailNode(dummyActivityId1).Id, graphBuilder.EdgeHeadNode(activityId2).Id);
            Assert.AreEqual(1, graphBuilder.EndNode.IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId1));

            Assert.AreEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(dummyActivityId1).Id);
        }

        [TestMethod]
        public void ArrowGraphBuilder_ThreeActivitiesOneDependentOnOtherTwo_DependentActivityHookedUpByTwoDummyEdges()
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
            var graphBuilder = new ArrowGraphBuilder<int, IActivity<int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());

            var activity1 = new Activity<int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            Assert.IsTrue(result1);

            Assert.AreEqual(2, graphBuilder.EdgeIds.Count());
            Assert.AreEqual(3, graphBuilder.NodeIds.Count());
            Assert.IsTrue(graphBuilder.AllDependenciesSatisfied);

            // First activity.
            Assert.AreEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(activityId1).Id);
            Assert.AreEqual(1, graphBuilder.StartNode.OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId1));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1));

            Assert.AreEqual(graphBuilder.EdgeHeadNode(activityId1).Id, graphBuilder.EdgeTailNode(dummyActivityId1).Id);

            Assert.AreEqual(1, graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId1));

            Assert.AreEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(dummyActivityId1).Id);
            Assert.AreEqual(1, graphBuilder.EndNode.IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId1));

            var activity2 = new Activity<int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            Assert.IsTrue(result2);

            Assert.AreEqual(4, graphBuilder.EdgeIds.Count());
            Assert.AreEqual(4, graphBuilder.NodeIds.Count());
            Assert.IsTrue(graphBuilder.AllDependenciesSatisfied);

            // Second activity.
            Assert.AreEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(activityId2).Id);
            Assert.AreEqual(2, graphBuilder.StartNode.OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId2));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2));

            Assert.AreEqual(graphBuilder.EdgeHeadNode(activityId2).Id, graphBuilder.EdgeTailNode(dummyActivityId2).Id);

            Assert.AreEqual(1, graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId2));

            Assert.AreEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(dummyActivityId2).Id);
            Assert.AreEqual(2, graphBuilder.EndNode.IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId2));

            // Dummy activity.
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Contains(activityId1));

            Assert.AreEqual(1, graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId1));

            Assert.IsTrue(graphBuilder.Edge(dummyActivityId1).Content.IsDummy);

            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId1));

            Assert.IsTrue(graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId1));

            Assert.AreEqual(graphBuilder.EdgeTailNode(dummyActivityId1).Id, graphBuilder.EdgeHeadNode(activityId1).Id);

            var activity3 = new Activity<int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int>(new[] { activityId1, activityId2 }));
            Assert.IsTrue(result3);

            Assert.AreEqual(8, graphBuilder.EdgeIds.Count());
            Assert.AreEqual(6, graphBuilder.NodeIds.Count());
            Assert.IsTrue(graphBuilder.AllDependenciesSatisfied);

            // First activity.
            Assert.AreEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(activityId1).Id);
            Assert.AreNotEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(activityId1).Id);

            Assert.AreEqual(2, graphBuilder.StartNode.OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId1));
            Assert.AreEqual(2, graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId1));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1));

            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId1));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId3));

            Assert.AreEqual(graphBuilder.EdgeHeadNode(activityId1).Id, graphBuilder.EdgeTailNode(dummyActivityId1).Id);

            // Second activity.
            Assert.AreEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(activityId2).Id);
            Assert.AreNotEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(activityId2).Id);

            Assert.AreEqual(2, graphBuilder.StartNode.OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId2));
            Assert.AreEqual(2, graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2));

            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId2));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId4));

            Assert.AreEqual(graphBuilder.EdgeHeadNode(activityId2).Id, graphBuilder.EdgeTailNode(dummyActivityId2).Id);

            // First dummy activity.
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Contains(activityId1));

            Assert.AreEqual(2, graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId1));
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Contains(dummyActivityId1));

            Assert.IsTrue(graphBuilder.Edge(dummyActivityId1).Content.IsDummy);

            Assert.AreEqual(3, graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId1));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId2));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId5));

            Assert.AreEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(dummyActivityId1).Id);

            // Second dummy activity.
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Contains(activityId2));

            Assert.AreEqual(2, graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId2));
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId4).OutgoingEdges.Contains(dummyActivityId2));

            Assert.IsTrue(graphBuilder.Edge(dummyActivityId2).Content.IsDummy);

            Assert.AreEqual(3, graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId1));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId2));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId5));

            Assert.AreEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(dummyActivityId2).Id);

            // Third dummy activity.
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Contains(activityId1));

            Assert.AreEqual(2, graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId1));
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Contains(dummyActivityId1));

            Assert.IsTrue(graphBuilder.Edge(dummyActivityId3).Content.IsDummy);

            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId3));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId4));

            // Forth dummy activity.
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(dummyActivityId4).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId4).IncomingEdges.Contains(activityId2));

            Assert.AreEqual(2, graphBuilder.EdgeTailNode(dummyActivityId4).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId2));
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId4).OutgoingEdges.Contains(dummyActivityId2));

            Assert.IsTrue(graphBuilder.Edge(dummyActivityId4).Content.IsDummy);

            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(dummyActivityId4).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId4).IncomingEdges.Contains(dummyActivityId3));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId4).IncomingEdges.Contains(dummyActivityId4));

            // Fifth dummy activity.
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(dummyActivityId5).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId5).IncomingEdges.Contains(activityId3));

            Assert.AreEqual(1, graphBuilder.EdgeTailNode(dummyActivityId5).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId5).OutgoingEdges.Contains(dummyActivityId5));

            Assert.IsTrue(graphBuilder.Edge(dummyActivityId5).Content.IsDummy);

            Assert.AreEqual(3, graphBuilder.EdgeHeadNode(dummyActivityId5).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId5).IncomingEdges.Contains(dummyActivityId1));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId5).IncomingEdges.Contains(dummyActivityId2));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId5).IncomingEdges.Contains(dummyActivityId5));

            Assert.AreEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(dummyActivityId5).Id);

            // Third activity.
            Assert.AreEqual(2, graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId3));
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId4));

            Assert.AreEqual(1, graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Contains(activityId3));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Contains(activityId3));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Contains(dummyActivityId5));
        }

        [TestMethod]
        public void ArrowGraphBuilder_ThreeActivitiesOneDependentOnOtherTwoReverseOrder_DependentActivityHookedUpByTwoDummyEdges()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int dummyActivityId = activityId3 + 1;
            int dummyActivityId1 = dummyActivityId + 1;
            int dummyActivityId2 = dummyActivityId1 + 1;
            int dummyActivityId3 = dummyActivityId2 + 1;
            var graphBuilder = new ArrowGraphBuilder<int, IActivity<int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());

            var activity3 = new Activity<int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int>(new[] { activityId1, activityId2 }));
            Assert.IsTrue(result3);

            Assert.AreEqual(2, graphBuilder.EdgeIds.Count());
            Assert.AreEqual(4, graphBuilder.NodeIds.Count());
            Assert.IsFalse(graphBuilder.AllDependenciesSatisfied);
            Assert.AreNotEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(activityId3).Id);
            Assert.AreEqual(0, graphBuilder.StartNode.OutgoingEdges.Count);

            Assert.AreEqual(0, graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Count);
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Contains(activityId3));

            var activity2 = new Activity<int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            Assert.IsTrue(result2);

            Assert.AreEqual(4, graphBuilder.EdgeIds.Count());
            Assert.AreEqual(5, graphBuilder.NodeIds.Count());
            Assert.IsFalse(graphBuilder.AllDependenciesSatisfied);
            Assert.AreNotEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(activityId3).Id);

            // Second activity.
            Assert.AreEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(activityId2).Id);
            Assert.AreNotEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(activityId2).Id);

            Assert.AreEqual(1, graphBuilder.StartNode.OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId2));
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId2));

            Assert.AreEqual(graphBuilder.EdgeHeadNode(activityId2).Id, graphBuilder.EdgeTailNode(dummyActivityId2).Id);

            // Second dummy activity.
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Contains(activityId2));

            Assert.AreEqual(1, graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId2));

            Assert.IsTrue(graphBuilder.Edge(dummyActivityId2).Content.IsDummy);

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId2));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(dummyActivityId2).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId2).OutgoingEdges.Contains(activityId3));

            Assert.AreEqual(graphBuilder.EdgeHeadNode(dummyActivityId2).Id, graphBuilder.EdgeTailNode(activityId3).Id);

            // Third activity.
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId2));

            Assert.AreEqual(1, graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Contains(activityId3));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Contains(activityId3));

            Assert.AreNotEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(activityId3).Id);
            Assert.AreNotEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(activityId3).Id);

            var activity1 = new Activity<int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            Assert.IsTrue(result1);

            Assert.AreEqual(6, graphBuilder.EdgeIds.Count());
            Assert.AreEqual(6, graphBuilder.NodeIds.Count());
            Assert.IsTrue(graphBuilder.AllDependenciesSatisfied);

            // First activity.
            Assert.AreEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(activityId1).Id);
            Assert.AreNotEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(activityId1).Id);

            Assert.AreEqual(2, graphBuilder.StartNode.OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId1));
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId2));
            Assert.AreEqual(2, graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId1));
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId2));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId3));

            Assert.AreEqual(graphBuilder.EdgeHeadNode(activityId1).Id, graphBuilder.EdgeTailNode(dummyActivityId3).Id);

            // Second activity.
            Assert.AreEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(activityId2).Id);
            Assert.AreNotEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(activityId2).Id);

            Assert.AreEqual(2, graphBuilder.StartNode.OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId2));
            Assert.AreEqual(2, graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId2));

            Assert.AreEqual(graphBuilder.EdgeHeadNode(activityId2).Id, graphBuilder.EdgeTailNode(dummyActivityId2).Id);

            // First dummy activity.
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Contains(activityId3));

            Assert.AreEqual(1, graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId1));

            Assert.IsTrue(graphBuilder.Edge(dummyActivityId1).Content.IsDummy);

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId1));

            Assert.AreEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(dummyActivityId1).Id);

            Assert.IsTrue(graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId1));

            // Second dummy activity.
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Contains(activityId2));

            Assert.AreEqual(1, graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId2));

            Assert.IsTrue(graphBuilder.Edge(dummyActivityId2).Content.IsDummy);

            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId2));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId3));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(dummyActivityId2).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId2).OutgoingEdges.Contains(activityId3));

            Assert.AreEqual(graphBuilder.EdgeHeadNode(dummyActivityId2).Id, graphBuilder.EdgeTailNode(activityId3).Id);

            // Third dummy activity.
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Contains(activityId1));

            Assert.AreEqual(1, graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Contains(dummyActivityId3));

            Assert.IsTrue(graphBuilder.Edge(dummyActivityId3).Content.IsDummy);

            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId2));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId3));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(dummyActivityId3).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId3).OutgoingEdges.Contains(activityId3));

            Assert.AreEqual(graphBuilder.EdgeHeadNode(dummyActivityId3).Id, graphBuilder.EdgeTailNode(activityId3).Id);

            // Third activity.
            Assert.AreEqual(2, graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId2));
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId3));

            Assert.AreEqual(1, graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Contains(activityId3));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Contains(activityId3));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Contains(dummyActivityId1));
        }

        [TestMethod]
        public void ArrowGraphBuilder_ThreeActivitiesOneDependentOnOtherTwoRemovedInStages_StructureAsExpected()
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
            var graphBuilder = new ArrowGraphBuilder<int, IActivity<int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());

            var activity1 = Activity<int>.CreateActivityDummy(activityId1);
            bool result1 = graphBuilder.AddActivity(activity1);
            Assert.IsTrue(result1);

            var activity2 = Activity<int>.CreateActivityDummy(activityId2);
            bool result2 = graphBuilder.AddActivity(activity2);
            Assert.IsTrue(result2);

            var activity3 = Activity<int>.CreateActivityDummy(activityId3);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int>(new[] { activityId1, activityId2 }));
            Assert.IsTrue(result3);

            Assert.AreEqual(8, graphBuilder.EdgeIds.Count());
            Assert.AreEqual(6, graphBuilder.NodeIds.Count());
            Assert.IsTrue(graphBuilder.AllDependenciesSatisfied);

            bool result4 = graphBuilder.RemoveDummyActivity(dummyActivityId1);
            Assert.IsTrue(result4);

            bool result5 = graphBuilder.RemoveDummyActivity(dummyActivityId2);
            Assert.IsTrue(result5);

            bool result6 = graphBuilder.RemoveDummyActivity(dummyActivityId3);
            Assert.IsTrue(result6);

            Assert.AreEqual(5, graphBuilder.EdgeIds.Count());
            Assert.AreEqual(5, graphBuilder.NodeIds.Count());
            Assert.IsTrue(graphBuilder.AllDependenciesSatisfied);

            // First activity.
            Assert.AreEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(activityId1).Id);
            Assert.AreNotEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(activityId1).Id);

            Assert.AreEqual(2, graphBuilder.StartNode.OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId2));
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId1));
            Assert.AreEqual(2, graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId2));
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId1));

            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(dummyActivityId4));
            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(activityId3));

            // Second activity.
            Assert.AreEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(activityId2).Id);
            Assert.AreNotEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(activityId2).Id);

            Assert.AreEqual(2, graphBuilder.StartNode.OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId2));
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId1));
            Assert.AreEqual(2, graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2));
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId1));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId4));

            Assert.AreEqual(graphBuilder.EdgeHeadNode(activityId2).Id, graphBuilder.EdgeTailNode(dummyActivityId4).Id);

            // Third activity.
            Assert.AreEqual(2, graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(activityId1));
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId4));

            Assert.AreEqual(1, graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Contains(activityId3));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Contains(activityId3));
            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Contains(dummyActivityId5));

            Assert.AreNotEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(activityId3).Id);
            Assert.AreNotEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(activityId3).Id);



            bool result7 = graphBuilder.RemoveDummyActivity(activityId3);
            Assert.IsTrue(result7);

            Assert.AreEqual(4, graphBuilder.EdgeIds.Count());
            Assert.AreEqual(4, graphBuilder.NodeIds.Count());
            Assert.IsTrue(graphBuilder.AllDependenciesSatisfied);

            // First activity.
            Assert.AreEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(activityId1).Id);
            Assert.AreNotEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(activityId1).Id);

            Assert.AreEqual(2, graphBuilder.StartNode.OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId2));
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId1));
            Assert.AreEqual(2, graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId2));
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId1));

            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(dummyActivityId4));
            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId5));

            // Second activity.
            Assert.AreEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(activityId2).Id);
            Assert.AreNotEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(activityId2).Id);

            Assert.AreEqual(2, graphBuilder.StartNode.OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId2));
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId1));
            Assert.AreEqual(2, graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2));
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId1));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId4));

            Assert.AreEqual(graphBuilder.EdgeHeadNode(activityId2).Id, graphBuilder.EdgeTailNode(dummyActivityId4).Id);

            // Third activity.
            Assert.IsFalse(graphBuilder.EdgeIds.Contains(activityId3));



            bool result8 = graphBuilder.RemoveDummyActivity(dummyActivityId5);
            Assert.IsTrue(result8);

            Assert.AreEqual(3, graphBuilder.EdgeIds.Count());
            Assert.AreEqual(3, graphBuilder.NodeIds.Count());
            Assert.IsTrue(graphBuilder.AllDependenciesSatisfied);


            // First activity.
            Assert.AreEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(activityId1).Id);
            Assert.AreEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(activityId1).Id);

            Assert.AreEqual(2, graphBuilder.StartNode.OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId2));
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId1));
            Assert.AreEqual(2, graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId2));
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId1));

            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(dummyActivityId4));

            // Second activity.
            Assert.AreEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(activityId2).Id);
            Assert.AreNotEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(activityId2).Id);

            Assert.AreEqual(2, graphBuilder.StartNode.OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId2));
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId1));
            Assert.AreEqual(2, graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2));
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId1));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId4));

            Assert.AreEqual(graphBuilder.EdgeHeadNode(activityId2).Id, graphBuilder.EdgeTailNode(dummyActivityId4).Id);

            // Third activity.
            Assert.IsFalse(graphBuilder.EdgeIds.Contains(activityId3));



            bool result9 = graphBuilder.RemoveDummyActivity(activityId1);
            Assert.IsFalse(result9);
            bool result10 = graphBuilder.RemoveDummyActivity(activityId2);
            Assert.IsFalse(result10);
            bool result11 = graphBuilder.RemoveDummyActivity(dummyActivityId4);
            Assert.IsFalse(result11);
        }

        [TestMethod]
        public void ArrowGraphBuilder_ThreeActivitiesOneDependentOnOtherTwoRedirectDummyEdges_DummiesRedirectedAsExpected()
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
            var graphBuilder = new ArrowGraphBuilder<int, IActivity<int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());

            var activity1 = new Activity<int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            Assert.IsTrue(result1);

            var activity2 = new Activity<int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            Assert.IsTrue(result2);

            var activity3 = new Activity<int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int>(new[] { activityId1, activityId2 }));
            Assert.IsTrue(result3);

            Assert.AreEqual(8, graphBuilder.EdgeIds.Count());
            Assert.AreEqual(6, graphBuilder.NodeIds.Count());
            Assert.IsTrue(graphBuilder.AllDependenciesSatisfied);

            bool result4 = graphBuilder.RedirectEdges();
            Assert.IsTrue(result4);

            Assert.AreEqual(7, graphBuilder.EdgeIds.Count());
            Assert.AreEqual(6, graphBuilder.NodeIds.Count());
            Assert.IsTrue(graphBuilder.AllDependenciesSatisfied);

            // First activity.
            Assert.AreEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(activityId1).Id);
            Assert.AreNotEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(activityId1).Id);

            Assert.AreEqual(2, graphBuilder.StartNode.OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId1));
            Assert.AreEqual(2, graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId1));

            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count);

            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1));

            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId1));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId3));

            Assert.AreEqual(graphBuilder.EdgeHeadNode(activityId1).Id, graphBuilder.EdgeTailNode(dummyActivityId1).Id);
            Assert.AreEqual(graphBuilder.EdgeHeadNode(activityId1).Id, graphBuilder.EdgeTailNode(dummyActivityId3).Id);

            // Second activity.
            Assert.AreEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(activityId2).Id);
            Assert.AreNotEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(activityId2).Id);

            Assert.AreEqual(2, graphBuilder.StartNode.OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId2));
            Assert.AreEqual(2, graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId2));

            Assert.AreEqual(graphBuilder.EdgeHeadNode(activityId2).Id, graphBuilder.EdgeTailNode(dummyActivityId2).Id);

            // First dummy activity.
            Assert.AreEqual(2, graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Contains(activityId1));
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId2));

            Assert.AreEqual(2, graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId1));
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId3));

            Assert.IsTrue(graphBuilder.Edge(dummyActivityId1).Content.IsDummy);

            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId1));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(activityId3));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(dummyActivityId1).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId5));

            Assert.AreEqual(graphBuilder.EdgeHeadNode(dummyActivityId1).Id, graphBuilder.EdgeTailNode(dummyActivityId5).Id);

            // Second dummy activity.
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Contains(activityId2));

            Assert.AreEqual(1, graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId2));

            Assert.IsTrue(graphBuilder.Edge(dummyActivityId2).Content.IsDummy);

            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(activityId1));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId2));

            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(dummyActivityId2).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId1));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId3));

            Assert.AreEqual(graphBuilder.EdgeHeadNode(dummyActivityId2).Id, graphBuilder.EdgeTailNode(dummyActivityId1).Id);
            Assert.AreEqual(graphBuilder.EdgeHeadNode(dummyActivityId2).Id, graphBuilder.EdgeTailNode(dummyActivityId3).Id);

            // Third activity.
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId3));

            Assert.AreEqual(1, graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Contains(activityId3));

            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Contains(dummyActivityId1));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Contains(activityId3));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Contains(dummyActivityId5));

            Assert.AreEqual(graphBuilder.EdgeHeadNode(activityId3).Id, graphBuilder.EdgeTailNode(dummyActivityId5).Id);

            // Third dummy activity.
            Assert.AreEqual(2, graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Contains(activityId1));
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId2));

            Assert.AreEqual(2, graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Contains(dummyActivityId1));
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Contains(dummyActivityId3));

            Assert.IsTrue(graphBuilder.Edge(dummyActivityId3).Content.IsDummy);

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId3));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(dummyActivityId3).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId3).OutgoingEdges.Contains(activityId3));

            Assert.AreEqual(graphBuilder.EdgeHeadNode(dummyActivityId3).Id, graphBuilder.EdgeTailNode(activityId3).Id);

            // Fourth dummy activity.
            Assert.IsFalse(graphBuilder.EdgeIds.Contains(dummyActivityId4));

            // Fifth dummy activity.
            Assert.AreEqual(2, graphBuilder.EdgeTailNode(dummyActivityId5).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId5).IncomingEdges.Contains(activityId3));
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId5).IncomingEdges.Contains(dummyActivityId1));

            Assert.AreEqual(1, graphBuilder.EdgeTailNode(dummyActivityId5).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId5).OutgoingEdges.Contains(dummyActivityId5));

            Assert.IsTrue(graphBuilder.Edge(dummyActivityId5).Content.IsDummy);

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(dummyActivityId5).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId5).IncomingEdges.Contains(dummyActivityId5));

            Assert.AreEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(dummyActivityId5).Id);
        }

        [TestMethod]
        public void ArrowGraphBuilder_FourActivitiesOneDependentOnOtherThreeRedirectDummyEdges_DummiesRedirectedAsExpected()
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
            var graphBuilder = new ArrowGraphBuilder<int, IActivity<int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());

            var activity1 = new Activity<int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            Assert.IsTrue(result1);

            var activity2 = new Activity<int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            Assert.IsTrue(result2);

            var activity3 = new Activity<int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3);
            Assert.IsTrue(result3);

            var activity4 = new Activity<int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int>(new[] { activityId1, activityId2, activityId3 }));
            Assert.IsTrue(result4);

            bool result5 = graphBuilder.RedirectEdges();
            Assert.IsTrue(result5);

            Assert.AreEqual(9, graphBuilder.EdgeIds.Count());
            Assert.AreEqual(7, graphBuilder.NodeIds.Count());
            Assert.IsTrue(graphBuilder.AllDependenciesSatisfied);

            // First activity.
            Assert.AreEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(activityId1).Id);
            Assert.AreNotEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(activityId1).Id);

            Assert.AreEqual(3, graphBuilder.StartNode.OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId1));
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId2));
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId3));
            Assert.AreEqual(3, graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId1));
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId2));
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId3));

            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(dummyActivityId2));

            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId1));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId4));

            Assert.AreEqual(graphBuilder.EdgeHeadNode(activityId1).Id, graphBuilder.EdgeTailNode(dummyActivityId1).Id);
            Assert.AreEqual(graphBuilder.EdgeHeadNode(activityId1).Id, graphBuilder.EdgeTailNode(dummyActivityId4).Id);

            // Second activity.
            Assert.AreEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(activityId2).Id);
            Assert.AreNotEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(activityId2).Id);

            Assert.AreEqual(3, graphBuilder.StartNode.OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId1));
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId2));
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId3));
            Assert.AreEqual(3, graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId1));
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2));
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId3));

            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(dummyActivityId3));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId2));

            Assert.AreEqual(graphBuilder.EdgeHeadNode(activityId2).Id, graphBuilder.EdgeTailNode(dummyActivityId2).Id);

            // Third activity.
            Assert.AreEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(activityId3).Id);
            Assert.AreNotEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(activityId3).Id);

            Assert.AreEqual(3, graphBuilder.StartNode.OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId1));
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId2));
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId3));
            Assert.AreEqual(3, graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Contains(activityId1));
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Contains(activityId2));
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Contains(activityId3));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Contains(activityId3));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Contains(dummyActivityId3));

            Assert.AreEqual(graphBuilder.EdgeHeadNode(activityId3).Id, graphBuilder.EdgeTailNode(dummyActivityId3).Id);

            // Fourth activity.
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(activityId4).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId4).IncomingEdges.Contains(dummyActivityId4));

            Assert.AreEqual(1, graphBuilder.EdgeTailNode(activityId4).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId4).OutgoingEdges.Contains(activityId4));

            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(activityId4).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId4).IncomingEdges.Contains(activityId4));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId4).IncomingEdges.Contains(dummyActivityId1));

            // First dummy activity.
            Assert.AreEqual(2, graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Contains(activityId1));
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId2));

            Assert.AreEqual(2, graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId1));
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId4));

            Assert.IsTrue(graphBuilder.Edge(dummyActivityId1).Content.IsDummy);

            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId1));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(activityId4));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(dummyActivityId1).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId7));

            Assert.AreEqual(graphBuilder.EdgeHeadNode(dummyActivityId1).Id, graphBuilder.EdgeTailNode(dummyActivityId7).Id);

            // Second dummy activity.
            Assert.AreEqual(2, graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Contains(activityId2));
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId3));

            Assert.AreEqual(1, graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId2));

            Assert.IsTrue(graphBuilder.Edge(dummyActivityId2).Content.IsDummy);

            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(activityId1));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId2));

            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(dummyActivityId2).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId1));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId4));

            Assert.AreEqual(graphBuilder.EdgeHeadNode(dummyActivityId2).Id, graphBuilder.EdgeTailNode(dummyActivityId1).Id);

            // Third dummy activity.
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Contains(activityId3));

            Assert.AreEqual(1, graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Contains(dummyActivityId3));

            Assert.IsTrue(graphBuilder.Edge(dummyActivityId2).Content.IsDummy);

            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(activityId2));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId3));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(dummyActivityId3).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId3).OutgoingEdges.Contains(dummyActivityId2));

            Assert.AreEqual(graphBuilder.EdgeHeadNode(dummyActivityId3).Id, graphBuilder.EdgeTailNode(dummyActivityId2).Id);

            // Fourth dummy activity.
            Assert.AreEqual(2, graphBuilder.EdgeTailNode(dummyActivityId4).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId4).IncomingEdges.Contains(activityId1));
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId4).IncomingEdges.Contains(dummyActivityId2));

            Assert.AreEqual(2, graphBuilder.EdgeTailNode(dummyActivityId4).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId4).OutgoingEdges.Contains(dummyActivityId1));
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId4).OutgoingEdges.Contains(dummyActivityId4));

            Assert.IsTrue(graphBuilder.Edge(dummyActivityId4).Content.IsDummy);

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(dummyActivityId4).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId4).IncomingEdges.Contains(dummyActivityId4));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(dummyActivityId4).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId4).OutgoingEdges.Contains(activityId4));

            Assert.AreEqual(graphBuilder.EdgeHeadNode(dummyActivityId4).Id, graphBuilder.EdgeTailNode(activityId4).Id);

            // Fifth dummy activity.
            Assert.IsFalse(graphBuilder.EdgeIds.Contains(dummyActivityId5));

            // Sixth dummy activity.
            Assert.IsFalse(graphBuilder.EdgeIds.Contains(dummyActivityId6));

            // Seventh dummy activity.
            Assert.AreEqual(1, graphBuilder.EndNode.IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId7));

            Assert.AreEqual(2, graphBuilder.EdgeTailNode(dummyActivityId7).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId7).IncomingEdges.Contains(activityId4));
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId7).IncomingEdges.Contains(dummyActivityId1));

            Assert.AreEqual(1, graphBuilder.EdgeTailNode(dummyActivityId7).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId7).OutgoingEdges.Contains(dummyActivityId7));

            Assert.IsTrue(graphBuilder.Edge(dummyActivityId7).Content.IsDummy);

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(dummyActivityId7).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId7).IncomingEdges.Contains(dummyActivityId7));

            Assert.AreEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(dummyActivityId7).Id);
            Assert.AreNotEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(dummyActivityId7).Id);
        }

        [TestMethod]
        public void ArrowGraphBuilder_FourActivitiesOneDependentOnOtherThreeGetAncestorNodesLookup_AncestorsAsExpected()
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
            var graphBuilder = new ArrowGraphBuilder<int, IActivity<int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());

            var activity1 = new Activity<int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            Assert.IsTrue(result1);

            var activity2 = new Activity<int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            Assert.IsTrue(result2);

            var activity3 = new Activity<int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3);
            Assert.IsTrue(result3);

            var activity4 = new Activity<int>(activityId4, 0);
            bool result4 = graphBuilder.AddActivity(activity4, new HashSet<int>(new[] { activityId1, activityId2, activityId3 }));
            Assert.IsTrue(result4);

            IDictionary<int, HashSet<int>> ancestorNodesLookup = graphBuilder.GetAncestorNodesLookup();

            // Start node (event 1).
            Assert.AreEqual(0, ancestorNodesLookup[eventId1].Count);

            // End node (event 2).
            HashSet<int> endNodeAncestors = ancestorNodesLookup[eventId2];
            Assert.AreEqual(6, endNodeAncestors.Count);
            Assert.IsTrue(endNodeAncestors.Contains(eventId1));
            Assert.IsTrue(endNodeAncestors.Contains(eventId3));
            Assert.IsTrue(endNodeAncestors.Contains(eventId4));
            Assert.IsTrue(endNodeAncestors.Contains(eventId5));
            Assert.IsTrue(endNodeAncestors.Contains(eventId6));
            Assert.IsTrue(endNodeAncestors.Contains(eventId7));

            // Event 3.
            HashSet<int> event2NodeAncestors = ancestorNodesLookup[eventId3];
            Assert.AreEqual(1, event2NodeAncestors.Count);
            Assert.IsTrue(event2NodeAncestors.Contains(eventId1));

            // Event 4.
            HashSet<int> event4NodeAncestors = ancestorNodesLookup[eventId4];
            Assert.AreEqual(1, event4NodeAncestors.Count);
            Assert.IsTrue(event4NodeAncestors.Contains(eventId1));

            // Event 5.
            HashSet<int> event5NodeAncestors = ancestorNodesLookup[eventId5];
            Assert.AreEqual(1, event5NodeAncestors.Count);
            Assert.IsTrue(event5NodeAncestors.Contains(eventId1));

            // Event 6.
            HashSet<int> event6NodeAncestors = ancestorNodesLookup[eventId6];
            Assert.AreEqual(4, event6NodeAncestors.Count);
            Assert.IsTrue(event6NodeAncestors.Contains(eventId1));
            Assert.IsTrue(event6NodeAncestors.Contains(eventId3));
            Assert.IsTrue(event6NodeAncestors.Contains(eventId4));
            Assert.IsTrue(event6NodeAncestors.Contains(eventId5));

            // Event 7.
            HashSet<int> event7NodeAncestors = ancestorNodesLookup[eventId7];
            Assert.AreEqual(5, event7NodeAncestors.Count);
            Assert.IsTrue(event7NodeAncestors.Contains(eventId1));
            Assert.IsTrue(event7NodeAncestors.Contains(eventId3));
            Assert.IsTrue(event7NodeAncestors.Contains(eventId4));
            Assert.IsTrue(event7NodeAncestors.Contains(eventId5));
            Assert.IsTrue(event7NodeAncestors.Contains(eventId6));
        }

        [TestMethod]
        public void ArrowGraphBuilder_ThreeActivitiesOneDependentOnOtherTwoWithTwoUnnecessaryDummies_TransitiveReductionAsExpected()
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
            var graphBuilder = new ArrowGraphBuilder<int, IActivity<int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());

            var activity1 = new Activity<int>(activityId1, 0);
            bool result1 = graphBuilder.AddActivity(activity1);
            Assert.IsTrue(result1);

            var activity2 = new Activity<int>(activityId2, 0);
            bool result2 = graphBuilder.AddActivity(activity2);
            Assert.IsTrue(result2);

            var activity3 = new Activity<int>(activityId3, 0);
            bool result3 = graphBuilder.AddActivity(activity3, new HashSet<int>(new[] { activityId1, activityId2, activityId6 }));
            Assert.IsTrue(result3);

            var activity4 = Activity<int>.CreateActivityDummy(activityId4);
            bool result4 = graphBuilder.AddActivity(activity4);
            Assert.IsTrue(result4);

            var activity5 = Activity<int>.CreateActivityDummy(activityId5);
            bool result5 = graphBuilder.AddActivity(activity5, new HashSet<int>(new[] { activityId1 }));
            Assert.IsTrue(result5);

            var activity6 = Activity<int>.CreateActivityDummy(activityId6);
            bool result6 = graphBuilder.AddActivity(activity6);
            Assert.IsTrue(result5);

            Assert.AreEqual(15, graphBuilder.EdgeIds.Count());
            Assert.AreEqual(10, graphBuilder.NodeIds.Count());

            // First activity.
            Assert.AreEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(activityId1).Id);
            Assert.AreNotEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(activityId1).Id);

            Assert.AreEqual(4, graphBuilder.StartNode.OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId1));
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId2));
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId4));
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId6));
            Assert.AreEqual(4, graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId1));
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId2));
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId4));
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId6));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1));

            Assert.AreEqual(3, graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId1));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId3));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId7));

            // Second activity.
            Assert.AreEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(activityId2).Id);
            Assert.AreNotEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(activityId2).Id);

            Assert.AreEqual(4, graphBuilder.StartNode.OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId1));
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId2));
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId4));
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId6));
            Assert.AreEqual(4, graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId1));
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2));
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId4));
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId6));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2));

            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId2));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId4));

            // Third activity.
            Assert.AreNotEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(activityId3).Id);
            Assert.AreNotEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(activityId3).Id);

            Assert.AreEqual(3, graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId3));
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId4));
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId9));

            Assert.AreEqual(1, graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Contains(activityId3));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Contains(activityId3));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Contains(dummyActivityId5));

            // First dummy activity.
            Assert.AreNotEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(dummyActivityId1).Id);
            Assert.AreEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(dummyActivityId1).Id);

            Assert.AreEqual(1, graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Contains(activityId1));

            Assert.AreEqual(3, graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId1));
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId3));
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId7));

            Assert.AreEqual(5, graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId1));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId2));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId5));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId6));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId8));

            // Second dummy activity.
            Assert.AreNotEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(dummyActivityId2).Id);
            Assert.AreEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(dummyActivityId2).Id);

            Assert.AreEqual(1, graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId2).IncomingEdges.Contains(activityId2));

            Assert.AreEqual(2, graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId2));
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId2).OutgoingEdges.Contains(dummyActivityId4));

            Assert.AreEqual(5, graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId1));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId2));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId5));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId6));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId2).IncomingEdges.Contains(dummyActivityId8));

            // Third dummy activity.
            Assert.AreNotEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(dummyActivityId3).Id);
            Assert.AreNotEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(dummyActivityId3).Id);

            Assert.AreEqual(1, graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Contains(activityId1));

            Assert.AreEqual(3, graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Contains(dummyActivityId1));
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Contains(dummyActivityId3));
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Contains(dummyActivityId7));

            Assert.AreEqual(3, graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId3));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId4));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId9));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(dummyActivityId3).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId3).OutgoingEdges.Contains(activityId3));

            // Transitive Reduction.
            bool result10 = graphBuilder.TransitiveReduction();
            Assert.IsTrue(result10);

            Assert.AreEqual(13, graphBuilder.EdgeIds.Count());
            Assert.AreEqual(10, graphBuilder.NodeIds.Count());
            Assert.IsTrue(graphBuilder.AllDependenciesSatisfied);

            // First activity.
            Assert.AreEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(activityId1).Id);
            Assert.AreNotEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(activityId1).Id);

            Assert.AreEqual(4, graphBuilder.StartNode.OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId1));
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId2));
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId4));
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId6));
            Assert.AreEqual(4, graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId1));
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId2));
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId4));
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId6));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1));

            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId3));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId7));

            // Second activity.
            Assert.AreEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(activityId2).Id);
            Assert.AreNotEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(activityId2).Id);

            Assert.AreEqual(4, graphBuilder.StartNode.OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId1));
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId2));
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId4));
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId6));
            Assert.AreEqual(4, graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId1));
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2));
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId4));
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId6));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId4));

            // Third activity.
            Assert.AreNotEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(activityId3).Id);
            Assert.AreNotEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(activityId3).Id);

            Assert.AreEqual(3, graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId3));
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId4));
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId9));

            Assert.AreEqual(1, graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Contains(activityId3));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Contains(activityId3));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId3).OutgoingEdges.Contains(dummyActivityId5));

            // First dummy activity.
            Assert.IsFalse(graphBuilder.EdgeIds.Contains(dummyActivityId1));

            // Second dummy activity.
            Assert.IsFalse(graphBuilder.EdgeIds.Contains(dummyActivityId2));

            // Third dummy activity.
            Assert.AreNotEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(dummyActivityId3).Id);
            Assert.AreNotEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(dummyActivityId3).Id);

            Assert.AreEqual(1, graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId3).IncomingEdges.Contains(activityId1));

            Assert.AreEqual(2, graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Contains(dummyActivityId3));
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId3).OutgoingEdges.Contains(dummyActivityId7));

            Assert.AreEqual(3, graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId3));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId4));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId3).IncomingEdges.Contains(dummyActivityId9));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(dummyActivityId3).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId3).OutgoingEdges.Contains(activityId3));
        }

        [TestMethod]
        public void ArrowGraphBuilder_FiveActivitiesWithThreeUnnecessaryDummies_RemoveRedundantDummyEdgesAsExpected()
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
            var graphBuilder = new ArrowGraphBuilder<int, IActivity<int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());

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

            Assert.AreEqual(13, graphBuilder.EdgeIds.Count());
            Assert.AreEqual(10, graphBuilder.NodeIds.Count());

            // RemoveRedundantEdges.
            bool result6 = graphBuilder.RemoveRedundantEdges();
            Assert.IsTrue(result6);

            Assert.AreEqual(9, graphBuilder.EdgeIds.Count());
            Assert.AreEqual(6, graphBuilder.NodeIds.Count());
            Assert.IsTrue(graphBuilder.AllDependenciesSatisfied);

            // First activity.
            Assert.AreEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(activityId1).Id);
            Assert.AreNotEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(activityId1).Id);

            Assert.AreEqual(2, graphBuilder.StartNode.OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId1));
            Assert.AreEqual(2, graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId1).OutgoingEdges.Contains(activityId1));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId1).IncomingEdges.Contains(activityId1));

            Assert.AreEqual(3, graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Count);

            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId1));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(dummyActivityId3));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId1).OutgoingEdges.Contains(activityId5));

            Assert.AreEqual(graphBuilder.EdgeHeadNode(activityId1).Id, graphBuilder.EdgeTailNode(dummyActivityId1).Id);

            // Second activity.
            Assert.AreEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(activityId2).Id);
            Assert.AreNotEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(activityId2).Id);

            Assert.AreEqual(2, graphBuilder.StartNode.OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.StartNode.OutgoingEdges.Contains(activityId2));
            Assert.AreEqual(2, graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId2).OutgoingEdges.Contains(activityId2));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId2).IncomingEdges.Contains(activityId2));

            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(activityId4));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId2).OutgoingEdges.Contains(dummyActivityId2));

            Assert.AreEqual(graphBuilder.EdgeHeadNode(activityId2).Id, graphBuilder.EdgeTailNode(activityId4).Id);

            // Fourth activity.
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(activityId4).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId4).IncomingEdges.Contains(activityId2));

            Assert.AreEqual(2, graphBuilder.EdgeTailNode(activityId4).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId4).OutgoingEdges.Contains(activityId4));
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId4).OutgoingEdges.Contains(dummyActivityId2));

            Assert.AreEqual(2, graphBuilder.EdgeHeadNode(activityId4).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId4).IncomingEdges.Contains(activityId4));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId4).IncomingEdges.Contains(dummyActivityId3));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId4).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId4).OutgoingEdges.Contains(activityId3));

            Assert.AreEqual(graphBuilder.EdgeHeadNode(activityId4).Id, graphBuilder.EdgeTailNode(activityId3).Id);

            // First dummy activity.
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId1).IncomingEdges.Contains(activityId1));

            Assert.AreEqual(3, graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId1));
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(dummyActivityId3));
            Assert.IsTrue(graphBuilder.EdgeTailNode(dummyActivityId1).OutgoingEdges.Contains(activityId5));

            Assert.IsTrue(graphBuilder.Edge(dummyActivityId1).Content.IsDummy);

            Assert.AreEqual(4, graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId1));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId2));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId8));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(activityId3));

            Assert.AreEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(dummyActivityId1).Id);
            Assert.AreNotEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(dummyActivityId1).Id);

            // Third activity.
            Assert.AreEqual(2, graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(dummyActivityId3));
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId3).IncomingEdges.Contains(activityId4));

            Assert.AreEqual(1, graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId3).OutgoingEdges.Contains(activityId3));

            Assert.AreEqual(4, graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId1));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId2));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(dummyActivityId1).IncomingEdges.Contains(dummyActivityId8));
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId3).IncomingEdges.Contains(activityId3));
            Assert.AreEqual(4, graphBuilder.EndNode.IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId1));
            Assert.IsTrue(graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId2));
            Assert.IsTrue(graphBuilder.EndNode.IncomingEdges.Contains(dummyActivityId8));
            Assert.IsTrue(graphBuilder.EndNode.IncomingEdges.Contains(activityId3));

            Assert.AreEqual(graphBuilder.EndNode.Id, graphBuilder.EdgeHeadNode(activityId3).Id);
            Assert.AreNotEqual(graphBuilder.StartNode.Id, graphBuilder.EdgeTailNode(activityId3).Id);

            // Fifth activity.
            Assert.AreEqual(1, graphBuilder.EdgeTailNode(activityId5).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId5).IncomingEdges.Contains(activityId1));

            Assert.AreEqual(3, graphBuilder.EdgeTailNode(activityId5).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId5).OutgoingEdges.Contains(activityId5));
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId5).OutgoingEdges.Contains(dummyActivityId1));
            Assert.IsTrue(graphBuilder.EdgeTailNode(activityId5).OutgoingEdges.Contains(dummyActivityId3));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId5).IncomingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId5).IncomingEdges.Contains(activityId5));

            Assert.AreEqual(1, graphBuilder.EdgeHeadNode(activityId5).OutgoingEdges.Count);
            Assert.IsTrue(graphBuilder.EdgeHeadNode(activityId5).OutgoingEdges.Contains(dummyActivityId8));

            Assert.AreEqual(graphBuilder.EdgeHeadNode(activityId5).Id, graphBuilder.EdgeTailNode(dummyActivityId8).Id);
        }

        [TestMethod]
        public void ArrowGraphBuilder_CtorCalledWithNullArrowGraph_ShouldThrowArgumentNullException()
        {
            int eventId = 0;
            int activityId1 = 1;
            Assert.ThrowsException<ArgumentNullException>(
                () => new ArrowGraphBuilder<int, IActivity<int>>(null, () => activityId1++, () => eventId = eventId.Next()));
        }

        [TestMethod]
        public void ArrowGraphBuilder_CtorCalledWithGraph_GraphSuccessfullyAssimilated()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int dummyActivityId = activityId5 + 1;
            var graphBuilder = new ArrowGraphBuilder<int, IActivity<int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());

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

            var graphBuilder2 = new ArrowGraphBuilder<int, IActivity<int>>(firstGraph, () => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
            var secondGraph = graphBuilder2.ToGraph();
            Assert.AreEqual(firstGraph, secondGraph);
        }

        [TestMethod]
        public void ArrowGraphBuilder_CtorCalledWithGraphWithMissingEdge_ShouldThrowArgumentException()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int dummyActivityId = activityId5 + 1;
            var graphBuilder = new ArrowGraphBuilder<int, IActivity<int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());

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
                () => new ArrowGraphBuilder<int, IActivity<int>>(graph, () => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next()));
        }

        [TestMethod]
        public void ArrowGraphBuilder_CtorCalledWithGraphWithTooManyEdges_ShouldThrowArgumentException()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int dummyActivityId = activityId5 + 1;
            var graphBuilder = new ArrowGraphBuilder<int, IActivity<int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());

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
            graph.Edges.Add(new Edge<int, IActivity<int>>(new Activity<int>(dummyActivityId = dummyActivityId.Next(), 0)));

            Assert.ThrowsException<ArgumentException>(
                () => new ArrowGraphBuilder<int, IActivity<int>>(graph, () => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next()));
        }

        [TestMethod]
        public void ArrowGraphBuilder_CtorCalledWithGraphWithMissingNode_ShouldThrowArgumentException()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int dummyActivityId = activityId5 + 1;
            var graphBuilder = new ArrowGraphBuilder<int, IActivity<int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());

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
            Node<int, IEvent<int>> node = graph.Nodes.First(x => x.NodeType == NodeType.Normal);
            graph.Nodes.Remove(node);

            Assert.ThrowsException<ArgumentException>(
                () => new ArrowGraphBuilder<int, IActivity<int>>(graph, () => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next()));
        }

        [TestMethod]
        public void ArrowGraphBuilder_CtorCalledWithGraphWithTooManyNodes_ShouldThrowArgumentException()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int dummyActivityId = activityId5 + 1;
            var graphBuilder = new ArrowGraphBuilder<int, IActivity<int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());

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
            graph.Nodes.Add(new Node<int, IEvent<int>>(new Event<int>(dummyActivityId = dummyActivityId.Next())));

            Assert.ThrowsException<ArgumentException>(
                () => new ArrowGraphBuilder<int, IActivity<int>>(graph, () => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next()));
        }

        [TestMethod]
        public void ArrowGraphBuilder_CtorCalledWithGraphWithNoStartNode_ShouldThrowArgumentException()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int dummyActivityId = activityId5 + 1;
            var graphBuilder = new ArrowGraphBuilder<int, IActivity<int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());

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
            Node<int, IEvent<int>> node = graph.Nodes.First(x => x.NodeType == NodeType.Start);
            node.SetNodeType(NodeType.Normal);

            Assert.ThrowsException<ArgumentException>(
                () => new ArrowGraphBuilder<int, IActivity<int>>(graph, () => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next()));
        }

        [TestMethod]
        public void ArrowGraphBuilder_CtorCalledWithGraphWithNoEndNode_ShouldThrowArgumentException()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int dummyActivityId = activityId5 + 1;
            var graphBuilder = new ArrowGraphBuilder<int, IActivity<int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());

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
            Node<int, IEvent<int>> node = graph.Nodes.First(x => x.NodeType == NodeType.End);
            node.SetNodeType(NodeType.Normal);

            Assert.ThrowsException<ArgumentException>(
                () => new ArrowGraphBuilder<int, IActivity<int>>(graph, () => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next()));
        }

        [TestMethod]
        public void ArrowGraphBuilder_CtorCalledWithGraphWithMoreThanOneStartNode_ShouldThrowArgumentException()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int dummyActivityId = activityId5 + 1;
            var graphBuilder = new ArrowGraphBuilder<int, IActivity<int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());

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

            Node<int, IEvent<int>> node = graph.Nodes.First(x => x.NodeType == NodeType.Normal);
            graph.Nodes.Remove(node);

            var newNode = new Node<int, IEvent<int>>(NodeType.Start, node.Content);
            foreach (int edgeId in node.OutgoingEdges)
            {
                newNode.OutgoingEdges.Add(edgeId);
            }
            graph.Nodes.Add(newNode);

            Assert.ThrowsException<ArgumentException>(
                () => new ArrowGraphBuilder<int, IActivity<int>>(graph, () => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next()));
        }

        [TestMethod]
        public void ArrowGraphBuilder_CtorCalledWithGraphWithMoreThanOneEndNode_ShouldThrowArgumentException()
        {
            int eventId = 0;
            int activityId1 = 1;
            int activityId2 = activityId1 + 1;
            int activityId3 = activityId2 + 1;
            int activityId4 = activityId3 + 1;
            int activityId5 = activityId4 + 1;
            int dummyActivityId = activityId5 + 1;
            var graphBuilder = new ArrowGraphBuilder<int, IActivity<int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());

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

            Node<int, IEvent<int>> node = graph.Nodes.First(x => x.NodeType == NodeType.Normal);
            graph.Nodes.Remove(node);

            var newNode = new Node<int, IEvent<int>>(NodeType.End, node.Content);
            foreach (int edgeId in node.IncomingEdges)
            {
                newNode.IncomingEdges.Add(edgeId);
            }
            graph.Nodes.Add(newNode);

            Assert.ThrowsException<ArgumentException>(
                () => new ArrowGraphBuilder<int, IActivity<int>>(graph, () => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next()));
        }

        [TestMethod]
        public void ArrowGraphBuilder_FindCircularDependencies_FindsCircularDependency()
        {
            int eventId = 0;
            int dummyActivityId = 100;
            var graphBuilder = new ArrowGraphBuilder<int, IActivity<int>>(() => dummyActivityId = dummyActivityId.Next(), () => eventId = eventId.Next());
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
