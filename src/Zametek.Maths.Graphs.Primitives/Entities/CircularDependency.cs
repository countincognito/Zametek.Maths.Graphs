using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Maths.Graphs
{
    public class CircularDependency<T>
        : ICircularDependency<T>, IEquatable<CircularDependency<T>>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        #region Fields

        private const int HashFactorOne = 17;
        private const int HashFactorTwo = 23;

        #endregion

        #region Ctors

        public CircularDependency(IEnumerable<T> circularDependencies)
        {
            Dependencies = new HashSet<T>(circularDependencies);
        }

        #endregion

        #region ICircularDependency<T> Members

        public HashSet<T> Dependencies
        {
            get;
        }

        #endregion

        #region Overrides

        public override bool Equals(object obj)
        {
            return Equals(obj as CircularDependency<T>);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = HashFactorOne;
                hash = Dependencies.OrderBy(x => x).Aggregate(hash, (a, b) => a * HashFactorTwo + b.GetHashCode());
                return hash;
            }
        }

        #endregion

        #region IEquatable

        public bool Equals(CircularDependency<T> other)
        {
            if (other is null)
            {
                return false;
            }
            return Dependencies.OrderBy(x => x).SequenceEqual(other.Dependencies.OrderBy(x => x));
        }

        #endregion
    }
}
