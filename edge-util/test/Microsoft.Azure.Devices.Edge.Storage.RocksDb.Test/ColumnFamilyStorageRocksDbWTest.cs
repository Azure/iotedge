// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb.Test
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class ColumnFamilyStorageRocksDbWTest
    {
        [Fact]
        public void CreateWithNullPathThrowsAsync()
        {
            // Arrange
            ICollection<string> partitions = new List<string>();

            // Act
            // Assert
            Assert.Throws<ArgumentException>(() => ColumnFamilyStorageRocksDbWrapper.Create(null, partitions));

            partitions.Clear();
        }

        [Fact]
        public void CreateWithNullPartitionsPathThrowsAsync()
        {
            // Arrange
            // Act
            // Assert
            Assert.Throws<ArgumentNullException>(() => ColumnFamilyStorageRocksDbWrapper.Create("AnyPath", null));
        }
    }
}
