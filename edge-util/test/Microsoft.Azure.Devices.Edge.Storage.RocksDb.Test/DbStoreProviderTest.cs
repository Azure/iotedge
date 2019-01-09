// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb.Test
{
    using System;
    using System.IO;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class DbStoreProviderTest
    {
        readonly string rocksDbFolder;

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
            var options = new RocksDbOptionsProvider(new SystemEnvironment(), true);

            var partitionsList1 = new[]
            {
                "Partition1",
                "Partition2",
                "Partition3",
            };

            using (IDbStoreProvider rocksDbStoreProvider = DbStoreProvider.Create(options, this.rocksDbFolder, partitionsList1))
            {
                Assert.NotNull(rocksDbStoreProvider);
            }

            var partitionsList2 = new[]
            {
                "Partition3",
                "Partition4"
            };

            using (IDbStoreProvider rocksDbStoreProvider = DbStoreProvider.Create(options, this.rocksDbFolder, partitionsList2))
            {
                Assert.NotNull(rocksDbStoreProvider);
            }

            var partitionsList3 = new string[0];

            using (IDbStoreProvider rocksDbStoreProvider = DbStoreProvider.Create(options, this.rocksDbFolder, partitionsList3))
            {
                Assert.NotNull(rocksDbStoreProvider);
            }
        }
    }
}
