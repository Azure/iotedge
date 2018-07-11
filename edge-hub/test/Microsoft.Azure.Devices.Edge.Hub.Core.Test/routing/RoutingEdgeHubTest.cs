// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Routing
{
    using App.Metrics;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Routing;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Moq;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Xunit;
    using IMessage = Microsoft.Azure.Devices.Routing.Core.IMessage;
    using Message = Microsoft.Azure.Devices.Edge.Hub.Core.EdgeMessage;

    [Unit]
    public class RoutingEdgeHubTest
    {
        [Fact]
        public async Task ProcessDeviceMessageBatch_ConvertsMessages()
        {
            // Create a mock endpoint capable of returning a mock processor
            var processor = Mock.Of<IProcessor>();
            var endpoint = new Mock<Endpoint>("myId");
            endpoint.Setup(ep => ep.CreateProcessor()).Returns(processor);
            endpoint.SetupGet(ep => ep.Id).Returns("myId");

            // Create a mock endpoint executor factory to create the endpoint executor to verify invocation
            var endpointExecutor = Mock.Of<IEndpointExecutor>();
            Mock.Get(endpointExecutor).SetupGet(ee => ee.Endpoint).Returns(() => endpoint.Object);
            var endpointExecutorFactory = Mock.Of<IEndpointExecutorFactory>();
            Mock.Get(endpointExecutorFactory).Setup(eef => eef.CreateAsync(It.IsAny<Endpoint>())).ReturnsAsync(endpointExecutor);

            // Create a route to map to the message
            var endpoints = new HashSet<Endpoint> { endpoint.Object };
            var route = new Route("myRoute", "true", "myIotHub", TelemetryMessageSource.Instance, endpoints);

            // Create a router
            var routerConfig = new RouterConfig(new[] { route });
            Router router = await Router.CreateAsync("myRouter", "myIotHub", routerConfig, endpointExecutorFactory);

            // Create mock message converter to generate a message with source matching the route
            var message = Mock.Of<IMessage>();
            Mock.Get(message).SetupGet(m => m.MessageSource).Returns(() => TelemetryMessageSource.Instance);
            var messageConverter = Mock.Of<Core.IMessageConverter<IMessage>>();
            Mock.Get(messageConverter).Setup(mc => mc.FromMessage(It.IsAny<Core.IMessage>())).Returns(message);

            // Create mock for IConnectionManager
            var connectionManager = Mock.Of<IConnectionManager>();

            // Mock of twin manager
            var twinManager = Mock.Of<ITwinManager>();

            // Test Scenario
            var routingEdgeHub = new RoutingEdgeHub(router, messageConverter, connectionManager, twinManager, "testEdgeDevice", Option.None<IMetricsRoot>());
            var identity = Mock.Of<IIdentity>();
            EdgeMessage[] messages = { new Message.Builder(new byte[0]).Build() };
            await routingEdgeHub.ProcessDeviceMessageBatch(identity, messages);

            // Verify Expectation
            Mock.Get(endpointExecutor).Verify(e => e.Invoke(It.IsAny<IMessage>()), Times.Once);
        }

        [Fact]
        public async Task EdgeHubChecksMessageSize()
        {
            // Create a mock endpoint capable of returning a mock processor
            var processor = Mock.Of<IProcessor>();
            var endpoint = new Mock<Endpoint>("myId");
            endpoint.Setup(ep => ep.CreateProcessor()).Returns(processor);
            endpoint.SetupGet(ep => ep.Id).Returns("myId");

            // Create a mock endpoint executor factory to create the endpoint executor to verify invocation
            var endpointExecutor = Mock.Of<IEndpointExecutor>();
            Mock.Get(endpointExecutor).SetupGet(ee => ee.Endpoint).Returns(() => endpoint.Object);
            var endpointExecutorFactory = Mock.Of<IEndpointExecutorFactory>();
            Mock.Get(endpointExecutorFactory).Setup(eef => eef.CreateAsync(It.IsAny<Endpoint>())).ReturnsAsync(endpointExecutor);

            // Create a route to map to the message
            var endpoints = new HashSet<Endpoint> { endpoint.Object };
            var route = new Route("myRoute", "true", "myIotHub", TelemetryMessageSource.Instance, endpoints);

            // Create a router
            var routerConfig = new RouterConfig(new[] { route });
            Router router = await Router.CreateAsync("myRouter", "myIotHub", routerConfig, endpointExecutorFactory);

            // Create mock for IConnectionManager
            var connectionManager = Mock.Of<IConnectionManager>();

            // Mock of twin manager
            var twinManager = Mock.Of<ITwinManager>();

            // Mock of identity
            var identity = Mock.Of<IIdentity>();

            var messageConverter = new RoutingMessageConverter();

            Message badMessage = new Message.Builder(new byte[300 * 1024]).Build();

            var routingEdgeHub = new RoutingEdgeHub(router, messageConverter, connectionManager, twinManager, "testEdgeDevice", Option.None<IMetricsRoot>());

            await Assert.ThrowsAsync<InvalidOperationException>(() => routingEdgeHub.ProcessDeviceMessage(identity, badMessage));

            string badString = System.Text.Encoding.UTF8.GetString(new byte[300 * 1024], 0, 300 * 1024);
            var badProperties = new Dictionary<string, string> { ["toolong"] = badString };

            badMessage = new Message.Builder(new byte[1]).SetProperties(badProperties).Build();

            await Assert.ThrowsAsync<InvalidOperationException>(() => routingEdgeHub.ProcessDeviceMessage(identity, badMessage));

            badMessage = new Message(new byte[1], new Dictionary<string, string>(), badProperties);

            await Assert.ThrowsAsync<InvalidOperationException>(() => routingEdgeHub.ProcessDeviceMessage(identity, badMessage));
        }

        [Fact]
        public async Task GetTwinForwardsToTwinManager()
        {
            // Create a mock endpoint capable of returning a mock processor
            var processor = Mock.Of<IProcessor>();
            var endpoint = new Mock<Endpoint>("myId");
            endpoint.Setup(ep => ep.CreateProcessor()).Returns(processor);
            endpoint.SetupGet(ep => ep.Id).Returns("myId");

            // Create a mock endpoint executor factory to create the endpoint executor to verify invocation
            var endpointExecutor = Mock.Of<IEndpointExecutor>();
            Mock.Get(endpointExecutor).SetupGet(ee => ee.Endpoint).Returns(() => endpoint.Object);
            var endpointExecutorFactory = Mock.Of<IEndpointExecutorFactory>();
            Mock.Get(endpointExecutorFactory).Setup(eef => eef.CreateAsync(It.IsAny<Endpoint>())).ReturnsAsync(endpointExecutor);

            // Create a route to map to the message
            var endpoints = new HashSet<Endpoint> { endpoint.Object };
            var route = new Route("myRoute", "true", "myIotHub", TelemetryMessageSource.Instance, endpoints);

            // Create a router
            var routerConfig = new RouterConfig(new[] { route });
            Router router = await Router.CreateAsync("myRouter", "myIotHub", routerConfig, endpointExecutorFactory);

            var messageConverter = Mock.Of<Core.IMessageConverter<IMessage>>();
            var connectionManager = Mock.Of<IConnectionManager>();
            var twinManager = new Mock<ITwinManager>();
            var message = Mock.Of<Core.IMessage>();
            twinManager.Setup(t => t.GetTwinAsync(It.IsAny<string>())).Returns(Task.FromResult(message));
            var routingEdgeHub = new RoutingEdgeHub(router, messageConverter, connectionManager, twinManager.Object, "testEdgeDevice", Option.None<IMetricsRoot>());

            Core.IMessage received = await routingEdgeHub.GetTwinAsync("*");
            twinManager.Verify(x => x.GetTwinAsync("*"), Times.Once);

            Assert.Equal(message, received);
        }


        [Fact]
        public async Task UpdateDesiredPropertiesForwardsToTwinManager()
        {
            // Create a mock endpoint capable of returning a mock processor
            var processor = Mock.Of<IProcessor>();
            var endpoint = new Mock<Endpoint>("myId");
            endpoint.Setup(ep => ep.CreateProcessor()).Returns(processor);
            endpoint.SetupGet(ep => ep.Id).Returns("myId");

            // Create a mock endpoint executor factory to create the endpoint executor to verify invocation
            var endpointExecutor = Mock.Of<IEndpointExecutor>();
            Mock.Get(endpointExecutor).SetupGet(ee => ee.Endpoint).Returns(() => endpoint.Object);
            var endpointExecutorFactory = Mock.Of<IEndpointExecutorFactory>();
            Mock.Get(endpointExecutorFactory).Setup(eef => eef.CreateAsync(It.IsAny<Endpoint>())).ReturnsAsync(endpointExecutor);

            // Create a route to map to the message
            var endpoints = new HashSet<Endpoint> { endpoint.Object };
            var route = new Route("myRoute", "true", "myIotHub", TelemetryMessageSource.Instance, endpoints);

            // Create a router
            var routerConfig = new RouterConfig(new[] { route });
            Router router = await Router.CreateAsync("myRouter", "myIotHub", routerConfig, endpointExecutorFactory);

            var messageConverter = Mock.Of<Core.IMessageConverter<IMessage>>();
            var connectionManager = Mock.Of<IConnectionManager>();
            var twinManager = new Mock<ITwinManager>();
            var message = Mock.Of<Core.IMessage>();
            Core.IMessage received = new Message.Builder(new byte[0]).Build();
            twinManager.Setup(t => t.UpdateDesiredPropertiesAsync(It.IsAny<string>(), It.IsAny<Core.IMessage>())).Callback<string, Core.IMessage>((s, m) => received = message).Returns(Task.CompletedTask);
            var routingEdgeHub = new RoutingEdgeHub(router, messageConverter, connectionManager, twinManager.Object, "testEdgeDevice", Option.None<IMetricsRoot>());

            await routingEdgeHub.UpdateDesiredPropertiesAsync("*", message);
            twinManager.Verify(x => x.UpdateDesiredPropertiesAsync("*", message), Times.Once);

            Assert.Equal(message, received);
        }

        [Fact]
        public async Task InvokeMethodAsyncTest()
        {
            // Create a mock endpoint capable of returning a mock processor
            var processor = Mock.Of<IProcessor>();
            var endpoint = new Mock<Endpoint>("myId");
            endpoint.Setup(ep => ep.CreateProcessor()).Returns(processor);
            endpoint.SetupGet(ep => ep.Id).Returns("myId");

            // Create a mock endpoint executor factory to create the endpoint executor to verify invocation
            var endpointExecutor = Mock.Of<IEndpointExecutor>();
            Mock.Get(endpointExecutor).SetupGet(ee => ee.Endpoint).Returns(() => endpoint.Object);
            var endpointExecutorFactory = Mock.Of<IEndpointExecutorFactory>();
            Mock.Get(endpointExecutorFactory).Setup(eef => eef.CreateAsync(It.IsAny<Endpoint>())).ReturnsAsync(endpointExecutor);

            // Create a route to map to the message
            var endpoints = new HashSet<Endpoint> { endpoint.Object };
            var route = new Route("myRoute", "true", "myIotHub", TelemetryMessageSource.Instance, endpoints);

            // Create a router
            var routerConfig = new RouterConfig(new[] { route });
            Router router = await Router.CreateAsync("myRouter", "myIotHub", routerConfig, endpointExecutorFactory);

            // Create mock message converter to generate a message with source matching the route
            var messageConverter = Mock.Of<Core.IMessageConverter<IMessage>>();            

            // Mock of twin manager
            var twinManager = Mock.Of<ITwinManager>();
            
            // DeviceListener
            var identity = Mock.Of<IModuleIdentity>(m => m.DeviceId == "device1" && m.ModuleId == "module1" && m.Id == "device1/module1");
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(c => c.BindCloudListener(It.IsAny<ICloudListener>()));
            var underlyingDeviceProxy = new Mock<IDeviceProxy>();
            underlyingDeviceProxy.Setup(d => d.InvokeMethodAsync(It.IsAny<DirectMethodRequest>())).ReturnsAsync(default(DirectMethodResponse));
            underlyingDeviceProxy.SetupGet(d => d.IsActive).Returns(true);

            // ICloudConnectionProvider
            var cloudConnection = Mock.Of<ICloudConnection>(c => c.IsActive && c.CloudProxy == Option.Some(cloudProxy.Object));
            var cloudConnectionProvider = new Mock<ICloudConnectionProvider>();
            cloudConnectionProvider.Setup(c => c.Connect(It.IsAny<IClientCredentials>(), It.IsAny<Action<string, CloudConnectionStatus>>()))
                .ReturnsAsync(Try.Success(cloudConnection));
            var connectionManager = new ConnectionManager(cloudConnectionProvider.Object);

            // RoutingEdgeHub
            var routingEdgeHub = new RoutingEdgeHub(router, messageConverter, connectionManager, twinManager, "testEdgeDevice", Option.None<IMetricsRoot>());

            var deviceMessageHandler = new DeviceMessageHandler(identity, routingEdgeHub, connectionManager);            
            var methodRequest = new DirectMethodRequest("device1/module1", "shutdown", null, TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(10));

            // Act
            deviceMessageHandler.BindDeviceProxy(underlyingDeviceProxy.Object);
            await deviceMessageHandler.AddSubscription(DeviceSubscription.Methods);
            Task<DirectMethodResponse> responseTask = routingEdgeHub.InvokeMethodAsync(identity.Id, methodRequest);                        

            // Assert
            Assert.False(responseTask.IsCompleted);

            // Arrange
            Message message = new Message.Builder(new byte[0]).Build();
            message.Properties[Core.SystemProperties.CorrelationId] = methodRequest.CorrelationId;
            message.Properties[Core.SystemProperties.StatusCode] = "200";

            // Act
            await deviceMessageHandler.ProcessMethodResponseAsync(message);

            // Assert
            Assert.True(responseTask.IsCompleted);
            Assert.Equal(methodRequest.CorrelationId, responseTask.Result.CorrelationId);
            Assert.Equal(200, responseTask.Result.Status);
        }

        [Fact]
        public async Task InvokeMethodNoSubscriptionTest()
        {
            // Create a mock endpoint capable of returning a mock processor
            var processor = Mock.Of<IProcessor>();
            var endpoint = new Mock<Endpoint>("myId");
            endpoint.Setup(ep => ep.CreateProcessor()).Returns(processor);
            endpoint.SetupGet(ep => ep.Id).Returns("myId");

            // Create a mock endpoint executor factory to create the endpoint executor to verify invocation
            var endpointExecutor = Mock.Of<IEndpointExecutor>();
            Mock.Get(endpointExecutor).SetupGet(ee => ee.Endpoint).Returns(() => endpoint.Object);
            var endpointExecutorFactory = Mock.Of<IEndpointExecutorFactory>();
            Mock.Get(endpointExecutorFactory).Setup(eef => eef.CreateAsync(It.IsAny<Endpoint>())).ReturnsAsync(endpointExecutor);

            // Create a route to map to the message
            var endpoints = new HashSet<Endpoint> { endpoint.Object };
            var route = new Route("myRoute", "true", "myIotHub", TelemetryMessageSource.Instance, endpoints);

            // Create a router
            var routerConfig = new RouterConfig(new[] { route });
            Router router = await Router.CreateAsync("myRouter", "myIotHub", routerConfig, endpointExecutorFactory);

            // Create mock message converter to generate a message with source matching the route
            var messageConverter = Mock.Of<Core.IMessageConverter<IMessage>>();

            // Mock of twin manager
            var twinManager = Mock.Of<ITwinManager>();

            // DeviceListener
            var identity = Mock.Of<IModuleIdentity>(m => m.DeviceId == "device1" && m.ModuleId == "module1" && m.Id == "device1/module1");
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(c => c.BindCloudListener(It.IsAny<ICloudListener>()));
            var underlyingDeviceProxy = new Mock<IDeviceProxy>();
            underlyingDeviceProxy.Setup(d => d.InvokeMethodAsync(It.IsAny<DirectMethodRequest>())).ReturnsAsync(default(DirectMethodResponse));
            underlyingDeviceProxy.SetupGet(d => d.IsActive).Returns(true);

            // ICloudConnectionProvider
            var cloudConnection = Mock.Of<ICloudConnection>(c => c.IsActive && c.CloudProxy == Option.Some(cloudProxy.Object));
            var cloudConnectionProvider = new Mock<ICloudConnectionProvider>();
            cloudConnectionProvider.Setup(c => c.Connect(It.IsAny<IClientCredentials>(), It.IsAny<Action<string, CloudConnectionStatus>>()))
                .ReturnsAsync(Try.Success(cloudConnection));
            var connectionManager = new ConnectionManager(cloudConnectionProvider.Object);

            // RoutingEdgeHub
            var routingEdgeHub = new RoutingEdgeHub(router, messageConverter, connectionManager, twinManager, "testEdgeDevice", Option.None<IMetricsRoot>());

            var deviceMessageHandler = new DeviceMessageHandler(identity, routingEdgeHub, connectionManager);
            var methodRequest = new DirectMethodRequest("device1/module1", "shutdown", null, TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(10));

            // Act
            deviceMessageHandler.BindDeviceProxy(underlyingDeviceProxy.Object);            
            Task<DirectMethodResponse> responseTask = routingEdgeHub.InvokeMethodAsync(identity.Id, methodRequest);

            // Assert
            Assert.True(responseTask.IsCompleted);
            Assert.Equal(404, responseTask.Result.Status);            
        }

        [Fact]
        public async Task AddEdgeSystemPropertiesTest()
        {
            // Create a mock endpoint capable of returning a mock processor
            var endpoint = new Mock<Endpoint>("myId");

            // Create a mock endpoint executor factory to create the endpoint executor to verify invocation
            var endpointExecutor = Mock.Of<IEndpointExecutor>();
            Mock.Get(endpointExecutor).SetupGet(ee => ee.Endpoint).Returns(() => endpoint.Object);
            var endpointExecutorFactory = Mock.Of<IEndpointExecutorFactory>();
            Mock.Get(endpointExecutorFactory).Setup(eef => eef.CreateAsync(It.IsAny<Endpoint>())).ReturnsAsync(endpointExecutor);

            // Create a route to map to the message
            var endpoints = new HashSet<Endpoint> { endpoint.Object };
            var route = new Route("myRoute", "true", "myIotHub", TelemetryMessageSource.Instance, endpoints);

            // Create a router
            var routerConfig = new RouterConfig(new[] { route });
            Router router = await Router.CreateAsync("myRouter", "myIotHub", routerConfig, endpointExecutorFactory);

            var messageConverter = Mock.Of<Core.IMessageConverter<IMessage>>();

            // Create mock for IConnectionManager
            var connectionManager = Mock.Of<IConnectionManager>();

            // Mock of twin manager
            var twinManager = Mock.Of<ITwinManager>();

            string edgeDeviceId = "testEdgeDevice";
            // Test Scenario
            var routingEdgeHub = new RoutingEdgeHub(router, messageConverter, connectionManager, twinManager, edgeDeviceId, Option.None<IMetricsRoot>());

            Message clientMessage1 = new Message.Builder(new byte[0]).Build();
            clientMessage1.SystemProperties[Core.SystemProperties.ConnectionDeviceId] = edgeDeviceId;
            routingEdgeHub.AddEdgeSystemProperties(clientMessage1);
            Assert.True(clientMessage1.SystemProperties.ContainsKey(Core.SystemProperties.EdgeHubOriginInterface));
            Assert.True(clientMessage1.SystemProperties.ContainsKey(Core.SystemProperties.EdgeMessageId));
            Assert.Equal(Core.Constants.InternalOriginInterface, clientMessage1.SystemProperties[Core.SystemProperties.EdgeHubOriginInterface]);

            Message clientMessage2 = new Message.Builder(new byte[0]).Build();
            clientMessage2.SystemProperties[Core.SystemProperties.ConnectionDeviceId] = "downstreamDevice";
            routingEdgeHub.AddEdgeSystemProperties(clientMessage2);
            Assert.True(clientMessage2.SystemProperties.ContainsKey(Core.SystemProperties.EdgeHubOriginInterface));
            Assert.True(clientMessage2.SystemProperties.ContainsKey(Core.SystemProperties.EdgeMessageId));
            Assert.Equal(Core.Constants.DownstreamOriginInterface, clientMessage2.SystemProperties[Core.SystemProperties.EdgeHubOriginInterface]);

            Message clientMessage3 = new Message.Builder(new byte[0]).Build();
            routingEdgeHub.AddEdgeSystemProperties(clientMessage3);
            Assert.False(clientMessage3.SystemProperties.ContainsKey(Core.SystemProperties.EdgeHubOriginInterface));
            Assert.True(clientMessage3.SystemProperties.ContainsKey(Core.SystemProperties.EdgeMessageId));
        }

        [Fact]
        public async Task ProcessC2DSubscriptionTest()
        {
            // Arrange
            RoutingEdgeHub edgeHub = await GetTestEdgeHub();
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(c => c.StartListening());

            // Act
            await edgeHub.ProcessSubscription(cloudProxy.Object, DeviceSubscription.C2D, true);

            // Assert
            cloudProxy.VerifyAll();

            // Arrange
            cloudProxy = new Mock<ICloudProxy>();
            
            // Act
            await edgeHub.ProcessSubscription(cloudProxy.Object, DeviceSubscription.C2D, false);

            // Assert
            cloudProxy.VerifyAll();
        }

        [Fact]
        public async Task ProcessDesiredPropertiesSubscriptionTest()
        {
            // Arrange
            RoutingEdgeHub edgeHub = await GetTestEdgeHub();
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(c => c.SetupDesiredPropertyUpdatesAsync())
                .Returns(Task.CompletedTask);

            // Act
            await edgeHub.ProcessSubscription(cloudProxy.Object, DeviceSubscription.DesiredPropertyUpdates, true);

            // Assert
            cloudProxy.VerifyAll();

            // Arrange
            cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(c => c.RemoveDesiredPropertyUpdatesAsync())
                .Returns(Task.CompletedTask);

            // Act
            await edgeHub.ProcessSubscription(cloudProxy.Object, DeviceSubscription.DesiredPropertyUpdates, false);

            // Assert
            cloudProxy.VerifyAll();
        }

        [Fact]
        public async Task ProcessMethodsSubscriptionTest()
        {
            // Arrange
            RoutingEdgeHub edgeHub = await GetTestEdgeHub();
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(c => c.SetupCallMethodAsync())
                .Returns(Task.CompletedTask);

            // Act
            await edgeHub.ProcessSubscription(cloudProxy.Object, DeviceSubscription.Methods, true);

            // Assert
            cloudProxy.VerifyAll();

            // Arrange
            cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(c => c.RemoveCallMethodAsync())
                .Returns(Task.CompletedTask);

            // Act
            await edgeHub.ProcessSubscription(cloudProxy.Object, DeviceSubscription.Methods, false);

            // Assert
            cloudProxy.VerifyAll();
        }

        [Fact]
        public async Task ProcessNoOpSubscriptionTest()
        {
            // Arrange
            RoutingEdgeHub edgeHub = await GetTestEdgeHub();
            var cloudProxy = new Mock<ICloudProxy>();
            
            // Act
            await edgeHub.ProcessSubscription(cloudProxy.Object, DeviceSubscription.ModuleMessages, true);
            await edgeHub.ProcessSubscription(cloudProxy.Object, DeviceSubscription.ModuleMessages, false);
            await edgeHub.ProcessSubscription(cloudProxy.Object, DeviceSubscription.TwinResponse, true);
            await edgeHub.ProcessSubscription(cloudProxy.Object, DeviceSubscription.TwinResponse, false);
            await edgeHub.ProcessSubscription(cloudProxy.Object, DeviceSubscription.Unknown, true);
            await edgeHub.ProcessSubscription(cloudProxy.Object, DeviceSubscription.Unknown, false);

            // Assert
            cloudProxy.VerifyAll();            
        }

        [Fact]
        public async Task AddSubscriptionTest()
        {
            // Arrange
            string deviceId = "d1";
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(c => c.SetupCallMethodAsync())
                .Returns(Task.CompletedTask);

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.AddSubscription(deviceId, DeviceSubscription.Methods));
            connectionManager.Setup(c => c.GetCloudConnection(deviceId)).Returns(Option.Some(cloudProxy.Object));
            IEdgeHub edgeHub = await GetTestEdgeHub(connectionManager.Object);
            
            // Act
            await edgeHub.AddSubscription(deviceId, DeviceSubscription.Methods);

            // Assert
            cloudProxy.VerifyAll();
            connectionManager.VerifyAll();
        }

        [Fact]
        public async Task AddSubscriptionThrowsTest()
        {
            // Arrange
            string deviceId = "d1";
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(c => c.SetupCallMethodAsync())
                .ThrowsAsync(new InvalidOperationException());

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.AddSubscription(deviceId, DeviceSubscription.Methods));
            connectionManager.Setup(c => c.GetCloudConnection(deviceId)).Returns(Option.Some(cloudProxy.Object));
            IEdgeHub edgeHub = await GetTestEdgeHub(connectionManager.Object);

            // Act
            await Assert.ThrowsAsync<InvalidOperationException>(() => edgeHub.AddSubscription(deviceId, DeviceSubscription.Methods));

            // Assert
            cloudProxy.VerifyAll();
            connectionManager.VerifyAll();
        }

        [Fact]
        public async Task AddSubscriptionTimesOutTest()
        {
            // Arrange
            string deviceId = "d1";
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(c => c.SetupCallMethodAsync())
                .ThrowsAsync(new TimeoutException());

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.AddSubscription(deviceId, DeviceSubscription.Methods));
            connectionManager.Setup(c => c.GetCloudConnection(deviceId)).Returns(Option.Some(cloudProxy.Object));
            IEdgeHub edgeHub = await GetTestEdgeHub(connectionManager.Object);

            // Act
            await edgeHub.AddSubscription(deviceId, DeviceSubscription.Methods);

            // Assert
            cloudProxy.VerifyAll();
            connectionManager.VerifyAll();
        }


        [Fact]
        public async Task RemoveSubscriptionTest()
        {
            // Arrange
            string deviceId = "d1";
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(c => c.SetupCallMethodAsync())
                .Returns(Task.CompletedTask);

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.AddSubscription(deviceId, DeviceSubscription.Methods));
            connectionManager.Setup(c => c.GetCloudConnection(deviceId)).Returns(Option.Some(cloudProxy.Object));
            IEdgeHub edgeHub = await GetTestEdgeHub(connectionManager.Object);

            // Act
            await edgeHub.AddSubscription(deviceId, DeviceSubscription.Methods);

            // Assert
            cloudProxy.VerifyAll();
            connectionManager.VerifyAll();
        }

        [Fact]
        public async Task RemoveSubscriptionThrowsTest()
        {
            // Arrange
            string deviceId = "d1";
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(c => c.SetupCallMethodAsync())
                .ThrowsAsync(new InvalidOperationException());

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.AddSubscription(deviceId, DeviceSubscription.Methods));
            connectionManager.Setup(c => c.GetCloudConnection(deviceId)).Returns(Option.Some(cloudProxy.Object));
            IEdgeHub edgeHub = await GetTestEdgeHub(connectionManager.Object);

            // Act
            await Assert.ThrowsAsync<InvalidOperationException>(() => edgeHub.AddSubscription(deviceId, DeviceSubscription.Methods));

            // Assert
            cloudProxy.VerifyAll();
            connectionManager.VerifyAll();
        }

        [Fact]
        public async Task RemoveSubscriptionTimesOutTest()
        {
            // Arrange
            string deviceId = "d1";
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(c => c.SetupCallMethodAsync())
                .ThrowsAsync(new TimeoutException());

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.AddSubscription(deviceId, DeviceSubscription.Methods));
            connectionManager.Setup(c => c.GetCloudConnection(deviceId)).Returns(Option.Some(cloudProxy.Object));
            IEdgeHub edgeHub = await GetTestEdgeHub(connectionManager.Object);

            // Act
            await edgeHub.AddSubscription(deviceId, DeviceSubscription.Methods);

            // Assert
            cloudProxy.VerifyAll();
            connectionManager.VerifyAll();
        }

        static async Task<RoutingEdgeHub> GetTestEdgeHub(IConnectionManager connectionManager = null)
        {
            // Arrange
            connectionManager = connectionManager ?? Mock.Of<IConnectionManager>();
            var endpoint = new Mock<Endpoint>("myId");
            var endpointExecutor = Mock.Of<IEndpointExecutor>();
            Mock.Get(endpointExecutor).SetupGet(ee => ee.Endpoint).Returns(() => endpoint.Object);
            var endpointExecutorFactory = Mock.Of<IEndpointExecutorFactory>();
            Mock.Get(endpointExecutorFactory).Setup(eef => eef.CreateAsync(It.IsAny<Endpoint>())).ReturnsAsync(endpointExecutor);

            // Create a route to map to the message
            var endpoints = new HashSet<Endpoint> { endpoint.Object };
            var route = new Route("myRoute", "true", "myIotHub", TelemetryMessageSource.Instance, endpoints);

            // Create a router
            var routerConfig = new RouterConfig(new[] { route });
            Router router = await Router.CreateAsync("myRouter", "myIotHub", routerConfig, endpointExecutorFactory);
            var edgeHub = new RoutingEdgeHub(router, Mock.Of<Core.IMessageConverter<IMessage>>(),
                connectionManager, Mock.Of<ITwinManager>(), "ed1", Option.None<IMetricsRoot>());
            return edgeHub;
        }
    }
}
