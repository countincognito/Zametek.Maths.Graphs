using System;

namespace Zametek.Maths.Graphs
{
    public class Resource<T>
        : IResource<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        #region Ctors

        public Resource(
            T id, string name, bool isExplicitTarget,
            InterActivityAllocationType interActivityAllocationType,
            double unitCost, int displayOrder)
        {
            Id = id;
            Name = name;
            IsExplicitTarget = isExplicitTarget;
            InterActivityAllocationType = interActivityAllocationType;
            UnitCost = unitCost;
            DisplayOrder = displayOrder;
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

        public InterActivityAllocationType InterActivityAllocationType
        {
            get;
        }

        public double UnitCost
        {
            get;
        }

        public int DisplayOrder
        {
            get;
        }

        public object CloneObject()
        {
            return new Resource<T>(Id, Name, IsExplicitTarget, InterActivityAllocationType, UnitCost, DisplayOrder);
        }

        #endregion
    }
}
