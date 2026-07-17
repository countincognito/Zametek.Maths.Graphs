using System;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// A thing that is identified by a unique ID.
    /// </summary>
    /// <typeparam name="T">The ID type.</typeparam>
    public interface IHaveId<out T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        /// <summary>
        /// The unique ID.
        /// </summary>
        T Id { get; }
    }
}
