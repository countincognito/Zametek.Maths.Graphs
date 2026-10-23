using System;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// The standard factory of Activity-on-Arrow graphs. Its builders have the engines of an
    /// <see cref="ArrowGraphBuilderEngines{T, TResourceId, TWorkStreamId, TActivity}"/> bundle (the standard
    /// engines, when the bundle has not been changed), and its compilers are an
    /// <see cref="ArrowGraphCompiler{T, TResourceId, TWorkStreamId, TDependentActivity}"/> around such a
    /// builder.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The factory gives the same engines to every builder that it makes, as
    /// <see cref="ArrowGraphBuilder{T, TResourceId, TWorkStreamId, TActivity}.CloneObject"/> does for a clone.
    /// The engines must therefore hold no state, or be safe for use by many threads. All the standard engines
    /// are. An engine that holds state needs a factory of your own, one that makes new engines each time:
    /// implement <see cref="IArrowGraphFactory{T, TResourceId, TWorkStreamId, TActivity}"/>.
    /// </para>
    /// <para>
    /// A generator of IDs is different: it holds the count of one graph, and so builders cannot share one. The
    /// factory gives each builder two new <see cref="PreviousIdGenerator{T}"/> objects, and it does not use the
    /// <see cref="ArrowGraphBuilderEngines{T, TResourceId, TWorkStreamId, TActivity}.EdgeIdGenerator"/> and the
    /// <see cref="ArrowGraphBuilderEngines{T, TResourceId, TWorkStreamId, TActivity}.NodeIdGenerator"/> of the
    /// bundle.
    /// </para>
    /// <para>
    /// The factory keeps the engines of the bundle, and not the bundle. A change to the bundle after the
    /// constructor has run does not change the factory.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The activity/event ID type.</typeparam>
    /// <typeparam name="TResourceId">The resource ID type.</typeparam>
    /// <typeparam name="TWorkStreamId">The work-stream ID type.</typeparam>
    /// <typeparam name="TActivity">The activity type.</typeparam>
    public class ArrowGraphFactory<T, TResourceId, TWorkStreamId, TActivity>
        : IArrowGraphFactory<T, TResourceId, TWorkStreamId, TActivity>
        where TActivity : class, IDependentActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        private readonly IActivityGenerator<T, TResourceId, TWorkStreamId, TActivity> m_DummyActivityGenerator;
        private readonly IEventGenerator<T> m_EventGenerator;
        private readonly IArrowStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity> m_SccFinder;
        private readonly IArrowCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity> m_CriticalPathEngine;
        private readonly IResourceSchedulingEngine<T, TResourceId, TWorkStreamId> m_ResourceSchedulingEngine;
        private readonly IDummyEdgeOrchestrator<T, TResourceId, TWorkStreamId, TActivity> m_DummyEdgeOrchestrator;
        private readonly IArrowTransitiveReducer<T, TResourceId, TWorkStreamId, TActivity> m_TransitiveReducer;

        /// <summary>
        /// Creates a factory with the standard engines.
        /// </summary>
        public ArrowGraphFactory()
            : this(new ArrowGraphBuilderEngines<T, TResourceId, TWorkStreamId, TActivity>())
        {
        }

        /// <summary>
        /// Creates a factory whose builders have the engines of the bundle. The
        /// <see cref="ArrowGraphBuilderEngines{T, TResourceId, TWorkStreamId, TActivity}.EdgeIdGenerator"/> and the
        /// <see cref="ArrowGraphBuilderEngines{T, TResourceId, TWorkStreamId, TActivity}.NodeIdGenerator"/> of the
        /// bundle are not used: each builder has ID generators of its own.
        /// </summary>
        /// <param name="engines">The engines of the builders.</param>
        /// <exception cref="ArgumentNullException">The bundle is null, or one of its engines is.</exception>
        public ArrowGraphFactory(ArrowGraphBuilderEngines<T, TResourceId, TWorkStreamId, TActivity> engines)
        {
            if (engines is null)
            {
                throw new ArgumentNullException(nameof(engines));
            }

            m_DummyActivityGenerator = engines.DummyActivityGenerator
                ?? throw new ArgumentNullException(nameof(engines), @"The engines have no generator of dummy activities.");
            m_EventGenerator = engines.EventGenerator
                ?? throw new ArgumentNullException(nameof(engines), @"The engines have no event generator.");
            m_SccFinder = engines.SccFinder
                ?? throw new ArgumentNullException(nameof(engines), @"The engines have no finder of strongly connected components.");
            m_CriticalPathEngine = engines.CriticalPathEngine
                ?? throw new ArgumentNullException(nameof(engines), @"The engines have no critical-path engine.");
            m_ResourceSchedulingEngine = engines.ResourceSchedulingEngine
                ?? throw new ArgumentNullException(nameof(engines), @"The engines have no resource scheduling engine.");
            m_DummyEdgeOrchestrator = engines.DummyEdgeOrchestrator
                ?? throw new ArgumentNullException(nameof(engines), @"The engines have no dummy-edge orchestrator.");
            m_TransitiveReducer = engines.TransitiveReducer
                ?? throw new ArgumentNullException(nameof(engines), @"The engines have no transitive reducer.");
        }

        /// <inheritdoc/>
        public ArrowGraphBuilder<T, TResourceId, TWorkStreamId, TActivity> CreateBuilder() =>
            new ArrowGraphBuilder<T, TResourceId, TWorkStreamId, TActivity>(
                new ArrowGraphBuilderEngines<T, TResourceId, TWorkStreamId, TActivity>
                {
                    EdgeIdGenerator = new PreviousIdGenerator<T>(),
                    NodeIdGenerator = new PreviousIdGenerator<T>(),
                    DummyActivityGenerator = m_DummyActivityGenerator,
                    EventGenerator = m_EventGenerator,
                    SccFinder = m_SccFinder,
                    CriticalPathEngine = m_CriticalPathEngine,
                    ResourceSchedulingEngine = m_ResourceSchedulingEngine,
                    DummyEdgeOrchestrator = m_DummyEdgeOrchestrator,
                    TransitiveReducer = m_TransitiveReducer,
                });

        /// <inheritdoc/>
        public ArrowGraphCompiler<T, TResourceId, TWorkStreamId, TActivity> CreateCompiler() =>
            new ArrowGraphCompiler<T, TResourceId, TWorkStreamId, TActivity>(CreateBuilder());
    }
}
