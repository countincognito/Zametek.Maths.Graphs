using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Zametek.Utility;

namespace Zametek.Maths.Graphs
{
    internal static class KeyExtensions
    {
        internal static T Next<T>(this T input)
            where T : struct, IComparable<T>, IEquatable<T>
        {
            object objectifiedInput = input;
            MethodInfo incrementMethod = null;
            var paramInputs = new List<ParameterExpression>();
            objectifiedInput.TypeSwitchOn()
                .Case<int>(x =>
                {
                    incrementMethod = typeof(KeyExtensions).GetMethod(nameof(NextInt));
                    paramInputs.Add(Expression.Parameter(typeof(T), nameof(objectifiedInput)));
                })
                .Case<Guid>(x =>
                {
                    incrementMethod = typeof(KeyExtensions).GetMethod(nameof(NextGuid));
                })
                .Default(x =>
                {
                    throw new InvalidOperationException($@"Type of input ({typeof(T)}) not defined for increment");
                });

            if (paramInputs.Any())
            {
                MethodCallExpression body = Expression.Call(incrementMethod, paramInputs.ToArray());
                Func<T, T> increment = Expression.Lambda<Func<T, T>>(body, paramInputs).Compile();
                return increment((T)objectifiedInput);
            }
            else
            {
                MethodCallExpression body = Expression.Call(incrementMethod);
                Func<T> increment = Expression.Lambda<Func<T>>(body).Compile();
                return increment();
            }
        }

        public static int NextInt(int input)
        {
            return ++input;
        }

        public static Guid NextGuid()
        {
            return Guid.NewGuid();
        }

        internal static T Previous<T>(this T input)
            where T : struct, IComparable<T>, IEquatable<T>
        {
            object objectifiedInput = input;
            MethodInfo decrementMethod = null;
            var paramInputs = new List<ParameterExpression>();
            objectifiedInput.TypeSwitchOn()
                .Case<int>(x =>
                {
                    decrementMethod = typeof(KeyExtensions).GetMethod(nameof(PreviousInt));
                    paramInputs.Add(Expression.Parameter(typeof(T), nameof(objectifiedInput)));
                })
                .Case<Guid>(x =>
                {
                    decrementMethod = typeof(KeyExtensions).GetMethod(nameof(PreviousGuid));
                })
                .Default(x =>
                {
                    throw new InvalidOperationException($@"Type of input ({typeof(T)}) not defined for decrement");
                });

            if (paramInputs.Any())
            {
                MethodCallExpression body = Expression.Call(decrementMethod, paramInputs.ToArray());
                Func<T, T> decrement = Expression.Lambda<Func<T, T>>(body, paramInputs).Compile();
                return decrement((T)objectifiedInput);
            }
            else
            {
                MethodCallExpression body = Expression.Call(decrementMethod);
                Func<T> decrement = Expression.Lambda<Func<T>>(body).Compile();
                return decrement();
            }
        }

        public static int PreviousInt(int input)
        {
            return --input;
        }

        public static Guid PreviousGuid()
        {
            return Guid.NewGuid();
        }
    }
}
