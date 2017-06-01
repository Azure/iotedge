// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;

    public class ProtocolGatewayMessage : IMessage
    {

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

        #region IDisposable Support
        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.Payload?.SafeRelease();
                }

                this.disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion

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

            public Builder WithAddress(string address)
            {
                this.address = address;
                return this;
            }

            public Builder WithProperties(IDictionary<string, string> properties)
            {
                this.properties = Preconditions.CheckNotNull(properties);
                return this;
            }

            public Builder WithId(string id)
            {
                this.id = id;
                return this;
            }

            public Builder WithCreatedTimeUtc(DateTime createdTime)
            {
                this.createdTimeUtc = createdTime;
                return this;
            }

            public Builder WithDeliveryCount(uint deliveryCount)
            {
                this.deliveryCount = deliveryCount;
                return this;
            }

            public Builder WithSequenceNumber(ulong sequenceNumber)
            {
                this.sequenceNumber = sequenceNumber;
                return this;
            }

            public ProtocolGatewayMessage Build()
            {
                return new ProtocolGatewayMessage(this.payload, this.address, this.properties, this.id, this.createdTimeUtc, this.deliveryCount, this.sequenceNumber);
            }
        }
    }
}