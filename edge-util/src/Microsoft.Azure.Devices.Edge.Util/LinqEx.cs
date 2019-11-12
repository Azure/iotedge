// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public static class LinqEx
    {
        /// <summary>
        /// Ignores exceptions thrown by preceding linq functions and optionally calls
        /// <paramref name="action"/> passing a reference to the exception that was thrown
        /// if it is not <c>null</c>. Adapted from https://stackoverflow.com/a/14741288/8080.
        /// </summary>
        public static IEnumerable<T> IgnoreExceptions<T, TException>(this IEnumerable<T> src, Action<TException> action = null)
            where TException : Exception
        {
            using (IEnumerator<T> enumerator = src.GetEnumerator())
            {
                bool next = true;
                while (next)
                {
                    try
                    {
                        next = enumerator.MoveNext();
                    }
                    catch (TException ex)
                    {
                        action?.Invoke(ex);
                        continue;
                    }

                    if (next)
                    {
                        yield return enumerator.Current;
                    }
                }
            }
        }

        /// <summary>
        /// Compares <paramref name="first"/> and <paramref name="second"/> and returns
        /// a new sequence where all values occurring in both sequences have been
        /// filtered out. Treats the strings in both the sequences as being in the
        /// format <c>key=value</c> and uses the <c>key</c> for equality comparison.
        /// </summary>
        public static IEnumerable<string> RemoveIntersectionKeys(
            this IEnumerable<string> first,
            IEnumerable<string> second) => first.Except(second, StringKeyComparer.DefaultStringKeyComparer);

        /// <summary>
        /// Compares <paramref name="first"/> and <paramref name="second"/> and returns
        /// a new sequence where all values occurring in both sequences have been
        /// filtered out. Uses <paramref name="keySelector"/> to extract a 'key'
        /// from the value that uniquely identifies the value (this can be used
        /// to extract a primary key sub-string from a data record for example).
        /// </summary>
        public static IEnumerable<string> RemoveIntersectionKeys(
            this IEnumerable<string> first,
            IEnumerable<string> second,
            Func<string, string> keySelector) => first.Except(second, new StringKeyComparer(keySelector));

        /// <summary>
        /// Converts an IEnumerable<typeparamref name="T"/> into an IEnumerable<(uint, typeparamref name="T")/>.
        /// The uint provides the current count of items.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="self"></param>
        /// <returns></returns>
        public static IEnumerable<(uint, T)> Enumerate<T>(this IEnumerable<T> self)
        {
            uint num = 0;
            foreach (var item in self)
            {
                yield return (num, item);
                num += 1;
            }
        }

        /// <summary>
        /// Converts an IEnumerable<typeparamref name="TSource"/> into an IEnumerable<typeparamref name="TResult"/>, only including the value
        /// if the first element in the selector result tuple is true.
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="source"></param>
        /// <param name="selector"></param>
        /// <returns></returns>
        public static IEnumerable<TResult> SelectWhere<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, (bool, TResult)> selector)
        {
            foreach (TSource s in source)
            {
                (bool include, TResult result) = selector(s);
                if (include)
                {
                    yield return result;
                }
            }
        }

        public static async Task<IEnumerable<T1>> SelectManyAsync<T, T1>(this IEnumerable<T> source, Func<T, Task<IEnumerable<T1>>> selector)
        {
            return (await Task.WhenAll(source.Select(selector))).SelectMany(s => s);
        }
    }

    class StringKeyComparer : IEqualityComparer<string>
    {
        internal static readonly StringKeyComparer DefaultStringKeyComparer = new StringKeyComparer(s => s.Split(new[] { '=' }, 2)[0]);
        readonly Func<string, string> keySelector;

        internal StringKeyComparer(Func<string, string> keySelector)
        {
            Preconditions.CheckNotNull(keySelector, nameof(keySelector));
            this.keySelector = s => s == null ? null : keySelector(s);
        }

        public bool Equals(string x, string y) => this.keySelector(x) == this.keySelector(y);

        public int GetHashCode(string obj) => this.keySelector(obj)?.GetHashCode() ?? 0;
    }
}
