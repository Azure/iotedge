// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Blob
{
    using System;
    using System.Threading.Tasks;
    using global::Azure.Storage.Blobs;
    using global::Azure.Storage.Blobs.Models;
    using global::Azure.Storage.Blobs.Specialized;
    using Microsoft.Azure.Devices.Edge.Util;

    class AzureBlobUploader : IAzureBlobUploader
    {
        public IAzureBlob GetBlob(Uri containerUri, string blobName, Option<string> contentType, Option<string> contentEncoding)
        {
            var container = new BlobContainerClient(Preconditions.CheckNotNull(containerUri, nameof(containerUri)));
            BlockBlobClient blob = container.GetBlockBlobClient(Preconditions.CheckNonWhiteSpace(blobName, nameof(blobName)));
            var headers = new BlobHttpHeaders();
            contentType.ForEach(c => headers.ContentType = c);
            contentEncoding.ForEach(c => headers.ContentEncoding = c);
            var azureBlob = new AzureBlob(blob, headers);
            return azureBlob;
        }

        public async Task<IAzureAppendBlob> GetAppendBlob(Uri containerUri, string blobName, Option<string> contentType, Option<string> contentEncoding)
        {
            var container = new BlobContainerClient(Preconditions.CheckNotNull(containerUri, nameof(containerUri)));
            AppendBlobClient blob = container.GetAppendBlobClient(Preconditions.CheckNonWhiteSpace(blobName, nameof(blobName)));
            var headers = new BlobHttpHeaders();
            contentType.ForEach(c => headers.ContentType = c);
            contentEncoding.ForEach(c => headers.ContentEncoding = c);
            var options = new AppendBlobCreateOptions { HttpHeaders = headers };
            await blob.CreateAsync(options);
            var azureBlob = new AzureAppendBlob(blob);
            return azureBlob;
        }
    }
}
