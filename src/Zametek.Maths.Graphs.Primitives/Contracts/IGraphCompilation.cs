using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// The result of compiling a set of dependent activities: the scheduled
    /// activities, the per-resource timelines, the work streams used, and any
    /// errors found. Check <see cref="CompilationErrors"/> before trusting the
    /// schedule.
    /// </summary>
    /// <typeparam name="T">The activity ID type.</typeparam>
    /// <typeparam name="TResourceId">The resource ID type.</typeparam>
    /// <typeparam name="TWorkStreamId">The work-stream ID type.</typeparam>
    /// <typeparam name="TDependentActivity">The activity type.</typeparam>
    public interface IGraphCompilation<T, out TResourceId, TWorkStreamId, out TDependentActivity>
        where TDependentActivity : IDependentActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        /// <summary>
        /// The problems found during compilation; empty when the schedule is valid.
        /// </summary>
        IEnumerable<IGraphCompilationError> CompilationErrors { get; }

        /// <summary>
        /// The compiled activities, each populated with its computed start/finish
        /// times and slack.
        /// </summary>
        IEnumerable<TDependentActivity> DependentActivities { get; }

        /// <summary>
        /// The per-resource timelines produced by resource scheduling.
        /// </summary>
        IEnumerable<IResourceSchedule<T, TResourceId, TWorkStreamId>> ResourceSchedules { get; }

        /// <summary>
        /// The work streams actually used by the compiled schedule.
        /// </summary>
        IEnumerable<IWorkStream<TWorkStreamId>> WorkStreams { get; }
    }
}
