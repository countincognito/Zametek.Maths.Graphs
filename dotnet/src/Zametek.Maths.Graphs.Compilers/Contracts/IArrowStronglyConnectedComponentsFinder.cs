using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// Finds strongly connected components (Tarjan's algorithm) for Arrow graphs.
    /// In an Arrow graph, activities are edges - the algorithm traverses edge-space.
    /// </summary>
    /// <typeparam name="T">The activity/event ID type.</typeparam>
    /// <typeparam name="TResourceId">The resource ID type.</typeparam>
    /// <typeparam name="TWorkStreamId">The work-stream ID type.</typeparam>
    /// <typeparam name="TActivity">The activity type.</typeparam>
    public interface IArrowStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
        where TActivity : class, IActivity<T, TResourceId, TWorkStreamId>
    {
        /// <summary>
        /// Finds all strongly connected components in the graph. When
        /// <paramref name="ignoreDummies"/> is true, removable (dummy) edges are
        /// excluded from the reported components.
        /// </summary>
        List<ICircularDependency<T>> FindStronglyConnectedComponents(
            IArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            bool ignoreDummies);

        /// <summary>
        /// Finds the strongly connected components that contain more than one
        /// member - i.e. the genuine circular dependencies.
        /// </summary>
        List<ICircularDependency<T>> FindStronglyCircularDependencies(
            IArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            bool ignoreDummies);
    }
}
