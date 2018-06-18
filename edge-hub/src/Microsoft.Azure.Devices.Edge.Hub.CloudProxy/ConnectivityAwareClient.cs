// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// This implementation of IClient wraps an underlying IClient, and 
    /// reports success/failures of the IoTHub calls to IDeviceConnectivityManager.
    /// It also receives connectivity callbacks from IDeviceConnectivityManager
    /// and translates them to the ConnectionStatusChangedHandler.
    /// </summary>
    class ConnectivityAwareClient : IClient
    {
        readonly IClient underlyingClient;
        readonly IDeviceConnectivityManager deviceConnectivityManager;
        ConnectionStatusChangesHandler connectionStatusChangedHandler;

        public ConnectivityAwareClient(IClient client, IDeviceConnectivityManager deviceConnectivityManager)            
        {
            this.underlyingClient = Preconditions.CheckNotNull(client, nameof(client));
            this.deviceConnectivityManager = Preconditions.CheckNotNull(deviceConnectivityManager, nameof(deviceConnectivityManager));

            this.deviceConnectivityManager.DeviceConnected += (_, __) =>
                this.connectionStatusChangedHandler?.Invoke(ConnectionStatus.Connected, ConnectionStatusChangeReason.Connection_Ok);
            this.deviceConnectivityManager.DeviceDisconnected += (_, __) =>
                this.connectionStatusChangedHandler?.Invoke(ConnectionStatus.Disconnected, ConnectionStatusChangeReason.No_Network);
        }

        public bool IsActive => this.underlyingClient.IsActive;
        
        public Task CloseAsync() => this.underlyingClient.CloseAsync();

        // This method could throw and is not a reliable candidate to check connectivity status
        public Task RejectAsync(string messageId) => this.underlyingClient.RejectAsync(messageId);

        // This method could throw and is not a reliable candidate to check connectivity status
        public Task<Message> ReceiveAsync(TimeSpan receiveMessageTimeout) => this.underlyingClient.ReceiveAsync(receiveMessageTimeout);

        public Task CompleteAsync(string messageId) => this.InvokeFunc(() => this.underlyingClient.CompleteAsync(messageId), nameof(this.CompleteAsync));

        public Task AbandonAsync(string messageId) => this.InvokeFunc(() => this.underlyingClient.AbandonAsync(messageId), nameof(this.AbandonAsync));

        public Task<Twin> GetTwinAsync() => this.InvokeFunc(() => this.underlyingClient.GetTwinAsync(), nameof(this.GetTwinAsync));

        public Task OpenAsync() => this.InvokeFunc(() => this.underlyingClient.OpenAsync(), nameof(this.OpenAsync));

        public Task SendEventAsync(Client.Message message) => this.InvokeFunc(() => this.underlyingClient.SendEventAsync(message), nameof(this.SendEventAsync));

        public Task SendEventBatchAsync(IEnumerable<Client.Message> messages) =>
            this.InvokeFunc(() => this.underlyingClient.SendEventBatchAsync(messages), nameof(this.SendEventBatchAsync));

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
            => this.InvokeFunc(() => this.underlyingClient.SetMethodDefaultHandlerAsync(methodHandler, userContext), nameof(this.SetMethodDefaultHandlerAsync));

        public void SetOperationTimeoutInMilliseconds(uint operationTimeoutMilliseconds) => this.underlyingClient.SetOperationTimeoutInMilliseconds(operationTimeoutMilliseconds);

        public void SetProductInfo(string productInfo) => this.underlyingClient.SetProductInfo(productInfo);

        public Task UpdateReportedPropertiesAsync(TwinCollection reportedProperties) =>
            this.InvokeFunc(() => this.underlyingClient.UpdateReportedPropertiesAsync(reportedProperties), nameof(this.UpdateReportedPropertiesAsync));

        public void Dispose()
        {
            this.underlyingClient?.Dispose();
        }

        void InternalConnectionStatusChangedHandler(ConnectionStatus status, ConnectionStatusChangeReason reason)
        {
            Events.ReceivedDeviceSdkCallback(status, reason);
            // @TODO: Ignore callback from Device SDK since it seems to be generating a lot of spurious Connected/NotConnected callbacks
            //if (status == ConnectionStatus.Connected)
            //{
            //    this.deviceConnectivityManager.CallSucceeded();
            //}
            //else
            //{
            //    this.deviceConnectivityManager.CallTimedOut();
            //}
            //this.connectionStatusChangedHandler?.Invoke(status, reason);
        }        

        async Task<T> InvokeFunc<T>(Func<Task<T>> func, string operation)
        {
            try
            {
                T result = await func();
                this.deviceConnectivityManager.CallSucceeded();
                Events.OperationSucceeded(operation);
                return result;
            }
            catch (Exception ex)
            {                
                if (ex.HasTimeoutException())
                {
                    Events.OperationTimedOut(operation);
                    this.deviceConnectivityManager.CallTimedOut();
                }
                Events.OperationFailed(operation, ex);
                throw;
            }
        }

        Task InvokeFunc(Func<Task> func, string operation) => this.InvokeFunc(
            async () =>
            {
                await func();
                return true;
            },
            operation);

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<ConnectivityAwareClient>();
            const int IdStart = CloudProxyEventIds.ConnectivityAwareClient;

            enum EventIds
            {
                ReceivedCallback = IdStart,
                OperationTimedOut,
                OperationFailed,
                OperationSucceeded
            }
            
            public static void ReceivedDeviceSdkCallback(ConnectionStatus status, ConnectionStatusChangeReason reason)
            {
                Log.LogDebug((int)EventIds.ReceivedCallback, $"Received connection status changed callback with connection status {status} and reason {reason}");
            }

            public static void OperationTimedOut(string operation)
            {
                Log.LogDebug((int)EventIds.OperationTimedOut, $"Operation {operation} timed out");
            }

            public static void OperationSucceeded(string operation)
            {
                Log.LogDebug((int)EventIds.OperationSucceeded, $"Operation {operation} succeeded");
            }

            public static void OperationFailed(string operation, Exception ex)
            {
                Log.LogDebug((int)EventIds.OperationFailed, ex, $"Operation {operation} failed");
            }
        }
    }
}
