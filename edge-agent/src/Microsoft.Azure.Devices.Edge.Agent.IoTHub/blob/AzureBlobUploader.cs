// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Blob
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.WindowsAzure.Storage.Blob;

    class AzureBlobUploader : IAzureBlobUploader
    {
        public IAzureBlob GetBlob(Uri containerUri, string blobName, Option<string> contentType, Option<string> contentEncoding)
        {
            var container = new CloudBlobContainer(Preconditions.CheckNotNull(containerUri, nameof(containerUri)));
            CloudBlockBlob blob = container.GetBlockBlobReference(Preconditions.CheckNonWhiteSpace(blobName, nameof(blobName)));
            contentType.ForEach(c => blob.Properties.ContentType = c);
            contentEncoding.ForEach(c => blob.Properties.ContentEncoding = c);
            var azureBlob = new AzureBlob(blob);
            return azureBlob;
        }

        public async Task<IAzureAppendBlob> GetAppendBlob(Uri containerUri, string blobName, Option<string> contentType, Option<string> contentEncoding)
        {
            var container = new CloudBlobContainer(Preconditions.CheckNotNull(containerUri, nameof(containerUri)));
            CloudAppendBlob blob = container.GetAppendBlobReference(Preconditions.CheckNonWhiteSpace(blobName, nameof(blobName)));
            contentType.ForEach(c => blob.Properties.ContentType = c);
            contentEncoding.ForEach(c => blob.Properties.ContentEncoding = c);
            await blob.CreateOrReplaceAsync();
            var azureBlob = new AzureAppendBlob(blob);
            return azureBlob;
        }
    }
}
