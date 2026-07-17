using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// Read-only projection of a graph builder that the resource scheduling
    /// engine operates on. Implemented by both arrow and vertex builders (and
    /// their clones).
    /// </summary>
    /// <typeparam name="T">The activity ID type.</typeparam>
    /// <typeparam name="TResourceId">The resource ID type.</typeparam>
    /// <typeparam name="TWorkStreamId">The work-stream ID type.</typeparam>
    public interface IResourceSchedulingGraph<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        /// <summary>
        /// Resolves the live activity for the given ID.
        /// </summary>
        IActivity<T, TResourceId, TWorkStreamId> Activity(T id);

        /// <summary>
        /// Returns the strong (resolved) dependency IDs for the given activity ID.
        /// </summary>
        List<T> StrongActivityDependencyIds(T id);

        /// <summary>
        /// Returns a cloned snapshot of all activities in the graph.
        /// </summary>
        List<IActivity<T, TResourceId, TWorkStreamId>> CloneActivities();
    }
}
