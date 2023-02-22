using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    public interface IUnavailableResources<T, TResourceId>
        : IHaveId<T>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
    {
        HashSet<TResourceId> ResourceIds { get; }
    }
}
