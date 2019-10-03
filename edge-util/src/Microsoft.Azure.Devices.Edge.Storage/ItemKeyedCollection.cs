// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;
    using ProtoBuf;

    public class ItemKeyedCollection : KeyedCollection<byte[], Item>
    {
        public ItemKeyedCollection(IEqualityComparer<byte[]> keyEqualityComparer)
            : base(keyEqualityComparer)
        {
        }

        public IList<(byte[], byte[])> ItemList => this.Items
            .Select(i => (i.Key, i.Value))
            .ToList();

        // Exposing the items through this property so that it can be used for serialization
        // using ProtoBuf when backups are created.
        internal IList<Item> AllItems => this.Items;

        protected override byte[] GetKeyForItem(Item item) => item.Key;
    }

    public class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[] x, byte[] y) => (x == null && y == null) || x.SequenceEqual(y);

        public int GetHashCode(byte[] obj)
        {
            int hashCode = 1291371069;
            foreach (byte b in obj)
            {
                hashCode = hashCode * -1521134295 + b.GetHashCode();
            }

            return hashCode;
        }
    }

    [ProtoContract]
    public class Item
    {
        private Item()
        {
        }

        public Item(byte[] key, byte[] value)
        {
            this.Key = Preconditions.CheckNotNull(key, nameof(key));
            this.Value = Preconditions.CheckNotNull(value, nameof(value));
        }

        [ProtoMember(1)]
        public byte[] Key { get; }

        [ProtoMember(2)]
        public byte[] Value { get; set; }
    }
}
