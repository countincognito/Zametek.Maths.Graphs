using System;

namespace Zametek.Maths.Graphs
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "No better term available")]
    public class Event<T>
        : IEvent<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        #region Ctors

        public Event(T id)
            : this(id, null, null)
        {
        }

        public Event(T id, int? earliestFinishTime, int? latestFinishTime)
        {
            Id = id;
            EarliestFinishTime = earliestFinishTime;
            LatestFinishTime = latestFinishTime;
        }

        #endregion

        #region IEvent<T> Members

        public T Id
        {
            get;
        }

        public int? EarliestFinishTime
        {
            get;
            set;
        }

        public int? LatestFinishTime
        {
            get;
            set;
        }

        public bool CanBeRemoved
        {
            get;
            private set;
        }

        public void SetAsReadOnly()
        {
            CanBeRemoved = false;
        }

        public void SetAsRemovable()
        {
            CanBeRemoved = true;
        }

        public object CloneObject()
        {
            return new Event<T>(Id, EarliestFinishTime, LatestFinishTime);
        }

        #endregion
    }
}
