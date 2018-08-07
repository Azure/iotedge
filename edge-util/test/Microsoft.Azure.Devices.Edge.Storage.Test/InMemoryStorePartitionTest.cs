// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.Test
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class InMemoryStorePartitionTest
    {
        [Fact]
        public async Task GetIndexTest()
        {
            var inMemoryStorePartition = new InMemoryDbStore();
            await inMemoryStorePartition.Put("key1".ToBytes(), "val1".ToBytes());
            await inMemoryStorePartition.Put("key2".ToBytes(), "val2".ToBytes());
            await inMemoryStorePartition.Put("key3".ToBytes(), "val3".ToBytes());

            Assert.Equal(0, inMemoryStorePartition.GetIndex("key1".ToBytes()));
            Assert.Equal(1, inMemoryStorePartition.GetIndex("key2".ToBytes()));
            Assert.Equal(2, inMemoryStorePartition.GetIndex("key3".ToBytes()));

            await inMemoryStorePartition.Remove("key2".ToBytes());
            Assert.Equal(0, inMemoryStorePartition.GetIndex("key1".ToBytes()));
            Assert.Equal(-1, inMemoryStorePartition.GetIndex("key2".ToBytes()));
            Assert.Equal(1, inMemoryStorePartition.GetIndex("key3".ToBytes()));

            await inMemoryStorePartition.Put("key3".ToBytes(), "newVal3".ToBytes());
            Assert.Equal(1, inMemoryStorePartition.GetIndex("key3".ToBytes()));            
        }

        [Fact]
        public async Task GetIndexDuringUpdateTest()
        {
            int totalCount = 10000;
            var inMemoryStorePartition = new InMemoryDbStore();
            for (int i = 0; i <= totalCount; i++)
            {
                await inMemoryStorePartition.Put($"key{i}".ToBytes(), $"val{i}".ToBytes());
            }

            Task getIndexTask = Task.Run(() => inMemoryStorePartition.GetIndex($"key{totalCount}".ToBytes()));

            Task removeTask = Task.Run(async () => await inMemoryStorePartition.Remove("key0".ToBytes()));
            Task putTask = Task.Run(async () => await inMemoryStorePartition.Put("key1".ToBytes(), "newvalue".ToBytes()));

            await Task.WhenAll(removeTask, putTask, getIndexTask);            
            Assert.True(getIndexTask.IsCompletedSuccessfully);
            Assert.True(removeTask.IsCompletedSuccessfully);
            Assert.True(putTask.IsCompletedSuccessfully);
        }

        [Fact]
        public async Task GetFirstLastValueTest()
        {
            var inMemoryStorePartition = new InMemoryDbStore();
            await inMemoryStorePartition.Put("key1".ToBytes(), "val1".ToBytes());
            await inMemoryStorePartition.Put("key2".ToBytes(), "val2".ToBytes());
            await inMemoryStorePartition.Put("key3".ToBytes(), "val3".ToBytes());

            Option<(byte[] key, byte[] value)> firstValueOption = await inMemoryStorePartition.GetFirstEntry();
            Assert.True(firstValueOption.HasValue);
            (byte[] key, byte[] value) firstValue = firstValueOption.OrDefault();
            Assert.Equal("key1", firstValue.key.FromBytes<string>());
            Assert.Equal("val1", firstValue.value.FromBytes<string>());

            Option<(byte[] key, byte[] value)> lastValueOption = await inMemoryStorePartition.GetLastEntry();
            Assert.True(lastValueOption.HasValue);
            (byte[] key, byte[] value) lastValue = lastValueOption.OrDefault();
            Assert.Equal("key3", lastValue.key.FromBytes<string>());
            Assert.Equal("val3", lastValue.value.FromBytes<string>());
        }

        [Fact]
        public async Task IterateBatchTest()
        {
            var inMemoryStorePartition = new InMemoryDbStore();
            for (int i = 0; i < 100; i++)
            {
                await inMemoryStorePartition.Put($"key{i}".ToBytes(), $"val{i}".ToBytes());
            }

            int[] counter = { 11 };
            await inMemoryStorePartition.IterateBatch($"key{counter[0]}".ToBytes(), 20, (key, value) =>
            {
                Assert.Equal($"key{counter[0]}", key.FromBytes<string>());
                Assert.Equal($"val{counter[0]}", value.FromBytes<string>());
                counter[0]++;
                return Task.CompletedTask;
            });

            counter[0] = 61;
            await inMemoryStorePartition.IterateBatch($"key{counter[0]}".ToBytes(), 20, (key, value) =>
            {
                Assert.Equal($"key{counter[0]}", key.FromBytes<string>());
                Assert.Equal($"val{counter[0]}", value.FromBytes<string>());
                counter[0]++;
                return Task.CompletedTask;
            });
        }
    }
}
