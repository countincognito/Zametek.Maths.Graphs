using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Zametek.Maths.Graphs.Tests
{
    // Locks in the time-gate semantics of PriorityListResourceScheduler so the
    // skip-ahead optimisation cannot change observable behaviour: activities gated
    // by EarliestStartTime idle until their gate opens, MaximumLatestFinishTime
    // schedules just-in-time, zero-duration activities complete on the tick after
    // they start, and activities queue behind busy resources.
    public class SchedulerTimeGateTests
    {
        private static Resource<int, int> CreateResource(int id)
        {
            return new Resource<int, int>(id, $@"R{id}", false, false, InterActivityAllocationType.None, 0.0, 0.0, 0, []);
        }

        [Fact]
        public void PriorityListResourceScheduler_GivenActivityGatedByEarliestStartTime_ThenSchedulesAtTheGate()
        {
            var scheduler = new PriorityListResourceScheduler<int, int, int>();
            var activity = new Activity<int, int, int>(1, 5) { EarliestStartTime = 250 };
            var graph = new FakeSchedulingGraph(activity);

            var schedules = scheduler.CalculateResourceSchedules(
                [1],
                [CreateResource(10)],
                infiniteResources: false,
                graph, TestContext.Current.CancellationToken).ToList();

            IScheduledActivity<int> scheduled = schedules.Single().ScheduledActivities.Single();
            scheduled.StartTime.ShouldBe(250);
            scheduled.FinishTime.ShouldBe(255);
        }

        [Fact]
        public void PriorityListResourceScheduler_GivenActivityWithMaximumLatestFinishTime_ThenSchedulesJustInTime()
        {
            var scheduler = new PriorityListResourceScheduler<int, int, int>();
            var activity = new Activity<int, int, int>(1, 5)
            {
                EarliestStartTime = 0,
                MaximumLatestFinishTime = 100,
            };
            var graph = new FakeSchedulingGraph(activity);

            var schedules = scheduler.CalculateResourceSchedules(
                [1],
                [CreateResource(10)],
                infiniteResources: false,
                graph, TestContext.Current.CancellationToken).ToList();

            // The scheduler delays a deadline-constrained activity until starting any
            // later would overshoot the deadline, so it finishes exactly on it.
            IScheduledActivity<int> scheduled = schedules.Single().ScheduledActivities.Single();
            scheduled.StartTime.ShouldBe(95);
            scheduled.FinishTime.ShouldBe(100);
        }

        [Fact]
        public void PriorityListResourceScheduler_GivenDeadlineConstrainedAndUnconstrainedActivities_ThenOnlyTheConstrainedOneWaits()
        {
            var scheduler = new PriorityListResourceScheduler<int, int, int>();
            var constrained = new Activity<int, int, int>(1, 5)
            {
                EarliestStartTime = 0,
                MaximumLatestFinishTime = 100,
            };
            var unconstrained = new Activity<int, int, int>(2, 5) { EarliestStartTime = 0 };
            var graph = new FakeSchedulingGraph(constrained, unconstrained);

            var schedules = scheduler.CalculateResourceSchedules(
                [1, 2],
                [CreateResource(10)],
                infiniteResources: false,
                graph, TestContext.Current.CancellationToken).ToList();

            List<IScheduledActivity<int>> scheduled = schedules.Single().ScheduledActivities.ToList();
            IScheduledActivity<int> first = scheduled.Single(x => x.Id == 2);
            IScheduledActivity<int> second = scheduled.Single(x => x.Id == 1);
            first.StartTime.ShouldBe(0);
            first.FinishTime.ShouldBe(5);
            second.StartTime.ShouldBe(95);
            second.FinishTime.ShouldBe(100);
        }

        [Fact]
        public void PriorityListResourceScheduler_GivenZeroDurationActivityGatedByEarliestStartTime_ThenSuccessorStartsOnTheNextTick()
        {
            var scheduler = new PriorityListResourceScheduler<int, int, int>();
            var milestone = new Activity<int, int, int>(1, 0) { EarliestStartTime = 100 };
            var successor = new Activity<int, int, int>(2, 5);
            var graph = new FakeSchedulingGraph(milestone, successor).WithStrongDependencies(2, 1);

            var schedules = scheduler.CalculateResourceSchedules(
                [1, 2],
                [CreateResource(10)],
                infiniteResources: false,
                graph, TestContext.Current.CancellationToken).ToList();

            List<IScheduledActivity<int>> scheduled = schedules.Single().ScheduledActivities.ToList();
            IScheduledActivity<int> milestoneScheduled = scheduled.Single(x => x.Id == 1);
            IScheduledActivity<int> successorScheduled = scheduled.Single(x => x.Id == 2);
            milestoneScheduled.StartTime.ShouldBe(100);
            milestoneScheduled.FinishTime.ShouldBe(100);
            // A zero-duration activity is only detected as completed on the tick after
            // it starts, so its successor begins one tick later.
            successorScheduled.StartTime.ShouldBe(101);
            successorScheduled.FinishTime.ShouldBe(106);
        }

        [Fact]
        public void PriorityListResourceScheduler_GivenActivityQueuedBehindBusyResource_ThenStartsWhenTheResourceFrees()
        {
            var scheduler = new PriorityListResourceScheduler<int, int, int>();
            var first = new Activity<int, int, int>(1, 7) { EarliestStartTime = 0 };
            var second = new Activity<int, int, int>(2, 3) { EarliestStartTime = 0 };
            var graph = new FakeSchedulingGraph(first, second);

            var schedules = scheduler.CalculateResourceSchedules(
                [1, 2],
                [CreateResource(10)],
                infiniteResources: false,
                graph, TestContext.Current.CancellationToken).ToList();

            List<IScheduledActivity<int>> scheduled = schedules.Single().ScheduledActivities.ToList();
            scheduled.Single(x => x.Id == 1).StartTime.ShouldBe(0);
            scheduled.Single(x => x.Id == 2).StartTime.ShouldBe(7);
            scheduled.Single(x => x.Id == 2).FinishTime.ShouldBe(10);
        }

        [Fact]
        public void VertexGraphCompiler_GivenMinimumEarliestStartTime_ThenScheduleHonoursTheDelay()
        {
            var compiler = new VertexGraphCompiler<int, int, int, IDependentActivity<int, int, int>>();
            compiler.AddActivity(new DependentActivity<int, int, int>(1, 5) { MinimumEarliestStartTime = 300 });
            compiler.AddActivity(new DependentActivity<int, int, int>(2, 5, [1]));

            IGraphCompilation<int, int, int, IDependentActivity<int, int, int>> compilation =
                compiler.Compile([CreateResource(10)], TestContext.Current.CancellationToken);

            compilation.CompilationErrors.ShouldBeEmpty();
            IDependentActivity<int, int, int> first = compilation.DependentActivities.Single(x => x.Id == 1);
            IDependentActivity<int, int, int> second = compilation.DependentActivities.Single(x => x.Id == 2);
            first.EarliestStartTime.ShouldBe(300);
            second.EarliestStartTime.ShouldBe(305);
        }
    }
}
