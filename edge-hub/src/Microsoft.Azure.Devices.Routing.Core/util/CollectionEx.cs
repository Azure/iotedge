// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Util
{
    using System.Collections.Generic;

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

        public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> list, int batchSize)
        {
            var current = new List<T>();
            foreach (T item in list)
            {
                current.Add(item);
                if (current.Count == batchSize)
                {
                    yield return current;
                    current = new List<T>(batchSize);
                }
            }

            if (current.Count > 0)
            {
                yield return current;
            }
        }
    }
}
