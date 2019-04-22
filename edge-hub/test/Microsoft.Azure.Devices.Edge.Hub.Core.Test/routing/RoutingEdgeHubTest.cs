// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Text;
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
    using Constants = Microsoft.Azure.Devices.Edge.Hub.Core.Constants;
    using IMessage = Microsoft.Azure.Devices.Edge.Hub.Core.IMessage;
    using Message = EdgeMessage;
    using SystemProperties = Microsoft.Azure.Devices.Edge.Hub.Core.SystemProperties;

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
            var message = Mock.Of<Devices.Routing.Core.IMessage>();
            Mock.Get(message).SetupGet(m => m.MessageSource).Returns(() => TelemetryMessageSource.Instance);
            var messageConverter = Mock.Of<Core.IMessageConverter<Devices.Routing.Core.IMessage>>();
            Mock.Get(messageConverter).Setup(mc => mc.FromMessage(It.IsAny<IMessage>())).Returns(message);

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
                Mock.Of<ISubscriptionProcessor>());
            var identity = new Mock<IIdentity>();
            identity.SetupGet(id => id.Id).Returns("something");
            EdgeMessage[] messages = { new EdgeMessage.Builder(new byte[0]).Build() };
            await routingEdgeHub.ProcessDeviceMessageBatch(identity.Object, messages);

            // Verify Expectation
            Mock.Get(endpointExecutor).Verify(e => e.Invoke(It.IsAny<Devices.Routing.Core.IMessage>()), Times.Once);
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

            Message badMessage = new EdgeMessage.Builder(new byte[300 * 1024]).Build();

            var routingEdgeHub = new RoutingEdgeHub(
                router,
                messageConverter,
                connectionManager,
                twinManager,
                "testEdgeDevice",
                Mock.Of<IInvokeMethodHandler>(),
                Mock.Of<ISubscriptionProcessor>());

            await Assert.ThrowsAsync<EdgeHubMessageTooLargeException>(() => routingEdgeHub.ProcessDeviceMessage(identity.Object, badMessage));

            string badString = Encoding.UTF8.GetString(new byte[300 * 1024], 0, 300 * 1024);
            var badProperties = new Dictionary<string, string> { ["toolong"] = badString };

            badMessage = new EdgeMessage.Builder(new byte[1]).SetProperties(badProperties).Build();

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

            var messageConverter = Mock.Of<Core.IMessageConverter<Devices.Routing.Core.IMessage>>();
            var connectionManager = Mock.Of<IConnectionManager>();
            var twinManager = new Mock<ITwinManager>();
            var message = Mock.Of<IMessage>();
            twinManager.Setup(t => t.GetTwinAsync(It.IsAny<string>())).Returns(Task.FromResult(message));
            var routingEdgeHub = new RoutingEdgeHub(
                router,
                messageConverter,
                connectionManager,
                twinManager.Object,
                "testEdgeDevice",
                Mock.Of<IInvokeMethodHandler>(),
                Mock.Of<ISubscriptionProcessor>());

            IMessage received = await routingEdgeHub.GetTwinAsync("*");
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

            var messageConverter = Mock.Of<Core.IMessageConverter<Devices.Routing.Core.IMessage>>();
            var connectionManager = Mock.Of<IConnectionManager>();
            var twinManager = new Mock<ITwinManager>();
            var message = Mock.Of<IMessage>();
            IMessage received = new EdgeMessage.Builder(new byte[0]).Build();
            twinManager.Setup(t => t.UpdateDesiredPropertiesAsync(It.IsAny<string>(), It.IsAny<IMessage>())).Callback<string, IMessage>((s, m) => received = message).Returns(Task.CompletedTask);
            var routingEdgeHub = new RoutingEdgeHub(
                router,
                messageConverter,
                connectionManager,
                twinManager.Object,
                "testEdgeDevice",
                Mock.Of<IInvokeMethodHandler>(),
                Mock.Of<ISubscriptionProcessor>());

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
            var messageConverter = Mock.Of<Core.IMessageConverter<Devices.Routing.Core.IMessage>>();

            // Mock of twin manager
            var twinManager = Mock.Of<ITwinManager>();

            // DeviceListener
            var identity = Mock.Of<IModuleIdentity>(m => m.DeviceId == "device1" && m.ModuleId == "module1" && m.Id == "device1/module1");
            var cloudProxy = new Mock<ICloudProxy>();
            var underlyingDeviceProxy = new Mock<IDeviceProxy>();
            underlyingDeviceProxy.Setup(d => d.InvokeMethodAsync(It.IsAny<DirectMethodRequest>())).ReturnsAsync(default(DirectMethodResponse));
            underlyingDeviceProxy.SetupGet(d => d.IsActive).Returns(true);

            // ICloudConnectionProvider
            var cloudConnection = Mock.Of<ICloudConnection>(c => c.IsActive && c.CloudProxy == Option.Some(cloudProxy.Object));
            var cloudConnectionProvider = new Mock<ICloudConnectionProvider>();
            cloudConnectionProvider.Setup(c => c.Connect(It.IsAny<IClientCredentials>(), It.IsAny<Action<string, CloudConnectionStatus>>()))
                .ReturnsAsync(Try.Success(cloudConnection));
            var connectionManager = new ConnectionManager(cloudConnectionProvider.Object, Mock.Of<ICredentialsCache>(), new IdentityProvider("myIotHub"));

            IInvokeMethodHandler invokeMethodHandler = new InvokeMethodHandler(connectionManager);

            var subscriptionProcessor = new SubscriptionProcessor(connectionManager, invokeMethodHandler, Mock.Of<IDeviceConnectivityManager>());

            // RoutingEdgeHub
            var routingEdgeHub = new RoutingEdgeHub(
                router,
                messageConverter,
                connectionManager,
                twinManager,
                "testEdgeDevice",
                invokeMethodHandler,
                subscriptionProcessor);

            var deviceMessageHandler = new DeviceMessageHandler(identity, routingEdgeHub, connectionManager);
            var methodRequest = new DirectMethodRequest("device1/module1", "shutdown", null, TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(10));

            // Act
            deviceMessageHandler.BindDeviceProxy(underlyingDeviceProxy.Object);
            await deviceMessageHandler.AddSubscription(DeviceSubscription.Methods);
            Task<DirectMethodResponse> responseTask = routingEdgeHub.InvokeMethodAsync(identity.Id, methodRequest);

            // Assert
            Assert.False(responseTask.IsCompleted);

            // Arrange
            Message message = new EdgeMessage.Builder(new byte[0]).Build();
            message.Properties[SystemProperties.CorrelationId] = methodRequest.CorrelationId;
            message.Properties[SystemProperties.StatusCode] = "200";

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
            var messageConverter = Mock.Of<Core.IMessageConverter<Devices.Routing.Core.IMessage>>();
            var methodRequest = new DirectMethodRequest("device1/module1", "shutdown", null, TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(10));

            // Mock of twin manager
            var twinManager = Mock.Of<ITwinManager>();

            // DeviceListener
            var identity = Mock.Of<IModuleIdentity>(m => m.DeviceId == "device1" && m.ModuleId == "module1" && m.Id == "device1/module1");
            var cloudProxy = new Mock<ICloudProxy>();
            var underlyingDeviceProxy = new Mock<IDeviceProxy>();
            underlyingDeviceProxy.Setup(d => d.InvokeMethodAsync(It.IsAny<DirectMethodRequest>())).ReturnsAsync(new DirectMethodResponse(methodRequest.CorrelationId, null, 200));
            underlyingDeviceProxy.SetupGet(d => d.IsActive).Returns(true);

            // ICloudConnectionProvider
            var cloudConnection = Mock.Of<ICloudConnection>(c => c.IsActive && c.CloudProxy == Option.Some(cloudProxy.Object));
            var cloudConnectionProvider = new Mock<ICloudConnectionProvider>();
            cloudConnectionProvider.Setup(c => c.Connect(It.IsAny<IClientCredentials>(), It.IsAny<Action<string, CloudConnectionStatus>>()))
                .ReturnsAsync(Try.Success(cloudConnection));
            var connectionManager = new ConnectionManager(cloudConnectionProvider.Object, Mock.Of<ICredentialsCache>(), new IdentityProvider("myIotHub"));

            IInvokeMethodHandler invokeMethodHandler = new InvokeMethodHandler(connectionManager);

            // RoutingEdgeHub
            var routingEdgeHub = new RoutingEdgeHub(
                router,
                messageConverter,
                connectionManager,
                twinManager,
                "testEdgeDevice",
                invokeMethodHandler,
                Mock.Of<ISubscriptionProcessor>());

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
            var messageConverter = Mock.Of<Core.IMessageConverter<Devices.Routing.Core.IMessage>>();
            var methodRequest = new DirectMethodRequest("device1/module1", "shutdown", null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(20));

            // Mock of twin manager
            var twinManager = Mock.Of<ITwinManager>();

            // DeviceListener
            var identity = Mock.Of<IModuleIdentity>(m => m.DeviceId == "device1" && m.ModuleId == "module1" && m.Id == "device1/module1");
            var cloudProxy = new Mock<ICloudProxy>();

            // ICloudConnectionProvider
            var cloudConnection = Mock.Of<ICloudConnection>(c => c.IsActive && c.CloudProxy == Option.Some(cloudProxy.Object));
            var cloudConnectionProvider = new Mock<ICloudConnectionProvider>();
            cloudConnectionProvider.Setup(c => c.Connect(It.IsAny<IIdentity>(), It.IsAny<Action<string, CloudConnectionStatus>>()))
                .ReturnsAsync(Try.Success(cloudConnection));
            var connectionManager = new ConnectionManager(cloudConnectionProvider.Object, Mock.Of<ICredentialsCache>(), new IdentityProvider("myIotHub"));

            IInvokeMethodHandler invokeMethodHandler = new InvokeMethodHandler(connectionManager);
            var subscriptionProcessor = new SubscriptionProcessor(connectionManager, invokeMethodHandler, Mock.Of<IDeviceConnectivityManager>());

            // RoutingEdgeHub
            var routingEdgeHub = new RoutingEdgeHub(
                router,
                messageConverter,
                connectionManager,
                twinManager,
                "testEdgeDevice",
                invokeMethodHandler,
                subscriptionProcessor);

            var deviceMessageHandler = new DeviceMessageHandler(identity, routingEdgeHub, connectionManager);
            var underlyingDeviceProxy = new Mock<IDeviceProxy>();

            // Arrange
            Message message = new EdgeMessage.Builder(new byte[0]).Build();
            message.Properties[SystemProperties.CorrelationId] = methodRequest.CorrelationId;
            message.Properties[SystemProperties.StatusCode] = "200";

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

            var messageConverter = Mock.Of<Core.IMessageConverter<Devices.Routing.Core.IMessage>>();

            // Create mock for IConnectionManager
            var connectionManager = Mock.Of<IConnectionManager>();

            // Mock of twin manager
            var twinManager = Mock.Of<ITwinManager>();

            string edgeDeviceId = "testEdgeDevice";
            // Test Scenario
            var routingEdgeHub = new RoutingEdgeHub(
                router,
                messageConverter,
                connectionManager,
                twinManager,
                edgeDeviceId,
                Mock.Of<IInvokeMethodHandler>(),
                Mock.Of<ISubscriptionProcessor>());

            Message clientMessage1 = new EdgeMessage.Builder(new byte[0]).Build();
            clientMessage1.SystemProperties[SystemProperties.ConnectionDeviceId] = edgeDeviceId;
            routingEdgeHub.AddEdgeSystemProperties(clientMessage1);
            Assert.True(clientMessage1.SystemProperties.ContainsKey(SystemProperties.EdgeHubOriginInterface));
            Assert.True(clientMessage1.SystemProperties.ContainsKey(SystemProperties.EdgeMessageId));
            Assert.Equal(Constants.InternalOriginInterface, clientMessage1.SystemProperties[SystemProperties.EdgeHubOriginInterface]);

            Message clientMessage2 = new EdgeMessage.Builder(new byte[0]).Build();
            clientMessage2.SystemProperties[SystemProperties.ConnectionDeviceId] = "downstreamDevice";
            routingEdgeHub.AddEdgeSystemProperties(clientMessage2);
            Assert.True(clientMessage2.SystemProperties.ContainsKey(SystemProperties.EdgeHubOriginInterface));
            Assert.True(clientMessage2.SystemProperties.ContainsKey(SystemProperties.EdgeMessageId));
            Assert.Equal(Constants.DownstreamOriginInterface, clientMessage2.SystemProperties[SystemProperties.EdgeHubOriginInterface]);

            Message clientMessage3 = new EdgeMessage.Builder(new byte[0]).Build();
            routingEdgeHub.AddEdgeSystemProperties(clientMessage3);
            Assert.False(clientMessage3.SystemProperties.ContainsKey(SystemProperties.EdgeHubOriginInterface));
            Assert.True(clientMessage3.SystemProperties.ContainsKey(SystemProperties.EdgeMessageId));
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
                Mock.Of<Core.IMessageConverter<Devices.Routing.Core.IMessage>>(),
                connectionManager,
                Mock.Of<ITwinManager>(),
                "ed1",
                Mock.Of<IInvokeMethodHandler>(),
                Mock.Of<ISubscriptionProcessor>());
            return edgeHub;
        }
    }
}
