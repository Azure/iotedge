// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Blob
{
    using System.IO;
    using System.Threading.Tasks;
    using global::Azure.Storage.Blobs.Specialized;
    using Microsoft.Azure.Devices.Edge.Util;

    class AzureBlob : IAzureBlob
    {
        readonly BlockBlobClient blockBlob;

        public AzureBlob(BlockBlobClient blockBlob)
        {
            this.blockBlob = Preconditions.CheckNotNull(blockBlob, nameof(blockBlob));
        }

        public string Name => this.blockBlob.Name;

        public Task UploadFromByteArrayAsync(byte[] bytes) => this.blockBlob.UploadAsync(new MemoryStream(bytes));

        public Task UploadFromStreamAsync(Stream source) => this.blockBlob.UploadAsync(source);
    }
}
