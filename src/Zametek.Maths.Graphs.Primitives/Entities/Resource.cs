using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    public class Resource<T, TWorkStreamId>
        : IResource<T, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        #region Ctors

        public Resource(
            T id, string name, bool isExplicitTarget, bool isInactive,
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

        public T Id
        {
            get;
        }

        public string Name
        {
            get;
        }

        public bool IsExplicitTarget
        {
            get;
        }

        public bool IsInactive
        {
            get;
        }

        public InterActivityAllocationType InterActivityAllocationType
        {
            get;
        }

        public double UnitCost
        {
            get;
        }

        public double UnitBilling
        {
            get;
        }

        public int AllocationOrder
        {
            get;
        }

        public HashSet<TWorkStreamId> InterActivityPhases
        {
            get;
        }

        public object CloneObject()
        {
            return new Resource<T, TWorkStreamId>(
                Id, Name, IsExplicitTarget, IsInactive, InterActivityAllocationType,
                UnitCost, UnitBilling, AllocationOrder, InterActivityPhases);
        }

        #endregion
    }
}
