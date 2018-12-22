// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Common.Exceptions;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Extensions.Logging;

    public class RetryingServiceClient : IServiceClient
    {
        readonly IServiceClient underlying;

        static readonly ITransientErrorDetectionStrategy TransientDetectionStrategy = new DeviceClientRetryStrategy();
        static readonly RetryStrategy TransientRetryStrategy = new ExponentialBackoff(3, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(4));

        public RetryingServiceClient(IServiceClient underlying)
        {
            this.underlying = Preconditions.CheckNotNull(underlying, nameof(underlying));
        }

        public void Dispose() => this.underlying.Dispose();

        public Task<IEnumerable<Module>> GetModules() => this.ExecuteWithRetry(() => this.underlying.GetModules(), nameof(this.underlying.GetModules));

        public Task<Module> GetModule(string moduleId) =>
            this.ExecuteWithRetry(() => this.underlying.GetModule(moduleId), nameof(this.underlying.GetModule));

        public Task<Module[]> CreateModules(IEnumerable<string> identities) =>
            this.ExecuteWithRetry(() => this.underlying.CreateModules(identities), nameof(this.underlying.CreateModules));

        public Task<Module[]> UpdateModules(IEnumerable<Module> modules) =>
            this.ExecuteWithRetry(() => this.underlying.UpdateModules(modules), nameof(this.underlying.UpdateModules));

        public Task RemoveModules(IEnumerable<string> identities) =>
            this.ExecuteWithRetry(async () =>
            {
                await this.underlying.RemoveModules(identities);
                return 0;
            }, nameof(this.underlying.RemoveModules));

        Task<T> ExecuteWithRetry<T>(Func<Task<T>> func, string action)
        {
            var transientRetryPolicy = new RetryPolicy(TransientDetectionStrategy, TransientRetryStrategy);
            transientRetryPolicy.Retrying += (_, args) => Events.ActionFailed(args, action);
            return transientRetryPolicy.ExecuteAsync(func);
        }

        class DeviceClientRetryStrategy : ITransientErrorDetectionStrategy
        {
            static readonly ISet<Type> NonTransientExceptions = new HashSet<Type>
            {
                typeof(ArgumentException),
                typeof(UnauthorizedException)
            };

            public bool IsTransient(Exception ex) => !(NonTransientExceptions.Contains(ex.GetType()));
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<RetryingServiceClient>();
            const int IdStart = AgentEventIds.RetryingServiceClient;

            enum EventIds
            {
                Retrying = IdStart
            }

            public static void ActionFailed(RetryingEventArgs args, string action)
            {
                Log.LogDebug((int)EventIds.Retrying, $"Service Client threw exception {args.LastException} on action {action}. Current retry count {args.CurrentRetryCount}.");
            }
        }
    }
}
