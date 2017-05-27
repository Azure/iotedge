// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public static class CollectionEx
    {
        public static Option<TValue> Get<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key)
        {
            TValue value;
            return dict.TryGetValue(key, out value) ? Option.Some(value) : Option.None<TValue>();
        }

        public static TValue GetOrElse<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue orElse)
        {
            TValue value;
            return dict.TryGetValue(key, out value) ? value : orElse;
        }

        public static TValue GetOrElse<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, Func<TValue> orElse)
        {
            TValue value;
            return dict.TryGetValue(key, out value) ? value : orElse();
        }

        public static Option<T> HeadOption<T>(this IEnumerable<T> src)
        {
            var list = src as IList<T>;

            if (list != null && list.Count > 0)
            {
                return Option.Some(list[0]);
            }
            else
            {
                using (IEnumerator<T> e = src.GetEnumerator())
                {
                    if (e.MoveNext())
                    {
                        return Option.Some(e.Current);
                    }
                }
            }
            return Option.None<T>();
        }

        /// <summary>
        /// Produces a string representation of an <see cref="IDictionary{TKey,TValue}"/> in the form
        /// <c>(key1, value1), (key2, value2)...</c>. The idea is to use this representation when you
        /// wish to log a dictionary object into a stream.
        /// </summary>
        public static string ToLogString<TKey, TValue>(this IDictionary<TKey, TValue> dictionary) =>
            string.Join(", ", dictionary.Select(kvp => $"({kvp.Key}, {kvp.Value})"));
    }
}