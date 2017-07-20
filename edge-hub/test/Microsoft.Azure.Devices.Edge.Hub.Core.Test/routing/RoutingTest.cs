// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Newtonsoft.Json;
    using Xunit;
    using IMessage = Microsoft.Azure.Devices.Edge.Hub.Core.IMessage;
    using Message = Microsoft.Azure.Devices.Edge.Hub.Core.Test.Message;
    using SystemProperties = Microsoft.Azure.Devices.Edge.Hub.Core.SystemProperties;

    [Integration]
    public class RoutingTest
    {
        [Fact]
        public async Task RouteToCloudTest()
        {
            var routes = new List<string>
            {
                "FROM /messages/events INTO $upstream"
            };

            string edgeDeviceId = "edge";
            var iotHub = new IoTHub();
            (IEdgeHub edgeHub, IConnectionManager connectionManager) = await SetupEdgeHub(routes, iotHub, edgeDeviceId);

            TestDevice device1 = await TestDevice.Create("device1", edgeHub, connectionManager);
            TestModule module1 = await TestModule.Create(edgeDeviceId, "mod1", "op1", "in1", edgeHub, connectionManager);

            IMessage message = GetMessage();
            await device1.SendMessage(message);

            Assert.True(iotHub.HasReceivedMessage(message));
            Assert.False(module1.HasReceivedMessage(message));
        }

        [Fact]
        public async Task RouteToModuleTest()
        {
            var routes = new List<string>
            {
                @"FROM /messages/events INTO BrokeredEndpoint(""/modules/mod1/inputs/in1"")"
            };

            string edgeDeviceId = "edge";
            var iotHub = new IoTHub();
            (IEdgeHub edgeHub, IConnectionManager connectionManager) = await SetupEdgeHub(routes, iotHub, edgeDeviceId);

            TestDevice device1 = await TestDevice.Create("device1", edgeHub, connectionManager);
            TestModule module1 = await TestModule.Create(edgeDeviceId, "mod1", "op1", "in1", edgeHub, connectionManager);

            IMessage message = GetMessage();
            await device1.SendMessage(message);

            Assert.False(iotHub.HasReceivedMessage(message));
            Assert.True(module1.HasReceivedMessage(message));
        }

        [Fact]
        public async Task MultipleRoutesTest()
        {
            var routes = new List<string>
            {
                @"FROM /messages/events INTO BrokeredEndpoint(""/modules/ml/inputs/in1"")",
                @"FROM /messages/modules/ml INTO BrokeredEndpoint(""/modules/asa/inputs/input1"")",
                @"FROM /messages/modules/asa/outputs/output1 INTO $upstream"
            };

            string edgeDeviceId = "edge";
            var iotHub = new IoTHub();
            (IEdgeHub edgeHub, IConnectionManager connectionManager) = await SetupEdgeHub(routes, iotHub, edgeDeviceId);

            TestDevice device1 = await TestDevice.Create("device1", edgeHub, connectionManager);
            TestModule moduleMl = await TestModule.Create(edgeDeviceId, "ml", "op1", "in1", edgeHub, connectionManager);
            TestModule moduleAsa = await TestModule.Create(edgeDeviceId, "asa", "output1", "input1", edgeHub, connectionManager);

            IMessage deviceMessage = GetMessage();
            await device1.SendMessage(deviceMessage);
            Assert.False(iotHub.HasReceivedMessage(deviceMessage));
            Assert.True(moduleMl.HasReceivedMessage(deviceMessage));
            Assert.False(moduleAsa.HasReceivedMessage(deviceMessage));

            IMessage mlMessage = GetMessage();
            await moduleMl.SendMessage(mlMessage);
            Assert.False(iotHub.HasReceivedMessage(mlMessage));
            Assert.False(moduleMl.HasReceivedMessage(mlMessage));
            Assert.True(moduleAsa.HasReceivedMessage(mlMessage));

            IMessage asaMessage = GetMessage();
            await moduleAsa.SendMessage(asaMessage);
            Assert.True(iotHub.HasReceivedMessage(asaMessage));
            Assert.False(moduleMl.HasReceivedMessage(asaMessage));
            Assert.False(moduleAsa.HasReceivedMessage(asaMessage));
        }

        [Fact]
        public async Task RoutesWithConditionsTest1()
        {
            var routes = new List<string>
            {
                @"FROM /messages/events WHERE as_number(temp) > 50 INTO BrokeredEndpoint(""/modules/ml/inputs/in1"")",
                @"FROM /messages/modules/ml WHERE messageType = 'alert' INTO BrokeredEndpoint(""/modules/asa/inputs/input1"")",
                @"FROM /messages/modules/asa/outputs/output1 WHERE info = 'aggregate' INTO $upstream"
            };

            string edgeDeviceId = "edge";
            var iotHub = new IoTHub();
            (IEdgeHub edgeHub, IConnectionManager connectionManager) = await SetupEdgeHub(routes, iotHub, edgeDeviceId);

            TestDevice device1 = await TestDevice.Create("device1", edgeHub, connectionManager);
            TestModule moduleMl = await TestModule.Create(edgeDeviceId, "ml", "op1", "in1", edgeHub, connectionManager);
            TestModule moduleAsa = await TestModule.Create(edgeDeviceId, "asa", "output1", "input1", edgeHub, connectionManager);

            IMessage deviceMessage = GetMessage();
            deviceMessage.Properties.Add("temp", "100");
            await device1.SendMessage(deviceMessage);
            Assert.False(iotHub.HasReceivedMessage(deviceMessage));
            Assert.True(moduleMl.HasReceivedMessage(deviceMessage));
            Assert.False(moduleAsa.HasReceivedMessage(deviceMessage));

            IMessage mlMessage = GetMessage();
            mlMessage.Properties.Add("messageType", "alert");
            await moduleMl.SendMessage(mlMessage);
            Assert.False(iotHub.HasReceivedMessage(mlMessage));
            Assert.False(moduleMl.HasReceivedMessage(mlMessage));
            Assert.True(moduleAsa.HasReceivedMessage(mlMessage));

            IMessage asaMessage = GetMessage();
            asaMessage.Properties.Add("info", "aggregate");
            await moduleAsa.SendMessage(asaMessage);
            Assert.True(iotHub.HasReceivedMessage(asaMessage));
            Assert.False(moduleMl.HasReceivedMessage(asaMessage));
            Assert.False(moduleAsa.HasReceivedMessage(asaMessage));
        }

        [Fact]
        public async Task RoutesWithConditionsTest2()
        {
            var routes = new List<string>
            {
                @"FROM /messages/events WHERE as_number(temp) > 50 INTO BrokeredEndpoint(""/modules/mod1/inputs/in1"")",
                @"FROM /messages/events WHERE as_number(temp) < 50 INTO BrokeredEndpoint(""/modules/mod2/inputs/in2"")",
            };

            string edgeDeviceId = "edge";
            var iotHub = new IoTHub();
            (IEdgeHub edgeHub, IConnectionManager connectionManager) = await SetupEdgeHub(routes, iotHub, edgeDeviceId);

            TestDevice device1 = await TestDevice.Create("device1", edgeHub, connectionManager);
            TestModule module1 = await TestModule.Create(edgeDeviceId, "mod1", "op1", "in1", edgeHub, connectionManager);
            TestModule module2 = await TestModule.Create(edgeDeviceId, "mod2", "op2", "in2", edgeHub, connectionManager);

            IMessage message1 = GetMessage();
            message1.Properties.Add("temp", "100");
            await device1.SendMessage(message1);
            Assert.False(iotHub.HasReceivedMessage(message1));
            Assert.True(module1.HasReceivedMessage(message1));
            Assert.False(module2.HasReceivedMessage(message1));

            IMessage message2 = GetMessage();
            message2.Properties.Add("temp", "20");
            await device1.SendMessage(message2);
            Assert.False(iotHub.HasReceivedMessage(message2));
            Assert.False(module1.HasReceivedMessage(message2));
            Assert.True(module2.HasReceivedMessage(message2));
        }

        [Fact]
        public async Task TestRoutingTwinChangeNotificationFromDevice()
        {
            var routes = new List<string>
            {
                @"FROM /twinChangeNotifications INTO BrokeredEndpoint(""/modules/mod1/inputs/in1"")"
            };

            string edgeDeviceId = "edge";
            var iotHub = new IoTHub();
            (IEdgeHub edgeHub, IConnectionManager connectionManager) = await SetupEdgeHub(routes, iotHub, edgeDeviceId);

            TestDevice device1 = await TestDevice.Create("device1", edgeHub, connectionManager);
            TestModule module1 = await TestModule.Create(edgeDeviceId, "mod1", "op1", "in1", edgeHub, connectionManager);

            IMessage message = GetReportedPropertiesMessage();
            await device1.UpdateReportedProperties(message);
            Assert.True(iotHub.HasReceivedTwinChangeNotification());
            Assert.True(module1.HasReceivedTwinChangeNotification());
        }

        [Fact]
        public async Task TestRoutingTwinChangeNotificationFromModule()
        {
            var routes = new List<string>
            {
                @"FROM /twinChangeNotifications INTO BrokeredEndpoint(""/modules/mod1/inputs/in1"")"
            };

            string edgeDeviceId = "edge";
            var iotHub = new IoTHub();
            (IEdgeHub edgeHub, IConnectionManager connectionManager) = await SetupEdgeHub(routes, iotHub, edgeDeviceId);

            TestModule module1 = await TestModule.Create(edgeDeviceId, "mod1", "op1", "in1", edgeHub, connectionManager);
            TestModule module2 = await TestModule.Create(edgeDeviceId, "mod2", "op2", "in2", edgeHub, connectionManager);

            IMessage message = GetReportedPropertiesMessage();
            await module2.UpdateReportedProperties(message);
            Assert.True(iotHub.HasReceivedTwinChangeNotification());
            Assert.True(module1.HasReceivedTwinChangeNotification());
        }

        static async Task<(IEdgeHub, IConnectionManager)> SetupEdgeHub(IEnumerable<string> routes, IoTHub iotHub, string edgeDeviceId)
        {
            string iotHubName = "testHub";

            Routing.UserMetricLogger = NullRoutingUserMetricLogger.Instance;
            Routing.PerfCounter = NullRoutingPerfCounter.Instance;
            Routing.UserAnalyticsLogger = NullUserAnalyticsLogger.Instance;

            RetryStrategy defaultRetryStrategy = new FixedInterval(0, TimeSpan.FromSeconds(1));
            TimeSpan defaultRevivePeriod = TimeSpan.FromHours(1);
            TimeSpan defaultTimeout = TimeSpan.FromSeconds(60);
            var endpointExecutorConfig = new EndpointExecutorConfig(defaultTimeout, defaultRetryStrategy, defaultRevivePeriod, true);

            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(c => c.SendMessageAsync(It.IsAny<IMessage>())).Callback<IMessage>(m => iotHub.ReceivedMessages.Add(m)).ReturnsAsync(true);
            cloudProxy.Setup(c => c.UpdateReportedPropertiesAsync(It.IsAny<IMessage>())).Callback<IMessage>(m => iotHub.ReceivedMessages.Add(m)).Returns(Task.CompletedTask);
            cloudProxy.SetupGet(c => c.IsActive).Returns(true);

            var cloudProxyProvider = new Mock<ICloudProxyProvider>();
            cloudProxyProvider.Setup(c => c.Connect(It.IsAny<IIdentity>())).ReturnsAsync(Try.Success(cloudProxy.Object));
            IConnectionManager connectionManager = new ConnectionManager(cloudProxyProvider.Object, edgeDeviceId);
            var routingMessageConverter = new RoutingMessageConverter();
            RouteFactory routeFactory = new EdgeRouteFactory(new EndpointFactory(connectionManager, routingMessageConverter, edgeDeviceId));
            IEnumerable<Route> routesList = routeFactory.Create(routes).ToList();
            IEnumerable<Endpoint> endpoints = routesList.SelectMany(r => r.Endpoints);
            var routerConfig = new RouterConfig(endpoints, routesList);
            Router router = await Router.CreateAsync(Guid.NewGuid().ToString(), iotHubName, routerConfig, new SyncEndpointExecutorFactory(endpointExecutorConfig));
            IEdgeHub edgeHub = new RoutingEdgeHub(router, routingMessageConverter, connectionManager);
            return (edgeHub, connectionManager);
        }

        class TestModule
        {
            readonly IDeviceListener deviceListener;
            readonly IModuleIdentity moduleIdentity;
            readonly List<IMessage> receivedMessages;
            readonly string outputName;

            TestModule(IModuleIdentity moduleIdentity, string endpointId, IDeviceListener deviceListener, List<IMessage> receivedMessages)
            {
                this.moduleIdentity = moduleIdentity;
                this.outputName = endpointId;
                this.deviceListener = deviceListener;
                this.receivedMessages = receivedMessages;
            }

            public static async Task<TestModule> Create(string deviceId, string moduleId, string outputEndpointId, string inputEndpointId, IEdgeHub edgeHub, IConnectionManager connectionManager)
            {
                IModuleIdentity moduleIdentity = SetupModuleIdentity(moduleId, deviceId);
                Try<ICloudProxy> cloudProxy = await connectionManager.GetOrCreateCloudConnectionAsync(moduleIdentity);
                Assert.True(cloudProxy.Success);
                var deviceListener = new DeviceListener(moduleIdentity, edgeHub, connectionManager, cloudProxy.Value);
                var receivedMessages = new List<IMessage>();
                var deviceProxy = new Mock<IDeviceProxy>();
                deviceProxy.Setup(d => d.SendMessageAsync(It.IsAny<IMessage>(), It.Is<string>(e => e.Equals(inputEndpointId, StringComparison.OrdinalIgnoreCase)))).Callback<IMessage, string>((m, e) => receivedMessages.Add(m)).ReturnsAsync(true);
                deviceProxy.SetupGet(d => d.IsActive).Returns(true);
                connectionManager.AddDeviceConnection(moduleIdentity, deviceProxy.Object);
                return new TestModule(moduleIdentity, outputEndpointId, deviceListener, receivedMessages);
            }

            public Task SendMessage(IMessage message)
            {
                message.SystemProperties[SystemProperties.ConnectionDeviceId] = this.moduleIdentity.DeviceId;
                message.SystemProperties[SystemProperties.ConnectionModuleId] = this.moduleIdentity.ModuleId;
                message.SystemProperties[SystemProperties.OutputName] = this.outputName;
                return this.deviceListener.ProcessMessageAsync(message);
            }

            public bool HasReceivedMessage(IMessage message) => this.receivedMessages.Any(m =>
                m.SystemProperties[SystemProperties.MessageId] == message.SystemProperties[SystemProperties.MessageId]);

            public Task UpdateReportedProperties(IMessage reportedPropertiesMessage) => 
                this.deviceListener.UpdateReportedPropertiesAsync(reportedPropertiesMessage);

            public bool HasReceivedTwinChangeNotification() => this.receivedMessages.Any(m =>
                m.SystemProperties[SystemProperties.MessageType] == Core.Constants.TwinChangeNotificationMessageType);
        }

        class TestDevice
        {
            readonly IDeviceListener deviceListener;
            readonly IDeviceIdentity deviceIdentity;

            TestDevice(IDeviceIdentity deviceIdentity, IDeviceListener deviceListener)
            {
                this.deviceIdentity = deviceIdentity;
                this.deviceListener = deviceListener;
            }

            public static async Task<TestDevice> Create(string deviceId, IEdgeHub edgeHub, IConnectionManager connectionManager)
            {
                IDeviceIdentity deviceIdentity = SetupDeviceIdentity(deviceId);
                Try<ICloudProxy> cloudProxy = await connectionManager.GetOrCreateCloudConnectionAsync(deviceIdentity);
                Assert.True(cloudProxy.Success);
                var deviceListener = new DeviceListener(deviceIdentity, edgeHub, connectionManager, cloudProxy.Value);
                return new TestDevice(deviceIdentity, deviceListener);
            }

            public Task SendMessage(IMessage message)
            {
                message.SystemProperties[SystemProperties.ConnectionDeviceId] = this.deviceIdentity.DeviceId;
                return this.deviceListener.ProcessMessageAsync(message);
            }

            public Task UpdateReportedProperties(IMessage reportedPropertiesMessage) => 
                this.deviceListener.UpdateReportedPropertiesAsync(reportedPropertiesMessage);
        }

        class IoTHub
        {
            public List<IMessage> ReceivedMessages { get; } = new List<IMessage>();

            public bool HasReceivedMessage(IMessage message) => this.ReceivedMessages.Any(m =>
                m.SystemProperties[SystemProperties.MessageId] == message.SystemProperties[SystemProperties.MessageId]);

            public bool HasReceivedTwinChangeNotification() => this.ReceivedMessages.Any(m =>
                m.SystemProperties[SystemProperties.MessageType] == Core.Constants.TwinChangeNotificationMessageType);
        }

        static IMessage GetMessage()
        {
            byte[] messageBody = Encoding.UTF8.GetBytes("Message body");
            var properties = new Dictionary<string, string>()
            {
                { "Prop1", "Val1" },
                { "Prop2", "Val2" }
            };

            var systemProperties = new Dictionary<string, string>
            {
                { SystemProperties.MessageId, Guid.NewGuid().ToString() }
            };
            return new Message(messageBody, properties, systemProperties);
        }

        static IMessage GetReportedPropertiesMessage()
        {
            var twinCollection = new TwinCollection();
            twinCollection["Status"] = "running";
            twinCollection["ElapsedTime"] = "0.5";
            byte[] messageBody = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(twinCollection));            
            return new Message(messageBody);
        }

        static IDeviceIdentity SetupDeviceIdentity(string deviceId) => new DeviceIdentity(
            "",
            deviceId,
            Guid.NewGuid().ToString(),
            AuthenticationScope.SasToken,
            null,
            "");

        static IModuleIdentity SetupModuleIdentity(string moduleId, string deviceId) => new ModuleIdentity(
            "",
            deviceId,
            moduleId,
            "",
            AuthenticationScope.SasToken,
            null,
            "");
    }
}