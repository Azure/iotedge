// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Integration]
    public abstract class SequentialStoreTestBase
    {
        [Fact]
        public async Task CreateTest()
        {
            IEntityStore<byte[], Item> entityStore = this.GetEntityStore<Item>($"testEntity{Guid.NewGuid().ToString()}");
            ISequentialStore<Item> sequentialStore = await SequentialStore<Item>.Create(entityStore);
            long offset = await sequentialStore.Append(new Item { Prop1 = 10 });
            Assert.Equal(0, offset);

            sequentialStore = await SequentialStore<Item>.Create(entityStore);
            offset = await sequentialStore.Append(new Item { Prop1 = 20 });
            Assert.Equal(1, offset);
        }

        [Fact]
        public async Task CreateWithOffsetTest()
        {
            IEntityStore<byte[], Item> entityStore = this.GetEntityStore<Item>($"testEntity{Guid.NewGuid().ToString()}");
            ISequentialStore<Item> sequentialStore = await SequentialStore<Item>.Create(entityStore, 10150);
            long offset = await sequentialStore.Append(new Item { Prop1 = 10 });
            Assert.Equal(10150, offset);

            sequentialStore = await SequentialStore<Item>.Create(entityStore);
            for (int i = 0; i < 10; i++)
            {
                offset = await sequentialStore.Append(new Item { Prop1 = 20 });
                Assert.Equal(10151 + i, offset);
            }

            sequentialStore = await SequentialStore<Item>.Create(entityStore);
            offset = await sequentialStore.Append(new Item { Prop1 = 20 });
            Assert.Equal(10161, offset);

            IList<(long, Item)> batch = (await sequentialStore.GetBatch(0, 100)).ToList();
            Assert.Equal(12, batch.Count);
            long expectedOffset = 10150;
            foreach ((long itemOffset, Item _) in batch)
            {
                Assert.Equal(itemOffset, expectedOffset++);
            }
        }

        [Theory]
        [MemberData(nameof(GetDefaultHeadOffset))]
        public async Task BasicTest(Option<long> defaultHeadOffset)
        {
            string entityId = $"basicTest{Guid.NewGuid().ToString()}";
            ISequentialStore<Item> sequentialStore = await this.GetSequentialStore(entityId, defaultHeadOffset);
            long startOffset = defaultHeadOffset.GetOrElse(0);
            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                int i1 = i;
                tasks.Add(
                    Task.Run(
                        async () =>
                        {
                            for (int j = 0; j < 1000; j++)
                            {
                                long offset = await sequentialStore.Append(new Item { Prop1 = i1 * 10 + j });
                                Assert.True(offset <= 10000 + startOffset);
                            }
                        }));
            }

            await Task.WhenAll(tasks);

            IEnumerable<(long offset, Item item)> batch = await sequentialStore.GetBatch(startOffset, 10000);
            IEnumerable<(long offset, Item item)> batchItems = batch as IList<(long offset, Item item)> ?? batch.ToList();
            Assert.Equal(10000, batchItems.Count());
            long counter = startOffset;
            foreach ((long offset, Item item) batchItem in batchItems)
            {
                Assert.Equal(counter++, batchItem.offset);
            }
        }

        [Theory]
        [MemberData(nameof(GetDefaultHeadOffset))]
        public async Task RemoveTest(Option<long> defaultHeadOffset)
        {
            string entityId = $"removeTestEntity{Guid.NewGuid().ToString()}";
            long startOffset = defaultHeadOffset.GetOrElse(0);
            ISequentialStore<Item> sequentialStore = await this.GetSequentialStore(entityId, defaultHeadOffset);
            for (int i = 0; i < 10; i++)
            {
                long offset = await sequentialStore.Append(new Item { Prop1 = i });
                Assert.Equal(i + startOffset, offset);
            }

            List<(long offset, Item item)> batch = (await sequentialStore.GetBatch(startOffset, 100)).ToList();
            Assert.Equal(10, batch.Count);
            long counter = startOffset;
            foreach ((long offset, Item item) batchItem in batch)
            {
                Assert.Equal(counter++, batchItem.offset);
            }

            await sequentialStore.RemoveFirst((offset, item) => Task.FromResult(item.Prop1 == 0));

            batch = (await sequentialStore.GetBatch(startOffset, 100)).ToList();
            Assert.Equal(9, batch.Count);
            counter = startOffset + 1;
            foreach ((long offset, Item item) batchItem in batch)
            {
                Assert.Equal(counter++, batchItem.offset);
            }

            batch = (await sequentialStore.GetBatch(startOffset + 1, 100)).ToList();
            Assert.Equal(9, batch.Count);
            counter = startOffset + 1;
            foreach ((long offset, Item item) batchItem in batch)
            {
                Assert.Equal(counter++, batchItem.offset);
            }

            await sequentialStore.RemoveFirst((offset, item) => Task.FromResult(item.Prop1 == 0));
            batch = (await sequentialStore.GetBatch(startOffset + 1, 100)).ToList();
            Assert.Equal(9, batch.Count);
            counter = startOffset + 1;
            foreach ((long offset, Item item) batchItem in batch)
            {
                Assert.Equal(counter++, batchItem.offset);
            }

            await sequentialStore.RemoveFirst((offset, item) => Task.FromResult(item.Prop1 == 1));
            batch = (await sequentialStore.GetBatch(startOffset + 2, 100)).ToList();
            Assert.Equal(8, batch.Count);
            counter = startOffset + 2;
            foreach ((long offset, Item item) batchItem in batch)
            {
                Assert.Equal(counter++, batchItem.offset);
            }
        }

        [Theory]
        [MemberData(nameof(GetDefaultHeadOffset))]
        public async Task GetBatchTest(Option<long> defaultHeadOffset)
        {
            // Arrange
            string entityId = $"invalidGetBatch{Guid.NewGuid().ToString()}";
            long startOffset = defaultHeadOffset.GetOrElse(0);
            ISequentialStore<Item> sequentialStore = await this.GetSequentialStore(entityId, defaultHeadOffset);

            // Try to get the batch, should return empty batch.
            List<(long, Item)> batch = (await sequentialStore.GetBatch(startOffset, 10)).ToList();
            Assert.NotNull(batch);
            Assert.Empty(batch);

            // Add 10 elements and remove 4, so that the range of elements is 4 - 9
            for (int i = 0; i < 10; i++)
            {
                long offset = await sequentialStore.Append(new Item { Prop1 = i });
                Assert.Equal(i + startOffset, offset);
            }

            for (int i = 0; i < 4; i++)
            {
                await sequentialStore.RemoveFirst((o, itm) => Task.FromResult(true));
            }

            // Try to get with starting offset <= 4, should return remaining elements - 4 - 9.
            for (int i = 0; i <= 4; i++)
            {
                batch = (await sequentialStore.GetBatch(i + startOffset, 10)).ToList();
                Assert.NotNull(batch);
                Assert.Equal(10 - 4, batch.Count);
                Assert.Equal(4 + startOffset, batch[0].Item1);
            }

            // Try to get with starting offset between 5 and 9, should return a valid batch.
            for (int i = 5; i < 10; i++)
            {
                batch = (await sequentialStore.GetBatch(i + startOffset, 10)).ToList();
                Assert.NotNull(batch);
                Assert.Equal(10 - i, batch.Count);
                Assert.Equal(i + startOffset, batch[0].Item1);
            }

            // Try to get with starting offset > 10, should return empty batch
            for (int i = 10; i < 14; i++)
            {
                batch = (await sequentialStore.GetBatch(i + startOffset, 10)).ToList();
                Assert.NotNull(batch);
                Assert.Empty(batch);
            }
        }

        protected abstract IEntityStore<byte[], TV> GetEntityStore<TV>(string entityName);

        public static IEnumerable<object[]> GetDefaultHeadOffset()
        {
            yield return new object[] { Option.None<long>() };
            yield return new object[] { Option.Some(1500L) };
        }

        async Task<ISequentialStore<Item>> GetSequentialStore(string entityName, Option<long> defaultHeadOffset)
        {
            IEntityStore<byte[], Item> entityStore = this.GetEntityStore<Item>(entityName);
            ISequentialStore<Item> sequentialStore = await defaultHeadOffset
                .Map(d => SequentialStore<Item>.Create(entityStore, d))
                .GetOrElse(() => SequentialStore<Item>.Create(entityStore));
            return sequentialStore;
        }

        public class Item
        {
            public long Prop1 { get; set; }
        }
    }
}
