using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    public class DependentActivity<T, TResourceId, TWorkStreamId>
        : Activity<T, TResourceId, TWorkStreamId>, IDependentActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        #region Ctors

        public DependentActivity(T id, int duration)
            : base(id, duration)
        {
            Dependencies = new HashSet<T>();
            ResourceDependencies = new HashSet<T>();
        }

        public DependentActivity(T id, int duration, bool canBeRemoved)
            : base(id, duration, canBeRemoved)
        {
            Dependencies = new HashSet<T>();
            ResourceDependencies = new HashSet<T>();
        }

        public DependentActivity(T id, int duration, IEnumerable<T> dependencies)
            : base(id, duration)
        {
            if (dependencies is null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }
            Dependencies = new HashSet<T>(dependencies);
            ResourceDependencies = new HashSet<T>();
        }

        public DependentActivity(
            T id, string name, string notes, IEnumerable<TWorkStreamId> targetWorkStreams, IEnumerable<TResourceId> targetResources,
            IEnumerable<T> dependencies, IEnumerable<T> resourceDependencies, LogicalOperator targetLogicalOperator,
            IEnumerable<TResourceId> allocatedToResources, bool canBeRemoved, bool hasNoCost, int duration, int? freeSlack,
            int? earliestStartTime, int? latestFinishTime, int? minimumFreeSlack, int? minimumEarliestStartTime, int? maximumLatestFinishTime)
            : base(
                  id, name, notes, targetWorkStreams, targetResources, targetLogicalOperator, allocatedToResources, canBeRemoved, hasNoCost,
                  duration, freeSlack, earliestStartTime, latestFinishTime, minimumFreeSlack, minimumEarliestStartTime, maximumLatestFinishTime)
        {
            if (dependencies is null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }
            if (resourceDependencies is null)
            {
                throw new ArgumentNullException(nameof(resourceDependencies));
            }
            Dependencies = new HashSet<T>(dependencies);
            ResourceDependencies = new HashSet<T>(resourceDependencies);
        }

        #endregion

        #region IDependentActivity<T> Members

        public HashSet<T> Dependencies
        {
            get;
        }

        public HashSet<T> ResourceDependencies
        {
            get;
        }

        #endregion

        #region Overrides

        public override object CloneObject()
        {
            return new DependentActivity<T, TResourceId, TWorkStreamId>(
                Id, Name, Notes, TargetWorkStreams, TargetResources, Dependencies, ResourceDependencies, TargetResourceOperator,
                AllocatedToResources, CanBeRemoved, HasNoCost, Duration, FreeSlack, EarliestStartTime, LatestFinishTime, MinimumFreeSlack,
                MinimumEarliestStartTime, MaximumLatestFinishTime);
        }

        #endregion
    }
}
