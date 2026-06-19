using System;

namespace Zametek.Maths.Graphs
{
    public interface IEventGenerator<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        IEvent<T> Generate(T id);

        IEvent<T> Generate(T id, int? earliestFinishTime, int? latestFinishTime);
    }
}
