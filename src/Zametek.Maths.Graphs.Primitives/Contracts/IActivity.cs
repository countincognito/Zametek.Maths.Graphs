using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// A unit of work in a project schedule. Carries the input values (duration,
    /// constraints, resource targeting) and receives the computed critical-path
    /// values (start/finish times and slack) during compilation.
    /// </summary>
    /// <typeparam name="T">The activity ID type.</typeparam>
    /// <typeparam name="TResourceId">The resource ID type.</typeparam>
    /// <typeparam name="TWorkStreamId">The work-stream ID type.</typeparam>
    public interface IActivity<out T, TResourceId, TWorkStreamId>
        : IHaveId<T>, ICanBeRemoved, ICloneObject<IActivity<T, TResourceId, TWorkStreamId>>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        /// <summary>
        /// The display name of the activity.
        /// </summary>
        string? Name { get; set; }

        /// <summary>
        /// Free-form notes attached to the activity.
        /// </summary>
        string? Notes { get; set; }

        /// <summary>
        /// The IDs of the work streams (phases) the activity belongs to.
        /// </summary>
        HashSet<TWorkStreamId> TargetWorkStreams { get; }

        /// <summary>
        /// The IDs of the resources allowed to perform the activity. Empty means
        /// any non-explicit-target resource may perform it.
        /// </summary>
        HashSet<TResourceId> TargetResources { get; }

        /// <summary>
        /// How the target resources combine (all of them, any one of them, or all
        /// of those actually available).
        /// </summary>
        LogicalOperator TargetResourceOperator { get; set; }

        /// <summary>
        /// The IDs of the resources the activity was actually allocated to during
        /// the last compilation.
        /// </summary>
        HashSet<TResourceId> AllocatedToResources { get; }

        /// <summary>
        /// Whether the activity is a zero-duration (dummy) activity.
        /// </summary>
        bool IsDummy { get; }

        /// <summary>
        /// Whether the activity is excluded from cost allocation.
        /// </summary>
        bool HasNoCost { get; set; }

        /// <summary>
        /// Whether the activity is excluded from billing allocation.
        /// </summary>
        bool HasNoBilling { get; set; }

        /// <summary>
        /// Whether the activity is excluded from effort allocation.
        /// </summary>
        bool HasNoEffort { get; set; }

        /// <summary>
        /// The duration of the activity, in whole time units.
        /// </summary>
        int Duration { get; set; }

        /// <summary>
        /// How long the activity can slip without delaying the project
        /// (latest finish minus earliest finish). Null until calculated.
        /// </summary>
        int? TotalSlack { get; }

        /// <summary>
        /// How long the activity can slip without delaying any of its successors.
        /// Null until calculated.
        /// </summary>
        int? FreeSlack { get; set; }

        /// <summary>
        /// The portion of total slack whose use would delay a successor
        /// (total slack minus free slack). Null until calculated.
        /// </summary>
        int? InterferingSlack { get; }

        /// <summary>
        /// Whether the activity is on the critical path (total slack of zero or less).
        /// </summary>
        bool IsCritical { get; }

        /// <summary>
        /// The earliest time the activity can start. Null until calculated.
        /// </summary>
        int? EarliestStartTime { get; set; }

        /// <summary>
        /// The latest time the activity can start without delaying the project
        /// (latest finish minus duration). Null until calculated.
        /// </summary>
        int? LatestStartTime { get; }

        /// <summary>
        /// The earliest time the activity can finish (earliest start plus
        /// duration). Null until calculated.
        /// </summary>
        int? EarliestFinishTime { get; }

        /// <summary>
        /// The latest time the activity can finish without delaying the project.
        /// Null until calculated.
        /// </summary>
        int? LatestFinishTime { get; set; }

        /// <summary>
        /// Optional constraint: the minimum free slack the activity must retain.
        /// </summary>
        int? MinimumFreeSlack { get; set; }

        /// <summary>
        /// Optional constraint: the activity must not start before this time.
        /// </summary>
        int? MinimumEarliestStartTime { get; set; }

        /// <summary>
        /// Optional constraint: the activity must finish by this time.
        /// </summary>
        int? MaximumLatestFinishTime { get; set; }
    }
}
