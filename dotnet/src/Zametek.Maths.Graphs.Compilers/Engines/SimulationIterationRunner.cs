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
        }

        // The activities, in the order of the plan.
        internal IReadOnlyList<TActivity> Activities => m_Activities;

        // Gives each activity its duration (in the order of the plan), schedules the plan, and
        // returns the finish time, or Failed when the resource scheduler could not place every
        // activity or when the schedule runs past the limit. After a call that did not fail, each
        // activity holds its times and its critical flag, and the resource dependencies that the
        // schedule implied.
        internal int Run(int[] durations, CancellationToken cancellationToken)
        {
            for (int i = 0; i < m_Activities.Length; i++)
            {
                m_Activities[i].Duration = durations[i];
            }
            m_Builder.ResetResourceState(m_ActivityList);

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
                return Failed;
            }

            if (!m_Builder.BackFillIsolatedNodes())
            {
                throw new InvalidOperationException(Properties.Resources.Message_CannotBackFillIsolatedNodes);
            }
            return m_Builder.FinishTime;
        }
    }
}
