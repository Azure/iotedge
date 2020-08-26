// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Blob
{
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

        public Task UploadFromStreamAsync(Stream source) => this.blockBlob.UploadFromStreamAsync(source);
    }
}
