using Shouldly;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    // Correctness tests for the transitive reducer's shared-ancestor handling and the
    // strong-dependency walk's dummy-chain resolution - the paths whose recursion was
    // replaced with iterative, visited-set traversals.
    public class DependencyResolutionTests
    {
        [Fact]
        public void VertexGraphBuilder_GivenSharedAncestorAcrossTwoSinks_ThenTransitiveReductionRemovesRedundantEdgesFromBoth()
        {
            var builder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(new NextIdGenerator<int>(0));

            // A -> B, and two sinks (D1, D2) each depending on BOTH A (directly, redundantly)
            // and B. A is a shared ancestor of both sinks; the direct A->D1 and A->D2 edges are
            // redundant and must both be removed (guards the reducer's single shared visited set).
            builder.AddActivity(new Activity<int, int, int>(1, 1)).ShouldBeTrue();        // A
            builder.AddActivity(new Activity<int, int, int>(2, 1), [1]).ShouldBeTrue();    // B  dep A
            builder.AddActivity(new Activity<int, int, int>(3, 1), [1, 2]).ShouldBeTrue(); // D1 dep A, B
            builder.AddActivity(new Activity<int, int, int>(4, 1), [1, 2]).ShouldBeTrue(); // D2 dep A, B

            builder.TransitiveReduction().ShouldBeTrue();

            builder.ActivityDependencyIds(3).ShouldBe([2]);
            builder.ActivityDependencyIds(4).ShouldBe([2]);
            builder.ActivityDependencyIds(2).ShouldBe([1]);
        }

        [Fact]
        public void VertexGraphBuilder_GivenRealRootBehindDummyDiamond_ThenStrongActivityDependencyIdsResolvesRealRoot()
        {
            var builder = new VertexGraphBuilder<int, int, int, IActivity<int, int, int>>(new NextIdGenerator<int>(0));

            // A (real) sits behind two zero-duration (dummy) links B and C, which D depends on.
            builder.AddActivity(new Activity<int, int, int>(1, 1)).ShouldBeTrue();        // A real
            builder.AddActivity(new Activity<int, int, int>(2, 0), [1]).ShouldBeTrue();    // B dummy dep A
            builder.AddActivity(new Activity<int, int, int>(3, 0), [1]).ShouldBeTrue();    // C dummy dep A
            builder.AddActivity(new Activity<int, int, int>(4, 1), [2, 3]).ShouldBeTrue(); // D real dep B, C

            List<int> strong = builder.StrongActivityDependencyIds(4);

            // A is reachable via two dummy paths; it must be found (once, as a set) and the
            // dummies themselves excluded.
            strong.Distinct().ShouldBe([1]);
            strong.ShouldNotContain(2);
            strong.ShouldNotContain(3);
        }

        [Fact]
        public void ArrowGraphBuilder_GivenRealRootBehindDummyDiamond_ThenStrongActivityDependencyIdsResolvesRealRoot()
        {
            var builder = new ArrowGraphBuilder<int, int, int, IActivity<int, int, int>>(
                new NextIdGenerator<int>(100),
                new NextIdGenerator<int>(0));

            builder.AddActivity(new Activity<int, int, int>(1, 1)).ShouldBeTrue();        // A real
            builder.AddActivity(new Activity<int, int, int>(2, 0), [1]).ShouldBeTrue();    // B dummy dep A
            builder.AddActivity(new Activity<int, int, int>(3, 0), [1]).ShouldBeTrue();    // C dummy dep A
            builder.AddActivity(new Activity<int, int, int>(4, 1), [2, 3]).ShouldBeTrue(); // D real dep B, C

            List<int> strong = builder.StrongActivityDependencyIds(4);

            strong.Distinct().ShouldBe([1]);
            strong.ShouldNotContain(2);
            strong.ShouldNotContain(3);
        }
    }
}
