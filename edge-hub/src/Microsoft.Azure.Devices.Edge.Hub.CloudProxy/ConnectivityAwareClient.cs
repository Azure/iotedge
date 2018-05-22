// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.Shared;

    /// <summary>
    /// This implementation of IClient wraps an underlying IClient, and 
    /// reports success/failures of the IoTHub calls to IDeviceConnectivityManager.
    /// It also receives connectivity callbacks from IDeviceConnectivityManager
    /// and translates them to the ConnectionStatusChangedHandler.
    /// </summary>
    class ConnectivityAwareClient : IClient
    {
        readonly IClient underlyingClient;
        readonly AtomicBoolean isActive;
        readonly IDeviceConnectivityManager deviceConnectivityManager;
        ConnectionStatusChangesHandler connectionStatusChangedHandler;

        public ConnectivityAwareClient(IClient client, IDeviceConnectivityManager deviceConnectivityManager)            
        {
            this.underlyingClient = Preconditions.CheckNotNull(client, nameof(client));
            this.isActive = new AtomicBoolean(false);
            this.deviceConnectivityManager = Preconditions.CheckNotNull(deviceConnectivityManager, nameof(deviceConnectivityManager));

            this.deviceConnectivityManager.DeviceConnected += (_, __) =>
                this.connectionStatusChangedHandler?.Invoke(ConnectionStatus.Connected, ConnectionStatusChangeReason.Connection_Ok);
            this.deviceConnectivityManager.DeviceDisconnected += (_, __) =>
                this.connectionStatusChangedHandler?.Invoke(ConnectionStatus.Disconnected, ConnectionStatusChangeReason.No_Network);
        }

        public bool IsActive => this.isActive;
        
        public Task CloseAsync() => this.isActive.GetAndSet(false)
            ? this.underlyingClient.CloseAsync()
            : Task.CompletedTask;

        // This method could throw and is not a reliable candidate to check connectivity status
        public Task RejectAsync(string messageId) => this.underlyingClient.RejectAsync(messageId);

        // This method could throw and is not a reliable candidate to check connectivity status
        public Task<Message> ReceiveAsync(TimeSpan receiveMessageTimeout) => this.underlyingClient.ReceiveAsync(receiveMessageTimeout);

        public Task CompleteAsync(string messageId) => this.InvokeFunc(() => this.underlyingClient.CompleteAsync(messageId));

        public Task AbandonAsync(string messageId) => this.InvokeFunc(() => this.underlyingClient.AbandonAsync(messageId));

        public Task<Twin> GetTwinAsync() => this.InvokeFunc(() => this.underlyingClient.GetTwinAsync());

        public Task OpenAsync() => !this.isActive.GetAndSet(true)
            ? this.InvokeFunc(() => this.underlyingClient.OpenAsync())
            : Task.CompletedTask;

        public Task SendEventAsync(Client.Message message) => this.InvokeFunc(() => this.underlyingClient.SendEventAsync(message));

        public Task SendEventBatchAsync(IEnumerable<Client.Message> messages) =>
            this.InvokeFunc(() => this.underlyingClient.SendEventBatchAsync(messages));

        public void SetConnectionStatusChangedHandler(ConnectionStatusChangesHandler handler)
        {
            this.connectionStatusChangedHandler = handler;
            this.underlyingClient.SetConnectionStatusChangedHandler(this.InternalConnectionStatusChangedHandler);
        }

        // The SDK caches whether DesiredProperty Update callback has been set and returns directly in that case.
        // So this method is not a good candidate for checking connectivity status. 
        public Task SetDesiredPropertyUpdateCallbackAsync(DesiredPropertyUpdateCallback onDesiredPropertyUpdates, object userContext)
            => this.underlyingClient.SetDesiredPropertyUpdateCallbackAsync(onDesiredPropertyUpdates, userContext);

        public Task SetMethodDefaultHandlerAsync(MethodCallback methodHandler, object userContext)
            => this.InvokeFunc(() => this.underlyingClient.SetMethodDefaultHandlerAsync(methodHandler, userContext));

        public void SetOperationTimeoutInMilliseconds(uint operationTimeoutMilliseconds) => this.underlyingClient.SetOperationTimeoutInMilliseconds(operationTimeoutMilliseconds);

        public void SetProductInfo(string productInfo) => this.underlyingClient.SetProductInfo(productInfo);

        public Task UpdateReportedPropertiesAsync(TwinCollection reportedProperties) =>
            this.InvokeFunc(() => this.underlyingClient.UpdateReportedPropertiesAsync(reportedProperties));

        public void Dispose()
        {
            this.isActive.Set(false);
            this.underlyingClient?.Dispose();
        }

        void InternalConnectionStatusChangedHandler(ConnectionStatus status, ConnectionStatusChangeReason reason)
        {            
            if (status == ConnectionStatus.Connected)
            {
                this.deviceConnectivityManager.CallSucceeded();
            }
            else
            {
                this.deviceConnectivityManager.CallTimedOut();
            }
            this.connectionStatusChangedHandler?.Invoke(status, reason);
        }

        async Task<T> InvokeFunc<T>(Func<Task<T>> func)
        {
            try
            {
                T result = await func();
                this.deviceConnectivityManager.CallSucceeded();
                return result;
            }
            catch (Exception ex)
            {
                if (ex.HasTimeoutException())
                {
                    this.deviceConnectivityManager.CallTimedOut();
                }
                throw;
            }
        }

        Task InvokeFunc(Func<Task> func) => this.InvokeFunc(
            async () =>
            {
                await func();
                return true;
            });
    }
}
