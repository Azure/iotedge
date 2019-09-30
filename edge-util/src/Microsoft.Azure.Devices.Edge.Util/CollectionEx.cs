// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public static class CollectionEx
    {
        public static Option<TValue> Get<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key)
            => dict.TryGetValue(key, out TValue value) ? Option.Some(value) : Option.None<TValue>();

        public static TValue GetOrElse<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue orElse)
            => dict.TryGetValue(key, out TValue value) ? value : orElse;

        public static TValue GetOrElse<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, Func<TValue> orElse)
            => dict.TryGetValue(key, out TValue value) ? value : orElse();

        public static Option<T> HeadOption<T>(this IEnumerable<T> src)
        {
            if (src is IList<T> list && list.Count > 0)
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

        public static Option<T> FirstOption<T>(this IEnumerable<T> src, Func<T, bool> predicate)
        {
            T t = src.FirstOrDefault(predicate);
            return EqualityComparer<T>.Default.Equals(t, default(T))
                ? Option.None<T>()
                : Option.Some(t);
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
        /// <returns>An &lt;see cref="IDictionary&lt;string, string"/&gt; containing the keys/values parsed
        ///  using the list of separators as delimiters.</returns>
        public static IDictionary<string, string> ToDictionary(this IEnumerable<string> strings, params char[] separators)
        {
            var dictionary = new Dictionary<string, string>();
            foreach (string str in strings)
            {
                string[] tokens = str.Split(separators, 2);
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

        public static IDictionary<string, string> ToDictionary(this IList<KeyValuePair<string, string>> values)
        {
            var dictionary = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> kvp in values)
            {
                dictionary.Add(kvp.Key, kvp.Value);
            }

            return dictionary;
        }

        public static TSource ElementAtOrDefault<TSource>(this ICollection<TSource> source, int index, TSource defaultValue)
            => index >= 0 && index < source.Count ? source.ElementAt(index) : defaultValue;

        public static string ElementAtOrEmpty(this ICollection<string> source, int index)
            => source.ElementAtOrDefault(index, string.Empty);

        public static bool TryRemove<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, out TValue value)
        {
            if (dictionary.TryGetValue(key, out value))
            {
                dictionary.Remove(key);
                return true;
            }

            return false;
        }

        public static void AddIfNonEmpty<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue value)
            where TKey : class
            where TValue : class
        {
            if (key != null && (typeof(TKey) != typeof(string) || !string.IsNullOrWhiteSpace(key as string)) &&
                value != null && (typeof(TValue) != typeof(string) || !string.IsNullOrWhiteSpace(value as string)))
            {
                dictionary.Add(key, value);
            }
        }

        public static bool TryGetNonEmptyValue<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, out TValue value)
            where TValue : class
        {
            if (dictionary.TryGetValue(key, out TValue dictVal)
                && dictVal != null
                && (typeof(TValue) != typeof(string) || !string.IsNullOrWhiteSpace(dictVal as string)))
            {
                value = dictVal;
                return true;
            }

            value = default(TValue);
            return false;
        }
    }
}
