// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Blob
{
    using System.IO;
    using System.Threading.Tasks;
    using global::Azure.Storage.Blobs.Models;
    using global::Azure.Storage.Blobs.Specialized;
    using Microsoft.Azure.Devices.Edge.Util;

    class AzureBlob : IAzureBlob
    {
        readonly BlockBlobClient blockBlob;
        readonly BlobHttpHeaders headers;

        public AzureBlob(BlockBlobClient blockBlob, BlobHttpHeaders headers)
        {
            this.blockBlob = Preconditions.CheckNotNull(blockBlob, nameof(blockBlob));
            this.headers = Preconditions.CheckNotNull(headers, nameof(headers));
        }

        public string Name => this.blockBlob.Name;

        public Task UploadFromByteArrayAsync(byte[] bytes)
        {
            var options = new BlobUploadOptions { HttpHeaders = this.headers };
            return this.blockBlob.UploadAsync(new MemoryStream(bytes), options);
        }

        public Task UploadFromStreamAsync(Stream source) => this.blockBlob.UploadAsync(source);
    }
}
