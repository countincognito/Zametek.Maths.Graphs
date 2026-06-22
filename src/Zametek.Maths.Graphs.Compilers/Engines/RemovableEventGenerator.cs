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
    public class RemovableEventGenerator<T>
        : IEventGenerator<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        private readonly IEventGenerator<T> m_Inner;

        public RemovableEventGenerator()
            : this(new EventGenerator<T>())
        {
        }

        public RemovableEventGenerator(IEventGenerator<T> inner)
        {
            m_Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public IEvent<T> Generate(T id)
        {
            IEvent<T> output = m_Inner.Generate(id);
            output.SetAsRemovable();
            return output;
        }

        public IEvent<T> Generate(T id, int? earliestFinishTime, int? latestFinishTime)
        {
            IEvent<T> output = m_Inner.Generate(id, earliestFinishTime, latestFinishTime);
            output.SetAsRemovable();
            return output;
        }
    }
}
