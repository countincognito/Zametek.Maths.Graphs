using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// Default implementation of <see cref="IGraphCompilation{T, TResourceId, TWorkStreamId, TDependentActivity}"/>.
    /// </summary>
    public class GraphCompilation<T, TResourceId, TWorkStreamId, TDependentActivity>
        : IGraphCompilation<T, TResourceId, TWorkStreamId, TDependentActivity>
        where TDependentActivity : IDependentActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        #region Ctors

        /// <summary>
        /// Creates a compilation result with no errors.
        /// </summary>
        public GraphCompilation(
            IEnumerable<TDependentActivity> dependentActivities,
            IEnumerable<IResourceSchedule<T, TResourceId, TWorkStreamId>> resourceSchedules,
            IEnumerable<IWorkStream<TWorkStreamId>> workStreams)
            : this(dependentActivities, resourceSchedules, workStreams, Enumerable.Empty<IGraphCompilationError>())
        {
        }

        /// <summary>
        /// Creates a compilation result including compilation errors.
        /// </summary>
        public GraphCompilation(
            IEnumerable<TDependentActivity> dependentActivities,
            IEnumerable<IResourceSchedule<T, TResourceId, TWorkStreamId>> resourceSchedules,
            IEnumerable<IWorkStream<TWorkStreamId>> workStreams,
            IEnumerable<IGraphCompilationError> compilationErrors)
        {
            DependentActivities = dependentActivities.ToList();
            ResourceSchedules = resourceSchedules.ToList();
            WorkStreams = workStreams.ToList();
            CompilationErrors = compilationErrors.ToList();
        }

        #endregion

        #region GraphCompilation<T> Members

        /// <inheritdoc/>
        public IEnumerable<TDependentActivity> DependentActivities
        {
            get;
        }

        /// <inheritdoc/>
        public IEnumerable<IResourceSchedule<T, TResourceId, TWorkStreamId>> ResourceSchedules
        {
            get;
        }

        /// <inheritdoc/>
        public IEnumerable<IWorkStream<TWorkStreamId>> WorkStreams
        {
            get;
        }

        /// <inheritdoc/>
        public IEnumerable<IGraphCompilationError> CompilationErrors
        {
            get;
        }

        #endregion
    }
}
