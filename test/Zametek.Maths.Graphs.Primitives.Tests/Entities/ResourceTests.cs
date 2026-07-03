using Shouldly;
using System;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class ResourceTests
    {
        [Fact]
        public void Resource_GivenCtor_WithNullPhases_ThenThrowsArgumentNullException()
        {
            Action act = () => new Resource<int, int>(
                1, "R1", false, false, InterActivityAllocationType.None, 1.0, 1.0, 0, null);
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void Resource_GivenCtor_ThenPropertiesSet()
        {
            var resource = new Resource<int, int>(
                1, "R1", isExplicitTarget: true, isInactive: true,
                InterActivityAllocationType.Indirect, 2.5, 3.5, 7, [11, 12]);

            resource.Id.ShouldBe(1);
            resource.Name.ShouldBe("R1");
            resource.IsExplicitTarget.ShouldBeTrue();
            resource.IsInactive.ShouldBeTrue();
            resource.InterActivityAllocationType.ShouldBe(InterActivityAllocationType.Indirect);
            resource.UnitCost.ShouldBe(2.5);
            resource.UnitBilling.ShouldBe(3.5);
            resource.AllocationOrder.ShouldBe(7);
            resource.InterActivityPhases.ShouldBe([11, 12], ignoreOrder: true);
        }

        [Fact]
        public void Resource_GivenCloneObject_ThenAllPropertiesPreserved()
        {
            var resource = new Resource<int, int>(
                1, "R1", isExplicitTarget: true, isInactive: false,
                InterActivityAllocationType.Direct, 2.5, 3.5, 7, [11]);

            var clone = (Resource<int, int>)resource.CloneObject();

            clone.Id.ShouldBe(1);
            clone.Name.ShouldBe("R1");
            clone.IsExplicitTarget.ShouldBeTrue();
            clone.IsInactive.ShouldBeFalse();
            clone.InterActivityAllocationType.ShouldBe(InterActivityAllocationType.Direct);
            clone.UnitCost.ShouldBe(2.5);
            clone.UnitBilling.ShouldBe(3.5);
            clone.AllocationOrder.ShouldBe(7);
            clone.InterActivityPhases.ShouldBe([11]);
        }

        [Fact]
        public void Resource_GivenCloneObject_ThenPhasesAreIndependentCopies()
        {
            var resource = new Resource<int, int>(
                1, "R1", false, false, InterActivityAllocationType.None, 1.0, 1.0, 0, [11]);

            var clone = (Resource<int, int>)resource.CloneObject();
            clone.InterActivityPhases.Add(12);

            resource.InterActivityPhases.Count.ShouldBe(1);
            clone.InterActivityPhases.Count.ShouldBe(2);
        }
    }
}
