using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class ResourceScheduleBuilderTests
        : IClassFixture<ResourceScheduleBuilderFixture>
    {
        private readonly ResourceScheduleBuilderFixture m_Fixture;

        public ResourceScheduleBuilderTests(ResourceScheduleBuilderFixture fixture)
        {
            m_Fixture = fixture;
        }

        [Fact]
        public void ResourceScheduleBuilder_Given_ResourceSchedule1_Input_ThenStart73AndFinish127()
        {
            const int start = 73;
            const int finish = 127;

            JObject json = JObject.Parse(m_Fixture.ResourceSchedule1_JsonString);

            int finishTime = int.Parse(json.GetValue(@"FinishTime", StringComparison.OrdinalIgnoreCase).ToString());
            Resource<int> resource = JsonConvert.DeserializeObject<Resource<int>>(json.GetValue(@"Resource", StringComparison.OrdinalIgnoreCase).ToString());
            IList<ScheduledActivity<int>> scheduledActivities = JsonConvert.DeserializeObject<IList<ScheduledActivity<int>>>(json.GetValue(@"ScheduledActivities", StringComparison.OrdinalIgnoreCase).ToString());

            var rsb = new ResourceScheduleBuilder<int, int>(resource);

            foreach (ScheduledActivity<int> scheduledActivity in scheduledActivities)
            {
                rsb.AppendActivity(scheduledActivity);
            }

            var rs = rsb.ToResourceSchedule(finishTime);

            var first = rs.ActivityAllocation.Take(start).Distinct();
            var second = rs.ActivityAllocation.Skip(start).Take(finish - start).Distinct();
            var third = rs.ActivityAllocation.Skip(finish).Distinct();

            first.Count().Should().Be(1);
            first.Single().Should().Be(false);
            second.Count().Should().Be(1);
            second.Single().Should().Be(true);
            third.Count().Should().Be(1);
            third.Single().Should().Be(false);
        }

        [Fact]
        public void ResourceScheduleBuilder_Given_ResourceSchedule2_Input_ThenStart73AndFinish101()
        {
            const int start = 73;
            const int finish = 101;

            JObject json = JObject.Parse(m_Fixture.ResourceSchedule2_JsonString);

            int finishTime = int.Parse(json.GetValue(@"FinishTime", StringComparison.OrdinalIgnoreCase).ToString());
            Resource<int> resource = JsonConvert.DeserializeObject<Resource<int>>(json.GetValue(@"Resource", StringComparison.OrdinalIgnoreCase).ToString());
            IList<ScheduledActivity<int>> scheduledActivities = JsonConvert.DeserializeObject<IList<ScheduledActivity<int>>>(json.GetValue(@"ScheduledActivities", StringComparison.OrdinalIgnoreCase).ToString());

            var rsb = new ResourceScheduleBuilder<int, int>(resource);

            foreach (ScheduledActivity<int> scheduledActivity in scheduledActivities)
            {
                rsb.AppendActivity(scheduledActivity);
            }

            var rs = rsb.ToResourceSchedule(finishTime);

            var first = rs.ActivityAllocation.Take(start).Distinct();
            var second = rs.ActivityAllocation.Skip(start).Take(finish - start).Distinct();
            var third = rs.ActivityAllocation.Skip(finish).Distinct();

            first.Count().Should().Be(1);
            first.Single().Should().Be(false);
            second.Count().Should().Be(1);
            second.Single().Should().Be(true);
            third.Count().Should().Be(1);
            third.Single().Should().Be(false);
        }
    }
}
