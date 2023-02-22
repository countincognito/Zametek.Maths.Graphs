using System;

namespace Zametek.Maths.Graphs
{
    public class Resource<T>
        : IResource<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        #region Ctors

        public Resource(
            T id, string name, bool isExplicitTarget, bool isInactive,
            InterActivityAllocationType interActivityAllocationType,
            double unitCost, int allocationOrder)
        {
            Id = id;
            Name = name;
            IsExplicitTarget = isExplicitTarget;
            IsInactive = isInactive;
            InterActivityAllocationType = interActivityAllocationType;
            UnitCost = unitCost;
            AllocationOrder = allocationOrder;
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

        public int AllocationOrder
        {
            get;
        }

        public object CloneObject()
        {
            return new Resource<T>(Id, Name, IsExplicitTarget, IsInactive, InterActivityAllocationType, UnitCost, AllocationOrder);
        }

        #endregion
    }
}
