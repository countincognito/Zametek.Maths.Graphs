using System;

namespace Zametek.Maths.Graphs
{
    public interface IResource<T>
        : IHaveId<T>, IWorkingCopy
        where T : struct, IComparable<T>, IEquatable<T>
    {
        string Name
        {
            get;
        }

        bool IsExplicitTarget
        {
            get;
        }

        InterActivityAllocationType InterActivityAllocationType
        {
            get;
        }

        double UnitCost
        {
            get;
        }

        int DisplayOrder
        {
            get;
        }
    }
}
