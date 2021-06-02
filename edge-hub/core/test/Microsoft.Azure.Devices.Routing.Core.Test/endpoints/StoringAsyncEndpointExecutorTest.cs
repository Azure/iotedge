// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Endpoints
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Routing.Core.Checkpointers;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Moq;
    using Xunit;

    [Integration]
    public class StoringAsyncEndpointExecutorTest
    {
        [Fact]
        public async Task InvokeTest()
        {
            // Arrange
            const int MessagesCount = 10;
            const string EndpointId = "endpoint1";
            const uint Priority = 100;
            var endpoint = new NullEndpoint(EndpointId);
            var priorities = new List<uint>() { Priority };
            var checkpointerFactory = new NullCheckpointerFactory();
            var endpointExecutorConfig = new EndpointExecutorConfig(TimeSpan.FromHours(1), RetryStrategy.NoRetry, TimeSpan.FromHours(1));
            var asyncEndpointExecutorOptions = new AsyncEndpointExecutorOptions(10);
            var messageStore = new TestMessageStore();
            var storingAsyncEndpointExecutor = new StoringAsyncEndpointExecutor(endpoint, checkpointerFactory, endpointExecutorConfig, asyncEndpointExecutorOptions, messageStore);
            await storingAsyncEndpointExecutor.UpdatePriorities(priorities, Option.None<Endpoint>());
            IEnumerable<IMessage> messages = GetNewMessages(MessagesCount, 0);
            string messageQueueId = $"{EndpointId}_Pri{Priority}";

            // Act - Send messages to invoke
            foreach (IMessage message in messages)
            {
                await storingAsyncEndpointExecutor.Invoke(message, Priority, 3600);
            }

            // Assert - Check that the message store received the messages sent to invoke.
            List<IMessage> storeMessages = messageStore.GetReceivedMessagesForEndpoint(messageQueueId);
            Assert.NotNull(storeMessages);
            Assert.Equal(MessagesCount, storeMessages.Count);
            for (int i = 0; i < MessagesCount; i++)
            {
                IMessage message = storeMessages[i];
                Assert.True(message.Properties.ContainsKey($"key{i}"));
                Assert.Equal($"value{i}", message.Properties[$"key{i}"]);
            }

            // Assert - Make sure no additional / duplicate messages were sent.
            storeMessages = messageStore.GetReceivedMessagesForEndpoint(messageQueueId);
            Assert.NotNull(storeMessages);
            Assert.Equal(10, storeMessages.Count);

            // Act - Send messages again to Invoke.
            messages = GetNewMessages(MessagesCount, MessagesCount);
            foreach (IMessage message in messages)
            {
                await storingAsyncEndpointExecutor.Invoke(message, Priority, 3600);
            }

            // Assert - Make sure the store now has the old and the new messages.
            storeMessages = messageStore.GetReceivedMessagesForEndpoint(messageQueueId);
            Assert.NotNull(storeMessages);
            Assert.Equal(MessagesCount * 2, storeMessages.Count);
            for (int i = 0; i < MessagesCount * 2; i++)
            {
                IMessage message = storeMessages[i];
                Assert.True(message.Properties.ContainsKey($"key{i}"));
                Assert.Equal($"value{i}", message.Properties[$"key{i}"]);
            }
        }

        [Fact]
        public async Task PumpMessagesTest()
        {
            // Arrange
            const int MessagesCount = 10;
            const string EndpointId = "endpoint1";
            var endpoint = new TestEndpoint(EndpointId);
            var priorities = new List<uint>() { 0, 1, 2, 100, 101, 102 };
            var checkpointerFactory = new NullCheckpointerFactory();
            var endpointExecutorConfig = new EndpointExecutorConfig(TimeSpan.FromHours(1), RetryStrategy.NoRetry, TimeSpan.FromHours(1));
            var asyncEndpointExecutorOptions = new AsyncEndpointExecutorOptions(4, TimeSpan.FromSeconds(2));
            var messageStore = new TestMessageStore();
            var storingAsyncEndpointExecutor = new StoringAsyncEndpointExecutor(endpoint, checkpointerFactory, endpointExecutorConfig, asyncEndpointExecutorOptions, messageStore);
            await storingAsyncEndpointExecutor.UpdatePriorities(priorities, Option.None<Endpoint>());
            IEnumerable<IMessage> messages = GetNewMessages(MessagesCount, 0);

            // Act - Send messages to invoke
            foreach (IMessage message in messages)
            {
                await storingAsyncEndpointExecutor.Invoke(message, 0, 3600);
            }

            await Task.Delay(TimeSpan.FromSeconds(10));

            // Assert - Make sure the endpoint received all the messages.
            Assert.Equal(MessagesCount, endpoint.N);
            for (int i = 0; i < MessagesCount; i++)
            {
                IMessage message = endpoint.Processed[i];
                Assert.True(message.Properties.ContainsKey($"key{i}"));
                Assert.Equal($"value{i}", message.Properties[$"key{i}"]);
            }
        }

        [Fact]
        public async Task PumpMessagesWithLargeIncomingBatchTest()
        {
            // Arrange
            const int MessagesCount = 200;
            const string EndpointId = "endpoint1";
            const int RoutingPumpBatchSize = 10;
            var endpoint = new TestEndpoint(EndpointId);
            var priorities = new List<uint>() { 0, 1, 2, 100, 101, 102 };
            var checkpointerFactory = new NullCheckpointerFactory();
            var endpointExecutorConfig = new EndpointExecutorConfig(TimeSpan.FromHours(1), RetryStrategy.NoRetry, TimeSpan.FromHours(1));
            var asyncEndpointExecutorOptions = new AsyncEndpointExecutorOptions(RoutingPumpBatchSize, TimeSpan.FromMilliseconds(1));
            var messageStore = new TestMessageStore();
            var storingAsyncEndpointExecutor = new StoringAsyncEndpointExecutor(endpoint, checkpointerFactory, endpointExecutorConfig, asyncEndpointExecutorOptions, messageStore);
            await storingAsyncEndpointExecutor.UpdatePriorities(priorities, Option.None<Endpoint>());
            IEnumerable<IMessage> messages = GetNewMessages(MessagesCount, 0);

            // Act - Send messages to invoke
            foreach (IMessage message in messages)
            {
                await storingAsyncEndpointExecutor.Invoke(message, 0, 3600);
            }

            await Task.Delay(TimeSpan.FromSeconds(10));

            // Assert - Make sure the endpoint received all the messages.
            Assert.Equal(MessagesCount, endpoint.N);
            for (int i = 0; i < MessagesCount; i++)
            {
                IMessage message = endpoint.Processed[i];
                Assert.True(message.Properties.ContainsKey($"key{i}"));
                Assert.Equal($"value{i}", message.Properties[$"key{i}"]);
            }
        }

        [Fact]
        public async Task CloseAsyncWillCallCloseAsyncOfAllFsmsTest()
        {
            var endpoint = new TestEndpoint("endpoint1");
            endpoint.CanProcess = false;
            var storingAsyncEndpointExecutor = new StoringAsyncEndpointExecutor(
                endpoint,
                new NullCheckpointerFactory(),
                new EndpointExecutorConfig(TimeSpan.FromHours(1), RetryStrategy.DefaultFixed, TimeSpan.FromHours(1)),
                new AsyncEndpointExecutorOptions(10, TimeSpan.FromMilliseconds(1)),
                new TestMessageStore());
            await storingAsyncEndpointExecutor.UpdatePriorities(new List<uint>() { 0 }, Option.None<Endpoint>());
            var message = GetNewMessages(1, 0).First();

            await storingAsyncEndpointExecutor.Invoke(message, 0, 3600);
            // await Task.Delay(1000);
            Assert.Equal(0, endpoint.N);

            Task closeTask = storingAsyncEndpointExecutor.CloseAsync();
            Task timeoutTask = Task.Delay(5000);
            var firstCompletedTask = await Task.WhenAny(closeTask, timeoutTask);

            Assert.True(firstCompletedTask == closeTask, "storingAsyncEndpointExecutor can't close when processing a message");
        }

        [Fact]
        public async Task TestSetEndpoint()
        {
            var endpoint1 = new TestEndpoint("id");
            var endpoint2 = new NullEndpoint("id");
            var endpoint3 = new TestEndpoint("id1");
            var priorities = new List<uint>() { 100, 101, 102, 0, 1, 2 };
            var checkpointerFactory = new NullCheckpointerFactory();
            var endpointExecutorConfig = new EndpointExecutorConfig(TimeSpan.FromHours(1), RetryStrategy.NoRetry, TimeSpan.FromHours(1));
            var asyncEndpointExecutorOptions = new AsyncEndpointExecutorOptions(1, TimeSpan.FromSeconds(1));
            var messageStore = new TestMessageStore();
            var executor = new StoringAsyncEndpointExecutor(endpoint1, checkpointerFactory, endpointExecutorConfig, asyncEndpointExecutorOptions, messageStore);
            await executor.UpdatePriorities(priorities, Option.None<Endpoint>());

            Assert.Equal(endpoint1, executor.Endpoint);
            await Assert.ThrowsAsync<ArgumentNullException>(() => executor.SetEndpoint(null, new List<uint>() { 0 }));
            await Assert.ThrowsAsync<ArgumentNullException>(() => executor.SetEndpoint(endpoint1, null));
            await Assert.ThrowsAsync<ArgumentException>(() => executor.SetEndpoint(endpoint3, new List<uint>() { 0 }));

            await executor.SetEndpoint(endpoint2, new List<uint>() { 103, 104, 105, 0, 1, 2, 2 });
            Assert.Equal(endpoint2, executor.Endpoint);

            await executor.CloseAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => executor.SetEndpoint(endpoint1, new List<uint>() { 0 }));
        }

        [Fact]
        public async Task StoreMessagesProviderInitTest()
        {
            // Arrange
            int batchSize = 100;
            List<IMessage> messages = GetNewMessages(batchSize, 0).ToList();
            var iterator = new Mock<IMessageIterator>();
            iterator.SetupSequence(i => i.GetNext(It.IsAny<int>()))
                .ReturnsAsync(messages.Take(15))
                .ReturnsAsync(messages.Skip(15).Take(15))
                .ReturnsAsync(messages.Skip(30).Take(70));

            // Act
            var messagesProvider = new StoringAsyncEndpointExecutor.StoreMessagesProvider(iterator.Object, 100);

            // Assert
            await Task.Delay(TimeSpan.FromSeconds(1));
            iterator.VerifyAll();

            // Act
            IMessage[] messagesBatch = await messagesProvider.GetMessages();

            // Assert
            Assert.NotNull(messagesBatch);
            Assert.Equal(batchSize, messagesBatch.Length);
            Assert.Equal(messages, messagesBatch);
        }

        [Fact]
        public async Task StoreMessagesProviderTest()
        {
            // Arrange
            int batchSize = 100;
            List<IMessage> messages = GetNewMessages(batchSize * 3, 0).ToList();
            var iterator = new Mock<IMessageIterator>();
            iterator.SetupSequence(i => i.GetNext(It.IsAny<int>()))
                .ReturnsAsync(messages.Take(15))
                .ReturnsAsync(messages.Skip(15).Take(15))
                .ReturnsAsync(messages.Skip(30).Take(70))
                .ReturnsAsync(messages.Skip(100).Take(52))
                .ReturnsAsync(messages.Skip(152).Take(48))
                .ReturnsAsync(messages.Skip(200));

            // Act
            var messagesProvider = new StoringAsyncEndpointExecutor.StoreMessagesProvider(iterator.Object, 100);
            await Task.Delay(TimeSpan.FromSeconds(1));
            IMessage[] messagesBatch = await messagesProvider.GetMessages();

            // Assert
            Assert.NotNull(messagesBatch);
            Assert.Equal(batchSize, messagesBatch.Length);
            Assert.Equal(messages.Take(100), messagesBatch);

            // Act
            await Task.Delay(TimeSpan.FromSeconds(1));
            messagesBatch = await messagesProvider.GetMessages();

            // Assert
            Assert.NotNull(messagesBatch);
            Assert.Equal(batchSize, messagesBatch.Length);
            Assert.Equal(messages.Skip(100).Take(100), messagesBatch);

            // Assert
            await Task.Delay(TimeSpan.FromSeconds(1));
            iterator.VerifyAll();

            // Act
            messagesBatch = await messagesProvider.GetMessages();

            // Assert
            Assert.NotNull(messagesBatch);
            Assert.Equal(batchSize, messagesBatch.Length);
            Assert.Equal(messages.Skip(200).Take(100), messagesBatch);
        }

        [Fact]
        public async Task StoreMessagesProviderIntermittantMessagesTest()
        {
            // Arrange
            int batchSize = 10;
            List<IMessage> messages = GetNewMessages(batchSize, 0).ToList();
            var iterator = new Mock<IMessageIterator>();
            iterator.SetupSequence(i => i.GetNext(It.IsAny<int>()))
                .ReturnsAsync(messages.Take(6))
                .ReturnsAsync(Enumerable.Empty<IMessage>())
                .ReturnsAsync(messages.Skip(6).Take(1))
                .ReturnsAsync(messages.Skip(7).Take(1))
                .ReturnsAsync(Enumerable.Empty<IMessage>())
                .ReturnsAsync(messages.Skip(8).Take(1))
                .ReturnsAsync(Enumerable.Empty<IMessage>());

            // Act
            var messagesProvider = new StoringAsyncEndpointExecutor.StoreMessagesProvider(iterator.Object, batchSize);
            await Task.Delay(TimeSpan.FromSeconds(1));
            IMessage[] messagesBatch = await messagesProvider.GetMessages();

            // Assert
            Assert.NotNull(messagesBatch);
            Assert.Equal(6, messagesBatch.Length);
            Assert.Equal(messages.Take(6), messagesBatch);

            // Act
            messagesBatch = await messagesProvider.GetMessages();

            // Assert
            Assert.NotNull(messagesBatch);
            Assert.Equal(2, messagesBatch.Length);
            Assert.Equal(messages.Skip(6).Take(2), messagesBatch);

            // Act
            messagesBatch = await messagesProvider.GetMessages();

            // Assert
            Assert.NotNull(messagesBatch);
            Assert.Single(messagesBatch);
            Assert.Equal(messages.Skip(8).Take(1), messagesBatch);

            // Assert
            iterator.VerifyAll();
        }

        [Fact]
        public async Task MessagePrioritiesTest()
        {
            // Arrange
            const string EndpointId = "endpoint1";
            const uint HighPri = 0;
            const uint NormalPri = 5;
            const uint LowPri = 10;
            var endpoint = new TestEndpoint(EndpointId);
            var priorities = new List<uint>() { HighPri, NormalPri, LowPri };
            var checkpointerFactory = new NullCheckpointerFactory();
            var endpointExecutorConfig = new EndpointExecutorConfig(TimeSpan.FromHours(1), RetryStrategy.DefaultFixed, TimeSpan.FromHours(1));
            var asyncEndpointExecutorOptions = new AsyncEndpointExecutorOptions(4, TimeSpan.FromSeconds(2));
            var messageStore = new TestMessageStore();
            var storingAsyncEndpointExecutor = new StoringAsyncEndpointExecutor(endpoint, checkpointerFactory, endpointExecutorConfig, asyncEndpointExecutorOptions, messageStore);
            await storingAsyncEndpointExecutor.UpdatePriorities(priorities, Option.None<Endpoint>());

            var normalPriMsg1 = new Message(TelemetryMessageSource.Instance, new byte[] { 1 }, new Dictionary<string, string> { { "normalPriority", string.Empty } }, 0L);
            var normalPriMsg2 = new Message(TelemetryMessageSource.Instance, new byte[] { 2 }, new Dictionary<string, string> { { "normalPriority", string.Empty } }, 1L);
            var normalPriMsg3 = new Message(TelemetryMessageSource.Instance, new byte[] { 3 }, new Dictionary<string, string> { { "normalPriority", string.Empty } }, 2L);
            var lowPriMsg1 = new Message(TelemetryMessageSource.Instance, new byte[] { 4 }, new Dictionary<string, string> { { "lowPriority", string.Empty } }, 3L);
            var lowPriMsg2 = new Message(TelemetryMessageSource.Instance, new byte[] { 5 }, new Dictionary<string, string> { { "lowPriority", string.Empty } }, 4L);
            var highPriMsg1 = new Message(TelemetryMessageSource.Instance, new byte[] { 6 }, new Dictionary<string, string> { { "highPriority", string.Empty } }, 5L);
            var normalPriMsg4 = new Message(TelemetryMessageSource.Instance, new byte[] { 7 }, new Dictionary<string, string> { { "normalPriority", string.Empty } }, 6L);
            var highPriMsg2 = new Message(TelemetryMessageSource.Instance, new byte[] { 8 }, new Dictionary<string, string> { { "highPriority", string.Empty } }, 7L);
            const int HighPriCount = 2;
            const int NormalPriCount = 4;
            const int LowPriCount = 2;

            // Disable the endpoint so messages are stuck in queue
            endpoint.CanProcess = false;

            // Send normal priority messages
            await storingAsyncEndpointExecutor.Invoke(normalPriMsg1, NormalPri, 3600);
            await storingAsyncEndpointExecutor.Invoke(normalPriMsg2, NormalPri, 3600);
            await storingAsyncEndpointExecutor.Invoke(normalPriMsg3, NormalPri, 3600);

            // Send low priority messages
            await storingAsyncEndpointExecutor.Invoke(lowPriMsg1, LowPri, 3600);
            await storingAsyncEndpointExecutor.Invoke(lowPriMsg2, LowPri, 3600);

            // Send the remaining messages mixed priority
            await storingAsyncEndpointExecutor.Invoke(highPriMsg1, HighPri, 3600);
            await storingAsyncEndpointExecutor.Invoke(normalPriMsg4, NormalPri, 3600);
            await storingAsyncEndpointExecutor.Invoke(highPriMsg2, HighPri, 3600);

            // Message store should have the messages in the corresponding queues
            var highPriQueue = messageStore.GetReceivedMessagesForEndpoint($"{endpoint.Id}_Pri{HighPri}");
            Assert.Equal(2, highPriQueue.Count);
            Assert.Contains(highPriMsg1, highPriQueue);
            Assert.Contains(highPriMsg2, highPriQueue);

            var normalPriQueue = messageStore.GetReceivedMessagesForEndpoint($"{endpoint.Id}_Pri{NormalPri}");
            Assert.Equal(4, normalPriQueue.Count);
            Assert.Contains(normalPriMsg1, normalPriQueue);
            Assert.Contains(normalPriMsg2, normalPriQueue);
            Assert.Contains(normalPriMsg3, normalPriQueue);
            Assert.Contains(normalPriMsg4, normalPriQueue);

            var lowPriQueue = messageStore.GetReceivedMessagesForEndpoint($"{endpoint.Id}_Pri{LowPri}");
            Assert.Equal(2, lowPriQueue.Count);
            Assert.Contains(lowPriMsg1, lowPriQueue);
            Assert.Contains(lowPriMsg2, lowPriQueue);

            // Re-enable the endpoint and let the queues drain
            endpoint.CanProcess = true;
            int retryAttempts = 0;
            int count = endpoint.Processed.Count();
            while (count != 8)
            {
                Assert.True(count < 8);
                await Task.Delay(TimeSpan.FromSeconds(3));
                retryAttempts++;
                Assert.True(retryAttempts < 8, "Too many retry attempts. Failed because test is taking too long.");
                count = endpoint.Processed.Count();
            }

            // Assert - Make sure the endpoint received all the messages
            // in the right priority order:
            //  - HighPri messages should finish processing before others
            //  - NormalPri messages should finish processing before LowPri
            Assert.Equal(8, endpoint.Processed.Count());
            int highPriMessagesProcessed = 0;
            int normalPriMessagesProcessed = 0;
            int lowPriMessagesProcessed = 0;
            for (int i = 0; i < endpoint.Processed.Count(); i++)
            {
                IMessage message = endpoint.Processed[i];
                if (message.Properties.ContainsKey($"highPriority"))
                {
                    if (++highPriMessagesProcessed == HighPriCount)
                    {
                        // Found all the high-pri messages,
                        // normal and low pri at this point
                        // must not have completed yet
                        Assert.True(normalPriMessagesProcessed < NormalPriCount);
                        Assert.True(lowPriMessagesProcessed < LowPriCount);
                    }
                }
                else if (message.Properties.ContainsKey($"normalPriority"))
                {
                    if (++normalPriMessagesProcessed == NormalPriCount)
                    {
                        // Found all the normal-pri messages,
                        // low pri messages at this point must
                        // not have completed yet
                        Assert.True(lowPriMessagesProcessed < LowPriCount);

                        // High pri messages should have completed
                        Assert.True(highPriMessagesProcessed == HighPriCount);
                    }
                }
                else if (message.Properties.ContainsKey($"lowPriority"))
                {
                    if (++lowPriMessagesProcessed == LowPriCount)
                    {
                        // Found all the low-pri messages,
                        // high-pri and normal-pri should also
                        // have completed before this
                        Assert.True(highPriMessagesProcessed == HighPriCount);
                        Assert.True(normalPriMessagesProcessed == NormalPriCount);
                    }
                }
                else
                {
                    // Bad test setup
                    Assert.True(false, "Bad test setup, processed a message with unexpected priority");
                }
            }
        }

        static IEnumerable<IMessage> GetNewMessages(int count, int indexStart)
        {
            for (int i = 0; i < count; i++)
            {
                yield return new Message(
                    TelemetryMessageSource.Instance,
                    new byte[] { 1, 2, 3 },
                    new Dictionary<string, string>
                    {
                        { $"key{indexStart + i}", $"value{indexStart + i}" }
                    },
                    i + indexStart);
            }
        }

        class CheckpointStore : ICheckpointStore
        {
            readonly Dictionary<string, CheckpointData> checkpointDatas = new Dictionary<string, CheckpointData>();

            public Task<CheckpointData> GetCheckpointDataAsync(string id, CancellationToken token)
            {
                CheckpointData checkpointData = this.checkpointDatas.ContainsKey(id)
                    ? this.checkpointDatas[id]
                    : new CheckpointData(Checkpointer.InvalidOffset);
                return Task.FromResult(checkpointData);
            }

            public Task<IDictionary<string, CheckpointData>> GetAllCheckpointDataAsync(CancellationToken token) => Task.FromResult((IDictionary<string, CheckpointData>)this.checkpointDatas);

            public Task SetCheckpointDataAsync(string id, CheckpointData checkpointData, CancellationToken token)
            {
                this.checkpointDatas[id] = checkpointData;
                return Task.CompletedTask;
            }

            public Task CloseAsync(CancellationToken token) => Task.CompletedTask;
        }

        class TestMessageStore : IMessageStore
        {
            readonly ConcurrentDictionary<string, TestMessageQueue> endpointQueues = new ConcurrentDictionary<string, TestMessageQueue>();

            public void Dispose()
            {
            }

            public async Task<IMessage> Add(string endpointId, IMessage message, uint _)
            {
                TestMessageQueue queue = this.GetQueue(endpointId);
                long offset = await queue.Add(message);
                return new Message(
                    message.MessageSource,
                    message.Body,
                    message.Properties,
                    message.SystemProperties,
                    offset,
                    message.EnqueuedTime,
                    message.DequeuedTime);
            }

            public IMessageIterator GetMessageIterator(string endpointId, long startingOffset) => this.GetQueue(endpointId);

            public IMessageIterator GetMessageIterator(string endpointId) => this.GetQueue(endpointId);

            public Task AddEndpoint(string endpointId)
            {
                this.endpointQueues[endpointId] = new TestMessageQueue();
                return Task.CompletedTask;
            }

            public Task RemoveEndpoint(string endpointId)
            {
                this.endpointQueues.Remove(endpointId, out TestMessageQueue _);
                return Task.CompletedTask;
            }

            public void SetTimeToLive(TimeSpan timeToLive) => throw new NotImplementedException();

            public List<IMessage> GetReceivedMessagesForEndpoint(string endpointId) => this.GetQueue(endpointId).Queue;

            TestMessageQueue GetQueue(string endpointId) => this.endpointQueues.GetOrAdd(endpointId, new TestMessageQueue());

            class TestMessageQueue : IMessageIterator
            {
                readonly List<IMessage> queue = new List<IMessage>();
                readonly AsyncLock queueLock = new AsyncLock();
                int index;

                public List<IMessage> Queue => this.queue;

                public async Task<long> Add(IMessage message)
                {
                    using (await this.queueLock.LockAsync())
                    {
                        this.queue.Add(message);
                        return (long)this.queue.Count - 1;
                    }
                }

                public async Task<IEnumerable<IMessage>> GetNext(int batchSize)
                {
                    using (await this.queueLock.LockAsync())
                    {
                        var batch = new List<IMessage>();
                        for (int i = 0; i < batchSize && this.index < this.queue.Count; i++, this.index++)
                        {
                            batch.Add(this.queue[this.index]);
                        }

                        return batch;
                    }
                }
            }
        }
    }
}
