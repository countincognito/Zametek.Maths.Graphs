using System;

namespace Zametek.Maths.Graphs
{
    public interface IEvent<out T>
        : IHaveId<T>, ICanBeRemoved, ICloneObject
        where T : IComparable<T>, IEquatable<T>
    {
        int? EarliestFinishTime { get; set; }

        int? LatestFinishTime { get; set; }
    }
}
