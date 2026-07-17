using System;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// Default factory for the Activity-on-Arrow dummy-edge orchestrator. The
    /// orchestrator mutates the graph, so this factory requires the state to be
    /// one created by a graph builder in this library (custom read-only state
    /// implementations cannot be mutated).
    /// </summary>
    /// <typeparam name="T">The activity/event ID type.</typeparam>
    /// <typeparam name="TResourceId">The resource ID type.</typeparam>
    /// <typeparam name="TWorkStreamId">The work-stream ID type.</typeparam>
    /// <typeparam name="TActivity">The activity type.</typeparam>
    public sealed class DummyEdgeOrchestratorFactory<T, TResourceId, TWorkStreamId, TActivity>
        : IDummyEdgeOrchestratorFactory<T, TResourceId, TWorkStreamId, TActivity>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
        where TActivity : class, IActivity<T, TResourceId, TWorkStreamId>
    {
        /// <inheritdoc/>
        public IDummyEdgeOrchestrator<T, TResourceId, TWorkStreamId, TActivity> Create(
            IIdGenerator<T> edgeIdGenerator,
            IActivityGenerator<T, TResourceId, TWorkStreamId, TActivity> dummyActivityGenerator,
            IArrowStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity> sccFinder,
            IArrowGraphState<T, TResourceId, TWorkStreamId, TActivity> state)
        {
            var concreteState = state as ArrowGraphState<T, TResourceId, TWorkStreamId, TActivity>;
            if (concreteState is null)
            {
                throw new InvalidOperationException(
                    $@"The supplied graph state must be created by a graph builder in this library (actual type: {state?.GetType().FullName ?? @"null"})");
            }

            return new DummyEdgeOrchestrator<T, TResourceId, TWorkStreamId, TActivity>(
                edgeIdGenerator,
                dummyActivityGenerator,
                sccFinder,
                concreteState);
        }
    }
}
