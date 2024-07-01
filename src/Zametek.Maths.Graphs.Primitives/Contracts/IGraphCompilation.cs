using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    public interface IGraphCompilation<T, out TResourceId, TWorkStreamId, out TDependentActivity>
        where TDependentActivity : IDependentActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        IEnumerable<IGraphCompilationError> CompilationErrors { get; }

        IEnumerable<TDependentActivity> DependentActivities { get; }

        IEnumerable<IResourceSchedule<T, TResourceId, TWorkStreamId>> ResourceSchedules { get; }

        IEnumerable<IWorkStream<TWorkStreamId>> WorkStreams { get; }
    }
}
