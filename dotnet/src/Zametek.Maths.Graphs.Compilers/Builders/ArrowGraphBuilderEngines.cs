using System;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// A bundle of the engines an <see cref="ArrowGraphBuilder{T, TResourceId, TWorkStreamId, TActivity}"/>
    /// relies on, each defaulting to the standard implementation. Passing the
    /// bundle to the builder keeps the constructor signature stable as engines are
    /// added: set only the properties you want to customise.
    /// </summary>
    /// <typeparam name="T">The activity/event ID type.</typeparam>
    /// <typeparam name="TResourceId">The resource ID type.</typeparam>
    /// <typeparam name="TWorkStreamId">The work-stream ID type.</typeparam>
    /// <typeparam name="TActivity">The activity type.</typeparam>
    public class ArrowGraphBuilderEngines<T, TResourceId, TWorkStreamId, TActivity>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
        where TActivity : class, IActivity<T, TResourceId, TWorkStreamId>
    {
        /// <summary>
        /// Generates the IDs for the edges (activities) of the graph, including
        /// dummy activities.
        /// </summary>
        public IIdGenerator<T> EdgeIdGenerator { get; set; } =
            new PreviousIdGenerator<T>();

        /// <summary>
        /// Generates the IDs for the nodes (events) of the graph.
        /// </summary>
        public IIdGenerator<T> NodeIdGenerator { get; set; } =
            new PreviousIdGenerator<T>();

        /// <summary>
        /// Creates the dummy (zero-duration) activities that preserve dependencies.
        /// </summary>
        public IActivityGenerator<T, TResourceId, TWorkStreamId, TActivity> DummyActivityGenerator { get; set; } =
            new DummyActivityGenerator<T, TResourceId, TWorkStreamId, TActivity>();

        /// <summary>
        /// Creates the events placed on the graph's nodes.
        /// </summary>
        public IEventGenerator<T> EventGenerator { get; set; } =
            new EventGenerator<T>();

        /// <summary>
        /// Detects circular dependencies.
        /// </summary>
        public IArrowStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity> SccFinder { get; set; } =
            new ArrowTarjanStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity>();

        /// <summary>
        /// Performs the critical-path calculations.
        /// </summary>
        public IArrowCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity> CriticalPathEngine { get; set; } =
            new ArrowCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity>();

        /// <summary>
        /// Performs resource scheduling and its surrounding pipeline.
        /// </summary>
        public IResourceSchedulingEngine<T, TResourceId, TWorkStreamId> ResourceSchedulingEngine { get; set; } =
            new PriorityListResourceScheduler<T, TResourceId, TWorkStreamId>();

        /// <summary>
        /// Performs the dummy-edge operations (stateless; the builder passes it the
        /// graph state and the generators/SCC finder each operation needs).
        /// </summary>
        public IDummyEdgeOrchestrator<T, TResourceId, TWorkStreamId, TActivity> DummyEdgeOrchestrator { get; set; } =
            new DummyEdgeOrchestrator<T, TResourceId, TWorkStreamId, TActivity>();

        /// <summary>
        /// Performs transitive reduction (stateless; the builder passes it the graph
        /// state, SCC finder and dummy-edge orchestrator per call).
        /// </summary>
        public IArrowTransitiveReducer<T, TResourceId, TWorkStreamId, TActivity> TransitiveReducer { get; set; } =
            new ArrowTransitiveReducer<T, TResourceId, TWorkStreamId, TActivity>();
    }
}
