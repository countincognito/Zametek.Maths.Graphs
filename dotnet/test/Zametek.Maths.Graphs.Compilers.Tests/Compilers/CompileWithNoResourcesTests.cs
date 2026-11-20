using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    // A compile with no resources. No activity waits for a resource, and so the result is the critical path of the plan
    // alone. The scheduler still puts the activities on lanes (schedules that have no resource), which show how many
    // activities run at the same time. Up to 4.1.1 the compile made each lane a synthetic resource: an activity depended
    // on the activity before it on its lane, and that changed the times of the plan. These tests hold that the lanes
    // change nothing, that no lane has two activities at once, and that a compile with real resources keeps its resource
    // dependencies.
    public class CompileWithNoResourcesTests
    {
        private static VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>> CompilerOf(
            params DependentActivity<int, int, int>[] activities)
        {
            var compiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            foreach (DependentActivity<int, int, int> activity in activities)
            {
                compiler.AddActivity(activity);
            }
            return compiler;
        }

        // The plain critical path of the same activities: the builder alone, with no scheduler and no lanes.
        private static VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> PlainCriticalPathOf(
            params DependentActivity<int, int, int>[] activities)
        {
            VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> builder =
                new VertexGraphFactory<int, int, int, IDependentActivity<int, int, int>>().CreateBuilder();
            foreach (DependentActivity<int, int, int> activity in activities)
            {
                var clone = (DependentActivity<int, int, int>)activity.CloneObject();
                builder.AddActivity(clone, [.. clone.Dependencies.Union(clone.PlanningDependencies)]);
            }
            builder.CalculateCriticalPath();
            builder.BackFillIsolatedNodes().ShouldBeTrue();
            return builder;
        }

        private static List<List<(int Id, int StartTime, int FinishTime)>> LanesOf(
            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation)
        {
            return [.. compilation.ResourceSchedules
                .Select(x => x.ScheduledActivities.Select(y => (y.Id, y.StartTime, y.FinishTime)).ToList())];
        }

        private static (int? EarliestStartTime, int? EarliestFinishTime, int? LatestStartTime, int? LatestFinishTime, int? FreeSlack) TimesOf(
            IActivity<int, int, int> activity)
        {
            return (activity.EarliestStartTime, activity.EarliestFinishTime, activity.LatestStartTime, activity.LatestFinishTime, activity.FreeSlack);
        }

        // The result of a compile with no resources is the critical path of the plan, and the lanes hold each activity
        // that takes time once, at its earliest start time, with no two activities at once.
        private static void ShouldBeThePlainCriticalPathWithLanes(
            VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>> compiler,
            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation,
            DependentActivity<int, int, int>[] activities,
            string customMessage = null)
        {
            VertexGraphBuilder<int, int, int, IDependentActivity<int, int, int>> plain = PlainCriticalPathOf(activities);
            Dictionary<int, IDependentActivity<int, int, int>> compiled = compilation.DependentActivities.ToDictionary(x => x.Id);

            compiler.FinishTime.ShouldBe(plain.FinishTime, customMessage);
            foreach (IDependentActivity<int, int, int> expected in plain.Activities)
            {
                TimesOf(compiled[expected.Id]).ShouldBe(TimesOf(expected), $@"{customMessage} activity {expected.Id}");
                compiled[expected.Id].IsCritical.ShouldBe(expected.IsCritical, $@"{customMessage} activity {expected.Id}");
                compiled[expected.Id].ResourceDependencies.ShouldBeEmpty($@"{customMessage} activity {expected.Id}");
            }

            var laneOf = new Dictionary<int, int>();
            int laneNumber = 0;
            foreach (IResourceSchedule<int, int, int> schedule in compilation.ResourceSchedules)
            {
                laneNumber++;
                schedule.Resource.ShouldBeNull(customMessage);
                IScheduledActivity<int> previous = null;
                foreach (IScheduledActivity<int> scheduledActivity in schedule.ScheduledActivities)
                {
                    scheduledActivity.StartTime.ShouldBe(
                        compiled[scheduledActivity.Id].EarliestStartTime.GetValueOrDefault(),
                        $@"{customMessage} activity {scheduledActivity.Id}");
                    if (previous != null)
                    {
                        scheduledActivity.StartTime.ShouldBeGreaterThanOrEqualTo(
                            previous.FinishTime,
                            $@"{customMessage} activities {previous.Id} and {scheduledActivity.Id}");
                    }
                    laneOf.ContainsKey(scheduledActivity.Id).ShouldBeFalse($@"{customMessage} activity {scheduledActivity.Id}");
                    laneOf.Add(scheduledActivity.Id, laneNumber);
                    previous = scheduledActivity;
                }
            }

            // An activity is allocated to the number of its lane, and a milestone is on no lane.
            foreach (IDependentActivity<int, int, int> activity in compiled.Values)
            {
                if (activity.Duration > 0)
                {
                    activity.AllocatedToResources.ShouldBe([laneOf[activity.Id]], $@"{customMessage} activity {activity.Id}");
                }
                else
                {
                    laneOf.ContainsKey(activity.Id).ShouldBeFalse($@"{customMessage} activity {activity.Id}");
                    activity.AllocatedToResources.ShouldBeEmpty($@"{customMessage} activity {activity.Id}");
                }
            }
        }

        // Activities 2 and 4 have no connection. The scheduler puts 4 after 2 on one lane, because the lane is free when 4
        // can start. Up to 4.1.1 the compile made that a dependency: 4 had the resource dependency 2, and 2 had LST 3,
        // LFT 4 and free slack 1. The plan gives 2 the whole time to the finish: LST 4, LFT 5 and free slack 4.
        [Fact]
        public void VertexGraphCompiler_GivenNoResourcesAndTwoUnrelatedActivitiesOnOneLane_ThenTheTimesAreThoseOfThePlan()
        {
            DependentActivity<int, int, int>[] activities =
            [
                new DependentActivity<int, int, int>(1, 5),
                new DependentActivity<int, int, int>(2, 1),
                new DependentActivity<int, int, int>(3, 2),
                new DependentActivity<int, int, int>(4, 1, [3]),
            ];
            VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>> compiler = CompilerOf(activities);

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation =
                compiler.Compile(TestContext.Current.CancellationToken);

            compilation.CompilationErrors.ShouldBeEmpty();
            compiler.FinishTime.ShouldBe(5);
            LanesOf(compilation).ShouldBe(
                [
                    [(1, 0, 5)],
                    [(2, 0, 1), (4, 2, 3)],
                    [(3, 0, 2)],
                ]);

            Dictionary<int, IDependentActivity<int, int, int>> compiled = compilation.DependentActivities.ToDictionary(x => x.Id);
            compiled[4].ResourceDependencies.ShouldBeEmpty();
            compiled[4].Dependencies.ShouldBe([3]);
            TimesOf(compiled[2]).ShouldBe((0, 1, 4, 5, 4));
            compiled[2].AllocatedToResources.ShouldBe([2]);
            compiled[4].AllocatedToResources.ShouldBe([2]);

            ShouldBeThePlainCriticalPathWithLanes(compiler, compilation, activities);
        }

        // Four activities with no dependencies, and activity 2 must finish by time 58. The scheduler holds an activity
        // with a maximum LFT back to the latest start that meets it, and so it puts 2 after 1 on one lane, at time 55.
        // Up to 4.1.1 the compile made that a dependency: 2 started at time 1, and the plan finished at time 4. The plan
        // alone lets each activity start at time 0, and it finishes with its longest activity, at time 3. Activity 2
        // is on a lane of its own, because each lane of the scheduler has an activity at time 0.
        [Fact]
        public void VertexGraphCompiler_GivenNoResourcesAndMaximumLatestFinishTime_ThenTheFinishIsThatOfThePlan()
        {
            DependentActivity<int, int, int>[] activities =
            [
                new DependentActivity<int, int, int>(1, 1),
                new DependentActivity<int, int, int>(2, 3) { MaximumLatestFinishTime = 58 },
                new DependentActivity<int, int, int>(3, 3),
                new DependentActivity<int, int, int>(4, 2),
            ];
            VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>> compiler = CompilerOf(activities);

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation =
                compiler.Compile(TestContext.Current.CancellationToken);

            compilation.CompilationErrors.ShouldBeEmpty();
            compiler.FinishTime.ShouldBe(3);
            compilation.DependentActivities.ShouldAllBe(x => x.EarliestStartTime == 0);
            compilation.DependentActivities.Single(x => x.Id == 2).ResourceDependencies.ShouldBeEmpty();
            LanesOf(compilation).ShouldBe(
                [
                    [(1, 0, 1)],
                    [(3, 0, 3)],
                    [(4, 0, 2)],
                    [(2, 0, 3)],
                ]);

            ShouldBeThePlainCriticalPathWithLanes(compiler, compilation, activities);
        }

        // The case that the synthetic resources were made for (November 2024). Activity 2 must finish by time 4, and the
        // scheduler holds it back to start at time 2, after activity 5 on one lane. The plan lets 2 start at time 1, when
        // 5 is not finished. Up to 4.1.1 the dependency on 5 moved 2 to time 2. Now 2 starts at time 1, on a lane of its
        // own, and no lane has two activities at once.
        [Fact]
        public void VertexGraphCompiler_GivenNoResourcesAndActivityHeldBackByMaximumLatestFinishTime_ThenNoLaneHasTwoActivitiesAtOnce()
        {
            DependentActivity<int, int, int>[] activities =
            [
                new DependentActivity<int, int, int>(1, 1),
                new DependentActivity<int, int, int>(2, 2, [1]) { MinimumEarliestStartTime = 1, MaximumLatestFinishTime = 4 },
                new DependentActivity<int, int, int>(3, 2, [1]),
                new DependentActivity<int, int, int>(4, 2, [1]),
                new DependentActivity<int, int, int>(5, 2),
            ];
            VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>> compiler = CompilerOf(activities);

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation =
                compiler.Compile(TestContext.Current.CancellationToken);

            compilation.CompilationErrors.ShouldBeEmpty();
            LanesOf(compilation).ShouldBe(
                [
                    [(1, 0, 1), (3, 1, 3)],
                    [(5, 0, 2)],
                    [(4, 1, 3)],
                    [(2, 1, 3)],
                ]);

            ShouldBeThePlainCriticalPathWithLanes(compiler, compilation, activities);
        }

        // Whatever the plan is, with dependencies, a minimum EST, a maximum LFT or a minimum free slack: the times, the
        // slack, the critical activities and the finish of a compile with no resources are those of the critical path of
        // the plan, no activity has a resource dependency, and no lane has two activities at once.
        [Fact]
        public void VertexGraphCompiler_GivenRandomPlansWithNoResources_ThenTheResultIsThePlainCriticalPath()
        {
            var random = new Random(20261120);
            int compared = 0;
            for (int plan = 0; plan < 200; plan++)
            {
                int count = random.Next(3, 16);
                var activities = new DependentActivity<int, int, int>[count];
                for (int id = 1; id <= count; id++)
                {
                    var dependencies = new List<int>();
                    for (int dependency = 1; dependency < id; dependency++)
                    {
                        if (random.Next(count) < 2)
                        {
                            dependencies.Add(dependency);
                        }
                    }
                    // One activity in ten is a milestone.
                    int duration = random.Next(10) == 0 ? 0 : random.Next(1, 6);
                    var activity = new DependentActivity<int, int, int>(id, duration, dependencies);
                    switch (random.Next(12))
                    {
                        case 0:
                            activity.MinimumEarliestStartTime = random.Next(1, 10);
                            break;
                        case 1:
                            activity.MaximumLatestFinishTime = random.Next(10, 40);
                            break;
                        case 2:
                            activity.MinimumFreeSlack = random.Next(1, 4);
                            break;
                    }
                    activities[id - 1] = activity;
                }
                VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>> compiler = CompilerOf(activities);

                IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation =
                    compiler.Compile(TestContext.Current.CancellationToken);

                // A plan that cannot meet a maximum LFT reports C0010, and it still has its times and its lanes.
                compilation.CompilationErrors.ShouldAllBe(x => x.ErrorCode == GraphCompilationErrorCode.C0010, $@"plan {plan}");
                ShouldBeThePlainCriticalPathWithLanes(compiler, compilation, activities, $@"plan {plan}");
                compared++;
            }
            compared.ShouldBe(200);
        }

        // A compile with a real resource is not a compile with no resources: the one resource does the two activities in
        // turn, the second depends on the first, and the first has no slack. This must stay.
        [Fact]
        public void VertexGraphCompiler_GivenOneResourceAndTwoUnrelatedActivities_ThenTheSecondDependsOnTheFirst()
        {
            VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>> compiler = CompilerOf(
                new DependentActivity<int, int, int>(1, 5),
                new DependentActivity<int, int, int>(2, 1));

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation = compiler.Compile(
                [new Resource<int, int>(1, string.Empty, false, false, InterActivityAllocationType.Direct, 1.0, 1.0, 0, [])],
                TestContext.Current.CancellationToken);

            compilation.CompilationErrors.ShouldBeEmpty();
            compilation.ResourceSchedules.Single().Resource!.Id.ShouldBe(1);
            LanesOf(compilation).ShouldBe([[(1, 0, 5), (2, 5, 6)]]);

            Dictionary<int, IDependentActivity<int, int, int>> compiled = compilation.DependentActivities.ToDictionary(x => x.Id);
            compiled[2].ResourceDependencies.ShouldBe([1]);
            compiled[2].Dependencies.ShouldBeEmpty();
            compiled[1].AllocatedToResources.ShouldBe([1]);
            compiled[2].AllocatedToResources.ShouldBe([1]);
            TimesOf(compiled[1]).ShouldBe((0, 5, 0, 5, 0));
            TimesOf(compiled[2]).ShouldBe((5, 6, 5, 6, 0));
            compiler.FinishTime.ShouldBe(6);
        }
    }
}
