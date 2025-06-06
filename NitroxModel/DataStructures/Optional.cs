using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace NitroxModel.DataStructures.Util
{
    /// <summary>
    ///     Used to give context on whether the wrapped value is nullable and to improve error logging.
    /// </summary>
    /// <remarks>
    ///     Used some hacks to circumvent C#' lack of reverse type inference (usually need to specify the type when returning a
    ///     value using a generic method).
    ///     Code from https://tyrrrz.me/blog/return-type-inference
    /// </remarks>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    [DataContract]
    public struct Optional<T> : ISerializable, IEquatable<Optional<T>> where T : class
    {
        private delegate bool HasValueDelegate(T value);

        /// <summary>
        ///     List of <see cref="HasValue" /> condition checks for current type (due to being a static on generic class).
        /// </summary>
        private static List<Func<object, bool>> valueChecks;

        /// <summary>
        ///     Has value check that can be replaced and defaults to generating a value check for current <see cref="T" /> based on
        ///     global filter conditions that were set.
        /// </summary>
        private static HasValueDelegate valueChecksForT = value =>
        {
            // Generate new HasValue check based on global filters for types.
            Type type = typeof(T);
            bool isObj = type == typeof(object);
            foreach (KeyValuePair<Type, Func<object, bool>> filter in Optional.ValueConditions)
            {
                if (isObj || filter.Key.IsAssignableFrom(type))
                {
                    // Only create the list in memory when required.
                    valueChecks ??= new List<Func<object, bool>>();

                    // Exclude check for Optional<object> if the type doesn't match the type of the filter (because it'll always be null for `o as T`)
                    valueChecks.Add(isObj ? o => !filter.Key.IsInstanceOfType(o) || filter.Value(o) : filter.Value);
                }
            }

            // Update check to just check has values directly for future calls (this is an optimization).
            if (valueChecks != null)
            {
                valueChecksForT = val =>
                {
                    if (ReferenceEquals(val, null))
                    {
                        return false;
                    }
                    foreach (Func<object, bool> check in valueChecks)
                    {
                        if (!check(val))
                        {
                            return false;
                        }
                    }
                    return true;
                };
            }
            else
            {
                valueChecksForT = val => !ReferenceEquals(val, null);
            }

            // Give initial result based on the updated check delegate
            return valueChecksForT(value);
        };

        [DataMember(Order = 1)]
        public T Value { get; private set; }

        public bool HasValue => valueChecksForT(Value);

        private Optional(T value)
        {
            Value = value;
        }

        public T OrElse(T elseValue)
        {
            return HasValue ? Value : elseValue;
        }

        public Optional<T> OrElse(Func<T> elseValue) => HasValue ? Value : elseValue();

        public T OrNull() => HasValue ? Value : null;

        internal static Optional<T> Of(T value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value), $"Tried to set null on {typeof(Optional<T>)}");
            }

            return new Optional<T>(value);
        }

        internal static Optional<T> OfNullable(T value)
        {
            return !valueChecksForT(value) ? Optional.Empty : new Optional<T>(value);
        }

        public override string ToString()
        {
            string str = Value != null ? Value.ToString() : "Nothing";
            return $"Optional Contains: {str}";
        }

        private Optional(SerializationInfo info, StreamingContext context)
        {
            Value = (T)info.GetValue("value", typeof(T));
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("value", Value);
        }

#pragma warning disable CS0618 // OptionalEmpty is only allowed to be used internally
        public static implicit operator Optional<T>(OptionalEmpty none)
        {
            return new Optional<T>();
        }
#pragma warning restore CS0618

        public static implicit operator Optional<T>?(T obj)
        {
            if (obj == null)
            {
                return null;
            }
            return new Optional<T>(obj);
        }

        public static implicit operator Optional<T>(T obj)
        {
            return Optional.Of(obj);
        }

        public static explicit operator T(Optional<T> value)
        {
            return value.Value;
        }
        public bool Equals(Optional<T> other)
        {
            return EqualityComparer<T>.Default.Equals(Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is Optional<T> other && Equals(other);
        }

        public override int GetHashCode()
        {
            return EqualityComparer<T>.Default.GetHashCode(Value);
        }

        public static bool operator ==(Optional<T> left, Optional<T> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Optional<T> left, Optional<T> right)
        {
            return !left.Equals(right);
        }
    }


    [Obsolete("Use Optional.Empty instead. This struct is required to trick the compiler for the lack of reverse type inference.")]
    public struct OptionalEmpty
    {
        public OptionalEmpty()
        {
        }
    }

    public static class Optional
    {
        internal static readonly Dictionary<Type, Func<object, bool>> ValueConditions = new();
#pragma warning disable CS0618 // OptionalEmpty is only allowed to be used internally
        public static OptionalEmpty Empty { get; } = new();
#pragma warning restore CS0618

        public static Optional<T> Of<T>(T value) where T : class => Optional<T>.Of(value);
        public static Optional<T> OfNullable<T>(T value) where T : class => Optional<T>.OfNullable(value);

        /// <summary>
        ///     Adds a condition to the optional of the given type that is checked whenever <see cref="Optional{T}.HasValue" /> is
        ///     checked.
        /// </summary>
        /// <param name="hasValueCondition">Condition to add to the <see cref="Optional{T}.HasValue" /> check.</param>
        /// <param arg="T">
        ///     Type that should have the extra condition. The given type will also apply to more specific types than
        ///     itself.
        /// </param>
        public static void ApplyHasValueCondition<T>(Func<T, bool> hasValueCondition) where T : class
        {
            // Add to global so that the Optional<T> can lazily evaluate which conditions it should add to its checks based on its type.
            ValueConditions.Add(typeof(T), o => hasValueCondition(o as T));
        }
    }

    public sealed class OptionalNullException<T> : Exception
    {
        public OptionalNullException() : base($"Optional <{nameof(T)}> is null!")
        {
        }

        public OptionalNullException(string message) : base($"Optional <{nameof(T)}> is null:\n\t{message}")
        {
        }
    }

    [Serializable]
    public sealed class OptionalEmptyException<T> : Exception
    {
        public OptionalEmptyException() : base($"Optional <{nameof(T)}> is empty.")
        {
        }

        public OptionalEmptyException(string message) : base($"Optional <{nameof(T)}> is empty:\n\t{message}")
        {
        }
    }
}
