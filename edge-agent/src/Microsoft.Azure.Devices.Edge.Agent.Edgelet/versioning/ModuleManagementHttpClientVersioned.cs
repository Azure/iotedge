// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet.Versioning
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.Models;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Edged;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Extensions.Logging;

    abstract class ModuleManagementHttpClientVersioned
    {
        static readonly RetryStrategy TransientRetryStrategy =
            new ExponentialBackoff(retryCount: 3, minBackoff: TimeSpan.FromSeconds(2), maxBackoff: TimeSpan.FromSeconds(30), deltaBackoff: TimeSpan.FromSeconds(3));

        readonly ITransientErrorDetectionStrategy transientErrorDetectionStrategy;

        protected ModuleManagementHttpClientVersioned(Uri managementUri, ApiVersion version, ITransientErrorDetectionStrategy transientErrorDetectionStrategy)
        {
            this.ManagementUri = Preconditions.CheckNotNull(managementUri, nameof(managementUri));
            this.Version = Preconditions.CheckNotNull(version, nameof(version));
            this.transientErrorDetectionStrategy = transientErrorDetectionStrategy;
        }

        protected Uri ManagementUri { get; }

        protected ApiVersion Version { get; }

        public abstract Task<Identity> CreateIdentityAsync(string name, string managedBy);

        public abstract Task<Identity> UpdateIdentityAsync(string name, string generationId, string managedBy);

        public abstract Task DeleteIdentityAsync(string name);

        public abstract Task<IEnumerable<Identity>> GetIdentities();

        public abstract Task CreateModuleAsync(ModuleSpec moduleSpec);

        public abstract Task DeleteModuleAsync(string name);

        public abstract Task RestartModuleAsync(string name);

        public abstract Task<SystemInfo> GetSystemInfoAsync();

        public abstract Task<IEnumerable<ModuleRuntimeInfo>> GetModules<T>(CancellationToken cancellationToken);

        public abstract Task StartModuleAsync(string name);

        public abstract Task StopModuleAsync(string name);

        public abstract Task UpdateModuleAsync(ModuleSpec moduleSpec);

        public abstract Task UpdateAndStartModuleAsync(ModuleSpec moduleSpec);

        public abstract Task PrepareUpdateAsync(ModuleSpec moduleSpec);

        protected abstract void HandleException(Exception ex, string operation);

        protected Task Execute(Func<Task> func, string operation) =>
            this.Execute(
                async () =>
                {
                    await func();
                    return 1;
                },
                operation);

        protected async Task<T> Execute<T>(Func<Task<T>> func, string operation)
        {
            try
            {
                Events.ExecutingOperation(operation, this.ManagementUri.ToString());
                T result = await ExecuteWithRetry(func, r => Events.RetryingOperation(operation, this.ManagementUri.ToString(), r), this.transientErrorDetectionStrategy);
                Events.SuccessfullyExecutedOperation(operation, this.ManagementUri.ToString());
                return result;
            }
            catch (Exception ex)
            {
                this.HandleException(ex, operation);
                Events.SuccessfullyExecutedOperation(operation, this.ManagementUri.ToString());
                return default(T);
            }
        }

        static Task<T> ExecuteWithRetry<T>(Func<Task<T>> func, Action<RetryingEventArgs> onRetry, ITransientErrorDetectionStrategy transientErrorDetectionStrategy)
        {
            var transientRetryPolicy = new RetryPolicy(transientErrorDetectionStrategy, TransientRetryStrategy);
            transientRetryPolicy.Retrying += (_, args) => onRetry(args);
            return transientRetryPolicy.ExecuteAsync(func);
        }

        static class Events
        {
            const int IdStart = AgentEventIds.ModuleManagementHttpClient;
            static readonly ILogger Log = Logger.Factory.CreateLogger<ModuleManagementHttpClient>();

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
