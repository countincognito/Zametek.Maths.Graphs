using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// Default implementation of <see cref="IUnavailableResources{T, TResourceId}"/>.
    /// </summary>
    public class UnavailableResources<T, TResourceId>
        : IUnavailableResources<T, TResourceId>, IEquatable<UnavailableResources<T, TResourceId>>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
    {
        #region Fields

        private const int HashFactorOne = 17;
        private const int HashFactorTwo = 23;

        #endregion

        #region Ctors

        /// <summary>
        /// Records the given unavailable resource IDs for an activity.
        /// </summary>
        public UnavailableResources(T id, IEnumerable<TResourceId> unavailableResourceIds)
        {
            Id = id;
            ResourceIds = new HashSet<TResourceId>(unavailableResourceIds);
        }

        #endregion

        #region IUnavailableResources<T, TResourceId> Members

        /// <inheritdoc/>
        public T Id
        {
            get;
        }

        /// <inheritdoc/>
        public HashSet<TResourceId> ResourceIds
        {
            get;
        }

        #endregion

        #region Overrides

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return Equals(obj as UnavailableResources<T, TResourceId>);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = HashFactorOne;
                hash = hash * HashFactorTwo + Id.GetHashCode();
                hash = ResourceIds.OrderBy(x => x).Aggregate(hash, (a, b) => a * HashFactorTwo + b.GetHashCode());
                return hash;
            }
        }

        #endregion

        #region IEquatable

        /// <inheritdoc/>
        public bool Equals(UnavailableResources<T, TResourceId>? other)
        {
            if (other is null)
            {
                return false;
            }

            return Id.Equals(other.Id)
                && ResourceIds.OrderBy(x => x).SequenceEqual(other.ResourceIds.OrderBy(x => x));
        }

        #endregion
    }
}
