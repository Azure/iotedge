// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
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

    public class ModuleMessageProcessorTest
    {
        [Fact]
        [Unit]
        public void BasicTest()
        {
            Core.IMessageConverter<IRoutingMessage> routingMessageConverter = new RoutingMessageConverter();
            var connectionManager = new Mock<IConnectionManager>();
            string modId = "device1/module1";
            string moduleEndpointId = "in1";

            var moduleEndpoint = new ModuleEndpoint($"{modId}/{moduleEndpointId}", modId, moduleEndpointId, connectionManager.Object, routingMessageConverter);
            IProcessor moduleMessageProcessor = moduleEndpoint.CreateProcessor();

            Assert.Equal(moduleEndpoint, moduleMessageProcessor.Endpoint);
            Assert.False(moduleMessageProcessor.ErrorDetectionStrategy.IsTransient(new Exception()));
        }

        [Fact]
        [Unit]
        public async Task ProcessAsyncTest()
        {
            var throwingDeviceProxy = ThrowingDeviceProxyBuilder
                             .Create()
                             .WithSuccess()
                             .Build();

            var messageProcessor = this.CreateMessageProcessor(throwingDeviceProxy);

            var message1 = GetMessage();
            var message2 = GetMessage();

            ISinkResult<IRoutingMessage> result = await messageProcessor.ProcessAsync(message1, CancellationToken.None);
            Assert.NotNull(result);
            Assert.NotEmpty(result.Succeeded);
            Assert.Empty(result.Failed);
            Assert.Empty(result.InvalidDetailsList);
            Assert.False(result.SendFailureDetails.HasValue);

            ISinkResult<IRoutingMessage> resultBatch = await messageProcessor.ProcessAsync(new[] { message1, message2 }, CancellationToken.None);
            Assert.NotNull(resultBatch);
            Assert.NotEmpty(resultBatch.Succeeded);
            Assert.Empty(resultBatch.Failed);
            Assert.Empty(resultBatch.InvalidDetailsList);
            Assert.False(resultBatch.SendFailureDetails.HasValue);
        }

        [Fact]
        [Unit]
        public async Task ProcessAsync_NoConnection_ShoulFailTest()
        {
            var throwingDeviceProxy = ThrowingDeviceProxyBuilder
                             .Create()
                             .WithActiveStatus(false)
                             .Build();

            var messageProcessor = this.CreateMessageProcessor(throwingDeviceProxy);

            var message1 = GetMessage();
            var message2 = GetMessage();

            ISinkResult<IRoutingMessage> result = await messageProcessor.ProcessAsync(message1, CancellationToken.None);
            Assert.NotNull(result);
            Assert.Empty(result.Succeeded);
            Assert.Equal(1, result.Failed.Count);
            Assert.Empty(result.InvalidDetailsList);
            Assert.True(result.SendFailureDetails.HasValue);

            ISinkResult<IRoutingMessage> resultBatch = await messageProcessor.ProcessAsync(new[] { message1, message2 }, CancellationToken.None);
            Assert.NotNull(resultBatch);
            Assert.Empty(resultBatch.Succeeded);
            Assert.Equal(2, resultBatch.Failed.Count);
            Assert.Empty(resultBatch.InvalidDetailsList);
            Assert.True(resultBatch.SendFailureDetails.HasValue);
        }

        [Fact]
        [Unit]
        public async Task ProcessAsync_SendThrowsRetryable_Test()
        {
            var throwingDeviceProxy = ThrowingDeviceProxyBuilder
                                        .Create()
                                        .WithThrow<TimeoutException>()
                                        .Build();

            var messageProcessor = this.CreateMessageProcessor(throwingDeviceProxy);

            var message1 = GetMessage();
            var message2 = GetMessage();

            ISinkResult<IRoutingMessage> result = await messageProcessor.ProcessAsync(message1, CancellationToken.None);
            Assert.NotNull(result);
            Assert.Empty(result.Succeeded);
            Assert.Equal(1, result.Failed.Count);
            Assert.Empty(result.InvalidDetailsList);
            Assert.True(result.SendFailureDetails.HasValue);

            ISinkResult<IRoutingMessage> resultBatch = await messageProcessor.ProcessAsync(new[] { message1, message2 }, CancellationToken.None);
            Assert.NotNull(resultBatch);
            Assert.Empty(resultBatch.Succeeded);
            Assert.Equal(2, resultBatch.Failed.Count);
            Assert.Empty(resultBatch.InvalidDetailsList);
            Assert.True(resultBatch.SendFailureDetails.HasValue);
        }

        [Fact]
        [Unit]
        public async Task ProcessAsync_SendThrowsNonRetryable_Test()
        {
            var throwingDeviceProxy = ThrowingDeviceProxyBuilder
                                        .Create()
                                        .WithThrow<Exception>()
                                        .Build();

            var messageProcessor = this.CreateMessageProcessor(throwingDeviceProxy);

            var message1 = GetMessage();
            var message2 = GetMessage();

            ISinkResult<IRoutingMessage> result = await messageProcessor.ProcessAsync(message1, CancellationToken.None);
            Assert.NotNull(result);
            Assert.Empty(result.Succeeded);
            Assert.Empty(result.Failed);
            Assert.Equal(1, result.InvalidDetailsList.Count);
            Assert.False(result.SendFailureDetails.HasValue);

            ISinkResult<IRoutingMessage> resultBatch = await messageProcessor.ProcessAsync(new[] { message1, message2 }, CancellationToken.None);
            Assert.NotNull(resultBatch);
            Assert.Empty(resultBatch.Succeeded);
            Assert.Empty(resultBatch.Failed);
            Assert.Equal(2, resultBatch.InvalidDetailsList.Count);
            Assert.False(resultBatch.SendFailureDetails.HasValue);
        }

        [Fact]
        [Unit]
        public async Task ProcessAsync_FastFailMessages_AfterOneFailedWithRetriable()
        {
            var throwingDeviceProxy = ThrowingDeviceProxyBuilder
                                        .Create()
                                        .WithSuccess()
                                        .WithThrow<EdgeHubIOException>()
                                        .WithSuccess()
                                        .Build();

            var messageProcessor = this.CreateMessageProcessor(throwingDeviceProxy);
            var messages = GetMessages(5);

            var result = await messageProcessor.ProcessAsync(messages, CancellationToken.None);

            Assert.False(result.IsSuccessful);
            Assert.Equal(1, result.Succeeded.Count);
            Assert.Equal(4, result.Failed.Count);
            Assert.Equal(FailureKind.Transient, result.SendFailureDetails.Expect(() => new Exception()).FailureKind);
        }

        [Fact]
        [Unit]
        public async Task ProcessAsync_DontFastFailMessages_AfterOneFailedWithNonRetriable()
        {
            var throwingDeviceProxy = ThrowingDeviceProxyBuilder
                                        .Create()
                                        .WithSuccess()
                                        .WithThrow<Exception>()
                                        .WithSuccess()
                                        .Build();

            var messageProcessor = this.CreateMessageProcessor(throwingDeviceProxy);
            var messages = GetMessages(5);

            var result = await messageProcessor.ProcessAsync(messages, CancellationToken.None);

            Assert.True(result.IsSuccessful); // non-retriable is not reported as failure
            Assert.Equal(4, result.Succeeded.Count);
            Assert.Equal(1, result.InvalidDetailsList.Count);
            Assert.Equal(0, result.Failed.Count);
            // SendFailureDetails is not reported for Non-Retriable - this is not consistent with cloud proxy, skip the assert for now
        }

        private IProcessor CreateMessageProcessor(IDeviceProxy deviceProxy)
        {
            IReadOnlyDictionary<DeviceSubscription, bool> deviceSubscriptions =
                new Dictionary<DeviceSubscription, bool>()
                {
                    [DeviceSubscription.ModuleMessages] = true
                };

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(call => call.GetDeviceConnection(It.IsAny<string>())).Returns(Option.Some(deviceProxy));
            connectionManager.Setup(c => c.GetSubscriptions(It.IsAny<string>())).Returns(Option.Some(deviceSubscriptions));

            var moduleEndpoint = new ModuleEndpoint("device1/module1", "module1", "in1", connectionManager.Object, new RoutingMessageConverter());
            return moduleEndpoint.CreateProcessor();
        }

        static IList<IRoutingMessage> GetMessages(int count)
        {
            var messages = new List<IRoutingMessage>();
            for (int i = 0; i < count; i++)
            {
                messages.Add(GetMessage());
            }

            return messages;
        }

        static IRoutingMessage GetMessage()
        {
            byte[] messageBody = Encoding.UTF8.GetBytes("Message body");
            var properties = new Dictionary<string, string>()
            {
                { "Prop1", "Val1" },
                { "Prop2", "Val2" }
            };

            var systemProperties = new Dictionary<string, string>
            {
                { SystemProperties.DeviceId, "device1/module1" }
            };

            var message = new RoutingMessage(TelemetryMessageSource.Instance, messageBody, properties, systemProperties);
            return message;
        }
    }

    internal class ThrowingDeviceProxyBuilder
    {
        private List<Func<Task>> responses = new List<Func<Task>>();
        private bool isActive = true;

        internal static ThrowingDeviceProxyBuilder Create() => new ThrowingDeviceProxyBuilder();

        internal ThrowingDeviceProxyBuilder WithThrow<T>()
            where T : Exception
        {
            this.responses.Add(() => Task.FromException(Activator.CreateInstance(typeof(T), "test error") as Exception));
            return this;
        }

        internal ThrowingDeviceProxyBuilder WithSuccess(int count = 1)
        {
            this.responses.AddRange(Enumerable.Repeat<Func<Task>>(() => Task.CompletedTask, count));
            return this;
        }

        internal ThrowingDeviceProxyBuilder WithActiveStatus(bool isActive)
        {
            this.isActive = isActive;
            return this;
        }

        internal IDeviceProxy Build()
        {
            var nextResponse = this.responses.GetEnumerator();
            var mock = new Mock<IDeviceProxy>();

            mock.SetupGet(call => call.IsActive).Returns(this.isActive);
            mock.Setup(
                call => call.SendMessageAsync(It.IsAny<IMessage>(), It.IsAny<string>()))
                            .Returns(() =>
                            {
                                if (nextResponse.MoveNext())
                                {
                                    return nextResponse.Current();
                                }
                                else
                                {
                                    return this.responses.Last()();
                                }
                            });

            return mock.Object;
        }
    }
}
