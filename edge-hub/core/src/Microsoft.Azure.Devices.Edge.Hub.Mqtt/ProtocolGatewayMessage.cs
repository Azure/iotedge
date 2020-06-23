// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;

    public class ProtocolGatewayMessage : IMessage
    {
        readonly AtomicBoolean isDisposed = new AtomicBoolean(false);

        ProtocolGatewayMessage(
            IByteBuffer payload,
            string address,
            IDictionary<string, string> properties,
            string id,
            DateTime createdTimeUtc,
            uint deliveryCount,
            ulong sequenceNumber)
        {
            this.Payload = Preconditions.CheckNotNull(payload);
            this.Address = address;
            this.Properties = Preconditions.CheckNotNull(properties);
            this.Id = id;
            this.CreatedTimeUtc = createdTimeUtc;
            this.DeliveryCount = deliveryCount;
            this.SequenceNumber = sequenceNumber;
        }

        public string Address { get; }

        public IByteBuffer Payload { get; }

        public string Id { get; }

        public IDictionary<string, string> Properties { get; }

        public DateTime CreatedTimeUtc { get; }

        public uint DeliveryCount { get; }

        public ulong SequenceNumber { get; }

        public class Builder
        {
            readonly IByteBuffer payload;
            string address;
            IDictionary<string, string> properties;
            string id;
            DateTime createdTimeUtc;
            uint deliveryCount;
            ulong sequenceNumber;

            public Builder(IByteBuffer payload, string address)
            {
                this.payload = payload;
                this.address = address;
                this.properties = new Dictionary<string, string>();
            }

            public Builder WithAddress(string addr)
            {
                this.address = addr;
                return this;
            }

            public Builder WithProperties(IDictionary<string, string> props)
            {
                this.properties = Preconditions.CheckNotNull(props);
                return this;
            }

            public Builder WithId(string identifier)
            {
                this.id = identifier;
                return this;
            }

            public Builder WithCreatedTimeUtc(DateTime createdTime)
            {
                this.createdTimeUtc = createdTime;
                return this;
            }

            public Builder WithDeliveryCount(uint count)
            {
                this.deliveryCount = count;
                return this;
            }

            public Builder WithSequenceNumber(ulong sequenceNum)
            {
                this.sequenceNumber = sequenceNum;
                return this;
            }

            public ProtocolGatewayMessage Build()
            {
                return new ProtocolGatewayMessage(this.payload, this.address, this.properties, this.id, this.createdTimeUtc, this.deliveryCount, this.sequenceNumber);
            }
        }

        #region IDisposable Support

        protected virtual void Dispose(bool disposing)
        {
            if (!this.isDisposed.GetAndSet(true))
            {
                if (disposing)
                {
                    this.Payload?.SafeRelease();
                }
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
        }

        #endregion
    }
}
