// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// This acts exactly like a regular dictionary except that indexing [] a nonexistant key will instead generate a default value using the provided function.
    /// </summary>
    /// <typeparam name="TKey">Key type</typeparam>
    /// <typeparam name="TValue">Value type</typeparam>
    public class DefaultDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        Dictionary<TKey, TValue> inner = new Dictionary<TKey, TValue>();
        Func<TKey, TValue> getDefault;

        public DefaultDictionary(Func<TKey, TValue> getDefault)
        {
            this.getDefault = getDefault;
        }

        public TValue this[TKey key]
        {
            get
            {
                TValue val;
                if (!this.inner.TryGetValue(key, out val))
                {
                    val = this.getDefault(key);
                    this.inner.Add(key, val);
                }

                return val;
            }

            set => this.inner[key] = value;
        }

        public ICollection<TKey> Keys => ((IDictionary<TKey, TValue>)this.inner).Keys;

        public ICollection<TValue> Values => ((IDictionary<TKey, TValue>)this.inner).Values;

        public int Count => ((ICollection<KeyValuePair<TKey, TValue>>)this.inner).Count;

        public bool IsReadOnly => ((ICollection<KeyValuePair<TKey, TValue>>)this.inner).IsReadOnly;

        public void Add(TKey key, TValue value)
        {
            ((IDictionary<TKey, TValue>)this.inner).Add(key, value);
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            ((ICollection<KeyValuePair<TKey, TValue>>)this.inner).Add(item);
        }

        public void Clear()
        {
            ((ICollection<KeyValuePair<TKey, TValue>>)this.inner).Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return ((ICollection<KeyValuePair<TKey, TValue>>)this.inner).Contains(item);
        }

        public bool ContainsKey(TKey key)
        {
            return ((IDictionary<TKey, TValue>)this.inner).ContainsKey(key);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            ((ICollection<KeyValuePair<TKey, TValue>>)this.inner).CopyTo(array, arrayIndex);
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<TKey, TValue>>)this.inner).GetEnumerator();
        }

        public bool Remove(TKey key)
        {
            return ((IDictionary<TKey, TValue>)this.inner).Remove(key);
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return ((ICollection<KeyValuePair<TKey, TValue>>)this.inner).Remove(item);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return ((IDictionary<TKey, TValue>)this.inner).TryGetValue(key, out value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)this.inner).GetEnumerator();
        }
    }
}
