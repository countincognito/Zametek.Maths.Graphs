using System;

namespace Zametek.Maths.Graphs
{
    // Default event generator for Activity-on-Vertex graphs. Events live on edges,
    // which are structural and can be removed during transitive reduction, so the
    // generated events are flagged as removable.
    //
    // Implemented as a decorator over an inner IEventGenerator<T>: it delegates the
    // actual event creation to the inner generator and then decorates the result by
    // flagging it as removable. By default it wraps an EventGenerator<T>, so it can
    // still be constructed parameterlessly.
    /// <summary>
    /// Default event generator for Activity-on-Vertex graphs: decorates an inner generator and flags every event as removable.
    /// </summary>
    public class RemovableEventGenerator<T>
        : IEventGenerator<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        private readonly IEventGenerator<T> m_Inner;

        /// <summary>
        /// Creates a generator decorating a default <see cref="EventGenerator{T}"/>.
        /// </summary>
        public RemovableEventGenerator()
            : this(new EventGenerator<T>())
        {
        }

        /// <summary>
        /// Creates a generator decorating the given inner generator.
        /// </summary>
        public RemovableEventGenerator(IEventGenerator<T> inner)
        {
            m_Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        /// <inheritdoc/>
        public IEvent<T> Generate(T id)
        {
            IEvent<T> output = m_Inner.Generate(id);
            output.SetAsRemovable();
            return output;
        }

        /// <inheritdoc/>
        public IEvent<T> Generate(T id, int? earliestFinishTime, int? latestFinishTime)
        {
            IEvent<T> output = m_Inner.Generate(id, earliestFinishTime, latestFinishTime);
            output.SetAsRemovable();
            return output;
        }
    }
}
