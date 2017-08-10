using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    public abstract class VertexGraphCompilerBase<T, TDependentActivity, TActivity, TEvent>
        : GraphCompilerBase<T, TEvent, TDependentActivity, TDependentActivity, TEvent>
        where TDependentActivity : IDependentActivity<T>, TActivity
        where TActivity : IActivity<T>
        where TEvent : IEvent<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        #region Fields

        private readonly object m_Lock;
        private readonly VertexGraphBuilderBase<T, TDependentActivity, TEvent> m_VertexGraphBuilder;

        #endregion

        #region Ctors

        protected VertexGraphCompilerBase(VertexGraphBuilderBase<T, TDependentActivity, TEvent> vertexGraphBuilder)
            : base(vertexGraphBuilder)
        {
            m_VertexGraphBuilder = vertexGraphBuilder ?? throw new ArgumentNullException(nameof(vertexGraphBuilder));
            m_Lock = new object();
        }

        #endregion

        #region Public Methods

        //public bool AddActivityDependencies(T activityId, HashSet<T> dependencies)
        //{
        //    lock (m_Lock)
        //    {
        //        if (dependencies == null)
        //        {
        //            throw new ArgumentNullException(nameof(dependencies));
        //        }
        //        if (!m_VertexGraphBuilder.ActivityIds.Contains(activityId))
        //        {
        //            return false;
        //        }
        //        if (!dependencies.Any())
        //        {
        //            return true;
        //        }

        //        var activity = (TDependentActivity)m_VertexGraphBuilder.Activity(activityId);
        //        var resourceAndCompiledDependencies = new HashSet<T>(activity.ResourceDependencies.Intersect(activity.Dependencies));
        //        var resourceOrCompiledDependencies = new HashSet<T>(activity.ResourceDependencies.Union(activity.Dependencies));
        //        var onlyResourceDependencies = new HashSet<T>(activity.ResourceDependencies.Except(resourceAndCompiledDependencies));

        //        // If a dependency is already a compiled dependency, then do nothing.

        //        // If a dependency is already a resource dependency, but not a compiled dependency,
        //        // then just add it to the the compiled dependencies.
        //        var toBeAddedToCompiledDependencies = new HashSet<T>(dependencies.Intersect(onlyResourceDependencies));

        //        foreach (T dependencyId in toBeAddedToCompiledDependencies)
        //        {
        //            activity.Dependencies.Add(dependencyId);
        //        }

        //        // If a dependency is neither a compiled dependency, nor a resource dependency,
        //        // then add it to everything.
        //        var toBeAddedToEverything = new HashSet<T>(dependencies.Except(resourceOrCompiledDependencies));

        //        foreach (T dependencyId in toBeAddedToEverything)
        //        {
        //            activity.Dependencies.Add(dependencyId);
        //        }

        //        return m_VertexGraphBuilder.AddActivityDependencies(activityId, toBeAddedToEverything);
        //    }
        //}

        //public bool RemoveActivityDependencies(T activityId, HashSet<T> dependencies)
        //{
        //    lock (m_Lock)
        //    {
        //        if (dependencies == null)
        //        {
        //            throw new ArgumentNullException(nameof(dependencies));
        //        }
        //        if (!m_VertexGraphBuilder.ActivityIds.Contains(activityId))
        //        {
        //            return false;
        //        }
        //        if (!dependencies.Any())
        //        {
        //            return true;
        //        }

        //        var activity = (TDependentActivity)m_VertexGraphBuilder.Activity(activityId);
        //        var resourceAndCompiledDependencies = new HashSet<T>(activity.ResourceDependencies.Intersect(activity.Dependencies));
        //        var onlyCompiledDependencies = new HashSet<T>(activity.Dependencies.Except(resourceAndCompiledDependencies));

        //        // If a dependency is a resource dependency, but not a compiled dependency,
        //        // then do nothing.

        //        // If a dependency is a resource dependency, and also a compiled dependency,
        //        // then just remove it from the compiled dependencies.
        //        var toBeRemovedFromCompiledDependencies = new HashSet<T>(dependencies.Intersect(resourceAndCompiledDependencies));

        //        foreach (T dependencyId in toBeRemovedFromCompiledDependencies)
        //        {
        //            activity.Dependencies.Remove(dependencyId);
        //        }

        //        // If a dependency is only a compiled dependency, but not a resource dependency,
        //        // then remove it from the compiled dependencies and the graph builder.
        //        var toBeRemovedFromEverything = new HashSet<T>(dependencies.Intersect(onlyCompiledDependencies));

        //        foreach (T dependencyId in toBeRemovedFromEverything)
        //        {
        //            activity.Dependencies.Remove(dependencyId);
        //        }

        //        return m_VertexGraphBuilder.RemoveActivityDependencies(activityId, toBeRemovedFromEverything);
        //    }
        //}

        public bool SetActivityDependencies(T activityId, HashSet<T> dependencies)
        {
            lock (m_Lock)
            {
                if (dependencies == null)
                {
                    throw new ArgumentNullException(nameof(dependencies));
                }
                if (!m_VertexGraphBuilder.ActivityIds.Contains(activityId))
                {
                    return false;
                }

                var activity = m_VertexGraphBuilder.Activity(activityId);
                var resourceAndCompiledDependencies = new HashSet<T>(activity.ResourceDependencies.Intersect(activity.Dependencies));
                var resourceOrCompiledDependencies = new HashSet<T>(activity.ResourceDependencies.Union(activity.Dependencies));
                var onlyCompiledDependencies = new HashSet<T>(activity.Dependencies.Except(resourceAndCompiledDependencies));
                var onlyResourceDependencies = new HashSet<T>(activity.ResourceDependencies.Except(resourceAndCompiledDependencies));

                // If an existing dependency is a resource dependency, but not a compiled
                // dependency, and is not in the new dependencies, then do nothing.

                // If an existing dependency is a resource dependency, and also a compiled
                // dependency, and is in the new dependencies, then do nothing.

                // If an existing dependency is a compiled dependency, but not a resource
                // dependency, and is in the new dependencies, then do nothing.

                // If an existing dependency is a resource dependency, and also a compiled
                // dependency, and is not in the new dependencies, then remove it from the
                // compiled dependencies.
                var toBeRemovedFromCompiledDependencies = new HashSet<T>(resourceAndCompiledDependencies.Except(dependencies));

                foreach (T dependencyId in toBeRemovedFromCompiledDependencies)
                {
                    activity.Dependencies.Remove(dependencyId);
                }

                // If an existing dependency is a resource dependency, but not a compiled
                // dependency, and is in the new dependencies, then add it to the compiled
                // dependencies.
                var toBeAddedToCompiledDependencies = new HashSet<T>(onlyResourceDependencies.Intersect(dependencies));

                foreach (T dependencyId in toBeAddedToCompiledDependencies)
                {
                    activity.Dependencies.Add(dependencyId);
                }

                // If an existing dependency is a compiled dependency, but not a resource
                // dependency, and is not in the new dependencies, then remove it from everything.
                var toBeRemovedFromEverything = new HashSet<T>(onlyCompiledDependencies.Except(dependencies));

                foreach (T dependencyId in toBeRemovedFromEverything)
                {
                    activity.Dependencies.Remove(dependencyId);
                }

                bool successfullyRemoved = m_VertexGraphBuilder.RemoveActivityDependencies(activityId, toBeRemovedFromEverything);

                // If a new dependency is neither a compiled dependency, nor a resource
                // dependency, then add it to everything.
                var toBeAddedToEverything = new HashSet<T>(dependencies.Except(resourceOrCompiledDependencies));

                foreach (T dependencyId in toBeAddedToEverything)
                {
                    activity.Dependencies.Add(dependencyId);
                }

                bool successfullyAdded = m_VertexGraphBuilder.AddActivityDependencies(activityId, toBeAddedToEverything);

                // Final return.
                return successfullyRemoved && successfullyAdded;
            }
        }

        public GraphCompilation<T, TDependentActivity> Compile(IList<IResource<T>> resources)
        {
            if (resources == null)
            {
                throw new ArgumentNullException(nameof(resources));
            }

            lock (m_Lock)
            {
                // Reset activity dependencies in the graph to match only the compiled dependencies
                // (i.e. remove any that are *only* resource dependencies).
                foreach (TDependentActivity activity in m_VertexGraphBuilder.Activities)
                {
                    m_VertexGraphBuilder.RemoveActivityDependencies(
                        activity.Id,
                        new HashSet<T>(activity.ResourceDependencies.Except(activity.Dependencies)));
                    activity.ResourceDependencies.Clear();
                }

                // Sanity check the resources.
                bool allResourcesExplicitTargetsButNotAllActivitiesTargeted =
                    resources.Any()
                    && resources.All(x => x.IsExplicitTarget)
                    && m_VertexGraphBuilder.Activities.Any(x => !x.IsDummy && !x.TargetResources.Any());

                // Sanity check the graph data.
                IEnumerable<CircularDependency<T>> circularDependencies = m_VertexGraphBuilder.FindStrongCircularDependencies();
                IEnumerable<T> missingDependencies = m_VertexGraphBuilder.MissingDependencies;

                if (circularDependencies.Any()
                    || missingDependencies.Any()
                    || allResourcesExplicitTargetsButNotAllActivitiesTargeted
                    || !m_VertexGraphBuilder.CleanUpEdges())
                {
                    return new GraphCompilation<T, TDependentActivity>(
                        allResourcesExplicitTargetsButNotAllActivitiesTargeted,
                        circularDependencies.ToList(),
                        missingDependencies.ToList(),
                        m_VertexGraphBuilder.Activities.Select(x => (TDependentActivity)x.WorkingCopy()),
                        Enumerable.Empty<IResourceSchedule<T>>());
                }

                // Perform first compilation and calculate resource schedules.
                m_VertexGraphBuilder.CalculateCriticalPath();
                IEnumerable<IResourceSchedule<T>> resourceSchedules = m_VertexGraphBuilder.CalculateResourceSchedulesByPriorityList(resources);

                if (resources.Any())
                {
                    // Determine the resource dependencies and add them to the compiled dependencies.
                    foreach (IResourceSchedule<T> resourceSchedule in resourceSchedules)
                    {
                        T previousId = default(T);
                        bool first = true;
                        foreach (IScheduledActivity<T> scheduledActivity in resourceSchedule.ScheduledActivities.OrderBy(x => x.StartTime))
                        {
                            if (!first)
                            {
                                // Here we add the previous activity ID to the set of resource
                                // dependencies attached to the activity itself. However, we do
                                // not add it to the compiled dependencies set - instead we make
                                // the change directly to graph data (which we reverse at the start
                                // of each compilation - see the top of this method).
                                T currentId = scheduledActivity.Id;
                                var activity = m_VertexGraphBuilder.Activity(currentId);

                                activity.ResourceDependencies.Add(previousId);
                                m_VertexGraphBuilder.AddActivityDependencies(
                                    currentId,
                                    new HashSet<T>(activity.ResourceDependencies.Except(activity.Dependencies)));
                            }

                            first = false;
                            previousId = scheduledActivity.Id;
                        }
                    }

                    // Rerun the compilation with the new dependencies.
                    m_VertexGraphBuilder.CalculateCriticalPath();
                }

                return new GraphCompilation<T, TDependentActivity>(
                    false,
                    Enumerable.Empty<CircularDependency<T>>(),
                    Enumerable.Empty<T>(),
                    m_VertexGraphBuilder.Activities.Select(x => (TDependentActivity)x.WorkingCopy()),
                    resourceSchedules.ToList());
            }
        }

        #endregion

        #region Overrides

        public override bool AddActivity(TDependentActivity activity)
        {
            lock (m_Lock)
            {
                return m_VertexGraphBuilder.AddActivity(activity, activity.Dependencies);
            }
        }

        public override bool RemoveActivity(T activityId)
        {
            lock (m_Lock)
            {
                // Clear out the activity from compiled dependencies first.
                IEnumerable<T> dependentActivityIds = m_VertexGraphBuilder
                    .Activities
                    .Where(x => x.Dependencies.Contains(activityId))
                    .Select(x => x.Id);

                foreach (T dependentActivityId in dependentActivityIds)
                {
                    var dependentActivity = m_VertexGraphBuilder.Activity(dependentActivityId);
                    dependentActivity.Dependencies.Remove(activityId);
                }

                m_VertexGraphBuilder.Activity(activityId)?.SetAsRemovable();
                return m_VertexGraphBuilder.RemoveActivity(activityId);
            }
        }

        #endregion
    }
}
