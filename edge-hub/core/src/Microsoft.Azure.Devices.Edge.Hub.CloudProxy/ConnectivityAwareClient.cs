// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
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
        readonly AtomicBoolean isConnected = new AtomicBoolean(false);
        readonly IIdentity identity;
        Action<ConnectionStatusInfo> connectionStatusChangedHandler;

        public ConnectivityAwareClient(IClient client, IDeviceConnectivityManager deviceConnectivityManager, IIdentity identity)
        {
            this.identity = Preconditions.CheckNotNull(identity, nameof(identity));
            this.underlyingClient = Preconditions.CheckNotNull(client, nameof(client));
            this.deviceConnectivityManager = Preconditions.CheckNotNull(deviceConnectivityManager, nameof(deviceConnectivityManager));
        }

        public bool IsActive => this.underlyingClient.IsActive;

        public async Task CloseAsync()
        {
            await this.underlyingClient.CloseAsync();
            this.isConnected.Set(false);
            this.deviceConnectivityManager.DeviceConnected -= this.HandleDeviceConnectedEvent;
            this.deviceConnectivityManager.DeviceDisconnected -= this.HandleDeviceDisconnectedEvent;
        }

        // This method could throw and is not a reliable candidate to check connectivity status
        public Task RejectAsync(string messageId) =>
            this.InvokeFunc(() => this.underlyingClient.RejectAsync(messageId), nameof(this.RejectAsync), false);

        // This method could throw and is not a reliable candidate to check connectivity status
        public Task<IncomingMessage> ReceiveAsync(TimeSpan receiveMessageTimeout) => this.underlyingClient.ReceiveAsync(receiveMessageTimeout);

        public Task CompleteAsync(string messageId) => this.InvokeFunc(() => this.underlyingClient.CompleteAsync(messageId), nameof(this.CompleteAsync));

        public Task AbandonAsync(string messageId) => this.InvokeFunc(() => this.underlyingClient.AbandonAsync(messageId), nameof(this.AbandonAsync));

        public Task<TwinProperties> GetTwinPropertiesAsync() => this.InvokeFunc(() => this.underlyingClient.GetTwinPropertiesAsync(), nameof(this.GetTwinPropertiesAsync));

        public async Task OpenAsync()
        {
            await this.InvokeFunc(() => this.underlyingClient.OpenAsync(), nameof(this.OpenAsync));
            this.deviceConnectivityManager.DeviceConnected += this.HandleDeviceConnectedEvent;
            this.deviceConnectivityManager.DeviceDisconnected += this.HandleDeviceDisconnectedEvent;
        }

        public Task SendTelemetryAsync(TelemetryMessage message) => this.InvokeFunc(() => this.underlyingClient.SendTelemetryAsync(message), nameof(this.SendTelemetryAsync));

        public Task SendTelemetryAsync(IEnumerable<TelemetryMessage> messages) =>
            this.InvokeFunc(() => this.underlyingClient.SendTelemetryAsync(messages), nameof(this.SendTelemetryAsync));

        public void SetConnectionStatusChangedHandler(Action<ConnectionStatusInfo> handler)
        {
            this.connectionStatusChangedHandler = handler;
            this.underlyingClient.SetConnectionStatusChangedHandler(this.InternalConnectionStatusChangedHandler);
        }

        // The SDK caches whether DesiredProperty Update callback has been set and returns directly in that case.
        // So this method is not a good candidate for checking connectivity status.
        public Task SetDesiredPropertyUpdateCallbackAsync(Func<PropertyCollection, Task> onDesiredPropertyUpdates)
            => this.InvokeFunc(() => this.underlyingClient.SetDesiredPropertyUpdateCallbackAsync(onDesiredPropertyUpdates), nameof(this.SetDesiredPropertyUpdateCallbackAsync), false);

        public Task SetDirectMethodCallbackAsync(Func<Client.DirectMethodRequest, Task<Client.DirectMethodResponse>> methodHandler)
            => this.InvokeFunc(() => this.underlyingClient.SetDirectMethodCallbackAsync(methodHandler), nameof(this.SetDirectMethodCallbackAsync));

        public void SetProductInfo(string productInfo) => this.underlyingClient.SetProductInfo(productInfo);

        public Task UpdateReportedPropertiesAsync(PropertyCollection reportedProperties) =>
            this.InvokeFunc(() => this.underlyingClient.UpdateReportedPropertiesAsync(reportedProperties), nameof(this.UpdateReportedPropertiesAsync));

        public void Dispose()
        {
            this.deviceConnectivityManager.DeviceConnected -= this.HandleDeviceConnectedEvent;
            this.deviceConnectivityManager.DeviceDisconnected -= this.HandleDeviceDisconnectedEvent;
            this.isConnected.Set(false);
            this.underlyingClient?.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            this.deviceConnectivityManager.DeviceConnected -= this.HandleDeviceConnectedEvent;
            this.deviceConnectivityManager.DeviceDisconnected -= this.HandleDeviceDisconnectedEvent;
            this.isConnected.Set(false);
            if (this.underlyingClient != null)
            {
                await this.underlyingClient.DisposeAsync();
            }
        }

        void HandleDeviceConnectedEvent(object sender, EventArgs eventArgs) => this.HandleDeviceConnectedEvent();

        void HandleDeviceConnectedEvent()
        {
            if (!this.isConnected.GetAndSet(true))
            {
                Events.ChangingStatus(this.isConnected, this.identity);
                this.connectionStatusChangedHandler?.Invoke(
                    new ConnectionStatusInfo(ConnectionStatus.Connected, ConnectionStatusChangeReason.ConnectionOk));
            }
        }

        void HandleDeviceDisconnectedEvent(object sender, EventArgs eventArgs) => this.HandleDeviceDisconnectedEvent();

        void HandleDeviceDisconnectedEvent()
        {
            if (this.isConnected.GetAndSet(false))
            {
                Events.ChangingStatus(this.isConnected, this.identity);
                this.connectionStatusChangedHandler?.Invoke(
                    new ConnectionStatusInfo(ConnectionStatus.Disconnected, ConnectionStatusChangeReason.CommunicationError));
            }
        }

        void InternalConnectionStatusChangedHandler(ConnectionStatusInfo statusInfo)
        {
            Events.ReceivedDeviceSdkCallback(this.identity, statusInfo.Status, statusInfo.ChangeReason);
            // @TODO: Ignore callback from Device SDK since it seems to be generating a lot of spurious Connected/NotConnected callbacks
            /*
            if (statusInfo.Status == ConnectionStatus.Connected)
            {
                this.deviceConnectivityManager.CallSucceeded();
                this.HandleDeviceConnectedEvent();
            }
            else if (statusInfo.Status == ConnectionStatus.Disconnected || statusInfo.Status == ConnectionStatus.Disabled)
            {
                this.deviceConnectivityManager.CallTimedOut();
                this.HandleDeviceDisconnectedEvent();
            }
            this.connectionStatusChangedHandler?.Invoke(statusInfo);
            */
        }

        async Task<T> InvokeFunc<T>(Func<Task<T>> func, string operation, bool useForConnectivityCheck = true)
        {
            try
            {
                T result = await func();
                if (useForConnectivityCheck)
                {
                    await this.deviceConnectivityManager.CallSucceeded();
                }

                this.HandleDeviceConnectedEvent(this, EventArgs.Empty);
                Events.OperationSucceeded(this.identity, operation);
                return result;
            }
            catch (Exception ex)
            {
                Exception mappedException = ex.GetEdgeException(operation);
                if (mappedException.HasTimeoutException())
                {
                    Events.OperationTimedOut(this.identity, operation);
                    if (useForConnectivityCheck)
                    {
                        await this.deviceConnectivityManager.CallTimedOut();
                    }
                }
                else if (ex.IsFailOver())
                {
                    Events.FailOverDetected(this.identity, operation, mappedException);
                    if (useForConnectivityCheck)
                    {
                        await this.deviceConnectivityManager.CallTimedOut();
                    }
                }
                else
                {
                    Events.OperationFailed(this.identity, operation, mappedException);
                }

                if (mappedException == ex)
                {
                    throw;
                }
                else
                {
                    throw mappedException;
                }
            }
        }

        Task InvokeFunc(Func<Task> func, string operation, bool useForConnectivityCheck = true) => this.InvokeFunc(
            async () =>
            {
                await func();
                return true;
            },
            operation,
            useForConnectivityCheck);

        static class Events
        {
            const int IdStart = CloudProxyEventIds.ConnectivityAwareClient;
            static readonly ILogger Log = Logger.Factory.CreateLogger<ConnectivityAwareClient>();

            enum EventIds
            {
                ReceivedCallback = IdStart,
                OperationTimedOut,
                OperationFailed,
                OperationSucceeded,
                ChangingStatus,
                FailOverDetected
            }

            public static void ReceivedDeviceSdkCallback(IIdentity identity, ConnectionStatus status, ConnectionStatusChangeReason reason)
            {
                Log.LogDebug((int)EventIds.ReceivedCallback, $"Received connection status changed callback with connection status {status} and reason {reason} for {identity.Id}");
            }

            public static void OperationTimedOut(IIdentity identity, string operation)
            {
                Log.LogDebug((int)EventIds.OperationTimedOut, $"Operation {operation} timed out for {identity.Id}");
            }

            public static void OperationSucceeded(IIdentity identity, string operation)
            {
                Log.LogDebug((int)EventIds.OperationSucceeded, $"Operation {operation} succeeded for {identity.Id}");
            }

            public static void OperationFailed(IIdentity identity, string operation, Exception ex)
            {
                Log.LogDebug((int)EventIds.OperationFailed, ex, $"Operation {operation} failed for {identity.Id}");
            }

            public static void ChangingStatus(AtomicBoolean isConnected, IIdentity identity)
            {
                Log.LogInformation((int)EventIds.ChangingStatus, $"Cloud connection for {identity.Id} is {isConnected.Get()}");
            }

            public static void FailOverDetected(IIdentity identity, string operation, Exception ex)
            {
                Log.LogInformation((int)EventIds.FailOverDetected, ex, $"Operation {operation} failed for {identity.Id} because of fail-over");
            }
        }
    }
}
