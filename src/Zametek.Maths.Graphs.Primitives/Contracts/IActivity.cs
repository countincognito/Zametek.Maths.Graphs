using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    public interface IActivity<T>
        : IHaveId<T>, ICanBeRemoved, ICloneObject
        where T : struct, IComparable<T>, IEquatable<T>
    {
        string Name
        {
            get;
            set;
        }

        HashSet<T> TargetResources
        {
            get;
        }

        LogicalOperator TargetResourceOperator
        {
            get;
            set;
        }

        bool IsDummy
        {
            get;
        }

        int Duration
        {
            get;
            set;
        }

        int? TotalSlack
        {
            get;
        }

        int? FreeSlack
        {
            get;
            set;
        }

        int? InterferingSlack
        {
            get;
        }

        bool IsCritical
        {
            get;
        }

        int? EarliestStartTime
        {
            get;
            set;
        }

        int? LatestStartTime
        {
            get;
        }

        int? EarliestFinishTime
        {
            get;
        }

        int? LatestFinishTime
        {
            get;
            set;
        }

        int? MinimumFreeSlack
        {
            get;
            set;
        }

        int? MinimumEarliestStartTime
        {
            get;
            set;
        }

        DateTime? MinimumEarliestStartDateTime
        {
            get;
            set;
        }

        void SetAsReadOnly();

        void SetAsRemovable();
    }
}
