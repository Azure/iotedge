// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Text;

    public class DefaultDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        readonly Func<TKey, TValue> getDefault;
        readonly Dictionary<TKey, TValue> inner = new Dictionary<TKey, TValue>();

        public DefaultDictionary(Func<TKey, TValue> getDefault)
        {
            this.getDefault = getDefault;
        }

        public TValue this[TKey key]
        {
            get
            {
                TValue value;
                if (!this.inner.TryGetValue(key, out value))
                {
                    value = this.getDefault(key);
                    this.inner[key] = value;
                }

                return value;
            }
            set => this.inner[key] = value;
        }

        public ICollection<TKey> Keys => ((IDictionary<TKey, TValue>)this.inner).Keys;

        public ICollection<TValue> Values => ((IDictionary<TKey, TValue>)this.inner).Values;

        public int Count => this.inner.Count;

        public bool IsReadOnly => ((IDictionary<TKey, TValue>)this.inner).IsReadOnly;

        public void Add(TKey key, TValue value)
        {
            this.inner.Add(key, value);
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            ((IDictionary<TKey, TValue>)this.inner).Add(item);
        }

        public void Clear()
        {
            this.inner.Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return ((IDictionary<TKey, TValue>)this.inner).Contains(item);
        }

        public bool ContainsKey(TKey key)
        {
            return this.inner.ContainsKey(key);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            ((IDictionary<TKey, TValue>)this.inner).CopyTo(array, arrayIndex);
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return ((IDictionary<TKey, TValue>)this.inner).GetEnumerator();
        }

        public bool Remove(TKey key)
        {
            return this.inner.Remove(key);
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return ((IDictionary<TKey, TValue>)this.inner).Remove(item);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return this.inner.TryGetValue(key, out value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IDictionary<TKey, TValue>)this.inner).GetEnumerator();
        }
    }
}
