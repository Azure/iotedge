// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;

    public class BrokeredCloudProxy : ICloudProxy
    {
        BrokeredCloudProxyDispatcher cloudProxyDispatcher;
        IIdentity identity;

        AtomicBoolean twinNeedsSubscribe = new AtomicBoolean(true);

        public BrokeredCloudProxy(IIdentity identity, BrokeredCloudProxyDispatcher cloudProxyDispatcher)
        {
            this.identity = Preconditions.CheckNotNull(identity);
            this.cloudProxyDispatcher = Preconditions.CheckNotNull(cloudProxyDispatcher);
        }

        public bool IsActive => this.cloudProxyDispatcher.IsActive;

        public Task<bool> CloseAsync() => this.cloudProxyDispatcher.CloseAsync(this.identity);
        public Task<bool> OpenAsync() => this.cloudProxyDispatcher.OpenAsync(this.identity);
        public Task RemoveCallMethodAsync() => this.cloudProxyDispatcher.RemoveCallMethodAsync(this.identity);
        public Task RemoveDesiredPropertyUpdatesAsync() => this.cloudProxyDispatcher.RemoveDesiredPropertyUpdatesAsync(this.identity);
        public Task SendFeedbackMessageAsync(string messageId, FeedbackStatus feedbackStatus) => this.cloudProxyDispatcher.SendFeedbackMessageAsync(this.identity, messageId, feedbackStatus);
        public Task SendMessageAsync(IMessage message) => this.cloudProxyDispatcher.SendMessageAsync(this.identity, message);
        public Task SendMessageBatchAsync(IEnumerable<IMessage> inputMessages) => this.cloudProxyDispatcher.SendMessageBatchAsync(this.identity, inputMessages);
        public Task SetupCallMethodAsync() => this.cloudProxyDispatcher.SetupCallMethodAsync(this.identity);
        public Task SetupDesiredPropertyUpdatesAsync() => this.cloudProxyDispatcher.SetupDesiredPropertyUpdatesAsync(this.identity);
        public Task StartListening() => this.cloudProxyDispatcher.StartListening(this.identity);

        public Task UpdateReportedPropertiesAsync(IMessage reportedPropertiesMessage)
        {
            return this.cloudProxyDispatcher.UpdateReportedPropertiesAsync(this.identity, reportedPropertiesMessage, this.twinNeedsSubscribe.GetAndSet(false));
        }

        public Task<IMessage> GetTwinAsync()
        {
            return this.cloudProxyDispatcher.GetTwinAsync(this.identity, this.twinNeedsSubscribe.GetAndSet(false));
        }
    }
}
