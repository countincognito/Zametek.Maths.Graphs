using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    // Tarjan's strongly connected components algorithm for Arrow (Activity-on-Arrow) graphs.
    // Activities are edges; the algorithm traverses edge-space. This is a thin facade that
    // wraps the graph state in an ArrowGraphTraversal and delegates to the shared algorithm.
    // https://en.wikipedia.org/wiki/Tarjan%27s_strongly_connected_components_algorithm
    public sealed class ArrowTarjanStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity>
        : IArrowStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
        where TActivity : class, IActivity<T, TResourceId, TWorkStreamId>
    {
        public List<ICircularDependency<T>> FindStronglyConnectedComponents(
            IArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            bool ignoreDummies)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            return TarjanStronglyConnectedComponents.FindStronglyConnectedComponents(
                new ArrowGraphTraversal<T, TResourceId, TWorkStreamId, TActivity>(state),
                ignoreDummies);
        }

        public List<ICircularDependency<T>> FindStronglyCircularDependencies(
            IArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state,
            bool ignoreDummies)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            return TarjanStronglyConnectedComponents.FindStronglyCircularDependencies(
                new ArrowGraphTraversal<T, TResourceId, TWorkStreamId, TActivity>(state),
                ignoreDummies);
        }
    }
}
