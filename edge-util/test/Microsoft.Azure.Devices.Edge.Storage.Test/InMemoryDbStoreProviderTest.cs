// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.Test
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class InMemoryDbStoreProviderTest
    {
        [Fact]
        public async Task SpaceCheckViolationsTest()
        {
            Option<long> maxStorageBytes = Option.Some(90L);
            IStorageSpaceChecker checker = new MemorySpaceChecker(() => 0L);
            checker.SetMaxSizeBytes(maxStorageBytes);
            InMemoryDbStoreProvider storeProvider = new InMemoryDbStoreProvider(Option.Some(checker));

            string store1Name = "store1";
            IDbStore store1 = storeProvider.GetDbStore(store1Name);

            string store2Name = "store2";
            IDbStore store2 = storeProvider.GetDbStore(store2Name);

            byte[] message1 = new byte[10];
            byte[] message2 = new byte[20];
            await store1.Put(message1, message1);
            await store2.Put(message2, message2);

            // Current sizes -
            // store1 -> (message1 * 2)
            // store2 -> (message2 * 2)
            // Aggregated size is less than limit, adding another item should succeed.
            byte[] message3 = new byte[30];
            await store1.Put(message3, message3, CancellationToken.None);

            // Current sizes -
            // store1 -> (message1 * 2) + (message3 * 2)
            // store2 -> (message2 * 2)
            // Aggregated size is greater than limit, adding another item should fail.
            byte[] message4 = new byte[40];
            await Assert.ThrowsAsync<StorageFullException>(() => store2.Put(message4, message4));
            await Assert.ThrowsAsync<StorageFullException>(() => store2.Put(message4, message4, CancellationToken.None));

            // Remove store2. The usage of store1 alone should be less than the max limit.
            // Current sizes -
            // store1 -> (message1 * 2) + (message3 * 2)
            // store2 -> X
            // Aggregated size is less than limit, adding another item should succeed.
            storeProvider.RemoveDbStore(store2Name);
            await store1.Put(message4, message4);

            // Current sizes -
            // store1 -> (message1 * 2) + (message3 * 2) + (message4 * 2)
            // store2 -> X
            // Aggregated size is greater than limit, adding another item should fail.
            byte[] message5 = new byte[45];
            await Assert.ThrowsAsync<StorageFullException>(() => store1.Put(message5, message5));

            // Re-add store2.
            store2 = storeProvider.GetDbStore(store2Name);

            await store1.Remove(message4);

            // Current sizes -
            // store1 -> (message1 * 2) + (message3 * 2)
            // store2 -> X
            // Aggregated size is less than limit, adding another item should succeed.
            await store2.Put(message1, message1);

            // Current sizes -
            // store1 -> (message1 * 2) + (message3 * 2)
            // store2 -> (message1 * 2)
            // Aggregated size is greater than limit, adding another item should fail.
            await Assert.ThrowsAsync<StorageFullException>(() => store2.Put(message5, message5));

            // Remove an item from store1 and then try adding a smaller item to store2 which should succeed.
            await store1.Remove(message3, CancellationToken.None);

            // Current sizes -
            // store1 -> (message1 * 2)
            // store2 -> (message1 * 2)
            // Aggregated size is less than limit, adding another item should succeed.
            await store2.Put(message3, message3, CancellationToken.None);

            // Current sizes -
            // store1 -> (message1 * 2)
            // store2 -> (message1 * 2) + (message3 * 2)
            // Aggregated size is greater than limit, adding another item should fail.
            await Assert.ThrowsAsync<StorageFullException>(() => store2.Put(message4, message4));

            // Set max storage size to be greater than the current db size.
            Option<long> newMaxStorageBytes = Option.Some((message1.Length * 2) * 2L + (message3.Length * 2) + 10);
            checker.SetMaxSizeBytes(newMaxStorageBytes);

            // Adding another item should now succeed after the limits have been increased.
            await store1.Put(message4, message4);

            // Adding a new item to store1 should fail.
            // Current sizes -
            // store1 -> (message1 * 2) + (message4 * 2)
            // store2 -> (message1 * 2) + (message3 * 2)
            // Aggregated size is greater than limit, adding another item should fail.
            await Assert.ThrowsAsync<StorageFullException>(() => store1.Put(message5, message5));

            // Updating the message4 item in store1 should succeed as the size difference between
            // the existing and updated value is zero bytes which doesn't lead to the limit being breached.
            await store1.Put(message4, message4);
        }
    }
}
