using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Zametek.Maths.Graphs
{
    public abstract class VertexGraphCompilerBase<T, TResourceId, TDependentActivity, TActivity, TEvent>
        : GraphCompilerBase<T, TResourceId, TEvent, TDependentActivity, TDependentActivity, TEvent>
        where TDependentActivity : IDependentActivity<T, TResourceId>
        where TActivity : IActivity<T, TResourceId>
        where TEvent : IEvent<T>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
    {
        #region Fields

        private readonly object m_Lock;
        private readonly VertexGraphBuilderBase<T, TResourceId, TDependentActivity, TEvent> m_VertexGraphBuilder;

        #endregion

        #region Ctors

        protected VertexGraphCompilerBase(VertexGraphBuilderBase<T, TResourceId, TDependentActivity, TEvent> vertexGraphBuilder)
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
        //        if (dependencies is null)
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
        //        if (dependencies is null)
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
                if (dependencies is null)
                {
                    throw new ArgumentNullException(nameof(dependencies));
                }
                if (!m_VertexGraphBuilder.ActivityIds.Contains(activityId))
                {
                    return false;
                }

                TDependentActivity activity = m_VertexGraphBuilder.Activity(activityId);
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

        public IGraphCompilation<T, TResourceId, TDependentActivity> Compile()
        {
            return Compile(new List<IResource<TResourceId>>());
        }

        public IGraphCompilation<T, TResourceId, TDependentActivity> Compile(IList<IResource<TResourceId>> resources)
        {
            if (resources is null)
            {
                throw new ArgumentNullException(nameof(resources));
            }

            lock (m_Lock)
            {
                IEnumerable<TDependentActivity> activities = m_VertexGraphBuilder.Activities;

                // Reset activity dependencies in the graph to match only the compiled dependencies
                // (i.e. remove any that are *only* resource dependencies).
                foreach (TDependentActivity activity in activities)
                {
                    m_VertexGraphBuilder.RemoveActivityDependencies(
                        activity.Id,
                        new HashSet<T>(activity.ResourceDependencies.Except(activity.Dependencies)));
                    activity.ResourceDependencies.Clear();
                    activity.AllocatedToResources.Clear();
                }

                // Sanity check the graph data.
                IEnumerable<T> missingDependencies = m_VertexGraphBuilder.MissingDependencies;
                IEnumerable<ICircularDependency<T>> circularDependencies = m_VertexGraphBuilder.FindStrongCircularDependencies();
                IEnumerable<IInvalidConstraint<T>> invalidPrecompilationConstraints = m_VertexGraphBuilder.FindInvalidPreCompilationConstraints();

                // If resources are 0, assume infinite.
                bool infiniteResources = !resources.Any();

                // Filter out disabled resources.
                IList<IResource<TResourceId>> filteredResources = resources.Where(x => !x.IsDisabled).ToList();

                // Sanity check the resources.
                bool allResourcesExplicitTargetsButNotAllActivitiesTargeted =
                    !infiniteResources
                    && filteredResources.All(x => x.IsExplicitTarget)
                    && m_VertexGraphBuilder.Activities.Any(x => !x.IsDummy && !x.TargetResources.Any());

                // Collate pre-compilation errors, if any exist.

                var compilationErrors = new List<GraphCompilationError>();

                // P0010
                if (missingDependencies.Any())
                {
                    compilationErrors.Add(
                        new GraphCompilationError(
                            GraphCompilationErrorCode.P0010,
                            BuildMissingDependenciesErrorMessage(missingDependencies, activities)));
                }

                // P0020
                if (circularDependencies.Any())
                {
                    compilationErrors.Add(
                        new GraphCompilationError(
                            GraphCompilationErrorCode.P0020,
                            BuildCircularDependenciesErrorMessage(circularDependencies)));
                }

                // P0030
                if (invalidPrecompilationConstraints.Any())
                {
                    compilationErrors.Add(
                        new GraphCompilationError(
                            GraphCompilationErrorCode.P0030,
                            BuildInvalidConstraintsErrorMessage(invalidPrecompilationConstraints)));
                }

                // P0040
                if (allResourcesExplicitTargetsButNotAllActivitiesTargeted)
                {
                    compilationErrors.Add(
                        new GraphCompilationError(
                            GraphCompilationErrorCode.P0040,
                            $@"{Resources.Message_AllResourcesExplicitTargetsNotAllActivitiesTargeted}{Environment.NewLine}"));
                }

                // P0050
                if (!m_VertexGraphBuilder.CleanUpEdges())
                {
                    compilationErrors.Add(
                        new GraphCompilationError(
                            GraphCompilationErrorCode.P0050,
                            $@"{Resources.Message_UnableToRemoveUnnecessaryEdges}{Environment.NewLine}"));
                }

                if (compilationErrors.Any())
                {
                    return new GraphCompilation<T, TResourceId, TDependentActivity>(
                        m_VertexGraphBuilder.Activities.Select(x => (TDependentActivity)x.CloneObject()),
                        Enumerable.Empty<IResourceSchedule<T, TResourceId>>(),
                        compilationErrors);
                }

                // Perform first compilation and calculate resource schedules.
                m_VertexGraphBuilder.CalculateCriticalPath();
                IEnumerable<IResourceSchedule<T, TResourceId>> resourceSchedules = m_VertexGraphBuilder.CalculateResourceSchedulesByPriorityList(filteredResources);

                if (!infiniteResources)
                {
                    // Determine the resource dependencies and add them to the compiled dependencies.
                    foreach (IResourceSchedule<T, TResourceId> resourceSchedule in resourceSchedules)
                    {
                        T previousId = default;
                        bool first = true;
                        IResource<TResourceId> resource = resourceSchedule.Resource;

                        foreach (IScheduledActivity<T> scheduledActivity in resourceSchedule.ScheduledActivities.OrderBy(x => x.StartTime))
                        {
                            T currentId = scheduledActivity.Id;
                            TDependentActivity activity = m_VertexGraphBuilder.Activity(currentId);

                            if (resource != null)
                            {
                                activity.AllocatedToResources.Add(resource.Id);
                            }

                            if (!first)
                            {
                                // Here we add the previous activity ID to the set of resource
                                // dependencies attached to the activity itself. However, we do
                                // not add it to the compiled dependencies set - instead we make
                                // the change directly to graph data (which we reverse at the start
                                // of each compilation - see the top of this method).

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

                // Collate post-compilation errors, if any exist.

                IEnumerable<IInvalidConstraint<T>> invalidPostcompilationConstraints = m_VertexGraphBuilder.FindInvalidPostCompilationConstraints();

                // C0010
                if (invalidPostcompilationConstraints.Any())
                {
                    compilationErrors.Add(
                        new GraphCompilationError(
                            GraphCompilationErrorCode.C0010,
                            BuildInvalidConstraintsErrorMessage(invalidPostcompilationConstraints)));
                }

                if (compilationErrors.Any())
                {
                    return new GraphCompilation<T, TResourceId, TDependentActivity>(
                        m_VertexGraphBuilder.Activities.Select(x => (TDependentActivity)x.CloneObject()),
                        Enumerable.Empty<IResourceSchedule<T, TResourceId>>(),
                        compilationErrors);
                }

                // Go through each resource schedule and ensure the scheduled activities
                // align with the compiled graph.

                int finishTime = m_VertexGraphBuilder.Duration;
                var newResourceScheduleBuilders = new List<ResourceScheduleBuilder<T, TResourceId>>();

                foreach (IResourceSchedule<T, TResourceId> oldResourceSchedule in resourceSchedules)
                {
                    ResourceScheduleBuilder<T, TResourceId> newResourceScheduleBuilder = oldResourceSchedule.Resource == null ?
                        new ResourceScheduleBuilder<T, TResourceId>() : new ResourceScheduleBuilder<T, TResourceId>(oldResourceSchedule.Resource);

                    IEnumerable<IScheduledActivity<T>> oldScheduledActivities = oldResourceSchedule.ScheduledActivities;

                    foreach (IScheduledActivity<T> oldScheduledActivity in oldScheduledActivities)
                    {
                        T oldScheduledActivityId = oldScheduledActivity.Id;
                        TDependentActivity activity = m_VertexGraphBuilder.Activity(oldScheduledActivityId);

                        // This add needs to be without checks because the alignment may not be perfect.
                        newResourceScheduleBuilder.AppendActivityWithoutChecks(activity, activity.EarliestStartTime.GetValueOrDefault());
                    }

                    newResourceScheduleBuilders.Add(newResourceScheduleBuilder);
                }

                IEnumerable<IResourceSchedule<T, TResourceId>> newResourceSchedules = newResourceScheduleBuilders
                    .Select(x => x.ToResourceSchedule(finishTime))
                    .Where(x => x.ScheduledActivities.Any());

                // Return the final values.

                return new GraphCompilation<T, TResourceId, TDependentActivity>(
                    m_VertexGraphBuilder.Activities.Select(x => (TDependentActivity)x.CloneObject()),
                    newResourceSchedules.ToList());
            }
        }

        #endregion

        #region Private Methods

        private static string BuildMissingDependenciesErrorMessage(
            IEnumerable<T> missingDependencies,
            IEnumerable<TDependentActivity> activities)
        {
            if (missingDependencies == null || !missingDependencies.Any()
                || activities == null || !activities.Any())
            {
                return string.Empty;
            }
            var output = new StringBuilder();
            output.AppendLine($@"{Resources.Message_MissingDependencies}");
            foreach (T missingDependency in missingDependencies)
            {
                IList<T> actsWithMissingDeps = activities
                    .Where(x => x.Dependencies.Contains(missingDependency))
                    .Select(x => x.Id)
                    .ToList();
                output.AppendLine($@"{missingDependency} {Resources.Message_IsMissingFrom} {string.Join(@", ", actsWithMissingDeps)}");
            }
            return output.ToString();
        }

        private static string BuildCircularDependenciesErrorMessage(IEnumerable<ICircularDependency<T>> circularDependencies)
        {
            if (circularDependencies == null || !circularDependencies.Any())
            {
                return string.Empty;
            }
            var output = new StringBuilder();
            output.AppendLine($@"{Resources.Message_CircularDependencies}");
            foreach (ICircularDependency<T> circularDependency in circularDependencies)
            {
                output.AppendLine(string.Join(@" -> ", circularDependency.Dependencies));
            }
            return output.ToString();
        }

        private static string BuildInvalidConstraintsErrorMessage(IEnumerable<IInvalidConstraint<T>> invalidConstraints)
        {
            if (invalidConstraints == null || !invalidConstraints.Any())
            {
                return string.Empty;
            }
            var output = new StringBuilder();
            output.AppendLine($@"{Resources.Message_InvalidConstraints}");
            foreach (IInvalidConstraint<T> invalidConstraint in invalidConstraints)
            {
                output.AppendLine($@"{invalidConstraint.Id} -> {invalidConstraint.Message}");
            }
            return output.ToString();
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

        public override void TransitiveReduction()
        {
            lock (m_Lock)
            {
                bool transitivelyReduced = m_VertexGraphBuilder.TransitiveReduction();
                if (!transitivelyReduced)
                {
                    throw new InvalidOperationException(Properties.Resources.CannotPerformTransitiveReduction);
                }

                // Now set the compiled dependencies to match the actual remaining dependencies.
                foreach (T activityId in m_VertexGraphBuilder.ActivityIds)
                {
                    TDependentActivity activity = m_VertexGraphBuilder.Activity(activityId);
                    IList<T> allDependencyIds = m_VertexGraphBuilder.ActivityDependencyIds(activityId);
                    var remainingCompiledDependencies = new HashSet<T>(activity.Dependencies.Intersect(allDependencyIds));
                    SetActivityDependencies(activityId, remainingCompiledDependencies);
                }
            }
        }

        #endregion
    }
}
