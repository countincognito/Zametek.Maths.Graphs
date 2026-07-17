using Shouldly;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    // Regression tests for VertexGraphBuilder.CloneObject: the clone round-trips
    // through Event.CloneObject, which used to reset CanBeRemoved to false. That
    // silently made every edge on a cloned vertex builder non-removable, so
    // transitive reduction (which only removes removable edges) became a no-op
    // on clones.
    public class VertexGraphBuilderCloneTests
    {
        private static VertexGraphBuilder<int, int, int, IActivity<int, int, int>> BuildRedundantTriangle()
        {
            // 1 -> 2 -> 3 plus the redundant direct dependency 1 -> 3.
            var graphBuilder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(new NextIdGenerator<int>());
            graphBuilder.AddActivity(new Activity<int, int, int>(1, 5));
            graphBuilder.AddActivity(new Activity<int, int, int>(2, 5), [1]);
            graphBuilder.AddActivity(new Activity<int, int, int>(3, 5), [1, 2]);
            return graphBuilder;
        }

        [Fact]
        public void VertexGraphBuilder_GivenCloneObject_ThenEdgeEventsRemainRemovable()
        {
            var graphBuilder = BuildRedundantTriangle();
            graphBuilder.Edges.All(x => x.Content.CanBeRemoved).ShouldBeTrue();

            var clone = (VertexGraphBuilder<int, int, int, IActivity<int, int, int>>)graphBuilder.CloneObject();

            clone.Edges.All(x => x.Content.CanBeRemoved).ShouldBeTrue();
        }

        [Fact]
        public void VertexGraphBuilder_GivenCloneObject_ThenTransitiveReductionStillRemovesRedundantEdges()
        {
            var graphBuilder = BuildRedundantTriangle();
            graphBuilder.EdgeIds.Count().ShouldBe(3);

            var clone = (VertexGraphBuilder<int, int, int, IActivity<int, int, int>>)graphBuilder.CloneObject();

            clone.TransitiveReduction().ShouldBeTrue();

            // The redundant 1 -> 3 edge must be removed on the clone, exactly as
            // it would be on the original.
            clone.EdgeIds.Count().ShouldBe(2);
        }
    }
}
