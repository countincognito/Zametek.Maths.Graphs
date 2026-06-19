using System;

namespace Zametek.Maths.Graphs
{
    // Default event generator for Activity-on-Vertex graphs. Events live on edges,
    // which are structural and can be removed during transitive reduction, so the
    // generated events are flagged as removable.
    public class RemovableEventGenerator<T>
        : IEventGenerator<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        public IEvent<T> Generate(T id)
        {
            var output = new Event<T>(id);
            output.SetAsRemovable();
            return output;
        }

        public IEvent<T> Generate(T id, int? earliestFinishTime, int? latestFinishTime)
        {
            var output = new Event<T>(id, earliestFinishTime, latestFinishTime);
            output.SetAsRemovable();
            return output;
        }
    }
}
