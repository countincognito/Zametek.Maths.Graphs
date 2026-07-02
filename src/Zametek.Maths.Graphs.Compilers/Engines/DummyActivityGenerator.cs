using System;

namespace Zametek.Maths.Graphs
{
    public class DummyActivityGenerator<T, TResourceId, TWorkStreamId, TActivity>
        : IActivityGenerator<T, TResourceId, TWorkStreamId, TActivity>
        where TActivity : class, IActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        public TActivity Generate(T id)
        {
            return new DependentActivity<T, TResourceId, TWorkStreamId>(id, 0, canBeRemoved: true) as TActivity
                ?? throw new InvalidOperationException($@"Unable to create a dummy activity assignable to type {typeof(TActivity).FullName}");
        }
    }
}
