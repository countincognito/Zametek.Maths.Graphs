using System;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// The standard factory of Activity-on-Vertex graphs. Its builders have the engines of a
    /// <see cref="VertexGraphBuilderEngines{T, TResourceId, TWorkStreamId, TActivity}"/> bundle (the standard
    /// engines, when the bundle has not been changed), and its compilers are a
    /// <see cref="VertexGraphCompiler{T, TResourceId, TWorkStreamId, TDependentActivity}"/> around such a
    /// builder.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The factory gives the same engines to every builder that it makes, as
    /// <see cref="VertexGraphBuilder{T, TResourceId, TWorkStreamId, TActivity}.CloneObject"/> does for a clone.
    /// The engines must therefore hold no state, or be safe for use by many threads. All the standard engines
    /// are. An engine that holds state needs a factory of your own, one that makes new engines each time:
    /// implement <see cref="IVertexGraphFactory{T, TResourceId, TWorkStreamId, TActivity}"/>.
    /// </para>
    /// <para>
    /// The ID generator factory of the bundle is one of these engines. A generator of IDs holds the count of one
    /// graph, and so builders cannot share one, but each builder asks the ID generator factory for a generator
    /// of its own.
    /// </para>
    /// <para>
    /// The factory keeps the engines of the bundle, and not the bundle. A change to the bundle after the
    /// constructor has run does not change the factory.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The activity ID type.</typeparam>
    /// <typeparam name="TResourceId">The resource ID type.</typeparam>
    /// <typeparam name="TWorkStreamId">The work-stream ID type.</typeparam>
    /// <typeparam name="TActivity">The activity type.</typeparam>
    public class VertexGraphFactory<T, TResourceId, TWorkStreamId, TActivity>
        : IVertexGraphFactory<T, TResourceId, TWorkStreamId, TActivity>
        where TActivity : IDependentActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        private readonly IIdGeneratorFactory<T> m_EdgeIdGeneratorFactory;
        private readonly IEventGenerator<T> m_EventGenerator;
        private readonly IVertexStronglyConnectedComponentsFinder<T, TResourceId, TWorkStreamId, TActivity> m_SccFinder;
        private readonly IVertexCriticalPathEngine<T, TResourceId, TWorkStreamId, TActivity> m_CriticalPathEngine;
        private readonly IResourceSchedulingEngine<T, TResourceId, TWorkStreamId> m_ResourceSchedulingEngine;
        private readonly IVertexTransitiveReducer<T, TResourceId, TWorkStreamId, TActivity> m_TransitiveReducer;

        /// <summary>
        /// Creates a factory with the standard engines.
        /// </summary>
        public VertexGraphFactory()
            : this(new VertexGraphBuilderEngines<T, TResourceId, TWorkStreamId, TActivity>())
        {
        }

        /// <summary>
        /// Creates a factory whose builders have the engines of the bundle. Each builder asks the
        /// <see cref="VertexGraphBuilderEngines{T, TResourceId, TWorkStreamId, TActivity}.EdgeIdGeneratorFactory"/> of
        /// the bundle for an ID generator of its own.
        /// </summary>
        /// <param name="engines">The engines of the builders.</param>
        /// <exception cref="ArgumentNullException">The bundle is null, or one of its engines is.</exception>
        public VertexGraphFactory(VertexGraphBuilderEngines<T, TResourceId, TWorkStreamId, TActivity> engines)
        {
            if (engines is null)
            {
                throw new ArgumentNullException(nameof(engines));
            }

            m_EdgeIdGeneratorFactory = engines.EdgeIdGeneratorFactory
                ?? throw new ArgumentNullException(nameof(engines), @"The engines have no edge ID generator factory.");
            m_EventGenerator = engines.EventGenerator
                ?? throw new ArgumentNullException(nameof(engines), @"The engines have no event generator.");
            m_SccFinder = engines.SccFinder
                ?? throw new ArgumentNullException(nameof(engines), @"The engines have no finder of strongly connected components.");
            m_CriticalPathEngine = engines.CriticalPathEngine
                ?? throw new ArgumentNullException(nameof(engines), @"The engines have no critical-path engine.");
            m_ResourceSchedulingEngine = engines.ResourceSchedulingEngine
                ?? throw new ArgumentNullException(nameof(engines), @"The engines have no resource scheduling engine.");
            m_TransitiveReducer = engines.TransitiveReducer
                ?? throw new ArgumentNullException(nameof(engines), @"The engines have no transitive reducer.");
        }

        /// <inheritdoc/>
        public VertexGraphBuilder<T, TResourceId, TWorkStreamId, TActivity> CreateBuilder() =>
            new VertexGraphBuilder<T, TResourceId, TWorkStreamId, TActivity>(
                new VertexGraphBuilderEngines<T, TResourceId, TWorkStreamId, TActivity>
                {
                    EdgeIdGeneratorFactory = m_EdgeIdGeneratorFactory,
                    EventGenerator = m_EventGenerator,
                    SccFinder = m_SccFinder,
                    CriticalPathEngine = m_CriticalPathEngine,
                    ResourceSchedulingEngine = m_ResourceSchedulingEngine,
                    TransitiveReducer = m_TransitiveReducer,
                });

        /// <inheritdoc/>
        public VertexGraphCompiler<T, TResourceId, TWorkStreamId, TActivity> CreateCompiler() =>
            new VertexGraphCompiler<T, TResourceId, TWorkStreamId, TActivity>(CreateBuilder());
    }
}
