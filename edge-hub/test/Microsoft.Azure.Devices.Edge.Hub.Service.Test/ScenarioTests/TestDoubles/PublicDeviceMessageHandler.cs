// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    using Xunit;

    /// <summary>
    /// The reason we need this class is because DeviceMessageHandler is not public, so we cannot use that as device fixture for tests.
    /// This one just a public wrapper around DeviceMessageHandler
    /// </summary>
    public class PublicDeviceMessageHandler : IDeviceListener, IDeviceProxy
    {
        // these two reference to the same DeviceMessageHandler
        private IDeviceListener handlerAsDeviceListener;
        private IDeviceProxy handlerAsDeviceProxy;

        public PublicDeviceMessageHandler(object deviceMessageHandler)
        {
            this.handlerAsDeviceListener = deviceMessageHandler as IDeviceListener;
            this.handlerAsDeviceProxy = deviceMessageHandler as IDeviceProxy;

            Assert.NotNull(this.handlerAsDeviceListener);
            Assert.NotNull(this.handlerAsDeviceProxy);
        }

        public IIdentity Identity => this.handlerAsDeviceListener.Identity;
        public Task AddDesiredPropertyUpdatesSubscription(string correlationId) => this.handlerAsDeviceListener.AddDesiredPropertyUpdatesSubscription(correlationId);
        public Task AddSubscription(DeviceSubscription subscription) => this.handlerAsDeviceListener.AddSubscription(subscription);
        public void BindDeviceProxy(IDeviceProxy deviceProxy) => this.handlerAsDeviceListener.BindDeviceProxy(deviceProxy);
        public Task CloseAsync() => this.handlerAsDeviceListener.CloseAsync();
        public Task ProcessDeviceMessageAsync(IMessage message) => this.handlerAsDeviceListener.ProcessDeviceMessageAsync(message);
        public Task ProcessDeviceMessageBatchAsync(IEnumerable<IMessage> message) => this.handlerAsDeviceListener.ProcessDeviceMessageBatchAsync(message);
        public Task ProcessMessageFeedbackAsync(string messageId, FeedbackStatus feedbackStatus) => this.handlerAsDeviceListener.ProcessMessageFeedbackAsync(messageId, feedbackStatus);
        public Task ProcessMethodResponseAsync(IMessage message) => this.handlerAsDeviceListener.ProcessMethodResponseAsync(message);
        public Task RemoveDesiredPropertyUpdatesSubscription(string correlationId) => this.handlerAsDeviceListener.RemoveDesiredPropertyUpdatesSubscription(correlationId);
        public Task RemoveSubscription(DeviceSubscription subscription) => this.handlerAsDeviceListener.RemoveSubscription(subscription);
        public Task SendGetTwinRequest(string correlationId) => this.handlerAsDeviceListener.SendGetTwinRequest(correlationId);
        public Task UpdateReportedPropertiesAsync(IMessage reportedPropertiesMessage, string correlationId) => this.handlerAsDeviceListener.UpdateReportedPropertiesAsync(reportedPropertiesMessage, correlationId);

        public bool IsActive => this.handlerAsDeviceProxy.IsActive;
        public Task CloseAsync(Exception ex) => this.handlerAsDeviceProxy.CloseAsync(ex);
        public Task<Option<IClientCredentials>> GetUpdatedIdentity() => this.handlerAsDeviceProxy.GetUpdatedIdentity();
        public Task<DirectMethodResponse> InvokeMethodAsync(DirectMethodRequest request) => this.handlerAsDeviceProxy.InvokeMethodAsync(request);
        public Task OnDesiredPropertyUpdates(IMessage desiredProperties) => this.handlerAsDeviceProxy.OnDesiredPropertyUpdates(desiredProperties);
        public Task SendC2DMessageAsync(IMessage message) => this.handlerAsDeviceProxy.SendC2DMessageAsync(message);
        public Task SendMessageAsync(IMessage message, string input) => this.handlerAsDeviceProxy.SendMessageAsync(message, input);
        public Task SendTwinUpdate(IMessage twin) => this.handlerAsDeviceProxy.SendTwinUpdate(twin);
        public void SetInactive() => this.handlerAsDeviceProxy.SetInactive();
    }
}
