using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// Schedules activities onto resources using a priority-list algorithm and
    /// owns the surrounding scheduling pipeline. The engine is stateless - all
    /// graph state is supplied via the
    /// <see cref="IResourceSchedulingGraph{T, TResourceId, TWorkStreamId}"/> view
    /// and the priority list, keeping it decoupled from both arrow and vertex
    /// builders.
    /// </summary>
    /// <typeparam name="T">The activity ID type.</typeparam>
    /// <typeparam name="TResourceId">The resource ID type.</typeparam>
    /// <typeparam name="TWorkStreamId">The work-stream ID type.</typeparam>
    public interface IResourceSchedulingEngine<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        /// <summary>
        /// Allocates the activities (in priority-list order, honouring their
        /// dependencies) onto the filtered resources and returns the resulting
        /// per-resource schedules. With <paramref name="infiniteResources"/> a new
        /// schedule is spawned whenever no existing resource is free.
        /// </summary>
        IEnumerable<IResourceSchedule<T, TResourceId, TWorkStreamId>> CalculateResourceSchedules(
            List<T> priorityList,
            List<IResource<TResourceId, TWorkStreamId>> filteredResources,
            bool infiniteResources,
            IResourceSchedulingGraph<T, TResourceId, TWorkStreamId> graph);

        /// <summary>
        /// Gathers the set of activities that reference resources not present in
        /// <paramref name="filteredResources"/>.
        /// </summary>
        IList<IUnavailableResources<T, TResourceId>> GatherUnavailableResources(
            List<IActivity<T, TResourceId, TWorkStreamId>> activities,
            List<IResource<TResourceId, TWorkStreamId>> filteredResources);

        /// <summary>
        /// Replaces infinite-resource schedules with synthetic resource IDs so
        /// that resource-dependency chaining works in the second compile pass.
        /// </summary>
        List<IResourceSchedule<T, TResourceId, TWorkStreamId>> ReplaceWithSyntheticResources(
            List<IResourceSchedule<T, TResourceId, TWorkStreamId>> resourceSchedules);

        /// <summary>
        /// Rebuilds resource schedules aligned to the CPM-computed earliest start
        /// times of the final activities.
        /// </summary>
        IEnumerable<IResourceSchedule<T, TResourceId, TWorkStreamId>> RebuildAlignedResourceSchedules(
            List<IResourceSchedule<T, TResourceId, TWorkStreamId>> resourceSchedules,
            bool infiniteResources,
            IResourceSchedulingGraph<T, TResourceId, TWorkStreamId> graph,
            List<IActivity<T, TResourceId, TWorkStreamId>> finalActivities,
            int startTime,
            int finishTime);

        /// <summary>
        /// Returns schedules for <see cref="InterActivityAllocationType.Indirect"/>
        /// resources that were not directly assigned any activities.
        /// </summary>
        IEnumerable<IResourceSchedule<T, TResourceId, TWorkStreamId>> CollectIndirectResourceSchedules(
            List<IResource<TResourceId, TWorkStreamId>> filteredResources,
            List<IResourceSchedule<T, TResourceId, TWorkStreamId>> scheduledResources,
            List<IActivity<T, TResourceId, TWorkStreamId>> finalActivities,
            int startTime,
            int finishTime);

        /// <summary>
        /// Returns the set of work-stream phase IDs that appear on at least one
        /// resource schedule.
        /// </summary>
        HashSet<TWorkStreamId> GetResourcePhasesUsed(
            List<IResourceSchedule<T, TResourceId, TWorkStreamId>> totalSchedules,
            HashSet<TWorkStreamId> workstreamsUsed);
    }
}
