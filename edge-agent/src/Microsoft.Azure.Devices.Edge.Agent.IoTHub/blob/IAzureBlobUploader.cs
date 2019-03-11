// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Blob
{
    using System;
    using System.Threading.Tasks;

    public interface IAzureBlobUploader
    {
        IAzureBlob GetBlob(Uri containerUri, string blobName);

        Task UploadBlob(IAzureBlob blob, byte[] bytes);
    }
}
