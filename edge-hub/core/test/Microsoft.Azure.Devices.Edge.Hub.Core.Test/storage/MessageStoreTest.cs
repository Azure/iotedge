// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Storage;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Routing.Core.Checkpointers;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Xunit;
    using SystemProperties = Microsoft.Azure.Devices.Edge.Hub.Core.SystemProperties;

    [Integration]
    public class MessageStoreTest
    {
        [Theory]
        [InlineData(0, false)]
        [InlineData(0, true)]
        [InlineData(10150, false)]
        [InlineData(10150, true)]
        [InlineData(-1, false)]
        [InlineData(-1, true)]
        public async Task BasicTest(long initialCheckpointOffset, bool checkEntireQueueOnCleanup)
        {
            (IMessageStore messageStore, ICheckpointStore checkpointStore) result = await this.GetMessageStore(initialCheckpointOffset, checkEntireQueueOnCleanup);
            using (IMessageStore messageStore = result.messageStore)
            {
                for (int i = 0; i < 10000; i++)
                {
                    if (i % 2 == 0)
                    {
                        IMessage input = this.GetMessage(i);
                        IMessage updatedMessage = await messageStore.Add("module1", input, 0);
                        CompareUpdatedMessageWithOffset(input, initialCheckpointOffset + 1 + i / 2, updatedMessage);
                    }
                    else
                    {
                        IMessage input = this.GetMessage(i);
                        IMessage updatedMessage = await messageStore.Add("module2", input, 0);
                        CompareUpdatedMessageWithOffset(input, initialCheckpointOffset + 1 + i / 2, updatedMessage);
                    }
                }

                IMessageIterator module1Iterator = messageStore.GetMessageIterator("module1");
                Assert.NotNull(module1Iterator);
                IMessageIterator module2Iterator = messageStore.GetMessageIterator("module2");
                Assert.NotNull(module2Iterator);

                for (int i = 0; i < 5; i++)
                {
                    IEnumerable<IMessage> batch = await module1Iterator.GetNext(1000);
                    IEnumerable<IMessage> batchItemsAsList = batch as IList<IMessage> ?? batch.ToList();
                    Assert.Equal(1000, batchItemsAsList.Count());
                    for (int j = 0; j < 1000; j++)
                    {
                        Assert.Equal((((i * 1000) + j) * 2).ToString(), batchItemsAsList.ElementAt(j).SystemProperties[SystemProperties.MessageId]);
                    }
                }

                for (int i = 0; i < 5; i++)
                {
                    IEnumerable<IMessage> batch = await module2Iterator.GetNext(1000);
                    IEnumerable<IMessage> batchItemsAsList2 = batch as IList<IMessage> ?? batch.ToList();
                    Assert.Equal(1000, batchItemsAsList2.Count());
                    for (int j = 0; j < 1000; j++)
                    {
                        Assert.Equal((((i * 1000) + j) * 2 + 1).ToString(), batchItemsAsList2.ElementAt(j).SystemProperties[SystemProperties.MessageId]);
                    }
                }
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CleanupTestTimeout(bool checkEntireQueueOnCleanup)
        {
            (IMessageStore messageStore, ICheckpointStore checkpointStore, InMemoryDbStore _) result = await this.GetMessageStore(checkEntireQueueOnCleanup, 20);
            using (IMessageStore messageStore = result.messageStore)
            {
                for (int i = 0; i < 200; i++)
                {
                    if (i % 2 == 0)
                    {
                        IMessage input = this.GetMessage(i);
                        IMessage updatedMessage = await messageStore.Add("module1", input, 0);
                        CompareUpdatedMessageWithOffset(input, i / 2, updatedMessage);
                    }
                    else
                    {
                        IMessage input = this.GetMessage(i);
                        IMessage updatedMessage = await messageStore.Add("module2", input, 0);
                        CompareUpdatedMessageWithOffset(input, i / 2, updatedMessage);
                    }
                }

                IMessageIterator module1Iterator = messageStore.GetMessageIterator("module1");
                IEnumerable<IMessage> batch = await module1Iterator.GetNext(100);
                Assert.Equal(100, batch.Count());

                IMessageIterator module2Iterator = messageStore.GetMessageIterator("module2");
                batch = await module2Iterator.GetNext(100);
                Assert.Equal(100, batch.Count());

                await Task.Delay(TimeSpan.FromSeconds(100));

                module1Iterator = messageStore.GetMessageIterator("module1");
                batch = await module1Iterator.GetNext(100);
                Assert.Empty(batch);

                module2Iterator = messageStore.GetMessageIterator("module2");
                batch = await module2Iterator.GetNext(100);
                Assert.Empty(batch);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CleanupTestTimeoutWithRead(bool checkEntireQueueOnCleanup)
        {
            (IMessageStore messageStore, ICheckpointStore checkpointStore, InMemoryDbStore _) result = await this.GetMessageStore(checkEntireQueueOnCleanup, 20);
            using (IMessageStore messageStore = result.messageStore)
            {
                for (int i = 0; i < 200; i++)
                {
                    if (i % 2 == 0)
                    {
                        IMessage input = this.GetMessage(i);
                        IMessage updatedMessage = await messageStore.Add("module1", input, 0);
                        CompareUpdatedMessageWithOffset(input, i / 2, updatedMessage);
                    }
                    else
                    {
                        IMessage input = this.GetMessage(i);
                        IMessage updatedMessage = await messageStore.Add("module2", input, 0);
                        CompareUpdatedMessageWithOffset(input, i / 2, updatedMessage);
                    }
                }

                IMessageIterator module1Iterator = messageStore.GetMessageIterator("module1");
                IEnumerable<IMessage> batch = await module1Iterator.GetNext(100);
                Assert.Equal(100, batch.Count());

                IMessageIterator module2Iterator = messageStore.GetMessageIterator("module2");
                batch = await module2Iterator.GetNext(100);
                Assert.Equal(100, batch.Count());

                await Task.Delay(TimeSpan.FromSeconds(100));

                for (int i = 200; i < 250; i++)
                {
                    if (i % 2 == 0)
                    {
                        IMessage input = this.GetMessage(i);
                        IMessage updatedMessage = await messageStore.Add("module1", input, 0);
                        CompareUpdatedMessageWithOffset(input, i / 2, updatedMessage);
                    }
                    else
                    {
                        IMessage input = this.GetMessage(i);
                        IMessage updatedMessage = await messageStore.Add("module2", input, 0);
                        CompareUpdatedMessageWithOffset(input, i / 2, updatedMessage);
                    }
                }

                module1Iterator = messageStore.GetMessageIterator("module1");
                batch = await module1Iterator.GetNext(100);
                Assert.Equal(25, batch.Count());

                module2Iterator = messageStore.GetMessageIterator("module2");
                batch = await module2Iterator.GetNext(100);
                Assert.Equal(25, batch.Count());
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CleanupTestCheckpointed(bool checkEntireQueueOnCleanup)
        {
            (IMessageStore messageStore, ICheckpointStore checkpointStore, InMemoryDbStore _) result = await this.GetMessageStore(checkEntireQueueOnCleanup, 20);
            ICheckpointStore checkpointStore = result.checkpointStore;
            using (IMessageStore messageStore = result.messageStore)
            {
                for (int i = 0; i < 200; i++)
                {
                    if (i % 2 == 0)
                    {
                        IMessage input = this.GetMessage(i);
                        IMessage updatedMessage = await messageStore.Add("module1", input, 0);
                        CompareUpdatedMessageWithOffset(input, i / 2, updatedMessage);
                    }
                    else
                    {
                        IMessage input = this.GetMessage(i);
                        IMessage updatedMessage = await messageStore.Add("module2", input, 0);
                        CompareUpdatedMessageWithOffset(input, i / 2, updatedMessage);
                    }
                }

                IMessageIterator module1Iterator = messageStore.GetMessageIterator("module1");
                IEnumerable<IMessage> batch = await module1Iterator.GetNext(100);
                Assert.Equal(100, batch.Count());

                IMessageIterator module2Iterator = messageStore.GetMessageIterator("module2");
                batch = await module2Iterator.GetNext(100);
                Assert.Equal(100, batch.Count());

                await checkpointStore.SetCheckpointDataAsync("module1", new CheckpointData(198), CancellationToken.None);
                await checkpointStore.SetCheckpointDataAsync("module2", new CheckpointData(199), CancellationToken.None);
                await Task.Delay(TimeSpan.FromSeconds(100));

                module2Iterator = messageStore.GetMessageIterator("module2");
                batch = await module2Iterator.GetNext(100);
                Assert.Empty(batch);

                module1Iterator = messageStore.GetMessageIterator("module1");
                batch = await module1Iterator.GetNext(100);
                Assert.Empty(batch);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CleanupTestTimeoutMultipleTTLs(bool checkEntireQueueOnCleanup)
        {
            (IMessageStore messageStore, ICheckpointStore checkpointStore, InMemoryDbStore inMemoryDbStore) result = await this.GetMessageStore(checkEntireQueueOnCleanup, 10);
            using (IMessageStore messageStore = result.messageStore)
            {
                var messageIdsAlive = new List<string>();
                var messageIdsExpired = new List<string>();
                for (int i = 0; i < 200; i++)
                {
                    IMessage input = this.GetMessage(i);
                    string edgeMessageId = input.SystemProperties[SystemProperties.EdgeMessageId];
                    uint timeToLiveSecs = 1000;
                    if (i % 2 == 0)
                    {
                        messageIdsExpired.Add(edgeMessageId);
                        timeToLiveSecs = 10;
                    }
                    else
                    {
                        messageIdsAlive.Add(edgeMessageId);
                    }

                    IMessage updatedMessage = await messageStore.Add("module1", input, timeToLiveSecs);
                    CompareUpdatedMessageWithOffset(input, i, updatedMessage);
                }

                for (int i = 0; i < messageIdsExpired.Count; i++)
                {
                    if (checkEntireQueueOnCleanup || i == 0)
                    {
                        int retryAttempts = 0;
                        while (await result.inMemoryDbStore.Contains(messageIdsExpired[i].ToBytes()))
                        {
                            Assert.True(retryAttempts < 10, "Test is taking too long and is considered a failure.");
                            retryAttempts++;
                            await Task.Delay(TimeSpan.FromSeconds(10));
                        }

                        Assert.False(await result.inMemoryDbStore.Contains(messageIdsExpired[i].ToBytes()));
                    }
                    else
                    {
                        Assert.True(await result.inMemoryDbStore.Contains(messageIdsExpired[i].ToBytes()));
                    }
                }

                IMessageIterator module1Iterator = messageStore.GetMessageIterator("module1");
                IEnumerable<IMessage> batch = await module1Iterator.GetNext(200);
                Assert.Equal(100, batch.Count());
                foreach (string edgeMessageId in messageIdsAlive)
                {
                    Assert.True(await result.inMemoryDbStore.Contains(edgeMessageId.ToBytes()));
                }
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CleanupTestTimeoutUpdateGlobalTimeToLive(bool checkEntireQueueOnCleanup)
        {
            (IMessageStore messageStore, ICheckpointStore checkpointStore, InMemoryDbStore _) result = await this.GetMessageStore(checkEntireQueueOnCleanup, 20);
            result.messageStore.SetTimeToLive(TimeSpan.FromSeconds(20));
            using (IMessageStore messageStore = result.messageStore)
            {
                for (int i = 0; i < 200; i++)
                {
                    if (i % 2 == 0)
                    {
                        IMessage input = this.GetMessage(i);
                        IMessage updatedMessage = await messageStore.Add("module1", input, 0);
                        CompareUpdatedMessageWithOffset(input, i / 2, updatedMessage);
                    }
                    else
                    {
                        IMessage input = this.GetMessage(i);
                        IMessage updatedMessage = await messageStore.Add("module2", input, 0);
                        CompareUpdatedMessageWithOffset(input, i / 2, updatedMessage);
                    }
                }

                IMessageIterator module1Iterator = messageStore.GetMessageIterator("module1");
                IEnumerable<IMessage> batch = await module1Iterator.GetNext(100);
                Assert.Equal(100, batch.Count());

                IMessageIterator module2Iterator = messageStore.GetMessageIterator("module2");
                batch = await module2Iterator.GetNext(100);
                Assert.Equal(100, batch.Count());

                await Task.Delay(TimeSpan.FromSeconds(100));

                module1Iterator = messageStore.GetMessageIterator("module1");
                batch = await module1Iterator.GetNext(100);
                Assert.Empty(batch);

                module2Iterator = messageStore.GetMessageIterator("module2");
                batch = await module2Iterator.GetNext(100);
                Assert.Empty(batch);

                result.messageStore.SetTimeToLive(TimeSpan.FromSeconds(2000));
                await Task.Delay(TimeSpan.FromSeconds(50));

                for (int i = 0; i < 200; i++)
                {
                    if (i % 2 == 0)
                    {
                        IMessage input = this.GetMessage(i);
                        IMessage updatedMessage = await messageStore.Add("module1", input, 0);
                        CompareUpdatedMessageWithOffset(input, 100 + i / 2, updatedMessage);
                    }
                    else
                    {
                        IMessage input = this.GetMessage(i);
                        IMessage updatedMessage = await messageStore.Add("module2", input, 0);
                        CompareUpdatedMessageWithOffset(input, 100 + i / 2, updatedMessage);
                    }
                }

                module1Iterator = messageStore.GetMessageIterator("module1");
                batch = await module1Iterator.GetNext(100);
                Assert.Equal(100, batch.Count());

                module2Iterator = messageStore.GetMessageIterator("module2");
                batch = await module2Iterator.GetNext(100);
                Assert.Equal(100, batch.Count());

                await Task.Delay(TimeSpan.FromSeconds(100));

                module1Iterator = messageStore.GetMessageIterator("module1");
                batch = await module1Iterator.GetNext(100);
                Assert.Equal(100, batch.Count());

                module2Iterator = messageStore.GetMessageIterator("module2");
                batch = await module2Iterator.GetNext(100);
                Assert.Equal(100, batch.Count());
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CleanupTestTimeoutUpdateIndividualMessageTimeToLive(bool checkEntireQueueOnCleanup)
        {
            (IMessageStore messageStore, ICheckpointStore checkpointStore, InMemoryDbStore _) result = await this.GetMessageStore(checkEntireQueueOnCleanup, 20);
            result.messageStore.SetTimeToLive(TimeSpan.FromSeconds(20));
            using (IMessageStore messageStore = result.messageStore)
            {
                for (int i = 0; i < 200; i++)
                {
                    if (i % 2 == 0)
                    {
                        IMessage input = this.GetMessage(i);
                        IMessage updatedMessage = await messageStore.Add("module1", input, 20);
                        CompareUpdatedMessageWithOffset(input, i / 2, updatedMessage);
                    }
                    else
                    {
                        IMessage input = this.GetMessage(i);
                        IMessage updatedMessage = await messageStore.Add("module2", input, 20);
                        CompareUpdatedMessageWithOffset(input, i / 2, updatedMessage);
                    }
                }

                IMessageIterator module1Iterator = messageStore.GetMessageIterator("module1");
                IEnumerable<IMessage> batch = await module1Iterator.GetNext(100);
                Assert.Equal(100, batch.Count());

                IMessageIterator module2Iterator = messageStore.GetMessageIterator("module2");
                batch = await module2Iterator.GetNext(100);
                Assert.Equal(100, batch.Count());

                await Task.Delay(TimeSpan.FromSeconds(100));

                module1Iterator = messageStore.GetMessageIterator("module1");
                batch = await module1Iterator.GetNext(100);
                Assert.Empty(batch);

                module2Iterator = messageStore.GetMessageIterator("module2");
                batch = await module2Iterator.GetNext(100);
                Assert.Empty(batch);

                // By setting the global TTL for the MessageStore to 20, the CleanupProcessor will run every 10 seconds
                // But it won't clean up any messages, since the individual messages are set to have TTL of 2000 seconds
                result.messageStore.SetTimeToLive(TimeSpan.FromSeconds(20));
                await Task.Delay(TimeSpan.FromSeconds(50));

                for (int i = 0; i < 200; i++)
                {
                    if (i % 2 == 0)
                    {
                        IMessage input = this.GetMessage(i);
                        IMessage updatedMessage = await messageStore.Add("module1", input, 2000);
                        CompareUpdatedMessageWithOffset(input, 100 + i / 2, updatedMessage);
                    }
                    else
                    {
                        IMessage input = this.GetMessage(i);
                        IMessage updatedMessage = await messageStore.Add("module2", input, 50);
                        CompareUpdatedMessageWithOffset(input, 100 + i / 2, updatedMessage);
                    }
                }

                module1Iterator = messageStore.GetMessageIterator("module1");
                batch = await module1Iterator.GetNext(100);
                Assert.Equal(100, batch.Count());

                module2Iterator = messageStore.GetMessageIterator("module2");
                batch = await module2Iterator.GetNext(100);
                Assert.Equal(100, batch.Count());

                await Task.Delay(TimeSpan.FromSeconds(100));

                module1Iterator = messageStore.GetMessageIterator("module1", 100);
                batch = await module1Iterator.GetNext(100);
                Assert.Equal(100, batch.Count());

                module2Iterator = messageStore.GetMessageIterator("module2", 100);
                batch = await module2Iterator.GetNext(100);
                Assert.Empty(batch);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task MessageStoreAddRemoveEndpointTest(bool checkEntireQueueOnCleanup)
        {
            // Arrange
            var dbStoreProvider = new InMemoryDbStoreProvider();
            IStoreProvider storeProvider = new StoreProvider(dbStoreProvider);
            ICheckpointStore checkpointStore = CheckpointStore.Create(storeProvider);
            IMessageStore messageStore = new MessageStore(storeProvider, checkpointStore, TimeSpan.FromHours(1), checkEntireQueueOnCleanup, 1800);

            // Act
            await messageStore.AddEndpoint("module1");

            for (int i = 0; i < 10; i++)
            {
                await messageStore.Add("module1", this.GetMessage(i), 0);
            }

            // Assert
            IMessageIterator module1Iterator = messageStore.GetMessageIterator("module1");
            Assert.NotNull(module1Iterator);

            IEnumerable<IMessage> batch = await module1Iterator.GetNext(1000);
            List<IMessage> batchItemsAsList = batch.ToList();
            Assert.Equal(10, batchItemsAsList.Count);

            for (int i = 0; i < 10; i++)
            {
                Assert.Equal($"{i}", batchItemsAsList.ElementAt(i).SystemProperties[SystemProperties.MessageId]);
            }

            // Remove
            await messageStore.RemoveEndpoint("module1");

            // Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => messageStore.Add("module1", this.GetMessage(0), 0));
            Assert.Throws<InvalidOperationException>(() => messageStore.GetMessageIterator("module1"));

            // Act
            await messageStore.AddEndpoint("module1");

            for (int i = 20; i < 30; i++)
            {
                await messageStore.Add("module1", this.GetMessage(i), 0);
            }

            // Assert
            module1Iterator = messageStore.GetMessageIterator("module1");
            Assert.NotNull(module1Iterator);

            batch = await module1Iterator.GetNext(1000);
            batchItemsAsList = batch.ToList();
            Assert.Equal(10, batchItemsAsList.Count);

            for (int i = 20; i < 30; i++)
            {
                Assert.Equal($"{i}", batchItemsAsList.ElementAt(i - 20).SystemProperties[SystemProperties.MessageId]);
            }
        }

        [Fact]
        public void MessageWrapperRoundtripTest()
        {
            IDictionary<string, string> properties = new Dictionary<string, string>
            {
                ["Prop1"] = "PropVal1",
                ["Prop2"] = "PropVal2"
            };

            IDictionary<string, string> systemProperties = new Dictionary<string, string>
            {
                [Devices.Routing.Core.SystemProperties.CorrelationId] = Guid.NewGuid().ToString(),
                [Devices.Routing.Core.SystemProperties.DeviceId] = "device1",
                [Devices.Routing.Core.SystemProperties.MessageId] = Guid.NewGuid().ToString()
            };

            byte[] body = "Test Message Body".ToBody();
            var enqueueTime = new DateTime(2017, 11, 20, 01, 02, 03);
            var dequeueTime = new DateTime(2017, 11, 20, 02, 03, 04);

            IMessage message = new Message(
                TelemetryMessageSource.Instance,
                body,
                properties,
                systemProperties,
                100,
                enqueueTime,
                dequeueTime);
            var messageWrapper = new MessageStore.MessageWrapper(message, DateTime.UtcNow, 3);

            byte[] messageWrapperBytes = messageWrapper.ToBytes();
            var retrievedMesssageWrapper = messageWrapperBytes.FromBytes<MessageStore.MessageWrapper>();

            Assert.NotNull(retrievedMesssageWrapper);
            Assert.Equal(messageWrapper.TimeStamp, retrievedMesssageWrapper.TimeStamp);
            Assert.Equal(messageWrapper.RefCount, retrievedMesssageWrapper.RefCount);
            Assert.Equal(messageWrapper.Message, retrievedMesssageWrapper.Message);
        }

        IMessage GetMessage(int i)
        {
            return new Message(
                TelemetryMessageSource.Instance,
                $"Test Message {i} Body".ToBody(),
                new Dictionary<string, string>(),
                new Dictionary<string, string>
                {
                    [SystemProperties.EdgeMessageId] = Guid.NewGuid().ToString(),
                    [SystemProperties.MessageId] = i.ToString()
                });
        }

        async Task<(IMessageStore, ICheckpointStore, InMemoryDbStore)> GetMessageStore(bool checkEntireQueueOnCleanup, int ttlSecs = 300, int messageCleanupIntervalSecs = 30)
        {
            var dbStoreProvider = new InMemoryDbStoreProvider();
            IStoreProvider storeProvider = new StoreProvider(dbStoreProvider);
            InMemoryDbStore inMemoryDbStore = dbStoreProvider.GetDbStore("messages") as InMemoryDbStore;
            ICheckpointStore checkpointStore = CheckpointStore.Create(storeProvider);
            IMessageStore messageStore = new MessageStore(storeProvider, checkpointStore, TimeSpan.FromSeconds(ttlSecs), checkEntireQueueOnCleanup, messageCleanupIntervalSecs);
            await messageStore.AddEndpoint("module1");
            await messageStore.AddEndpoint("module2");
            return (messageStore, checkpointStore, inMemoryDbStore);
        }

        async Task<(IMessageStore, ICheckpointStore)> GetMessageStore(long initialCheckpointOffset, bool checkEntireQueueOnCleanup, int ttlSecs = 300)
        {
            var dbStoreProvider = new InMemoryDbStoreProvider();
            IStoreProvider storeProvider = new StoreProvider(dbStoreProvider);

            IEntityStore<string, CheckpointStore.CheckpointEntity> checkpointUnderlyingStore = storeProvider.GetEntityStore<string, CheckpointStore.CheckpointEntity>($"Checkpoint{Guid.NewGuid().ToString()}");
            if (initialCheckpointOffset >= 0)
            {
                await checkpointUnderlyingStore.Put("module1", new CheckpointStore.CheckpointEntity(initialCheckpointOffset, null, null));
                await checkpointUnderlyingStore.Put("module2", new CheckpointStore.CheckpointEntity(initialCheckpointOffset, null, null));
            }

            ICheckpointStore checkpointStore = new CheckpointStore(checkpointUnderlyingStore);
            IMessageStore messageStore = new MessageStore(storeProvider, checkpointStore, TimeSpan.FromSeconds(ttlSecs), checkEntireQueueOnCleanup, 1800);
            await messageStore.AddEndpoint("module1");
            await messageStore.AddEndpoint("module2");
            return (messageStore, checkpointStore);
        }

        static void CompareUpdatedMessageWithOffset(IMessage originalMessage, long offset, IMessage updatedMessage)
        {
            Assert.NotNull(updatedMessage);
            Assert.Equal(originalMessage.Body, updatedMessage.Body);
            Assert.Equal(originalMessage.MessageSource, updatedMessage.MessageSource);
            Assert.Equal(originalMessage.Properties, updatedMessage.Properties);
            Assert.Equal(originalMessage.SystemProperties, updatedMessage.SystemProperties);
            Assert.Equal(offset, updatedMessage.Offset);
        }
    }
}
