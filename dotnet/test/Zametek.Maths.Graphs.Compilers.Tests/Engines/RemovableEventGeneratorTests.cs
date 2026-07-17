using Shouldly;
using System;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class RemovableEventGeneratorTests
    {
        // Records the arguments it is asked for so the decorator's delegation can be verified.
        private sealed class RecordingEventGenerator : IEventGenerator<int>
        {
            public int LastId { get; private set; }

            public IEvent<int> Generate(int id)
            {
                LastId = id;
                return new Event<int>(id);
            }

            public IEvent<int> Generate(int id, int? earliestFinishTime, int? latestFinishTime)
            {
                LastId = id;
                return new Event<int>(id, earliestFinishTime, latestFinishTime);
            }
        }

        [Fact]
        public void RemovableEventGenerator_GivenGenerate_ThenReturnsRemovableEvent()
        {
            var generator = new RemovableEventGenerator<int>();

            IEvent<int> output = generator.Generate(5);

            output.Id.ShouldBe(5);
            output.CanBeRemoved.ShouldBeTrue();
        }

        [Fact]
        public void RemovableEventGenerator_GivenGenerate_WithFinishTimes_ThenReturnsRemovableEventWithThoseTimes()
        {
            var generator = new RemovableEventGenerator<int>();

            IEvent<int> output = generator.Generate(5, earliestFinishTime: 3, latestFinishTime: 7);

            output.Id.ShouldBe(5);
            output.EarliestFinishTime.ShouldBe(3);
            output.LatestFinishTime.ShouldBe(7);
            output.CanBeRemoved.ShouldBeTrue();
        }

        [Fact]
        public void RemovableEventGenerator_GivenGenerate_WithInjectedInner_ThenDelegatesToInnerAndFlagsRemovable()
        {
            var inner = new RecordingEventGenerator();
            var generator = new RemovableEventGenerator<int>(inner);

            IEvent<int> output = generator.Generate(9, earliestFinishTime: 1, latestFinishTime: 4);

            inner.LastId.ShouldBe(9);
            output.EarliestFinishTime.ShouldBe(1);
            output.LatestFinishTime.ShouldBe(4);
            output.CanBeRemoved.ShouldBeTrue();
        }

        [Fact]
        public void RemovableEventGenerator_GivenCtor_WithNullInner_ThenThrowsArgumentNullException()
        {
            Action act = () => new RemovableEventGenerator<int>(null);
            act.ShouldThrow<ArgumentNullException>();
        }
    }
}
