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
    using IMessage = Microsoft.Azure.Devices.Edge.Hub.Core.IMessage;
    using Message = Microsoft.Azure.Devices.Edge.Hub.Core.EdgeMessage;
    using SystemProperties = Microsoft.Azure.Devices.Edge.Hub.Core.SystemProperties;

    [Integration]
    public class RoutingTest
    {
        static readonly Random Rand = new Random();

        static TimeSpan GetSleepTime(int baseSleepSecs = 10) => TimeSpan.FromSeconds(baseSleepSecs + Rand.Next(0, 10));

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

            IMessage message = GetMessage();
            await device1.SendMessage(message);

            await Task.Delay(GetSleepTime());

            Assert.True(iotHub.HasReceivedMessage(message));
            Assert.False(module1.HasReceivedMessage(message));
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

            IMessage message = GetMessage();
            await device1.SendMessage(message);

            await Task.Delay(GetSleepTime());

            Assert.False(iotHub.HasReceivedMessage(message));
            Assert.True(module1.HasReceivedMessage(message));
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

            IMessage deviceMessage = GetMessage();
            await device1.SendMessage(deviceMessage);

            await Task.Delay(GetSleepTime());

            Assert.False(iotHub.HasReceivedMessage(deviceMessage));
            Assert.True(moduleMl.HasReceivedMessage(deviceMessage));
            Assert.False(moduleAsa.HasReceivedMessage(deviceMessage));

            IMessage mlMessage = GetMessage();
            await moduleMl.SendMessageOnOutput(mlMessage);

            await Task.Delay(GetSleepTime());

            Assert.False(iotHub.HasReceivedMessage(mlMessage));
            Assert.False(moduleMl.HasReceivedMessage(mlMessage));
            Assert.True(moduleAsa.HasReceivedMessage(mlMessage));

            IMessage asaMessage = GetMessage();
            await moduleAsa.SendMessageOnOutput(asaMessage);

            await Task.Delay(GetSleepTime());

            Assert.True(iotHub.HasReceivedMessage(asaMessage));
            Assert.False(moduleMl.HasReceivedMessage(asaMessage));
            Assert.False(moduleAsa.HasReceivedMessage(asaMessage));
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

            IMessage deviceMessage = GetMessage();
            await device1.SendMessage(deviceMessage);
            await Task.Delay(GetSleepTime());
            Assert.False(iotHub.HasReceivedMessage(deviceMessage));
            Assert.True(moduleMl.HasReceivedMessage(deviceMessage));
            Assert.False(moduleAsa.HasReceivedMessage(deviceMessage));

            IMessage mlMessage = GetMessage();
            await moduleMl.SendMessageOnOutput(mlMessage);
            await Task.Delay(GetSleepTime());
            Assert.False(iotHub.HasReceivedMessage(mlMessage));
            Assert.True(moduleMl.HasReceivedMessage(mlMessage));
            Assert.False(moduleAsa.HasReceivedMessage(mlMessage));

            IMessage mlMessage2 = GetMessage();
            await moduleMl.SendMessageOnOutput(mlMessage2, "op2");
            await Task.Delay(GetSleepTime());
            Assert.False(iotHub.HasReceivedMessage(mlMessage2));
            Assert.False(moduleMl.HasReceivedMessage(mlMessage2));
            Assert.True(moduleAsa.HasReceivedMessage(mlMessage2));
        }

        [Fact(Skip = "Flaky test, bug #2509253")]
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

            IMessage deviceMessage = GetMessage();
            await device1.SendMessage(deviceMessage);
            await Task.Delay(GetSleepTime(20));
            Assert.False(iotHub.HasReceivedMessage(deviceMessage));
            Assert.True(moduleMl.HasReceivedMessage(deviceMessage));
            Assert.False(moduleAsa.HasReceivedMessage(deviceMessage));

            IMessage mlMessage = GetMessage();
            await moduleMl.SendMessage(mlMessage);
            await Task.Delay(GetSleepTime());
            Assert.False(iotHub.HasReceivedMessage(mlMessage));
            Assert.False(moduleMl.HasReceivedMessage(mlMessage));
            Assert.True(moduleAsa.HasReceivedMessage(mlMessage));

            IMessage asaMessage = GetMessage();
            await moduleAsa.SendMessage(asaMessage);
            await Task.Delay(GetSleepTime());
            Assert.True(iotHub.HasReceivedMessage(asaMessage));
            Assert.False(moduleMl.HasReceivedMessage(asaMessage));
            Assert.False(moduleAsa.HasReceivedMessage(asaMessage));
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

            IMessage deviceMessage = GetMessage();
            await device1.SendMessage(deviceMessage);
            await Task.Delay(GetSleepTime());
            Assert.False(iotHub.HasReceivedMessage(deviceMessage));
            Assert.True(moduleMl.HasReceivedMessage(deviceMessage));
            Assert.False(moduleAsa.HasReceivedMessage(deviceMessage));

            IMessage mlMessage = GetMessage();
            await moduleMl.SendMessage(mlMessage);
            await Task.Delay(GetSleepTime());
            Assert.False(iotHub.HasReceivedMessage(mlMessage));
            Assert.False(moduleMl.HasReceivedMessage(mlMessage));
            Assert.True(moduleAsa.HasReceivedMessage(mlMessage));

            IMessage asaMessage = GetMessage();
            await moduleAsa.SendMessage(asaMessage);
            await Task.Delay(GetSleepTime());
            Assert.False(iotHub.HasReceivedMessage(asaMessage));
            Assert.False(moduleMl.HasReceivedMessage(asaMessage));
            Assert.False(moduleAsa.HasReceivedMessage(asaMessage));
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

            IMessage deviceMessage = GetMessage();
            deviceMessage.Properties.Add("temp", "100");
            await device1.SendMessage(deviceMessage);
            await Task.Delay(GetSleepTime());
            Assert.False(iotHub.HasReceivedMessage(deviceMessage));
            Assert.True(moduleMl.HasReceivedMessage(deviceMessage));
            Assert.False(moduleAsa.HasReceivedMessage(deviceMessage));

            IMessage mlMessage = GetMessage();
            mlMessage.Properties.Add("messageType", "alert");
            await moduleMl.SendMessageOnOutput(mlMessage);
            await Task.Delay(GetSleepTime());
            Assert.False(iotHub.HasReceivedMessage(mlMessage));
            Assert.False(moduleMl.HasReceivedMessage(mlMessage));
            Assert.True(moduleAsa.HasReceivedMessage(mlMessage));

            IMessage asaMessage = GetMessage();
            asaMessage.Properties.Add("info", "aggregate");
            await moduleAsa.SendMessageOnOutput(asaMessage);
            await Task.Delay(GetSleepTime());
            Assert.True(iotHub.HasReceivedMessage(asaMessage));
            Assert.False(moduleMl.HasReceivedMessage(asaMessage));
            Assert.False(moduleAsa.HasReceivedMessage(asaMessage));
        }

        // TODO: Re-enable!
        [Fact(Skip = "Failing intermittently")]
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

            IMessage message1 = GetMessage();
            message1.Properties.Add("temp", "100");
            await device1.SendMessage(message1);
            await Task.Delay(GetSleepTime());
            Assert.False(iotHub.HasReceivedMessage(message1));
            Assert.True(module1.HasReceivedMessage(message1));
            Assert.False(module2.HasReceivedMessage(message1));

            IMessage message2 = GetMessage();
            message2.Properties.Add("temp", "20");
            await device1.SendMessage(message2);
            await Task.Delay(GetSleepTime());
            Assert.False(iotHub.HasReceivedMessage(message2));
            Assert.False(module1.HasReceivedMessage(message2));
            Assert.True(module2.HasReceivedMessage(message2));
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

            IMessage message1 = GetMessage();
            message1.SystemProperties[SystemProperties.ContentType] = "application/json";
            message1.SystemProperties[SystemProperties.ContentEncoding] = "utf-8";
            await device1.SendMessage(message1);
            await Task.Delay(GetSleepTime());
            Assert.True(iotHub.HasReceivedMessage(message1));
            Assert.False(module1.HasReceivedMessage(message1));
            Assert.False(module2.HasReceivedMessage(message1));

            IMessage message2 = GetMessage();
            message2.SystemProperties[SystemProperties.ContentType] = "application/json";
            message2.SystemProperties[SystemProperties.ContentEncoding] = "utf-16";
            await device1.SendMessage(message2);
            await Task.Delay(GetSleepTime());
            Assert.False(iotHub.HasReceivedMessage(message2));
            Assert.False(module1.HasReceivedMessage(message2));
            Assert.True(module2.HasReceivedMessage(message2));
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
            IConnectionManager connectionManager = new ConnectionManager(cloudConnectionProvider.Object);
            var routingMessageConverter = new RoutingMessageConverter();
            RouteFactory routeFactory = new EdgeRouteFactory(new EndpointFactory(connectionManager, routingMessageConverter, edgeDeviceId));
            IEnumerable<Route> routesList = routeFactory.Create(routes).ToList();
            IEnumerable<Endpoint> endpoints = routesList.SelectMany(r => r.Endpoints);
            var routerConfig = new RouterConfig(endpoints, routesList);
            IDbStoreProvider dbStoreProvider = new InMemoryDbStoreProvider();
            IStoreProvider storeProvider = new StoreProvider(dbStoreProvider);
            IMessageStore messageStore = new MessageStore(storeProvider, CheckpointStore.Create(dbStoreProvider), TimeSpan.MaxValue);
            IEndpointExecutorFactory endpointExecutorFactory = new StoringAsyncEndpointExecutorFactory(endpointExecutorConfig, new AsyncEndpointExecutorOptions(1), messageStore);
            Router router = await Router.CreateAsync(Guid.NewGuid().ToString(), iotHubName, routerConfig, endpointExecutorFactory);
            ITwinManager twinManager = new TwinManager(connectionManager, new TwinCollectionMessageConverter(), new TwinMessageConverter(), Option.None<IEntityStore<string, TwinInfo>>());
            IEdgeHub edgeHub = new RoutingEdgeHub(router, routingMessageConverter, connectionManager, twinManager, edgeDeviceId);
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

            public static Task<TestModule> Create(string deviceId, string moduleId, string outputEndpointId, string inputEndpointId, IEdgeHub edgeHub, IConnectionManager connectionManager) =>
                Create(deviceId, moduleId, outputEndpointId, new List<string> { inputEndpointId }, edgeHub, connectionManager);

            public static async Task<TestModule> Create(string deviceId, string moduleId, string outputEndpointId, List<string> inputEndpointIds, IEdgeHub edgeHub, IConnectionManager connectionManager)
            {
                IClientCredentials moduleCredentials = SetupModuleCredentials(moduleId, deviceId);
                Try<ICloudProxy> cloudProxy = await connectionManager.GetOrCreateCloudConnectionAsync(moduleCredentials);
                Assert.True(cloudProxy.Success);
                var deviceListener = new DeviceMessageHandler(moduleCredentials.Identity, edgeHub, connectionManager);
                var receivedMessages = new List<IMessage>();
                var deviceProxy = new Mock<IDeviceProxy>();
                deviceProxy.Setup(d => d.SendMessageAsync(It.IsAny<IMessage>(), It.Is<string>(e => inputEndpointIds.Contains(e))))
                    .Callback<IMessage, string>((m, e) =>
                    {
                        receivedMessages.Add(m);
                        deviceListener.ProcessMessageFeedbackAsync(m.SystemProperties[SystemProperties.LockToken], FeedbackStatus.Complete).Wait();
                    })
                    .Returns(Task.CompletedTask);
                deviceProxy.SetupGet(d => d.IsActive).Returns(true);
                deviceListener.BindDeviceProxy(deviceProxy.Object);
                return new TestModule(moduleCredentials.Identity as IModuleIdentity, outputEndpointId, deviceListener, receivedMessages);
            }

            public Task SendMessageOnOutput(IMessage message) => this.SendMessageOnOutput(message, this.outputName);

            public Task SendMessageOnOutput(IMessage message, string outputNameArg)
            {
                message.SystemProperties[SystemProperties.ConnectionDeviceId] = this.moduleIdentity.DeviceId;
                message.SystemProperties[SystemProperties.ConnectionModuleId] = this.moduleIdentity.ModuleId;
                message.SystemProperties[SystemProperties.OutputName] = outputNameArg;
                return this.deviceListener.ProcessDeviceMessageAsync(message);
            }

            public Task SendMessage(IMessage message)
            {
                message.SystemProperties[SystemProperties.ConnectionDeviceId] = this.moduleIdentity.DeviceId;
                message.SystemProperties[SystemProperties.ConnectionModuleId] = this.moduleIdentity.ModuleId;
                return this.deviceListener.ProcessDeviceMessageAsync(message);
            }

            public bool HasReceivedMessage(IMessage message) => this.receivedMessages.Any(m =>
                m.SystemProperties[SystemProperties.MessageId] == message.SystemProperties[SystemProperties.MessageId]);

            public Task UpdateReportedProperties(IMessage reportedPropertiesMessage) =>
                this.deviceListener.UpdateReportedPropertiesAsync(reportedPropertiesMessage, Guid.NewGuid().ToString());

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
                IClientCredentials deviceCredentials = SetupDeviceIdentity(deviceId);
                Try<ICloudProxy> cloudProxy = await connectionManager.GetOrCreateCloudConnectionAsync(deviceCredentials);
                Assert.True(cloudProxy.Success);
                var deviceProxy = Mock.Of<IDeviceProxy>();
                var deviceListener = new DeviceMessageHandler(deviceCredentials.Identity, edgeHub, connectionManager);
                deviceListener.BindDeviceProxy(deviceProxy);
                return new TestDevice(deviceCredentials.Identity as IDeviceIdentity, deviceListener);
            }

            public Task SendMessage(IMessage message)
            {
                message.SystemProperties[SystemProperties.ConnectionDeviceId] = this.deviceIdentity.DeviceId;
                return this.deviceListener.ProcessDeviceMessageAsync(message);
            }

            public Task UpdateReportedProperties(IMessage reportedPropertiesMessage) =>
                this.deviceListener.UpdateReportedPropertiesAsync(reportedPropertiesMessage, Guid.NewGuid().ToString());
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
            return new Message.Builder(messageBody).Build();
        }

        static IClientCredentials SetupDeviceIdentity(string deviceId) =>
            new TokenCredentials(new DeviceIdentity("iotHub", deviceId), Guid.NewGuid().ToString(), string.Empty);

        static IClientCredentials SetupModuleCredentials(string moduleId, string deviceId) =>
            new TokenCredentials(new ModuleIdentity("iotHub", deviceId, moduleId), Guid.NewGuid().ToString(), string.Empty);
    }
}
