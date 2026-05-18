using System;

namespace Zametek.Maths.Graphs
{
    internal static class KeyExtensions
    {
        internal static T Next<T>(this T input)
            where T : struct, IComparable<T>, IEquatable<T>
        {
            if (typeof(T) == typeof(int))
                return (T)(object)((int)(object)input + 1);
            if (typeof(T) == typeof(Guid))
                return (T)(object)Guid.NewGuid();
            throw new InvalidOperationException($"Type {typeof(T)} is not supported for key generation.");
        }

        internal static T Previous<T>(this T input)
            where T : struct, IComparable<T>, IEquatable<T>
        {
            if (typeof(T) == typeof(int))
                return (T)(object)((int)(object)input - 1);
            if (typeof(T) == typeof(Guid))
                throw new InvalidOperationException("Guid keys do not support Previous.");
            throw new InvalidOperationException($"Type {typeof(T)} is not supported for key generation.");
        }
    }
}
