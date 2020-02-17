using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    public class DependentActivity<T>
        : Activity<T>, IDependentActivity<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        #region Ctors

        public DependentActivity(T id, int duration)
            : base(id, duration)
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
            T id, string name, IEnumerable<T> targetResources, IEnumerable<T> dependencies, IEnumerable<T> resourceDependencies,
            LogicalOperator targetLogicalOperator, bool canBeRemoved, int duration, int? freeSlack, int? earliestStartTime,
            int? latestFinishTime, int? minimumFreeSlack, int? minimumEarliestStartTime, DateTime? minimumEarliestStartDateTime)
            : base(
                  id, name, targetResources, targetLogicalOperator, canBeRemoved, duration, freeSlack, earliestStartTime,
                  latestFinishTime, minimumFreeSlack, minimumEarliestStartTime, minimumEarliestStartDateTime)
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

        #region Public Methods

        public static IDependentActivity<T> CreateDependentActivityDummy(T id)
        {
            var dummy = new DependentActivity<T>(id, 0);
            dummy.SetAsRemovable();
            return dummy;
        }

        #endregion

        #region IHaveDependencies<T> Members

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

        public override object WorkingCopy()
        {
            return new DependentActivity<T>(
                Id, Name, TargetResources, Dependencies, ResourceDependencies, TargetResourceOperator,
                CanBeRemoved, Duration, FreeSlack, EarliestStartTime, LatestFinishTime, MinimumFreeSlack,
                MinimumEarliestStartTime, MinimumEarliestStartDateTime);
        }

        #endregion
    }
}
