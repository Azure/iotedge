// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Blob
{
    using System;
    using System.Globalization;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Logs;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Storage.Blob;

    public class AzureBlobLogsUploader : ILogsUploader
    {
        const int RetryCount = 2;

        static readonly ITransientErrorDetectionStrategy TransientErrorDetectionStrategy = new ErrorDetectionStrategy();

        static readonly RetryStrategy TransientRetryStrategy =
            new ExponentialBackoff(RetryCount, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(4));

        readonly string iotHubName;
        readonly string deviceId;
        readonly IAzureBlobUploader azureBlobUploader;

        public AzureBlobLogsUploader(string iotHubName, string deviceId, IAzureBlobUploader azureBlobUploader)
        {
            this.iotHubName = Preconditions.CheckNonWhiteSpace(iotHubName, nameof(iotHubName));
            this.deviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.azureBlobUploader = Preconditions.CheckNotNull(azureBlobUploader, nameof(azureBlobUploader));
        }

        public async Task Upload(string uri, string id, byte[] payload, LogsContentEncoding logsContentEncoding, LogsContentType logsContentType)
        {
            Preconditions.CheckNonWhiteSpace(uri, nameof(uri));
            Preconditions.CheckNonWhiteSpace(id, nameof(id));
            Preconditions.CheckNotNull(payload, nameof(payload));

            try
            {
                var containerUri = new Uri(uri);
                string blobName = this.GetBlobName(id, logsContentEncoding, logsContentType);
                var container = new CloudBlobContainer(containerUri);
                Events.Uploading(blobName, container.Name);
                await ExecuteWithRetry(
                    () =>
                    {
                        IAzureBlob blob = this.azureBlobUploader.GetBlob(containerUri, blobName);
                        SetContentEncoding(blob, logsContentEncoding);
                        SetContentType(blob, logsContentType);
                        return blob.UploadFromByteArrayAsync(payload);
                    },
                    r => Events.UploadErrorRetrying(blobName, container.Name, r));
                Events.UploadSuccess(blobName, container.Name);
            }
            catch (Exception e)
            {
                Events.UploadError(e, id);
                throw;
            }
        }

        internal static string GetExtension(LogsContentEncoding logsContentEncoding, LogsContentType logsContentType)
        {
            if (logsContentEncoding == LogsContentEncoding.Gzip)
            {
                return "gz";
            }

            if (logsContentType == LogsContentType.Json)
            {
                return "json";
            }

            return "log";
        }

        internal string GetBlobName(string id, LogsContentEncoding logsContentEncoding, LogsContentType logsContentType)
        {
            string extension = GetExtension(logsContentEncoding, logsContentType);
            string blobName = $"{id}-{DateTime.UtcNow.ToString("yyyy-MM-dd--HH-mm-ss", CultureInfo.InvariantCulture)}";
            return $"{this.iotHubName}/{this.deviceId}/{blobName}.{extension}";
        }

        static void SetContentType(IAzureBlob blob, LogsContentType logsContentType)
        {
            switch (logsContentType)
            {
                case LogsContentType.Json:
                    blob.BlobProperties.ContentType = "application/json";
                    break;
                case LogsContentType.Text:
                    blob.BlobProperties.ContentType = "text/plain";
                    break;
            }
        }

        static void SetContentEncoding(IAzureBlob blob, LogsContentEncoding logsContentEncoding)
        {
            switch (logsContentEncoding)
            {
                case LogsContentEncoding.Gzip:
                    blob.BlobProperties.ContentEncoding = "gzip";
                    break;
            }
        }

        static Task ExecuteWithRetry(Func<Task> func, Action<RetryingEventArgs> onRetry)
        {
            var transientRetryPolicy = new RetryPolicy(TransientErrorDetectionStrategy, TransientRetryStrategy);
            transientRetryPolicy.Retrying += (_, args) => onRetry(args);
            return transientRetryPolicy.ExecuteAsync(func);
        }

        class ErrorDetectionStrategy : ITransientErrorDetectionStrategy
        {
            public bool IsTransient(Exception ex) => !ex.IsFatal();
        }

        static class Events
        {
            const int IdStart = AgentEventIds.AzureBlobLogsUploader;
            static readonly ILogger Log = Logger.Factory.CreateLogger<AzureBlobLogsUploader>();

            enum EventIds
            {
                Uploading = IdStart + 1,
                UploadSuccess,
                UploadErrorRetrying,
                UploadError
            }

            public static void Uploading(string blobName, string container)
            {
                Log.LogInformation((int)EventIds.Uploading, $"Uploading blob {blobName} to container {container}");
            }

            public static void UploadSuccess(string blobName, string container)
            {
                Log.LogDebug((int)EventIds.UploadSuccess, $"Successfully uploaded blob {blobName} to container {container}");
            }

            public static void UploadErrorRetrying(string blobName, string container, RetryingEventArgs retryingEventArgs)
            {
                Log.LogDebug((int)EventIds.UploadErrorRetrying, retryingEventArgs.LastException, $"Error uploading {blobName} to container {container}. Retry count - {retryingEventArgs.CurrentRetryCount}");
            }

            public static void UploadError(Exception ex, string module)
            {
                Log.LogDebug((int)EventIds.UploadError, ex, $"Error uploading logs for {module}");
            }
        }
    }
}
