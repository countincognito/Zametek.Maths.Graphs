using Shouldly;
using System;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class EventGeneratorTests
    {
        [Fact]
        public void EventGenerator_GivenGenerate_ThenReturnsEventWithIdAndNotRemovable()
        {
            var generator = new EventGenerator<int>();

            IEvent<int> output = generator.Generate(5);

            output.Id.ShouldBe(5);
            output.EarliestFinishTime.ShouldBeNull();
            output.LatestFinishTime.ShouldBeNull();
            output.CanBeRemoved.ShouldBeFalse();
        }

        [Fact]
        public void EventGenerator_GivenGenerate_WithFinishTimes_ThenReturnsEventWithThoseTimesAndNotRemovable()
        {
            var generator = new EventGenerator<int>();

            IEvent<int> output = generator.Generate(5, earliestFinishTime: 3, latestFinishTime: 7);

            output.Id.ShouldBe(5);
            output.EarliestFinishTime.ShouldBe(3);
            output.LatestFinishTime.ShouldBe(7);
            output.CanBeRemoved.ShouldBeFalse();
        }
    }
}
