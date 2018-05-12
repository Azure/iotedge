// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.Shared;

    class ModuleClientWrapper : IClient
    {
        readonly ModuleClient underlyingModuleClient;
        readonly AtomicBoolean isActive;

        public ModuleClientWrapper(ModuleClient moduleClient)
        {
            this.underlyingModuleClient = moduleClient;
            this.isActive = new AtomicBoolean(false);
        }

        public bool IsActive => this.isActive;

        public Task AbandonAsync(string messageId) => this.underlyingModuleClient.AbandonAsync(messageId);

        public Task CloseAsync() => this.isActive.GetAndSet(false)
            ? this.underlyingModuleClient.CloseAsync()
            : Task.CompletedTask;

        public Task CompleteAsync(string messageId) => this.underlyingModuleClient.CompleteAsync(messageId);

        public void Dispose()
        {
            this.isActive.Set(false);
            this.underlyingModuleClient?.Dispose();
        }

        public Task<Twin> GetTwinAsync() => this.underlyingModuleClient.GetTwinAsync();

        public Task OpenAsync() => !this.isActive.GetAndSet(true)
            ? this.underlyingModuleClient.OpenAsync()
            : Task.CompletedTask;

        public Task SendEventAsync(Message message) => this.underlyingModuleClient.SendEventAsync(message);

        public Task SendEventBatchAsync(IEnumerable<Message> messages) => this.underlyingModuleClient.SendEventBatchAsync(messages);

        public void SetConnectionStatusChangedHandler(ConnectionStatusChangesHandler handler) => this.underlyingModuleClient.SetConnectionStatusChangesHandler(handler);

        public Task SetDesiredPropertyUpdateCallbackAsync(DesiredPropertyUpdateCallback onDesiredPropertyUpdates, object userContext)
            => this.underlyingModuleClient.SetDesiredPropertyUpdateCallbackAsync(onDesiredPropertyUpdates, userContext);

        public Task SetMethodDefaultHandlerAsync(MethodCallback methodHandler, object userContext)
            => this.underlyingModuleClient.SetMethodDefaultHandlerAsync(methodHandler, userContext);

        public void SetOperationTimeoutInMilliseconds(uint operationTimeoutMilliseconds) => this.underlyingModuleClient.OperationTimeoutInMilliseconds = operationTimeoutMilliseconds;

        public void SetProductInfo(string productInfo) => this.underlyingModuleClient.ProductInfo = productInfo;

        public Task UpdateReportedPropertiesAsync(TwinCollection reportedProperties) => this.underlyingModuleClient.UpdateReportedPropertiesAsync(reportedProperties);
    }
}
