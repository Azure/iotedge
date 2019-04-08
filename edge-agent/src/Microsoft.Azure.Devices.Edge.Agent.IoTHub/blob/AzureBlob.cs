// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Blob
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.WindowsAzure.Storage.Blob;

    class AzureBlob : IAzureBlob
    {
        readonly CloudBlockBlob blockBlob;

        public AzureBlob(CloudBlockBlob blockBlob)
        {
            this.blockBlob = Preconditions.CheckNotNull(blockBlob, nameof(blockBlob));
        }

        public string Name => this.blockBlob.Name;

        public Task UploadFromByteArrayAsync(byte[] bytes) => this.blockBlob.UploadFromByteArrayAsync(bytes, 0, bytes.Length);
    }

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
