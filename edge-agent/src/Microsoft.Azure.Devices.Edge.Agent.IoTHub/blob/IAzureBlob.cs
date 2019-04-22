// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Blob
{
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Storage.Blob;

    public interface IAzureBlob
    {
        string Name { get; }

        BlobProperties BlobProperties { get; }

        Task UploadFromByteArrayAsync(byte[] bytes);
    }
}
