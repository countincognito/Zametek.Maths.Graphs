using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    // Schedules activities onto resources using a priority-list algorithm.
    // The engine is stateless — all graph state is supplied via callback delegates
    // and the priority list, keeping it decoupled from both Arrow and Vertex builders.
    internal interface IResourceSchedulingEngine<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        IEnumerable<IResourceSchedule<T, TResourceId, TWorkStreamId>> CalculateResourceSchedules(
            List<T> priorityList,
            List<IResource<TResourceId, TWorkStreamId>> filteredResources,
            bool infiniteResources,
            Func<T, IActivity<T, TResourceId, TWorkStreamId>> activityLookup,
            Func<T, List<T>> strongDependencyLookup,
            Func<List<IActivity<T, TResourceId, TWorkStreamId>>> finalActivitiesFactory);
    }
}
