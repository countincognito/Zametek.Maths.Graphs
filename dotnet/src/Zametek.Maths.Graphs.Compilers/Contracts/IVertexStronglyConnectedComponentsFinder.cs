using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// Finds strongly connected components (Tarjan's algorithm) for Vertex graphs.
    /// In a Vertex graph, activities are nodes - the algorithm traverses node-space.
    /// </summary>
    /// <typeparam name="T">The activity/event ID type.</typeparam>
    /// <typeparam name="TResourceId">The resource ID type.</typeparam>
    /// <typeparam name="TWorkStreamId">The work-stream ID type.</typeparam>
    /// <typeparam name="TActivity">The activity type.</typeparam>
    public interface IVertexStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
        where TActivity : IActivity<T, TResourceId, TWorkStreamId>
    {
        /// <summary>
        /// Finds all strongly connected components in the graph. When
        /// <paramref name="ignoreDummies"/> is true, removable (dummy) nodes are
        /// excluded from the reported components.
        /// </summary>
        List<ICircularDependency<T>> FindStronglyConnectedComponents(
            IVertexGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            bool ignoreDummies);

        /// <summary>
        /// Finds the strongly connected components that contain more than one
        /// member - i.e. the genuine circular dependencies.
        /// </summary>
        List<ICircularDependency<T>> FindStronglyCircularDependencies(
            IVertexGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            bool ignoreDummies);
    }
}
