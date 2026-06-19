using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    // Schedules activities onto resources using a priority-list algorithm and owns the
    // surrounding scheduling pipeline. The engine is stateless - all graph state is
    // supplied via the IResourceSchedulingGraph view and the priority list, keeping it
    // decoupled from both Arrow and Vertex builders.
    public interface IResourceSchedulingEngine<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        IEnumerable<IResourceSchedule<T, TResourceId, TWorkStreamId>> CalculateResourceSchedules(
            List<T> priorityList,
            List<IResource<TResourceId, TWorkStreamId>> filteredResources,
            bool infiniteResources,
            IResourceSchedulingGraph<T, TResourceId, TWorkStreamId> graph);

        // Gathers the set of activities that reference resources not present in filteredResources.
        IList<IUnavailableResources<T, TResourceId>> GatherUnavailableResources(
            List<IActivity<T, TResourceId, TWorkStreamId>> activities,
            List<IResource<TResourceId, TWorkStreamId>> filteredResources);

        // Replaces infinite-resource schedules with synthetic resource IDs so that resource-dependency
        // chaining works in the second compile pass.
        List<IResourceSchedule<T, TResourceId, TWorkStreamId>> ReplaceWithSyntheticResources(
            List<IResourceSchedule<T, TResourceId, TWorkStreamId>> resourceSchedules);

        // Rebuilds resource schedules aligned to CPM-computed EarliestStartTime values.
        IEnumerable<IResourceSchedule<T, TResourceId, TWorkStreamId>> RebuildAlignedResourceSchedules(
            List<IResourceSchedule<T, TResourceId, TWorkStreamId>> resourceSchedules,
            bool infiniteResources,
            IResourceSchedulingGraph<T, TResourceId, TWorkStreamId> graph,
            List<IActivity<T, TResourceId, TWorkStreamId>> finalActivities,
            int startTime,
            int finishTime);

        // Returns schedules for Indirect resources that were not directly assigned any activities.
        IEnumerable<IResourceSchedule<T, TResourceId, TWorkStreamId>> CollectIndirectResourceSchedules(
            List<IResource<TResourceId, TWorkStreamId>> filteredResources,
            List<IResourceSchedule<T, TResourceId, TWorkStreamId>> scheduledResources,
            List<IActivity<T, TResourceId, TWorkStreamId>> finalActivities,
            int startTime,
            int finishTime);

        // Returns the set of work-stream phase IDs that appear on at least one resource schedule.
        HashSet<TWorkStreamId> GetResourcePhasesUsed(
            List<IResourceSchedule<T, TResourceId, TWorkStreamId>> totalSchedules,
            HashSet<TWorkStreamId> workstreamsUsed);
    }
}
