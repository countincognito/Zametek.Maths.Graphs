using System;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// Default factory for the Activity-on-Vertex transitive reducer. The reducer
    /// mutates the graph, so this factory requires the state to be one created by
    /// a graph builder in this library (custom read-only state implementations
    /// cannot be reduced).
    /// </summary>
    /// <typeparam name="T">The activity/event ID type.</typeparam>
    /// <typeparam name="TResourceId">The resource ID type.</typeparam>
    /// <typeparam name="TWorkStreamId">The work-stream ID type.</typeparam>
    /// <typeparam name="TActivity">The activity type.</typeparam>
    public sealed class VertexTransitiveReducerFactory<T, TResourceId, TWorkStreamId, TActivity>
        : IVertexTransitiveReducerFactory<T, TResourceId, TWorkStreamId, TActivity>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
        where TActivity : IActivity<T, TResourceId, TWorkStreamId>
    {
        /// <inheritdoc/>
        public ITransitiveReducer<T> Create(
            IVertexStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity> sccFinder,
            IVertexGraphState<T, TResourceId, TWorkStreamId, TActivity> state)
        {
            var concreteState = state as VertexGraphState<T, TResourceId, TWorkStreamId, TActivity>;
            if (concreteState is null)
            {
                throw new InvalidOperationException(
                    $@"The supplied graph state must be created by a graph builder in this library (actual type: {state?.GetType().FullName ?? @"null"})");
            }

            return new VertexTransitiveReducer<T, TResourceId, TWorkStreamId, TActivity>(
                sccFinder,
                concreteState);
        }
    }
}
