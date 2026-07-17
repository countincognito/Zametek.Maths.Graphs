using System;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// Creates the dummy-edge orchestrator for an Activity-on-Arrow graph. The
    /// orchestrator is bound to the builder's graph state at construction time,
    /// so it is the factory - not the orchestrator itself - that is injected into
    /// the builder. Custom implementations typically decorate the orchestrator
    /// produced by <see cref="DummyEdgeOrchestratorFactory{T, TResourceId, TWorkStreamId, TActivity}"/>.
    /// </summary>
    /// <typeparam name="T">The activity/event ID type.</typeparam>
    /// <typeparam name="TResourceId">The resource ID type.</typeparam>
    /// <typeparam name="TWorkStreamId">The work-stream ID type.</typeparam>
    /// <typeparam name="TActivity">The activity type.</typeparam>
    public interface IDummyEdgeOrchestratorFactory<T, TResourceId, TWorkStreamId, TActivity>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
        where TActivity : class, IActivity<T, TResourceId, TWorkStreamId>
    {
        /// <summary>
        /// Creates a dummy-edge orchestrator bound to the given graph state.
        /// </summary>
        IDummyEdgeOrchestrator<T, TResourceId, TWorkStreamId, TActivity> Create(
            IIdGenerator<T> edgeIdGenerator,
            IActivityGenerator<T, TResourceId, TWorkStreamId, TActivity> dummyActivityGenerator,
            IArrowStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity> sccFinder,
            IArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state);
    }
}
