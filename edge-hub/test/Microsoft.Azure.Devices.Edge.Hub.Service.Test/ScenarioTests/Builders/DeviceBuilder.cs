// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Routing;

    public class DeviceBuilder
    {
        private IIdentity identity = new DeviceIdentity(TestContext.IotHubName, TestContext.DeviceId);
        private RoutingEdgeHub edgeHub;
        private IDeviceProxy deviceProxy = new AllGoodDeviceProxy();

        private bool withNoSubscription = false;
        private List<DeviceSubscription> deviceSubscriptions = new List<DeviceSubscription>();

        public static DeviceBuilder Create() => new DeviceBuilder();

        public DeviceBuilder WithIdentity(IIdentity identity)
        {
            this.identity = identity;
            return this;
        }

        public DeviceBuilder WithDeviceProxy(IDeviceProxy deviceProxy)
        {
            this.deviceProxy = deviceProxy;
            return this;
        }

        public DeviceBuilder WithDeviceProxy<T>(Func<T, T> deviceProxyDecorator)
            where T : IDeviceProxy, new()
        {
            this.deviceProxy = deviceProxyDecorator(new T());
            return this;
        }

        public DeviceBuilder WithEdgeHub(RoutingEdgeHub edgeHub)
        {
            this.edgeHub = edgeHub;
            return this;
        }

        public DeviceBuilder WithNoSubscription()
        {
            this.withNoSubscription = true;
            return this;
        }

        public DeviceBuilder WithSubscription(DeviceSubscription subscription)
        {
            this.deviceSubscriptions.Add(subscription);
            return this;
        }

        public PublicDeviceMessageHandler Build()
        {
            var hub = this.edgeHub ?? EdgeHubBuilder.Create().Build();
            var connectionManager = hub.AsPrivateAccessible().connectionManager as ConnectionManager;
            var innerdeviceMessageHandler = GetNonPublicDeviceMessageHandler(this.identity, hub, connectionManager);

            innerdeviceMessageHandler.AsPrivateAccessible().BindDeviceProxy(this.deviceProxy);
            var result = new PublicDeviceMessageHandler(innerdeviceMessageHandler);

            // this is needed to make fake proxies to be able to call back
            if (this.deviceProxy is IListenerBoundProxy listenerBoundProxy)
            {
                listenerBoundProxy.BindListener(result);
            }

            this.AddSubscriptions(result);

            return result;
        }

        private void AddSubscriptions(PublicDeviceMessageHandler handler)
        {
            var subscriptions = default(List<DeviceSubscription>);
            if (this.deviceSubscriptions.Count == 0)
            {
                if (this.withNoSubscription)
                {
                    subscriptions = new List<DeviceSubscription>();
                }
                else
                {
                    subscriptions = new List<DeviceSubscription>
                    {
                        DeviceSubscription.C2D,
                        DeviceSubscription.DesiredPropertyUpdates,
                        DeviceSubscription.Methods,
                        DeviceSubscription.ModuleMessages,
                        DeviceSubscription.TwinResponse
                    };
                }
            }
            else
            {
                subscriptions = this.deviceSubscriptions;
            }

            subscriptions.ForEach(s => handler.AddSubscription(s).Wait());
        }

        private static object GetNonPublicDeviceMessageHandler(IIdentity identity, RoutingEdgeHub hub, object connectionManager)
        {
            // using IDeviceListener just because it happen to live in the same assembly we want a type from
            var deviceMessageHandlerType = typeof(IDeviceListener).Assembly.GetType("Microsoft.Azure.Devices.Edge.Hub.Core.Device.DeviceMessageHandler");
            var deviceMessageHandler = Activator.CreateInstance(deviceMessageHandlerType, BindingFlags.Public | BindingFlags.Instance, null, new object[] { identity, hub, connectionManager }, null);

            return deviceMessageHandler;
        }
    }
}
