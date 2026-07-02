using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// An activity that carries its own dependency information. This is the input
    /// type the graph compilers consume.
    /// </summary>
    /// <typeparam name="T">The activity ID type.</typeparam>
    /// <typeparam name="TResourceId">The resource ID type.</typeparam>
    /// <typeparam name="TWorkStreamId">The work-stream ID type.</typeparam>
    public interface IDependentActivity<T, TResourceId, TWorkStreamId>
        : IActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        /// <summary>
        /// The IDs of the activities that must finish before this one can start.
        /// </summary>
        HashSet<T> Dependencies { get; }

        /// <summary>
        /// Additional planning-time dependencies, kept separate from the compiled
        /// dependencies.
        /// </summary>
        HashSet<T> PlanningDependencies { get; }

        /// <summary>
        /// Dependencies introduced by resource allocation (activities queued on
        /// the same resource). Populated during compilation.
        /// </summary>
        HashSet<T> ResourceDependencies { get; }

        /// <summary>
        /// The IDs of the activities that depend on this one. Populated during
        /// compilation.
        /// </summary>
        HashSet<T> Successors { get; }
    }
}
