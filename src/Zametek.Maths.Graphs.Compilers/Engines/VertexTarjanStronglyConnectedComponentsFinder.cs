using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    // Tarjan's strongly connected components algorithm for Vertex (Activity-on-Vertex) graphs.
    // Activities are nodes; the algorithm traverses node-space. This is a thin facade that
    // wraps the graph state in a VertexGraphTraversal and delegates to the shared algorithm.
    // https://en.wikipedia.org/wiki/Tarjan%27s_strongly_connected_components_algorithm
    public sealed class VertexTarjanStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity>
        : IVertexStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
        where TActivity : IActivity<T, TResourceId, TWorkStreamId>
    {
        public List<ICircularDependency<T>> FindStronglyConnectedComponents(
            IVertexGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            bool ignoreDummies)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            return TarjanStronglyConnectedComponents.FindStronglyConnectedComponents(
                new VertexGraphTraversal<T, TResourceId, TWorkStreamId, TActivity>(state),
                ignoreDummies);
        }

        public List<ICircularDependency<T>> FindStronglyCircularDependencies(
            IVertexGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            bool ignoreDummies)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            return TarjanStronglyConnectedComponents.FindStronglyCircularDependencies(
                new VertexGraphTraversal<T, TResourceId, TWorkStreamId, TActivity>(state),
                ignoreDummies);
        }
    }
}
