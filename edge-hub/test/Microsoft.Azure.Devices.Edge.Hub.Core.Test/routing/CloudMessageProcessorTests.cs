// Copyright (c) Microsoft. All rights reserved.
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
                .Callback<IMessage>(
                    msg =>
                    {
                        if (msg.Properties.ContainsKey("Delay"))
                        {
                            Task.Delay(TimeSpan.FromSeconds(10)).Wait();
                        }
                    })
                .Returns(Task.CompletedTask);
            cloudProxyMock.SetupGet(p => p.IsActive).Returns(true);

            string device1Id = "device1";
            string device2Id = "device2";

            byte[] messageBody = Encoding.UTF8.GetBytes("Message body");
            var properties = new Dictionary<string, string>()
            {
                { "Prop1", "Val1" },
                { "Prop2", "Val2" }
            };

            var device1SystemProperties = new Dictionary<string, string>
            {
                { SystemProperties.DeviceId, device1Id }
            };

            var device2SystemProperties = new Dictionary<string, string>
            {
                { SystemProperties.DeviceId, device2Id }
            };

            var cancelProperties = new Dictionary<string, string>()
            {
                { "Delay", "true" },
                { "Prop2", "Val2" }
            };

            var message1 = new RoutingMessage(TelemetryMessageSource.Instance, messageBody, properties, device1SystemProperties);
            var message2 = new RoutingMessage(TelemetryMessageSource.Instance, messageBody, properties, device2SystemProperties);
            var message3 = new RoutingMessage(TelemetryMessageSource.Instance, messageBody, cancelProperties, device1SystemProperties);

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
            Assert.Equal(1, resultBatch.Succeeded.Count);
            Assert.Equal(1, resultBatch.Failed.Count);
            Assert.Empty(resultBatch.InvalidDetailsList);
            Assert.True(resultBatch.SendFailureDetails.HasValue);

            ISinkResult<IRoutingMessage> resultBatchCancelled = await cloudMessageProcessor.ProcessAsync(new[] { message1, message2 }, new CancellationToken(true));
            Assert.NotNull(resultBatchCancelled);
            Assert.Empty(resultBatchCancelled.Succeeded);
            Assert.NotEmpty(resultBatchCancelled.Failed);
            Assert.Empty(resultBatchCancelled.InvalidDetailsList);
            Assert.True(resultBatchCancelled.SendFailureDetails.HasValue);

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            ISinkResult<IRoutingMessage> resultBatchCancelled2 = await cloudMessageProcessor.ProcessAsync(new[] { message1, message3, message1 }, cts.Token);
            Assert.NotNull(resultBatchCancelled2);
            Assert.Equal(2, resultBatchCancelled2.Succeeded.Count);
            Assert.Equal(1, resultBatchCancelled2.Failed.Count);
            Assert.Empty(resultBatchCancelled2.InvalidDetailsList);
            Assert.True(resultBatchCancelled2.SendFailureDetails.HasValue);
        }

        [Fact]
        [Unit]
        public async Task ProcessAsync_SendThrows_Test()
        {
            Core.IMessageConverter<IRoutingMessage> routingMessageConverter = new RoutingMessageConverter();
            string cloudEndpointId = Guid.NewGuid().ToString();

            var cloudProxyMock = new Mock<ICloudProxy>();
            cloudProxyMock.Setup(c => c.SendMessageAsync(It.IsAny<IMessage>()))
                .Throws<TimeoutException>();
            cloudProxyMock.SetupGet(p => p.IsActive).Returns(true);

            string device1Id = "device1";
            string device2Id = "device2";

            byte[] messageBody = Encoding.UTF8.GetBytes("Message body");
            var properties = new Dictionary<string, string>()
            {
                { "Prop1", "Val1" },
                { "Prop2", "Val2" }
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
                return Task.FromResult(Option.Some(cloudProxyMock.Object));
            }

            var cloudEndpoint = new CloudEndpoint(cloudEndpointId, GetCloudProxy, routingMessageConverter);
            IProcessor cloudMessageProcessor = cloudEndpoint.CreateProcessor();

            ISinkResult<IRoutingMessage> result = await cloudMessageProcessor.ProcessAsync(new[] { message1, message2 }, CancellationToken.None);
            Assert.NotNull(result);
            Assert.Empty(result.Succeeded);
            Assert.Equal(2, result.Failed.Count);
            Assert.Empty(result.InvalidDetailsList);
            Assert.True(result.SendFailureDetails.HasValue);

            // throw non-retryable
            cloudProxyMock.Setup(c => c.SendMessageAsync(It.IsAny<IMessage>()))
                .Throws<Exception>();

            ISinkResult<IRoutingMessage> result1 = await cloudMessageProcessor.ProcessAsync(new[] { message1, message2 }, CancellationToken.None);
            Assert.NotNull(result1);
            Assert.Empty(result1.Succeeded);
            Assert.Empty(result1.Failed);
            Assert.Equal(2, result1.InvalidDetailsList.Count);
            Assert.True(result1.SendFailureDetails.HasValue);
        }
    }
}
