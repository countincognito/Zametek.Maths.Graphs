using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    // Which resource an activity takes when it can use more than one. The scheduler goes through the ready activities in
    // the order of the priority list, and each takes the first free resource in the allocation order that it can use.
    // Then, if a ready activity still waits while a resource is free, the activities that started at that moment move to
    // free resources to let it start. Up to 4.1.1 they did not move, and the activity waited while a resource did nothing.
    // These tests hold the rule, its limit, and what it must leave alone: the allocation order with no contention, the
    // activities that need all of their targets, and the resources that are not explicit targets.
    public class SchedulerResourceChoiceTests
    {
        private static Resource<int, int> ResourceOf(int id, bool isExplicitTarget, int allocationOrder)
        {
            return new Resource<int, int>(id, $@"R{id}", isExplicitTarget, false, InterActivityAllocationType.Direct, 1.0, 1.0, allocationOrder, []);
        }

        private static DependentActivity<int, int, int> ActivityOf(
            int id, int duration, int[] targetResources, LogicalOperator targetResourceOperator, params int[] dependencies)
        {
            var activity = new DependentActivity<int, int, int>(id, duration, dependencies)
            {
                TargetResourceOperator = targetResourceOperator,
            };
            activity.TargetResources.UnionWith(targetResources);
            return activity;
        }

        private static (VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>> Compiler, IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> Compilation) Compile(
            List<IResource<int, int>> resources,
            params DependentActivity<int, int, int>[] activities)
        {
            var compiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            foreach (DependentActivity<int, int, int> activity in activities)
            {
                compiler.AddActivity(activity);
            }
            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation =
                compiler.Compile(resources, TestContext.Current.CancellationToken);
            compilation.CompilationErrors.ShouldBeEmpty();
            return (compiler, compilation);
        }

        // The schedule of each resource that has one, by the ID of the resource.
        private static Dictionary<int, List<(int Id, int StartTime, int FinishTime)>> SchedulesOf(
            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation)
        {
            return compilation.ResourceSchedules
                .Where(x => x.ScheduledActivities.Any())
                .ToDictionary(
                    x => x.Resource!.Id,
                    x => x.ScheduledActivities.Select(y => (y.Id, y.StartTime, y.FinishTime)).ToList());
        }

        // Activities 2 and 3 are ready at time 1, and 2 is first in the priority list. Activity 2 can use R1 or R2, and it
        // takes R1, the first in the allocation order. Activity 3 can use only R1. Up to 4.1.1 it waited for a day while R2
        // did nothing: R1 had 1, 2 and 3, R2 had 4, and the plan finished at time 4. Now 2 moves to R2, and 3 starts on R1.
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void VertexGraphCompiler_GivenActivityWithTwoTargetsAndAnotherThatCanUseOnlyTheFirst_ThenTheFirstGivesWay(bool isExplicitTarget)
        {
            (VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>> compiler, IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation) = Compile(
                [ResourceOf(1, isExplicitTarget, 0), ResourceOf(2, isExplicitTarget, 1)],
                ActivityOf(1, 1, [1], LogicalOperator.OR),
                ActivityOf(2, 1, [1, 2], LogicalOperator.OR, 1),
                ActivityOf(3, 1, [1], LogicalOperator.OR, 1),
                ActivityOf(4, 1, [2], LogicalOperator.OR, 2, 3));

            Dictionary<int, List<(int Id, int StartTime, int FinishTime)>> schedules = SchedulesOf(compilation);
            schedules[1].ShouldBe([(1, 0, 1), (3, 1, 2)]);
            schedules[2].ShouldBe([(2, 1, 2), (4, 2, 3)]);
            compiler.FinishTime.ShouldBe(3);

            Dictionary<int, IDependentActivity<int, int, int>> compiled = compilation.DependentActivities.ToDictionary(x => x.Id);
            compiled[2].AllocatedToResources.ShouldBe([2]);
            compiled[3].AllocatedToResources.ShouldBe([1]);
            compiled[3].ResourceDependencies.ShouldBe([1]);
            compiled[4].ResourceDependencies.ShouldBe([2]);
        }

        // The same with an activity that has no targets: it can use each resource that is not an explicit target, and it
        // takes the first one in the allocation order. It moves to the second for the activity that can use only the first.
        [Fact]
        public void VertexGraphCompiler_GivenActivityWithNoTargetsAndAnotherThatCanUseOnlyTheFirstResource_ThenTheFirstGivesWay()
        {
            (VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>> compiler, IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation) = Compile(
                [ResourceOf(1, false, 0), ResourceOf(2, false, 1)],
                ActivityOf(1, 1, [1], LogicalOperator.OR),
                ActivityOf(2, 1, [], LogicalOperator.AND, 1),
                ActivityOf(3, 1, [1], LogicalOperator.OR, 1),
                ActivityOf(4, 1, [2], LogicalOperator.OR, 2, 3));

            Dictionary<int, List<(int Id, int StartTime, int FinishTime)>> schedules = SchedulesOf(compilation);
            schedules[1].ShouldBe([(1, 0, 1), (3, 1, 2)]);
            schedules[2].ShouldBe([(2, 1, 2), (4, 2, 3)]);
            compiler.FinishTime.ShouldBe(3);
        }

        // Activity 2 needs R1 and R2. Activity 1 took R1 at the same moment, and it can also use R3, which is free. Up to
        // 4.1.1 activity 2 waited until time 3. Now 1 moves to R3, and 2 starts at once on R1 and R2.
        [Fact]
        public void VertexGraphCompiler_GivenActivityThatNeedsAllOfItsTargetsAndAnotherThatTookOneOfThem_ThenTheOtherGivesWay()
        {
            (VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>> compiler, IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation) = Compile(
                [ResourceOf(1, true, 0), ResourceOf(2, true, 1), ResourceOf(3, true, 2)],
                ActivityOf(1, 3, [1, 3], LogicalOperator.OR),
                ActivityOf(2, 1, [1, 2], LogicalOperator.AND));

            Dictionary<int, List<(int Id, int StartTime, int FinishTime)>> schedules = SchedulesOf(compilation);
            schedules[1].ShouldBe([(2, 0, 1)]);
            schedules[2].ShouldBe([(2, 0, 1)]);
            schedules[3].ShouldBe([(1, 0, 3)]);
            compiler.FinishTime.ShouldBe(3);
        }

        // A move can need a chain of moves. Activity 3 can use only R1, which 1 took. Activity 1 can also use R2, which 2
        // took, and 2 can also use R3, which is free. Thus, 2 moves to R3, 1 moves to R2, and 3 starts on R1. Up to 4.1.1
        // activity 3 waited until time 2, and R3 did nothing.
        [Fact]
        public void VertexGraphCompiler_GivenTwoActivitiesThatMustBothMove_ThenAChainOfMovesFreesTheResource()
        {
            (VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>> compiler, IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation) = Compile(
                [ResourceOf(1, true, 0), ResourceOf(2, true, 1), ResourceOf(3, true, 2)],
                ActivityOf(1, 2, [1, 2], LogicalOperator.OR),
                ActivityOf(2, 2, [2, 3], LogicalOperator.OR),
                ActivityOf(3, 2, [1], LogicalOperator.OR));

            Dictionary<int, List<(int Id, int StartTime, int FinishTime)>> schedules = SchedulesOf(compilation);
            schedules[1].ShouldBe([(3, 0, 2)]);
            schedules[2].ShouldBe([(1, 0, 2)]);
            schedules[3].ShouldBe([(2, 0, 2)]);
            compiler.FinishTime.ShouldBe(2);
        }

        // The limit of the rule: only an activity that started at the same moment moves. Activity 1 took R1 at time 0.
        // Activity 2 can use only R1, and it cannot start before time 1. It waits until 1 finishes, and R2 does nothing,
        // because the scheduler does not look ahead and it does not move an activity that has started.
        [Fact]
        public void VertexGraphCompiler_GivenActivityThatStartedAtAnEarlierTime_ThenItDoesNotMove()
        {
            DependentActivity<int, int, int> activity2 = ActivityOf(2, 1, [1], LogicalOperator.OR);
            activity2.MinimumEarliestStartTime = 1;

            (VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>> compiler, IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation) = Compile(
                [ResourceOf(1, true, 0), ResourceOf(2, true, 1)],
                ActivityOf(1, 3, [1, 2], LogicalOperator.OR),
                activity2);

            Dictionary<int, List<(int Id, int StartTime, int FinishTime)>> schedules = SchedulesOf(compilation);
            schedules[1].ShouldBe([(1, 0, 3), (2, 3, 4)]);
            schedules.ContainsKey(2).ShouldBeFalse();
            compiler.FinishTime.ShouldBe(4);
        }

        // With no other activity that wants a resource, an activity takes the first of its targets in the allocation
        // order, whatever the IDs of the resources are. This is the setting of the user, and it must stay.
        [Theory]
        [InlineData(0, 1, 1)]
        [InlineData(1, 0, 2)]
        public void VertexGraphCompiler_GivenActivityWithTwoTargetsAndNoContention_ThenItTakesTheFirstInTheAllocationOrder(
            int allocationOrder1, int allocationOrder2, int expectedResourceId)
        {
            (_, IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation) = Compile(
                [ResourceOf(1, true, allocationOrder1), ResourceOf(2, true, allocationOrder2)],
                ActivityOf(1, 2, [1, 2], LogicalOperator.OR),
                ActivityOf(2, 2, [1, 2], LogicalOperator.OR, 1));

            Dictionary<int, List<(int Id, int StartTime, int FinishTime)>> schedules = SchedulesOf(compilation);
            schedules.Keys.ShouldBe([expectedResourceId]);
            schedules[expectedResourceId].ShouldBe([(1, 0, 2), (2, 2, 4)]);
        }

        // Two activities that can each use R1 or R2 are ready at the same time. The first in the priority list takes the
        // first resource in the allocation order, and the second takes the other. Both start at once. Activity 3 makes
        // activity 2 the one with the least slack, and so 2 is first in the priority list.
        [Fact]
        public void VertexGraphCompiler_GivenTwoActivitiesWithTheSameTwoTargets_ThenTheFirstInThePriorityListTakesTheFirstResource()
        {
            (VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>> compiler, IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation) = Compile(
                [ResourceOf(1, true, 0), ResourceOf(2, true, 1)],
                ActivityOf(1, 3, [1, 2], LogicalOperator.OR),
                ActivityOf(2, 5, [1, 2], LogicalOperator.OR),
                ActivityOf(3, 1, [1, 2], LogicalOperator.OR, 1, 2));

            Dictionary<int, List<(int Id, int StartTime, int FinishTime)>> schedules = SchedulesOf(compilation);
            schedules[1].ShouldBe([(2, 0, 5), (3, 5, 6)]);
            schedules[2].ShouldBe([(1, 0, 3)]);
            compiler.FinishTime.ShouldBe(6);
        }

        // An activity with no targets takes the first resource in the allocation order that is not an explicit target,
        // and it never takes an explicit target.
        [Fact]
        public void VertexGraphCompiler_GivenActivitiesWithNoTargets_ThenTheyTakeTheResourcesThatAreNotExplicitTargetsInTheAllocationOrder()
        {
            (VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>> compiler, IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation) = Compile(
                [ResourceOf(1, true, 0), ResourceOf(2, false, 2), ResourceOf(3, false, 1)],
                ActivityOf(1, 4, [], LogicalOperator.AND),
                ActivityOf(2, 3, [], LogicalOperator.AND),
                ActivityOf(3, 2, [], LogicalOperator.AND));

            Dictionary<int, List<(int Id, int StartTime, int FinishTime)>> schedules = SchedulesOf(compilation);
            schedules.ContainsKey(1).ShouldBeFalse();
            schedules[3].ShouldBe([(1, 0, 4)]);
            schedules[2].ShouldBe([(2, 0, 3), (3, 3, 5)]);
            compiler.FinishTime.ShouldBe(5);
        }

        // An activity that needs all of its targets starts when each of them is free, and it is on each of them.
        [Theory]
        [InlineData(LogicalOperator.AND)]
        [InlineData(LogicalOperator.ACTIVE_AND)]
        public void VertexGraphCompiler_GivenActivityThatNeedsAllOfItsTargets_ThenItIsOnEachOfThem(LogicalOperator targetResourceOperator)
        {
            (VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>> compiler, IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation) = Compile(
                [ResourceOf(1, true, 0), ResourceOf(2, true, 1)],
                ActivityOf(1, 3, [2], LogicalOperator.OR),
                ActivityOf(2, 2, [1, 2], targetResourceOperator));

            Dictionary<int, List<(int Id, int StartTime, int FinishTime)>> schedules = SchedulesOf(compilation);
            schedules[1].ShouldBe([(2, 3, 5)]);
            schedules[2].ShouldBe([(1, 0, 3), (2, 3, 5)]);
            compilation.DependentActivities.Single(x => x.Id == 2).AllocatedToResources.ShouldBe([1, 2], ignoreOrder: true);
            compiler.FinishTime.ShouldBe(5);
        }

        // Three activities want two resources, and each resource is taken. Activity 2 can use only R1, and it waits for
        // it. No resource is free while it waits, and so no other choice of resource lets it start earlier.
        [Fact]
        public void VertexGraphCompiler_GivenMoreReadyActivitiesThanResources_ThenTheLastWaitsAndNoResourceIsFree()
        {
            (VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>> compiler, IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation) = Compile(
                [ResourceOf(1, true, 0), ResourceOf(2, true, 1)],
                ActivityOf(1, 2, [1, 2], LogicalOperator.OR),
                ActivityOf(2, 1, [1], LogicalOperator.OR),
                ActivityOf(3, 1, [2], LogicalOperator.OR));

            Dictionary<int, List<(int Id, int StartTime, int FinishTime)>> schedules = SchedulesOf(compilation);
            schedules[1].ShouldBe([(1, 0, 2), (2, 2, 3)]);
            schedules[2].ShouldBe([(3, 0, 1)]);
            compiler.FinishTime.ShouldBe(3);
        }

        // Whatever resource each activity takes, the schedule holds the rules of the plan: no resource has two activities
        // at once, each activity starts after the activities that it depends on, and each activity is on resources that
        // it can use.
        [Fact]
        public void VertexGraphCompiler_GivenRandomPlansWithTargets_ThenEachScheduleHoldsTheRulesOfThePlan()
        {
            var random = new Random(20261120);
            for (int plan = 0; plan < 300; plan++)
            {
                int resourceCount = random.Next(2, 5);
                var resources = new List<IResource<int, int>>();
                for (int id = 1; id <= resourceCount; id++)
                {
                    resources.Add(ResourceOf(id, random.Next(2) == 0, random.Next(resourceCount)));
                }
                bool anyOpenResource = resources.Any(x => !x.IsExplicitTarget);

                int count = random.Next(4, 21);
                var activities = new DependentActivity<int, int, int>[count];
                for (int id = 1; id <= count; id++)
                {
                    int[] targets;
                    LogicalOperator targetResourceOperator = LogicalOperator.OR;
                    int kind = random.Next(100);
                    if (kind < 30 && anyOpenResource)
                    {
                        targets = [];
                    }
                    else if (kind < 55)
                    {
                        targets = [random.Next(1, resourceCount + 1)];
                    }
                    else if (kind < 88)
                    {
                        targets = [.. Enumerable.Range(1, resourceCount).OrderBy(_ => random.Next()).Take(random.Next(2, resourceCount + 1))];
                    }
                    else
                    {
                        targets = [.. Enumerable.Range(1, resourceCount).OrderBy(_ => random.Next()).Take(2)];
                        targetResourceOperator = random.Next(3) == 0 ? LogicalOperator.ACTIVE_AND : LogicalOperator.AND;
                    }
                    var dependencies = new List<int>();
                    for (int dependency = 1; dependency < id; dependency++)
                    {
                        if (random.Next(count) < 2)
                        {
                            dependencies.Add(dependency);
                        }
                    }
                    activities[id - 1] = ActivityOf(id, random.Next(1, 6), targets, targetResourceOperator, [.. dependencies]);
                }

                (_, IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation) = Compile(resources, activities);

                Dictionary<int, IDependentActivity<int, int, int>> compiled = compilation.DependentActivities.ToDictionary(x => x.Id);
                var resourcesOf = activities.ToDictionary(x => x.Id, _ => new List<int>());
                foreach (IResourceSchedule<int, int, int> schedule in compilation.ResourceSchedules)
                {
                    IScheduledActivity<int> previous = null;
                    foreach (IScheduledActivity<int> scheduledActivity in schedule.ScheduledActivities.OrderBy(x => x.StartTime))
                    {
                        if (previous != null)
                        {
                            scheduledActivity.StartTime.ShouldBeGreaterThanOrEqualTo(previous.FinishTime, $@"plan {plan}, resource {schedule.Resource!.Id}");
                        }
                        scheduledActivity.StartTime.ShouldBe(compiled[scheduledActivity.Id].EarliestStartTime.GetValueOrDefault(), $@"plan {plan}, activity {scheduledActivity.Id}");
                        resourcesOf[scheduledActivity.Id].Add(schedule.Resource!.Id);
                        previous = scheduledActivity;
                    }
                }
                foreach (DependentActivity<int, int, int> activity in activities)
                {
                    foreach (int dependency in activity.Dependencies)
                    {
                        compiled[activity.Id].EarliestStartTime.GetValueOrDefault().ShouldBeGreaterThanOrEqualTo(
                            compiled[dependency].EarliestFinishTime.GetValueOrDefault(), $@"plan {plan}, activity {activity.Id} after {dependency}");
                    }

                    List<int> on = resourcesOf[activity.Id];
                    if (activity.TargetResources.Count == 0)
                    {
                        on.Count.ShouldBe(1, $@"plan {plan}, activity {activity.Id}");
                        resources.Single(x => x.Id == on[0]).IsExplicitTarget.ShouldBeFalse($@"plan {plan}, activity {activity.Id}");
                    }
                    else if (activity.TargetResourceOperator == LogicalOperator.OR)
                    {
                        on.Count.ShouldBe(1, $@"plan {plan}, activity {activity.Id}");
                        activity.TargetResources.ShouldContain(on[0], $@"plan {plan}, activity {activity.Id}");
                    }
                    else
                    {
                        on.ShouldBe(activity.TargetResources, ignoreOrder: true, $@"plan {plan}, activity {activity.Id}");
                    }
                }
            }
        }
    }
}
