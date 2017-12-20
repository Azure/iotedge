// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Routing
{
    using System;
    using System.Collections.Generic;
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
    using Message = Microsoft.Azure.Devices.Edge.Hub.Core.Test.Message;

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
            var routingEdgeHub = new RoutingEdgeHub(router, messageConverter, connectionManager, twinManager, "testEdgeDevice");
            var identity = Mock.Of<IIdentity>();
            var messages = new[] { new Message(new byte[0]) };
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

            var badMessage = new Message(new byte[300 * 1024]);

            var routingEdgeHub = new RoutingEdgeHub(router, messageConverter, connectionManager, twinManager, "testEdgeDevice");

            await Assert.ThrowsAsync<InvalidOperationException>(() => routingEdgeHub.ProcessDeviceMessage(identity, badMessage));

            string badString = System.Text.Encoding.UTF8.GetString(new byte[300 * 1024], 0, 300 * 1024);
            var badProperties = new Dictionary<string, string> { ["toolong"] = badString };

            badMessage = new Message(new byte[1], badProperties);

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
            var routingEdgeHub = new RoutingEdgeHub(router, messageConverter, connectionManager, twinManager.Object, "testEdgeDevice");

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
            Core.IMessage received = new Message(new byte[0]);
            twinManager.Setup(t => t.UpdateDesiredPropertiesAsync(It.IsAny<string>(), It.IsAny<Core.IMessage>())).Callback<string, Core.IMessage>((s, m) => received = message).Returns(Task.CompletedTask);
            var routingEdgeHub = new RoutingEdgeHub(router, messageConverter, connectionManager, twinManager.Object, "testEdgeDevice");

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

            // Create mock for IConnectionManager
            var connectionManager = Mock.Of<IConnectionManager>();
            Mock.Get(connectionManager).Setup(c => c.AddDeviceConnection(It.IsAny<IIdentity>(), It.IsAny<IDeviceProxy>()));

            // Mock of twin manager
            var twinManager = Mock.Of<ITwinManager>();

            // RoutingEdgeHub
            var routingEdgeHub = new RoutingEdgeHub(router, messageConverter, connectionManager, twinManager, "testEdgeDevice");

            // DeviceListener
            var identity = Mock.Of<IModuleIdentity>(m => m.DeviceId == "device1" && m.ModuleId == "module1" && m.Id == "device1/module1");
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(c => c.BindCloudListener(It.IsAny<ICloudListener>()));
            var underlyingDeviceProxy = new Mock<IDeviceProxy>();
            underlyingDeviceProxy.Setup(d => d.InvokeMethodAsync(It.IsAny<DirectMethodRequest>())).ReturnsAsync(default(DirectMethodResponse));
            var deviceMessageHandler = new DeviceMessageHandler(identity, routingEdgeHub, connectionManager, cloudProxy.Object);
            deviceMessageHandler.BindDeviceProxy(underlyingDeviceProxy.Object);
            Mock.Get(connectionManager).Setup(c => c.GetDeviceConnection(It.Is<string>(d => d == "device1/module1"))).Returns(Option.Some((IDeviceProxy)deviceMessageHandler));

            var methodRequest = new DirectMethodRequest("device1/module1", "shutdown", null, TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(10));

            // Act
            Task<DirectMethodResponse> responseTask = routingEdgeHub.InvokeMethodAsync(identity.Id, methodRequest);
            Assert.False(responseTask.IsCompleted);

            var message = new Message(new byte[0]);
            message.Properties[Hub.Core.SystemProperties.CorrelationId] = methodRequest.CorrelationId;
            message.Properties[Hub.Core.SystemProperties.StatusCode] = "200";
            await deviceMessageHandler.ProcessMethodResponseAsync(message);
            Assert.True(responseTask.IsCompleted);
            Assert.Equal(methodRequest.CorrelationId, responseTask.Result.CorrelationId);
            Assert.Equal(200, responseTask.Result.Status);
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
            var routingEdgeHub = new RoutingEdgeHub(router, messageConverter, connectionManager, twinManager, edgeDeviceId);            

            var clientMessage1 = new Message(new byte[0]);
            clientMessage1.SystemProperties[Core.SystemProperties.ConnectionDeviceId] = edgeDeviceId;
            routingEdgeHub.AddEdgeSystemProperties(clientMessage1);
            Assert.True(clientMessage1.SystemProperties.ContainsKey(Core.SystemProperties.EdgeHubOriginInterface));
            Assert.True(clientMessage1.SystemProperties.ContainsKey(Core.SystemProperties.EdgeMessageId));
            Assert.Equal(Core.Constants.InternalOriginInterface, clientMessage1.SystemProperties[Core.SystemProperties.EdgeHubOriginInterface]);

            var clientMessage2 = new Message(new byte[0]);
            clientMessage2.SystemProperties[Core.SystemProperties.ConnectionDeviceId] = "downstreamDevice";
            routingEdgeHub.AddEdgeSystemProperties(clientMessage2);
            Assert.True(clientMessage2.SystemProperties.ContainsKey(Core.SystemProperties.EdgeHubOriginInterface));
            Assert.True(clientMessage2.SystemProperties.ContainsKey(Core.SystemProperties.EdgeMessageId));
            Assert.Equal(Core.Constants.DownstreamOriginInterface, clientMessage2.SystemProperties[Core.SystemProperties.EdgeHubOriginInterface]);

            var clientMessage3 = new Message(new byte[0]);
            routingEdgeHub.AddEdgeSystemProperties(clientMessage3);
            Assert.False(clientMessage3.SystemProperties.ContainsKey(Core.SystemProperties.EdgeHubOriginInterface));
            Assert.True(clientMessage3.SystemProperties.ContainsKey(Core.SystemProperties.EdgeMessageId));
        }
    }
}
