// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb.Test
{
    using System;
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
            IEnumerable<(long offset, Item item)> batchItems = batch as IList<(long offset, Item item)> ?? batch.ToList();
            Assert.Equal(10000, batchItems.Count());
            int counter = 0;
            foreach ((long offset, Item item) batchItem in batchItems)
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
            IEnumerable<(long offset, Item item)> batchItemsAsList = batch as IList<(long offset, Item item)> ?? batch.ToList();
            Assert.Equal(10, batchItemsAsList.Count());
            int counter = 0;
            foreach ((long offset, Item item) batchItem in batchItemsAsList)
            {
                Assert.Equal(counter++, batchItem.offset);
            }

            await sequentialStore.RemoveFirst((offset, item) => Task.FromResult(item.Prop1 == 0));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sequentialStore.GetBatch(0, 100));

            batch = await sequentialStore.GetBatch(1, 100);
            IEnumerable<(long offset, Item item)> items = batch as IList<(long offset, Item item)> ?? batch.ToList();
            Assert.Equal(9, items.Count());
            counter = 1;
            foreach ((long offset, Item item) batchItem in items)
            {
                Assert.Equal(counter++, batchItem.offset);
            }

            await sequentialStore.RemoveFirst((offset, item) => Task.FromResult(item.Prop1 == 0));
            batch = await sequentialStore.GetBatch(1, 100);
            IEnumerable<(long offset, Item item)> batchItems = batch as IList<(long offset, Item item)> ?? batch.ToList();
            Assert.Equal(9, batchItems.Count());
            counter = 1;
            foreach ((long offset, Item item) batchItem in batchItems)
            {
                Assert.Equal(counter++, batchItem.offset);
            }

            await sequentialStore.RemoveFirst((offset, item) => Task.FromResult(item.Prop1 == 1));
            batch = await sequentialStore.GetBatch(2, 100);
            IEnumerable<(long offset, Item item)> batchAsList = batch as IList<(long offset, Item item)> ?? batch.ToList();
            Assert.Equal(8, batchAsList.Count());
            counter = 2;
            foreach ((long offset, Item item) batchItem in batchAsList)
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
