using System;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// A constraint violation found on an activity before or after compilation.
    /// </summary>
    /// <typeparam name="T">The activity ID type.</typeparam>
    public interface IInvalidConstraint<T>
        : IHaveId<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        /// <summary>
        /// A human-readable description of the violated constraint.
        /// </summary>
        string Message { get; }
    }
}
