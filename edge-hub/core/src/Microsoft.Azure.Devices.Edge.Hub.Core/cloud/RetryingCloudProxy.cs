// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Cloud
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
    using Microsoft.Extensions.Logging;
    using Nito.AsyncEx;

    public class RetryingCloudProxy : ICloudProxy
    {
        const int RetryCount = 2;
        readonly AsyncLock cloudProxyLock = new AsyncLock();
        readonly Func<Task<Try<ICloudProxy>>> cloudProxyGetter;
        readonly string id;

        ICloudProxy innerCloudProxy;

        public RetryingCloudProxy(string id, Func<Task<Try<ICloudProxy>>> cloudProxyGetter, ICloudProxy cloudProxyImplementation)
        {
            this.id = Preconditions.CheckNonWhiteSpace(id, nameof(id));
            this.cloudProxyGetter = Preconditions.CheckNotNull(cloudProxyGetter, nameof(cloudProxyGetter));
            this.innerCloudProxy = Preconditions.CheckNotNull(cloudProxyImplementation, nameof(cloudProxyImplementation));
        }

        public bool IsActive => this.innerCloudProxy.IsActive;

        internal ICloudProxy InnerCloudProxy => this.innerCloudProxy;

        public Task<bool> CloseAsync() => this.ExecuteOperation(c => c.CloseAsync(), "CloseAsync");

        public Task<bool> OpenAsync() => this.ExecuteOperation(c => c.OpenAsync(), "OpenAsync");

        public Task SendMessageAsync(IMessage message) => this.ExecuteOperation(c => c.SendMessageAsync(message), "SendMessageAsync");

        public Task SendMessageBatchAsync(IEnumerable<IMessage> inputMessages) => this.ExecuteOperation(c => c.SendMessageBatchAsync(inputMessages), "SendMessageBatchAsync");

        public Task UpdateReportedPropertiesAsync(IMessage reportedPropertiesMessage) => this.ExecuteOperation(c => c.UpdateReportedPropertiesAsync(reportedPropertiesMessage), "UpdateReportedPropertiesAsync");

        public Task<IMessage> GetTwinAsync() => this.ExecuteOperation(c => c.GetTwinAsync(), "GetTwinAsync");

        public Task SendFeedbackMessageAsync(string messageId, FeedbackStatus feedbackStatus) => this.ExecuteOperation(c => c.SendFeedbackMessageAsync(messageId, feedbackStatus), "SendFeedbackMessageAsync");

        public Task SetupCallMethodAsync() => this.ExecuteOperation(c => c.SetupCallMethodAsync(), "SetupCallMethodAsync");

        public Task RemoveCallMethodAsync() => this.ExecuteOperation(c => c.RemoveCallMethodAsync(), "RemoveCallMethodAsync");

        public Task SetupDesiredPropertyUpdatesAsync() => this.ExecuteOperation(c => c.SetupDesiredPropertyUpdatesAsync(), "SetupDesiredPropertyUpdatesAsync");

        public Task RemoveDesiredPropertyUpdatesAsync() => this.ExecuteOperation(c => c.RemoveDesiredPropertyUpdatesAsync(), "RemoveDesiredPropertyUpdatesAsync");

        public Task StartListening() => this.ExecuteOperation(c => c.StartListening(), "StartListening");

        Task ExecuteOperation(Func<ICloudProxy, Task> func, string operation) => this.ExecuteOperation(
            async c =>
            {
                await func(c);
                return 1;
            },
            operation);

        async Task<T> ExecuteOperation<T>(Func<ICloudProxy, Task<T>> func, string operation)
        {
            int i = 0;
            while (true)
            {
                ICloudProxy cloudProxy = await this.GetCloudProxy();
                try
                {
                    return await func(cloudProxy);
                }
                catch (Exception e)
                {
                    if (cloudProxy.IsActive)
                    {
                        Events.ThrowExceptionProxyActive(this.id, e, operation);
                        throw;
                    }

                    if (++i == RetryCount)
                    {
                        Events.ThrowExceptionRetryCountReached(this.id, e, operation, RetryCount);
                        throw;
                    }

                    Metrics.AddRetryOperation(this.id, operation);
                    Events.Retrying(this.id, e, operation);
                }
            }
        }

        async Task<ICloudProxy> GetCloudProxy()
        {
            if (!this.innerCloudProxy.IsActive)
            {
                using (await this.cloudProxyLock.LockAsync())
                {
                    if (!this.innerCloudProxy.IsActive)
                    {
                        Events.GettingNewCloudProxy(this.id);
                        Try<ICloudProxy> cloudProxyTry = await this.cloudProxyGetter();
                        if (!cloudProxyTry.Success)
                        {
                            throw new EdgeHubIOException("Unable to create IoTHub connection", cloudProxyTry.Exception);
                        }

                        Events.GotNewCloudProxy(this.id);
                        this.innerCloudProxy = cloudProxyTry.Value;
                    }
                }
            }

            return this.innerCloudProxy;
        }

        static class Events
        {
            const int IdStart = HubCoreEventIds.RetryingCloudProxy;
            static readonly ILogger Log = Logger.Factory.CreateLogger<RetryingCloudProxy>();

            enum EventIds
            {
                UnhandledException = IdStart,
                RetryingOperation,
                GettingNewCloudProxy,
                GotNewCloudProxy
            }

            public static void ThrowExceptionProxyActive(string id, Exception e, string operation)
            {
                Log.LogDebug((int)EventIds.UnhandledException, e, $"Not retrying cloud proxy operation {operation} for {id} since the cloud proxy is still active");
            }

            public static void ThrowExceptionRetryCountReached(string id, Exception e, string operation, int retryCount)
            {
                Log.LogDebug((int)EventIds.UnhandledException, e, $"Not retrying cloud proxy operation {operation} for {id} since the max retry count ({retryCount}) has been reached");
            }

            public static void Retrying(string id, Exception e, string operation)
            {
                Log.LogInformation((int)EventIds.RetryingOperation, e, $"Retrying cloud proxy operation {operation} for {id}.");
            }

            public static void GettingNewCloudProxy(string id)
            {
                Log.LogDebug((int)EventIds.GettingNewCloudProxy, $"Getting new cloud proxy for client {id} since current cloud proxy is not active");
            }

            public static void GotNewCloudProxy(string id)
            {
                Log.LogDebug((int)EventIds.GotNewCloudProxy, $"Get new cloud proxy for client {id}");
            }
        }

        static class Metrics
        {
            static readonly IMetricsCounter RetriesCounter = Util.Metrics.Metrics.Instance.CreateCounter(
                    "operation_retry",
                    "Operation retries",
                    new List<string> { "id", "operation", MetricsConstants.MsTelemetry });

            public static void AddRetryOperation(string id, string operation) => RetriesCounter.Increment(1, new[] { id, operation, bool.TrueString });
        }
    }
}
