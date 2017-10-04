// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Storage;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Routing.Core.Checkpointers;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Moq;
    using Xunit;

    [Bvt]
    public class MessageStoreTest
    {
        [Fact]
        public async Task BasicTest()
        {
            (IMessageStore messageStore, ICheckpointStore checkpointStore) result = await this.GetMessageStore();
            using (IMessageStore messageStore = result.messageStore)
            {
                for (int i = 0; i < 10000; i++)
                {
                    if (i % 2 == 0)
                    {
                        long offset = await messageStore.Add("module1", this.GetMessage(i));
                        Assert.Equal(i / 2, offset);
                    }
                    else
                    {
                        long offset = await messageStore.Add("module2", this.GetMessage(i));
                        Assert.Equal(i / 2, offset);
                    }
                }

                IMessageIterator module1Iterator = messageStore.GetMessageIterator("module1");
                Assert.NotNull(module1Iterator);
                IMessageIterator module2Iterator = messageStore.GetMessageIterator("module2");
                Assert.NotNull(module2Iterator);

                for (int i = 0; i < 5; i++)
                {
                    IEnumerable<IMessage> batch = await module1Iterator.GetNext(1000);
                    Assert.Equal(1000, batch.Count());
                    for (int j = 0; j < 1000; j++)
                    {
                        Assert.Equal((((i * 1000) + j) * 2).ToString(), batch.ElementAt(j).SystemProperties[Core.SystemProperties.MessageId]);
                    }
                }

                for (int i = 0; i < 5; i++)
                {
                    IEnumerable<IMessage> batch = await module2Iterator.GetNext(1000);
                    Assert.Equal(1000, batch.Count());
                    for (int j = 0; j < 1000; j++)
                    {
                        Assert.Equal((((i * 1000) + j) * 2 + 1).ToString(), batch.ElementAt(j).SystemProperties[Core.SystemProperties.MessageId]);
                    }
                }
            }
        }

        [Fact]
        public async Task CleanupTestTimeout()
        {
            (IMessageStore messageStore, ICheckpointStore checkpointStore) result = await this.GetMessageStore(20);
            using (IMessageStore messageStore = result.messageStore)
            {
                for (int i = 0; i < 200; i++)
                {
                    if (i % 2 == 0)
                    {
                        long offset = await messageStore.Add("module1", this.GetMessage(i));
                        Assert.Equal(i / 2, offset);
                    }
                    else
                    {
                        long offset = await messageStore.Add("module2", this.GetMessage(i));
                        Assert.Equal(i / 2, offset);
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
                Assert.Equal(0, batch.Count());

                module2Iterator = messageStore.GetMessageIterator("module2");
                batch = await module2Iterator.GetNext(100);
                Assert.Equal(0, batch.Count());
            }
        }

        [Fact]
        public async Task CleanupTestCheckpointed()
        {
            (IMessageStore messageStore, ICheckpointStore checkpointStore) result = await this.GetMessageStore(20);
            ICheckpointStore checkpointStore = result.checkpointStore;
            using (IMessageStore messageStore = result.messageStore)
            {
                for (int i = 0; i < 200; i++)
                {
                    if (i % 2 == 0)
                    {
                        long offset = await messageStore.Add("module1", this.GetMessage(i));
                        Assert.Equal(i / 2, offset);
                    }
                    else
                    {
                        long offset = await messageStore.Add("module2", this.GetMessage(i));
                        Assert.Equal(i / 2, offset);
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
                Assert.Equal(0, batch.Count());

                module1Iterator = messageStore.GetMessageIterator("module1");
                batch = await module1Iterator.GetNext(100);
                Assert.Equal(0, batch.Count());
            }
        }

        [Fact]
        public async Task CleanupTestCheckpointAndTimeout()
        {
            (IMessageStore messageStore, ICheckpointStore checkpointStore) result = await this.GetMessageStore(80);
            ICheckpointStore checkpointStore = result.checkpointStore;
            using (IMessageStore messageStore = result.messageStore)
            {
                for (int i = 0; i < 100; i++)
                {
                    await messageStore.Add("module1", this.GetMessage(i));
                }

                await Task.Delay(TimeSpan.FromSeconds(10));

                for (int i = 0; i < 100; i++)
                {
                    await messageStore.Add("module2", this.GetMessage(i));
                }

                IMessageIterator module2Iterator = messageStore.GetMessageIterator("module2");
                IEnumerable<IMessage> batch = await module2Iterator.GetNext(100);
                Assert.Equal(100, batch.Count());

                IMessageIterator module1Iterator = messageStore.GetMessageIterator("module1");
                batch = await module1Iterator.GetNext(100);
                Assert.Equal(100, batch.Count());

                await checkpointStore.SetCheckpointDataAsync("module2", new CheckpointData(99), CancellationToken.None);
                await Task.Delay(TimeSpan.FromSeconds(80));

                module2Iterator = messageStore.GetMessageIterator("module2");
                batch = await module2Iterator.GetNext(100);
                Assert.Equal(0, batch.Count());

                module1Iterator = messageStore.GetMessageIterator("module1");
                batch = await module1Iterator.GetNext(100);
                Assert.Equal(100, batch.Count());

                await Task.Delay(TimeSpan.FromSeconds(80));

                module1Iterator = messageStore.GetMessageIterator("module1");
                batch = await module1Iterator.GetNext(100);
                Assert.Equal(0, batch.Count());
            }
        }

        IMessage GetMessage(int i)
        {
            return new Message(TelemetryMessageSource.Instance,
                $"Test Message {i} Body".ToBody(),
                new Dictionary<string, string>(),
                new Dictionary<string, string>
                {
                    [Core.SystemProperties.EdgeMessageId] = Guid.NewGuid().ToString(),
                    [Core.SystemProperties.MessageId] = i.ToString()
                });
        }

        async Task<(IMessageStore, ICheckpointStore)> GetMessageStore(int ttlSecs = 300)
        {
            var dbStoreProvider = new InMemoryDbStoreProvider();
            IStoreProvider storeProvider = new StoreProvider(dbStoreProvider);
            ICheckpointStore checkpointStore = CheckpointStore.Create(dbStoreProvider);
            IMessageStore messageStore = new MessageStore(storeProvider, checkpointStore, TimeSpan.FromSeconds(ttlSecs));
            await messageStore.AddEndpoint("module1");
            await messageStore.AddEndpoint("module2");
            return (messageStore, checkpointStore);
        }
    }
}
