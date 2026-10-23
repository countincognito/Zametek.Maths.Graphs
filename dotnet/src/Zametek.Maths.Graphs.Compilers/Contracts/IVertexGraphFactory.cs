using System;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// Makes the builders and the compilers of Activity-on-Vertex graphs. Code that needs more than one
    /// builder (for example one for each thread), or a compiler that agrees with its builders, takes one of
    /// these, in place of a delegate that makes them.
    /// <see cref="VertexGraphFactory{T, TResourceId, TWorkStreamId, TActivity}"/> is the standard
    /// implementation, and <see cref="ScheduleSimulator{T, TResourceId, TWorkStreamId, TActivity}"/> takes one.
    /// </summary>
    /// <remarks>
    /// A builder holds the state of one graph, and so builders cannot be shared: each call to a method of the
    /// factory gives a new object. The engines of a builder are not the same. They hold no state, and a factory
    /// can give the same engines to all the builders that it makes. An implementation must be safe for use by
    /// many threads, because its caller can call it from any thread.
    /// </remarks>
    /// <typeparam name="T">The activity ID type.</typeparam>
    /// <typeparam name="TResourceId">The resource ID type.</typeparam>
    /// <typeparam name="TWorkStreamId">The work-stream ID type.</typeparam>
    /// <typeparam name="TActivity">The activity type.</typeparam>
    public interface IVertexGraphFactory<T, TResourceId, TWorkStreamId, TActivity>
        where TActivity : IDependentActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        /// <summary>
        /// Makes a new, empty builder. It shares no graph and no state with any builder that the factory made
        /// before.
        /// </summary>
        VertexGraphBuilder<T, TResourceId, TWorkStreamId, TActivity> CreateBuilder();

        /// <summary>
        /// Makes a new compiler, around a new, empty builder of its own (not one that
        /// <see cref="CreateBuilder"/> gave). The builder of the compiler has the same engines as the builders
        /// of the factory, so that a plan compiles in the same way as it is scheduled by them.
        /// </summary>
        VertexGraphCompiler<T, TResourceId, TWorkStreamId, TActivity> CreateCompiler();
    }
}
