// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb.Test
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class ColumnFamilyStoreTest : IClassFixture<TestRocksDbStoreProvider>
    {
        readonly TestRocksDbStoreProvider rocksDbStoreProvider;

        public ColumnFamilyStoreTest(TestRocksDbStoreProvider rocksDbStoreProvider)
        {
            this.rocksDbStoreProvider = rocksDbStoreProvider;
        }

        [Fact]
        public async Task BasicTest()
        {
            IDbStore columnFamilyDbStore = this.rocksDbStoreProvider.GetDbStore("test");

            string key = "key1";
            string value = "value1";
            byte[] keyBytes = key.ToBytes();
            byte[] valueBytes = value.ToBytes();
            await columnFamilyDbStore.Put(keyBytes, valueBytes);
            Option<byte[]> returnedValueBytesOption = await columnFamilyDbStore.Get(keyBytes);
            Assert.True(returnedValueBytesOption.HasValue);
            byte[] returnedValueBytes = returnedValueBytesOption.OrDefault();
            Assert.True(valueBytes.SequenceEqual(returnedValueBytes));
            Assert.Equal(value, returnedValueBytes.FromBytes());
            Assert.True(await columnFamilyDbStore.Contains(keyBytes));
            Assert.False(await columnFamilyDbStore.Contains("key2".ToBytes()));

            await columnFamilyDbStore.Remove(keyBytes);
            returnedValueBytesOption = await columnFamilyDbStore.Get(keyBytes);
            Assert.False(returnedValueBytesOption.HasValue);
        }

        [Fact]
        public async Task FirstLastTest()
        {
            IDbStore columnFamilyDbStore = this.rocksDbStoreProvider.GetDbStore("firstLastTest");

            string firstKey = "firstKey";
            string firstValue = "firstValue";
            await columnFamilyDbStore.Put(firstKey.ToBytes(), firstValue.ToBytes());

            for (int i = 0; i < 10; i++)
            {
                string key = $"key{i}";
                string value = "$value{i}";
                await columnFamilyDbStore.Put(key.ToBytes(), value.ToBytes());
            }

            string lastKey = "lastKey";
            string lastValue = "lastValue";
            await columnFamilyDbStore.Put(lastKey.ToBytes(), lastValue.ToBytes());

            Option<(byte[], byte[])> firstEntryOption = await columnFamilyDbStore.GetFirstEntry();
            Assert.True(firstEntryOption.HasValue);
            (byte[] key, byte[] value) firstEntry = firstEntryOption.OrDefault();
            Assert.Equal(firstKey, firstEntry.key.FromBytes());
            Assert.Equal(firstValue, firstEntry.value.FromBytes());

            Option<(byte[], byte[])> lastEntryOption = await columnFamilyDbStore.GetLastEntry();
            Assert.True(lastEntryOption.HasValue);
            (byte[] key, byte[] value) lastEntry = lastEntryOption.OrDefault();
            Assert.Equal(lastKey, lastEntry.key.FromBytes());
            Assert.Equal(lastValue, lastEntry.value.FromBytes());
        }

        [Fact]
        public async Task BackupNotImplementedTest()
        {
            IDbStore columnFamilyDbStore = this.rocksDbStoreProvider.GetDbStore("firstLastTest");
            await Assert.ThrowsAsync<NotImplementedException>(() => columnFamilyDbStore.BackupAsync(string.Empty));
        }
    }
}
