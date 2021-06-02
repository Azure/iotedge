// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Routing;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core;
    using Moq;
    using Xunit;
    using IMessage = Microsoft.Azure.Devices.Edge.Hub.Core.IMessage;
    using IRoutingMessage = Microsoft.Azure.Devices.Routing.Core.IMessage;

    [Unit]
    public class ModuleEndpointTest
    {
        [Fact]
        public void ModuleEndpoint_MembersTest()
        {
            Core.IMessageConverter<IRoutingMessage> routingMessageConverter = new RoutingMessageConverter();
            var connectionManager = Mock.Of<IConnectionManager>();
            string moduleId = "device1/module1";
            string moduleEndpointAddress = "in1";
            string endpointId = $"{moduleId}/{moduleEndpointAddress}";
            var moduleEndpoint = new ModuleEndpoint(endpointId, moduleId, moduleEndpointAddress, connectionManager, routingMessageConverter);

            Assert.Equal(endpointId, moduleEndpoint.Id);
            Assert.Equal("ModuleEndpoint", moduleEndpoint.Type);
            Assert.Equal(endpointId, moduleEndpoint.Name);
            Assert.Equal(string.Empty, moduleEndpoint.IotHubName);
        }

        [Fact]
        public void ModuleMessageProcessor_CreateProcessorTest()
        {
            Core.IMessageConverter<IRoutingMessage> routingMessageConverter = new RoutingMessageConverter();
            var connectionManager = Mock.Of<IConnectionManager>();
            string moduleId = "device1/module1";
            string moduleEndpointAddress = "in1";

            var moduleEndpoint = new ModuleEndpoint($"{moduleId}/{moduleEndpointAddress}", moduleId, moduleEndpointAddress, connectionManager, routingMessageConverter);

            IProcessor moduleMessageProcessor = moduleEndpoint.CreateProcessor();
            Assert.NotNull(moduleMessageProcessor);
            Assert.Equal(moduleEndpoint, moduleMessageProcessor.Endpoint);
        }

        [Fact]
        public void ModuleMessageProcessor_CloseAsyncTest()
        {
            Core.IMessageConverter<IRoutingMessage> routingMessageConverter = new RoutingMessageConverter();
            var connectionManager = Mock.Of<IConnectionManager>();
            string moduleId = "device1/module1";
            string moduleEndpointAddress = "in1";

            var moduleEndpoint = new ModuleEndpoint($"{moduleId}/{moduleEndpointAddress}", moduleId, moduleEndpointAddress, connectionManager, routingMessageConverter);

            IProcessor moduleMessageProcessor = moduleEndpoint.CreateProcessor();
            Task result = moduleMessageProcessor.CloseAsync(CancellationToken.None);
            Assert.Equal(TaskEx.Done, result);
        }

        [Fact]
        public async Task ModuleMessageProcessor_ProcessAsyncTest()
        {
            Core.IMessageConverter<IRoutingMessage> routingMessageConverter = new RoutingMessageConverter();
            var deviceProxy = new Mock<IDeviceProxy>();
            deviceProxy.Setup(d => d.SendMessageAsync(It.IsAny<IMessage>(), It.IsAny<string>())).Returns(Task.CompletedTask);
            deviceProxy.Setup(d => d.IsActive).Returns(true);
            IReadOnlyDictionary<DeviceSubscription, bool> deviceSubscriptions = new ReadOnlyDictionary<DeviceSubscription, bool>(
                new Dictionary<DeviceSubscription, bool>
                {
                    [DeviceSubscription.ModuleMessages] = true
                });
            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.GetDeviceConnection(It.IsAny<string>())).Returns(Option.Some(deviceProxy.Object));
            connectionManager.Setup(c => c.GetSubscriptions(It.IsAny<string>())).Returns(Option.Some(deviceSubscriptions));
            var routingMessage = Mock.Of<IRoutingMessage>();
            string moduleId = "device1/module1";
            string moduleEndpointAddress = "in1";

            var moduleEndpoint = new ModuleEndpoint($"{moduleId}/{moduleEndpointAddress}", moduleId, moduleEndpointAddress, connectionManager.Object, routingMessageConverter);

            IProcessor moduleMessageProcessor = moduleEndpoint.CreateProcessor();
            ISinkResult<IRoutingMessage> sinkResult = await moduleMessageProcessor.ProcessAsync(routingMessage, CancellationToken.None);
            Assert.True(sinkResult.Succeeded.Contains(routingMessage));
        }

        [Fact]
        public async Task ModuleMessageProcessor_ProcessAsyncTest_InactiveDeviceProxy()
        {
            Core.IMessageConverter<IRoutingMessage> routingMessageConverter = new RoutingMessageConverter();
            var deviceProxy = new Mock<IDeviceProxy>();
            deviceProxy.Setup(d => d.SendMessageAsync(It.IsAny<IMessage>(), It.IsAny<string>())).Returns(Task.CompletedTask);
            deviceProxy.Setup(d => d.IsActive).Returns(false);
            var routingMessage = Mock.Of<IRoutingMessage>();
            string moduleId = "device1/module1";
            string moduleEndpointAddress = "in1";
            IReadOnlyDictionary<DeviceSubscription, bool> deviceSubscriptions = new ReadOnlyDictionary<DeviceSubscription, bool>(
                new Dictionary<DeviceSubscription, bool>
                {
                    [DeviceSubscription.ModuleMessages] = true
                });
            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.GetDeviceConnection(It.IsAny<string>())).Returns(Option.Some(deviceProxy.Object));
            connectionManager.Setup(c => c.GetSubscriptions(It.IsAny<string>())).Returns(Option.Some(deviceSubscriptions));
            var moduleEndpoint = new ModuleEndpoint($"{moduleId}/{moduleEndpointAddress}", moduleId, moduleEndpointAddress, connectionManager.Object, routingMessageConverter);

            IProcessor moduleMessageProcessor = moduleEndpoint.CreateProcessor();
            ISinkResult<IRoutingMessage> sinkResult = await moduleMessageProcessor.ProcessAsync(routingMessage, CancellationToken.None);
            Assert.True(sinkResult.Failed.Contains(routingMessage));
            Assert.Equal(FailureKind.None, sinkResult.SendFailureDetails.Map(x => x.FailureKind).GetOrElse(FailureKind.None));
        }

        [Fact]
        public async Task ModuleMessageProcessor_ProcessAsyncTest_NoDeviceProxy()
        {
            Core.IMessageConverter<IRoutingMessage> routingMessageConverter = new RoutingMessageConverter();
            var routingMessage = Mock.Of<IRoutingMessage>();
            string moduleId = "device1/module1";
            string moduleEndpointAddress = "in1";
            IReadOnlyDictionary<DeviceSubscription, bool> deviceSubscriptions = new ReadOnlyDictionary<DeviceSubscription, bool>(
                new Dictionary<DeviceSubscription, bool>
                {
                    [DeviceSubscription.ModuleMessages] = true
                });
            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.GetDeviceConnection(It.IsAny<string>())).Returns(Option.None<IDeviceProxy>());
            connectionManager.Setup(c => c.GetSubscriptions(It.IsAny<string>())).Returns(Option.Some(deviceSubscriptions));
            var moduleEndpoint = new ModuleEndpoint($"{moduleId}/{moduleEndpointAddress}", moduleId, moduleEndpointAddress, connectionManager.Object, routingMessageConverter);

            IProcessor moduleMessageProcessor = moduleEndpoint.CreateProcessor();
            ISinkResult<IRoutingMessage> sinkResult = await moduleMessageProcessor.ProcessAsync(routingMessage, CancellationToken.None);
            Assert.Equal(FailureKind.None, sinkResult.SendFailureDetails.Map(x => x.FailureKind).GetOrElse(FailureKind.None));
            Assert.True(sinkResult.Failed.Contains(routingMessage));
        }

        [Fact]
        public async Task ModuleMessageProcessor_ProcessAsyncTest_NoSubscription()
        {
            Core.IMessageConverter<IRoutingMessage> routingMessageConverter = new RoutingMessageConverter();
            var deviceProxy = new Mock<IDeviceProxy>();
            deviceProxy.Setup(d => d.SendMessageAsync(It.IsAny<IMessage>(), It.IsAny<string>())).Returns(Task.CompletedTask);
            deviceProxy.Setup(d => d.IsActive).Returns(true);
            var routingMessage = Mock.Of<IRoutingMessage>();
            string moduleId = "device1/module1";
            string moduleEndpointAddress = "in1";
            IReadOnlyDictionary<DeviceSubscription, bool> deviceSubscriptions = new ReadOnlyDictionary<DeviceSubscription, bool>(
                new Dictionary<DeviceSubscription, bool>
                {
                    [DeviceSubscription.DesiredPropertyUpdates] = true
                });
            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.GetDeviceConnection(It.IsAny<string>())).Returns(Option.Some(deviceProxy.Object));
            connectionManager.Setup(c => c.GetSubscriptions(It.IsAny<string>())).Returns(Option.Some(deviceSubscriptions));
            var moduleEndpoint = new ModuleEndpoint($"{moduleId}/{moduleEndpointAddress}", moduleId, moduleEndpointAddress, connectionManager.Object, routingMessageConverter);

            IProcessor moduleMessageProcessor = moduleEndpoint.CreateProcessor();
            ISinkResult<IRoutingMessage> sinkResult = await moduleMessageProcessor.ProcessAsync(routingMessage, CancellationToken.None);
            Assert.True(sinkResult.Failed.Contains(routingMessage));
            Assert.Equal(FailureKind.None, sinkResult.SendFailureDetails.Map(x => x.FailureKind).GetOrElse(FailureKind.None));
        }

        [Fact]
        public async Task ModuleMessageProcessor_ProcessAsyncTest_InactiveSubscription()
        {
            Core.IMessageConverter<IRoutingMessage> routingMessageConverter = new RoutingMessageConverter();
            var deviceProxy = new Mock<IDeviceProxy>();
            deviceProxy.Setup(d => d.SendMessageAsync(It.IsAny<IMessage>(), It.IsAny<string>())).Returns(Task.CompletedTask);
            deviceProxy.Setup(d => d.IsActive).Returns(true);
            var routingMessage = Mock.Of<IRoutingMessage>();
            string moduleId = "device1/module1";
            string moduleEndpointAddress = "in1";
            IReadOnlyDictionary<DeviceSubscription, bool> deviceSubscriptions = new ReadOnlyDictionary<DeviceSubscription, bool>(
                new Dictionary<DeviceSubscription, bool>
                {
                    [DeviceSubscription.ModuleMessages] = false
                });
            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.GetDeviceConnection(It.IsAny<string>())).Returns(Option.Some(deviceProxy.Object));
            connectionManager.Setup(c => c.GetSubscriptions(It.IsAny<string>())).Returns(Option.Some(deviceSubscriptions));
            var moduleEndpoint = new ModuleEndpoint($"{moduleId}/{moduleEndpointAddress}", moduleId, moduleEndpointAddress, connectionManager.Object, routingMessageConverter);

            IProcessor moduleMessageProcessor = moduleEndpoint.CreateProcessor();
            ISinkResult<IRoutingMessage> sinkResult = await moduleMessageProcessor.ProcessAsync(routingMessage, CancellationToken.None);
            Assert.True(sinkResult.Failed.Contains(routingMessage));
            Assert.Equal(FailureKind.None, sinkResult.SendFailureDetails.Map(x => x.FailureKind).GetOrElse(FailureKind.None));
        }

        [Theory]
        [MemberData(nameof(GetRetryableExceptionsTestData))]
        public async Task ModuleMessageProcessor_RetryableExceptionTest(Exception exception, bool isRetryable)
        {
            // Arrange
            Core.IMessageConverter<IRoutingMessage> routingMessageConverter = new RoutingMessageConverter();
            var deviceProxy = new Mock<IDeviceProxy>();
            deviceProxy.Setup(d => d.SendMessageAsync(It.IsAny<IMessage>(), It.IsAny<string>()))
                .ThrowsAsync(exception);
            deviceProxy.Setup(d => d.IsActive).Returns(true);
            IReadOnlyDictionary<DeviceSubscription, bool> deviceSubscriptions = new ReadOnlyDictionary<DeviceSubscription, bool>(
                new Dictionary<DeviceSubscription, bool>
                {
                    [DeviceSubscription.ModuleMessages] = true
                });
            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.GetDeviceConnection(It.IsAny<string>())).Returns(Option.Some(deviceProxy.Object));
            connectionManager.Setup(c => c.GetSubscriptions(It.IsAny<string>())).Returns(Option.Some(deviceSubscriptions));
            var routingMessage = Mock.Of<IRoutingMessage>();
            string moduleId = "device1/module1";
            string moduleEndpointAddress = "in1";

            var moduleEndpoint = new ModuleEndpoint($"{moduleId}/{moduleEndpointAddress}", moduleId, moduleEndpointAddress, connectionManager.Object, routingMessageConverter);

            // Act
            IProcessor moduleMessageProcessor = moduleEndpoint.CreateProcessor();
            ISinkResult<IRoutingMessage> result = await moduleMessageProcessor.ProcessAsync(routingMessage, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            if (isRetryable)
            {
                Assert.Equal(1, result.Failed.Count);
                Assert.Equal(0, result.Succeeded.Count);
                Assert.Equal(0, result.InvalidDetailsList.Count);
                Assert.Equal(routingMessage, result.Failed.First());
            }
            else
            {
                Assert.Equal(1, result.InvalidDetailsList.Count);
                Assert.Equal(0, result.Succeeded.Count);
                Assert.Equal(0, result.Failed.Count);
                Assert.Equal(routingMessage, result.InvalidDetailsList.First().Item);
                Assert.Equal(FailureKind.InvalidInput, result.InvalidDetailsList.First().FailureKind);
            }
        }

        public static IEnumerable<object[]> GetRetryableExceptionsTestData()
        {
            yield return new object[] { new TimeoutException("Dummy"), true };

            yield return new object[] { new IOException("Dummy"), true };

            yield return new object[] { new EdgeHubIOException("Dummy"), true };

            yield return new object[] { new DummyEdgeHubIOException("Dummy"), true };

            yield return new object[] { new ArgumentException("Dummy"), false };

            yield return new object[] { new ArgumentNullException("dummy"), false };
        }

        public class DummyEdgeHubIOException : EdgeHubIOException
        {
            public DummyEdgeHubIOException(string message)
                : base(message)
            {
            }
        }
    }
}
