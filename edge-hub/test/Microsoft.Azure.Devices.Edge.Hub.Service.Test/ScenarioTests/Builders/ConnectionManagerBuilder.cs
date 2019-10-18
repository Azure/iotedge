namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ConnectionManagerBuilder
    {
        private string iotHubName = TestContext.IotHubName;

        // the default behavior is that if nothing specified, this class adds a device
        // however if the user explicitly says they don't need any device, this flag shows that
        private bool withDevices = true;
        private List<ConnectedDeviceBuilder> toBeBuiltDevices = new List<ConnectedDeviceBuilder>();

        public static ConnectionManagerBuilder Create() => new ConnectionManagerBuilder();

        public ConnectionManagerBuilder WithHubName(string iotHubName)
        {
            this.iotHubName = iotHubName;
            return this;
        }

        public ConnectionManagerBuilder WithNoDevices()
        {
            this.withDevices = false;
            return this;
        }

        public ConnectionManagerBuilder WithConnectedDevice(Func<ConnectedDeviceBuilder, ConnectedDeviceBuilder> connectedDevice)
        {
            this.toBeBuiltDevices.Add(connectedDevice(new ConnectedDeviceBuilder()));
            return this;
        }

        public ConnectionManager Build()
        {
            var result = new ConnectionManager(
                                new SimpleCloudConnectionProvider(),
                                new NullCredentialsCache(),
                                new IdentityProvider(this.iotHubName));

            if (this.withDevices)
            {
                if (this.toBeBuiltDevices.Count > 0)
                {
                    this.toBeBuiltDevices.ForEach(device => device.Build(result, this.iotHubName));
                }
                else
                {
                    new ConnectedDeviceBuilder().Build(result, this.iotHubName);
                }
            }

            return result;
        }

        // This class is designed to use from ConnectionManagerBuilder and should not be created explicitly
        public class ConnectedDeviceBuilder
        {
            private string deviceId = TestContext.DeviceId;
            private string moduleId = TestContext.ModuleId;

            private List<DeviceSubscription> subscriptions = new List<DeviceSubscription>();
            private bool useDefaultSubscription = true;

            private Func<IIdentity, IDeviceProxy> deviceProvider = id => new AllGoodDeviceProxy().WithIdentity(id);
            private ICloudProxy cloudProxy = new AllGoodCloudProxy();

            private bool isModule = false;

            public ConnectedDeviceBuilder AsModule()
            {
                this.isModule = true;
                return this;
            }

            public ConnectedDeviceBuilder AsDevice()
            {
                this.isModule = false;
                return this;
            }

            public ConnectedDeviceBuilder WithDeviceId(string deviceId)
            {
                this.deviceId = deviceId;
                return this;
            }

            public ConnectedDeviceBuilder WithModuleId(string moduleId)
            {
                this.AsModule();
                this.moduleId = moduleId;
                return this;
            }

            public ConnectedDeviceBuilder WithSubscription(params DeviceSubscription[] subscriptions)
            {
                useDefaultSubscription = false;
                this.subscriptions.AddRange(subscriptions);
                return this;
            }

            public ConnectedDeviceBuilder WithDeviceProxy(IDeviceProxy deviceProxy)
            {
                this.deviceProvider =
                    id =>
                    {                        
                        deviceProxy.AsPrivateAccessible().Identity = id;
                        return deviceProxy;
                    };

                return this;
            }

            public ConnectedDeviceBuilder WithDeviceProxy<T>()
                where T : IDeviceProxy, new()
            {
                this.deviceProvider =
                    id =>
                    {
                        var deviceProxy = new T();
                        deviceProxy.AsPrivateAccessible().Identity = id;
                        return deviceProxy;
                    };

                return this;
            }

            public ConnectedDeviceBuilder WithCloudProxy<T>()
                where T : ICloudProxy, new()
            {
                this.cloudProxy = new T();
                return this;
            }

            public ConnectedDeviceBuilder WithCloudProxy<T>(Func<T,T> cloudProxy)
                where T : ICloudProxy, new()
            {
                this.cloudProxy = cloudProxy(new T());
                return this;
            }

            public void Build(ConnectionManager connectionManager, string iotHubName)
            {  
                var identity = this.GetIdentity(iotHubName);
                var device = this.deviceProvider(identity);
                
                var connectedDevice = connectionManager.AsPrivateAccessible().CreateOrUpdateConnectedDevice(identity) as Object;
                connectedDevice.AsPrivateAccessible().CloudConnection = Option.Some(new SimpleCloudConnection(this.cloudProxy) as ICloudConnection);
                connectedDevice.AsPrivateAccessible().AddDeviceConnection(this.deviceProvider(identity));

                if (this.useDefaultSubscription)
                {
                    connectionManager.AddSubscription(identity.Id, DeviceSubscription.C2D);
                    connectionManager.AddSubscription(identity.Id, DeviceSubscription.DesiredPropertyUpdates);
                    connectionManager.AddSubscription(identity.Id, DeviceSubscription.Methods);
                    connectionManager.AddSubscription(identity.Id, DeviceSubscription.ModuleMessages);
                    connectionManager.AddSubscription(identity.Id, DeviceSubscription.TwinResponse);
                }
                else
                {
                    this.subscriptions.ForEach(subscription => connectionManager.AddSubscription(identity.Id, subscription));
                }
                
                return;
            }

            private IIdentity GetIdentity(string iotHubName)
            {
                if (this.isModule)
                {
                    var idComponents = this.moduleId.Split('/');

                    if (idComponents.LongLength != 2)
                    {
                        throw new InvalidOperationException($"Bad test setup, module id pattern should be [deviceId/moduleId] : {this.moduleId}");
                    }

                    return new ModuleIdentity(iotHubName, idComponents[0], idComponents[1]);
                }
                else
                {
                    return new DeviceIdentity(iotHubName, this.deviceId);
                }
            }
        }
    }
}
