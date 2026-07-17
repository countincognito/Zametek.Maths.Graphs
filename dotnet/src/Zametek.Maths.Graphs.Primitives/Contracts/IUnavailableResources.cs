using System;
using System.Collections.Generic;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// Records that an activity requires target resources that are not present
    /// in the supplied resource list.
    /// </summary>
    /// <typeparam name="T">The activity ID type.</typeparam>
    /// <typeparam name="TResourceId">The resource ID type.</typeparam>
    public interface IUnavailableResources<T, TResourceId>
        : IHaveId<T>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
    {
        /// <summary>
        /// The IDs of the required resources that are unavailable.
        /// </summary>
        HashSet<TResourceId> ResourceIds { get; }
    }
}
