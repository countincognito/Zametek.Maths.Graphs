using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    // Read-only projection of a graph builder that the resource scheduling engine
    // operates on. Implemented by both Arrow and Vertex builders (and their clones),
    // replacing the activity/dependency callback delegates the scheduler used to take.
    public interface IResourceSchedulingGraph<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        // Resolves the live activity for the given id.
        IActivity<T, TResourceId, TWorkStreamId> Activity(T id);

        // Returns the strong (resolved) dependency ids for the given activity id.
        List<T> StrongActivityDependencyIds(T id);

        // Returns a cloned snapshot of all activities in the graph.
        List<IActivity<T, TResourceId, TWorkStreamId>> CloneActivities();
    }
}
