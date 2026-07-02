using Shouldly;
using System;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    // Exercises the strongly-typed ICloneObject<T>.Clone() surface added alongside
    // the untyped ICloneObject.CloneObject().
    public class CloneObjectTests
    {
        [Fact]
        public void Event_GivenClone_ThenReturnsTypedCopyPreservingCanBeRemoved()
        {
            var ev = new Event<int>(1, 2, 3);
            ev.SetAsRemovable();

            IEvent<int> clone = ev.Clone();

            clone.ShouldNotBeSameAs(ev);
            clone.Id.ShouldBe(1);
            clone.EarliestFinishTime.ShouldBe(2);
            clone.LatestFinishTime.ShouldBe(3);
            clone.CanBeRemoved.ShouldBeTrue();
        }

        [Fact]
        public void Activity_GivenClone_ThenReturnsTypedCopy()
        {
            var activity = new Activity<int, int, int>(1, 5) { Name = @"A1" };

            IActivity<int, int, int> clone = activity.Clone();

            clone.ShouldNotBeSameAs(activity);
            clone.Id.ShouldBe(1);
            clone.Duration.ShouldBe(5);
            clone.Name.ShouldBe(@"A1");
        }

        [Fact]
        public void DependentActivity_GivenClone_ThenReturnsDependentActivityInstance()
        {
            var activity = new DependentActivity<int, int, int>(1, 5, new[] { 2, 3 });

            IActivity<int, int, int> clone = activity.Clone();

            var dependentClone = clone.ShouldBeOfType<DependentActivity<int, int, int>>();
            dependentClone.Dependencies.ShouldBe(new[] { 2, 3 }, ignoreOrder: true);
        }

        [Fact]
        public void Resource_GivenClone_ThenReturnsTypedCopy()
        {
            var resource = new Resource<int, int>(
                10, @"R1", isExplicitTarget: true, isInactive: false,
                InterActivityAllocationType.Indirect, 1.5, 2.5, 7, new[] { 4 });

            IResource<int, int> clone = resource.Clone();

            clone.ShouldNotBeSameAs(resource);
            clone.Id.ShouldBe(10);
            clone.Name.ShouldBe(@"R1");
            clone.IsExplicitTarget.ShouldBeTrue();
            clone.InterActivityAllocationType.ShouldBe(InterActivityAllocationType.Indirect);
            clone.AllocationOrder.ShouldBe(7);
            clone.InterActivityPhases.ShouldBe(new[] { 4 });
        }

        [Fact]
        public void WorkStream_GivenClone_ThenReturnsTypedCopy()
        {
            var workStream = new WorkStream<int>(3, @"Phase 1", isPhase: true);

            IWorkStream<int> clone = workStream.Clone();

            clone.ShouldNotBeSameAs(workStream);
            clone.Id.ShouldBe(3);
            clone.Name.ShouldBe(@"Phase 1");
            clone.IsPhase.ShouldBeTrue();
        }

        [Fact]
        public void ScheduledActivity_GivenClone_ThenReturnsTypedCopy()
        {
            var scheduled = new ScheduledActivity<int>(1, @"A1", false, false, false, 5, 10, 15);

            IScheduledActivity<int> clone = scheduled.Clone();

            clone.ShouldNotBeSameAs(scheduled);
            clone.Id.ShouldBe(1);
            clone.StartTime.ShouldBe(10);
            clone.FinishTime.ShouldBe(15);
        }

        [Fact]
        public void Edge_GivenClone_ThenReturnsTypedCopyWithClonedContent()
        {
            var edge = new Edge<int, IEvent<int>>(new Event<int>(1));

            Edge<int, IEvent<int>> clone = edge.Clone();

            clone.ShouldNotBeSameAs(edge);
            clone.Id.ShouldBe(1);
            clone.Content.ShouldNotBeSameAs(edge.Content);
        }

        [Fact]
        public void Node_GivenClone_ThenReturnsTypedCopyWithEdgesAndNodeType()
        {
            var node = new Node<int, IEvent<int>>(NodeType.Normal, new Event<int>(1));
            node.IncomingEdges.Add(5);
            node.OutgoingEdges.Add(6);

            Node<int, IEvent<int>> clone = node.Clone();

            clone.ShouldNotBeSameAs(node);
            clone.NodeType.ShouldBe(NodeType.Normal);
            clone.IncomingEdges.ShouldBe(new[] { 5 });
            clone.OutgoingEdges.ShouldBe(new[] { 6 });
        }
    }
}
