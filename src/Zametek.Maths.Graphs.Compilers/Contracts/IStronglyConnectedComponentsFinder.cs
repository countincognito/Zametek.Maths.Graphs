using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    // Finds strongly connected components (Tarjan's algorithm) for Arrow graphs.
    // In an Arrow graph, activities are edges — the algorithm traverses edge-space.
    internal interface IArrowStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
        where TActivity : class, IActivity<T, TResourceId, TWorkStreamId>
    {
        List<ICircularDependency<T>> FindStronglyConnectedComponents(
            ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            bool ignoreDummies);

        List<ICircularDependency<T>> FindStronglyCircularDependencies(
            ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            bool ignoreDummies);
    }

    // Finds strongly connected components (Tarjan's algorithm) for Vertex graphs.
    // In a Vertex graph, activities are nodes — the algorithm traverses node-space.
    internal interface IVertexStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
        where TActivity : IActivity<T, TResourceId, TWorkStreamId>
    {
        List<ICircularDependency<T>> FindStronglyConnectedComponents(
            VertexGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            bool ignoreDummies);

        List<ICircularDependency<T>> FindStronglyCircularDependencies(
            VertexGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            bool ignoreDummies);
    }
}
