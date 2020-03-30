using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    public interface IGraphCompilation<T, out TResourceId, out TDependentActivity>
        where TDependentActivity : IDependentActivity<T, TResourceId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
    {
        IGraphCompilationErrors<T> Errors { get; }

        IEnumerable<TDependentActivity> DependentActivities { get; }

        IEnumerable<IResourceSchedule<T, TResourceId>> ResourceSchedules { get; }
    }
}
