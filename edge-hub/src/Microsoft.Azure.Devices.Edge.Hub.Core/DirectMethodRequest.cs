// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;

    public class DirectMethodRequest
    {
        public DirectMethodRequest(string id, string name, byte[] data, TimeSpan responseTimeout)
            : this(id, name, data, responseTimeout, TimeSpan.Zero)
        { }

        public DirectMethodRequest(string id, string name, byte[] data, TimeSpan responseTimeout, TimeSpan connectTimeout)
        {
            this.Id = id;
            this.Name = name;
            this.Data = data;            
            this.ConnectTimeout = connectTimeout;
            this.ResponseTimeout = responseTimeout;
            this.CorrelationId = Guid.NewGuid().ToString();
        }

        public string Id { get; }

        public string CorrelationId { get; }

        public string Name { get; }

        public byte[] Data { get; }

        public TimeSpan ConnectTimeout { get; }

        public TimeSpan ResponseTimeout { get; }
    }
}
