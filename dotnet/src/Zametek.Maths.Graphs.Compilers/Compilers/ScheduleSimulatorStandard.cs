using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// A Monte Carlo simulation of a schedule, with the standard engines of the library. The activities are
    /// <see cref="IDependentActivity{T, TResourceId, TWorkStreamId}"/>, and the plan is scheduled in the way that a
    /// <see cref="VertexGraphCompiler{T, TResourceId, TWorkStreamId, TDependentActivity}"/> that is made with no builder
    /// schedules it: the factory of this class is a <see cref="VertexGraphFactory{T, TResourceId, TWorkStreamId, TActivity}"/>
    /// with the standard engines. Use <see cref="ScheduleSimulator{T, TResourceId, TWorkStreamId, TActivity}"/> to simulate a
    /// plan with a factory that has other engines, or with another type of activity. See that class for how a simulation works.
    /// </summary>
    /// <typeparam name="T">The activity ID type.</typeparam>
    /// <typeparam name="TResourceId">The resource ID type.</typeparam>
    /// <typeparam name="TWorkStreamId">The work-stream ID type.</typeparam>
    public sealed class ScheduleSimulator<T, TResourceId, TWorkStreamId>
        : ScheduleSimulator<T, TResourceId, TWorkStreamId, IDependentActivity<T, TResourceId, TWorkStreamId>>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        /// <summary>
        /// Prepares a simulation of a plan, with the standard engines. See
        /// <see cref="ScheduleSimulator{T, TResourceId, TWorkStreamId, TActivity}"/> for the arguments.
        /// </summary>
        /// <param name="activities">The activities of the plan. The order is part of the contract: it is the order in which a simulation draws the durations, and the order of the columns of the result.</param>
        /// <param name="resources">The resources of the plan. An empty list is the same as in a compile: infinite resources.</param>
        /// <param name="workStreams">The work streams of the plan.</param>
        /// <param name="estimates">The estimates of the activities that have them, by the ID of the activity.</param>
        /// <exception cref="ArgumentException">An estimate belongs to an activity that is not in the plan, or two activities have the same ID.</exception>
        public ScheduleSimulator(
            IEnumerable<IDependentActivity<T, TResourceId, TWorkStreamId>> activities,
            IEnumerable<IResource<TResourceId, TWorkStreamId>> resources,
            IEnumerable<IWorkStream<TWorkStreamId>> workStreams,
            IReadOnlyDictionary<T, DurationEstimate> estimates)
            : base(
                  activities,
                  resources,
                  workStreams,
                  estimates,
                  new VertexGraphFactory<T, TResourceId, TWorkStreamId, IDependentActivity<T, TResourceId, TWorkStreamId>>())
        {
        }
    }
}
