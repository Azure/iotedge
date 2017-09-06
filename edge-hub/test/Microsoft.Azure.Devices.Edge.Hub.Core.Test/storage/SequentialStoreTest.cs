// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Storage
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Storage;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class SequentialStoreTest
    {        
        [Fact]
        public async Task CreateTest()
        {
            var entityStore = new EntityStore<byte[], Item>(new InMemoryDbStore());
            var sequentialStore = await SequentialStore<Item>.Create(entityStore);
            long offset = await sequentialStore.Append(new Item { Prop1 = 10 });
            Assert.Equal(0, offset);

            sequentialStore = await SequentialStore<Item>.Create(entityStore);
            offset = await sequentialStore.Append(new Item { Prop1 = 20 });
            Assert.Equal(1, offset);
        }

        [Fact]
        public async Task BasicTest()
        {
            using(var dbStoreProvider = new InMemoryDbStoreProvider())
            {
                var entityStore = new EntityStore<byte[], Item>(dbStoreProvider.GetDbStore());
                var sequentialStore = await SequentialStore<Item>.Create(entityStore);
                var tasks = new List<Task>();
                for (int i = 0; i < 10; i++)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        for (int j = 0; j < 1000; j++)
                        {
                            long offset = await sequentialStore.Append(new Item { Prop1 = i * 10 + j });
                            Assert.True(offset <= 10000);
                        }
                    }));
                }
                await Task.WhenAll(tasks);

                IEnumerable<(long offset, Item item)> batch = await sequentialStore.GetBatch(0, 10000);
                Assert.Equal(10000, batch.Count());
                int counter = 0;
                foreach ((long offset, Item item) batchItem in batch)
                {
                    Assert.Equal(counter++, batchItem.offset);
                }
            }
        }        

        class Item
        {
            public long Prop1 { get; set; }
        }
    }
}
