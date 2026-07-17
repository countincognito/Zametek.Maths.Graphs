using System;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// Default implementation of <see cref="IInvalidConstraint{T}"/>.
    /// </summary>
    public class InvalidConstraint<T>
        : IInvalidConstraint<T>, IEquatable<InvalidConstraint<T>>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        #region Fields

        private const int HashFactorOne = 17;
        private const int HashFactorTwo = 23;

        #endregion

        #region Ctors

        /// <summary>
        /// Creates a constraint violation for the given activity ID.
        /// </summary>
        public InvalidConstraint(T id, string message)
        {
            Id = id;
            Message = message;
        }

        #endregion

        #region IInvalidConstraint<T> Members

        /// <inheritdoc/>
        public T Id
        {
            get;
        }

        /// <inheritdoc/>
        public string Message
        {
            get;
        }

        #endregion

        #region Overrides

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return Equals(obj as InvalidConstraint<T>);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = HashFactorOne;
                hash = hash * HashFactorTwo + Id.GetHashCode();
                hash = hash * HashFactorTwo + Message.GetHashCode();
                return hash;
            }
        }

        #endregion

        #region IEquatable

        /// <inheritdoc/>
        public bool Equals(InvalidConstraint<T>? other)
        {
            if (other is null)
            {
                return false;
            }

            return Id.Equals(other.Id)
                && string.Equals(Message, other.Message, StringComparison.OrdinalIgnoreCase);
        }

        #endregion
    }
}
