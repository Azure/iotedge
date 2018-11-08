// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
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
    }
}
