// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Cloud
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Nito.AsyncEx;

    public class RetryingCloudProxy : ICloudProxy
    {
        const int RetryCount = 3;
        readonly AsyncLock cloudProxyLock = new AsyncLock();
        readonly Func<Task<Try<ICloudProxy>>> cloudProxyGetter;

        ICloudProxy innerCloudProxy;

        public RetryingCloudProxy(Func<Task<Try<ICloudProxy>>> cloudProxyGetter, ICloudProxy cloudProxyImplementation)
        {
            this.cloudProxyGetter = Preconditions.CheckNotNull(cloudProxyGetter, nameof(cloudProxyGetter));
            this.innerCloudProxy = Preconditions.CheckNotNull(cloudProxyImplementation, nameof(cloudProxyImplementation));
        }

        public bool IsActive => this.innerCloudProxy.IsActive;

        internal ICloudProxy InnerCloudProxy => this.innerCloudProxy;

        public Task<bool> CloseAsync() => this.ExecuteOperation(c => c.CloseAsync());

        public Task<bool> OpenAsync() => this.ExecuteOperation(c => c.OpenAsync());

        public Task SendMessageAsync(IMessage message) => this.ExecuteOperation(c => c.SendMessageAsync(message));

        public Task SendMessageBatchAsync(IEnumerable<IMessage> inputMessages) => this.ExecuteOperation(c => c.SendMessageBatchAsync(inputMessages));

        public Task UpdateReportedPropertiesAsync(IMessage reportedPropertiesMessage) => this.ExecuteOperation(c => c.UpdateReportedPropertiesAsync(reportedPropertiesMessage));

        public Task<IMessage> GetTwinAsync() => this.ExecuteOperation(c => c.GetTwinAsync());

        public Task SendFeedbackMessageAsync(string messageId, FeedbackStatus feedbackStatus) => this.ExecuteOperation(c => c.SendFeedbackMessageAsync(messageId, feedbackStatus));

        public Task SetupCallMethodAsync() => this.ExecuteOperation(c => c.SetupCallMethodAsync());

        public Task RemoveCallMethodAsync() => this.ExecuteOperation(c => c.RemoveCallMethodAsync());

        public Task SetupDesiredPropertyUpdatesAsync() => this.ExecuteOperation(c => c.SetupDesiredPropertyUpdatesAsync());

        public Task RemoveDesiredPropertyUpdatesAsync() => this.ExecuteOperation(c => c.RemoveDesiredPropertyUpdatesAsync());

        public Task StartListening() => this.ExecuteOperation(c => c.StartListening());

        Task ExecuteOperation(Func<ICloudProxy, Task> func) => this.ExecuteOperation(
            async c =>
            {
                await func(c);
                return 1;
            });

        async Task<T> ExecuteOperation<T>(Func<ICloudProxy, Task<T>> func)
        {
            int i = 0;
            while (true)
            {
                ICloudProxy cloudProxy = await this.GetCloudProxy();
                try
                {
                    return await func(cloudProxy);
                }
                catch (Exception)
                {
                    if (cloudProxy.IsActive || ++i == RetryCount)
                    {
                        throw;
                    }
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
                        Try<ICloudProxy> cloudProxyTry = await this.cloudProxyGetter();
                        if (!cloudProxyTry.Success)
                        {
                            throw new EdgeHubIOException("Unable to create IoTHub connection", cloudProxyTry.Exception);
                        }

                        this.innerCloudProxy = cloudProxyTry.Value;
                    }
                }
            }

            return this.innerCloudProxy;
        }
    }
}
