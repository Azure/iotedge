// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb.Test
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class SequentialStoreTest : IClassFixture<TestRocksDbStoreProvider>
    {
        private readonly TestRocksDbStoreProvider rocksDbStoreProvider;

        public SequentialStoreTest(TestRocksDbStoreProvider rocksDbStoreProvider)
        {
            this.rocksDbStoreProvider = rocksDbStoreProvider;
        }

        [Fact]
        public async Task CreateTest()
        {
            string entityName = "testEntity";
            var entityStore = new EntityStore<byte[], Item>(this.rocksDbStoreProvider.GetColumnStoreFamily(entityName), entityName);
            ISequentialStore<Item> sequentialStore = await SequentialStore<Item>.Create(entityStore);
            long offset = await sequentialStore.Append(new Item { Prop1 = 10 });
            Assert.Equal(0, offset);

            sequentialStore = await SequentialStore<Item>.Create(entityStore);
            offset = await sequentialStore.Append(new Item { Prop1 = 20 });
            Assert.Equal(1, offset);
        }

        [Fact]
        public async Task BasicTest()
        {
            string entityName = "basicTestEntity";
            var entityStore = new EntityStore<byte[], Item>(this.rocksDbStoreProvider.GetColumnStoreFamily(entityName), entityName);
            ISequentialStore<Item> sequentialStore = await SequentialStore<Item>.Create(entityStore);
            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                int i1 = i;
                tasks.Add(Task.Run(async () =>
                {
                    for (int j = 0; j < 1000; j++)
                    {
                        long offset = await sequentialStore.Append(new Item { Prop1 = i1 * 10 + j });
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

        [Fact]
        public async Task RemoveTest()
        {
            string entityName = "removeTestEntity";
            var entityStore = new EntityStore<byte[], Item>(this.rocksDbStoreProvider.GetColumnStoreFamily(entityName), entityName);
            ISequentialStore<Item> sequentialStore = await SequentialStore<Item>.Create(entityStore);
            for (int i = 0; i < 10; i++)
            {
                long offset = await sequentialStore.Append(new Item { Prop1 = i });
                Assert.Equal(i, offset);
            }

            IEnumerable<(long offset, Item item)> batch = await sequentialStore.GetBatch(0, 100);
            Assert.Equal(10, batch.Count());
            int counter = 0;
            foreach ((long offset, Item item) batchItem in batch)
            {
                Assert.Equal(counter++, batchItem.offset);
            }

            await sequentialStore.RemoveFirst((offset, item) => Task.FromResult(item.Prop1 == 0));
            batch = await sequentialStore.GetBatch(0, 100);
            Assert.Equal(9, batch.Count());
            counter = 1;
            foreach ((long offset, Item item) batchItem in batch)
            {
                Assert.Equal(counter++, batchItem.offset);
            }

            await sequentialStore.RemoveFirst((offset, item) => Task.FromResult(item.Prop1 == 0));
            batch = await sequentialStore.GetBatch(0, 100);
            Assert.Equal(9, batch.Count());
            counter = 1;
            foreach ((long offset, Item item) batchItem in batch)
            {
                Assert.Equal(counter++, batchItem.offset);
            }

            await sequentialStore.RemoveFirst((offset, item) => Task.FromResult(item.Prop1 == 1));
            batch = await sequentialStore.GetBatch(0, 100);
            Assert.Equal(8, batch.Count());
            counter = 2;
            foreach ((long offset, Item item) batchItem in batch)
            {
                Assert.Equal(counter++, batchItem.offset);
            }
        }

        class Item
        {
            public long Prop1 { get; set; }
        }
    }
}
