// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Blob
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IAzureBlobUploader
    {
        IAzureBlob GetBlob(Uri containerUri, string blobName, Option<string> contentType, Option<string> contentEncoding);

        Task<IAzureAppendBlob> GetAppendBlob(Uri containerUri, string blobName, Option<string> contentType, Option<string> contentEncoding);
    }
}
