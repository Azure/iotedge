
// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Collections.Concurrent;
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

        public void AddDeviceConnection(IHubDeviceIdentity hubDeviceIdentity, IDeviceProxy deviceProxy)
        {
            ConnectedDevice device = this.GetOrCreateConnectedDevice(Preconditions.CheckNotNull(hubDeviceIdentity, nameof(hubDeviceIdentity)));
            Option<IDeviceProxy> currentDeviceProxy = device.UpdateDeviceProxy(Preconditions.CheckNotNull(deviceProxy, nameof(deviceProxy)));

            currentDeviceProxy.Filter(dp => dp.IsActive)
                .ForEach(dp => dp.Close(new MultipleConnectionsException($"Multiple connections detected for device {hubDeviceIdentity.Id}")));
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

        public async Task<bool> CloseConnection(string deviceId)
        {
            if (!this.devices.TryGetValue(Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId)), out ConnectedDevice device))
            {
                return false;
            }

            device.DeviceProxy.Filter(dp => dp.IsActive)
                .ForEach(dp => dp.Close(new MultipleConnectionsException($"Multiple connections detected for device {deviceId}")));

            return await device.CloudProxy.Filter(cp => cp.IsActive)
                .Map(cp => cp.CloseAsync())
                .GetOrElse(Task.FromResult(true));            
        }

        public async Task<Try<ICloudProxy>> CreateCloudConnection(IHubDeviceIdentity hubDeviceIdentity)
        {
            Preconditions.CheckNotNull(hubDeviceIdentity, nameof(hubDeviceIdentity));
            Try<ICloudProxy> cloudProxy = await this.cloudProxyProvider.Connect(hubDeviceIdentity.ConnectionString);
            if (cloudProxy.Success)
            {
                ConnectedDevice device = this.GetOrCreateConnectedDevice(hubDeviceIdentity);
                Option<ICloudProxy> currentCloudProxy = device.UpdateCloudProxy(cloudProxy.Value);
                await currentCloudProxy.Filter(cp => cp.IsActive)
                    .Map(cp => cp.CloseAsync())
                    .GetOrElse(Task.FromResult(true));
            }
            return cloudProxy;
        }

        public Task<Try<ICloudProxy>> GetOrCreateCloudConnection(IHubDeviceIdentity hubDeviceIdentity)
        {
            ConnectedDevice device = this.GetOrCreateConnectedDevice(hubDeviceIdentity);

            return device.CloudProxy.Filter(cp => cp.IsActive)
                .Match(cp => Task.FromResult(Try.Success(cp)), () => this.CreateCloudConnection(hubDeviceIdentity));            
        }

        ConnectedDevice GetOrCreateConnectedDevice(IHubDeviceIdentity hubDeviceIdentity)
        {
            string deviceId = Preconditions.CheckNotNull(hubDeviceIdentity, nameof(hubDeviceIdentity)).Id;
            ConnectedDevice device = this.devices.GetOrAdd(Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId)), new ConnectedDevice(hubDeviceIdentity));
            return device;
        }

        class ConnectedDevice
        {
            readonly object lockObject = new object();

            public ConnectedDevice(IHubDeviceIdentity deviceIdentity)
            {
                this.DeviceIdentity = deviceIdentity;
                this.CloudProxy = Option.None<ICloudProxy>();
                this.DeviceProxy = Option.None<IDeviceProxy>();
            }

            IHubDeviceIdentity DeviceIdentity { get; }

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
