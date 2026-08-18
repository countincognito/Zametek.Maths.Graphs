using System;
using System.Globalization;

namespace Zametek.Maths.Graphs
{
    /// <summary>
    /// Default dummy-activity generator: creates removable zero-duration <see cref="DependentActivity{T, TResourceId, TWorkStreamId}"/> instances.
    /// </summary>
    public class DummyActivityGenerator<T, TResourceId, TWorkStreamId, TActivity>
        : IActivityGenerator<T, TResourceId, TWorkStreamId, TActivity>
        where TActivity : class, IActivity<T, TResourceId, TWorkStreamId>
        where T : struct, IComparable<T>, IEquatable<T>
        where TResourceId : struct, IComparable<TResourceId>, IEquatable<TResourceId>
        where TWorkStreamId : struct, IComparable<TWorkStreamId>, IEquatable<TWorkStreamId>
    {
        /// <inheritdoc/>
        public TActivity Generate(T id)
        {
            return new DependentActivity<T, TResourceId, TWorkStreamId>(id, 0, canBeRemoved: true) as TActivity
                ?? throw new InvalidOperationException(string.Format(
                    CultureInfo.CurrentCulture,
                    Properties.Resources.Message_UnableToCreateDummyActivityAssignableToType,
                    typeof(TActivity).FullName));
        }
    }
}
