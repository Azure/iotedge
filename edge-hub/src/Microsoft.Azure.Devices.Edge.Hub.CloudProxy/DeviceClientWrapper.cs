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
    using Microsoft.Extensions.Logging;

    class DeviceClientWrapper : IClient
    {
        static readonly ILogger Log = Logger.Factory.CreateLogger<DeviceClientWrapper>();

        readonly DeviceClient underlyingDeviceClient;
        readonly AtomicBoolean isActive;
        readonly string deviceId;

        public DeviceClientWrapper(DeviceClient deviceClient, string deviceId = null)
        {
            this.underlyingDeviceClient = Preconditions.CheckNotNull(deviceClient);
            this.isActive = new AtomicBoolean(true);
            this.deviceId = deviceId;
        }

        public bool IsActive => this.isActive;

        public Task AbandonAsync(string messageId) => this.underlyingDeviceClient.AbandonAsync(messageId);

        public Task CloseAsync()
        {
            if (this.isActive.GetAndSet(false))
            {
                Debugging($"Before close DeviceClient {this.underlyingDeviceClient.GetHashCode()} of device {this.deviceId}.");
                this.underlyingDeviceClient?.Dispose();
                Debugging($"After close DeviceClient {this.underlyingDeviceClient.GetHashCode()} of device {this.deviceId}.");
            }

            return Task.CompletedTask;
        }

        public Task CompleteAsync(string messageId) => this.underlyingDeviceClient.CompleteAsync(messageId);

        public void Dispose()
        {
            Debugging($"Dispose DeviceClient {this.underlyingDeviceClient.GetHashCode()} of device {this.deviceId}.");
            this.isActive.Set(false);
            this.underlyingDeviceClient?.Dispose();
        }

        public Task<Twin> GetTwinAsync() => this.underlyingDeviceClient.GetTwinAsync();

        public async Task OpenAsync()
        {
            try
            {
                Debugging($"Before connect DeviceClient {this.underlyingDeviceClient.GetHashCode()} of device {this.deviceId}.");
                await this.underlyingDeviceClient.OpenAsync().TimeoutAfter(TimeSpan.FromMinutes(2));
                Debugging($"After connect DeviceClient {this.underlyingDeviceClient.GetHashCode()} of device {this.deviceId}.");
            }
            catch (Exception e)
            {
                Debugging($"After connect DeviceClient {this.underlyingDeviceClient.GetHashCode()} of device {this.deviceId} failed with error {e}.");
                this.isActive.Set(false);
                this.underlyingDeviceClient?.Dispose();
                throw;
            }
        }

        public Task<Message> ReceiveAsync(TimeSpan receiveMessageTimeout) => this.underlyingDeviceClient.ReceiveAsync(receiveMessageTimeout);

        public Task RejectAsync(string messageId) => this.underlyingDeviceClient.RejectAsync(messageId);

        public async Task SendEventAsync(Message message)
        {
            try
            {
                Debugging($"Before SendEventAsync with DeviceClient {this.underlyingDeviceClient.GetHashCode()} of device {this.deviceId}.");
                await this.underlyingDeviceClient.SendEventAsync(message);
                Debugging($"After SendEventAsync with DeviceClient {this.underlyingDeviceClient.GetHashCode()} of device {this.deviceId}.");
            }
            catch (Exception e)
            {
                Debugging($"After SendEventAsync with DeviceClient {this.underlyingDeviceClient.GetHashCode()} of device {this.deviceId} failed with error {e}.");
                throw;
            }
        }

        public async Task SendEventBatchAsync(IEnumerable<Message> messages)
        {
            try
            {
                Debugging($"Before SendEventBatchAsync with DeviceClient {this.underlyingDeviceClient.GetHashCode()} of device {this.deviceId}.");
                await this.underlyingDeviceClient.SendEventBatchAsync(messages);
                Debugging($"After SendEventBatchAsync with DeviceClient {this.underlyingDeviceClient.GetHashCode()} of device {this.deviceId}.");
            }
            catch (Exception e)
            {
                Debugging($"After SendEventBatchAsync with DeviceClient {this.underlyingDeviceClient.GetHashCode()} of device {this.deviceId} failed with error {e}.");
                throw;
            }
        }

        public void SetConnectionStatusChangedHandler(ConnectionStatusChangesHandler handler) => this.underlyingDeviceClient.SetConnectionStatusChangesHandler(handler);

        public Task SetDesiredPropertyUpdateCallbackAsync(DesiredPropertyUpdateCallback onDesiredPropertyUpdates, object userContext)
            => this.underlyingDeviceClient.SetDesiredPropertyUpdateCallbackAsync(onDesiredPropertyUpdates, userContext);

        public Task SetMethodDefaultHandlerAsync(MethodCallback methodHandler, object userContext)
            => this.underlyingDeviceClient.SetMethodDefaultHandlerAsync(methodHandler, userContext);

        public void SetOperationTimeoutInMilliseconds(uint operationTimeoutMilliseconds) => this.underlyingDeviceClient.OperationTimeoutInMilliseconds = operationTimeoutMilliseconds;

        public void SetProductInfo(string productInfo) => this.underlyingDeviceClient.ProductInfo = productInfo;

        public Task UpdateReportedPropertiesAsync(TwinCollection reportedProperties) => this.underlyingDeviceClient.UpdateReportedPropertiesAsync(reportedProperties);

        static void Debugging(string message)
        {
            Log.LogError($"[Debugging]-[DeviceClientWrapper]: {message}");
        }
    }
}
