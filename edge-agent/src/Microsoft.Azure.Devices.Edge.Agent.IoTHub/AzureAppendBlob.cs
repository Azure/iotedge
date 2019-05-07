// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.WindowsAzure.Storage.Blob;

    class AzureAppendBlob : IAzureAppendBlob
    {
        readonly CloudAppendBlob appendBlob;

        public AzureAppendBlob(CloudAppendBlob appendBlob)
        {
            this.appendBlob = Preconditions.CheckNotNull(appendBlob, nameof(appendBlob));
        }

        public string Name => this.appendBlob.Name;

        public Task AppendByteArray(ArraySegment<byte> bytes) => this.appendBlob.AppendFromByteArrayAsync(bytes.Array, bytes.Offset, bytes.Count);
    }
}
