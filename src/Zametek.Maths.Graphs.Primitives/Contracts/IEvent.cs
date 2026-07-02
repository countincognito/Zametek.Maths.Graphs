using System;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// A milestone marker within a graph. In Activity-on-Arrow graphs events sit
    /// on the nodes; in Activity-on-Vertex graphs they sit on the edges.
    /// </summary>
    /// <typeparam name="T">The event ID type.</typeparam>
    public interface IEvent<out T>
        : IHaveId<T>, ICanBeRemoved, ICloneObject
        where T : struct, IComparable<T>, IEquatable<T>
    {
        /// <summary>
        /// The earliest time the event can occur. Null until calculated.
        /// </summary>
        int? EarliestFinishTime { get; set; }

        /// <summary>
        /// The latest time the event can occur. Null until calculated.
        /// </summary>
        int? LatestFinishTime { get; set; }
    }
}
