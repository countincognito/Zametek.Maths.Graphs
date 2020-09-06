using System;

namespace Zametek.Maths.Graphs
{
    public class Edge<T, TContent>
        : IHaveId<T>, IHaveContent<TContent>, IEquatable<Edge<T, TContent>>, ICloneObject
        where T : IComparable<T>, IEquatable<T>
        where TContent : IHaveId<T>, ICloneObject
    {
        #region Fields

        private const int HashFactorOne = 17;
        private const int HashFactorTwo = 23;

        #endregion

        #region Ctors

        public Edge(TContent content)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }
            Content = content;
        }

        #endregion

        #region Properties

        public T Id => Content.Id;

        public TContent Content
        {
            get;
        }

        #endregion

        #region Overrides

        public override bool Equals(object obj)
        {
            return Equals(obj as Edge<T, TContent>);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = HashFactorOne;
                hash = hash * HashFactorTwo + Id.GetHashCode();
                return hash;
            }
        }

        #endregion

        #region IEquatable

        public bool Equals(Edge<T, TContent> other)
        {
            if (other is null)
            {
                return false;
            }
            return Id.Equals(other.Id);
        }

        #endregion

        #region ICloneObject

        public object CloneObject()
        {
            return new Edge<T, TContent>((TContent)Content.CloneObject());
        }

        #endregion
    }
}
