// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class DictionaryComparer<TKey, TValue> : IEqualityComparer<IDictionary<TKey, TValue>>
        where TValue : IEquatable<TValue>
    {
        public bool Equals(IDictionary<TKey, TValue> x, IDictionary<TKey, TValue> y) =>
            ReferenceEquals(x, y) ||
            (
                x != null &&
                y != null &&
                x.Keys.Count() == y.Keys.Count() &&
                x.Keys.All(
                    key => y.ContainsKey(key) &&
                    (
                        (x[key] == null && y[key] == null) ||
                        (
                            x[key] != null &&
                            x[key].Equals(y[key])
                        )
                    )
                )
            );

        public int GetHashCode(IDictionary<TKey, TValue> obj) =>
            obj.Aggregate(17, (acc, pair) => (acc * 31 + pair.Key.GetHashCode()) * 31 + pair.Value.GetHashCode());
    }

    public class ReadOnlyDictionaryComparer<TKey, TValue> : IEqualityComparer<IReadOnlyDictionary<TKey, TValue>>
        where TValue : IEquatable<TValue>
    {
        public bool Equals(IReadOnlyDictionary<TKey, TValue> x, IReadOnlyDictionary<TKey, TValue> y) =>
            ReferenceEquals(x, y) ||
            (
                x != null &&
                y != null &&
                x.Keys.Count() == y.Keys.Count() &&
                x.Keys.All(
                    key => y.ContainsKey(key) &&
                    (
                        (x[key] == null && y[key] == null) ||
                        (
                            x[key] != null &&
                            x[key].Equals(y[key])
                        )
                    )
                )
            );

        public int GetHashCode(IDictionary<TKey, TValue> obj) =>
            obj.Aggregate(17, (acc, pair) => (acc * 31 + pair.Key.GetHashCode()) * 31 + pair.Value.GetHashCode());
    }

    public class DictionaryComparer
    {
        public static readonly DictionaryComparer<string, string> StringDictionaryComparer = new DictionaryComparer<string, string>();
    }
}
