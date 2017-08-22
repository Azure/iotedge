// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Storage;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Xunit;

    [Unit]
    public class MessageStoreTest
    {
        [Fact]
        public async Task BasicTest()
        {
            using (IMessageStore messageStore = await this.GetMessageStore())
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

        async Task<IMessageStore> GetMessageStore()
        {
            var dbStoreProvider = new InMemoryDbStoreProvider();
            IStoreProvider storeProvider = new StoreProvider(dbStoreProvider);
            IMessageStore messageStore = await MessageStore.CreateAsync(storeProvider, new List<string> { "module1", "module2" });
            Assert.NotNull(messageStore);
            return messageStore;
        }
    }
}
