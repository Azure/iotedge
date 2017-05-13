// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Routing;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints;
    using Microsoft.Azure.Devices.Routing.Core.TransientFaultHandling;
    using Moq;
    using Xunit;
    using IMessage = Microsoft.Azure.Devices.Edge.Hub.Core.IMessage;
    using IRoutingMessage = Microsoft.Azure.Devices.Routing.Core.IMessage;
    using Message = Microsoft.Azure.Devices.Edge.Hub.Core.Test.Message;

    public class RoutingTest
    {
        static readonly RetryStrategy DefaultRetryStrategy = new FixedInterval(0, TimeSpan.FromSeconds(1));
        static readonly TimeSpan DefaultRevivePeriod = TimeSpan.FromHours(1);
        static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);
        static readonly EndpointExecutorConfig DefaultConfig = new EndpointExecutorConfig(DefaultTimeout, DefaultRetryStrategy, DefaultRevivePeriod, true);

        public static IEnumerable<object[]> GetRoutingData()
        {
            string deviceId = "device1";
            string moduleId = "module1";
            string id = $"{deviceId}/{moduleId}";
            var mockModuleIdentity = new Mock<IModuleIdentity>();
            mockModuleIdentity.SetupGet(p => p.DeviceId).Returns(deviceId);
            mockModuleIdentity.SetupGet(p => p.ModuleId).Returns(moduleId);
            mockModuleIdentity.SetupGet(p => p.Id).Returns(id);

            var mockDeviceIdentity = new Mock<IDeviceIdentity>();
            mockDeviceIdentity.SetupGet(p => p.DeviceId).Returns(deviceId);
            mockDeviceIdentity.SetupGet(p => p.Id).Returns(deviceId);

            byte[] messageBody = Encoding.UTF8.GetBytes("Message body");
            var properties = new Dictionary<string, string>()
            {
                { "Prop1", "Val1" },
                { "Prop2", "Val2" },
            };
            var systemProperties = new Dictionary<string, string>
            {
                { SystemProperties.DeviceId, deviceId }
            };
            var message = new Message(messageBody, properties, systemProperties);

            var routingData = new List<object[]>();
            routingData.Add(new object[] { mockModuleIdentity.Object, message });
            routingData.Add(new object[] { mockDeviceIdentity.Object, message });
            return routingData;
        }

        [Theory]
        [Integration]
        [MemberData(nameof(GetRoutingData))]
        public async Task TestRoutingToCloud(IIdentity identity, Message message)
        {
            string iotHubName = "TestIotHub";
            Core.IMessageConverter<IRoutingMessage> routingMessageConverter = new RoutingMessageConverter();

            IMessage cloudMessage = null;
            var cloudProxyMock = new Mock<ICloudProxy>();
            cloudProxyMock.Setup(c => c.SendMessageAsync(It.IsAny<IMessage>()))
                .Callback<IMessage>((m) => cloudMessage = m)
                .ReturnsAsync(() => true);
            cloudProxyMock.SetupGet(p => p.IsActive).Returns(true);

            Util.Option<ICloudProxy> GetCloudProxy(string destId) => destId.Equals(identity.Id)
                ? Option.Some(cloudProxyMock.Object)
                : Option.None<ICloudProxy>();

            var cloudEndpoint = new CloudEndpoint(Guid.NewGuid().ToString(), GetCloudProxy, routingMessageConverter);

            Router router = await SetupRouter(cloudEndpoint, iotHubName);
            IEdgeHub edgeHub = new RoutingEdgeHub(router, routingMessageConverter);

            await edgeHub.ProcessDeviceMessage(identity, message);

            CheckOutput(identity, message, cloudMessage);
        }

        [Theory]
        [Integration]
        [MemberData(nameof(GetRoutingData))]
        public async Task TestRoutingToDevice(IIdentity identity, Message message)
        {
            string iotHubName = "TestIotHub";
            Core.IMessageConverter<IRoutingMessage> routingMessageConverter = new RoutingMessageConverter();

            string moduleEndpoint = "in1";
            IMessage deviceMessage = null;
            var deviceProxyMock = new Mock<IDeviceProxy>();
            deviceProxyMock.Setup(c => c.SendMessage(It.IsAny<IMessage>(), It.Is<string>((ep) => ep.Equals(moduleEndpoint, StringComparison.OrdinalIgnoreCase))))
                .Callback<IMessage, string>((m, e) => deviceMessage = m)
                .ReturnsAsync(() => true);

            deviceProxyMock.SetupGet(p => p.IsActive).Returns(true);

            Util.Option<IDeviceProxy> GetDeviceProxy(string destId) => destId.Equals(identity.Id)
                ? Option.Some(deviceProxyMock.Object)
                : Option.None<IDeviceProxy>();
            
            var deviceEndpoint = new ModuleEndpoint(identity.Id, moduleEndpoint, GetDeviceProxy, routingMessageConverter);
            Router router = await SetupRouter(deviceEndpoint, iotHubName);

            IEdgeHub edgeHub = new RoutingEdgeHub(router, routingMessageConverter);

            await edgeHub.ProcessDeviceMessage(identity, message);

            CheckOutput(identity, message, deviceMessage);
        }

        static void CheckOutput(IIdentity identity, Message message, IMessage dispatchedMessage)
        {
            Assert.NotNull(dispatchedMessage);

            var moduleIdentity = identity as IModuleIdentity;
            if (moduleIdentity != null)
            {
                Assert.Equal(moduleIdentity.ModuleId, dispatchedMessage.Properties["moduleId"]);
            }
            Assert.Equal(identity.Id, dispatchedMessage.SystemProperties[SystemProperties.DeviceId]);

            foreach (KeyValuePair<string, string> property in message.SystemProperties)
            {
                Assert.True(dispatchedMessage.SystemProperties.ContainsKey(property.Key));
                Assert.Equal(property.Value, dispatchedMessage.SystemProperties[property.Key]);
            }

            foreach (KeyValuePair<string, string> property in message.SystemProperties)
            {
                Assert.True(dispatchedMessage.SystemProperties.ContainsKey(property.Key));
                Assert.Equal(property.Value, dispatchedMessage.SystemProperties[property.Key]);
            }
        }

        static async Task<Router> SetupRouter(Endpoint endpoint, string iotHubName)
        {
            Routing.PerfCounter = new NullRoutingPerfCounter();
            Routing.UserAnalyticsLogger = new NullUserAnalyticsLogger();
            Routing.UserMetricLogger = new NullRoutingUserMetricLogger();

            var endpoints = new HashSet<Endpoint>();
            var routes = new List<Route>();

            endpoints.Add(endpoint);

            var route = new Route(
                Guid.NewGuid().ToString(),
                "true",
                iotHubName,
                MessageSource.Telemetry,
                new HashSet<Endpoint>
                {
                    endpoint
                });

            routes.Add(route);

            var config = new RouterConfig(endpoints, routes);
            Router router = await Router.CreateAsync(Guid.NewGuid().ToString(), iotHubName, config, new SyncEndpointExecutorFactory(DefaultConfig));
            return router;
        }
    }
}