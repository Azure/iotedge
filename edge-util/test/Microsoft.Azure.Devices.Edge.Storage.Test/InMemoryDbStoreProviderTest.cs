// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.Test
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
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
            long maxStorageBytes = 90;
            IStorageSpaceChecker checker = new MemorySpaceChecker(TimeSpan.FromSeconds(3), maxStorageBytes, () => Task.FromResult(0L));
            InMemoryDbStoreProvider storeProvider = new InMemoryDbStoreProvider(Option.Some(checker));

            string store1Name = "store1";
            IDbStore store1 = storeProvider.GetDbStore(store1Name);

            string store2Name = "store2";
            IDbStore store2 = storeProvider.GetDbStore(store2Name);

            byte[] message1 = new byte[10];
            byte[] message2 = new byte[20];
            await store1.Put(message1, message1);
            await store2.Put(message2, message2);

            await Task.Delay(TimeSpan.FromSeconds(4));

            // This should succeed as the message store size will be (message1 * 2) + (message2 * 2) size in bytes.
            byte[] message3 = new byte[30];
            await store1.Put(message3, message3);

            // Let the space checker run again, this time the usage limit would have been reached (after adding message3).
            await Task.Delay(TimeSpan.FromSeconds(4));

            byte[] message4 = new byte[40];
            await Assert.ThrowsAsync<StorageFullException>(() => store2.Put(message4, message4));
            await Assert.ThrowsAsync<StorageFullException>(() => store2.Put(message4, message4, CancellationToken.None));

            // Remove store2. Once the memory checker runs again, the usage of store1 alone should be less than the max limit.
            storeProvider.RemoveDbStore(store2Name);
            await Task.Delay(TimeSpan.FromSeconds(4));
            await store1.Put(message4, message4);
        }
    }
}
