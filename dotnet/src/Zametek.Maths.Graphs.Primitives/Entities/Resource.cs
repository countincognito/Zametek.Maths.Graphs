using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// Default implementation of <see cref="IResource{T, TWorkStreamId}"/>.
    /// </summary>
    public class Resource<T, TWorkStreamId>
        : IResource<T, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        #region Ctors

        /// <summary>
        /// Creates a resource with the given properties.
        /// </summary>
        public Resource(
            T id, string? name, bool isExplicitTarget, bool isInactive,
            InterActivityAllocationType interActivityAllocationType,
            double unitCost, double unitBilling, int allocationOrder,
            IEnumerable<TWorkStreamId> interActivityPhases)
        {
            if (interActivityPhases is null)
            {
                throw new ArgumentNullException(nameof(interActivityPhases));
            }
            Id = id;
            Name = name;
            IsExplicitTarget = isExplicitTarget;
            IsInactive = isInactive;
            InterActivityAllocationType = interActivityAllocationType;
            UnitCost = unitCost;
            UnitBilling = unitBilling;
            AllocationOrder = allocationOrder;
            InterActivityPhases = new HashSet<TWorkStreamId>(interActivityPhases);
        }

        #endregion

        #region IResource<T> Members

        /// <inheritdoc/>
        public T Id
        {
            get;
        }

        /// <inheritdoc/>
        public string? Name
        {
            get;
        }

        /// <inheritdoc/>
        public bool IsExplicitTarget
        {
            get;
        }

        /// <inheritdoc/>
        public bool IsInactive
        {
            get;
        }

        /// <inheritdoc/>
        public InterActivityAllocationType InterActivityAllocationType
        {
            get;
        }

        /// <inheritdoc/>
        public double UnitCost
        {
            get;
        }

        /// <inheritdoc/>
        public double UnitBilling
        {
            get;
        }

        /// <inheritdoc/>
        public int AllocationOrder
        {
            get;
        }

        /// <inheritdoc/>
        public HashSet<TWorkStreamId> InterActivityPhases
        {
            get;
        }

        /// <inheritdoc/>
        public virtual object CloneObject()
        {
            return new Resource<T, TWorkStreamId>(
                Id, Name, IsExplicitTarget, IsInactive, InterActivityAllocationType,
                UnitCost, UnitBilling, AllocationOrder, InterActivityPhases);
        }

        #endregion
    }
}
