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
        public ProtocolGatewayMessage(IByteBuffer payload, string address)
            : this(payload,
                  address,
                  new Dictionary<string, string>(),
                  null,
                  DateTime.MinValue,
                  0,
                  0)
        { }

        public ProtocolGatewayMessage(
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
    }
}