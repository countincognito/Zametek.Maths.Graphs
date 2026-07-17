using System;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// Default implementation of <see cref="IEvent{T}"/>.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "No better term available")]
    public class Event<T>
        : IEvent<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        #region Ctors

        /// <summary>
        /// Creates an event with the given ID.
        /// </summary>
        public Event(T id)
            : this(id, null, null)
        {
        }

        /// <summary>
        /// Creates an event with the given ID and finish times.
        /// </summary>
        public Event(T id, int? earliestFinishTime, int? latestFinishTime)
        {
            Id = id;
            EarliestFinishTime = earliestFinishTime;
            LatestFinishTime = latestFinishTime;
        }

        #endregion

        #region IEvent<T> Members

        /// <inheritdoc/>
        public T Id
        {
            get;
        }

        /// <inheritdoc/>
        public int? EarliestFinishTime
        {
            get;
            set;
        }

        /// <inheritdoc/>
        public int? LatestFinishTime
        {
            get;
            set;
        }

        /// <inheritdoc/>
        public bool CanBeRemoved
        {
            get;
            private set;
        }

        /// <inheritdoc/>
        public void SetAsReadOnly()
        {
            CanBeRemoved = false;
        }

        /// <inheritdoc/>
        public void SetAsRemovable()
        {
            CanBeRemoved = true;
        }

        /// <inheritdoc/>
        public virtual object CloneObject()
        {
            return new Event<T>(Id, EarliestFinishTime, LatestFinishTime)
            {
                CanBeRemoved = CanBeRemoved,
            };
        }

        #endregion
    }
}
