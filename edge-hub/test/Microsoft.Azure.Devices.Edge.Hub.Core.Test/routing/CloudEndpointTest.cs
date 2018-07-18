// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using App.Metrics;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Routing;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core;
    using Moq;
    using Xunit;
    using IMessage = Microsoft.Azure.Devices.Edge.Hub.Core.IMessage;
    using IRoutingMessage = Microsoft.Azure.Devices.Routing.Core.IMessage;

    [Unit]
    public class CloudEndpointTest
    {
        [Fact]
        public void CloudEndpoint_MembersTest()
        {
            Core.IMessageConverter<IRoutingMessage> routingMessageConverter = new RoutingMessageConverter();
            var cloudProxy = Mock.Of<ICloudProxy>();
            string cloudEndpointId = Guid.NewGuid().ToString();

            var cloudEndpoint = new CloudEndpoint(cloudEndpointId, (id) => Option.Some(cloudProxy), routingMessageConverter);

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

            var cloudEndpoint = new CloudEndpoint(cloudEndpointId, (id) => Option.Some(cloudProxy), routingMessageConverter);

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

            var cloudEndpoint = new CloudEndpoint(cloudEndpointId, id => Option.Some(cloudProxy), routingMessageConverter);

            IProcessor moduleMessageProcessor = cloudEndpoint.CreateProcessor();
            Task result = moduleMessageProcessor.CloseAsync(CancellationToken.None);
            Assert.Equal(TaskEx.Done, result);
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
            var cloudEndpoint = new CloudEndpoint(cloudEndpointId, id => Option.Some(cloudProxy), routingMessageConverter);

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
            var cloudEndpoint = new CloudEndpoint(cloudEndpointId, (id) => Option.None<ICloudProxy>(), routingMessageConverter);

            IProcessor cloudMessageProcessor = cloudEndpoint.CreateProcessor();
            ISinkResult<IRoutingMessage> sinkResult = await cloudMessageProcessor.ProcessAsync(routingMessage, CancellationToken.None);
            Assert.Equal(FailureKind.None, sinkResult.SendFailureDetails.OrDefault().FailureKind);
            Assert.Equal(typeof(EdgeHubConnectionException), sinkResult.SendFailureDetails.OrDefault().RawException.GetType());
        }

        [Fact]
        public async Task Events_DeviceIdNotFoundTest()
        {
            Core.IMessageConverter<IRoutingMessage> routingMessageConverter = new RoutingMessageConverter();
            var routingMessage = Mock.Of<IRoutingMessage>();
            string cloudEndpointId = Guid.NewGuid().ToString();

            Mock.Get(routingMessage).Setup(rm => rm.SystemProperties).Returns(new Dictionary<string, string> { { "messageId", "myConnectionDeviceId" } });
            var cloudEndpoint = new CloudEndpoint(cloudEndpointId, (id) => Option.None<ICloudProxy>(), routingMessageConverter);

            IProcessor cloudMessageProcessor = cloudEndpoint.CreateProcessor();
            ISinkResult<IRoutingMessage> sinkResult = await cloudMessageProcessor.ProcessAsync(routingMessage, CancellationToken.None);
            Assert.Equal(FailureKind.None, sinkResult.SendFailureDetails.OrDefault().FailureKind);
            Assert.Equal(typeof(EdgeHubConnectionException), sinkResult.SendFailureDetails.OrDefault().RawException.GetType());
        }
    }
}
