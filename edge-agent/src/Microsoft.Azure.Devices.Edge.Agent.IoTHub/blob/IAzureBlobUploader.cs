// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Blob
{
    using System;

    public interface IAzureBlobUploader
    {
        IAzureBlob GetBlob(Uri containerUri, string blobName);

        IAzureAppendBlob GetAppendBlob(Uri containerUri, string blobName);
    }
}
