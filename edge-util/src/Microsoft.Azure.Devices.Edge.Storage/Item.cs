// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    using Microsoft.Azure.Devices.Edge.Util;
    using ProtoBuf;

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
