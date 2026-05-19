using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    public class PriorityListResourceSchedulerTests
    {
        [Fact]
        public void PriorityListResourceScheduler_GivenCalculateResourceSchedules_WithNullPriorityList_ThenThrowsArgumentNullException()
        {
            var scheduler = new PriorityListResourceScheduler<int, int, int>();
            Action act = () => scheduler.CalculateResourceSchedules(
                null,
                [],
                infiniteResources: false,
                id => null,
                id => [],
                () => []);
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void PriorityListResourceScheduler_GivenCalculateResourceSchedules_WithNullFilteredResources_ThenThrowsArgumentNullException()
        {
            var scheduler = new PriorityListResourceScheduler<int, int, int>();
            Action act = () => scheduler.CalculateResourceSchedules(
                [],
                null,
                infiniteResources: false,
                id => null,
                id => [],
                () => []).ToList();
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void PriorityListResourceScheduler_GivenCalculateResourceSchedules_WithNullActivityLookup_ThenThrowsArgumentNullException()
        {
            var scheduler = new PriorityListResourceScheduler<int, int, int>();
            Action act = () => scheduler.CalculateResourceSchedules(
                [],
                [],
                infiniteResources: false,
                null,
                id => [],
                () => []).ToList();
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void PriorityListResourceScheduler_GivenCalculateResourceSchedules_WithNullStrongDependencyLookup_ThenThrowsArgumentNullException()
        {
            var scheduler = new PriorityListResourceScheduler<int, int, int>();
            Action act = () => scheduler.CalculateResourceSchedules(
                [],
                [],
                infiniteResources: false,
                id => null,
                null,
                () => []).ToList();
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void PriorityListResourceScheduler_GivenCalculateResourceSchedules_WithNullFinalActivitiesFactory_ThenThrowsArgumentNullException()
        {
            var scheduler = new PriorityListResourceScheduler<int, int, int>();
            Action act = () => scheduler.CalculateResourceSchedules(
                [],
                [],
                infiniteResources: false,
                id => null,
                id => [],
                null).ToList();
            act.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void PriorityListResourceScheduler_GivenCalculateResourceSchedules_WithEmptyPriorityListAndNoResources_ThenReturnsEmpty()
        {
            var scheduler = new PriorityListResourceScheduler<int, int, int>();

            IEnumerable<IResourceSchedule<int, int, int>> output = scheduler.CalculateResourceSchedules(
                [],
                [],
                infiniteResources: false,
                id => null,
                id => [],
                () => []);

            output.ShouldBeEmpty();
        }

        [Fact]
        public void PriorityListResourceScheduler_GivenCalculateResourceSchedules_WithSingleActivityAndSingleResource_ThenSchedulesActivity()
        {
            var scheduler = new PriorityListResourceScheduler<int, int, int>();
            var activity = new Activity<int, int, int>(1, 5) { EarliestStartTime = 0 };
            var resource = new Resource<int, int>(10, "R1", false, false, InterActivityAllocationType.Direct, 1.0, 1.0, 0, []);

            var schedules = scheduler.CalculateResourceSchedules(
                [1],
                [resource],
                infiniteResources: false,
                id => activity,
                id => [],
                () => [activity]).ToList();

            schedules.Count.ShouldBe(1);
            schedules[0].Resource.Id.ShouldBe(10);
            schedules[0].ScheduledActivities.Any(x => x.Id == 1).ShouldBeTrue();
        }

        [Fact]
        public void PriorityListResourceScheduler_GivenCalculateResourceSchedules_WithInfiniteResources_ThenSpawnsResourcesForAllActivities()
        {
            var scheduler = new PriorityListResourceScheduler<int, int, int>();
            var a1 = new Activity<int, int, int>(1, 5) { EarliestStartTime = 0 };
            var a2 = new Activity<int, int, int>(2, 5) { EarliestStartTime = 0 };

            IDictionary<int, IActivity<int, int, int>> lookup = new Dictionary<int, IActivity<int, int, int>>
            {
                [1] = a1,
                [2] = a2,
            };

            var schedules = scheduler.CalculateResourceSchedules(
                [1, 2],
                [],
                infiniteResources: true,
                id => lookup[id],
                id => [],
                () => [a1, a2]).ToList();

            schedules.Count.ShouldBeGreaterThanOrEqualTo(1);
            schedules.SelectMany(x => x.ScheduledActivities).Select(x => x.Id).OrderBy(x => x).ShouldBe([1, 2]);
        }

        [Fact]
        public void PriorityListResourceScheduler_GivenGatherUnavailableResources_WithNoTargetResources_ThenReturnsEmpty()
        {
            var activity = new Activity<int, int, int>(1, 5);
            var resources = new List<IResource<int, int>>();

            IList<IUnavailableResources<int, int>> output =
                PriorityListResourceScheduler<int, int, int>.GatherUnavailableResources([activity], resources);

            output.ShouldBeEmpty();
        }

        [Fact]
        public void PriorityListResourceScheduler_GivenGatherUnavailableResources_WithAndOperatorAndMissingResource_ThenReturnsActivityWithMissingIds()
        {
            var activity = new Activity<int, int, int>(1, 5)
            {
                TargetResourceOperator = LogicalOperator.AND,
            };
            activity.TargetResources.Add(10);
            activity.TargetResources.Add(20);
            var resources = new List<IResource<int, int>>
            {
                new Resource<int, int>(10, "R10", false, false, InterActivityAllocationType.Direct, 1.0, 1.0, 0, []),
            };

            IList<IUnavailableResources<int, int>> output =
                PriorityListResourceScheduler<int, int, int>.GatherUnavailableResources([activity], resources);

            output.Count.ShouldBe(1);
            output[0].Id.ShouldBe(1);
            output[0].ResourceIds.ShouldContain(20);
            output[0].ResourceIds.ShouldNotContain(10);
        }

        [Fact]
        public void PriorityListResourceScheduler_GivenGatherUnavailableResources_WithOrOperatorAndAllMissing_ThenReturnsActivityWithAllIds()
        {
            var activity = new Activity<int, int, int>(1, 5)
            {
                TargetResourceOperator = LogicalOperator.OR,
            };
            activity.TargetResources.Add(10);
            activity.TargetResources.Add(20);
            var resources = new List<IResource<int, int>>
            {
                new Resource<int, int>(30, "R30", false, false, InterActivityAllocationType.Direct, 1.0, 1.0, 0, []),
            };

            IList<IUnavailableResources<int, int>> output =
                PriorityListResourceScheduler<int, int, int>.GatherUnavailableResources([activity], resources);

            output.Count.ShouldBe(1);
            output[0].Id.ShouldBe(1);
            output[0].ResourceIds.ShouldContain(10);
            output[0].ResourceIds.ShouldContain(20);
        }

        [Fact]
        public void PriorityListResourceScheduler_GivenGatherUnavailableResources_WithOrOperatorAndPartialMatch_ThenReturnsEmpty()
        {
            var activity = new Activity<int, int, int>(1, 5)
            {
                TargetResourceOperator = LogicalOperator.OR,
            };
            activity.TargetResources.Add(10);
            activity.TargetResources.Add(20);
            var resources = new List<IResource<int, int>>
            {
                new Resource<int, int>(10, "R10", false, false, InterActivityAllocationType.Direct, 1.0, 1.0, 0, []),
            };

            IList<IUnavailableResources<int, int>> output =
                PriorityListResourceScheduler<int, int, int>.GatherUnavailableResources([activity], resources);

            output.ShouldBeEmpty();
        }

        [Fact]
        public void PriorityListResourceScheduler_GivenReplaceWithSyntheticResources_WithEmptyInput_ThenReturnsEmpty()
        {
            List<IResourceSchedule<int, int, int>> output =
                PriorityListResourceScheduler<int, int, int>.ReplaceWithSyntheticResources([]);

            output.ShouldBeEmpty();
        }

        [Fact]
        public void PriorityListResourceScheduler_GivenReplaceWithSyntheticResources_WithSchedules_ThenAssignsSyntheticResourceIds()
        {
            var resource = new Resource<int, int>(99, "Original", false, false, InterActivityAllocationType.Direct, 1.0, 1.0, 0, []);
            var schedule = new ResourceSchedule<int, int, int>(
                resource,
                [],
                0, 10,
                [],
                [],
                [],
                []);

            List<IResourceSchedule<int, int, int>> output =
                PriorityListResourceScheduler<int, int, int>.ReplaceWithSyntheticResources(
                    [schedule]);

            output.Count.ShouldBe(1);
            output[0].Resource.ShouldNotBeNull();
            output[0].Resource.Id.ShouldNotBe(99);
        }

        [Fact]
        public void PriorityListResourceScheduler_GivenCollectIndirectResourceSchedules_WithUnscheduledIndirectResource_ThenIncludesIndirectResource()
        {
            var indirect = new Resource<int, int>(50, "I", false, false, InterActivityAllocationType.Indirect, 1.0, 1.0, 0, []);
            var direct = new Resource<int, int>(60, "D", false, false, InterActivityAllocationType.Direct, 1.0, 1.0, 0, []);

            var output = PriorityListResourceScheduler<int, int, int>.CollectIndirectResourceSchedules(
                [indirect, direct],
                [],
                [],
                0, 10).ToList();

            output.Count.ShouldBe(1);
            output[0].Resource.Id.ShouldBe(50);
        }

        [Fact]
        public void PriorityListResourceScheduler_GivenCollectIndirectResourceSchedules_WithAllIndirectAlreadyScheduled_ThenReturnsEmpty()
        {
            var indirect = new Resource<int, int>(50, "I", false, false, InterActivityAllocationType.Indirect, 1.0, 1.0, 0, []);

            var scheduled = new ResourceSchedule<int, int, int>(
                indirect,
                [],
                0, 10,
                [],
                [],
                [],
                []);

            var output = PriorityListResourceScheduler<int, int, int>.CollectIndirectResourceSchedules(
                [indirect],
                [scheduled],
                [],
                0, 10).ToList();

            output.ShouldBeEmpty();
        }

        [Fact]
        public void PriorityListResourceScheduler_GivenGetResourcePhasesUsed_WithIntersectingPhases_ThenReturnsIntersection()
        {
            var resource = new Resource<int, int>(50, "R", false, false, InterActivityAllocationType.Indirect, 1.0, 1.0, 0, [ 1, 2, 3 ]);
            var schedule = new ResourceSchedule<int, int, int>(
                resource,
                [],
                0, 10,
                [],
                [],
                [],
                []);

            var workstreamsUsed = new HashSet<int> { 2, 3, 4 };

            HashSet<int> output = PriorityListResourceScheduler<int, int, int>.GetResourcePhasesUsed(
                [schedule],
                workstreamsUsed);

            output.ShouldContain(2);
            output.ShouldContain(3);
            output.ShouldNotContain(1);
            output.ShouldNotContain(4);
        }

        [Fact]
        public void PriorityListResourceScheduler_GivenGetResourcePhasesUsed_WithNoSchedulesHavingResource_ThenReturnsEmpty()
        {
            var schedule = new ResourceSchedule<int, int, int>(
                null,
                [],
                0, 10,
                [],
                [],
                [],
                []);

            HashSet<int> output = PriorityListResourceScheduler<int, int, int>.GetResourcePhasesUsed(
                [schedule],
                [1, 2]);

            output.ShouldBeEmpty();
        }

        [Fact]
        public void PriorityListResourceScheduler_GivenRebuildAlignedResourceSchedules_WithEmptyInput_ThenReturnsEmpty()
        {
            var output = PriorityListResourceScheduler<int, int, int>.RebuildAlignedResourceSchedules(
                [],
                infiniteResources: false,
                id => null,
                [],
                0, 10).ToList();

            output.ShouldBeEmpty();
        }
    }
}
