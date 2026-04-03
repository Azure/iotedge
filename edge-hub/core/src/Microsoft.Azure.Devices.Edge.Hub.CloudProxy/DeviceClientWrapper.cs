// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;

    class DeviceClientWrapper : IClient
    {
        readonly IotHubDeviceClient underlyingDeviceClient;
        readonly AtomicBoolean isActive;

        public DeviceClientWrapper(IotHubDeviceClient deviceClient)
        {
            this.underlyingDeviceClient = Preconditions.CheckNotNull(deviceClient);
            this.isActive = new AtomicBoolean(true);
        }

        public bool IsActive => this.isActive;

        public Task AbandonAsync(string messageId) => this.underlyingDeviceClient.AbandonAsync(messageId);

        public async Task CloseAsync()
        {
            if (this.isActive.GetAndSet(false))
            {
                await this.underlyingDeviceClient.DisposeAsync();
            }
        }

        public Task CompleteAsync(string messageId) => this.underlyingDeviceClient.CompleteAsync(messageId);

        public void Dispose()
        {
            this.isActive.Set(false);
            this.underlyingDeviceClient?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        public async ValueTask DisposeAsync()
        {
            this.isActive.Set(false);
            if (this.underlyingDeviceClient != null)
            {
                await this.underlyingDeviceClient.DisposeAsync();
            }
        }

        public Task<TwinProperties> GetTwinPropertiesAsync() => this.underlyingDeviceClient.GetTwinPropertiesAsync();

        public async Task OpenAsync()
        {
            try
            {
                await this.underlyingDeviceClient.OpenAsync().TimeoutAfter(TimeSpan.FromMinutes(2));
            }
            catch (Exception)
            {
                await this.CloseAsync();
                throw;
            }
        }

        public Task<IncomingMessage> ReceiveAsync(TimeSpan receiveMessageTimeout)
        {
            using var cts = new CancellationTokenSource(receiveMessageTimeout);
            return this.underlyingDeviceClient.ReceiveMessageAsync(cts.Token);
        }

        public Task RejectAsync(string messageId) => this.underlyingDeviceClient.RejectAsync(messageId);

        public Task SendTelemetryAsync(TelemetryMessage message) => this.underlyingDeviceClient.SendTelemetryAsync(message);

        public Task SendTelemetryAsync(IEnumerable<TelemetryMessage> messages) => this.underlyingDeviceClient.SendTelemetryAsync(messages);

        public void SetConnectionStatusChangedHandler(Action<ConnectionStatusInfo> handler) =>
            this.underlyingDeviceClient.ConnectionStatusChangeCallback = (info) => handler(info);

        public Task SetDesiredPropertyUpdateCallbackAsync(Func<PropertyCollection, Task> onDesiredPropertyUpdates)
            => this.underlyingDeviceClient.SetDesiredPropertyUpdateCallbackAsync(onDesiredPropertyUpdates);

        public Task SetDirectMethodCallbackAsync(Func<Client.DirectMethodRequest, Task<Client.DirectMethodResponse>> methodHandler)
            => this.underlyingDeviceClient.SetDirectMethodCallbackAsync(methodHandler);

        public void SetProductInfo(string productInfo)
        {
            // In v2 SDK, product info is set via IotHubClientOptions.AdditionalUserAgentInfo at construction time.
            // This is now a no-op as the client has already been created.
        }

        public Task UpdateReportedPropertiesAsync(PropertyCollection reportedProperties) => this.underlyingDeviceClient.UpdateReportedPropertiesAsync(reportedProperties);
    }
}
