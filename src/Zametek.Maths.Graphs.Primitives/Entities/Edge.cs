using System;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// A directed-graph edge carrying a content payload (an activity in arrow graphs; an event in vertex graphs). Equality is by ID only.
    /// </summary>
    public class Edge<T, TContent>
        : IHaveId<T>, IHaveContent<TContent>, IEquatable<Edge<T, TContent>>, ICloneObject<Edge<T, TContent>>
        where T : struct, IComparable<T>, IEquatable<T>
        where TContent : IHaveId<T>, ICloneObject
    {
        #region Fields

        private const int HashFactorOne = 17;
        private const int HashFactorTwo = 23;

        #endregion

        #region Ctors

        /// <summary>
        /// Creates an edge carrying the given content.
        /// </summary>
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

        /// <inheritdoc/>
        public T Id => Content.Id;

        /// <inheritdoc/>
        public TContent Content
        {
            get;
        }

        #endregion

        #region Overrides

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return Equals(obj as Edge<T, TContent>);
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public bool Equals(Edge<T, TContent>? other)
        {
            if (other is null)
            {
                return false;
            }
            return Id.Equals(other.Id);
        }

        #endregion

        #region ICloneObject

        /// <inheritdoc/>
        public Edge<T, TContent> Clone()
        {
            return (Edge<T, TContent>)CloneObject();
        }

        /// <inheritdoc/>
        public object CloneObject()
        {
            return new Edge<T, TContent>((TContent)Content.CloneObject());
        }

        #endregion
    }
}
