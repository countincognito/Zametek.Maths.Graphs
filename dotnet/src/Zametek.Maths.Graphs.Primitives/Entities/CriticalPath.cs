using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// A critical path of a schedule: a chain of critical activities, each of which starts at
    /// the moment that the one before it finishes, from the start of the plan to its finish.
    /// </summary>
    /// <remarks>
    /// The chain holds only the activities that take time. An activity of zero duration (a
    /// milestone) is passed through and does not appear, and so two chains that differ only by a
    /// milestone are one path. When the chain passes through a milestone between two activities,
    /// the link of the second is <see cref="CriticalPathLink.Resource"/> if any hop of the chain
    /// between them is a resource link.
    /// </remarks>
    /// <typeparam name="T">The activity ID type.</typeparam>
    public sealed class CriticalPath<T>
        : IEquatable<CriticalPath<T>>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        /// <summary>
        /// Creates a path.
        /// </summary>
        /// <param name="steps">The steps, in the order of the schedule.</param>
        public CriticalPath(IEnumerable<CriticalPathStep<T>> steps)
        {
            if (steps is null)
            {
                throw new ArgumentNullException(nameof(steps));
            }
            Steps = steps.ToList();
        }

        /// <summary>
        /// The steps, in the order of the schedule.
        /// </summary>
        public IReadOnlyList<CriticalPathStep<T>> Steps { get; }

        /// <inheritdoc/>
        public bool Equals(CriticalPath<T>? other) => other != null && Steps.SequenceEqual(other.Steps);

        /// <inheritdoc/>
        public override bool Equals(object? obj) => Equals(obj as CriticalPath<T>);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (CriticalPathStep<T> step in Steps)
            {
                hash.Add(step);
            }
            return hash.ToHashCode();
        }

        /// <inheritdoc/>
        public override string ToString() => string.Join(@" > ", Steps.Select(x => x.ActivityId));
    }
}
