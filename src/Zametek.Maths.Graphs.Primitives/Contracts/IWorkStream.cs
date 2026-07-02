using System;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// A grouping of work within a project - optionally a sequential phase.
    /// Activities declare their work streams via <see cref="IActivity{T, TResourceId, TWorkStreamId}.TargetWorkStreams"/>.
    /// </summary>
    /// <typeparam name="T">The work-stream ID type.</typeparam>
    public interface IWorkStream<out T>
        : IHaveId<T>, ICloneObject<IWorkStream<T>>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        /// <summary>
        /// The display name of the work stream.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Whether the work stream represents a sequential phase of the project.
        /// </summary>
        bool IsPhase { get; }
    }
}
