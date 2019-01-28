// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Edged
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Extensions.Logging;

    abstract class WorkloadClientVersioned
    {
        static readonly RetryStrategy TransientRetryStrategy =
            new ExponentialBackoff(retryCount: 3, minBackoff: TimeSpan.FromSeconds(2), maxBackoff: TimeSpan.FromSeconds(30), deltaBackoff: TimeSpan.FromSeconds(3));

        readonly ITransientErrorDetectionStrategy transientErrorDetectionStrategy;

        protected WorkloadClientVersioned(Uri serverUri, ApiVersion apiVersion, string moduleId, string moduleGenerationId, ITransientErrorDetectionStrategy transientErrorDetectionStrategy)
        {
            this.WorkloadUri = Preconditions.CheckNotNull(serverUri, nameof(serverUri));
            this.Version = Preconditions.CheckNotNull(apiVersion, nameof(apiVersion));
            this.ModuleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            this.ModuleGenerationId = Preconditions.CheckNonWhiteSpace(moduleGenerationId, nameof(moduleGenerationId));
            this.transientErrorDetectionStrategy = transientErrorDetectionStrategy;
        }

        protected Uri WorkloadUri { get; }

        protected ApiVersion Version { get; }

        protected string ModuleId { get; }

        protected string ModuleGenerationId { get; }

        public abstract Task<ServerCertificateResponse> CreateServerCertificateAsync(string hostname, DateTime expiration);

        public abstract Task<string> GetTrustBundleAsync();

        public abstract Task<string> EncryptAsync(string initializationVector, string plainText);

        public abstract Task<string> DecryptAsync(string initializationVector, string encryptedText);

        public abstract Task<string> SignAsync(string keyId, string algorithm, string data);

        protected abstract void HandleException(Exception ex, string operation);

        protected async Task<T> Execute<T>(Func<Task<T>> func, string operation)
        {
            try
            {
                Events.ExecutingOperation(operation, this.WorkloadUri.ToString());
                T result = await ExecuteWithRetry(func, r => Events.RetryingOperation(operation, this.WorkloadUri.ToString(), r), this.transientErrorDetectionStrategy);
                Events.SuccessfullyExecutedOperation(operation, this.WorkloadUri.ToString());
                return result;
            }
            catch (Exception ex)
            {
                this.HandleException(ex, operation);
                Events.SuccessfullyExecutedOperation(operation, this.WorkloadUri.ToString());
                return default(T);
            }
        }

        static Task<T> ExecuteWithRetry<T>(Func<Task<T>> func, Action<RetryingEventArgs> onRetry, ITransientErrorDetectionStrategy transientErrorDetection)
        {
            var transientRetryPolicy = new RetryPolicy(transientErrorDetection, TransientRetryStrategy);
            transientRetryPolicy.Retrying += (_, args) => onRetry(args);
            return transientRetryPolicy.ExecuteAsync(func);
        }

        static class Events
        {
            const int IdStart = UtilEventsIds.EdgeletWorkloadClient;
            static readonly ILogger Log = Logger.Factory.CreateLogger<WorkloadClient>();

            enum EventIds
            {
                ExecutingOperation = IdStart,
                SuccessfullyExecutedOperation,
                RetryingOperation
            }

            internal static void RetryingOperation(string operation, string url, RetryingEventArgs r)
            {
                Log.LogDebug((int)EventIds.RetryingOperation, $"Retrying Http call to {url} to {operation} because of error {r.LastException.Message}, retry count = {r.CurrentRetryCount}");
            }

            internal static void ExecutingOperation(string operation, string url)
            {
                Log.LogDebug((int)EventIds.ExecutingOperation, $"Making a Http call to {url} to {operation}");
            }

            internal static void SuccessfullyExecutedOperation(string operation, string url)
            {
                Log.LogDebug((int)EventIds.SuccessfullyExecutedOperation, $"Received a valid Http response from {url} for {operation}");
            }
        }
    }
}
