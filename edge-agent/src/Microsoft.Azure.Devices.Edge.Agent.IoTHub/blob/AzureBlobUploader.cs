// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Blob
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Storage.Blob;

    class AzureBlobUploader : IAzureBlobUploader
    {
        public IAzureBlob GetBlob(Uri containerUri, string blobName)
        {
            var container = new CloudBlobContainer(containerUri);
            CloudBlockBlob blob = container.GetBlockBlobReference(blobName);
            var azureBlob = new AzureBlob(blob);
            return azureBlob;
        }

        public IAzureAppendBlob GetAppendBlob(Uri containerUri, string blobName)
        {
            var container = new CloudBlobContainer(containerUri);
            CloudAppendBlob blob = container.GetAppendBlobReference(blobName);
            var azureBlob = new AzureAppendBlob(blob);
            return azureBlob;
        }
    }
}
