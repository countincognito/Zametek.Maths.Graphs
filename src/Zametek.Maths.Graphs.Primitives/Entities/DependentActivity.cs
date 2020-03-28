using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    public class DependentActivity<T, TResourceId>
        : Activity<T, TResourceId>, IDependentActivity<T, TResourceId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
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
            if (dependencies == null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }
            Dependencies = new HashSet<T>(dependencies);
            ResourceDependencies = new HashSet<T>();
        }

        public DependentActivity(
            T id, string name, IEnumerable<TResourceId> targetResources, IEnumerable<T> dependencies, IEnumerable<T> resourceDependencies,
            LogicalOperator targetLogicalOperator, IEnumerable<TResourceId> allocatedToResources, bool canBeRemoved, int duration,
            int? freeSlack, int? earliestStartTime, int? latestFinishTime, int? minimumFreeSlack, int? minimumEarliestStartTime)
            : base(
                  id, name, targetResources, targetLogicalOperator, allocatedToResources, canBeRemoved, duration, freeSlack,
                  earliestStartTime, latestFinishTime, minimumFreeSlack, minimumEarliestStartTime)
        {
            if (dependencies == null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }
            if (resourceDependencies == null)
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
            return new DependentActivity<T, TResourceId>(
                Id, Name, TargetResources, Dependencies, ResourceDependencies, TargetResourceOperator, AllocatedToResources,
                CanBeRemoved, Duration, FreeSlack, EarliestStartTime, LatestFinishTime, MinimumFreeSlack, MinimumEarliestStartTime);
        }

        #endregion
    }
}
