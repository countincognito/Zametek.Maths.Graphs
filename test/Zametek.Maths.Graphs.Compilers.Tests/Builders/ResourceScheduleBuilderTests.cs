using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shouldly;
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
        public void ResourceScheduleBuilder_Given_ResourceSchedule1_ForIndirectResource_ZeroFinishTime_Input_ThenActivityAllocationEmpty()
        {
            const int startTime = 0;
            const int finishTime = 0;

            int resourceId1 = 1;
            var resource = new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.Indirect, 1.0, 1.0, 0, Enumerable.Empty<int>());

            var rsb = new ResourceScheduleBuilder<int, int, int>(resource);
            var rs = rsb.ToResourceSchedule(Enumerable.Empty<IActivity<int, int, int>>(), startTime, finishTime);

            rs.ActivityAllocation.ShouldBeEmpty();
            rs.FinishTime.ShouldBe(finishTime);
            rs.Resource.ShouldBe(resource);
            rs.ScheduledActivities.ShouldBeEmpty();
        }

        [Fact]
        public void ResourceScheduleBuilder_Given_ResourceSchedule1_ForIndirectResource_LargeFinishTime_Input_ThenActivityAllocationFull()
        {
            const int startTime = 0;
            const int finishTime = 10;

            int resourceId1 = 1;
            var resource = new Resource<int, int>(resourceId1, string.Empty, false, false, InterActivityAllocationType.Indirect, 1.0, 1.0, 0, Enumerable.Empty<int>());

            var rsb = new ResourceScheduleBuilder<int, int, int>(resource);
            var rs = rsb.ToResourceSchedule(Enumerable.Empty<IActivity<int, int, int>>(), startTime, finishTime);

            rs.ActivityAllocation.Count().ShouldBe(10);
            rs.FinishTime.ShouldBe(finishTime);
            rs.Resource.ShouldBe(resource);
            rs.ScheduledActivities.ShouldBeEmpty();
        }

        [Fact]
        public void ResourceScheduleBuilder_Given_ResourceSchedule1_ForDirectResource_Input_ThenStart73AndFinish127()
        {
            const int start = 73;
            const int finish = 127;

            JObject json = JObject.Parse(m_Fixture.ResourceSchedule1_JsonString);

            int startTime = int.Parse(json.GetValue(@"StartTime", StringComparison.OrdinalIgnoreCase).ToString());
            int finishTime = int.Parse(json.GetValue(@"FinishTime", StringComparison.OrdinalIgnoreCase).ToString());
            Resource<int, int> resource = JsonConvert.DeserializeObject<Resource<int, int>>(json.GetValue(@"Resource", StringComparison.OrdinalIgnoreCase).ToString());
            IList<ScheduledActivity<int>> scheduledActivities = JsonConvert.DeserializeObject<IList<ScheduledActivity<int>>>(json.GetValue(@"ScheduledActivities", StringComparison.OrdinalIgnoreCase).ToString());

            var rsb = new ResourceScheduleBuilder<int, int, int>(resource);

            foreach (ScheduledActivity<int> scheduledActivity in scheduledActivities)
            {
                rsb.AppendActivity(scheduledActivity);
            }

            var rs = rsb.ToResourceSchedule(Enumerable.Empty<IActivity<int, int, int>>(), startTime, finishTime);

            var first = rs.ActivityAllocation.Take(start).Distinct();
            var second = rs.ActivityAllocation.Skip(start).Take(finish - start).Distinct();
            var third = rs.ActivityAllocation.Skip(finish).Distinct();

            first.Count().ShouldBe(1);
            first.Single().ShouldBe(false);
            second.Count().ShouldBe(1);
            second.Single().ShouldBe(true);
            third.Count().ShouldBe(1);
            third.Single().ShouldBe(false);
        }

        [Fact]
        public void ResourceScheduleBuilder_Given_ResourceSchedule2_ForDirectResource_Input_ThenStart73AndFinish101()
        {
            const int start = 73;
            const int finish = 101;

            JObject json = JObject.Parse(m_Fixture.ResourceSchedule2_JsonString);

            int startTime = int.Parse(json.GetValue(@"StartTime", StringComparison.OrdinalIgnoreCase).ToString());
            int finishTime = int.Parse(json.GetValue(@"FinishTime", StringComparison.OrdinalIgnoreCase).ToString());
            Resource<int, int> resource = JsonConvert.DeserializeObject<Resource<int, int>>(json.GetValue(@"Resource", StringComparison.OrdinalIgnoreCase).ToString());
            IList<ScheduledActivity<int>> scheduledActivities = JsonConvert.DeserializeObject<IList<ScheduledActivity<int>>>(json.GetValue(@"ScheduledActivities", StringComparison.OrdinalIgnoreCase).ToString());

            var rsb = new ResourceScheduleBuilder<int, int, int>(resource);

            foreach (ScheduledActivity<int> scheduledActivity in scheduledActivities)
            {
                rsb.AppendActivity(scheduledActivity);
            }

            var rs = rsb.ToResourceSchedule(Enumerable.Empty<IActivity<int, int, int>>(), startTime, finishTime);

            var first = rs.ActivityAllocation.Take(start).Distinct();
            var second = rs.ActivityAllocation.Skip(start).Take(finish - start).Distinct();
            var third = rs.ActivityAllocation.Skip(finish).Distinct();

            first.Count().ShouldBe(1);
            first.Single().ShouldBe(false);
            second.Count().ShouldBe(1);
            second.Single().ShouldBe(true);
            third.Count().ShouldBe(1);
            third.Single().ShouldBe(false);
        }

        [Theory]
        [InlineData(3, 6, 16, 1, 1, 1)] // Res3, Phase 1, 6 -> 16
        [InlineData(4, 8, 19, 1, 1, 1)] // Res4, Phase 2, 8 -> 19
        [InlineData(5, 6, 19, 1, 1, 1)] // Res5, Phase 1+2, 6 -> 19
        [InlineData(6, 0, 22, 0, 1, 0)] // Res6, Default, 0 -> 22
        [InlineData(7, 0, 0, 0, 0, 1)] // Res7, Phase 3, 0 -> 0
        [InlineData(8, 0, 3, 0, 1, 1)] // Res8, Phase 4 (single start), 0 -> 3
        [InlineData(9, 10, 12, 1, 1, 1)] // Res9, Phase 5 (single middle), 10 -> 12
        [InlineData(10, 19, 22, 1, 1, 0)] // Res10, Phase 6 (single end), 19 -> 22
        public void ResourceScheduleBuilder_Given_ResourceSchedule2_ForIndirectResource_WithPhase_ThenFindStartAndFinish(
            int resourceId, int start, int finish,
            int firstCount, int secondCount, int thirdCount)
        {
            const int startTime = 0;
            const int finishTime = 22;
            JObject json = JObject.Parse(m_Fixture.ResourceSchedule3_JsonString);

            var dependentActivities = JsonConvert.DeserializeObject<IList<DummyDependentActivity>>(json.GetValue(@"DependentActivities", StringComparison.OrdinalIgnoreCase).ToString());
            var resources = JsonConvert.DeserializeObject<IList<Resource<int, int>>>(json.GetValue(@"Resources", StringComparison.OrdinalIgnoreCase).ToString());

            var resource = resources.First(x => x.Id == resourceId);

            var rsb = new ResourceScheduleBuilder<int, int, int>(resource);

            var rs = rsb.ToResourceSchedule(dependentActivities, startTime, finishTime);

            var first = rs.ActivityAllocation.Take(start).Distinct();
            var second = rs.ActivityAllocation.Skip(start).Take(finish - start).Distinct();
            var third = rs.ActivityAllocation.Skip(finish).Distinct();

            first.Count().ShouldBe(firstCount);
            if (firstCount > 0)
            {
                first.Single().ShouldBe(false);
            }

            second.Count().ShouldBe(secondCount);
            if (secondCount > 0)
            {
                second.Single().ShouldBe(true);
            }

            third.Count().ShouldBe(thirdCount);
            if (thirdCount > 0)
            {
                third.Single().ShouldBe(false);
            }
        }

        [Fact]
        public void ResourceScheduleBuilder_Given_ResourceSchedule3_ForDirectAndIndirectResources_WithPhases_ThenResultsAreCorrect()
        {
            JObject json = JObject.Parse(m_Fixture.ResourceSchedule3_JsonString);

            var dependentActivities = JsonConvert.DeserializeObject<IList<DummyDependentActivity>>(json.GetValue(@"DependentActivities", StringComparison.OrdinalIgnoreCase).ToString());
            var resources = JsonConvert.DeserializeObject<IList<Resource<int, int>>>(json.GetValue(@"Resources", StringComparison.OrdinalIgnoreCase).ToString());
            var resourceSchedules = JsonConvert.DeserializeObject<IList<DummyResourceSchedule>>(json.GetValue(@"ResourceSchedules", StringComparison.OrdinalIgnoreCase).ToString());
            var workStreams = JsonConvert.DeserializeObject<IList<WorkStream<int>>>(json.GetValue(@"WorkStreams", StringComparison.OrdinalIgnoreCase).ToString());

            var graphCompiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();

            foreach (var activity in dependentActivities)
            {
                graphCompiler.AddActivity(activity);
            }

            var output = graphCompiler.Compile(
                resources.Cast<IResource<int, int>>().ToList(),
                workStreams.Cast<IWorkStream<int>>().ToList());

            var rs1 = resourceSchedules.First(x => x.Resource.Id == 1).AsBase();
            var rs2 = resourceSchedules.First(x => x.Resource.Id == 2).AsBase();
            var rs11 = resourceSchedules.First(x => x.Resource.Id == 11).AsBase();

            var outputRs1 = output.ResourceSchedules.First(x => x.Resource.Id == 1);
            var outputRs2 = output.ResourceSchedules.First(x => x.Resource.Id == 2);
            var outputRs11 = output.ResourceSchedules.First(x => x.Resource.Id == 11);

            rs1.ShouldBeEquivalentTo(outputRs1);
            rs2.ShouldBeEquivalentTo(outputRs2);
            rs11.ShouldBeEquivalentTo(outputRs11);
        }
    }
}
