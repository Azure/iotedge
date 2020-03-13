// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.Shared;

    class DeviceClientWrapper : IClient
    {
        readonly DeviceClient underlyingDeviceClient;
        readonly AtomicBoolean isActive;

        public DeviceClientWrapper(DeviceClient deviceClient)
        {
            this.underlyingDeviceClient = Preconditions.CheckNotNull(deviceClient);
            this.isActive = new AtomicBoolean(true);
        }

        public bool IsActive => this.isActive;

        public Task AbandonAsync(string messageId) => this.underlyingDeviceClient.AbandonAsync(messageId);

        public Task CloseAsync()
        {
            if (this.isActive.GetAndSet(false))
            {
                this.underlyingDeviceClient?.Dispose();
            }

            return Task.CompletedTask;
        }

        public Task CompleteAsync(string messageId) => this.underlyingDeviceClient.CompleteAsync(messageId);

        public void Dispose()
        {
            this.isActive.Set(false);
            this.underlyingDeviceClient?.Dispose();
        }

        public Task<Twin> GetTwinAsync() => this.underlyingDeviceClient.GetTwinAsync();

        public async Task OpenAsync()
        {
            try
            {
                await this.underlyingDeviceClient.OpenAsync().TimeoutAfter(TimeSpan.FromMinutes(2));
            }
            catch (Exception)
            {
                this.isActive.Set(false);
                this.underlyingDeviceClient?.Dispose();
                throw;
            }
        }

        public Task<Message> ReceiveAsync(TimeSpan receiveMessageTimeout) => this.underlyingDeviceClient.ReceiveAsync(receiveMessageTimeout);

        public Task RejectAsync(string messageId) => this.underlyingDeviceClient.RejectAsync(messageId);

        public Task SendEventAsync(Message message) => this.underlyingDeviceClient.SendEventAsync(message);

        public Task SendEventBatchAsync(IEnumerable<Message> messages) => this.underlyingDeviceClient.SendEventBatchAsync(messages);

        public void SetConnectionStatusChangedHandler(ConnectionStatusChangesHandler handler) => this.underlyingDeviceClient.SetConnectionStatusChangesHandler(handler);

        public Task SetDesiredPropertyUpdateCallbackAsync(DesiredPropertyUpdateCallback onDesiredPropertyUpdates, object userContext)
            => this.underlyingDeviceClient.SetDesiredPropertyUpdateCallbackAsync(onDesiredPropertyUpdates, userContext);

        public Task SetMethodDefaultHandlerAsync(MethodCallback methodHandler, object userContext)
            => this.underlyingDeviceClient.SetMethodDefaultHandlerAsync(methodHandler, userContext);

        public void SetOperationTimeoutInMilliseconds(uint operationTimeoutMilliseconds) => this.underlyingDeviceClient.OperationTimeoutInMilliseconds = operationTimeoutMilliseconds;

        public void SetProductInfo(string productInfo) => this.underlyingDeviceClient.ProductInfo = productInfo;

        public Task UpdateReportedPropertiesAsync(TwinCollection reportedProperties) => this.underlyingDeviceClient.UpdateReportedPropertiesAsync(reportedProperties);
    }
}
