// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading.Tasks;
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

        public event EventHandler<IIdentity> CloudConnectionLost;
        public event EventHandler<IIdentity> CloudConnectionEstablished;
        public event EventHandler<IIdentity> DeviceConnected;
        public event EventHandler<IIdentity> DeviceDisconnected;

        public ConnectionManager(ICloudConnectionProvider cloudConnectionProvider, int maxClients = DefaultMaxClients)
        {
            this.cloudConnectionProvider = Preconditions.CheckNotNull(cloudConnectionProvider, nameof(cloudConnectionProvider));
            this.maxClients = Preconditions.CheckRange(maxClients, 1, nameof(maxClients));
        }

        public async Task AddDeviceConnection(IIdentity identity, IDeviceProxy deviceProxy)
        {
            ConnectedDevice device = this.GetOrCreateConnectedDevice(Preconditions.CheckNotNull(identity, nameof(identity)));
            Option<IDeviceProxy> currentDeviceProxy = device.UpdateDeviceProxy(Preconditions.CheckNotNull(deviceProxy, nameof(deviceProxy)));
            Events.NewDeviceConnection(identity);

            await currentDeviceProxy
                .Filter(dp => dp.IsActive)
                .ForEachAsync(dp => dp.CloseAsync(new MultipleConnectionsException($"Multiple connections detected for device {identity.Id}")));
            this.DeviceConnected?.Invoke(this, identity);
        }

        public Task RemoveDeviceConnection(string id)
        {
            return this.devices.TryGetValue(Preconditions.CheckNonWhiteSpace(id, nameof(id)), out ConnectedDevice device)
                ? this.RemoveDeviceConnection(device, true)
                : Task.CompletedTask;
        }

        async Task RemoveDeviceConnection(ConnectedDevice device, bool removeCloudConnection)
        {
            await device.DeviceConnection.Filter(dp => dp.IsActive)
                .ForEachAsync(dp => dp.DeviceProxy.CloseAsync(new EdgeHubConnectionException($"Connection closed for device {device.Identity.Id}.")));

            if (removeCloudConnection)
            {
                await device.CloudConnection.Filter(cp => cp.IsActive)
                    .ForEachAsync(cp => cp.CloseAsync());
            }

            Events.RemoveDeviceConnection(device.Identity.Id);
            this.DeviceDisconnected?.Invoke(this, device.Identity);
        }

        public Option<IDeviceProxy> GetDeviceConnection(string id)
        {
            return this.devices.TryGetValue(Preconditions.CheckNonWhiteSpace(id, nameof(id)), out ConnectedDevice device)
                ? device.DeviceConnection.Filter(dp => dp.IsActive).Map(d => d.DeviceProxy)
                : Option.None<IDeviceProxy>();
        }

        public Option<ICloudProxy> GetCloudConnection(string id)
        {
            return this.devices.TryGetValue(Preconditions.CheckNonWhiteSpace(id, nameof(id)), out ConnectedDevice device)
                ? device.CloudConnection.FlatMap(cp => cp.CloudProxy)
                : Option.None<ICloudProxy>();
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

        public async Task<Try<ICloudProxy>> CreateCloudConnectionAsync(IClientCredentials credentials)
        {
            Preconditions.CheckNotNull(credentials, nameof(credentials));

            ConnectedDevice device = this.CreateOrUpdateConnectedDevice(credentials.Identity);
            Try<ICloudConnection> newCloudConnection = await device.CreateOrUpdateCloudConnection(c => this.CreateOrUpdateCloudConnection(c, credentials));
            Events.NewCloudConnection(credentials.Identity, newCloudConnection);
            Try<ICloudProxy> cloudProxyTry = GetCloudProxyFromCloudConnection(newCloudConnection, credentials.Identity);
            return cloudProxyTry;
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
            return cloudProxyTry;
        }

        Task<Try<ICloudConnection>> CreateOrUpdateCloudConnection(ConnectedDevice device, IClientCredentials credentials) =>
            device.CloudConnection.Map(
                async c =>
                {
                    try
                    {
                        await c.CreateOrUpdateAsync(credentials);
                        return Try.Success(c);
                    }
                    catch (Exception ex)
                    {
                        return Try<ICloudConnection>.Failure(new EdgeHubConnectionException($"Error updating identity for device {device.Identity.Id}", ex));
                    }
                })
            .GetOrElse(() => this.cloudConnectionProvider.Connect(credentials, (identity, status) => this.CloudConnectionStatusChangedHandler(identity, status)));


        async void CloudConnectionStatusChangedHandler(string deviceId,
            CloudConnectionStatus connectionStatus)
        {
            Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            if (!this.devices.TryGetValue(deviceId, out ConnectedDevice device))
            {
                throw new InvalidOperationException($"Device {deviceId} not found in the list of connected devices");
            }

            switch (connectionStatus)
            {
                case CloudConnectionStatus.TokenNearExpiry:

                    Option<IDeviceProxy> deviceProxy = device.DeviceConnection.Map(d => d.DeviceProxy).Filter(d => d.IsActive);
                    if (deviceProxy.HasValue)
                    {
                        Option<IClientCredentials> token = await deviceProxy.Map(d => d.GetUpdatedIdentity())
                            .GetOrElse(Task.FromResult(Option.None<IClientCredentials>()));
                        if (token.HasValue)
                        {
                            await token.ForEachAsync(async t =>
                            {
                                Try<ICloudConnection> cloudConnectionTry = await device.CreateOrUpdateCloudConnection(c => this.CreateOrUpdateCloudConnection(c, t));
                                if (!cloudConnectionTry.Success)
                                {
                                    await this.RemoveDeviceConnection(device, true);
                                    this.CloudConnectionLost?.Invoke(this, device.Identity);
                                }
                            });
                        }
                        else
                        {
                            await this.RemoveDeviceConnection(device, false);
                        }
                    }
                    else
                    {
                        await this.RemoveDeviceConnection(device, true);
                    }
                    
                    break;

                case CloudConnectionStatus.DisconnectedTokenExpired:
                    await this.RemoveDeviceConnection(device, true);
                    this.CloudConnectionLost?.Invoke(this, device.Identity);
                    break;

                case CloudConnectionStatus.Disconnected:
                    this.CloudConnectionLost?.Invoke(this, device.Identity);
                    break;

                case CloudConnectionStatus.ConnectionEstablished:
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
            return this.devices.AddOrUpdate(deviceId,
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

        static Try<ICloudProxy> GetCloudProxyFromCloudConnection(Try<ICloudConnection> cloudConnection, IIdentity identity) => cloudConnection.Success
            ? cloudConnection.Value.CloudProxy.Map(cp => Try.Success(cp))
            .GetOrElse(() => Try<ICloudProxy>.Failure(new EdgeHubConnectionException($"Unable to get cloud proxy for device {identity.Id}")))
            : Try<ICloudProxy>.Failure(cloudConnection.Exception);


        class ConnectedDevice
        {
            // Device Proxy methods are sync coming from the Protocol gateway,
            // so using traditional locking mechanism for those.
            readonly object deviceProxyLock = new object();
            readonly AsyncLock cloudConnectionLock = new AsyncLock();

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

            public Option<IDeviceProxy> UpdateDeviceProxy(IDeviceProxy deviceProxy)
            {
                Preconditions.CheckNotNull(deviceProxy, nameof(deviceProxy));
                Option<IDeviceProxy> currentValue;
                lock (this.deviceProxyLock)
                {
                    currentValue = this.DeviceConnection.Map(d => d.DeviceProxy);
                    // TODO: Here we set the subscriptions to the existing device subscriptions. This is because if the MQTT setting of cleanSession is set to false
                    // the server is expected to "remember" the subscriptions. 
                    // This way of keeping subscriptions might cause issues in the unlikely case that to different devices with 2 different set of subscriptions use the same device ID.
                    // The right way to do this, would be to not store the subscriptions, and instead, query the transport layer (MQTT/AMQP) for the current subscriptions. 
                    IDictionary<DeviceSubscription, bool> subscriptions = this.DeviceConnection.Map(d => d.Subscriptions)
                        .GetOrElse(new ConcurrentDictionary<DeviceSubscription, bool>());
                    this.DeviceConnection = Option.Some(new DeviceConnection(deviceProxy, subscriptions));
                }
                return currentValue;
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
                Func<ConnectedDevice, Task<Try<ICloudConnection>>> cloudConnectionUpdater)
            {
                Preconditions.CheckNotNull(cloudConnectionUpdater, nameof(cloudConnectionUpdater));
                // Lock in case multiple connections are created to the cloud for the same device at the same time
                using (await this.cloudConnectionLock.LockAsync())
                {
                    return await this.CloudConnection.Filter(cp => cp.IsActive)
                        .Match(cp => Task.FromResult(Try.Success(cp)),
                        async () =>
                        {
                            Try<ICloudConnection> cloudConnection = await cloudConnectionUpdater(this);
                            if (cloudConnection.Success)
                            {
                                this.CloudConnection = Option.Some(cloudConnection.Value);
                            }
                            return cloudConnection;
                        });
                }
            }
        }

        class DeviceConnection
        {
            public DeviceConnection(IDeviceProxy deviceProxy, IDictionary<DeviceSubscription, bool> subscriptions)
            {
                this.DeviceProxy = deviceProxy;
                this.Subscriptions = subscriptions;
            }

            public IDeviceProxy DeviceProxy { get; }

            public IDictionary<DeviceSubscription, bool> Subscriptions { get; }

            public bool IsActive => this.DeviceProxy.IsActive;
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<ConnectionManager>();
            const int IdStart = HubCoreEventIds.ConnectionManager;

            enum EventIds
            {
                CreateNewCloudConnection = IdStart,
                NewDeviceConnection,
                RemoveDeviceConnection,
                CreateNewCloudConnectionError,
                ObtainedCloudConnection,
                ObtainCloudConnectionError
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
    }
}
