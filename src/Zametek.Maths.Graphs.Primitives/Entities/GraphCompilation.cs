using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    public class GraphCompilation<T, TResourceId, TWorkStreamId, TDependentActivity>
        : IGraphCompilation<T, TResourceId, TWorkStreamId, TDependentActivity>
        where TDependentActivity : IDependentActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        #region Ctors

        public GraphCompilation(
            IEnumerable<TDependentActivity> dependentActivities,
            IEnumerable<IResourceSchedule<T, TResourceId, TWorkStreamId>> resourceSchedules,
            IEnumerable<IWorkStream<TWorkStreamId>> workStreams)
            : this(dependentActivities, resourceSchedules, workStreams, Enumerable.Empty<IGraphCompilationError>())
        {
        }

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

        public IEnumerable<TDependentActivity> DependentActivities
        {
            get;
        }

        public IEnumerable<IResourceSchedule<T, TResourceId, TWorkStreamId>> ResourceSchedules
        {
            get;
        }

        public IEnumerable<IWorkStream<TWorkStreamId>> WorkStreams
        {
            get;
        }

        public IEnumerable<IGraphCompilationError> CompilationErrors
        {
            get;
        }

        #endregion
    }
}
