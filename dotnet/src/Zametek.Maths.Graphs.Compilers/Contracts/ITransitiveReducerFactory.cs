using System;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// Creates the transitive reducer for an Activity-on-Arrow graph. The reducer
    /// is bound to the builder's graph state at construction time, so it is the
    /// factory - not the reducer itself - that is injected into the builder.
    /// Custom implementations typically decorate the reducer produced by
    /// <see cref="ArrowTransitiveReducerFactory{T, TResourceId, TWorkStreamId, TActivity}"/>.
    /// </summary>
    /// <typeparam name="T">The activity/event ID type.</typeparam>
    /// <typeparam name="TResourceId">The resource ID type.</typeparam>
    /// <typeparam name="TWorkStreamId">The work-stream ID type.</typeparam>
    /// <typeparam name="TActivity">The activity type.</typeparam>
    public interface IArrowTransitiveReducerFactory<T, TResourceId, TWorkStreamId, TActivity>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
        where TActivity : class, IActivity<T, TResourceId, TWorkStreamId>
    {
        /// <summary>
        /// Creates a transitive reducer bound to the given graph state.
        /// </summary>
        ITransitiveReducer<T> Create(
            IDummyEdgeOrchestrator<T, TResourceId, TWorkStreamId, TActivity> dummyEdgeOrchestrator,
            IArrowStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity> sccFinder,
            IArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state);
    }

    /// <summary>
    /// Creates the transitive reducer for an Activity-on-Vertex graph. The reducer
    /// is bound to the builder's graph state at construction time, so it is the
    /// factory - not the reducer itself - that is injected into the builder.
    /// Custom implementations typically decorate the reducer produced by
    /// <see cref="VertexTransitiveReducerFactory{T, TResourceId, TWorkStreamId, TActivity}"/>.
    /// </summary>
    /// <typeparam name="T">The activity/event ID type.</typeparam>
    /// <typeparam name="TResourceId">The resource ID type.</typeparam>
    /// <typeparam name="TWorkStreamId">The work-stream ID type.</typeparam>
    /// <typeparam name="TActivity">The activity type.</typeparam>
    public interface IVertexTransitiveReducerFactory<T, TResourceId, TWorkStreamId, TActivity>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
        where TActivity : IActivity<T, TResourceId, TWorkStreamId>
    {
        /// <summary>
        /// Creates a transitive reducer bound to the given graph state.
        /// </summary>
        ITransitiveReducer<T> Create(
            IVertexStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity> sccFinder,
            IVertexGraphState<T, TResourceId, TWorkStreamId, TActivity> state);
    }
}
