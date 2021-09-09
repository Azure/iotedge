// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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

            Task<Try<ICloudProxy>> GetCloudProxy(string id) => Task.FromResult(Try.Success(cloudProxyMock.Object));

            var cloudEndpoint = new CloudEndpoint(cloudEndpointId, GetCloudProxy, routingMessageConverter, true);
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
            string device3Id = "device3";

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

            var device3SystemProperties = new Dictionary<string, string>
            {
                { SystemProperties.DeviceId, device3Id }
            };

            var cancelProperties = new Dictionary<string, string>()
            {
                { "Delay", "true" },
                { "Prop2", "Val2" }
            };

            var message1 = new RoutingMessage(TelemetryMessageSource.Instance, messageBody, properties, device1SystemProperties);
            var message2 = new RoutingMessage(TelemetryMessageSource.Instance, messageBody, properties, device2SystemProperties);
            var message3 = new RoutingMessage(TelemetryMessageSource.Instance, messageBody, cancelProperties, device1SystemProperties);
            var message4 = new RoutingMessage(TelemetryMessageSource.Instance, messageBody, properties, device3SystemProperties);

            Task<Try<ICloudProxy>> GetCloudProxy(string id)
            {
                if (id == device1Id)
                {
                    return Task.FromResult(Try.Success(cloudProxyMock.Object));
                }
                else if (id == device2Id)
                {
                    return Task.FromResult(Try<ICloudProxy>.Failure(new DeviceInvalidStateException()));
                }
                else
                {
                    return Task.FromResult(Try<ICloudProxy>.Failure(new Exception()));
                }
            }

            var cloudEndpoint = new CloudEndpoint(cloudEndpointId, GetCloudProxy, routingMessageConverter, false, maxBatchSize: 1);
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

            // Should handle as no connection on unhandled exception
            ISinkResult<IRoutingMessage> result4 = await cloudMessageProcessor.ProcessAsync(message4, CancellationToken.None);
            Assert.NotNull(result4);
            Assert.Empty(result4.InvalidDetailsList);
            Assert.NotEmpty(result4.Failed);
            Assert.Empty(result4.Succeeded);
            Assert.True(result4.SendFailureDetails.HasValue);

            // Initialize CloudEndpoint with trackDeviceState=true
            var cloudEndpoint2 = new CloudEndpoint(cloudEndpointId, GetCloudProxy, routingMessageConverter, true, maxBatchSize: 1);
            var cloudMessageProcessor2 = cloudEndpoint2.CreateProcessor();

            // Should fail and mark operation invalid on UnauthorizedException which will drop the message
            var invalidOnDeviceInvalidStateException = await cloudMessageProcessor2.ProcessAsync(message2, CancellationToken.None);
            Assert.NotNull(invalidOnDeviceInvalidStateException);
            Assert.NotEmpty(invalidOnDeviceInvalidStateException.InvalidDetailsList);
            Assert.Empty(invalidOnDeviceInvalidStateException.Failed);
            Assert.Empty(invalidOnDeviceInvalidStateException.Succeeded);
            Assert.True(invalidOnDeviceInvalidStateException.SendFailureDetails.HasValue);

            // Should fail and mark operation invalid on unhandled exception which will drop the message
            var invalidOnUnhandledException = await cloudMessageProcessor2.ProcessAsync(message4, CancellationToken.None);
            Assert.NotNull(invalidOnUnhandledException);
            Assert.NotEmpty(invalidOnUnhandledException.InvalidDetailsList);
            Assert.Empty(invalidOnUnhandledException.Failed);
            Assert.Empty(invalidOnUnhandledException.Succeeded);
            Assert.True(invalidOnUnhandledException.SendFailureDetails.HasValue);
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

            Task<Try<ICloudProxy>> GetCloudProxy(string id)
            {
                return Task.FromResult(Try.Success(cloudProxyMock.Object));
            }

            var cloudEndpoint = new CloudEndpoint(cloudEndpointId, GetCloudProxy, routingMessageConverter, true);
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

        [Fact]
        [Unit]
        public async Task ProcessInBatchesTest()
        {
            // Arrange
            string device1 = "d1";
            string device2 = "d2";
            string device3 = "d3";

            IList<IRoutingMessage> device1Messages = GetMessages(device1, 45);
            IList<IRoutingMessage> device2Messages = GetMessages(device2, 25);
            IList<IRoutingMessage> device3Messages = GetMessages(device3, 30);

            IList<IRoutingMessage> messagesToProcess = device1Messages
                .Concat(device2Messages)
                .Concat(device3Messages)
                .ToList();

            Mock<ICloudProxy> InitCloudProxy(List<int> receivedMsgCountList)
            {
                var cp = new Mock<ICloudProxy>();
                cp.Setup(c => c.SendMessageBatchAsync(It.IsAny<IEnumerable<IMessage>>()))
                    .Callback<IEnumerable<IMessage>>(b => receivedMsgCountList.Add(b.Count()))
                    .Returns(Task.CompletedTask);
                cp.SetupGet(p => p.IsActive).Returns(true);
                return cp;
            }

            var device1CloudReceivedMessagesCountList = new List<int>();
            Mock<ICloudProxy> device1CloudProxy = InitCloudProxy(device1CloudReceivedMessagesCountList);

            var device2CloudReceivedMessagesCountList = new List<int>();
            Mock<ICloudProxy> device2CloudProxy = InitCloudProxy(device2CloudReceivedMessagesCountList);

            var device3CloudReceivedMessagesCountList = new List<int>();
            Mock<ICloudProxy> device3CloudProxy = InitCloudProxy(device3CloudReceivedMessagesCountList);

            Task<Try<ICloudProxy>> GetCloudProxy(string id)
            {
                ICloudProxy cp = null;
                if (id == device1)
                {
                    cp = device1CloudProxy.Object;
                }
                else if (id == device2)
                {
                    cp = device2CloudProxy.Object;
                }
                else if (id == device3)
                {
                    cp = device3CloudProxy.Object;
                }

                return Task.FromResult(Try.Success(cp));
            }

            Core.IMessageConverter<IRoutingMessage> routingMessageConverter = new RoutingMessageConverter();
            string cloudEndpointId = Guid.NewGuid().ToString();
            var cloudEndpoint = new CloudEndpoint(cloudEndpointId, GetCloudProxy, routingMessageConverter, true, 10);

            // Act
            IProcessor cloudMessageProcessor = cloudEndpoint.CreateProcessor();
            ISinkResult<IRoutingMessage> sinkResult = await cloudMessageProcessor.ProcessAsync(messagesToProcess, CancellationToken.None);

            // Assert
            Assert.Equal(messagesToProcess, sinkResult.Succeeded);
            Assert.Equal(device1CloudReceivedMessagesCountList, new[] { 10, 10, 10, 10, 5 });
            Assert.Equal(device2CloudReceivedMessagesCountList, new[] { 10, 10, 5 });
            Assert.Equal(device3CloudReceivedMessagesCountList, new[] { 10, 10, 10 });
        }

        [Fact]
        [Unit]
        public async Task ProcessInBatchesWithBatchSizeTest()
        {
            // Arrange
            string device1 = "d1";
            string device2 = "d2";
            string device3 = "d3";

            IList<IRoutingMessage> device1Messages = GetMessages(device1, 45);
            IList<IRoutingMessage> device2Messages = GetMessages(device2, 25);
            IList<IRoutingMessage> device3Messages = GetMessages(device3, 30);

            IList<IRoutingMessage> messagesToProcess = device1Messages
                .Concat(device2Messages)
                .Concat(device3Messages)
                .ToList();

            Mock<ICloudProxy> InitCloudProxy(List<int> receivedMsgCountList)
            {
                var cp = new Mock<ICloudProxy>();
                cp.Setup(c => c.SendMessageBatchAsync(It.IsAny<IEnumerable<IMessage>>()))
                    .Callback<IEnumerable<IMessage>>(b => receivedMsgCountList.Add(b.Count()))
                    .Returns(Task.CompletedTask);
                cp.SetupGet(p => p.IsActive).Returns(true);
                return cp;
            }

            var device1CloudReceivedMessagesCountList = new List<int>();
            Mock<ICloudProxy> device1CloudProxy = InitCloudProxy(device1CloudReceivedMessagesCountList);

            var device2CloudReceivedMessagesCountList = new List<int>();
            Mock<ICloudProxy> device2CloudProxy = InitCloudProxy(device2CloudReceivedMessagesCountList);

            var device3CloudReceivedMessagesCountList = new List<int>();
            Mock<ICloudProxy> device3CloudProxy = InitCloudProxy(device3CloudReceivedMessagesCountList);

            Task<Try<ICloudProxy>> GetCloudProxy(string id)
            {
                ICloudProxy cp = null;
                if (id == device1)
                {
                    cp = device1CloudProxy.Object;
                }
                else if (id == device2)
                {
                    cp = device2CloudProxy.Object;
                }
                else if (id == device3)
                {
                    cp = device3CloudProxy.Object;
                }

                return Task.FromResult(Try.Success(cp));
            }

            Core.IMessageConverter<IRoutingMessage> routingMessageConverter = new RoutingMessageConverter();
            string cloudEndpointId = Guid.NewGuid().ToString();
            var cloudEndpoint = new CloudEndpoint(cloudEndpointId, GetCloudProxy, routingMessageConverter, true, 30);

            // Act
            IProcessor cloudMessageProcessor = cloudEndpoint.CreateProcessor();
            ISinkResult<IRoutingMessage> sinkResult = await cloudMessageProcessor.ProcessAsync(messagesToProcess, CancellationToken.None);

            // Assert
            Assert.Equal(messagesToProcess, sinkResult.Succeeded);
            Assert.Equal(device1CloudReceivedMessagesCountList, new[] { 30, 15 });
            Assert.Equal(device2CloudReceivedMessagesCountList, new[] { 25 });
            Assert.Equal(device3CloudReceivedMessagesCountList, new[] { 30 });
        }

        [Fact]
        [Unit]
        public async Task NoErrorFromCloudProxy_NoErrorDetailsReturned()
        {
            var batchSize = 10;
            Core.IMessageConverter<IRoutingMessage> routingMessageConverter = new RoutingMessageConverter();
            string cloudEndpointId = Guid.NewGuid().ToString();

            var cloudProxy = ThrowingCloudProxy
                                .Create()
                                .WithBatchSize(batchSize)
                                .WithReportSuccessfulBatch(3)
                                .Build();

            Task<Try<ICloudProxy>> GetCloudProxy(string id) => Task.FromResult(Try.Success(cloudProxy));

            var cloudEndpoint = new CloudEndpoint(cloudEndpointId, GetCloudProxy, routingMessageConverter, true, batchSize);
            IProcessor cloudMessageProcessor = cloudEndpoint.CreateProcessor();

            var sinkResult = await cloudMessageProcessor.ProcessAsync(GetMessages("device1", 3 * batchSize), CancellationToken.None);

            Assert.True(sinkResult.IsSuccessful);
            Assert.Equal(3 * batchSize, sinkResult.Succeeded.Count);
            Assert.Equal(0, sinkResult.Failed.Count);
            Assert.Equal(0, sinkResult.InvalidDetailsList.Count);
            Assert.Equal(Option.None<SendFailureDetails>(), sinkResult.SendFailureDetails);
        }

        [Fact]
        [Unit]
        public async Task TransientErrorInFirstBatch_FastFailsRest()
        {
            var batchSize = 10;
            Core.IMessageConverter<IRoutingMessage> routingMessageConverter = new RoutingMessageConverter();
            string cloudEndpointId = Guid.NewGuid().ToString();

            var cloudProxy = ThrowingCloudProxy
                                .Create()
                                .WithBatchSize(batchSize)
                                .WithReportErrorInBatch(ThrowingCloudProxy.Throw<Client.Exceptions.IotHubException>())
                                .WithReportSuccessfulBatch(2)
                                .Build();

            Task<Try<ICloudProxy>> GetCloudProxy(string id) => Task.FromResult(Try.Success(cloudProxy));

            var cloudEndpoint = new CloudEndpoint(cloudEndpointId, GetCloudProxy, routingMessageConverter, true);
            IProcessor cloudMessageProcessor = cloudEndpoint.CreateProcessor();

            var sinkResult = await cloudMessageProcessor.ProcessAsync(GetMessages("device1", 3 * batchSize), CancellationToken.None);

            // although the test is setup to succeed with batch 2 and 3, they will fast fail because of the first one
            Assert.False(sinkResult.IsSuccessful);
            Assert.Equal(0, sinkResult.Succeeded.Count);
            Assert.Equal(30, sinkResult.Failed.Count);
            Assert.Equal(0, sinkResult.InvalidDetailsList.Count);
            Assert.True(sinkResult.SendFailureDetails.HasValue);
            Assert.Equal(FailureKind.Transient, sinkResult.SendFailureDetails.Expect(() => new Exception()).FailureKind);
        }

        [Fact]
        [Unit]
        public async Task NonTransientErrorInFirstBatch_LetsTryTheRest_ButReportsSendFailure()
        {
            var batchSize = 10;
            Core.IMessageConverter<IRoutingMessage> routingMessageConverter = new RoutingMessageConverter();
            string cloudEndpointId = Guid.NewGuid().ToString();

            var cloudProxy = ThrowingCloudProxy
                                .Create()
                                .WithBatchSize(batchSize)
                                .WithReportErrorInBatch(ThrowingCloudProxy.Throw<Exception>())
                                .WithReportSuccessfulBatch(2)
                                .Build();

            Task<Try<ICloudProxy>> GetCloudProxy(string id) => Task.FromResult(Try.Success(cloudProxy));

            var cloudEndpoint = new CloudEndpoint(cloudEndpointId, GetCloudProxy, routingMessageConverter, true);
            IProcessor cloudMessageProcessor = cloudEndpoint.CreateProcessor();

            var sinkResult = await cloudMessageProcessor.ProcessAsync(GetMessages("device1", 3 * batchSize), CancellationToken.None);

            Assert.True(sinkResult.IsSuccessful); // non-transient errors are ignored, but reported in SendFailureDetails
            Assert.Equal(2 * batchSize, sinkResult.Succeeded.Count);
            Assert.Equal(0, sinkResult.Failed.Count);
            Assert.Equal(batchSize, sinkResult.InvalidDetailsList.Count);
            Assert.True(sinkResult.SendFailureDetails.HasValue);
            Assert.Equal(FailureKind.InvalidInput, sinkResult.SendFailureDetails.Expect(() => new Exception()).FailureKind);
        }

        [Fact]
        [Unit]
        public async Task TransientErrorInSecondBatch_FastFailsRest_OverwritesNonTransientResult()
        {
            var batchSize = 10;
            Core.IMessageConverter<IRoutingMessage> routingMessageConverter = new RoutingMessageConverter();
            string cloudEndpointId = Guid.NewGuid().ToString();

            var cloudProxy = ThrowingCloudProxy
                                .Create()
                                .WithBatchSize(batchSize)
                                .WithReportErrorInBatch(ThrowingCloudProxy.Throw<Exception>())
                                .WithReportErrorInBatch(ThrowingCloudProxy.Throw<Client.Exceptions.IotHubException>())
                                .WithReportSuccessfulBatch()
                                .Build();

            Task<Try<ICloudProxy>> GetCloudProxy(string id) => Task.FromResult(Try.Success(cloudProxy));

            var cloudEndpoint = new CloudEndpoint(cloudEndpointId, GetCloudProxy, routingMessageConverter, true);
            IProcessor cloudMessageProcessor = cloudEndpoint.CreateProcessor();

            var sinkResult = await cloudMessageProcessor.ProcessAsync(GetMessages("device1", 3 * batchSize), CancellationToken.None);

            Assert.False(sinkResult.IsSuccessful);
            Assert.Equal(0, sinkResult.Succeeded.Count);
            Assert.Equal(2 * batchSize, sinkResult.Failed.Count);
            Assert.Equal(1 * batchSize, sinkResult.InvalidDetailsList.Count);
            Assert.True(sinkResult.SendFailureDetails.HasValue);
            Assert.Equal(FailureKind.Transient, sinkResult.SendFailureDetails.Expect(() => new Exception()).FailureKind);
        }

        [Fact]
        [Unit]
        public async Task TransientErrorOfFirstIdentity_DoesNotFastFailsSecondIdentity_ButReportsError()
        {
            var batchSize = 10;
            Core.IMessageConverter<IRoutingMessage> routingMessageConverter = new RoutingMessageConverter();
            string cloudEndpointId = Guid.NewGuid().ToString();

            // this wont fast fail
            var cloudProxy1 = ThrowingCloudProxy
                                .Create()
                                .WithBatchSize(batchSize)
                                .WithReportSuccessfulBatch(3)
                                .Build();

            // this will fast fail after a batch (skipping the second and the third)
            var cloudProxy2 = ThrowingCloudProxy
                                .Create()
                                .WithBatchSize(batchSize)
                                .WithReportSuccessfulBatch()
                                .WithReportErrorInBatch(ThrowingCloudProxy.Throw<Client.Exceptions.IotHubException>())
                                .WithReportSuccessfulBatch()
                                .Build();

            var proxyMap = new Dictionary<string, ICloudProxy> { ["device1"] = cloudProxy1, ["device2"] = cloudProxy2 };

            Task<Try<ICloudProxy>> GetCloudProxy(string id) => Task.FromResult(Try.Success(proxyMap[id]));

            var cloudEndpoint = new CloudEndpoint(cloudEndpointId, GetCloudProxy, routingMessageConverter, true);
            var cloudMessageProcessor = cloudEndpoint.CreateProcessor();

            var random = new Random(35325);
            var messages = GetMessages("device1", 3 * batchSize).Concat(GetMessages("device2", 3 * batchSize)).OrderBy(order => random.Next()).ToList();
            var sinkResult = await cloudMessageProcessor.ProcessAsync(messages, CancellationToken.None);

            Assert.False(sinkResult.IsSuccessful); // one batch went wrong, should report here
            Assert.Equal(3 * batchSize + 1 * batchSize, sinkResult.Succeeded.Count);  // dev1 all good, dev2 1st good
            Assert.Equal(2 * batchSize, sinkResult.Failed.Count);
            Assert.Equal(0, sinkResult.InvalidDetailsList.Count);
            Assert.True(sinkResult.SendFailureDetails.HasValue);
            Assert.Equal(FailureKind.Transient, sinkResult.SendFailureDetails.Expect(() => new Exception()).FailureKind);
        }

        [Theory]
        [InlineData(10, 1024, 10)]
        [InlineData(10, 64 * 1024, 4)]
        [InlineData(20, 50 * 1024, 5)]
        [Unit]
        public void GetBatchSizeTest(int maxBatchSize, int maxMessageSize, int expectedBatchSize)
        {
            Assert.Equal(expectedBatchSize, CloudEndpoint.CloudMessageProcessor.GetBatchSize(maxBatchSize, maxMessageSize));
        }

        static IList<IRoutingMessage> GetMessages(string id, int count)
        {
            var messages = new List<IRoutingMessage>();
            for (int i = 0; i < count; i++)
            {
                messages.Add(GetMessage(id));
            }

            return messages;
        }

        static IRoutingMessage GetMessage(string id)
        {
            byte[] messageBody = Encoding.UTF8.GetBytes("Message body");
            var properties = new Dictionary<string, string>()
            {
                { "Prop1", "Val1" },
                { "Prop2", "Val2" }
            };

            var systemProperties = new Dictionary<string, string>
            {
                { SystemProperties.DeviceId, id }
            };

            var message = new RoutingMessage(TelemetryMessageSource.Instance, messageBody, properties, systemProperties);
            return message;
        }
    }

    internal class ThrowingCloudProxy : ICloudProxy
    {
        private List<(int, Func<Task>)> callResponses;
        private int callCounter;

        private ThrowingCloudProxy(List<(int, Func<Task>)> callResponses)
        {
            this.callResponses = callResponses;
        }

        public bool IsActive => true;
        public Task<bool> CloseAsync() => Task.FromResult(true);
        public Task<IMessage> GetTwinAsync() => throw new NotImplementedException();
        public Task<bool> OpenAsync() => Task.FromResult(true);
        public Task RemoveCallMethodAsync() => throw new NotImplementedException();
        public Task RemoveDesiredPropertyUpdatesAsync() => throw new NotImplementedException();
        public Task RemoveTwinResponseAsync() => throw new NotImplementedException();
        public Task SendFeedbackMessageAsync(string messageId, FeedbackStatus feedbackStatus) => throw new NotImplementedException();
        public Task SetupCallMethodAsync() => throw new NotImplementedException();
        public Task SetupDesiredPropertyUpdatesAsync() => throw new NotImplementedException();
        public Task StartListening() => Task.FromResult(true);
        public Task StopListening() => Task.FromResult(true);
        public Task UpdateReportedPropertiesAsync(IMessage reportedPropertiesMessage) => throw new NotImplementedException();

        public Task SendMessageAsync(IMessage message)
        {
            var currentAction = this.Find(++this.callCounter);
            return currentAction();
        }

        private Func<Task> Find(int targetCount)
        {
            var currentCount = 0;
            foreach (var (count, response) in this.callResponses)
            {
                currentCount += count;
                if (currentCount >= targetCount)
                    return response;
            }

            throw new InvalidOperationException("No response is defined for ThrowingCloudProxy call");
        }

        public Task SendMessageBatchAsync(IEnumerable<IMessage> inputMessages)
        {
            return Task.WhenAll(inputMessages.Select(m => this.SendMessageAsync(m)));
        }

        internal static Func<Task> Success() => new Func<Task>(() => Task.FromResult(true));
        internal static Func<Task> Throw<T>()
            where T : Exception, new()
        {
            return () => Task.FromException(new T());
        }

        internal static CloudProxyBuilder Create()
        {
            return new CloudProxyBuilder();
        }

        internal class CloudProxyBuilder
        {
            private Random random = new Random(834793);
            private List<(int, Func<Task>)> callResponses = new List<(int, Func<Task>)>();
            private int batchSize = 10;
            private Func<Task> successAction = ThrowingCloudProxy.Success();

            internal CloudProxyBuilder WithBatchSize(int batchSize)
            {
                this.batchSize = batchSize;
                return this;
            }

            internal CloudProxyBuilder WithSuccessAction(Func<Task> successAction)
            {
                this.successAction = successAction;
                return this;
            }

            internal CloudProxyBuilder WithReportSuccessfulBatch(int batchCount = 1)
            {
                this.callResponses.Add((batchCount * this.batchSize, this.successAction));
                return this;
            }

            internal CloudProxyBuilder WithReportErrorInBatch(Func<Task> errorAction)
            {
                var index = this.random.Next(this.batchSize);

                if (index > 0)
                    this.callResponses.Add((index, this.successAction));

                this.callResponses.Add((1, errorAction));

                if (index + 1 != this.batchSize)
                    this.callResponses.Add((this.batchSize - (index + 1), this.successAction));

                return this;
            }

            internal ICloudProxy Build()
            {
                return new ThrowingCloudProxy(this.callResponses);
            }
        }
    }
}
