using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// Default implementation of <see cref="IDependentActivity{T, TResourceId, TWorkStreamId}"/>. This is the input type the graph compilers consume.
    /// </summary>
    public class DependentActivity<T, TResourceId, TWorkStreamId>
        : Activity<T, TResourceId, TWorkStreamId>, IDependentActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        #region Ctors

        /// <summary>
        /// Creates an activity with the given ID and duration, and no dependencies.
        /// </summary>
        public DependentActivity(T id, int duration)
            : base(id, duration)
        {
            Dependencies = new HashSet<T>();
            PlanningDependencies = new HashSet<T>();
            ResourceDependencies = new HashSet<T>();
            Successors = new HashSet<T>();
        }

        /// <summary>
        /// Creates an activity with the given ID, duration and removability flag.
        /// </summary>
        public DependentActivity(T id, int duration, bool canBeRemoved)
            : base(id, duration, canBeRemoved)
        {
            Dependencies = new HashSet<T>();
            PlanningDependencies = new HashSet<T>();
            ResourceDependencies = new HashSet<T>();
            Successors = new HashSet<T>();
        }

        /// <summary>
        /// Creates an activity with the given ID, duration and dependencies.
        /// </summary>
        public DependentActivity(T id, int duration, IEnumerable<T> dependencies)
            : base(id, duration)
        {
            if (dependencies is null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }
            Dependencies = new HashSet<T>(dependencies);
            PlanningDependencies = new HashSet<T>();
            ResourceDependencies = new HashSet<T>();
            Successors = new HashSet<T>();
        }

        /// <summary>
        /// Creates an activity with the given ID, duration, dependencies and planning dependencies.
        /// </summary>
        public DependentActivity(T id, int duration, IEnumerable<T> dependencies, IEnumerable<T> planningDependencies)
            : base(id, duration)
        {
            if (dependencies is null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }
            if (planningDependencies is null)
            {
                throw new ArgumentNullException(nameof(planningDependencies));
            }
            Dependencies = new HashSet<T>(dependencies);
            PlanningDependencies = new HashSet<T>(planningDependencies);
            ResourceDependencies = new HashSet<T>();
            Successors = new HashSet<T>();
        }

        /// <summary>
        /// Creates a fully-specified activity (used by cloning).
        /// </summary>
        public DependentActivity(
            T id, string? name, string? notes, IEnumerable<TWorkStreamId> targetWorkStreams, IEnumerable<TResourceId> targetResources,
            IEnumerable<T> dependencies, IEnumerable<T> planningDependencies, IEnumerable<T> resourceDependencies, IEnumerable<T> successors,
            LogicalOperator targetLogicalOperator, IEnumerable<TResourceId> allocatedToResources, bool canBeRemoved, bool hasNoCost, bool hasNoBilling, bool hasNoEffort,
            int duration, int? freeSlack, int? earliestStartTime, int? latestFinishTime, int? minimumFreeSlack, int? minimumEarliestStartTime, int? maximumLatestFinishTime)
            : base(
                  id, name, notes, targetWorkStreams, targetResources, targetLogicalOperator, allocatedToResources, canBeRemoved, hasNoCost, hasNoBilling,
                  hasNoEffort, duration, freeSlack, earliestStartTime, latestFinishTime, minimumFreeSlack, minimumEarliestStartTime, maximumLatestFinishTime)
        {
            if (dependencies is null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }
            if (planningDependencies is null)
            {
                throw new ArgumentNullException(nameof(planningDependencies));
            }
            if (resourceDependencies is null)
            {
                throw new ArgumentNullException(nameof(resourceDependencies));
            }
            if (successors is null)
            {
                throw new ArgumentNullException(nameof(successors));
            }
            Dependencies = new HashSet<T>(dependencies);
            PlanningDependencies = new HashSet<T>(planningDependencies);
            ResourceDependencies = new HashSet<T>(resourceDependencies);
            Successors = new HashSet<T>(successors);
        }

        #endregion

        #region IDependentActivity<T> Members

        /// <inheritdoc/>
        public HashSet<T> Dependencies
        {
            get;
        }

        /// <inheritdoc/>
        public HashSet<T> PlanningDependencies
        {
            get;
        }

        /// <inheritdoc/>
        public HashSet<T> ResourceDependencies
        {
            get;
        }

        /// <inheritdoc/>
        public HashSet<T> Successors
        {
            get;
        }

        #endregion

        #region Overrides

        /// <inheritdoc/>
        public override object CloneObject()
        {
            return new DependentActivity<T, TResourceId, TWorkStreamId>(
                Id, Name, Notes, TargetWorkStreams, TargetResources, Dependencies, PlanningDependencies, ResourceDependencies, Successors,
                TargetResourceOperator, AllocatedToResources, CanBeRemoved, HasNoCost, HasNoBilling, HasNoEffort, Duration, FreeSlack,
                EarliestStartTime, LatestFinishTime, MinimumFreeSlack, MinimumEarliestStartTime, MaximumLatestFinishTime);
        }

        #endregion
    }
}
