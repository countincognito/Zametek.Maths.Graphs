using System;
using System.Globalization;

namespace Zametek.Maths.Graphs
{
    internal static class KeyExtensions
    {
        internal static T Next<T>(this T input)
            where T : struct, IComparable<T>, IEquatable<T>
        {
            if (typeof(T) == typeof(int))
            {
                return (T)(object)((int)(object)input + 1);
            }
            if (typeof(T) == typeof(Guid))
            {
                return (T)(object)Guid.NewGuid();
            }
            throw new InvalidOperationException(string.Format(
                CultureInfo.CurrentCulture,
                Properties.Resources.Message_TypeNotSupportedForKeyGeneration,
                typeof(T)));
        }

        internal static T Previous<T>(this T input)
            where T : struct, IComparable<T>, IEquatable<T>
        {
            if (typeof(T) == typeof(int))
            {
                return (T)(object)((int)(object)input - 1);
            }
            if (typeof(T) == typeof(Guid))
            {
                throw new InvalidOperationException(Properties.Resources.Message_GuidKeysDoNotSupportPrevious);
            }
            throw new InvalidOperationException(string.Format(
                CultureInfo.CurrentCulture,
                Properties.Resources.Message_TypeNotSupportedForKeyGeneration,
                typeof(T)));
        }
    }
}
