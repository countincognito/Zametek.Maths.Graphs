using System;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// Creates the <see cref="IEvent{T}"/> instances the graph builders place on
    /// nodes (arrow graphs) or edges (vertex graphs).
    /// </summary>
    /// <typeparam name="T">The event ID type.</typeparam>
    public interface IEventGenerator<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        /// <summary>
        /// Creates an event with the given ID.
        /// </summary>
        IEvent<T> Generate(T id);

        /// <summary>
        /// Creates an event with the given ID and initial finish times.
        /// </summary>
        IEvent<T> Generate(T id, int? earliestFinishTime, int? latestFinishTime);
    }
}
