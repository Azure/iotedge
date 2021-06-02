// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Routing;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Moq;
    using Xunit;
    using IMessage = Microsoft.Azure.Devices.Edge.Hub.Core.IMessage;
    using IRoutingMessage = Microsoft.Azure.Devices.Routing.Core.IMessage;
    using RoutingMessage = Microsoft.Azure.Devices.Routing.Core.Message;

    [Unit]
    public class CloudEndpointTest
    {
        [Fact]
        public void CloudEndpoint_MembersTest()
        {
            Core.IMessageConverter<IRoutingMessage> routingMessageConverter = new RoutingMessageConverter();
            var cloudProxy = Mock.Of<ICloudProxy>();
            string cloudEndpointId = Guid.NewGuid().ToString();

            var cloudEndpoint = new CloudEndpoint(cloudEndpointId, id => Task.FromResult(Try.Success(cloudProxy)), routingMessageConverter, true);

            Assert.Equal(cloudEndpointId, cloudEndpoint.Id);
            Assert.Equal("CloudEndpoint", cloudEndpoint.Type);
            Assert.Equal(cloudEndpointId, cloudEndpoint.Name);
            Assert.Equal(string.Empty, cloudEndpoint.IotHubName);

            IProcessor processor = cloudEndpoint.CreateProcessor();
            Assert.NotNull(processor);
            Assert.Equal(cloudEndpoint, processor.Endpoint);
        }

        [Fact]
        public void CloudEndpoint_CreateProcessorTest()
        {
            Core.IMessageConverter<IRoutingMessage> routingMessageConverter = new RoutingMessageConverter();
            var cloudProxy = Mock.Of<ICloudProxy>();
            string cloudEndpointId = Guid.NewGuid().ToString();

            var cloudEndpoint = new CloudEndpoint(cloudEndpointId, id => Task.FromResult(Try.Success(cloudProxy)), routingMessageConverter, true);

            IProcessor processor = cloudEndpoint.CreateProcessor();
            Assert.NotNull(processor);
            Assert.Equal(cloudEndpoint, processor.Endpoint);
        }

        [Fact]
        public void CloudMessageProcessor_CloseAsyncTest()
        {
            Core.IMessageConverter<IRoutingMessage> routingMessageConverter = new RoutingMessageConverter();
            var cloudProxy = Mock.Of<ICloudProxy>();
            string cloudEndpointId = Guid.NewGuid().ToString();

            var cloudEndpoint = new CloudEndpoint(cloudEndpointId, id => Task.FromResult(Try.Success(cloudProxy)), routingMessageConverter, true);

            IProcessor moduleMessageProcessor = cloudEndpoint.CreateProcessor();
            Task result = moduleMessageProcessor.CloseAsync(CancellationToken.None);
            Assert.Equal(Task.CompletedTask, result);
        }

        [Fact]
        public async Task CloudMessageProcessor_ProcessAsyncTest()
        {
            Core.IMessageConverter<IRoutingMessage> routingMessageConverter = new RoutingMessageConverter();
            var routingMessage = Mock.Of<IRoutingMessage>();
            var cloudProxy = Mock.Of<ICloudProxy>(c => c.IsActive);
            string cloudEndpointId = Guid.NewGuid().ToString();

            Mock.Get(routingMessage).Setup(rm => rm.SystemProperties).Returns(new Dictionary<string, string> { { "connectionDeviceId", "myConnectionDeviceId" } });
            Mock.Get(cloudProxy).Setup(cp => cp.SendMessageAsync(It.IsAny<IMessage>())).Returns(Task.FromResult(true));
            var cloudEndpoint = new CloudEndpoint(cloudEndpointId, id => Task.FromResult(Try.Success(cloudProxy)), routingMessageConverter, true);

            IProcessor cloudMessageProcessor = cloudEndpoint.CreateProcessor();
            ISinkResult<IRoutingMessage> sinkResult = await cloudMessageProcessor.ProcessAsync(routingMessage, CancellationToken.None);
            Assert.True(sinkResult.Succeeded.Contains(routingMessage));
        }

        [Fact]
        public async Task Events_CloudProxyNotFoundTest()
        {
            Core.IMessageConverter<IRoutingMessage> routingMessageConverter = new RoutingMessageConverter();
            var routingMessage = Mock.Of<IRoutingMessage>();
            string cloudEndpointId = Guid.NewGuid().ToString();

            Mock.Get(routingMessage).Setup(rm => rm.SystemProperties).Returns(new Dictionary<string, string> { { "connectionDeviceId", "myConnectionDeviceId" } });
            var cloudEndpoint = new CloudEndpoint(cloudEndpointId, id => Task.FromResult(Try<ICloudProxy>.Failure(new EdgeHubConnectionException("EdgeHubConnectionException"))), routingMessageConverter, trackDeviceState: false);

            IProcessor cloudMessageProcessor = cloudEndpoint.CreateProcessor();
            ISinkResult<IRoutingMessage> sinkResult = await cloudMessageProcessor.ProcessAsync(routingMessage, CancellationToken.None);
            Assert.Equal(FailureKind.Transient, sinkResult.SendFailureDetails.OrDefault().FailureKind);
            Assert.Equal(typeof(EdgeHubConnectionException), sinkResult.SendFailureDetails.OrDefault().RawException.GetType());
        }

        [Fact]
        public async Task Events_CloudProxyGetterReturnException_ShouldHandleAsNoConnection()
        {
            Core.IMessageConverter<IRoutingMessage> routingMessageConverter = new RoutingMessageConverter();
            var routingMessage = Mock.Of<IRoutingMessage>();
            string cloudEndpointId = Guid.NewGuid().ToString();

            Mock.Get(routingMessage).Setup(rm => rm.SystemProperties).Returns(new Dictionary<string, string> { { "connectionDeviceId", "myConnectionDeviceId" } });
            var cloudEndpoint = new CloudEndpoint(cloudEndpointId, id => Task.FromResult(Try<ICloudProxy>.Failure(new Exception("EdgeHubConnectionException"))), routingMessageConverter, trackDeviceState: false);

            IProcessor cloudMessageProcessor = cloudEndpoint.CreateProcessor();
            ISinkResult<IRoutingMessage> sinkResult = await cloudMessageProcessor.ProcessAsync(routingMessage, CancellationToken.None);
            Assert.Equal(FailureKind.Transient, sinkResult.SendFailureDetails.OrDefault().FailureKind);
            Assert.Equal(typeof(EdgeHubConnectionException), sinkResult.SendFailureDetails.OrDefault().RawException.GetType());
        }

        [Fact]
        public async Task Events_CloudProxyNotFoundWithDeviceInvalidStateExceptionTest()
        {
            Core.IMessageConverter<IRoutingMessage> routingMessageConverter = new RoutingMessageConverter();
            var routingMessage = Mock.Of<IRoutingMessage>();
            string cloudEndpointId = Guid.NewGuid().ToString();

            Mock.Get(routingMessage).Setup(rm => rm.SystemProperties).Returns(new Dictionary<string, string> { { "connectionDeviceId", "myConnectionDeviceId" } });
            var cloudEndpoint = new CloudEndpoint(cloudEndpointId, id => Task.FromResult(Try<ICloudProxy>.Failure(new DeviceInvalidStateException("Device removed from scope"))), routingMessageConverter, trackDeviceState: false);

            IProcessor cloudMessageProcessor = cloudEndpoint.CreateProcessor();
            ISinkResult<IRoutingMessage> sinkResult = await cloudMessageProcessor.ProcessAsync(routingMessage, CancellationToken.None);
            Assert.Equal(FailureKind.Transient, sinkResult.SendFailureDetails.OrDefault().FailureKind);
            Assert.Equal(typeof(EdgeHubConnectionException), sinkResult.SendFailureDetails.OrDefault().RawException.GetType());
        }

        [Fact]
        public async Task Events_CloudProxyNotFoundWithDeviceInvalidStateException_DropMessage_Test()
        {
            Core.IMessageConverter<IRoutingMessage> routingMessageConverter = new RoutingMessageConverter();
            var routingMessage = Mock.Of<IRoutingMessage>();
            string cloudEndpointId = Guid.NewGuid().ToString();

            Mock.Get(routingMessage).Setup(rm => rm.SystemProperties).Returns(new Dictionary<string, string> { { "connectionDeviceId", "myConnectionDeviceId" } });
            var cloudEndpoint = new CloudEndpoint(cloudEndpointId, id => Task.FromResult(Try<ICloudProxy>.Failure(new DeviceInvalidStateException("Device removed from scope"))), routingMessageConverter, trackDeviceState: true);

            IProcessor cloudMessageProcessor = cloudEndpoint.CreateProcessor();
            ISinkResult<IRoutingMessage> sinkResult = await cloudMessageProcessor.ProcessAsync(routingMessage, CancellationToken.None);
            Assert.Equal(FailureKind.InvalidInput, sinkResult.SendFailureDetails.OrDefault().FailureKind);
            Assert.Equal(typeof(DeviceInvalidStateException), sinkResult.SendFailureDetails.OrDefault().RawException.GetType());
        }

        [Fact]
        public async Task Events_DeviceIdNotFoundTest()
        {
            Core.IMessageConverter<IRoutingMessage> routingMessageConverter = new RoutingMessageConverter();
            var routingMessage = Mock.Of<IRoutingMessage>();
            string cloudEndpointId = Guid.NewGuid().ToString();

            Mock.Get(routingMessage).Setup(rm => rm.SystemProperties).Returns(new Dictionary<string, string> { { "messageId", "myConnectionDeviceId" } });
            var cloudProxy = Mock.Of<ICloudProxy>();
            var cloudEndpoint = new CloudEndpoint(cloudEndpointId, id => Task.FromResult(Try.Success(cloudProxy)), routingMessageConverter, trackDeviceState: false);

            IProcessor cloudMessageProcessor = cloudEndpoint.CreateProcessor();
            ISinkResult<IRoutingMessage> sinkResult = await cloudMessageProcessor.ProcessAsync(routingMessage, CancellationToken.None);
            Assert.Equal(FailureKind.InvalidInput, sinkResult.SendFailureDetails.OrDefault().FailureKind);
            Assert.Equal(typeof(InvalidOperationException), sinkResult.SendFailureDetails.OrDefault().RawException.GetType());
            Assert.Equal(1, sinkResult.InvalidDetailsList.Count);
            Assert.Equal(0, sinkResult.Failed.Count);
            Assert.Equal(0, sinkResult.Succeeded.Count);
        }

        [Theory]
        [MemberData(nameof(GetRetryableExceptionsTestData))]
        public async Task RetryableExceptionsTest(Exception exception, bool isRetryable)
        {
            // Arrange
            string id = "d1";
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(c => c.SendMessageAsync(It.IsAny<IMessage>()))
                .ThrowsAsync(exception);
            var cloudEndpoint = new CloudEndpoint(Guid.NewGuid().ToString(), _ => Task.FromResult(Try.Success(cloudProxy.Object)), new RoutingMessageConverter(), false);
            IProcessor processor = cloudEndpoint.CreateProcessor();
            var message = new RoutingMessage(TelemetryMessageSource.Instance, new byte[0], ImmutableDictionary<string, string>.Empty, new Dictionary<string, string>
            {
                [Core.SystemProperties.ConnectionDeviceId] = id
            });

            // Act
            ISinkResult<IRoutingMessage> result = await processor.ProcessAsync(message, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            if (isRetryable)
            {
                Assert.Equal(1, result.Failed.Count);
                Assert.Equal(0, result.Succeeded.Count);
                Assert.Equal(0, result.InvalidDetailsList.Count);
                Assert.Equal(message, result.Failed.First());
            }
            else
            {
                Assert.Equal(1, result.InvalidDetailsList.Count);
                Assert.Equal(0, result.Succeeded.Count);
                Assert.Equal(0, result.Failed.Count);
                Assert.Equal(message, result.InvalidDetailsList.First().Item);
                Assert.Equal(FailureKind.InvalidInput, result.InvalidDetailsList.First().FailureKind);
            }
        }

        [Theory]
        [MemberData(nameof(GetRetryableExceptionsTestData))]
        public async Task RetryableExceptionsTestWhileTrackDeviceStateEnabled(Exception exception, bool isRetryable)
        {
            // Arrange
            string id = "d1";
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(c => c.SendMessageAsync(It.IsAny<IMessage>()))
                .ThrowsAsync(exception);
            var cloudEndpoint = new CloudEndpoint(Guid.NewGuid().ToString(), _ => Task.FromResult(Try.Success(cloudProxy.Object)), new RoutingMessageConverter(), trackDeviceState: true);
            IProcessor processor = cloudEndpoint.CreateProcessor();
            var message = new RoutingMessage(TelemetryMessageSource.Instance, new byte[0], ImmutableDictionary<string, string>.Empty, new Dictionary<string, string>
            {
                [Core.SystemProperties.ConnectionDeviceId] = id
            });

            // Act
            ISinkResult<IRoutingMessage> result = await processor.ProcessAsync(message, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            if (!isRetryable || exception is DeviceInvalidStateException)
            {
                Assert.Equal(1, result.InvalidDetailsList.Count);
                Assert.Equal(0, result.Succeeded.Count);
                Assert.Equal(0, result.Failed.Count);
                Assert.Equal(message, result.InvalidDetailsList.First().Item);
                Assert.Equal(FailureKind.InvalidInput, result.InvalidDetailsList.First().FailureKind);
            }
            else
            {
                Assert.Equal(1, result.Failed.Count);
                Assert.Equal(0, result.Succeeded.Count);
                Assert.Equal(0, result.InvalidDetailsList.Count);
                Assert.Equal(message, result.Failed.First());
            }
        }

        public static IEnumerable<object[]> GetRetryableExceptionsTestData()
        {
            yield return new object[] { new IotHubException("Dummy", true), true };

            yield return new object[] { new IotHubException("Dummy", false), true };

            yield return new object[] { new IOException("Dummy"), true };

            yield return new object[] { new TimeoutException("Dummy"), true };

            yield return new object[] { new UnauthorizedException("Dummy"), true };

            yield return new object[] { new DeviceInvalidStateException("Dummy"), true };

            yield return new object[] { new DeviceMaximumQueueDepthExceededException("Dummy"), true };

            yield return new object[] { new IotHubSuspendedException("Dummy"), true };

            yield return new object[] { new ArgumentException("Dummy"), false };

            yield return new object[] { new ArgumentNullException("dummy"), false };
        }
    }
}
