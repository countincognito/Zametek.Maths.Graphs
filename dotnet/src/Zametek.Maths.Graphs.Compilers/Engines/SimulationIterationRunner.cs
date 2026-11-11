using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Zametek.Maths.Graphs
{
    // One scheduling pass of the simulation, on a graph that is built once and then given new
    // durations. This is the seam behind which the cost of an iteration lives: everything that
    // the simulation needs from a schedule comes out of this class, and nothing outside it
    // touches the builder. A faster way to schedule (a flat kernel, a different priority list)
    // replaces this class and leaves the rest of the simulation as it is.
    //
    // A compile does a good deal that an iteration does not need. It checks the plan for errors
    // before and after, copies every activity to give its caller its own results, rebuilds the
    // schedules to align them with the final times, and works out the schedules of the indirect
    // resources. None of that changes the finish time or the critical activities. This class
    // keeps the steps that do:
    //
    //   1. the first critical-path calculation,
    //   2. the priority-list scheduling onto the resources,
    //   3. the resource dependencies that the schedule implies,
    //   4. the second critical-path calculation, which includes them.
    //
    // Then it checks each maximum LFT, because a maximum that the durations cannot meet pulls its
    // activity back, and the finish time with it. Such an iteration fails, as a compile reports an
    // error for it.
    //
    // The compile is the oracle. The tests compare the finish time and the critical activities
    // of this class with those of a full compile of the same plan with the same durations, for
    // many random durations, on one runner (and so they also find state that an iteration leaves
    // behind for the next).
    //
    // The builder is the caller's. It holds the engines that decide how a plan is scheduled (the
    // critical-path engine, the resource scheduler and the others), and so a plan is simulated with
    // the engines that its caller compiles it with. The builder must be new and empty.
    //
    // A runner holds a builder and the objects of one plan, and it is not safe for use by more
    // than one thread at a time. A simulation gives each thread a runner of its own.
    internal sealed class SimulationIterationRunner<T, TResourceId, TWorkStreamId, TActivity>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
        where TActivity : class, IDependentActivity<T, TResourceId, TWorkStreamId>
    {
        internal const int Failed = SimulationResult<T>.FailedFinishTime;

        private readonly VertexGraphBuilder<T, TResourceId, TWorkStreamId, TActivity> m_Builder;
        private readonly TActivity[] m_Activities;
        private readonly List<TActivity> m_ActivityList;
        private readonly List<IResource<TResourceId, TWorkStreamId>> m_ActiveResources;
        private readonly bool m_StopAfterTheFirstPass;
        private readonly bool m_ReplaceWithSyntheticResources;
        private readonly int[] m_ConstrainedActivities;
        private readonly List<int> m_MissedMaximumLatestFinishTimes = new List<int>();
        private SimulationIterationFailure m_Failure;

        // The activities are the runner's own: it changes their durations and their times, and so
        // the caller gives it copies. The resources are the runner's own too.
        internal SimulationIterationRunner(
            VertexGraphBuilder<T, TResourceId, TWorkStreamId, TActivity> builder,
            IReadOnlyList<TActivity> activities,
            IEnumerable<IResource<TResourceId, TWorkStreamId>> resources,
            bool scheduleOntoSyntheticResources)
        {
            m_Builder = builder ?? throw new ArgumentNullException(nameof(builder));
            if (m_Builder.Activities.Any())
            {
                throw new InvalidOperationException(@"The builder already holds activities. A simulation needs a new, empty builder from its factory each time.");
            }

            m_Activities = new TActivity[activities.Count];
            for (int i = 0; i < activities.Count; i++)
            {
                TActivity activity = activities[i];
                m_Activities[i] = activity;
                m_Builder.AddActivity(activity, new HashSet<T>(activity.Dependencies.Union(activity.PlanningDependencies)));
            }
            m_ActivityList = m_Builder.Activities.ToList();

            // The compile treats an empty list of resources as infinite resources, and a list of
            // resources that are all inactive as no resources to use. It is the list before the
            // filter that decides.
            List<IResource<TResourceId, TWorkStreamId>> allResources = resources.ToList();
            m_ActiveResources = allResources.Where(x => !x.IsInactive).ToList();
            bool infinite = allResources.Count == 0;
            m_StopAfterTheFirstPass = infinite && !scheduleOntoSyntheticResources;
            m_ReplaceWithSyntheticResources = infinite;

            // Only a maximum LFT can pull an activity back, and so only these activities can make an
            // iteration invalid (see Run).
            m_ConstrainedActivities = Enumerable.Range(0, m_Activities.Length)
                .Where(i => m_Activities[i].MaximumLatestFinishTime.HasValue)
                .ToArray();
        }

        // The activities, in the order of the plan.
        internal IReadOnlyList<TActivity> Activities => m_Activities;

        // Why the last run failed, or None.
        internal SimulationIterationFailure Failure => m_Failure;

        // The activities (by their places in the plan) that could not finish by their maximum LFTs in
        // the last run. The list is empty unless the run failed with InvalidConstraints.
        internal IReadOnlyList<int> MissedMaximumLatestFinishTimes => m_MissedMaximumLatestFinishTimes;

        // Gives each activity its duration (in the order of the plan), schedules the plan, and
        // returns the finish time, or Failed when the resource scheduler could not place every
        // activity, when the schedule runs past the limit, or when an activity cannot finish by its
        // maximum LFT. Failure then tells why. After a call that did not fail, each activity holds
        // its times and its critical flag, and the resource dependencies that the schedule implied.
        internal int Run(int[] durations, CancellationToken cancellationToken)
        {
            m_Failure = SimulationIterationFailure.None;
            m_MissedMaximumLatestFinishTimes.Clear();

            for (int i = 0; i < m_Activities.Length; i++)
            {
                m_Activities[i].Duration = durations[i];
            }
            m_Builder.ResetResourceState(m_ActivityList);

            // A minimum EST and a maximum LFT fix an activity to a window. A duration that is longer
            // than the window cannot meet the maximum. A compile refuses such a plan before it starts
            // (P0030), and the critical-path calculation throws, and so the iteration fails here.
            foreach (int index in m_ConstrainedActivities)
            {
                TActivity activity = m_Activities[index];
                if (activity.MinimumEarliestStartTime.HasValue
                    && activity.MinimumEarliestStartTime.GetValueOrDefault() + activity.Duration > activity.MaximumLatestFinishTime.GetValueOrDefault())
                {
                    m_MissedMaximumLatestFinishTimes.Add(index);
                }
            }
            if (m_MissedMaximumLatestFinishTimes.Count != 0)
            {
                m_Failure = SimulationIterationFailure.InvalidConstraints;
                return Failed;
            }

            m_Builder.CalculateCriticalPath();

            if (!m_StopAfterTheFirstPass)
            {
                List<IResourceSchedule<T, TResourceId, TWorkStreamId>> schedules;
                try
                {
                    schedules = m_Builder.CalculateResourceSchedulesByPriorityList(m_ActiveResources, cancellationToken);
                }
                catch (ResourceSchedulingStallException)
                {
                    m_Failure = SimulationIterationFailure.ResourceSchedulingStall;
                    return Failed;
                }

                // Without resources the first calculation could not hold resource dependencies,
                // and so the compile makes synthetic resources for them to work.
                if (m_ReplaceWithSyntheticResources)
                {
                    schedules = m_Builder.ResourceSchedulingEngine.ReplaceWithSyntheticResources(schedules);
                }

                m_Builder.AssignResourceDependencies(schedules);
                m_Builder.CalculateCriticalPath();
            }

            // The compile refuses a schedule that runs past the horizon, because the allocation
            // streams that it builds next would be unbounded. An iteration fails in the same way.
            if (m_Builder.FinishTime > GraphLimits.MaximumTimeValue)
            {
                m_Failure = SimulationIterationFailure.MaximumTimeValueExceeded;
                return Failed;
            }

            if (!m_Builder.BackFillIsolatedNodes())
            {
                throw new InvalidOperationException(Properties.Resources.Message_CannotBackFillIsolatedNodes);
            }

            // A maximum LFT that the durations cannot meet pulls its activity back in the forward pass.
            // The latest LFT of the plan then comes too early, and the finish time of the iteration with
            // it. The schedule is not valid, and a compile with these durations reports C0010. The check
            // of the compile confirms the failure, so that an engine of the caller that pulls activities
            // back in another way also fails the iteration.
            if (m_ConstrainedActivities.Length != 0)
            {
                foreach (int index in m_ConstrainedActivities)
                {
                    if (IsPulledBack(m_Activities[index]))
                    {
                        m_MissedMaximumLatestFinishTimes.Add(index);
                    }
                }
                if (m_MissedMaximumLatestFinishTimes.Count != 0
                    || m_Builder.FindInvalidPostCompilationConstraints().Count != 0)
                {
                    m_Failure = SimulationIterationFailure.InvalidConstraints;
                    return Failed;
                }
            }
            return m_Builder.FinishTime;
        }

        // Whether the forward pass pulled an activity back to meet its maximum LFT. The activity then
        // starts before time 0, before its minimum EST, or before a predecessor hands on its finish:
        // the finish plus the minimum free slack of the predecessor (an activity cannot have both a
        // minimum free slack and a maximum LFT). The predecessors are those of the plan and those that
        // the resources imply.
        private bool IsPulledBack(TActivity activity)
        {
            int earliestPossibleStartTime = Math.Max(0, activity.MinimumEarliestStartTime.GetValueOrDefault());
            earliestPossibleStartTime = LatestHandOver(activity.Dependencies, earliestPossibleStartTime);
            earliestPossibleStartTime = LatestHandOver(activity.PlanningDependencies, earliestPossibleStartTime);
            earliestPossibleStartTime = LatestHandOver(activity.ResourceDependencies, earliestPossibleStartTime);
            return activity.EarliestStartTime.GetValueOrDefault() < earliestPossibleStartTime;
        }

        private int LatestHandOver(HashSet<T> predecessorIds, int time)
        {
            foreach (T predecessorId in predecessorIds)
            {
                TActivity predecessor = m_Builder.Activity(predecessorId);
                int handOver = predecessor.EarliestFinishTime.GetValueOrDefault() + predecessor.MinimumFreeSlack.GetValueOrDefault();
                if (handOver > time)
                {
                    time = handOver;
                }
            }
            return time;
        }
    }
}
