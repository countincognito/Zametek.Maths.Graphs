using System;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// Makes the builders and the compilers of Activity-on-Arrow graphs. It is the counterpart of
    /// <see cref="IVertexGraphFactory{T, TResourceId, TWorkStreamId, TActivity}"/>, for the arrow graphs that
    /// are drawn. <see cref="ArrowGraphFactory{T, TResourceId, TWorkStreamId, TActivity}"/> is the standard
    /// implementation.
    /// </summary>
    /// <remarks>
    /// A builder holds the state of one graph, and so builders cannot be shared: each call to a method of the
    /// factory gives a new object. The engines of a builder are not the same. They hold no state, and a factory
    /// can give the same engines to all the builders that it makes. An implementation must be safe for use by
    /// many threads, because its caller can call it from any thread.
    /// </remarks>
    /// <typeparam name="T">The activity/event ID type.</typeparam>
    /// <typeparam name="TResourceId">The resource ID type.</typeparam>
    /// <typeparam name="TWorkStreamId">The work-stream ID type.</typeparam>
    /// <typeparam name="TActivity">The activity type.</typeparam>
    public interface IArrowGraphFactory<T, TResourceId, TWorkStreamId, TActivity>
        where TActivity : class, IDependentActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        /// <summary>
        /// Makes a new, empty builder. It shares no graph and no state with any builder that the factory made
        /// before.
        /// </summary>
        ArrowGraphBuilder<T, TResourceId, TWorkStreamId, TActivity> CreateBuilder();

        /// <summary>
        /// Makes a new compiler, around a new, empty builder of its own (not one that
        /// <see cref="CreateBuilder"/> gave). The builder of the compiler has the same engines as the builders
        /// of the factory.
        /// </summary>
        ArrowGraphCompiler<T, TResourceId, TWorkStreamId, TActivity> CreateCompiler();
    }
}
