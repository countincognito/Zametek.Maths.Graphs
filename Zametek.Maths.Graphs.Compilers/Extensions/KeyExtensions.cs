using System;
using System.Linq.Expressions;
using System.Reflection;
using Zametek.Utility;

namespace Zametek.Maths.Graphs
{
    public static class KeyExtensions
    {
        public static T Next<T>(this T input)
            where T : struct, IComparable<T>, IEquatable<T>
        {
            object objectifiedInput = input;
            MethodInfo incrementMethod = null;
            objectifiedInput.TypeSwitchOn()
                .Case<int>(x => incrementMethod = typeof(KeyExtensions).GetMethod(nameof(NextInt)))
                .Case<Guid>(x => incrementMethod = typeof(KeyExtensions).GetMethod(nameof(NextGuid)))
                .Default(x =>
                {
                    throw new InvalidOperationException($@"Type of input ({typeof(T)}) not defined for increment");
                });
            ParameterExpression paramInput = Expression.Parameter(typeof(T), nameof(objectifiedInput));
            UnaryExpression body = Expression.Increment(paramInput, incrementMethod);
            Func<T, T> increment = Expression.Lambda<Func<T, T>>(body, paramInput).Compile();
            return increment((T)objectifiedInput);
        }

        public static int NextInt(int input)
        {
            return ++input;
        }

        public static Guid NextGuid(Guid input)
        {
            return Guid.NewGuid();
        }

        public static T Previous<T>(this T input)
            where T : struct, IComparable<T>, IEquatable<T>
        {
            object objectifiedInput = input;
            MethodInfo decrementMethod = null;
            objectifiedInput.TypeSwitchOn()
                .Case<int>(x => decrementMethod = typeof(KeyExtensions).GetMethod(nameof(PreviousInt)))
                .Case<Guid>(x => decrementMethod = typeof(KeyExtensions).GetMethod(nameof(PreviousGuid)))
                .Default(x =>
                {
                    throw new InvalidOperationException($@"Type of input ({typeof(T)}) not defined for decrement");
                });
            ParameterExpression paramInput = Expression.Parameter(typeof(T), nameof(objectifiedInput));
            UnaryExpression body = Expression.Decrement(paramInput, decrementMethod);
            Func<T, T> decrement = Expression.Lambda<Func<T, T>>(body, paramInput).Compile();
            return decrement((T)objectifiedInput);
        }

        public static int PreviousInt(int input)
        {
            return --input;
        }

        public static Guid PreviousGuid(Guid input)
        {
            return Guid.NewGuid();
        }
    }
}
