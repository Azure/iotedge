// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb.Test
{
    using System;
    using System.IO;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class DbStoreProviderTest
    {
        string rocksDbFolder;

        public DbStoreProviderTest()
        {
            string tempFolder = Path.GetTempPath();
            this.rocksDbFolder = Path.Combine(tempFolder, $"edgeTestDb{Guid.NewGuid()}");
            if (Directory.Exists(this.rocksDbFolder))
            {
                Directory.Delete(this.rocksDbFolder);
            }
            Directory.CreateDirectory(this.rocksDbFolder);
        }

        ~DbStoreProviderTest()
        {
            if (!string.IsNullOrWhiteSpace(this.rocksDbFolder) && Directory.Exists(this.rocksDbFolder))
            {
                Directory.Delete(this.rocksDbFolder, true);
            }
        }

        [Fact]
        public void CreateTestAsync()
        {
            var partitionsList1 = new string[]
            {
                "Partition1",
                "Partition2",
                "Partition3",
            };

            using (IDbStoreProvider rocksDbStoreProvider = DbStoreProvider.Create(this.rocksDbFolder, partitionsList1))
            {
                Assert.NotNull(rocksDbStoreProvider);
            }

            var partitionsList2 = new string[]
            {
                "Partition3",
                "Partition4"
            };

            using (IDbStoreProvider rocksDbStoreProvider = DbStoreProvider.Create(this.rocksDbFolder, partitionsList2))
            {
                Assert.NotNull(rocksDbStoreProvider);
            }

            var partitionsList3 = new string[0];

            using (IDbStoreProvider rocksDbStoreProvider = DbStoreProvider.Create(this.rocksDbFolder, partitionsList3))
            {
                Assert.NotNull(rocksDbStoreProvider);
            }
        }
    }
}
