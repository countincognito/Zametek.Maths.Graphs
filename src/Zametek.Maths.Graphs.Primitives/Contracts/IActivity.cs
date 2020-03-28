using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    public interface IActivity<out T, TResourceId>
        : IHaveId<T>, ICanBeRemoved, ICloneObject
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
    {
        string Name { get; set; }

        IEnumerable<TResourceId> TargetResources { get; }

        LogicalOperator TargetResourceOperator { get; set; }

        HashSet<TResourceId> AllocatedToResources { get; }

        bool IsDummy { get; }

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

        void SetAsReadOnly();

        void SetAsRemovable();
    }
}
