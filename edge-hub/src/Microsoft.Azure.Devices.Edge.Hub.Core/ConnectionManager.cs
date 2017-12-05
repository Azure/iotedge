// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;

    public class ConnectionManager : IConnectionManager
    {
        const int DefaultMaxClients = 101; // 100 Clients + 1 Edgehub
        readonly object deviceConnLock = new object();
        readonly ConcurrentDictionary<string, ConnectedDevice> devices = new ConcurrentDictionary<string, ConnectedDevice>();
        readonly ICloudProxyProvider cloudProxyProvider;
        readonly int maxClients;

        public event EventHandler<IIdentity> CloudConnectionLost;
        public event EventHandler<IIdentity> CloudConnectionEstablished;
        public event EventHandler<IIdentity> DeviceConnected;
        public event EventHandler<IIdentity> DeviceDisconnected;

        public ConnectionManager(ICloudProxyProvider cloudProxyProvider, int maxClients = DefaultMaxClients)
        {
            this.cloudProxyProvider = Preconditions.CheckNotNull(cloudProxyProvider, nameof(cloudProxyProvider));
            this.maxClients = Preconditions.CheckRange(maxClients, 1, nameof(maxClients));
        }

        public async Task AddDeviceConnection(IIdentity identity, IDeviceProxy deviceProxy)
        {
            ConnectedDevice device = this.GetOrCreateConnectedDevice(Preconditions.CheckNotNull(identity, nameof(identity)));
            Option<IDeviceProxy> currentDeviceProxy = device.UpdateDeviceProxy(Preconditions.CheckNotNull(deviceProxy, nameof(deviceProxy)));
            Events.NewDeviceConnection(identity);

            await currentDeviceProxy
                .Filter(dp => dp.IsActive)
                .Map(dp => dp.CloseAsync(new MultipleConnectionsException($"Multiple connections detected for device {identity.Id}")))
                .GetOrElse(Task.CompletedTask);
            this.DeviceConnected?.Invoke(this, identity);
        }

        public Task RemoveDeviceConnection(string id)
        {
            return this.devices.TryGetValue(Preconditions.CheckNonWhiteSpace(id, nameof(id)), out ConnectedDevice device)
                ? this.RemoveDeviceConnection(device)
                : Task.CompletedTask;
        }

        async Task RemoveDeviceConnection(ConnectedDevice device)
        {
            await device.DeviceProxy.Filter(dp => dp.IsActive)
                .ForEachAsync(dp => dp.CloseAsync(new EdgeHubConnectionException($"Connection closed for device {device.Identity.Id}.")));

            await device.CloudProxy.Filter(cp => cp.IsActive)
                .ForEachAsync(cp => cp.CloseAsync());

            Events.RemoveDeviceConnection(device.Identity.Id);
            this.DeviceDisconnected?.Invoke(this, device.Identity);
        }

        public Option<IDeviceProxy> GetDeviceConnection(string id)
        {
            return this.devices.TryGetValue(Preconditions.CheckNonWhiteSpace(id, nameof(id)), out ConnectedDevice device)
                ? device.DeviceProxy.Filter(dp => dp.IsActive)
                : Option.None<IDeviceProxy>();
        }

        public Option<ICloudProxy> GetCloudConnection(string id)
        {
            return this.devices.TryGetValue(Preconditions.CheckNonWhiteSpace(id, nameof(id)), out ConnectedDevice device)
                ? device.CloudProxy.Filter(cp => cp.IsActive)
                : Option.None<ICloudProxy>();
        }

        public async Task<Try<ICloudProxy>> CreateCloudConnectionAsync(IIdentity identity)
        {
            Preconditions.CheckNotNull(identity, nameof(identity));

            ConnectedDevice device = this.CreateOrUpdateConnectedDevice(identity);
            (Try<ICloudProxy> newCloudProxy, Option<ICloudProxy> existingCloudProxy) = await device.UpdateCloudProxy(() => this.GetCloudProxy(device));

            // If the existing cloud proxy had an active connection then close it since we
            // now have a new connected cloud proxy.
            await existingCloudProxy.Filter(cp => cp.IsActive)
                .Map(cp => cp.CloseAsync())
                .GetOrElse(Task.FromResult(true));
            Events.NewCloudConnection(identity, newCloudProxy);

            return newCloudProxy;
        }

        public async Task<Try<ICloudProxy>> GetOrCreateCloudConnectionAsync(IIdentity identity)
        {
            Preconditions.CheckNotNull(identity, nameof(identity));

            // Get an existing ConnectedDevice from this.devices or add a new non-connected
            // instance to this.devices and return that.
            ConnectedDevice device = this.GetOrCreateConnectedDevice(identity);

            Try<ICloudProxy> cloudProxy = await device.GetOrCreateCloudProxy(
                () => this.GetCloudProxy(device));
            Events.GetCloudConnection(identity, cloudProxy);
            return cloudProxy;
        }

        Task<Try<ICloudProxy>> GetCloudProxy(ConnectedDevice device) => this.cloudProxyProvider.Connect(device.Identity,
                (status, reason) => this.CloudConnectionStatusChangedHandler(device, status, reason));

        async void CloudConnectionStatusChangedHandler(ConnectedDevice device,
            ConnectionStatus connectionStatus,
            ConnectionStatusChangeReason connectionStatusChangeReason)
        {
            if (connectionStatus == ConnectionStatus.Connected)
            {
                this.CloudConnectionEstablished?.Invoke(this, device.Identity);
            }
            else
            {
                if (connectionStatusChangeReason == ConnectionStatusChangeReason.Expired_SAS_Token)
                {
                    await this.RemoveDeviceConnection(device);
                }
                this.CloudConnectionLost?.Invoke(this, device.Identity);
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
               (id, cd) => new ConnectedDevice(identity, cd.CloudProxy, cd.DeviceProxy));
        }

        ConnectedDevice CreateNewConnectedDevice(IIdentity identity)
        {
            lock(this.deviceConnLock)
            {
                if(this.devices.Values.Count(d => d.DeviceProxy.Filter(d1 => d1.IsActive).HasValue) >= this.maxClients)
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
            readonly AsyncLock cloudProxyLock = new AsyncLock();

            public ConnectedDevice(IIdentity identity)
                : this(identity, Option.None<ICloudProxy>(), Option.None<IDeviceProxy>())
            {
            }

            public ConnectedDevice(IIdentity identity, Option<ICloudProxy> cloudProxy, Option<IDeviceProxy> deviceProxy)
            {
                this.Identity = identity;
                this.CloudProxy = cloudProxy;
                this.DeviceProxy = deviceProxy;
            }

            public IIdentity Identity { get; }

            public Option<ICloudProxy> CloudProxy { get; private set; }

            public Option<IDeviceProxy> DeviceProxy { get; private set; }

            public Option<IDeviceProxy> UpdateDeviceProxy(IDeviceProxy deviceProxy)
            {
                Option<IDeviceProxy> deviceProxyOption = Option.Some(Preconditions.CheckNotNull(deviceProxy, nameof(deviceProxy)));
                Option<IDeviceProxy> currentValue;
                lock (this.deviceProxyLock)
                {
                    currentValue = this.DeviceProxy;
                    this.DeviceProxy = deviceProxyOption;
                }
                return currentValue;
            }

            public async Task<(Try<ICloudProxy>, Option<ICloudProxy>)> UpdateCloudProxy(Func<Task<Try<ICloudProxy>>> cloudProxyGetter)
            {
                Preconditions.CheckNotNull(cloudProxyGetter, nameof(cloudProxyGetter));
                // Lock in case multiple connections are created to the cloud for the same device at the same time
                using (await this.cloudProxyLock.LockAsync())
                {
                    Option<ICloudProxy> existingCloudProxy = this.CloudProxy;
                    Try<ICloudProxy> newCloudProxy = await cloudProxyGetter();
                    if (newCloudProxy.Success)
                    {
                        this.CloudProxy = Option.Some(newCloudProxy.Value);
                    }
                    return (newCloudProxy, existingCloudProxy);
                }
            }

            public async Task<Try<ICloudProxy>> GetOrCreateCloudProxy(Func<Task<Try<ICloudProxy>>> cloudProxyGetter)
            {
                Preconditions.CheckNotNull(cloudProxyGetter, nameof(cloudProxyGetter));
                // Lock in case multiple connections are created to the cloud for the same device at the same time
                using (await this.cloudProxyLock.LockAsync())
                {
                    return await this.CloudProxy.Filter(cp => cp.IsActive)
                        .Match(cp => Task.FromResult(Try.Success(cp)),
                        async () =>
                        {
                            Try<ICloudProxy> cloudProxy = await cloudProxyGetter();
                            if (cloudProxy.Success)
                            {
                                this.CloudProxy = Option.Some(cloudProxy.Value);
                            }
                            return cloudProxy;
                        });
                }
            }
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

            public static void NewCloudConnection(IIdentity identity, Try<ICloudProxy> cloudProxy)
            {                
                if (cloudProxy.Success)
                {
                    Log.LogInformation((int)EventIds.CreateNewCloudConnection, Invariant($"New cloud connection created for device {identity.Id}"));
                }
                else
                {
                    Log.LogInformation((int)EventIds.CreateNewCloudConnectionError, cloudProxy.Exception, Invariant($"Error creating new device connection for device {identity.Id}"));
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

            internal static void GetCloudConnection(IIdentity identity, Try<ICloudProxy> cloudProxy)
            {
                if (cloudProxy.Success)
                {
                    Log.LogDebug((int)EventIds.ObtainedCloudConnection, Invariant($"Obtained cloud connection for device {identity.Id}"));
                }
                else
                {
                    Log.LogInformation((int)EventIds.ObtainCloudConnectionError, cloudProxy.Exception, Invariant($"Error getting cloud connection for device {identity.Id}"));
                }
            }
        }
    }
}
