using System;

namespace Zametek.Maths.Graphs
{
    public class WorkStream<T>
        : IWorkStream<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        #region Ctors

        public WorkStream(T id, string name, bool isPhase)
        {
            Id = id;
            Name = name;
            IsPhase = isPhase;
        }

        #endregion

        #region IWorkStream<T> Members

        public T Id
        {
            get;
        }

        public string Name
        {
            get;
        }

        public bool IsPhase
        {
            get;
        }

        public object CloneObject()
        {
            return new WorkStream<T>(Id, Name, IsPhase);
        }

        #endregion
    }
}
