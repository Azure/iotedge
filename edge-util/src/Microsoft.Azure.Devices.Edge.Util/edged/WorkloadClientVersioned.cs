// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Edged
{
    using System;
    using System.Net.Sockets;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Extensions.Logging;

    abstract class WorkloadClientVersioned
    {
        static readonly TimeSpan DefaultOperationTimeout = TimeSpan.FromMinutes(5);

        static readonly RetryStrategy TransientRetryStrategy =
            new ExponentialBackoff(retryCount: 3, minBackoff: TimeSpan.FromSeconds(1), maxBackoff: TimeSpan.FromSeconds(3), deltaBackoff: TimeSpan.FromSeconds(2));

        readonly ITransientErrorDetectionStrategy transientErrorDetectionStrategy;
        readonly TimeSpan operationTimeout;

        protected WorkloadClientVersioned(
            Uri serverUri,
            ApiVersion apiVersion,
            string moduleId,
            string moduleGenerationId,
            ITransientErrorDetectionStrategy transientErrorDetectionStrategy,
            Option<TimeSpan> operationTimeout)
        {
            this.WorkloadUri = Preconditions.CheckNotNull(serverUri, nameof(serverUri));
            this.Version = Preconditions.CheckNotNull(apiVersion, nameof(apiVersion));
            this.ModuleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            this.ModuleGenerationId = Preconditions.CheckNonWhiteSpace(moduleGenerationId, nameof(moduleGenerationId));
            this.transientErrorDetectionStrategy = transientErrorDetectionStrategy;
            this.operationTimeout = operationTimeout.GetOrElse(DefaultOperationTimeout);
        }

        protected Uri WorkloadUri { get; }

        protected ApiVersion Version { get; }

        protected string ModuleId { get; }

        protected string ModuleGenerationId { get; }

        public abstract Task<ServerCertificateResponse> CreateServerCertificateAsync(string hostname, DateTime expiration);

        public abstract Task<string> GetTrustBundleAsync();

        public abstract Task<string> GetManifestTrustBundleAsync();

        public abstract Task<string> EncryptAsync(string initializationVector, string plainText);

        public abstract Task<string> DecryptAsync(string initializationVector, string encryptedText);

        public abstract Task<string> SignAsync(string keyId, string algorithm, string data);

        public abstract Task<string> ValidateTokenAsync(string token);

        protected internal async Task<T> Execute<T>(Func<Task<T>> func, string operation)
        {
            try
            {
                Events.ExecutingOperation(operation, this.WorkloadUri.ToString());
                T result = await ExecuteWithRetry(
                    func,
                    r => Events.RetryingOperation(operation, this.WorkloadUri.ToString(), r),
                    this.transientErrorDetectionStrategy)
                    .TimeoutAfter(this.operationTimeout);
                Events.SuccessfullyExecutedOperation(operation, this.WorkloadUri.ToString());
                return result;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionRefused)
            {
                Events.StaleSocketShutdown(ex, operation, this.WorkloadUri.ToString());
                Environment.Exit(ex.ErrorCode);
                return default(T);
            }
            catch (Exception ex)
            {
                Events.ErrorExecutingOperation(ex, operation, this.WorkloadUri.ToString());
                this.HandleException(ex, operation);
                return default(T);
            }
        }

        protected abstract void HandleException(Exception ex, string operation);

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
                RetryingOperation,
                ErrorExecutingOperation,
                StaleSocketShutdown
            }

            public static void StaleSocketShutdown(Exception ex, string operation, string url)
            {
                Log.LogError((int)EventIds.StaleSocketShutdown, ex, $"Shutting down because no response from {url} for {operation}");
            }

            public static void ErrorExecutingOperation(Exception ex, string operation, string url)
            {
                Log.LogDebug((int)EventIds.ErrorExecutingOperation, ex, $"Error when getting an Http response from {url} for {operation}");
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
