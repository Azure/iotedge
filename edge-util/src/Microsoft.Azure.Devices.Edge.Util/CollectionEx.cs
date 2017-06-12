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

        /// <summary>
        /// Transforms a list of strings into a dictionary by using the provided list of separators
        /// as delimiters for each string in the source list to split it into keys and values.
        /// </summary>
        /// <param name="strings">The source list of strings.</param>
        /// <param name="separators">Collection of separator characters to use as string delimiters.</param>
        /// <returns>An <see cref="IDictionary{string, string}"/> containing the keys/values parsed
        /// using the list of separators as delimiters.</returns>
        public static IDictionary<string, string> ToDictionary(this IEnumerable<string> strings, params char[] separators)
        {
            var dictionary = new Dictionary<string, string>();
            foreach (string str in strings)
            {
                string[] tokens = str.Split(separators);
                if (tokens.Length == 2)
                {
                    dictionary.Add(tokens[0], tokens[1]);
                }
                else
                {
                    throw new FormatException($"Invalid string format found: {str}");
                }
            }

            return dictionary;
        }
    }
}