using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    public interface IActivity<out T, TResourceId, TWorkStreamId>
        : IHaveId<T>, ICanBeRemoved, ICloneObject
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        string Name { get; set; }

        string Notes { get; set; }

        HashSet<TWorkStreamId> TargetWorkStreams { get; }

        HashSet<TResourceId> TargetResources { get; }

        LogicalOperator TargetResourceOperator { get; set; }

        HashSet<TResourceId> AllocatedToResources { get; }

        bool IsDummy { get; }

        bool HasNoCost { get; set; }

        bool HasNoBilling { get; set; }

        bool HasNoEffort { get; set; }

        int Duration { get; set; }

        int? TotalSlack { get; }

        int? FreeSlack { get; set; }

        int? InterferingSlack { get; }

        bool IsCritical { get; }

        int? EarliestStartTime { get; set; }

        int? LatestStartTime { get; }

        int? EarliestFinishTime { get; }

        int? LatestFinishTime { get; set; }

        int? MinimumFreeSlack { get; set; }

        int? MinimumEarliestStartTime { get; set; }

        int? MaximumLatestFinishTime { get; set; }

        void SetAsReadOnly();

        void SetAsRemovable();
    }
}
