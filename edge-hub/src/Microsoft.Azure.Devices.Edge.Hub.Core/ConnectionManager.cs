// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading.Tasks;
    using App.Metrics;
    using App.Metrics.Gauge;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;

    public class ConnectionManager : IConnectionManager
    {
        const int DefaultMaxClients = 101; // 100 Clients + 1 Edgehub
        readonly object deviceConnLock = new object();
        readonly ConcurrentDictionary<string, ConnectedDevice> devices = new ConcurrentDictionary<string, ConnectedDevice>();
        readonly ICloudConnectionProvider cloudConnectionProvider;
        readonly int maxClients;
        readonly ICredentialsCache credentialsCache;
        readonly IIdentityProvider identityProvider;

        public ConnectionManager(
            ICloudConnectionProvider cloudConnectionProvider,
            ICredentialsCache credentialsCache,
            IIdentityProvider identityProvider,
            int maxClients = DefaultMaxClients)
        {
            this.cloudConnectionProvider = Preconditions.CheckNotNull(cloudConnectionProvider, nameof(cloudConnectionProvider));
            this.maxClients = Preconditions.CheckRange(maxClients, 1, nameof(maxClients));
            this.credentialsCache = Preconditions.CheckNotNull(credentialsCache, nameof(credentialsCache));
            this.identityProvider = Preconditions.CheckNotNull(identityProvider, nameof(identityProvider));
            Util.Metrics.MetricsV0.RegisterGaugeCallback(() => MetricsV0.SetConnectedClientCountGauge(this));
        }

        public event EventHandler<IIdentity> CloudConnectionEstablished;

        public event EventHandler<IIdentity> CloudConnectionLost;

        public event EventHandler<IIdentity> DeviceConnected;

        public event EventHandler<IIdentity> DeviceDisconnected;

        public IEnumerable<IIdentity> GetConnectedClients() =>
            this.devices.Values
                .Where(d => d.DeviceConnection.Map(dc => dc.IsActive).GetOrElse(false))
                .Select(d => d.Identity);

        public async Task AddDeviceConnection(IIdentity identity, IDeviceProxy deviceProxy)
        {
            Preconditions.CheckNotNull(identity, nameof(identity));
            Preconditions.CheckNotNull(deviceProxy, nameof(deviceProxy));
            ConnectedDevice device = this.GetOrCreateConnectedDevice(identity);
            Option<DeviceConnection> currentDeviceConnection = device.AddDeviceConnection(deviceProxy);
            Events.NewDeviceConnection(identity);
            await currentDeviceConnection
                .Filter(dc => dc.IsActive)
                .ForEachAsync(dc => dc.CloseAsync(new MultipleConnectionsException($"Multiple connections detected for device {identity.Id}")));
            this.DeviceConnected?.Invoke(this, identity);
        }

        public Task RemoveDeviceConnection(string id)
        {
            return this.devices.TryGetValue(Preconditions.CheckNonWhiteSpace(id, nameof(id)), out ConnectedDevice device)
                ? this.RemoveDeviceConnection(device, false)
                : Task.CompletedTask;
        }

        public Option<IDeviceProxy> GetDeviceConnection(string id)
        {
            return this.devices.TryGetValue(Preconditions.CheckNonWhiteSpace(id, nameof(id)), out ConnectedDevice device)
                ? device.DeviceConnection.Filter(dp => dp.IsActive).Map(d => d.DeviceProxy)
                : Option.None<IDeviceProxy>();
        }

        public async Task<Option<ICloudProxy>> GetCloudConnection(string id)
        {
            Try<ICloudProxy> cloudProxyTry = await this.TryGetCloudConnection(id);
            return cloudProxyTry
                .Ok()
                .Map(c => (ICloudProxy)new RetryingCloudProxy(id, () => this.TryGetCloudConnection(id), c));
        }

        async Task<Try<ICloudProxy>> TryGetCloudConnection(string id)
        {
            IIdentity identity = this.identityProvider.Create(Preconditions.CheckNonWhiteSpace(id, nameof(id)));
            ConnectedDevice device = this.GetOrCreateConnectedDevice(identity);

            Try<ICloudConnection> cloudConnectionTry = await device.GetOrCreateCloudConnection(
                c => this.cloudConnectionProvider.Connect(c.Identity, this.CloudConnectionStatusChangedHandler));

            Events.GetCloudConnection(device.Identity, cloudConnectionTry);
            Try<ICloudProxy> cloudProxyTry = GetCloudProxyFromCloudConnection(cloudConnectionTry, device.Identity);
            return cloudProxyTry;
        }

        public void AddSubscription(string id, DeviceSubscription deviceSubscription)
        {
            if (!this.devices.TryGetValue(Preconditions.CheckNonWhiteSpace(id, nameof(id)), out ConnectedDevice device))
            {
                throw new ArgumentException($"A connection for {id} not found.");
            }

            device.DeviceConnection.Filter(d => d.IsActive)
                .ForEach(d => d.Subscriptions[deviceSubscription] = true);
        }

        public void RemoveSubscription(string id, DeviceSubscription deviceSubscription)
        {
            if (!this.devices.TryGetValue(Preconditions.CheckNonWhiteSpace(id, nameof(id)), out ConnectedDevice device))
            {
                throw new ArgumentException($"A connection for {id} not found.");
            }

            device.DeviceConnection.Filter(d => d.IsActive)
                .ForEach(d => d.Subscriptions[deviceSubscription] = false);
        }

        public Option<IReadOnlyDictionary<DeviceSubscription, bool>> GetSubscriptions(string id) =>
            this.devices.TryGetValue(Preconditions.CheckNonWhiteSpace(id, nameof(id)), out ConnectedDevice device)
                ? device.DeviceConnection.Filter(d => d.IsActive)
                    .Map(d => new ReadOnlyDictionary<DeviceSubscription, bool>(d.Subscriptions) as IReadOnlyDictionary<DeviceSubscription, bool>)
                : Option.None<IReadOnlyDictionary<DeviceSubscription, bool>>();

        public bool CheckClientSubscription(string id, DeviceSubscription subscription) =>
            this.GetSubscriptions(id)
                .Filter(s => s.TryGetValue(subscription, out bool isActive) && isActive)
                .HasValue;

        public async Task<Try<ICloudProxy>> CreateCloudConnectionAsync(IClientCredentials credentials)
        {
            Preconditions.CheckNotNull(credentials, nameof(credentials));

            ConnectedDevice device = this.CreateOrUpdateConnectedDevice(credentials.Identity);
            Try<ICloudConnection> newCloudConnection = await device.CreateOrUpdateCloudConnection(c => this.CreateOrUpdateCloudConnection(c, credentials));
            Events.NewCloudConnection(credentials.Identity, newCloudConnection);
            Try<ICloudProxy> cloudProxyTry = GetCloudProxyFromCloudConnection(newCloudConnection, credentials.Identity);
            return cloudProxyTry.Success
                ? Try.Success((ICloudProxy)new RetryingCloudProxy(credentials.Identity.Id, () => this.TryGetCloudConnection(credentials.Identity.Id), cloudProxyTry.Value))
                : cloudProxyTry;
        }

        // This method is not used, but it has important logic and this will be useful for offline scenarios.
        // So do not delete this method.
        public async Task<Try<ICloudProxy>> GetOrCreateCloudConnectionAsync(IClientCredentials credentials)
        {
            Preconditions.CheckNotNull(credentials, nameof(credentials));

            // Get an existing ConnectedDevice from this.devices or add a new non-connected
            // instance to this.devices and return that.
            ConnectedDevice device = this.GetOrCreateConnectedDevice(credentials.Identity);

            Try<ICloudConnection> cloudConnectionTry = await device.GetOrCreateCloudConnection((c) => this.CreateOrUpdateCloudConnection(c, credentials));
            Events.GetCloudConnection(credentials.Identity, cloudConnectionTry);
            Try<ICloudProxy> cloudProxyTry = GetCloudProxyFromCloudConnection(cloudConnectionTry, credentials.Identity);
            return cloudProxyTry.Success
                ? Try.Success((ICloudProxy)new RetryingCloudProxy(credentials.Identity.Id, () => this.TryGetCloudConnection(credentials.Identity.Id), cloudProxyTry.Value))
                : cloudProxyTry;
        }

        static Try<ICloudProxy> GetCloudProxyFromCloudConnection(Try<ICloudConnection> cloudConnection, IIdentity identity) => cloudConnection.Success
            ? cloudConnection.Value.CloudProxy.Map(Try.Success)
                .GetOrElse(() => Try<ICloudProxy>.Failure(new EdgeHubConnectionException($"Unable to get cloud proxy for device {identity.Id}")))
            : Try<ICloudProxy>.Failure(cloudConnection.Exception);

        async Task RemoveDeviceConnection(ConnectedDevice device, bool removeCloudConnection)
        {
            await device.DeviceConnection.Filter(dp => dp.IsActive)
                .ForEachAsync(dp => dp.CloseAsync(new EdgeHubConnectionException($"Connection closed for device {device.Identity.Id}.")));

            if (removeCloudConnection)
            {
                await device.CloudConnection.Filter(cp => cp.IsActive)
                    .ForEachAsync(cp => cp.CloseAsync());
            }

            Events.RemoveDeviceConnection(device.Identity.Id);
            this.DeviceDisconnected?.Invoke(this, device.Identity);
        }

        Task<Try<ICloudConnection>> CreateOrUpdateCloudConnection(ConnectedDevice device, IClientCredentials credentials) =>
            device.CloudConnection.Map(
                    async c =>
                    {
                        try
                        {
                            if (!(credentials is ITokenCredentials tokenCredentials))
                            {
                                throw new InvalidOperationException($"Cannot update credentials of type {credentials.AuthenticationType} for {credentials.Identity.Id}");
                            }
                            else if (!(c is IClientTokenCloudConnection clientTokenCloudConnection))
                            {
                                throw new InvalidOperationException($"Cannot update token for an existing cloud connection that is not based on client token for {credentials.Identity.Id}");
                            }
                            else
                            {
                                await clientTokenCloudConnection.UpdateTokenAsync(tokenCredentials);
                                return Try.Success(c);
                            }
                        }
                        catch (Exception ex)
                        {
                            return Try<ICloudConnection>.Failure(new EdgeHubConnectionException($"Error updating identity for device {device.Identity.Id}", ex));
                        }
                    })
                .GetOrElse(() => this.cloudConnectionProvider.Connect(credentials, (identity, status) => this.CloudConnectionStatusChangedHandler(identity, status)));

        async void CloudConnectionStatusChangedHandler(
            string deviceId,
            CloudConnectionStatus connectionStatus)
        {
            Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            Events.HandlingConnectionStatusChangedHandler(deviceId, connectionStatus);
            if (!this.devices.TryGetValue(deviceId, out ConnectedDevice device))
            {
                throw new InvalidOperationException($"Device {deviceId} not found in the list of connected devices");
            }

            switch (connectionStatus)
            {
                case CloudConnectionStatus.TokenNearExpiry:
                    Events.ProcessingTokenNearExpiryEvent(device.Identity);
                    Option<IClientCredentials> clientCredentials = await this.credentialsCache.Get(device.Identity);
                    if (clientCredentials.HasValue)
                    {
                        await clientCredentials.ForEachAsync(
                            async cc =>
                            {
                                if (cc is ITokenCredentials tokenCredentials && tokenCredentials.IsUpdatable)
                                {
                                    Try<ICloudConnection> cloudConnectionTry = await device.CreateOrUpdateCloudConnection(c => this.CreateOrUpdateCloudConnection(c, tokenCredentials));
                                    if (!cloudConnectionTry.Success)
                                    {
                                        await this.RemoveDeviceConnection(device, true);
                                        this.CloudConnectionLost?.Invoke(this, device.Identity);
                                    }
                                }
                                else
                                {
                                    await this.RemoveDeviceConnection(device, false);
                                }
                            });
                    }
                    else
                    {
                        await this.RemoveDeviceConnection(device, true);
                        this.CloudConnectionLost?.Invoke(this, device.Identity);
                    }

                    break;

                case CloudConnectionStatus.DisconnectedTokenExpired:
                    await this.RemoveDeviceConnection(device, true);
                    Events.InvokingCloudConnectionLostEvent(device.Identity);
                    this.CloudConnectionLost?.Invoke(this, device.Identity);
                    break;

                case CloudConnectionStatus.Disconnected:
                    Events.InvokingCloudConnectionLostEvent(device.Identity);
                    this.CloudConnectionLost?.Invoke(this, device.Identity);
                    await device.CloudConnection.Filter(cp => cp.IsActive).ForEachAsync(
                        cp =>
                        {
                            Events.CloudConnectionLostClosingClient(device.Identity);
                            return cp.CloseAsync();
                        });
                    break;

                case CloudConnectionStatus.ConnectionEstablished:
                    Events.InvokingCloudConnectionEstablishedEvent(device.Identity);
                    this.CloudConnectionEstablished?.Invoke(this, device.Identity);
                    break;
            }
        }

        ConnectedDevice GetOrCreateConnectedDevice(IIdentity identity)
        {
            string deviceId = Preconditions.CheckNotNull(identity, nameof(identity)).Id;
            return this.devices.GetOrAdd(
                Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId)),
                id => this.CreateNewConnectedDevice(identity));
        }

        ConnectedDevice CreateOrUpdateConnectedDevice(IIdentity identity)
        {
            string deviceId = Preconditions.CheckNotNull(identity, nameof(identity)).Id;
            Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            return this.devices.AddOrUpdate(
                deviceId,
                id => this.CreateNewConnectedDevice(identity),
                (id, cd) => new ConnectedDevice(identity, cd.CloudConnection, cd.DeviceConnection));
        }

        ConnectedDevice CreateNewConnectedDevice(IIdentity identity)
        {
            lock (this.deviceConnLock)
            {
                if (this.devices.Values.Count(d => d.DeviceConnection.Filter(d1 => d1.IsActive).HasValue) >= this.maxClients)
                {
                    throw new EdgeHubConnectionException($"Edge hub already has maximum allowed clients ({this.maxClients - 1}) connected.");
                }

                return new ConnectedDevice(identity);
            }
        }

        class ConnectedDevice
        {
            // Device Proxy methods are sync coming from the Protocol gateway,
            // so using traditional locking mechanism for those.
            readonly object deviceProxyLock = new object();
            readonly AsyncLock cloudConnectionLock = new AsyncLock();
            Option<Task<Try<ICloudConnection>>> cloudConnectionCreateTask = Option.None<Task<Try<ICloudConnection>>>();

            public ConnectedDevice(IIdentity identity)
                : this(identity, Option.None<ICloudConnection>(), Option.None<DeviceConnection>())
            {
            }

            public ConnectedDevice(IIdentity identity, Option<ICloudConnection> cloudProxy, Option<DeviceConnection> deviceConnection)
            {
                this.Identity = identity;
                this.CloudConnection = cloudProxy;
                this.DeviceConnection = deviceConnection;
            }

            public IIdentity Identity { get; }

            public Option<ICloudConnection> CloudConnection { get; private set; }

            // ReSharper disable once MemberHidesStaticFromOuterClass
            public Option<DeviceConnection> DeviceConnection { get; private set; }

            public Option<DeviceConnection> AddDeviceConnection(IDeviceProxy deviceProxy)
            {
                Preconditions.CheckNotNull(deviceProxy, nameof(deviceProxy));
                lock (this.deviceProxyLock)
                {
                    Option<DeviceConnection> currentValue = this.DeviceConnection;
                    IDictionary<DeviceSubscription, bool> subscriptions = this.DeviceConnection.Map(d => d.Subscriptions)
                        .GetOrElse(new ConcurrentDictionary<DeviceSubscription, bool>());
                    this.DeviceConnection = Option.Some(new DeviceConnection(deviceProxy, subscriptions));
                    return currentValue;
                }
            }

            public async Task<Try<ICloudConnection>> CreateOrUpdateCloudConnection(
                Func<ConnectedDevice, Task<Try<ICloudConnection>>> cloudConnectionUpdater)
            {
                Preconditions.CheckNotNull(cloudConnectionUpdater, nameof(cloudConnectionUpdater));
                // Lock in case multiple connections are created to the cloud for the same device at the same time
                using (await this.cloudConnectionLock.LockAsync())
                {
                    Try<ICloudConnection> newCloudConnection = await cloudConnectionUpdater(this);
                    if (newCloudConnection.Success)
                    {
                        this.CloudConnection = Option.Some(newCloudConnection.Value);
                    }

                    return newCloudConnection;
                }
            }

            public async Task<Try<ICloudConnection>> GetOrCreateCloudConnection(
                Func<ConnectedDevice, Task<Try<ICloudConnection>>> cloudConnectionCreator)
            {
                Preconditions.CheckNotNull(cloudConnectionCreator, nameof(cloudConnectionCreator));

                return await this.CloudConnection.Filter(cp => cp.IsActive)
                    .Map(c => Task.FromResult(Try.Success(c)))
                    .GetOrElse(
                        async () =>
                        {
                            return await this.cloudConnectionCreateTask.Filter(c => !c.IsCompleted)
                                .GetOrElse(
                                    async () =>
                                    {
                                        using (await this.cloudConnectionLock.LockAsync())
                                        {
                                            return await this.CloudConnection.Filter(cp => cp.IsActive)
                                                .Map(c => Task.FromResult(Try.Success(c)))
                                                .GetOrElse(
                                                    async () =>
                                                    {
                                                        return await this.cloudConnectionCreateTask.Filter(c => !c.IsCompleted)
                                                            .GetOrElse(
                                                                async () =>
                                                                {
                                                                    Task<Try<ICloudConnection>> createTask = cloudConnectionCreator(this);
                                                                    this.cloudConnectionCreateTask = Option.Some(createTask);
                                                                    Try<ICloudConnection> cloudConnectionResult = await createTask;
                                                                    this.CloudConnection = cloudConnectionResult.Ok();
                                                                    return cloudConnectionResult;
                                                                });
                                                    });
                                        }
                                    });
                        });
            }
        }

        class DeviceConnection
        {
            public DeviceConnection(IDeviceProxy deviceProxy, IDictionary<DeviceSubscription, bool> subscriptions)
            {
                this.Subscriptions = subscriptions;
                this.DeviceProxy = deviceProxy;
            }

            public IDeviceProxy DeviceProxy { get; }

            public IDictionary<DeviceSubscription, bool> Subscriptions { get; }

            public bool IsActive => this.DeviceProxy.IsActive;

            public Task CloseAsync(Exception ex) => this.DeviceProxy.CloseAsync(ex);
        }

        static class Events
        {
            const int IdStart = HubCoreEventIds.ConnectionManager;
            static readonly ILogger Log = Logger.Factory.CreateLogger<ConnectionManager>();

            enum EventIds
            {
                CreateNewCloudConnection = IdStart,
                NewDeviceConnection,
                RemoveDeviceConnection,
                CreateNewCloudConnectionError,
                ObtainedCloudConnection,
                ObtainCloudConnectionError,
                ProcessingTokenNearExpiryEvent,
                InvokingCloudConnectionLostEvent,
                InvokingCloudConnectionEstablishedEvent,
                HandlingConnectionStatusChangedHandler,
                CloudConnectionLostClosingClient
            }

            public static void NewCloudConnection(IIdentity identity, Try<ICloudConnection> cloudConnection)
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

            internal static void GetCloudConnection(IIdentity identity, Try<ICloudConnection> cloudConnection)
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
