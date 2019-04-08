// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Blob
{
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Storage.Blob;

    public interface IAzureBlobBase
    {
        string Name { get; }

        BlobProperties BlobProperties { get; }
    }

    public interface IAzureBlob : IAzureBlobBase
    {
        Task UploadFromByteArrayAsync(byte[] bytes);
    }

    public interface IAzureAppendBlob : IAzureBlobBase
    {
        Task AppendByteArray(byte[] bytes);
    }
}
