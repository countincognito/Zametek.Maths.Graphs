using System;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// An activity as placed on a resource's timeline: a snapshot of its identity
    /// plus the scheduled start and finish times.
    /// </summary>
    /// <typeparam name="T">The activity ID type.</typeparam>
    public interface IScheduledActivity<out T>
        : IHaveId<T>, ICloneObject
        where T : struct, IComparable<T>, IEquatable<T>
    {
        /// <summary>
        /// The display name of the activity.
        /// </summary>
        string? Name { get; }

        /// <summary>
        /// Whether the activity is excluded from cost allocation.
        /// </summary>
        bool HasNoCost { get; }

        /// <summary>
        /// Whether the activity is excluded from billing allocation.
        /// </summary>
        bool HasNoBilling { get; }

        /// <summary>
        /// Whether the activity is excluded from effort allocation.
        /// </summary>
        bool HasNoEffort { get; }

        /// <summary>
        /// The duration of the activity, in whole time units.
        /// </summary>
        int Duration { get; }

        /// <summary>
        /// The time the activity is scheduled to start.
        /// </summary>
        int StartTime { get; }

        /// <summary>
        /// The time the activity is scheduled to finish.
        /// </summary>
        int FinishTime { get; }
    }
}
