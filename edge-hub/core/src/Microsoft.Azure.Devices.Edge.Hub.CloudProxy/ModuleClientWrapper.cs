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

    class ModuleClientWrapper : IClient
    {
        readonly IotHubModuleClient underlyingModuleClient;
        readonly AtomicBoolean isActive;

        public ModuleClientWrapper(IotHubModuleClient moduleClient)
        {
            this.underlyingModuleClient = moduleClient;
            this.isActive = new AtomicBoolean(true);
        }

        public bool IsActive => this.isActive;

        public Task AbandonAsync(string messageId) => this.underlyingModuleClient.AbandonAsync(messageId);

        public async Task CloseAsync()
        {
            if (this.isActive.GetAndSet(false))
            {
                await this.underlyingModuleClient.DisposeAsync();
            }
        }

        public Task RejectAsync(string messageId) => throw new InvalidOperationException("Reject is not supported for modules.");

        public Task<IncomingMessage> ReceiveAsync(TimeSpan receiveMessageTimeout) => throw new InvalidOperationException("C2D messages are not supported for modules.");

        public Task CompleteAsync(string messageId) => this.underlyingModuleClient.CompleteAsync(messageId);

        public void Dispose()
        {
            this.isActive.Set(false);
            this.underlyingModuleClient?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        public async ValueTask DisposeAsync()
        {
            this.isActive.Set(false);
            if (this.underlyingModuleClient != null)
            {
                await this.underlyingModuleClient.DisposeAsync();
            }
        }

        public Task<TwinProperties> GetTwinPropertiesAsync() => this.underlyingModuleClient.GetTwinPropertiesAsync();

        public async Task OpenAsync()
        {
            try
            {
                await this.underlyingModuleClient.OpenAsync().TimeoutAfter(TimeSpan.FromMinutes(2));
            }
            catch (Exception)
            {
                await this.CloseAsync();
                throw;
            }
        }

        public Task SendTelemetryAsync(TelemetryMessage message) => this.underlyingModuleClient.SendTelemetryAsync(message);

        public Task SendTelemetryAsync(IEnumerable<TelemetryMessage> messages) => this.underlyingModuleClient.SendTelemetryAsync(messages);

        public void SetConnectionStatusChangedHandler(Action<ConnectionStatusInfo> handler) =>
            this.underlyingModuleClient.ConnectionStatusChangeCallback = (info) => handler(info);

        public Task SetDesiredPropertyUpdateCallbackAsync(Func<PropertyCollection, Task> onDesiredPropertyUpdates)
            => this.underlyingModuleClient.SetDesiredPropertyUpdateCallbackAsync(onDesiredPropertyUpdates);

        public Task SetDirectMethodCallbackAsync(Func<Client.DirectMethodRequest, Task<Client.DirectMethodResponse>> methodHandler)
            => this.underlyingModuleClient.SetDirectMethodCallbackAsync(methodHandler);

        public void SetProductInfo(string productInfo)
        {
            // In v2 SDK, product info is set via IotHubClientOptions.AdditionalUserAgentInfo at construction time.
            // This is now a no-op as the client has already been created.
        }

        public Task UpdateReportedPropertiesAsync(PropertyCollection reportedProperties) => this.underlyingModuleClient.UpdateReportedPropertiesAsync(reportedProperties);
    }
}
