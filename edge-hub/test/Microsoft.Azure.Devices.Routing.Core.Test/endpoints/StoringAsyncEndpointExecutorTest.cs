// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Endpoints
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Routing.Core.Checkpointers;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
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
            var endpoint = new NullEndpoint(EndpointId);
            var checkpointer = new NullCheckpointer();
            var endpointExecutorConfig = new EndpointExecutorConfig(TimeSpan.FromHours(1), RetryStrategy.NoRetry, TimeSpan.FromHours(1));
            var asyncEndpointExecutorOptions = new AsyncEndpointExecutorOptions(10);
            var messageStore = new TestMessageStore();
            var storingAsyncEndpointExecutor = new StoringAsyncEndpointExecutor(endpoint, checkpointer, endpointExecutorConfig, asyncEndpointExecutorOptions, messageStore);
            IEnumerable<IMessage> messages = GetNewMessages(MessagesCount, 0);

            // Act - Send messages to invoke
            foreach (IMessage message in messages)
            {
                await storingAsyncEndpointExecutor.Invoke(message);
            }

            // Assert - Check that the message store received the messages sent to invoke.
            List<IMessage> storeMessages = messageStore.GetReceivedMessagesForEndpoint(EndpointId);
            Assert.NotNull(storeMessages);
            Assert.Equal(MessagesCount, storeMessages.Count);
            for (int i = 0; i < MessagesCount; i++)
            {
                IMessage message = storeMessages[i];
                Assert.True(message.Properties.ContainsKey($"key{i}"));
                Assert.Equal($"value{i}", message.Properties[$"key{i}"]);
            }

            // Assert - Make sure no additional / duplicate messages were sent.
            storeMessages = messageStore.GetReceivedMessagesForEndpoint(EndpointId);
            Assert.NotNull(storeMessages);
            Assert.Equal(10, storeMessages.Count);

            // Act - Send messages again to Invoke.
            messages = GetNewMessages(MessagesCount, MessagesCount);
            foreach (IMessage message in messages)
            {
                await storingAsyncEndpointExecutor.Invoke(message);
            }

            // Assert - Make sure the store now has the old and the new messages.
            storeMessages = messageStore.GetReceivedMessagesForEndpoint(EndpointId);
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
            ICheckpointer checkpointer = await Checkpointer.CreateAsync(EndpointId, new CheckpointStore());
            var endpointExecutorConfig = new EndpointExecutorConfig(TimeSpan.FromHours(1), RetryStrategy.NoRetry, TimeSpan.FromHours(1));
            var asyncEndpointExecutorOptions = new AsyncEndpointExecutorOptions(4, TimeSpan.FromSeconds(2));
            var messageStore = new TestMessageStore();
            var storingAsyncEndpointExecutor = new StoringAsyncEndpointExecutor(endpoint, checkpointer, endpointExecutorConfig, asyncEndpointExecutorOptions, messageStore);
            IEnumerable<IMessage> messages = GetNewMessages(MessagesCount, 0);

            // Act - Send messages to invoke
            foreach (IMessage message in messages)
            {
                await storingAsyncEndpointExecutor.Invoke(message);
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
            ICheckpointer checkpointer = await Checkpointer.CreateAsync(EndpointId, new CheckpointStore());
            var endpointExecutorConfig = new EndpointExecutorConfig(TimeSpan.FromHours(1), RetryStrategy.NoRetry, TimeSpan.FromHours(1));
            var asyncEndpointExecutorOptions = new AsyncEndpointExecutorOptions(RoutingPumpBatchSize);
            var messageStore = new TestMessageStore();
            var storingAsyncEndpointExecutor = new StoringAsyncEndpointExecutor(endpoint, checkpointer, endpointExecutorConfig, asyncEndpointExecutorOptions, messageStore);
            IEnumerable<IMessage> messages = GetNewMessages(MessagesCount, 0);

            // Act - Send messages to invoke
            foreach (IMessage message in messages)
            {
                await storingAsyncEndpointExecutor.Invoke(message);
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
        public async Task TestSetEndpoint()
        {
            var endpoint1 = new TestEndpoint("id");
            var endpoint2 = new NullEndpoint("id");
            var endpoint3 = new TestEndpoint("id1");
            ICheckpointer checkpointer = await Checkpointer.CreateAsync("cid", new CheckpointStore());
            var endpointExecutorConfig = new EndpointExecutorConfig(TimeSpan.FromHours(1), RetryStrategy.NoRetry, TimeSpan.FromHours(1));
            var asyncEndpointExecutorOptions = new AsyncEndpointExecutorOptions(1, TimeSpan.FromSeconds(1));
            var messageStore = new TestMessageStore();
            var executor = new StoringAsyncEndpointExecutor(endpoint1, checkpointer, endpointExecutorConfig, asyncEndpointExecutorOptions, messageStore);

            Assert.Equal(endpoint1, executor.Endpoint);
            await Assert.ThrowsAsync<ArgumentNullException>(() => executor.SetEndpoint(null));
            await Assert.ThrowsAsync<ArgumentException>(() => executor.SetEndpoint(endpoint3));

            await executor.SetEndpoint(endpoint2);
            Assert.Equal(endpoint2, executor.Endpoint);

            await executor.CloseAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => executor.SetEndpoint(endpoint1));
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

            public Task<long> Add(string endpointId, IMessage message)
            {
                TestMessageQueue queue = this.GetQueue(endpointId);
                return queue.Add(message);
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
