// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Routing;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Moq;
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
            var routingEdgeHub = new RoutingEdgeHub(
                router,
                messageConverter,
                connectionManager,
                twinManager,
                "testEdgeDevice",
                Mock.Of<IInvokeMethodHandler>(),
                Mock.Of<IDeviceConnectivityManager>());
            var identity = new Mock<IIdentity>();
            identity.SetupGet(id => id.Id).Returns("something");
            EdgeMessage[] messages = { new Message.Builder(new byte[0]).Build() };
            await routingEdgeHub.ProcessDeviceMessageBatch(identity.Object, messages);

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
            var identity = new Mock<IIdentity>();
            identity.SetupGet(id => id.Id).Returns("something");

            var messageConverter = new RoutingMessageConverter();

            Message badMessage = new Message.Builder(new byte[300 * 1024]).Build();

            var routingEdgeHub = new RoutingEdgeHub(
                router,
                messageConverter,
                connectionManager,
                twinManager,
                "testEdgeDevice",
                Mock.Of<IInvokeMethodHandler>(),
                Mock.Of<IDeviceConnectivityManager>());

            await Assert.ThrowsAsync<EdgeHubMessageTooLargeException>(() => routingEdgeHub.ProcessDeviceMessage(identity.Object, badMessage));

            string badString = System.Text.Encoding.UTF8.GetString(new byte[300 * 1024], 0, 300 * 1024);
            var badProperties = new Dictionary<string, string> { ["toolong"] = badString };

            badMessage = new Message.Builder(new byte[1]).SetProperties(badProperties).Build();

            await Assert.ThrowsAsync<EdgeHubMessageTooLargeException>(() => routingEdgeHub.ProcessDeviceMessage(identity.Object, badMessage));

            badMessage = new Message(new byte[1], new Dictionary<string, string>(), badProperties);

            await Assert.ThrowsAsync<EdgeHubMessageTooLargeException>(() => routingEdgeHub.ProcessDeviceMessage(identity.Object, badMessage));
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
            var routingEdgeHub = new RoutingEdgeHub(router, messageConverter, connectionManager, twinManager.Object, "testEdgeDevice", Mock.Of<IInvokeMethodHandler>(), Mock.Of<IDeviceConnectivityManager>());

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
            var routingEdgeHub = new RoutingEdgeHub(router, messageConverter, connectionManager, twinManager.Object, "testEdgeDevice", Mock.Of<IInvokeMethodHandler>(), Mock.Of<IDeviceConnectivityManager>());

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
            var clientCredentials = Mock.Of<IClientCredentials>(c => c.Identity == identity);
            var cloudProxy = new Mock<ICloudProxy>();
            var underlyingDeviceProxy = new Mock<IDeviceProxy>();
            underlyingDeviceProxy.Setup(d => d.InvokeMethodAsync(It.IsAny<DirectMethodRequest>())).ReturnsAsync(default(DirectMethodResponse));
            underlyingDeviceProxy.SetupGet(d => d.IsActive).Returns(true);

            // ICloudConnectionProvider
            var cloudConnection = Mock.Of<ICloudConnection>(c => c.IsActive && c.CloudProxy == Option.Some(cloudProxy.Object));
            var cloudConnectionProvider = new Mock<ICloudConnectionProvider>();
            cloudConnectionProvider.Setup(c => c.Connect(It.IsAny<IClientCredentials>(), It.IsAny<Action<string, CloudConnectionStatus>>()))
                .ReturnsAsync(Try.Success(cloudConnection));
            var credentialsCache = Mock.Of<ICredentialsCache>(c => c.Get(identity) == Task.FromResult(Option.Some(clientCredentials)));
            var connectionManager = new ConnectionManager(cloudConnectionProvider.Object, credentialsCache);

            IInvokeMethodHandler invokeMethodHandler = new InvokeMethodHandler(connectionManager);

            // RoutingEdgeHub
            var routingEdgeHub = new RoutingEdgeHub(router, messageConverter, connectionManager, twinManager, "testEdgeDevice", invokeMethodHandler, Mock.Of<IDeviceConnectivityManager>());

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
            var methodRequest = new DirectMethodRequest("device1/module1", "shutdown", null, TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(10));

            // Mock of twin manager
            var twinManager = Mock.Of<ITwinManager>();

            // DeviceListener
            var identity = Mock.Of<IModuleIdentity>(m => m.DeviceId == "device1" && m.ModuleId == "module1" && m.Id == "device1/module1");
            var clientCredentials = Mock.Of<IClientCredentials>(c => c.Identity == identity);
            var cloudProxy = new Mock<ICloudProxy>();
            var underlyingDeviceProxy = new Mock<IDeviceProxy>();
            underlyingDeviceProxy.Setup(d => d.InvokeMethodAsync(It.IsAny<DirectMethodRequest>())).ReturnsAsync(new DirectMethodResponse(methodRequest.CorrelationId, null, 200));
            underlyingDeviceProxy.SetupGet(d => d.IsActive).Returns(true);

            // ICloudConnectionProvider
            var cloudConnection = Mock.Of<ICloudConnection>(c => c.IsActive && c.CloudProxy == Option.Some(cloudProxy.Object));
            var cloudConnectionProvider = new Mock<ICloudConnectionProvider>();
            cloudConnectionProvider.Setup(c => c.Connect(It.IsAny<IClientCredentials>(), It.IsAny<Action<string, CloudConnectionStatus>>()))
                .ReturnsAsync(Try.Success(cloudConnection));
            var credentialsCache = Mock.Of<ICredentialsCache>(c => c.Get(identity) == Task.FromResult(Option.Some(clientCredentials)));
            var connectionManager = new ConnectionManager(cloudConnectionProvider.Object, credentialsCache);

            IInvokeMethodHandler invokeMethodHandler = new InvokeMethodHandler(connectionManager);

            // RoutingEdgeHub
            var routingEdgeHub = new RoutingEdgeHub(router, messageConverter, connectionManager, twinManager, "testEdgeDevice", invokeMethodHandler, Mock.Of<IDeviceConnectivityManager>());

            var deviceMessageHandler = new DeviceMessageHandler(identity, routingEdgeHub, connectionManager);

            // Act
            deviceMessageHandler.BindDeviceProxy(underlyingDeviceProxy.Object);
            Task<DirectMethodResponse> responseTask = routingEdgeHub.InvokeMethodAsync(identity.Id, methodRequest);

            // Assert
            Assert.False(responseTask.IsCompleted);

            // Act
            await routingEdgeHub.AddSubscription(identity.Id, DeviceSubscription.Methods);
            await Task.Delay(TimeSpan.FromSeconds(5));

            // Assert
            Assert.True(responseTask.IsCompleted);
            Assert.Null(responseTask.Result.CorrelationId);
            Assert.Equal(0, responseTask.Result.Status);
            Assert.IsType<EdgeHubTimeoutException>(responseTask.Result.Exception.OrDefault());
            Assert.Equal(HttpStatusCode.NotFound, responseTask.Result.HttpStatusCode);
        }

        [Fact]
        public async Task InvokeMethodLateSubscriptionTest()
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
            var methodRequest = new DirectMethodRequest("device1/module1", "shutdown", null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(20));

            // Mock of twin manager
            var twinManager = Mock.Of<ITwinManager>();

            // DeviceListener
            var identity = Mock.Of<IModuleIdentity>(m => m.DeviceId == "device1" && m.ModuleId == "module1" && m.Id == "device1/module1");
            var clientCredentials = Mock.Of<IClientCredentials>(c => c.Identity == identity);
            var cloudProxy = new Mock<ICloudProxy>();

            // ICloudConnectionProvider
            var cloudConnection = Mock.Of<ICloudConnection>(c => c.IsActive && c.CloudProxy == Option.Some(cloudProxy.Object));
            var cloudConnectionProvider = new Mock<ICloudConnectionProvider>();
            cloudConnectionProvider.Setup(c => c.Connect(It.IsAny<IClientCredentials>(), It.IsAny<Action<string, CloudConnectionStatus>>()))
                .ReturnsAsync(Try.Success(cloudConnection));
            var credentialsCache = Mock.Of<ICredentialsCache>(c => c.Get(identity) == Task.FromResult(Option.Some(clientCredentials)));
            var connectionManager = new ConnectionManager(cloudConnectionProvider.Object, credentialsCache);

            IInvokeMethodHandler invokeMethodHandler = new InvokeMethodHandler(connectionManager);

            // RoutingEdgeHub
            var routingEdgeHub = new RoutingEdgeHub(router, messageConverter, connectionManager, twinManager, "testEdgeDevice", invokeMethodHandler, Mock.Of<IDeviceConnectivityManager>());

            var deviceMessageHandler = new DeviceMessageHandler(identity, routingEdgeHub, connectionManager);
            var underlyingDeviceProxy = new Mock<IDeviceProxy>();

            // Arrange
            Message message = new Message.Builder(new byte[0]).Build();
            message.Properties[Core.SystemProperties.CorrelationId] = methodRequest.CorrelationId;
            message.Properties[Core.SystemProperties.StatusCode] = "200";

            underlyingDeviceProxy.Setup(d => d.InvokeMethodAsync(It.IsAny<DirectMethodRequest>()))
                .Callback(() => deviceMessageHandler.ProcessMethodResponseAsync(message))
                .ReturnsAsync(default(DirectMethodResponse));
            underlyingDeviceProxy.SetupGet(d => d.IsActive).Returns(true);

            // Act
            deviceMessageHandler.BindDeviceProxy(underlyingDeviceProxy.Object);
            Task<DirectMethodResponse> responseTask = routingEdgeHub.InvokeMethodAsync(identity.Id, methodRequest);

            // Assert
            Assert.False(responseTask.IsCompleted);

            // Act
            await routingEdgeHub.AddSubscription(identity.Id, DeviceSubscription.Methods);
            await Task.Delay(TimeSpan.FromSeconds(5));

            // Assert
            Assert.True(responseTask.IsCompleted);
            Assert.Equal(methodRequest.CorrelationId, responseTask.Result.CorrelationId);
            Assert.Equal(200, responseTask.Result.Status);
            Assert.False(responseTask.Result.Exception.HasValue);
            Assert.Equal(HttpStatusCode.OK, responseTask.Result.HttpStatusCode);
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
            var routingEdgeHub = new RoutingEdgeHub(router, messageConverter, connectionManager, twinManager, edgeDeviceId, Mock.Of<IInvokeMethodHandler>(), Mock.Of<IDeviceConnectivityManager>());

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
            string id = "d1";
            RoutingEdgeHub edgeHub = await GetTestEdgeHub();
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(c => c.StartListening());

            // Act
            await edgeHub.ProcessSubscription(id, Option.Some(cloudProxy.Object), DeviceSubscription.C2D, true);

            // Assert
            cloudProxy.VerifyAll();

            // Arrange
            cloudProxy = new Mock<ICloudProxy>();

            // Act
            await edgeHub.ProcessSubscription(id, Option.Some(cloudProxy.Object), DeviceSubscription.C2D, false);

            // Assert
            cloudProxy.VerifyAll();
        }

        [Fact]
        public async Task ProcessDesiredPropertiesSubscriptionTest()
        {
            // Arrange
            string id = "d1";
            RoutingEdgeHub edgeHub = await GetTestEdgeHub();
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(c => c.SetupDesiredPropertyUpdatesAsync())
                .Returns(Task.CompletedTask);

            // Act
            await edgeHub.ProcessSubscription(id, Option.Some(cloudProxy.Object), DeviceSubscription.DesiredPropertyUpdates, true);

            // Assert
            cloudProxy.VerifyAll();

            // Arrange
            cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(c => c.RemoveDesiredPropertyUpdatesAsync())
                .Returns(Task.CompletedTask);

            // Act
            await edgeHub.ProcessSubscription(id, Option.Some(cloudProxy.Object), DeviceSubscription.DesiredPropertyUpdates, false);

            // Assert
            cloudProxy.VerifyAll();
        }

        [Fact]
        public async Task ProcessMethodsSubscriptionTest()
        {
            // Arrange
            string id = "d1";
            RoutingEdgeHub edgeHub = await GetTestEdgeHub();
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(c => c.SetupCallMethodAsync())
                .Returns(Task.CompletedTask);

            // Act
            await edgeHub.ProcessSubscription(id, Option.Some(cloudProxy.Object), DeviceSubscription.Methods, true);

            // Assert
            cloudProxy.VerifyAll();

            // Arrange
            cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(c => c.RemoveCallMethodAsync())
                .Returns(Task.CompletedTask);

            // Act
            await edgeHub.ProcessSubscription(id, Option.Some(cloudProxy.Object), DeviceSubscription.Methods, false);

            // Assert
            cloudProxy.VerifyAll();
        }

        [Fact]
        public async Task ProcessNoOpSubscriptionTest()
        {
            // Arrange
            string id = "d1";
            RoutingEdgeHub edgeHub = await GetTestEdgeHub();
            var cloudProxy = new Mock<ICloudProxy>();

            // Act
            await edgeHub.ProcessSubscription(id, Option.Some(cloudProxy.Object), DeviceSubscription.ModuleMessages, true);
            await edgeHub.ProcessSubscription(id, Option.Some(cloudProxy.Object), DeviceSubscription.ModuleMessages, false);
            await edgeHub.ProcessSubscription(id, Option.Some(cloudProxy.Object), DeviceSubscription.TwinResponse, true);
            await edgeHub.ProcessSubscription(id, Option.Some(cloudProxy.Object), DeviceSubscription.TwinResponse, false);
            await edgeHub.ProcessSubscription(id, Option.Some(cloudProxy.Object), DeviceSubscription.Unknown, true);
            await edgeHub.ProcessSubscription(id, Option.Some(cloudProxy.Object), DeviceSubscription.Unknown, false);

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
            connectionManager.Setup(c => c.GetCloudConnection(deviceId)).Returns(Task.FromResult(Option.Some(cloudProxy.Object)));
            IEdgeHub edgeHub = await GetTestEdgeHub(connectionManager.Object);

            // Act
            await edgeHub.AddSubscription(deviceId, DeviceSubscription.Methods);

            // Assert
            cloudProxy.VerifyAll();
            connectionManager.VerifyAll();
        }

        [Fact]
        public async Task AddSubscriptionHandlesExceptionTest()
        {
            // Arrange
            string deviceId = "d1";
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(c => c.SetupCallMethodAsync())
                .ThrowsAsync(new InvalidOperationException());

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.AddSubscription(deviceId, DeviceSubscription.Methods));
            connectionManager.Setup(c => c.GetCloudConnection(deviceId)).Returns(Task.FromResult(Option.Some(cloudProxy.Object)));
            IEdgeHub edgeHub = await GetTestEdgeHub(connectionManager.Object);

            // Act
            await edgeHub.AddSubscription(deviceId, DeviceSubscription.Methods);

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
            connectionManager.Setup(c => c.GetCloudConnection(deviceId)).Returns(Task.FromResult(Option.Some(cloudProxy.Object)));
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
            connectionManager.Setup(c => c.GetCloudConnection(deviceId)).Returns(Task.FromResult(Option.Some(cloudProxy.Object)));
            IEdgeHub edgeHub = await GetTestEdgeHub(connectionManager.Object);

            // Act
            await edgeHub.AddSubscription(deviceId, DeviceSubscription.Methods);

            // Assert
            cloudProxy.VerifyAll();
            connectionManager.VerifyAll();
        }

        [Fact]
        public async Task RemoveSubscriptionHandlesExceptionTest()
        {
            // Arrange
            string deviceId = "d1";
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(c => c.SetupCallMethodAsync())
                .ThrowsAsync(new InvalidOperationException());

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.AddSubscription(deviceId, DeviceSubscription.Methods));
            connectionManager.Setup(c => c.GetCloudConnection(deviceId)).Returns(Task.FromResult(Option.Some(cloudProxy.Object)));
            IEdgeHub edgeHub = await GetTestEdgeHub(connectionManager.Object);

            // Act
            await edgeHub.AddSubscription(deviceId, DeviceSubscription.Methods);

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
            connectionManager.Setup(c => c.GetCloudConnection(deviceId)).Returns(Task.FromResult(Option.Some(cloudProxy.Object)));
            IEdgeHub edgeHub = await GetTestEdgeHub(connectionManager.Object);

            // Act
            await edgeHub.AddSubscription(deviceId, DeviceSubscription.Methods);

            // Assert
            cloudProxy.VerifyAll();
            connectionManager.VerifyAll();
        }

        [Fact]
        public async Task ProcessSubscriptionsOnDeviceConnected()
        {
            // Arrange
            string d1 = "d1";
            var deviceIdentity = Mock.Of<IIdentity>(d => d.Id == d1);
            string m1 = "d2/m1";
            var moduleIdentity = Mock.Of<IIdentity>(m => m.Id == m1);

            var connectedClients = new List<IIdentity>
            {
                deviceIdentity,
                moduleIdentity
            };
            
            IReadOnlyDictionary<DeviceSubscription, bool> device1Subscriptions = new Dictionary<DeviceSubscription, bool>()
            {
                [DeviceSubscription.Methods] = true,
                [DeviceSubscription.DesiredPropertyUpdates] = true
            };

            IReadOnlyDictionary<DeviceSubscription, bool> module1Subscriptions = new Dictionary<DeviceSubscription, bool>()
            {
                [DeviceSubscription.Methods] = true,
                [DeviceSubscription.ModuleMessages] = true
            };

            var device1CloudProxy = Mock.Of<ICloudProxy>(dc => dc.SetupDesiredPropertyUpdatesAsync() == Task.CompletedTask
                && dc.SetupCallMethodAsync() == Task.CompletedTask);
            Mock.Get(device1CloudProxy).SetupGet(d => d.IsActive).Returns(true);
            var module1CloudProxy = Mock.Of<ICloudProxy>(mc => mc.SetupCallMethodAsync() == Task.CompletedTask && mc.IsActive);

            var invokeMethodHandler = Mock.Of<IInvokeMethodHandler>(m =>
                m.ProcessInvokeMethodSubscription(d1) == Task.CompletedTask
                && m.ProcessInvokeMethodSubscription(m1) == Task.CompletedTask);

            var connectionManager = Mock.Of<IConnectionManager>(c =>
                c.GetConnectedClients() == connectedClients
                && c.GetSubscriptions(d1) == Option.Some(device1Subscriptions)
                && c.GetSubscriptions(m1) == Option.Some(module1Subscriptions)
                && c.GetCloudConnection(d1) == Task.FromResult(Option.Some(device1CloudProxy))
                && c.GetCloudConnection(m1) == Task.FromResult(Option.Some(module1CloudProxy)));

            var endpoint = new Mock<Endpoint>("myId");
            var endpointExecutor = Mock.Of<IEndpointExecutor>();
            Mock.Get(endpointExecutor).SetupGet(ee => ee.Endpoint).Returns(() => endpoint.Object);
            var endpointExecutorFactory = Mock.Of<IEndpointExecutorFactory>();
            Mock.Get(endpointExecutorFactory).Setup(eef => eef.CreateAsync(It.IsAny<Endpoint>())).ReturnsAsync(endpointExecutor);
            var endpoints = new HashSet<Endpoint> { endpoint.Object };
            var route = new Route("myRoute", "true", "myIotHub", TelemetryMessageSource.Instance, endpoints);
            var routerConfig = new RouterConfig(new[] { route });
            Router router = await Router.CreateAsync("myRouter", "myIotHub", routerConfig, endpointExecutorFactory);

            var deviceConnectivityManager = Mock.Of<IDeviceConnectivityManager>();

            var edgeHub = new RoutingEdgeHub(
                router,
                Mock.Of<Core.IMessageConverter<IMessage>>(),
                connectionManager,
                Mock.Of<ITwinManager>(),
                "ed1",
                invokeMethodHandler,
                deviceConnectivityManager);

            // Act
            Mock.Get(deviceConnectivityManager).Raise(d => d.DeviceConnected += null, new EventArgs());

            // Assert
            Mock.Get(device1CloudProxy).Verify(d => d.SetupDesiredPropertyUpdatesAsync(), Times.Once);
            Mock.Get(device1CloudProxy).Verify(d => d.SetupCallMethodAsync(), Times.Once);
            Mock.Get(module1CloudProxy).Verify(m => m.SetupCallMethodAsync(), Times.Once);
            Mock.Get(invokeMethodHandler).VerifyAll();
            Mock.Get(connectionManager).VerifyAll();
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
            var edgeHub = new RoutingEdgeHub(
                router,
                Mock.Of<Core.IMessageConverter<IMessage>>(),
                connectionManager,
                Mock.Of<ITwinManager>(),
                "ed1",
                Mock.Of<IInvokeMethodHandler>(),
                Mock.Of<IDeviceConnectivityManager>());
            return edgeHub;
        }
    }
}
