using System;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// Default implementation of <see cref="IWorkStream{T}"/>.
    /// </summary>
    public class WorkStream<T>
        : IWorkStream<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        #region Ctors

        /// <summary>
        /// Creates a work stream with the given ID, name and phase flag.
        /// </summary>
        public WorkStream(T id, string name, bool isPhase)
        {
            Id = id;
            Name = name;
            IsPhase = isPhase;
        }

        #endregion

        #region IWorkStream<T> Members

        /// <inheritdoc/>
        public T Id
        {
            get;
        }

        /// <inheritdoc/>
        public string Name
        {
            get;
        }

        /// <inheritdoc/>
        public bool IsPhase
        {
            get;
        }

        /// <inheritdoc/>
        public IWorkStream<T> Clone()
        {
            return (IWorkStream<T>)CloneObject();
        }

        /// <inheritdoc/>
        public object CloneObject()
        {
            return new WorkStream<T>(Id, Name, IsPhase);
        }

        #endregion
    }
}
