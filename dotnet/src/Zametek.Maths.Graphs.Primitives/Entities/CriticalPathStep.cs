using System;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// One activity on a critical path, and how it follows the activity before it.
    /// </summary>
    /// <typeparam name="T">The activity ID type.</typeparam>
    public readonly struct CriticalPathStep<T>
        : IEquatable<CriticalPathStep<T>>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        /// <summary>
        /// Creates a step.
        /// </summary>
        /// <param name="activityId">The activity.</param>
        /// <param name="link">How the activity follows the one before it.</param>
        public CriticalPathStep(T activityId, CriticalPathLink link)
        {
            ActivityId = activityId;
            Link = link;
        }

        /// <summary>
        /// The activity.
        /// </summary>
        public T ActivityId { get; }

        /// <summary>
        /// How the activity follows the one before it: <see cref="CriticalPathLink.None"/> for the first step.
        /// </summary>
        public CriticalPathLink Link { get; }

        /// <inheritdoc/>
        public bool Equals(CriticalPathStep<T> other) => ActivityId.Equals(other.ActivityId) && Link == other.Link;

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is CriticalPathStep<T> other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(ActivityId, Link);

        /// <inheritdoc/>
        public override string ToString() => $@"{ActivityId} ({Link})";
    }
}
