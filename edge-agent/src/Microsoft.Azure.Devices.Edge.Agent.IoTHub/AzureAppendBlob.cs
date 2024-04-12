// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using global::Azure.Storage.Blobs.Specialized;
    using Microsoft.Azure.Devices.Edge.Util;

    class AzureAppendBlob : IAzureAppendBlob
    {
        readonly AppendBlobClient appendBlob;

        public AzureAppendBlob(AppendBlobClient appendBlob)
        {
            this.appendBlob = Preconditions.CheckNotNull(appendBlob, nameof(appendBlob));
        }

        public string Name => this.appendBlob.Name;

        public Task AppendByteArray(ArraySegment<byte> bytes) =>
            this.appendBlob.AppendBlockAsync(new MemoryStream(bytes.Array, bytes.Offset, bytes.Count));
    }
}
