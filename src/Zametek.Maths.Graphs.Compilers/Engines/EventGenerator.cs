using System;

namespace Zametek.Maths.Graphs
{
    // Default event generator for Activity-on-Arrow graphs. Events represent real
    // milestones, so they are created read-only (not removable).
    /// <summary>
    /// Default event generator for Activity-on-Arrow graphs - events are created read-only (not removable).
    /// </summary>
    public class EventGenerator<T>
        : IEventGenerator<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        /// <inheritdoc/>
        public IEvent<T> Generate(T id)
        {
            return new Event<T>(id);
        }

        /// <inheritdoc/>
        public IEvent<T> Generate(T id, int? earliestFinishTime, int? latestFinishTime)
        {
            return new Event<T>(id, earliestFinishTime, latestFinishTime);
        }
    }
}
