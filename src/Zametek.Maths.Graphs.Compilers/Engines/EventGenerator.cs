using System;

namespace Zametek.Maths.Graphs
{
    // Default event generator for Activity-on-Arrow graphs. Events represent real
    // milestones, so they are created read-only (not removable).
    public class EventGenerator<T>
        : IEventGenerator<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        public IEvent<T> Generate(T id)
        {
            return new Event<T>(id);
        }

        public IEvent<T> Generate(T id, int? earliestFinishTime, int? latestFinishTime)
        {
            return new Event<T>(id, earliestFinishTime, latestFinishTime);
        }
    }
}
