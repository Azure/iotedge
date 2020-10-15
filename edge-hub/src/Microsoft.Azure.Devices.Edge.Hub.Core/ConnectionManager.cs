// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Reflection.Metadata.Ecma335;
    using System.Threading;
    using System.Threading.Tasks;
    using App.Metrics;
    using App.Metrics.Gauge;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Nito.AsyncEx;
    using static System.FormattableString;
    using AsyncLock = Microsoft.Azure.Devices.Edge.Util.Concurrency.AsyncLock;

    public class ConnectionManager : IConnectionManager
    {
        const int DefaultMaxClients = 101; // 100 Clients + 1 Edgehub
        static readonly TimeSpan OperationTimeOut = TimeSpan.FromMinutes(2);
        readonly SemaphoreSlim stateLock = new SemaphoreSlim(1);
        readonly ConcurrentDictionary<IIdentity, DeviceBridge> deviceBridges = new ConcurrentDictionary<IIdentity, DeviceBridge>();
        readonly ICloudConnectionProvider cloudConnectionProvider;
        readonly int maxClients;
        readonly ICredentialsCache credentialsCache;
        readonly IIdentityProvider identityProvider;
        readonly IDeviceConnectivityManager connectivityManager;
        readonly bool closeCloudConnectionOnDeviceDisconnect;

        public ConnectionManager(
            ICloudConnectionProvider cloudConnectionProvider,
            ICredentialsCache credentialsCache,
            IIdentityProvider identityProvider,
            IDeviceConnectivityManager connectivityManager,
            int maxClients = DefaultMaxClients,
            bool closeCloudConnectionOnDeviceDisconnect = true)
        {
            this.cloudConnectionProvider = Preconditions.CheckNotNull(cloudConnectionProvider, nameof(cloudConnectionProvider));
            this.maxClients = Preconditions.CheckRange(maxClients, 1, nameof(maxClients));
            this.credentialsCache = Preconditions.CheckNotNull(credentialsCache, nameof(credentialsCache));
            this.identityProvider = Preconditions.CheckNotNull(identityProvider, nameof(identityProvider));
            this.connectivityManager = Preconditions.CheckNotNull(connectivityManager, nameof(connectivityManager));
            this.connectivityManager.DeviceDisconnected += (o, args) => this.HandleCloudConnectivityLostAsync();
            Util.Metrics.MetricsV0.RegisterGaugeCallback(() => MetricsV0.SetConnectedClientCountGauge(this));
            this.closeCloudConnectionOnDeviceDisconnect = closeCloudConnectionOnDeviceDisconnect;
        }

        public event EventHandler<IIdentity> CloudConnectionEstablished;

        public event EventHandler<IIdentity> CloudConnectionLost;

        public event EventHandler<IIdentity> DeviceConnected;

        public event EventHandler<IIdentity> DeviceDisconnected;

        public IEnumerable<IIdentity> GetConnectedClients()
        {
            if (this.stateLock.Wait(OperationTimeOut))
            {
                try
                {
                    return this.deviceBridges.Values
                        .Where(db => db.IsDeviceProxyActive())
                        .Select(db => db.Identity);
                }
                finally
                {
                    this.stateLock.Release();
                }
            }
            else
            {
                throw new TimeoutException($"GetConnectedClients timedout in {OperationTimeOut}.");
            }
        }

        public async Task AddDeviceConnection(IIdentity identity, IDeviceProxy deviceProxy)
        {
            Preconditions.CheckNotNull(identity, nameof(identity));
            Preconditions.CheckNotNull(deviceProxy, nameof(deviceProxy));
            Events.NewDeviceConnection(identity);
            await this.RetrieveDeviceBridge(identity, true)
                .Expect(() => new EdgeHubConnectionException($"A connection for device {identity.Id} not found."))
                .ReplaceDeviceProxyAsync(deviceProxy);
            this.DeviceConnected?.Invoke(this, identity);
        }

        public async Task RemoveDeviceConnection(string id)
        {
            var identity = this.ConvertIdToIdentity(id);
            await this.RetrieveDeviceBridge(identity, false)
                 .Map(db => db.CloseDeviceProxyAsync())
                 .GetOrElse(Task.CompletedTask);
            this.DeviceDisconnected?.Invoke(this, identity);
        }

        public Option<IDeviceProxy> GetDeviceConnection(string id) => this.RetrieveDeviceBridge(this.ConvertIdToIdentity(id), false)
                .FlatMap(db => Option.Maybe(db.GetDeviceProxy()));

        public Task<Option<ICloudProxy>> GetCloudConnection(string id) => this.RetrieveDeviceBridge(this.ConvertIdToIdentity(id), true)
                .Map(db => db.RetrieveCloudProxyAsync())
                .GetOrElse(() => Task.FromResult(Option.None<ICloudProxy>()));

        public void AddSubscription(string id, DeviceSubscription deviceSubscription)
        {
            if (!this.deviceBridges.TryGetValue(this.ConvertIdToIdentity(id), out var deviceBridge))
            {
                throw new ArgumentException($"A connection for {id} not found.");
            }

            deviceBridge.AddSubscription(deviceSubscription);
        }

        public void RemoveSubscription(string id, DeviceSubscription deviceSubscription) => this.RetrieveDeviceBridge(this.ConvertIdToIdentity(id), false)
                .Expect(() => new ArgumentException($"A connection for device {id} not found."))
                .RemoveSubscription(deviceSubscription);

        public Option<IReadOnlyDictionary<DeviceSubscription, bool>> GetSubscriptions(string id) => this.RetrieveDeviceBridge(this.ConvertIdToIdentity(id), false)
                .Map(db => db.GetSubscriptions())
                .Map(subscriptions => subscriptions.ToDictionary(subscription => subscription, _ => true))
                .Map(subscriptions => new ReadOnlyDictionary<DeviceSubscription, bool>(subscriptions) as IReadOnlyDictionary<DeviceSubscription, bool>);

        public bool CheckClientSubscription(string id, DeviceSubscription subscription) => this.RetrieveDeviceBridge(this.ConvertIdToIdentity(id), false)
                .Exists(db => db.CheckClientSubscription(subscription));

        public async Task<ITry<ICloudProxy>> CreateCloudConnectionAsync(IClientCredentials credentials)
        {
            // This function only be called as cloud authenticate. We're going to create a new ConnectedDevice instance and connect to cloud.
            // It should replace existing instance if succeed
            Preconditions.CheckNotNull(credentials, nameof(credentials));
            var deviceBridge = new DeviceBridge(credentials, this.closeCloudConnectionOnDeviceDisconnect, this.cloudConnectionProvider, this.OnCloudConnectionStatusChanged);
            var cloudProxy = await deviceBridge.TryRetrieveCloudProxyAsync();

            if (cloudProxy.Success)
            {
                await this.ReplaceDeviceBridgeAsync(credentials.Identity, deviceBridge);
            }

            return cloudProxy;
        }

        async void HandleCloudConnectivityLostAsync()
        {
            var closeUpstreamConnectionTasks = new List<Task>();
            if (this.stateLock.Wait(OperationTimeOut))
            {
                try
                {
                    Events.CloudConnectionLostClosingAllClients();
                    foreach (var deviceConnectionState in this.deviceBridges.Values)
                    {
                        Events.CloudConnectionLostClosingClient(deviceConnectionState.Identity);
                        closeUpstreamConnectionTasks.Add(deviceConnectionState.CloseCloudProxyAsync());
                    }
                }
                finally
                {
                    this.stateLock.Release();
                }
            }
            else
            {
                throw new TimeoutException($"OnCloudConnectivityLost timedout in {OperationTimeOut}.");
            }

            await closeUpstreamConnectionTasks.WhenAll();
        }

        async Task ReplaceDeviceBridgeAsync(IIdentity identity, DeviceBridge deviceBridge)
        {
            DeviceBridge replacedDeviceBridge;
            if (this.stateLock.Wait(OperationTimeOut))
            {
                try
                {
                    this.deviceBridges.TryRemove(identity, out replacedDeviceBridge);
                    this.deviceBridges[identity] = deviceBridge;
                }
                finally
                {
                    this.stateLock.Release();
                }

                Events.InvokingCloudConnectionEstablishedEvent(identity);
                this.CloudConnectionEstablished?.Invoke(this, identity);
            }
            else
            {
                throw new TimeoutException($"ReplaceDeviceBridgeAsync {identity} timedout in {OperationTimeOut}.");
            }

            if (replacedDeviceBridge != null)
            {
                await Task.WhenAll(replacedDeviceBridge.CloseCloudProxyAsync(), replacedDeviceBridge.CloseDeviceProxyAsync());
            }
        }

        Option<DeviceBridge> RetrieveDeviceBridge(IIdentity identity, bool createIfAbsent)
        {
            if (this.stateLock.Wait(OperationTimeOut))
            {
                try
                {
                    this.deviceBridges.TryGetValue(Preconditions.CheckNotNull(identity, nameof(identity)), out var deviceBridge);
                    if (createIfAbsent && deviceBridge == null)
                    {
                        if (this.deviceBridges.Values.Count(db => db.IsDeviceProxyActive()) >= this.maxClients)
                        {
                            throw new EdgeHubConnectionException($"Edge hub already has maximum allowed clients ({this.maxClients - 1}) connected.");
                        }

                        deviceBridge = new DeviceBridge(identity, this.closeCloudConnectionOnDeviceDisconnect, this.cloudConnectionProvider, this.OnCloudConnectionStatusChanged);
                        this.deviceBridges[identity] = deviceBridge;
                    }

                    return Option.Maybe(deviceBridge);
                }
                finally
                {
                    this.stateLock.Release();
                }
            }
            else
            {
                throw new TimeoutException($"RetrieveDeviceBridge {identity} timedout in {OperationTimeOut}.");
            }
        }

        void OnCloudConnectionStatusChanged(DeviceBridge deviceBridge, CloudConnectionStatus connectionStatus)
        {
            var identity = deviceBridge.Identity;
            Events.HandlingConnectionStatusChangedHandler(identity.Id, connectionStatus);
            if (!this.deviceBridges.TryGetValue(identity, out var db) || db != deviceBridge)
            {
                Events.Debugging($"Ignored: Device {identity} not found in the list of connected devices, it's during switching device bridge instance.");
                return;
            }

            switch (connectionStatus)
            {
                case CloudConnectionStatus.TokenNearExpiry:
                case CloudConnectionStatus.DisconnectedTokenExpired:
                case CloudConnectionStatus.Disconnected:
                    Events.InvokingCloudConnectionLostEvent(identity);
                    this.CloudConnectionLost?.Invoke(this, identity);
                    break;
                case CloudConnectionStatus.ConnectionEstablished:
                    Events.InvokingCloudConnectionEstablishedEvent(identity);
                    this.CloudConnectionEstablished?.Invoke(this, identity);
                    break;
            }
        }

        IIdentity ConvertIdToIdentity(string id) => this.identityProvider.Create(Preconditions.CheckNonWhiteSpace(id, nameof(id)));

        static class Events
        {
            const int IdStart = HubCoreEventIds.ConnectionManager;
            static readonly ILogger Log = Logger.Factory.CreateLogger<ConnectionManager>();

            enum EventIds
            {
                CreateNewCloudConnection = IdStart,
                NewDeviceConnection,
                RemovingDeviceConnection,
                RemoveDeviceConnection,
                CreateNewCloudConnectionError,
                ObtainedCloudConnection,
                ObtainCloudConnectionError,
                ProcessingTokenNearExpiryEvent,
                InvokingCloudConnectionLostEvent,
                InvokingCloudConnectionEstablishedEvent,
                HandlingConnectionStatusChangedHandler,
                CloudConnectionLostClosingClient,
                CloudConnectionLostClosingAllClients,
                GettingCloudConnectionForDeviceSubscriptions,
                Debugging
            }

            public static void Debugging(string message) => Log.LogDebug($"[ConnectionManager]: {message}");

            public static void NewCloudConnection(IIdentity identity, ITry<ICloudConnection> cloudConnection)
            {
                if (cloudConnection.Success)
                {
                    Log.LogInformation((int)EventIds.CreateNewCloudConnection, Invariant($"New cloud connection created for device {identity.Id}"));
                }
                else
                {
                    Log.LogInformation((int)EventIds.CreateNewCloudConnectionError, cloudConnection.Exception, Invariant($"Error creating new device connection for device {identity.Id}"));
                }
            }

            public static void NewDeviceConnection(IIdentity identity)
            {
                Log.LogInformation((int)EventIds.NewDeviceConnection, Invariant($"New device connection for device {identity.Id}"));
            }

            public static void RemovingDeviceConnection(string id, bool removeCloudConnection)
            {
                Log.LogInformation((int)EventIds.RemovingDeviceConnection, Invariant($"Removing device connection for device {id} with removeCloudConnection flag '{removeCloudConnection}'."));
            }

            public static void RemoveDeviceConnection(string id)
            {
                Log.LogInformation((int)EventIds.RemoveDeviceConnection, Invariant($"Device connection removed for device {id}"));
            }

            public static void ProcessingTokenNearExpiryEvent(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.ProcessingTokenNearExpiryEvent, Invariant($"Processing token near expiry for {identity.Id}"));
            }

            public static void InvokingCloudConnectionLostEvent(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.InvokingCloudConnectionLostEvent, Invariant($"Invoking cloud connection lost event for {identity.Id}"));
            }

            public static void InvokingCloudConnectionEstablishedEvent(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.InvokingCloudConnectionEstablishedEvent, Invariant($"Invoking cloud connection established event for {identity.Id}"));
            }

            public static void HandlingConnectionStatusChangedHandler(string deviceId, CloudConnectionStatus connectionStatus)
            {
                Log.LogInformation((int)EventIds.HandlingConnectionStatusChangedHandler, Invariant($"Connection status for {deviceId} changed to {connectionStatus}"));
            }

            public static void CloudConnectionLostClosingClient(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.CloudConnectionLostClosingClient, Invariant($"Cloud connection lost for {identity.Id}, closing client."));
            }

            internal static void GetCloudConnection(IIdentity identity, ITry<ICloudConnection> cloudConnection)
            {
                if (cloudConnection.Success)
                {
                    Log.LogDebug((int)EventIds.ObtainedCloudConnection, Invariant($"Obtained cloud connection for device {identity.Id}"));
                }
                else
                {
                    Log.LogInformation((int)EventIds.ObtainCloudConnectionError, cloudConnection.Exception, Invariant($"Error getting cloud connection for device {identity.Id}"));
                }
            }

            public static void CloudConnectionLostClosingAllClients()
            {
                Log.LogDebug((int)EventIds.CloudConnectionLostClosingAllClients, Invariant($"Cloud connection lost, closing all clients."));
            }

            public static void GettingCloudConnectionForDeviceSubscriptions()
            {
                Log.LogDebug((int)EventIds.GettingCloudConnectionForDeviceSubscriptions, $"Device has subscriptions. Trying to get cloud connection.");
            }
        }

        static class MetricsV0
        {
            static readonly GaugeOptions ConnectedClientGaugeOptions = new GaugeOptions
            {
                Name = "EdgeHubConnectedClientGauge",
                MeasurementUnit = Unit.Events
            };

            public static void SetConnectedClientCountGauge(ConnectionManager connectionManager)
            {
                // Subtract EdgeHub from the list of connected clients
                int connectedClients = connectionManager.GetConnectedClients().Count() - 1;
                Util.Metrics.MetricsV0.SetGauge(ConnectedClientGaugeOptions, connectedClients);
            }
        }
    }
}
