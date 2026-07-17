using Shouldly;
using System;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class EventTests
    {
        [Fact]
        public void Event_GivenCtor_WithIdOnly_ThenTimesAreNullAndNotRemovable()
        {
            var ev = new Event<int>(5);

            ev.Id.ShouldBe(5);
            ev.EarliestFinishTime.ShouldBeNull();
            ev.LatestFinishTime.ShouldBeNull();
            ev.CanBeRemoved.ShouldBeFalse();
        }

        [Fact]
        public void Event_GivenCtor_WithTimes_ThenTimesAreSet()
        {
            var ev = new Event<int>(5, 3, 7);

            ev.EarliestFinishTime.ShouldBe(3);
            ev.LatestFinishTime.ShouldBe(7);
        }

        [Fact]
        public void Event_GivenSetAsRemovable_ThenCanBeRemovedIsTrue()
        {
            var ev = new Event<int>(1);

            ev.SetAsRemovable();

            ev.CanBeRemoved.ShouldBeTrue();
        }

        [Fact]
        public void Event_GivenSetAsReadOnly_ThenCanBeRemovedIsFalse()
        {
            var ev = new Event<int>(1);
            ev.SetAsRemovable();

            ev.SetAsReadOnly();

            ev.CanBeRemoved.ShouldBeFalse();
        }

        [Fact]
        public void Event_GivenCloneObject_ThenAllPropertiesPreserved()
        {
            var ev = new Event<int>(5, 3, 7);

            var clone = (Event<int>)ev.CloneObject();

            clone.Id.ShouldBe(5);
            clone.EarliestFinishTime.ShouldBe(3);
            clone.LatestFinishTime.ShouldBe(7);
            clone.CanBeRemoved.ShouldBeFalse();
        }

        // Regression: CloneObject used to reset CanBeRemoved to false, which broke
        // transitive reduction on cloned vertex graph builders (their edge events
        // silently became non-removable).
        [Fact]
        public void Event_GivenCloneObject_WhenRemovable_ThenCloneIsAlsoRemovable()
        {
            var ev = new Event<int>(5, 3, 7);
            ev.SetAsRemovable();

            var clone = (Event<int>)ev.CloneObject();

            clone.CanBeRemoved.ShouldBeTrue();
        }
    }
}
