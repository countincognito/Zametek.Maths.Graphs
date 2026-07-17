using Shouldly;
using System;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class WorkStreamTests
    {
        [Fact]
        public void WorkStream_GivenCtor_ThenPropertiesSet()
        {
            var workStream = new WorkStream<int>(1, "phase1", isPhase: true);

            workStream.Id.ShouldBe(1);
            workStream.Name.ShouldBe("phase1");
            workStream.IsPhase.ShouldBeTrue();
        }

        [Fact]
        public void WorkStream_GivenCloneObject_ThenAllPropertiesPreserved()
        {
            var workStream = new WorkStream<int>(1, "phase1", isPhase: true);

            var clone = (WorkStream<int>)workStream.CloneObject();

            clone.Id.ShouldBe(1);
            clone.Name.ShouldBe("phase1");
            clone.IsPhase.ShouldBeTrue();
        }
    }
}
