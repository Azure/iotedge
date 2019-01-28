// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Routing;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Storage;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Newtonsoft.Json;
    using Xunit;
    using Constants = Microsoft.Azure.Devices.Edge.Hub.Core.Constants;
    using IMessage = Microsoft.Azure.Devices.Edge.Hub.Core.IMessage;
    using Message = EdgeMessage;
    using SystemProperties = Microsoft.Azure.Devices.Edge.Hub.Core.SystemProperties;

    [Integration]
    public class RoutingTest
    {
        static readonly Random Rand = new Random();

        [Fact]
        public async Task RouteToCloudTest()
        {
            var routes = new List<string>
            {
                "FROM /messages INTO $upstream"
            };

            string edgeDeviceId = "edge";
            var iotHub = new IoTHub();
            (IEdgeHub edgeHub, IConnectionManager connectionManager) = await SetupEdgeHub(routes, iotHub, edgeDeviceId);

            TestDevice device1 = await TestDevice.Create("device1", edgeHub, connectionManager);
            TestModule module1 = await TestModule.Create(edgeDeviceId, "mod1", "op1", "in1", edgeHub, connectionManager);

            IList<IMessage> messages = GetMessages();
            await device1.SendMessages(messages);

            await Task.Delay(GetSleepTime());

            Assert.True(iotHub.HasReceivedMessages(messages));
            Assert.False(module1.HasReceivedMessages(messages));
        }

        [Fact]
        public async Task RouteToModuleTest()
        {
            var routes = new List<string>
            {
                @"FROM /messages INTO BrokeredEndpoint(""/modules/mod1/inputs/in1"")"
            };

            string edgeDeviceId = "edge";
            var iotHub = new IoTHub();
            (IEdgeHub edgeHub, IConnectionManager connectionManager) = await SetupEdgeHub(routes, iotHub, edgeDeviceId);

            TestDevice device1 = await TestDevice.Create("device1", edgeHub, connectionManager);
            TestModule module1 = await TestModule.Create(edgeDeviceId, "mod1", "op1", "in1", edgeHub, connectionManager);

            IList<IMessage> messages = GetMessages();
            await device1.SendMessages(messages);

            await Task.Delay(GetSleepTime());

            Assert.False(iotHub.HasReceivedMessages(messages));
            Assert.True(module1.HasReceivedMessages(messages));
        }

        [Fact]
        public async Task MultipleRoutesTest()
        {
            var routes = new List<string>
            {
                @"FROM /messages/* WHERE $connectionDeviceId = 'device1' INTO BrokeredEndpoint(""/modules/ml/inputs/in1"")",
                @"FROM /messages/modules/ml WHERE $connectionModuleId = 'ml' INTO BrokeredEndpoint(""/modules/asa/inputs/input1"")",
                @"FROM /messages/modules/asa/outputs/output1 INTO $upstream"
            };

            string edgeDeviceId = "edge";
            var iotHub = new IoTHub();
            (IEdgeHub edgeHub, IConnectionManager connectionManager) = await SetupEdgeHub(routes, iotHub, edgeDeviceId);

            TestDevice device1 = await TestDevice.Create("device1", edgeHub, connectionManager);
            TestModule moduleMl = await TestModule.Create(edgeDeviceId, "ml", "op1", "in1", edgeHub, connectionManager);
            TestModule moduleAsa = await TestModule.Create(edgeDeviceId, "asa", "output1", "input1", edgeHub, connectionManager);

            IList<IMessage> deviceMessages = GetMessages();
            await device1.SendMessages(deviceMessages);

            await Task.Delay(GetSleepTime());

            Assert.False(iotHub.HasReceivedMessages(deviceMessages));
            Assert.True(moduleMl.HasReceivedMessages(deviceMessages));
            Assert.False(moduleAsa.HasReceivedMessages(deviceMessages));

            IList<IMessage> mlMessages = GetMessages();
            await moduleMl.SendMessageOnOutput(mlMessages);

            await Task.Delay(GetSleepTime());

            Assert.False(iotHub.HasReceivedMessages(mlMessages));
            Assert.False(moduleMl.HasReceivedMessages(mlMessages));
            Assert.True(moduleAsa.HasReceivedMessages(mlMessages));

            IList<IMessage> asaMessages = GetMessages();
            await moduleAsa.SendMessageOnOutput(asaMessages);

            await Task.Delay(GetSleepTime());

            Assert.True(iotHub.HasReceivedMessages(asaMessages));
            Assert.False(moduleMl.HasReceivedMessages(asaMessages));
            Assert.False(moduleAsa.HasReceivedMessages(asaMessages));
        }

        [Fact]
        public async Task MultipleRoutesSameModuleTest()
        {
            var routes = new List<string>
            {
                @"FROM /messages/* WHERE $connectionDeviceId = 'device1' INTO BrokeredEndpoint(""/modules/ml/inputs/in1"")",
                @"FROM /messages/modules/ml/outputs/op1 WHERE $connectionModuleId = 'ml' INTO BrokeredEndpoint(""/modules/ml/inputs/in2"")",
                @"FROM /messages/modules/ml/outputs/op2 INTO BrokeredEndpoint(""/modules/asa/inputs/input1"")"
            };

            string edgeDeviceId = "edge";
            var iotHub = new IoTHub();
            (IEdgeHub edgeHub, IConnectionManager connectionManager) = await SetupEdgeHub(routes, iotHub, edgeDeviceId);

            TestDevice device1 = await TestDevice.Create("device1", edgeHub, connectionManager);
            TestModule moduleMl = await TestModule.Create(edgeDeviceId, "ml", "op1", new List<string> { "in1", "in2" }, edgeHub, connectionManager);
            TestModule moduleAsa = await TestModule.Create(edgeDeviceId, "asa", "output1", "input1", edgeHub, connectionManager);

            IList<IMessage> deviceMessages = GetMessages();
            await device1.SendMessages(deviceMessages);
            await Task.Delay(GetSleepTime());
            Assert.False(iotHub.HasReceivedMessages(deviceMessages));
            Assert.True(moduleMl.HasReceivedMessages(deviceMessages));
            Assert.False(moduleAsa.HasReceivedMessages(deviceMessages));

            IList<IMessage> mlMessages = GetMessages();
            await moduleMl.SendMessageOnOutput(mlMessages);
            await Task.Delay(GetSleepTime());
            Assert.False(iotHub.HasReceivedMessages(mlMessages));
            Assert.True(moduleMl.HasReceivedMessages(mlMessages));
            Assert.False(moduleAsa.HasReceivedMessages(mlMessages));

            IList<IMessage> mlMessages2 = GetMessages();
            await moduleMl.SendMessageOnOutput(mlMessages2, "op2");
            await Task.Delay(GetSleepTime());
            Assert.False(iotHub.HasReceivedMessages(mlMessages2));
            Assert.False(moduleMl.HasReceivedMessages(mlMessages2));
            Assert.True(moduleAsa.HasReceivedMessages(mlMessages2));
        }

        [Fact]
        public async Task MultipleRoutesTest_WithNoModuleOutput()
        {
            var routes = new List<string>
            {
                @"FROM /messages WHERE $connectionDeviceId = 'device1' INTO BrokeredEndpoint(""/modules/ml/inputs/in1"")",
                @"FROM /messages/modules/ml INTO BrokeredEndpoint(""/modules/asa/inputs/input1"")",
                @"FROM /messages/modules/asa/* INTO $upstream"
            };

            string edgeDeviceId = "edge";
            var iotHub = new IoTHub();
            (IEdgeHub edgeHub, IConnectionManager connectionManager) = await SetupEdgeHub(routes, iotHub, edgeDeviceId);

            TestDevice device1 = await TestDevice.Create("device1", edgeHub, connectionManager);
            TestModule moduleMl = await TestModule.Create(edgeDeviceId, "ml", "op1", "in1", edgeHub, connectionManager);
            TestModule moduleAsa = await TestModule.Create(edgeDeviceId, "asa", "output1", "input1", edgeHub, connectionManager);

            IList<IMessage> deviceMessages = GetMessages();
            await device1.SendMessages(deviceMessages);
            await Task.Delay(GetSleepTime(20));
            Assert.False(iotHub.HasReceivedMessages(deviceMessages));
            Assert.True(moduleMl.HasReceivedMessages(deviceMessages));
            Assert.False(moduleAsa.HasReceivedMessages(deviceMessages));

            IList<IMessage> mlMessages = GetMessages();
            await moduleMl.SendMessages(mlMessages);
            await Task.Delay(GetSleepTime());
            Assert.False(iotHub.HasReceivedMessages(mlMessages));
            Assert.False(moduleMl.HasReceivedMessages(mlMessages));
            Assert.True(moduleAsa.HasReceivedMessages(mlMessages));

            IList<IMessage> asaMessages = GetMessages();
            await moduleAsa.SendMessages(asaMessages);
            await Task.Delay(GetSleepTime());
            Assert.True(iotHub.HasReceivedMessages(asaMessages));
            Assert.False(moduleMl.HasReceivedMessages(asaMessages));
            Assert.False(moduleAsa.HasReceivedMessages(asaMessages));
        }

        [Fact]
        public async Task MultipleRoutesTest_WithNoModuleOutput_WrongRoute()
        {
            var routes = new List<string>
            {
                @"FROM /messages WHERE $connectionDeviceId = 'device1' INTO BrokeredEndpoint(""/modules/ml/inputs/in1"")",
                @"FROM /messages/modules/ml INTO BrokeredEndpoint(""/modules/asa/inputs/input1"")",
                @"FROM /messages/modules/asa/outputs/output1 INTO $upstream"
            };

            string edgeDeviceId = "edge";
            var iotHub = new IoTHub();
            (IEdgeHub edgeHub, IConnectionManager connectionManager) = await SetupEdgeHub(routes, iotHub, edgeDeviceId);

            TestDevice device1 = await TestDevice.Create("device1", edgeHub, connectionManager);
            TestModule moduleMl = await TestModule.Create(edgeDeviceId, "ml", "op1", "in1", edgeHub, connectionManager);
            TestModule moduleAsa = await TestModule.Create(edgeDeviceId, "asa", "output1", "input1", edgeHub, connectionManager);

            IList<IMessage> deviceMessages = GetMessages();
            await device1.SendMessages(deviceMessages);
            await Task.Delay(GetSleepTime());
            Assert.False(iotHub.HasReceivedMessages(deviceMessages));
            Assert.True(moduleMl.HasReceivedMessages(deviceMessages));
            Assert.False(moduleAsa.HasReceivedMessages(deviceMessages));

            IList<IMessage> mlMessages = GetMessages();
            await moduleMl.SendMessages(mlMessages);
            await Task.Delay(GetSleepTime());
            Assert.False(iotHub.HasReceivedMessages(mlMessages));
            Assert.False(moduleMl.HasReceivedMessages(mlMessages));
            Assert.True(moduleAsa.HasReceivedMessages(mlMessages));

            IList<IMessage> asaMessages = GetMessages();
            await moduleAsa.SendMessages(asaMessages);
            await Task.Delay(GetSleepTime());
            Assert.False(iotHub.HasReceivedMessages(asaMessages));
            Assert.False(moduleMl.HasReceivedMessages(asaMessages));
            Assert.False(moduleAsa.HasReceivedMessages(asaMessages));
        }

        [Fact]
        public async Task RoutesWithConditionsTest1()
        {
            var routes = new List<string>
            {
                @"FROM /messages WHERE as_number(temp) > 50 INTO BrokeredEndpoint(""/modules/ml/inputs/in1"")",
                @"FROM /messages/modules/ml WHERE messageType = 'alert' INTO BrokeredEndpoint(""/modules/asa/inputs/input1"")",
                @"FROM /messages/modules/asa/outputs/output1 WHERE info = 'aggregate' INTO $upstream"
            };

            string edgeDeviceId = "edge";
            var iotHub = new IoTHub();
            (IEdgeHub edgeHub, IConnectionManager connectionManager) = await SetupEdgeHub(routes, iotHub, edgeDeviceId);

            TestDevice device1 = await TestDevice.Create("device1", edgeHub, connectionManager);
            TestModule moduleMl = await TestModule.Create(edgeDeviceId, "ml", "op1", "in1", edgeHub, connectionManager);
            TestModule moduleAsa = await TestModule.Create(edgeDeviceId, "asa", "output1", "input1", edgeHub, connectionManager);

            List<IMessage> deviceMessages = GetMessages();
            deviceMessages.ForEach(d => d.Properties.Add("temp", "100"));
            await device1.SendMessages(deviceMessages);
            await Task.Delay(GetSleepTime());
            Assert.False(iotHub.HasReceivedMessages(deviceMessages));
            Assert.True(moduleMl.HasReceivedMessages(deviceMessages));
            Assert.False(moduleAsa.HasReceivedMessages(deviceMessages));

            List<IMessage> mlMessages = GetMessages();
            mlMessages.ForEach(d => d.Properties.Add("messageType", "alert"));
            await moduleMl.SendMessageOnOutput(mlMessages);
            await Task.Delay(GetSleepTime());
            Assert.False(iotHub.HasReceivedMessages(mlMessages));
            Assert.False(moduleMl.HasReceivedMessages(mlMessages));
            Assert.True(moduleAsa.HasReceivedMessages(mlMessages));

            List<IMessage> asaMessages = GetMessages();
            asaMessages.ForEach(d => d.Properties.Add("info", "aggregate"));
            await moduleAsa.SendMessageOnOutput(asaMessages);
            await Task.Delay(GetSleepTime());
            Assert.True(iotHub.HasReceivedMessages(asaMessages));
            Assert.False(moduleMl.HasReceivedMessages(asaMessages));
            Assert.False(moduleAsa.HasReceivedMessages(asaMessages));
        }

        [Fact]
        public async Task RoutesWithConditionsTest2()
        {
            var routes = new List<string>
            {
                @"FROM /messages WHERE as_number(temp) > 50 INTO BrokeredEndpoint(""/modules/mod1/inputs/in1"")",
                @"FROM /messages/* WHERE as_number(temp) < 50 INTO BrokeredEndpoint(""/modules/mod2/inputs/in2"")",
            };

            string edgeDeviceId = "edge";
            var iotHub = new IoTHub();
            (IEdgeHub edgeHub, IConnectionManager connectionManager) = await SetupEdgeHub(routes, iotHub, edgeDeviceId);

            TestDevice device1 = await TestDevice.Create("device1", edgeHub, connectionManager);
            TestModule module1 = await TestModule.Create(edgeDeviceId, "mod1", "op1", "in1", edgeHub, connectionManager);
            TestModule module2 = await TestModule.Create(edgeDeviceId, "mod2", "op2", "in2", edgeHub, connectionManager);

            List<IMessage> messages1 = GetMessages();
            messages1.ForEach(d => d.Properties.Add("temp", "100"));
            await device1.SendMessages(messages1);
            await Task.Delay(GetSleepTime());
            Assert.False(iotHub.HasReceivedMessages(messages1));
            Assert.True(module1.HasReceivedMessages(messages1));
            Assert.False(module2.HasReceivedMessages(messages1));

            List<IMessage> messages2 = GetMessages();
            messages2.ForEach(d => d.Properties.Add("temp", "20"));
            await device1.SendMessages(messages2);
            await Task.Delay(GetSleepTime());
            Assert.False(iotHub.HasReceivedMessages(messages2));
            Assert.False(module1.HasReceivedMessages(messages2));
            Assert.True(module2.HasReceivedMessages(messages2));
        }

        [Fact]
        public async Task RoutesWithConditionsOnSystemPropertiesTest1()
        {
            var routes = new List<string>
            {
                @"FROM /messages WHERE $contentType = 'application/json' AND $contentEncoding = 'utf-8' INTO $upstream",
                @"FROM /messages WHERE $contentType = 'application/json' AND $contentEncoding <> 'utf-8' INTO BrokeredEndpoint(""/modules/mod2/inputs/in2"")",
            };

            string edgeDeviceId = "edge";
            var iotHub = new IoTHub();
            (IEdgeHub edgeHub, IConnectionManager connectionManager) = await SetupEdgeHub(routes, iotHub, edgeDeviceId);

            TestDevice device1 = await TestDevice.Create("device1", edgeHub, connectionManager);
            TestModule module1 = await TestModule.Create(edgeDeviceId, "mod1", "op1", "in1", edgeHub, connectionManager);
            TestModule module2 = await TestModule.Create(edgeDeviceId, "mod2", "op2", "in2", edgeHub, connectionManager);

            List<IMessage> message1 = GetMessages();
            message1.ForEach(d => d.SystemProperties[SystemProperties.ContentType] = "application/json");
            message1.ForEach(d => d.SystemProperties[SystemProperties.ContentEncoding] = "utf-8");
            await device1.SendMessages(message1);
            await Task.Delay(GetSleepTime());
            Assert.True(iotHub.HasReceivedMessages(message1));
            Assert.False(module1.HasReceivedMessages(message1));
            Assert.False(module2.HasReceivedMessages(message1));

            List<IMessage> message2 = GetMessages();
            message2.ForEach(d => d.SystemProperties[SystemProperties.ContentType] = "application/json");
            message2.ForEach(d => d.SystemProperties[SystemProperties.ContentEncoding] = "utf-16");
            await device1.SendMessages(message2);
            await Task.Delay(GetSleepTime());
            Assert.False(iotHub.HasReceivedMessages(message2));
            Assert.False(module1.HasReceivedMessages(message2));
            Assert.True(module2.HasReceivedMessages(message2));
        }

        [Fact(Skip = "Flaky test, bug #2494150")]
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
            await Task.Delay(GetSleepTime());
            Assert.True(iotHub.HasReceivedTwinChangeNotification());
            Assert.True(module1.HasReceivedTwinChangeNotification());
        }

        [Fact(Skip = "Flaky test, bug #2494150")]
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
            await Task.Delay(GetSleepTime());
            Assert.True(iotHub.HasReceivedTwinChangeNotification());
            Assert.True(module1.HasReceivedTwinChangeNotification());
        }

        // Need longer sleep when run tests in parallel
        static TimeSpan GetSleepTime(int baseSleepSecs = 15) => TimeSpan.FromSeconds(baseSleepSecs + Rand.Next(0, 10));

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
            cloudProxy.Setup(c => c.SendMessageAsync(It.IsAny<IMessage>())).Callback<IMessage>(m => iotHub.ReceivedMessages.Add(m)).Returns(Task.CompletedTask);
            cloudProxy.Setup(c => c.UpdateReportedPropertiesAsync(It.IsAny<IMessage>())).Callback<IMessage>(m => iotHub.ReceivedMessages.Add(m)).Returns(Task.CompletedTask);
            cloudProxy.SetupGet(c => c.IsActive).Returns(true);
            var cloudConnection = Mock.Of<ICloudConnection>(c => c.IsActive && c.CloudProxy == Option.Some(cloudProxy.Object));

            var cloudConnectionProvider = new Mock<ICloudConnectionProvider>();
            cloudConnectionProvider.Setup(c => c.Connect(It.IsAny<IClientCredentials>(), It.IsAny<Action<string, CloudConnectionStatus>>())).ReturnsAsync(Try.Success(cloudConnection));

            IConnectionManager connectionManager = new ConnectionManager(cloudConnectionProvider.Object, Mock.Of<ICredentialsCache>(), new IdentityProvider(iotHubName));
            var routingMessageConverter = new RoutingMessageConverter();
            RouteFactory routeFactory = new EdgeRouteFactory(new EndpointFactory(connectionManager, routingMessageConverter, edgeDeviceId));
            IEnumerable<Route> routesList = routeFactory.Create(routes).ToList();
            IEnumerable<Endpoint> endpoints = routesList.SelectMany(r => r.Endpoints);
            var routerConfig = new RouterConfig(endpoints, routesList);
            IDbStoreProvider dbStoreProvider = new InMemoryDbStoreProvider();
            IStoreProvider storeProvider = new StoreProvider(dbStoreProvider);
            IMessageStore messageStore = new MessageStore(storeProvider, CheckpointStore.Create(storeProvider), TimeSpan.MaxValue);
            IEndpointExecutorFactory endpointExecutorFactory = new StoringAsyncEndpointExecutorFactory(endpointExecutorConfig, new AsyncEndpointExecutorOptions(1, TimeSpan.FromMilliseconds(10)), messageStore);
            Router router = await Router.CreateAsync(Guid.NewGuid().ToString(), iotHubName, routerConfig, endpointExecutorFactory);
            ITwinManager twinManager = new TwinManager(connectionManager, new TwinCollectionMessageConverter(), new TwinMessageConverter(), Option.None<IEntityStore<string, TwinInfo>>());
            IEdgeHub edgeHub = new RoutingEdgeHub(router, routingMessageConverter, connectionManager, twinManager, edgeDeviceId, Mock.Of<IInvokeMethodHandler>(), Mock.Of<IDeviceConnectivityManager>());
            return (edgeHub, connectionManager);
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

        static List<IMessage> GetMessages()
        {
            var messages = new List<IMessage>();
            for (int i = 0; i < 10; i++)
            {
                messages.Add(GetMessage());
            }

            return messages;
        }

        static IMessage GetReportedPropertiesMessage()
        {
            var twinCollection = new TwinCollection();
            twinCollection["Status"] = "running";
            twinCollection["ElapsedTime"] = "0.5";
            byte[] messageBody = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(twinCollection));
            return new EdgeMessage.Builder(messageBody).Build();
        }

        static IClientCredentials SetupDeviceIdentity(string deviceId) =>
            new TokenCredentials(new DeviceIdentity("iotHub", deviceId), Guid.NewGuid().ToString(), string.Empty, false);

        static IClientCredentials SetupModuleCredentials(string moduleId, string deviceId) =>
            new TokenCredentials(new ModuleIdentity("iotHub", deviceId, moduleId), Guid.NewGuid().ToString(), string.Empty, false);

        class IoTHub
        {
            public List<IMessage> ReceivedMessages { get; } = new List<IMessage>();

            public bool HasReceivedMessages(IEnumerable<IMessage> messages) => messages.All(m => this.HasReceivedMessage(m));

            public bool HasReceivedMessage(IMessage message) => this.ReceivedMessages.Any(
                m =>
                    m.SystemProperties[SystemProperties.MessageId] == message.SystemProperties[SystemProperties.MessageId]);

            public bool HasReceivedTwinChangeNotification() => this.ReceivedMessages.Any(
                m =>
                    m.SystemProperties[SystemProperties.MessageType] == Constants.TwinChangeNotificationMessageType);
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
                IClientCredentials deviceCredentials = SetupDeviceIdentity(deviceId);
                Try<ICloudProxy> cloudProxy = await connectionManager.CreateCloudConnectionAsync(deviceCredentials);
                Assert.True(cloudProxy.Success);
                var deviceProxy = Mock.Of<IDeviceProxy>();
                var deviceListener = new DeviceMessageHandler(deviceCredentials.Identity, edgeHub, connectionManager);
                deviceListener.BindDeviceProxy(deviceProxy);
                return new TestDevice(deviceCredentials.Identity as IDeviceIdentity, deviceListener);
            }

            public Task SendMessages(IEnumerable<IMessage> messages) => Task.WhenAll(messages.Select(m => this.SendMessage(m)));

            public Task SendMessage(IMessage message)
            {
                message.SystemProperties[SystemProperties.ConnectionDeviceId] = this.deviceIdentity.DeviceId;
                return this.deviceListener.ProcessDeviceMessageAsync(message);
            }

            public Task UpdateReportedProperties(IMessage reportedPropertiesMessage) =>
                this.deviceListener.UpdateReportedPropertiesAsync(reportedPropertiesMessage, Guid.NewGuid().ToString());
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

            public static Task<TestModule> Create(string deviceId, string moduleId, string outputEndpointId, string inputEndpointId, IEdgeHub edgeHub, IConnectionManager connectionManager) =>
                Create(deviceId, moduleId, outputEndpointId, new List<string> { inputEndpointId }, edgeHub, connectionManager);

            public static async Task<TestModule> Create(string deviceId, string moduleId, string outputEndpointId, List<string> inputEndpointIds, IEdgeHub edgeHub, IConnectionManager connectionManager)
            {
                IClientCredentials moduleCredentials = SetupModuleCredentials(moduleId, deviceId);
                Try<ICloudProxy> cloudProxy = await connectionManager.CreateCloudConnectionAsync(moduleCredentials);
                Assert.True(cloudProxy.Success);
                var deviceListener = new DeviceMessageHandler(moduleCredentials.Identity, edgeHub, connectionManager);
                var receivedMessages = new List<IMessage>();
                var deviceProxy = new Mock<IDeviceProxy>();
                deviceProxy.Setup(d => d.SendMessageAsync(It.IsAny<IMessage>(), It.Is<string>(e => inputEndpointIds.Contains(e))))
                    .Callback<IMessage, string>(
                        (m, e) =>
                        {
                            receivedMessages.Add(m);
                            deviceListener.ProcessMessageFeedbackAsync(m.SystemProperties[SystemProperties.LockToken], FeedbackStatus.Complete).Wait();
                        })
                    .Returns(Task.CompletedTask);
                deviceProxy.SetupGet(d => d.IsActive).Returns(true);
                deviceListener.BindDeviceProxy(deviceProxy.Object);
                await deviceListener.AddSubscription(DeviceSubscription.ModuleMessages);
                return new TestModule(moduleCredentials.Identity as IModuleIdentity, outputEndpointId, deviceListener, receivedMessages);
            }

            public Task SendMessageOnOutput(IEnumerable<IMessage> messages) => Task.WhenAll(messages.Select(m => this.SendMessageOnOutput(m)));

            public Task SendMessageOnOutput(IMessage message) => this.SendMessageOnOutput(message, this.outputName);

            public Task SendMessageOnOutput(IEnumerable<IMessage> messages, string outputNameArg) => Task.WhenAll(messages.Select(m => this.SendMessageOnOutput(m, outputNameArg)));

            public Task SendMessageOnOutput(IMessage message, string outputNameArg)
            {
                message.SystemProperties[SystemProperties.ConnectionDeviceId] = this.moduleIdentity.DeviceId;
                message.SystemProperties[SystemProperties.ConnectionModuleId] = this.moduleIdentity.ModuleId;
                message.SystemProperties[SystemProperties.OutputName] = outputNameArg;
                return this.deviceListener.ProcessDeviceMessageAsync(message);
            }

            public Task SendMessages(IEnumerable<IMessage> messages) => Task.WhenAll(messages.Select(m => this.SendMessage(m)));

            public Task SendMessage(IMessage message)
            {
                message.SystemProperties[SystemProperties.ConnectionDeviceId] = this.moduleIdentity.DeviceId;
                message.SystemProperties[SystemProperties.ConnectionModuleId] = this.moduleIdentity.ModuleId;
                return this.deviceListener.ProcessDeviceMessageAsync(message);
            }

            public bool HasReceivedMessages(IEnumerable<IMessage> messages) => messages.All(m => this.HasReceivedMessage(m));

            public bool HasReceivedMessage(IMessage message) => this.receivedMessages.Any(
                m =>
                    m.SystemProperties[SystemProperties.MessageId] == message.SystemProperties[SystemProperties.MessageId]);

            public Task UpdateReportedProperties(IMessage reportedPropertiesMessage) =>
                this.deviceListener.UpdateReportedPropertiesAsync(reportedPropertiesMessage, Guid.NewGuid().ToString());

            public bool HasReceivedTwinChangeNotification() => this.receivedMessages.Any(
                m =>
                    m.SystemProperties[SystemProperties.MessageType] == Constants.TwinChangeNotificationMessageType);
        }
    }
}
