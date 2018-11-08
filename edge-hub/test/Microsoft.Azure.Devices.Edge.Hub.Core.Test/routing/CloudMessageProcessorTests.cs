// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

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

    public class CloudMessageProcessorTests
    {
        [Fact]
        [Unit]
        public void BasicTest()
        {
            Core.IMessageConverter<IRoutingMessage> routingMessageConverter = new RoutingMessageConverter();
            var cloudProxyMock = new Mock<ICloudProxy>();
            string cloudEndpointId = Guid.NewGuid().ToString();

            Task<Option<ICloudProxy>> GetCloudProxy(string id) => Task.FromResult(Option.Some(cloudProxyMock.Object));

            var cloudEndpoint = new CloudEndpoint(cloudEndpointId, GetCloudProxy, routingMessageConverter);
            IProcessor cloudMessageProcessor = cloudEndpoint.CreateProcessor();

            Assert.Equal(cloudEndpoint, cloudMessageProcessor.Endpoint);
            Assert.False(cloudMessageProcessor.ErrorDetectionStrategy.IsTransient(new Exception()));
        }

        [Fact]
        [Unit]
        public async Task ProcessAsyncTest()
        {
            Core.IMessageConverter<IRoutingMessage> routingMessageConverter = new RoutingMessageConverter();
            string cloudEndpointId = Guid.NewGuid().ToString();

            var cloudProxyMock = new Mock<ICloudProxy>();
            cloudProxyMock.Setup(c => c.SendMessageAsync(It.IsAny<IMessage>()))
                .Returns(Task.CompletedTask);
            cloudProxyMock.SetupGet(p => p.IsActive).Returns(true);

            string device1Id = "device1";
            string device2Id = "device2";

            byte[] messageBody = Encoding.UTF8.GetBytes("Message body");
            var properties = new Dictionary<string, string>()
            {
                { "Prop1", "Val1" },
                { "Prop2", "Val2" },
            };

            var device1SystemProperties = new Dictionary<string, string>
            {
                { SystemProperties.DeviceId, device1Id }
            };

            var device2SystemProperties = new Dictionary<string, string>
            {
                { SystemProperties.DeviceId, device2Id }
            };

            var message1 = new RoutingMessage(TelemetryMessageSource.Instance, messageBody, properties, device1SystemProperties);
            var message2 = new RoutingMessage(TelemetryMessageSource.Instance, messageBody, properties, device2SystemProperties);

            Task<Option<ICloudProxy>> GetCloudProxy(string id)
            {
                return Task.FromResult(
                    id.Equals(device1Id)
                        ? Option.Some(cloudProxyMock.Object)
                        : Option.None<ICloudProxy>());
            }

            var cloudEndpoint = new CloudEndpoint(cloudEndpointId, GetCloudProxy, routingMessageConverter);
            IProcessor cloudMessageProcessor = cloudEndpoint.CreateProcessor();

            ISinkResult<IRoutingMessage> result1 = await cloudMessageProcessor.ProcessAsync(message1, CancellationToken.None);
            Assert.NotNull(result1);
            Assert.NotEmpty(result1.Succeeded);
            Assert.Empty(result1.Failed);
            Assert.Empty(result1.InvalidDetailsList);
            Assert.False(result1.SendFailureDetails.HasValue);

            ISinkResult<IRoutingMessage> result2 = await cloudMessageProcessor.ProcessAsync(message2, CancellationToken.None);
            Assert.NotNull(result2);
            Assert.Empty(result2.InvalidDetailsList);
            Assert.NotEmpty(result2.Failed);
            Assert.Empty(result2.Succeeded);
            Assert.True(result2.SendFailureDetails.HasValue);

            ISinkResult<IRoutingMessage> resultBatch = await cloudMessageProcessor.ProcessAsync(new[] { message1, message2 }, CancellationToken.None);
            Assert.NotNull(resultBatch);
            Assert.NotEmpty(resultBatch.Succeeded);
            Assert.NotEmpty(resultBatch.Failed);
            Assert.Empty(resultBatch.InvalidDetailsList);
            Assert.True(resultBatch.SendFailureDetails.HasValue);
        }
    }
}
