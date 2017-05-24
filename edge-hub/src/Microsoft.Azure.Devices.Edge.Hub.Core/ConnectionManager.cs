// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ConnectionManager : IConnectionManager
    {
        readonly ConcurrentDictionary<string, ConnectedDevice> devices = new ConcurrentDictionary<string, ConnectedDevice>();
        readonly ICloudProxyProvider cloudProxyProvider;

        public ConnectionManager(ICloudProxyProvider cloudProxyProvider)
        {
            this.cloudProxyProvider = cloudProxyProvider;
        }

        public void AddDeviceConnection(IIdentity identity, IDeviceProxy deviceProxy)
        {
            ConnectedDevice device = this.GetOrCreateConnectedDevice(Preconditions.CheckNotNull(identity, nameof(identity)));
            Option<IDeviceProxy> currentDeviceProxy = device.UpdateDeviceProxy(Preconditions.CheckNotNull(deviceProxy, nameof(deviceProxy)));

            currentDeviceProxy
                .Filter(dp => dp.IsActive)
                .ForEach(dp => dp.CloseAsync(new MultipleConnectionsException($"Multiple connections detected for device {identity.Id}")));
        }

        public void RemoveDeviceConnection(string deviceId)
        {
            // TODO - Currently this doesn't close the cloud connection as other modules might be using it. 
            // After IoTHub supports module identity, add code to close cloud connection.
            this.GetDeviceConnection(deviceId)
                .ForEach(deviceproxy => deviceproxy.SetInactive());
        }

        public Option<IDeviceProxy> GetDeviceConnection(string deviceId)
        {
            return this.devices.TryGetValue(Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId)), out ConnectedDevice device)
                ? device.DeviceProxy.Filter(dp => dp.IsActive)
                : Option.None<IDeviceProxy>();
        }

        public Option<ICloudProxy> GetCloudConnection(string deviceId)
        {
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

            return await device.CloudProxy.Filter(cp => cp.IsActive)
                .Map(cp => cp.CloseAsync())
                .GetOrElse(Task.FromResult(true));
        }        

        public async Task<Try<ICloudProxy>> CreateCloudConnectionAsync(IIdentity identity)
        {
            Preconditions.CheckNotNull(identity, nameof(identity));
            Try<ICloudProxy> cloudProxy = await this.cloudProxyProvider.Connect(identity.ConnectionString);
            if (cloudProxy.Success)
            {
                ConnectedDevice device = this.GetOrCreateConnectedDevice(identity);
                Option<ICloudProxy> currentCloudProxy = device.UpdateCloudProxy(cloudProxy.Value);
                await currentCloudProxy.Filter(cp => cp.IsActive)
                    .Map(cp => cp.CloseAsync())
                    .GetOrElse(Task.FromResult(true));
            }
            return cloudProxy;
        }

        public Task<Try<ICloudProxy>> GetOrCreateCloudConnectionAsync(IIdentity identity)
        {
            ConnectedDevice device = this.GetOrCreateConnectedDevice(identity);

            return device.CloudProxy.Filter(cp => cp.IsActive)
                .Match(cp => Task.FromResult(Try.Success(cp)),
                    () =>
                    {
                        // TODO - Temporary code to allow multiple modules to use same connection to the IoTHub.
                        // Once IoTHub supports modules connecting to it, we should remove this. 
                         Option<ICloudProxy> existingConnection = this.CheckExistingCloudConnection(identity);
                         return existingConnection.Match(
                             c =>
                             {
                                 device.UpdateCloudProxy(c);
                                 return Task.FromResult(Try.Success(c));
                             }, 
                             () => this.CreateCloudConnectionAsync(identity));                        
                    }
            );
        }

        Option<ICloudProxy> CheckExistingCloudConnection(IIdentity identity)
        {
            KeyValuePair<string, ConnectedDevice> deviceWithActiveCloudConnection = this.devices.FirstOrDefault(
                d => IsSameDevice(identity, d.Value.Identity) && d.Value.CloudProxy.Exists(cp => cp.IsActive));
            return deviceWithActiveCloudConnection.Equals(default(KeyValuePair<string, ConnectedDevice>))
                ? Option.None<ICloudProxy>()
                : deviceWithActiveCloudConnection.Value.CloudProxy;
        }

        static bool IsSameDevice(IIdentity id1, IIdentity id2)
        {
            return GetDeviceId(id1).Equals(GetDeviceId(id2), StringComparison.OrdinalIgnoreCase);
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
            ConnectedDevice device = this.devices.GetOrAdd(Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId)), new ConnectedDevice(identity));
            return device;
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
    }
}
