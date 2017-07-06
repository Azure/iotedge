// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;

    public class ConnectionManager : IConnectionManager
    {
        readonly ConcurrentDictionary<string, ConnectedDevice> devices = new ConcurrentDictionary<string, ConnectedDevice>();
        readonly ICloudProxyProvider cloudProxyProvider;
        readonly string edgeDeviceId;

        public ConnectionManager(ICloudProxyProvider cloudProxyProvider, string edgeDeviceId)
        {
            this.cloudProxyProvider = Preconditions.CheckNotNull(cloudProxyProvider, nameof(cloudProxyProvider));
            // TODO: edgeDeviceId is only used to check if device connection should not be removed (see method RemoveDeviceConnection)
            // Remove it when module identity is supported in IoTHub 
            this.edgeDeviceId = Preconditions.CheckNotNull(edgeDeviceId, nameof(this.edgeDeviceId));
        }

        public void AddDeviceConnection(IIdentity identity, IDeviceProxy deviceProxy)
        {
            ConnectedDevice device = this.GetOrCreateConnectedDevice(Preconditions.CheckNotNull(identity, nameof(identity)));
            Option<IDeviceProxy> currentDeviceProxy = device.UpdateDeviceProxy(Preconditions.CheckNotNull(deviceProxy, nameof(deviceProxy)));
            Events.NewDeviceConnection(identity);

            currentDeviceProxy
                .Filter(dp => dp.IsActive)
                .ForEach(dp => dp.CloseAsync(new MultipleConnectionsException($"Multiple connections detected for device {identity.Id}")));
        }

        public void RemoveDeviceConnection(string deviceId)
        {
            this.GetDeviceConnection(deviceId)
                .ForEach(deviceproxy => deviceproxy.SetInactive());
            // TODO - Currently this doesn't close the cloud connection for device that has same id as Edge deviceId as other modules might be using it. 
            // After IoTHub supports module identity, add code to close all cloud connections.
            if (!this.IsEdgeDevice(deviceId))
            {
                this.GetCloudConnection(deviceId).ForEach(cp => cp.CloseAsync());
            }
            Events.RemoveDeviceConnection(deviceId);
        }

        bool IsEdgeDevice(string deviceId) => this.edgeDeviceId.Equals(deviceId, StringComparison.OrdinalIgnoreCase);

        public Option<IDeviceProxy> GetDeviceConnection(string deviceId)
        {
            return this.devices.TryGetValue(Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId)), out ConnectedDevice device)
                ? device.DeviceProxy.Filter(dp => dp.IsActive)
                : Option.None<IDeviceProxy>();
        }

        public Option<ICloudProxy> GetCloudConnection(string deviceId)
        {
            // TODO: This line is a temporary workaround to use the underlying DeviceIdentity for cloud connections for modules
            deviceId = GetDeviceId(deviceId);

            return this.devices.TryGetValue(Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId)), out ConnectedDevice device)
                ? device.CloudProxy.Filter(cp => cp.IsActive)
                : Option.None<ICloudProxy>();
        }

        public async Task<bool> CloseConnectionAsync(string deviceId)
        {
            if (!this.devices.TryGetValue(Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId)), out ConnectedDevice device))
            {
                return false;
            }

            device.DeviceProxy.Filter(dp => dp.IsActive)
                .ForEach(dp => dp.CloseAsync(new EdgeHubConnectionException($"Connection closed for device {deviceId}.")));

            bool returnVal = await device.CloudProxy.Filter(cp => cp.IsActive)
                .Map(cp => cp.CloseAsync())
                .GetOrElse(Task.FromResult(true));

            Events.CloseConnection(deviceId);

            return returnVal;
        }

        public async Task<Try<ICloudProxy>> CreateCloudConnectionAsync(IIdentity identity)
        {
            Preconditions.CheckNotNull(identity, nameof(identity));

            // TODO: This line is a temporary workaround to use the underlying DeviceIdentity for cloud connections for modules
            IIdentity deviceIdentity = GetDeviceIdentity(identity);

            // Open a connection to Azure IoT Hub for this device/module.
            Try<ICloudProxy> cloudProxy = await this.cloudProxyProvider.Connect(deviceIdentity);
            if (cloudProxy.Success)
            {
                // Update the cloud proxy stored in this.devices with this new cloud proxy
                // instance.
                ConnectedDevice device = this.GetOrCreateConnectedDevice(deviceIdentity);
                Option<ICloudProxy> currentCloudProxy = device.UpdateCloudProxy(cloudProxy.Value);

                // If the existing cloud proxy had an active connection then close it since we
                // now have a new connected cloud proxy.
                await currentCloudProxy.Filter(cp => cp.IsActive)
                    .Map(cp => cp.CloseAsync())
                    .GetOrElse(Task.FromResult(true));
                Events.NewCloudConnection(deviceIdentity);
            }
            return cloudProxy;
        }

        public Task<Try<ICloudProxy>> GetOrCreateCloudConnectionAsync(IIdentity identity)
        {
            Preconditions.CheckNotNull(identity, nameof(identity));

            // TODO: This line is a temporary workaround to use the underlying DeviceIdentity for cloud connections for modules
            IIdentity deviceIdentity = GetDeviceIdentity(identity);

            // Get an existing ConnectedDevice from this.devices or add a new non-connected
            // instance to this.devices and return that.
            ConnectedDevice device = this.GetOrCreateConnectedDevice(deviceIdentity);

            return device.CloudProxy.Filter(cp => cp.IsActive)
                .Match(cp => Task.FromResult(Try.Success(cp)), () => this.CreateCloudConnectionAsync(deviceIdentity));
        }

        /// <summary>
        /// If the identity is a moduleIdentity, it creates an identity for the underlying device. 
        /// TODO: This is a temporary workaround to use the underlying DeviceIdentity for cloud connections for modules
        /// </summary>
        static IIdentity GetDeviceIdentity(IIdentity identity)
        {
            var moduleIdentity = identity as ModuleIdentity;
            return moduleIdentity != null ? new DeviceIdentity(moduleIdentity, moduleIdentity.DeviceId) : identity;
        }

        /// <summary>
        /// If the id is deviceId/moduleId, then it gets the deviceId from it
        /// TODO: This is a temporary workaround to use the underlying DeviceIdentity for cloud connections for modules
        /// </summary>
        static string GetDeviceId(string id)
        {
            int seperatorIndex = id.IndexOf('/');
            return seperatorIndex > 0 ? id.Substring(0, seperatorIndex) : id;
        }

        static string GetDeviceId(IIdentity identity)
        {
            switch (identity)
            {
                case IModuleIdentity moduleIdentity:
                    return moduleIdentity.DeviceId;
                case IDeviceIdentity deviceIdentity:
                    return deviceIdentity.DeviceId;
                default:
                    throw new InvalidOperationException($"Unknown identity - {identity}");
            }
        }

        ConnectedDevice GetOrCreateConnectedDevice(IIdentity identity)
        {
            string deviceId = Preconditions.CheckNotNull(identity, nameof(identity)).Id;
            return this.devices.GetOrAdd(
                Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId)),
                id => new ConnectedDevice(identity));
        }

        class ConnectedDevice
        {
            readonly object lockObject = new object();

            public ConnectedDevice(IIdentity identity)
            {
                this.Identity = identity;
                this.CloudProxy = Option.None<ICloudProxy>();
                this.DeviceProxy = Option.None<IDeviceProxy>();
            }

            public IIdentity Identity { get; }

            public Option<ICloudProxy> CloudProxy { get; private set; }

            public Option<IDeviceProxy> DeviceProxy { get; private set; }

            public Option<IDeviceProxy> UpdateDeviceProxy(IDeviceProxy deviceProxy)
            {
                Option<IDeviceProxy> deviceProxyOption = Option.Some(Preconditions.CheckNotNull(deviceProxy, nameof(deviceProxy)));
                Option<IDeviceProxy> currentValue;
                // TODO - Interlocked.Exchange doesn't work on structs. Figure out if another locking method could be faster
                lock (this.lockObject)
                {
                    currentValue = this.DeviceProxy;
                    this.DeviceProxy = deviceProxyOption;
                }
                return currentValue;
            }

            public Option<ICloudProxy> UpdateCloudProxy(ICloudProxy cloudProxy)
            {
                Option<ICloudProxy> cloudProxyOption = Option.Some(Preconditions.CheckNotNull(cloudProxy, nameof(cloudProxy)));
                Option<ICloudProxy> currentValue;
                // TODO - Interlocked.Exchange doesn't work on structs. Figure out if another locking method could be faster
                lock (this.lockObject)
                {
                    currentValue = this.CloudProxy;
                    this.CloudProxy = cloudProxyOption;
                }
                return currentValue;
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
                CloseDeviceConnection
            }

            public static void NewCloudConnection(IIdentity identity)
            {
                Log.LogInformation((int)EventIds.CreateNewCloudConnection, Invariant($"New cloud connection created for device {identity.Id}"));
            }

            public static void NewDeviceConnection(IIdentity identity)
            {
                Log.LogInformation((int)EventIds.NewDeviceConnection, Invariant($"New device connection for device {identity.Id}"));
            }

            public static void RemoveDeviceConnection(string id)
            {
                Log.LogInformation((int)EventIds.RemoveDeviceConnection, Invariant($"Device connection removed for device {id}"));
            }

            public static void CloseConnection(string id)
            {
                Log.LogInformation((int)EventIds.CloseDeviceConnection, Invariant($"Connection closed for device {id}"));
            }
        }
    }
}
