using System;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// A bundle of the engines and factories a <see cref="VertexGraphBuilder{T, TResourceId, TWorkStreamId, TActivity}"/>
    /// relies on, each defaulting to the standard implementation. Passing the
    /// bundle to the builder keeps the constructor signature stable as engines are
    /// added: set only the properties you want to customise.
    /// </summary>
    /// <typeparam name="T">The activity/event ID type.</typeparam>
    /// <typeparam name="TResourceId">The resource ID type.</typeparam>
    /// <typeparam name="TWorkStreamId">The work-stream ID type.</typeparam>
    /// <typeparam name="TActivity">The activity type.</typeparam>
    public class VertexGraphBuilderEngines<T, TResourceId, TWorkStreamId, TActivity>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
        where TActivity : IActivity<T, TResourceId, TWorkStreamId>
    {
        /// <summary>
        /// Generates the IDs for the edges (events) of the graph.
        /// </summary>
        public IIdGenerator<T> EdgeIdGenerator { get; set; } =
            new PreviousIdGenerator<T>();

        /// <summary>
        /// Creates the events placed on the graph's edges.
        /// </summary>
        public IEventGenerator<T> EventGenerator { get; set; } =
            new RemovableEventGenerator<T>();

        /// <summary>
        /// Detects circular dependencies.
        /// </summary>
        public IVertexStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity> SccFinder { get; set; } =
            new VertexTarjanStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity>();

        /// <summary>
        /// Performs the critical-path calculations.
        /// </summary>
        public IVertexCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity> CriticalPathEngine { get; set; } =
            new VertexCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity>();

        /// <summary>
        /// Performs resource scheduling and its surrounding pipeline.
        /// </summary>
        public IResourceSchedulingEngine<T, TResourceId, TWorkStreamId> ResourceSchedulingEngine { get; set; } =
            new PriorityListResourceScheduler<T, TResourceId, TWorkStreamId>();

        /// <summary>
        /// Creates the transitive reducer bound to the builder's graph state.
        /// </summary>
        public IVertexTransitiveReducerFactory<T, TResourceId, TWorkStreamId, TActivity> TransitiveReducerFactory { get; set; } =
            new VertexTransitiveReducerFactory<T, TResourceId, TWorkStreamId, TActivity>();
    }
}
