using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;

namespace Zametek.Maths.Graphs
{
    // Stateless priority-list resource scheduler.
    // Receives the priority-ordered activity list and callbacks for graph state -
    // works identically for both Arrow and Vertex builders.
    // Also owns the scheduling pipeline helpers used before and after the core loop:
    //   GatherUnavailableResources, ReplaceWithSyntheticResources,
    //   RebuildAlignedResourceSchedules, CollectIndirectResourceSchedules, GetResourcePhasesUsed.
    /// <summary>
    /// Default resource scheduling engine: priority-list allocation plus the surrounding scheduling pipeline.
    /// </summary>
    public sealed class PriorityListResourceScheduler<T, TResourceId, TWorkStreamId>
        : IResourceSchedulingEngine<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        /// <inheritdoc/>
        public IEnumerable<IResourceSchedule<T, TResourceId, TWorkStreamId>> CalculateResourceSchedules(
            List<T> priorityList,
            List<IResource<TResourceId, TWorkStreamId>> filteredResources,
            bool infiniteResources,
            IResourceSchedulingGraph<T, TResourceId, TWorkStreamId> graph,
            CancellationToken cancellationToken)
        {
            if (priorityList is null)
            {
                throw new ArgumentNullException(nameof(priorityList));
            }
            if (filteredResources is null)
            {
                throw new ArgumentNullException(nameof(filteredResources));
            }
            if (graph is null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            List<T?> workingList = priorityList.Select(x => new T?(x)).ToList();

            // Each activity's strong (resolved) dependency set is invariant for the duration
            // of scheduling - the loop only mutates resource allocations, never the graph
            // structure - so resolve it once here rather than re-walking the graph (which
            // recurses through dummy chains) for every pending activity on every time tick.
            var strongDependencyLookup = new Dictionary<T, HashSet<T>>();
            foreach (T activityId in priorityList)
            {
                if (!strongDependencyLookup.ContainsKey(activityId))
                {
                    strongDependencyLookup.Add(activityId, new HashSet<T>(graph.StrongActivityDependencyIds(activityId)));
                }
            }

            List<ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>> resourceScheduleBuilders = filteredResources
                .OrderBy(x => x.AllocationOrder)
                .Select(x => new ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>(x))
                .ToList();

            var completed = new HashSet<T>();
            var started = new HashSet<T>();
            var ready = new List<T?>(Enumerable.Repeat((T?)null, workingList.Count));
            int timeCounter = 0;

            while (workingList.Any(x => x.HasValue) || started.Count != 0 || ready.Any(x => x.HasValue))
            {
                cancellationToken.ThrowIfCancellationRequested();
                AdvanceCompletedActivities(resourceScheduleBuilders, timeCounter, started, completed);
                PromoteReadyActivities(workingList, ready, completed, started, strongDependencyLookup);
                AssignReadyActivitiesToResources(ready, resourceScheduleBuilders, graph, filteredResources,
                    infiniteResources, started, timeCounter);
                timeCounter = NextTickOfInterest(workingList, ready, started, completed, resourceScheduleBuilders,
                    graph, infiniteResources, strongDependencyLookup, timeCounter);
            }

            List<IActivity<T, TResourceId, TWorkStreamId>> finalActivities = graph.CloneActivities();

            // FirstActivityStartTime rather than a minimum over a copy of each builder's
            // whole list: appends are in non-decreasing start-time order, so the first
            // entry already is the minimum, and this was the only place in the library
            // that copied a builder's schedule to read one number out of it.
            int startTime = resourceScheduleBuilders
                .Select(x => x.FirstActivityStartTime)
                .DefaultIfEmpty().Min();

            // The allocation streams run from time 0 to this finish time, and an indirect resource marks its phases in them
            // from the first to the last activity of each phase. An activity of zero duration is on no resource, but it can
            // be in a phase and finish after each activity that takes time. So the finish time covers each activity, and not
            // only the activities on the resources.
            int finishTime = Math.Max(
                resourceScheduleBuilders
                    .Select(x => x.LastActivityFinishTime)
                    .DefaultIfEmpty().Max(),
                finalActivities
                    .Select(x => x.EarliestFinishTime.GetValueOrDefault())
                    .DefaultIfEmpty().Max());

            return resourceScheduleBuilders
                .Select(x => x.ToResourceSchedule(finalActivities, startTime, finishTime))
                .Where(x => x.ScheduledActivities.Any())
                .ToList();
        }

        private static void AdvanceCompletedActivities(
            List<ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>> builders,
            int timeCounter,
            HashSet<T> started,
            HashSet<T> completed)
        {
            // Any started activities that are currently not running must now be completed.
            HashSet<T> running = new HashSet<T>(builders
                .Select(x => x.ActivityAt(timeCounter))
                .Where(x => x.HasValue)
                .Select(x => x.GetValueOrDefault()));

            // Now work out which of the started jobs are now completed.
            IList<T> notYetCompleted = started.Intersect(running).ToList();
            started.ExceptWith(notYetCompleted);
            completed.UnionWith(started);
            // Refresh the started set.
            started.Clear();
            started.UnionWith(notYetCompleted);
        }

        private static void PromoteReadyActivities(
            List<T?> workingList,
            List<T?> ready,
            HashSet<T> completed,
            HashSet<T> started,
            IReadOnlyDictionary<T, HashSet<T>> strongDependencyLookup)
        {
            // Get the activities that have completed direct dependencies.
            // Add these to the ready queue since there is nothing preventing them from starting.
            var indicesToRemove = new HashSet<int>();
            for (int i = 0; i < workingList.Count; i++)
            {
                if (!workingList[i].HasValue)
                {
                    continue;
                }
                T activityId = workingList[i].GetValueOrDefault();
                // Cached set; IsSubsetOf only reads it, so sharing across ticks is safe.
                HashSet<T> directDependencies = strongDependencyLookup[activityId];
                if (directDependencies.IsSubsetOf(completed)
                    && !completed.Contains(activityId)
                    && !started.Contains(activityId))
                {
                    ready[i] = activityId;
                    indicesToRemove.Add(i);
                }
            }
            // Now clear the activities that have been added to the ready queue.
            foreach (int idx in indicesToRemove)
            {
                workingList[idx] = null;
            }
        }

        private static void AssignReadyActivitiesToResources(
            List<T?> ready,
            List<ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>> builders,
            IResourceSchedulingGraph<T, TResourceId, TWorkStreamId> graph,
            List<IResource<TResourceId, TWorkStreamId>> filteredResources,
            bool infiniteResources,
            HashSet<T> started,
            int timeCounter)
        {
            // Cycle through each ready activity and find the first currently available schedule builder.
            bool keepLooking = true;
            while (ready.Any(x => x.HasValue) && keepLooking)
            {
                keepLooking = false;
                bool availableBuilderExists = false;

                for (int i = 0; i < ready.Count; i++)
                {
                    if (!ready[i].HasValue)
                    {
                        continue;
                    }
                    T activityId = ready[i].GetValueOrDefault();
                    IActivity<T, TResourceId, TWorkStreamId> activity = graph.Activity(activityId);
                    activity.AllocatedToResources.Clear();

                    // Check to see if the activity has to be targeted to specific resources,
                    // and that this resource is one of those specific targets.
                    bool mustTargetSpecific = !infiniteResources && activity.TargetResources.Count != 0;

                    if (!mustTargetSpecific)
                    {
                        bool scheduled = TryScheduleUnrestricted(
                            builders, activity, activityId, timeCounter, started, ref availableBuilderExists);
                        if (scheduled)
                        {
                            ready[i] = null;
                            keepLooking = true;
                        }
                    }
                    else
                    {
                        bool scheduled = TryScheduleTargeted(
                            builders, activity, activityId, filteredResources, timeCounter, started,
                            ref availableBuilderExists);
                        if (scheduled)
                        {
                            ready[i] = null;
                            keepLooking = true;
                        }
                    }
                }

                if (infiniteResources && !availableBuilderExists && !keepLooking)
                {
                    builders.Add(new ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>());
                    keepLooking = true;
                }
            }

            // With infinite resources each ready activity gets a builder, and so none waits for one.
            if (!infiniteResources)
            {
                GiveWayToWaitingActivities(ready, builders, graph, started, timeCounter);
            }
        }

        // Each ready activity has now taken the first free builder in the allocation order that it can use. A ready
        // activity can still wait while a builder is free: an activity that started at this tick took the builder that
        // it needs, and that activity can also use the free one. Then the activity that started moves to the free builder,
        // and the activity that waits starts on the builder that it leaves. Neither starts later than it did, and a
        // resource does not stay idle for an activity that could not use it.
        //
        // A move can need a chain of moves: A leaves R1 for R2, and B leaves R2 for the free R3. The search is breadth
        // first, and so it makes the fewest moves, and it tries the builders in the allocation order. Only an activity
        // that can use any one of several builders moves. An activity that needs all of its targets stays, but it can be
        // the one that waits: then each activity on one of its builders must move to a builder that it does not need.
        //
        // Nothing happens when no builder is free, or when no activity waits that could start now. Thus, the schedule is
        // the one of the first-free rule alone, except at a tick where that rule leaves a builder idle that a chain of
        // moves can put to use.
        private static void GiveWayToWaitingActivities(
            List<T?> ready,
            List<ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>> builders,
            IResourceSchedulingGraph<T, TResourceId, TWorkStreamId> graph,
            HashSet<T> started,
            int timeCounter)
        {
            int builderCount = builders.Count;
            bool anyFree = false;
            for (int index = 0; index < builderCount && !anyFree; index++)
            {
                anyFree = builders[index].EarliestAvailableStartTimeForNextActivity <= timeCounter;
            }
            if (!anyFree)
            {
                return;
            }

            // The activities that wait and whose time gates are open. An activity of zero duration takes no time on a
            // builder, and the first-free rule alone decides where it goes.
            var waiting = new List<int>();
            for (int i = 0; i < ready.Count; i++)
            {
                if (!ready[i].HasValue)
                {
                    continue;
                }
                IActivity<T, TResourceId, TWorkStreamId> activity = graph.Activity(ready[i].GetValueOrDefault());
                if (!activity.IsDummy && CanStartAt(activity, timeCounter))
                {
                    waiting.Add(i);
                }
            }
            if (waiting.Count == 0)
            {
                return;
            }

            var seats = new TickSeats(builders, graph, timeCounter);
            var newlySeated = new List<(int ReadyIndex, List<int> Seats)>();

            foreach (int readyIndex in waiting)
            {
                T activityId = ready[readyIndex].GetValueOrDefault();
                IActivity<T, TResourceId, TWorkStreamId> activity = graph.Activity(activityId);

                List<int>? taken = CanUseAnyOneOfItsBuilders(activity)
                    ? seats.TrySeatOnOne(activityId)
                    : seats.TrySeatOnAll(activityId, activity);

                if (taken != null)
                {
                    newlySeated.Add((readyIndex, taken));
                }
            }

            if (newlySeated.Count == 0)
            {
                return;
            }

            // First take each activity that moves off its builder. It is the last one there, because it started at this
            // tick and it takes time. Then put each on its new builder, and start the activities that waited.
            List<T> movers = seats.Movers();
            foreach (T mover in movers)
            {
                builders[seats.OriginalSeatOf(mover)].RemoveLastActivity();
            }
            foreach (T mover in movers)
            {
                builders[seats.SeatOf(mover)].AppendActivity(graph.Activity(mover), timeCounter);
            }
            foreach ((int readyIndex, List<int> taken) in newlySeated)
            {
                T activityId = ready[readyIndex].GetValueOrDefault();
                IActivity<T, TResourceId, TWorkStreamId> activity = graph.Activity(activityId);

                // A later move can have moved an activity that waited to another builder.
                List<int> finalSeats = CanUseAnyOneOfItsBuilders(activity)
                    ? new List<int> { seats.SeatOf(activityId) }
                    : taken;
                foreach (int seat in finalSeats)
                {
                    builders[seat].AppendActivity(activity, timeCounter);
                }
                started.Add(activityId);
                ready[readyIndex] = null;
            }
        }

        // Whether the time gates of the activity are open at this tick: its earliest start time is reached, a maximum
        // latest finish time does not hold it back to a later start, and it finishes within the supported time horizon.
        private static bool CanStartAt(IActivity<T, TResourceId, TWorkStreamId> activity, int timeCounter)
        {
            if (activity.EarliestStartTime.GetValueOrDefault() > timeCounter)
            {
                return false;
            }
            if (activity.MaximumLatestFinishTime.HasValue
                && activity.MaximumLatestFinishTime.GetValueOrDefault() > ((long)timeCounter + activity.Duration))
            {
                return false;
            }
            // Starting here would push the finish time past the supported time
            // horizon; the activity can never start, which the stall detector reports.
            if ((long)timeCounter + activity.Duration > GraphLimits.MaximumTimeValue)
            {
                return false;
            }
            return true;
        }

        // An activity with no targets can use any one builder that is not an explicit target, and an activity with the
        // OR operator can use any one of its targets. An activity with AND or ACTIVE_AND needs all of its targets at once.
        private static bool CanUseAnyOneOfItsBuilders(IActivity<T, TResourceId, TWorkStreamId> activity)
        {
            return activity.TargetResources.Count == 0
                || activity.TargetResourceOperator == LogicalOperator.OR;
        }

        // The builders of one tick, for GiveWayToWaitingActivities: which are free, and which hold an activity that
        // started at this tick and can move. The moves are made here first, and on the builders themselves only when the
        // search is over.
        private sealed class TickSeats
        {
            private readonly List<ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>> m_Builders;
            private readonly IResourceSchedulingGraph<T, TResourceId, TWorkStreamId> m_Graph;
            private readonly bool[] m_IsFree;
            private readonly T?[] m_HolderOf;
            private readonly Dictionary<T, int> m_SeatOf = new Dictionary<T, int>();
            private readonly Dictionary<T, int> m_OriginalSeatOf = new Dictionary<T, int>();
            private readonly Dictionary<T, List<int>> m_UsableBuilders = new Dictionary<T, List<int>>();

            // The builders that a search must not use. A search that fails leaves its marks: the builders that it reached
            // lead to no free builder, also for the next activity that waits. A search that succeeds clears them, because
            // its moves change what can be reached.
            private readonly bool[] m_Closed;
            private readonly int[] m_Previous;
            private readonly Queue<int> m_Queue = new Queue<int>();

            internal TickSeats(
                List<ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>> builders,
                IResourceSchedulingGraph<T, TResourceId, TWorkStreamId> graph,
                int timeCounter)
            {
                m_Builders = builders;
                m_Graph = graph;
                m_IsFree = new bool[builders.Count];
                m_HolderOf = new T?[builders.Count];
                m_Closed = new bool[builders.Count];
                m_Previous = new int[builders.Count];

                for (int index = 0; index < builders.Count; index++)
                {
                    m_IsFree[index] = builders[index].EarliestAvailableStartTimeForNextActivity <= timeCounter;
                    if (m_IsFree[index])
                    {
                        continue;
                    }
                    // A builder that is not free holds an activity that takes time. It started at this tick when its
                    // start time is this tick, and then it can move when it can use any one of several builders.
                    IScheduledActivity<T>? last = builders[index].LastScheduledActivity;
                    if (last != null
                        && last.StartTime == timeCounter
                        && CanUseAnyOneOfItsBuilders(graph.Activity(last.Id)))
                    {
                        m_HolderOf[index] = last.Id;
                        m_SeatOf.Add(last.Id, index);
                        m_OriginalSeatOf.Add(last.Id, index);
                    }
                }
            }

            internal int SeatOf(T activityId) => m_SeatOf[activityId];

            internal int OriginalSeatOf(T activityId) => m_OriginalSeatOf[activityId];

            // The activities that started at this tick and are now on another builder.
            internal List<T> Movers()
            {
                var movers = new List<T>();
                foreach (KeyValuePair<T, int> original in m_OriginalSeatOf)
                {
                    if (m_SeatOf[original.Key] != original.Value)
                    {
                        movers.Add(original.Key);
                    }
                }
                return movers;
            }

            // Seats an activity that can use any one of its builders. Returns its builder, or null.
            internal List<int>? TrySeatOnOne(T activityId)
            {
                int seat = FreeOneFor(activityId);
                if (seat < 0)
                {
                    return null;
                }
                Occupy(seat, activityId);
                Array.Clear(m_Closed, 0, m_Closed.Length);
                return new List<int> { seat };
            }

            // Seats an activity that needs all of its builders: each activity on one of them moves to a builder that is not
            // one of them. Returns the builders, or null when one of them cannot be made free. Then nothing has moved.
            internal List<int>? TrySeatOnAll(T activityId, IActivity<T, TResourceId, TWorkStreamId> activity)
            {
                List<int> needed = UsableBuilders(activityId);

                // With AND, each target must be a resource of the plan. With ACTIVE_AND, the targets that are resources of
                // the plan are enough, and there must be one.
                if (needed.Count == 0
                    || (activity.TargetResourceOperator == LogicalOperator.AND && needed.Count != activity.TargetResources.Count))
                {
                    return null;
                }

                bool[] isFreeBefore = (bool[])m_IsFree.Clone();
                T?[] holderOfBefore = (T?[])m_HolderOf.Clone();
                var seatOfBefore = new Dictionary<T, int>(m_SeatOf);
                bool[] closedBefore = (bool[])m_Closed.Clone();

                bool allFree = true;
                foreach (int index in needed)
                {
                    if (m_IsFree[index])
                    {
                        continue;
                    }
                    T? holder = m_HolderOf[index];
                    if (!holder.HasValue)
                    {
                        allFree = false;
                        break;
                    }
                    // The holder must not move to a builder that the activity needs.
                    Array.Clear(m_Closed, 0, m_Closed.Length);
                    foreach (int reserved in needed)
                    {
                        m_Closed[reserved] = true;
                    }
                    int seat = FreeOneFor(holder.GetValueOrDefault());
                    if (seat < 0)
                    {
                        allFree = false;
                        break;
                    }
                    Occupy(seat, holder.GetValueOrDefault());
                    m_HolderOf[index] = null;
                    m_IsFree[index] = true;
                }

                if (!allFree)
                {
                    Array.Copy(isFreeBefore, m_IsFree, m_IsFree.Length);
                    Array.Copy(holderOfBefore, m_HolderOf, m_HolderOf.Length);
                    m_SeatOf.Clear();
                    foreach (KeyValuePair<T, int> seat in seatOfBefore)
                    {
                        m_SeatOf.Add(seat.Key, seat.Value);
                    }
                    Array.Copy(closedBefore, m_Closed, m_Closed.Length);
                    return null;
                }

                // An activity that needs all of its builders does not move, and so it is no holder.
                foreach (int index in needed)
                {
                    m_IsFree[index] = false;
                    m_HolderOf[index] = null;
                }
                Array.Clear(m_Closed, 0, m_Closed.Length);
                return needed;
            }

            private void Occupy(int index, T activityId)
            {
                m_IsFree[index] = false;
                m_HolderOf[index] = activityId;
                m_SeatOf[activityId] = index;
            }

            // The builders that the activity can use, in the allocation order: its targets, or each builder that is not an
            // explicit target when it has no targets.
            private List<int> UsableBuilders(T activityId)
            {
                if (!m_UsableBuilders.TryGetValue(activityId, out List<int>? usable))
                {
                    IActivity<T, TResourceId, TWorkStreamId> activity = m_Graph.Activity(activityId);
                    bool targeted = activity.TargetResources.Count != 0;
                    usable = new List<int>();
                    for (int index = 0; index < m_Builders.Count; index++)
                    {
                        ResourceScheduleBuilder<T, TResourceId, TWorkStreamId> builder = m_Builders[index];
                        if (targeted
                            ? builder.ResourceId != null && activity.TargetResources.Contains(builder.ResourceId.GetValueOrDefault())
                            : !builder.IsExplicitTarget)
                        {
                            usable.Add(index);
                        }
                    }
                    m_UsableBuilders.Add(activityId, usable);
                }
                return usable;
            }

            // Makes one builder free that the activity can use, and returns it, or -1. The search goes from the builders of
            // the activity, through the builders that the holders of those can use, to a builder that is free. Then each
            // holder on the way moves one step along it, and the first builder of the way is free.
            private int FreeOneFor(T activityId)
            {
                m_Queue.Clear();
                foreach (int index in UsableBuilders(activityId))
                {
                    if (!m_Closed[index])
                    {
                        m_Closed[index] = true;
                        m_Previous[index] = -1;
                        m_Queue.Enqueue(index);
                    }
                }

                while (m_Queue.Count != 0)
                {
                    int index = m_Queue.Dequeue();
                    if (m_IsFree[index])
                    {
                        int current = index;
                        while (m_Previous[current] != -1)
                        {
                            int previous = m_Previous[current];
                            Occupy(current, m_HolderOf[previous].GetValueOrDefault());
                            current = previous;
                        }
                        m_HolderOf[current] = null;
                        m_IsFree[current] = true;
                        return current;
                    }

                    T? holder = m_HolderOf[index];
                    if (!holder.HasValue)
                    {
                        continue;
                    }
                    foreach (int next in UsableBuilders(holder.GetValueOrDefault()))
                    {
                        if (!m_Closed[next])
                        {
                            m_Closed[next] = true;
                            m_Previous[next] = index;
                            m_Queue.Enqueue(next);
                        }
                    }
                }
                return -1;
            }
        }

        private static bool TryScheduleUnrestricted(
            List<ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>> builders,
            IActivity<T, TResourceId, TWorkStreamId> activity,
            T activityId,
            int timeCounter,
            HashSet<T> started,
            ref bool availableBuilderExists)
        {
            foreach (ResourceScheduleBuilder<T, TResourceId, TWorkStreamId> builder in builders)
            {
                if (builder.EarliestAvailableStartTimeForNextActivity > timeCounter)
                {
                    continue;
                }
                availableBuilderExists = true;
                if (builder.IsExplicitTarget)
                {
                    continue;
                }
                if (!CanStartAt(activity, timeCounter))
                {
                    continue;
                }
                builder.AppendActivity(activity, timeCounter);
                started.Add(activityId);
                return true;
            }
            return false;
        }

        private static bool TryScheduleTargeted(
            List<ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>> builders,
            IActivity<T, TResourceId, TWorkStreamId> activity,
            T activityId,
            List<IResource<TResourceId, TWorkStreamId>> filteredResources,
            int timeCounter,
            HashSet<T> started,
            ref bool availableBuilderExists)
        {
            var available = new HashSet<ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>>();

            foreach (ResourceScheduleBuilder<T, TResourceId, TWorkStreamId> builder in builders)
            {
                if (builder.EarliestAvailableStartTimeForNextActivity > timeCounter)
                {
                    continue;
                }
                availableBuilderExists = true;

                if (builder.ResourceId != null
                    && !activity.TargetResources.Contains(builder.ResourceId.GetValueOrDefault()))
                {
                    continue;
                }
                if (!CanStartAt(activity, timeCounter))
                {
                    continue;
                }

                // Find just one resource that can accommodate the activity.
                if (activity.TargetResourceOperator == LogicalOperator.OR)
                {
                    builder.AppendActivity(activity, timeCounter);
                    started.Add(activityId);
                    return true;
                }
                // Find all the resources that must accommodate the activity.
                else if (activity.TargetResourceOperator == LogicalOperator.AND)
                {
                    available.Add(builder);
                    var targetSet = new HashSet<TResourceId>(activity.TargetResources);
                    if (targetSet.SetEquals(available.Select(x => x.ResourceId.GetValueOrDefault())))
                    {
                        foreach (var r in available) { r.AppendActivity(activity, timeCounter); started.Add(activityId); }
                        return true;
                    }
                }
                // Find all the active resources that must accommodate the activity.
                else if (activity.TargetResourceOperator == LogicalOperator.ACTIVE_AND)
                {
                    available.Add(builder);
                    // Check intersection of TargetResources and filtered Resources.
                    var intersection = new HashSet<TResourceId>(
                        activity.TargetResources.Intersect(filteredResources.Select(x => x.Id)));
                    if (intersection.SetEquals(available.Select(x => x.ResourceId.GetValueOrDefault())))
                    {
                        foreach (var r in available) { r.AppendActivity(activity, timeCounter); started.Add(activityId); }
                        return true;
                    }
                }
                else
                {
                    throw new NotImplementedException($@"Unknown TargetResourceOperator value ({activity.TargetResourceOperator})");
                }
            }
            return false;
        }

        // Decides which tick the scheduling loop should process next. Most of the time
        // that is simply the next tick, but when every remaining activity is waiting on
        // a time gate the loop can jump straight to the earliest gate opening - every
        // skipped tick is provably a no-op (no completion, promotion or assignment can
        // occur before the next running activity finishes or the next gate opens).
        // When there is nothing running, nothing pending completion and no future time
        // gate while activities still remain, no amount of further ticking can change
        // anything - so instead of looping forever the scheduler stops and reports
        // which activities are stuck and why.
        private static int NextTickOfInterest(
            List<T?> workingList,
            List<T?> ready,
            HashSet<T> started,
            HashSet<T> completed,
            List<ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>> builders,
            IResourceSchedulingGraph<T, TResourceId, TWorkStreamId> graph,
            bool infiniteResources,
            IReadOnlyDictionary<T, HashSet<T>> strongDependencyLookup,
            int timeCounter)
        {
            // Times are compared in long so that pathological input values cannot
            // overflow int arithmetic inside the loop.
            long nextEventTime = long.MaxValue;

            if (started.Count != 0)
            {
                // Find the activities currently occupying resources, and when the
                // earliest of them will finish. A builder is busy when its last
                // recorded activity finishes after the current tick (activities are
                // only ever appended at the current tick, so nothing later exists).
                var running = new HashSet<T>();
                foreach (ResourceScheduleBuilder<T, TResourceId, TWorkStreamId> builder in builders)
                {
                    int lastFinishTime = builder.LastActivityFinishTime;
                    if (lastFinishTime > timeCounter)
                    {
                        T? runningActivityId = builder.ActivityAt(timeCounter);
                        if (runningActivityId.HasValue)
                        {
                            running.Add(runningActivityId.GetValueOrDefault());
                        }
                        if (lastFinishTime < nextEventTime)
                        {
                            nextEventTime = lastFinishTime;
                        }
                    }
                }

                // A started activity that is no longer running (e.g. one with zero
                // duration) completes on the very next tick, which may unblock its
                // successors - so no jump is possible.
                foreach (T startedId in started)
                {
                    if (!running.Contains(startedId))
                    {
                        return timeCounter + 1;
                    }
                }
            }

            // Ready activities can also be unblocked purely by the passage of time: an
            // EarliestStartTime that has not been reached yet, or a deadline whose
            // just-in-time start point is still ahead.
            for (int i = 0; i < ready.Count; i++)
            {
                if (!ready[i].HasValue)
                {
                    continue;
                }
                IActivity<T, TResourceId, TWorkStreamId> activity = graph.Activity(ready[i].GetValueOrDefault());
                long earliestStartTime = activity.EarliestStartTime.GetValueOrDefault();
                if (earliestStartTime > timeCounter && earliestStartTime < nextEventTime)
                {
                    nextEventTime = earliestStartTime;
                }
                if (activity.MaximumLatestFinishTime.HasValue)
                {
                    long maximumLatestFinishTime = activity.MaximumLatestFinishTime.GetValueOrDefault();
                    // The just-in-time gate holds the activity back while starting now
                    // would still finish ahead of the deadline; it opens at the tick
                    // where the finish would land exactly on the deadline.
                    long gateOpensAt = maximumLatestFinishTime - activity.Duration;
                    if (maximumLatestFinishTime > (long)timeCounter + activity.Duration
                        && gateOpensAt < nextEventTime)
                    {
                        nextEventTime = gateOpensAt;
                    }
                }
            }

            if (nextEventTime != long.MaxValue)
            {
                // Jumping beyond the supported horizon means nothing schedulable remains
                // within it, so the state is dead however far the clock is wound forward.
                if (nextEventTime > GraphLimits.MaximumTimeValue)
                {
                    throw new ResourceSchedulingStallException(
                        BuildStallMessage(workingList, ready, builders, graph, infiniteResources, strongDependencyLookup, completed, timeCounter));
                }
                // Guaranteed to be at least timeCounter + 1, because every candidate
                // above is strictly greater than the current tick.
                return (int)nextEventTime;
            }

            // No running activity, no pending completion, and no future time gate: if
            // any activities remain then they can never be scheduled.
            if (ready.Any(x => x.HasValue) || workingList.Any(x => x.HasValue) || started.Count != 0)
            {
                throw new ResourceSchedulingStallException(
                    BuildStallMessage(workingList, ready, builders, graph, infiniteResources, strongDependencyLookup, completed, timeCounter));
            }

            // Everything has drained; the loop condition is about to terminate the loop.
            return timeCounter + 1;
        }

        // Builds the diagnostic message for a scheduling stall: one line per stuck
        // activity explaining, as precisely as possible, why it can never be scheduled.
        private static string BuildStallMessage(
            List<T?> workingList,
            List<T?> ready,
            List<ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>> builders,
            IResourceSchedulingGraph<T, TResourceId, TWorkStreamId> graph,
            bool infiniteResources,
            IReadOnlyDictionary<T, HashSet<T>> strongDependencyLookup,
            HashSet<T> completed,
            int timeCounter)
        {
            // Collect the builder resource IDs into a set of our own so the diagnosis
            // below relies only on value comparisons, never on the (possibly corrupted)
            // lookup behaviour of the input collections.
            var availableResourceIds = new HashSet<TResourceId>();
            bool allBuildersExplicitTarget = builders.Count != 0;
            foreach (ResourceScheduleBuilder<T, TResourceId, TWorkStreamId> builder in builders)
            {
                if (builder.ResourceId != null)
                {
                    availableResourceIds.Add(builder.ResourceId.GetValueOrDefault());
                }
                if (!builder.IsExplicitTarget)
                {
                    allBuildersExplicitTarget = false;
                }
            }

            var output = new StringBuilder();
            output.AppendLine($@"{Properties.Resources.Message_ResourceSchedulingStalled}");
            for (int i = 0; i < ready.Count; i++)
            {
                if (!ready[i].HasValue)
                {
                    continue;
                }
                T activityId = ready[i].GetValueOrDefault();
                IActivity<T, TResourceId, TWorkStreamId> activity = graph.Activity(activityId);
                output.AppendLine($@"{activityId} -> {DescribeUnschedulableActivity(activity, availableResourceIds, allBuildersExplicitTarget, infiniteResources, timeCounter)}");
            }
            for (int i = 0; i < workingList.Count; i++)
            {
                if (!workingList[i].HasValue)
                {
                    continue;
                }
                T activityId = workingList[i].GetValueOrDefault();
                IEnumerable<T> outstanding = strongDependencyLookup[activityId]
                    .Where(x => !completed.Contains(x))
                    .OrderBy(x => x);
                output.AppendLine($@"{activityId} -> {Properties.Resources.Message_WaitingOnDependenciesThatCanNeverComplete} {string.Join(@", ", outstanding)}");
            }
            return output.ToString();
        }

        // Explains why a ready activity could not be assigned to any resource. The
        // checks below deliberately enumerate the activity's target resource set and
        // compare values rather than calling Contains on it, so that a structurally
        // corrupted set is diagnosed instead of skewing the diagnosis.
        private static string DescribeUnschedulableActivity(
            IActivity<T, TResourceId, TWorkStreamId> activity,
            HashSet<TResourceId> availableResourceIds,
            bool allBuildersExplicitTarget,
            bool infiniteResources,
            int timeCounter)
        {
            List<TResourceId> targetResources = activity.TargetResources.ToList();
            bool mustTargetSpecific = !infiniteResources && targetResources.Count != 0;

            if (mustTargetSpecific)
            {
                List<TResourceId> missing = targetResources
                    .Where(x => !availableResourceIds.Contains(x))
                    .OrderBy(x => x)
                    .ToList();
                if (activity.TargetResourceOperator == LogicalOperator.AND
                    && missing.Count != 0)
                {
                    return $@"{Properties.Resources.Message_RequiresAllTargetResourcesButSomeNotAvailable} {string.Join(@", ", missing)}";
                }
                if ((activity.TargetResourceOperator == LogicalOperator.OR
                        || activity.TargetResourceOperator == LogicalOperator.ACTIVE_AND)
                    && missing.Count == targetResources.Count)
                {
                    return $@"{Properties.Resources.Message_NoneOfTargetResourcesAreAvailable} {string.Join(@", ", targetResources.OrderBy(x => x))}";
                }
                // The target resources are all present by value, yet assignment still
                // failed - probe whether the set's own lookups agree with its contents.
                if (!targetResources.All(x => activity.TargetResources.Contains(x)))
                {
                    return Properties.Resources.Message_TargetResourceSetInternallyInconsistent;
                }
            }
            else if (allBuildersExplicitTarget)
            {
                return Properties.Resources.Message_NoTargetResourcesButAllResourcesAreExplicitTargets;
            }

            // Either the activity cannot start until after the horizon, or starting it at
            // the earliest opportunity would still finish beyond it - in both cases no
            // amount of waiting can make it schedulable.
            long earliestStartTime = activity.EarliestStartTime.GetValueOrDefault();
            if (earliestStartTime > GraphLimits.MaximumTimeValue
                || earliestStartTime + activity.Duration > GraphLimits.MaximumTimeValue
                || (long)timeCounter + activity.Duration > GraphLimits.MaximumTimeValue)
            {
                return string.Format(
                    CultureInfo.CurrentCulture,
                    Properties.Resources.Message_CannotBeScheduledWithinMaximumTimeValue,
                    GraphLimits.MaximumTimeValue);
            }

            return Properties.Resources.Message_CouldNotBeAssignedToAnyResource;
        }

        #region Scheduling Pipeline Helpers

        // Gathers the set of activities that reference resources not present in filteredResources.
        /// <inheritdoc/>
        public IList<IUnavailableResources<T, TResourceId>> GatherUnavailableResources(
            List<IActivity<T, TResourceId, TWorkStreamId>> activities,
            List<IResource<TResourceId, TWorkStreamId>> filteredResources)
        {
            var output = new List<IUnavailableResources<T, TResourceId>>();
            foreach (IActivity<T, TResourceId, TWorkStreamId> activity in activities)
            {
                if (activity.TargetResources.Count == 0)
                {
                    continue;
                }
                // When all explicit target resources must be available.
                if (activity.TargetResourceOperator == LogicalOperator.AND)
                {
                    // Ids in TargetResources that are not in filtered Resources.
                    IEnumerable<TResourceId> unavailable = activity.TargetResources.Except(filteredResources.Select(x => x.Id));
                    if (unavailable.Any())
                    {
                        output.Add(new UnavailableResources<T, TResourceId>(activity.Id, unavailable));
                    }
                }
                // When at least one explicit target resource must be available.
                else if (activity.TargetResourceOperator == LogicalOperator.OR
                         || activity.TargetResourceOperator == LogicalOperator.ACTIVE_AND)
                {
                    // Check intersection of TargetResources and filtered Resources.
                    IEnumerable<TResourceId> intersection = activity.TargetResources.Intersect(filteredResources.Select(x => x.Id));
                    if (!intersection.Any())
                    {
                        output.Add(new UnavailableResources<T, TResourceId>(activity.Id, activity.TargetResources));
                    }
                }
            }
            return output;
        }

        // Replaces infinite-resource schedules with synthetic resource IDs so that resource-dependency
        // chaining works in the second compile pass.
        /// <inheritdoc/>
        public List<IResourceSchedule<T, TResourceId, TWorkStreamId>> ReplaceWithSyntheticResources(
            List<IResourceSchedule<T, TResourceId, TWorkStreamId>> resourceSchedules)
        {
            // Remember to wipe the resources if we assume infinite resources.
            TResourceId resourceId = default;
            var replacements = new List<IResourceSchedule<T, TResourceId, TWorkStreamId>>();
            foreach (IResourceSchedule<T, TResourceId, TWorkStreamId> schedule in resourceSchedules)
            {
                resourceId = resourceId.Next();
                replacements.Add(new ResourceSchedule<T, TResourceId, TWorkStreamId>(
                    new Resource<TResourceId, TWorkStreamId>(
                        resourceId, null, false, false, InterActivityAllocationType.None, 0.0, 0.0, 0,
                        Enumerable.Empty<TWorkStreamId>()),
                    schedule.ScheduledActivities,
                    schedule.StartTime,
                    schedule.FinishTime,
                    schedule.ResourceAllocation,
                    schedule.CostAllocation,
                    schedule.BillingAllocation,
                    schedule.EffortAllocation,
                    schedule.ActivityAllocation));
            }
            return replacements;
        }

        // Rebuilds resource schedules aligned to CPM-computed EarliestStartTime values.
        // The graph view resolves each scheduled activity by ID.
        /// <inheritdoc/>
        public IEnumerable<IResourceSchedule<T, TResourceId, TWorkStreamId>> RebuildAlignedResourceSchedules(
            List<IResourceSchedule<T, TResourceId, TWorkStreamId>> resourceSchedules,
            bool infiniteResources,
            IResourceSchedulingGraph<T, TResourceId, TWorkStreamId> graph,
            List<IActivity<T, TResourceId, TWorkStreamId>> finalActivities,
            int startTime,
            int finishTime)
        {
            var builders = new List<ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>>();
            foreach (IResourceSchedule<T, TResourceId, TWorkStreamId> oldSchedule in resourceSchedules)
            {
                ResourceScheduleBuilder<T, TResourceId, TWorkStreamId> builder =
                    oldSchedule.Resource == null || infiniteResources
                    ? new ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>()
                    : new ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>(oldSchedule.Resource);

                foreach (IScheduledActivity<T> scheduledActivity in oldSchedule.ScheduledActivities)
                {
                    IActivity<T, TResourceId, TWorkStreamId> activityObj = graph.Activity(scheduledActivity.Id);
                    // This add needs to be without checks because the alignment may not be perfect.
                    builder.AppendActivityWithoutChecks(activityObj, activityObj.EarliestStartTime.GetValueOrDefault());
                }
                builders.Add(builder);
            }
            return builders
                .Select(x => x.ToResourceSchedule(finalActivities, startTime, finishTime))
                .Where(x => x.ScheduledActivities.Any())
                .ToList();
        }

        // Returns schedules for Indirect resources that were not directly assigned any activities.
        /// <inheritdoc/>
        public IEnumerable<IResourceSchedule<T, TResourceId, TWorkStreamId>> CollectIndirectResourceSchedules(
            List<IResource<TResourceId, TWorkStreamId>> filteredResources,
            List<IResourceSchedule<T, TResourceId, TWorkStreamId>> scheduledResources,
            List<IActivity<T, TResourceId, TWorkStreamId>> finalActivities,
            int startTime,
            int finishTime)
        {
            HashSet<TResourceId> scheduledIds = scheduledResources
                .Where(x => x.Resource != null).Select(x => x.Resource!.Id).ToHashSet();
            return filteredResources
                .Where(x => x.InterActivityAllocationType == InterActivityAllocationType.Indirect
                            && !scheduledIds.Contains(x.Id))
                .Select(x => new ResourceScheduleBuilder<T, TResourceId, TWorkStreamId>(x)
                    .ToResourceSchedule(finalActivities, startTime, finishTime))
                .ToList();
        }

        // Returns the set of work-stream phase IDs that appear on at least one resource schedule.
        /// <inheritdoc/>
        public HashSet<TWorkStreamId> GetResourcePhasesUsed(
            List<IResourceSchedule<T, TResourceId, TWorkStreamId>> totalSchedules,
            HashSet<TWorkStreamId> workstreamsUsed)
        {
            HashSet<TWorkStreamId> resourcePhases = totalSchedules
                .Where(x => x.Resource != null).SelectMany(x => x.Resource!.InterActivityPhases).Distinct().ToHashSet();
            return resourcePhases.Intersect(workstreamsUsed).ToHashSet();
        }

        #endregion
    }
}
