// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb.Test
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class RockDbWrapperTest
    {
        [Fact]
        public void CreateWithNullOptionThrowsAsync()
        {
            // Arrange
            ICollection<string> partitions = new List<string>();
            // Act
            // Assert
            Assert.Throws<ArgumentNullException>(() => RocksDbWrapper.Create(null, "AnyPath", partitions));
            partitions.Clear();
        }

        [Fact]
        public void CreateWithNullPathThrowsAsync()
        {
            // Arrange
            ICollection<string> partitions = new List<string>();
            var options = new RocksDbOptionsProvider(new SystemEnvironment(), true);
            // Act
            // Assert
            Assert.Throws<ArgumentException>(() => RocksDbWrapper.Create(options, null, partitions));
            partitions.Clear();
        }

        [Fact]
        public void CreateWithNullPartitionsPathThrowsAsync()
        {
            // Arrange
            var options = new RocksDbOptionsProvider(new SystemEnvironment(), true);

            // Act
            // Assert
            Assert.Throws<ArgumentNullException>(() => RocksDbWrapper.Create(options, "AnyPath", null));
        }
    }
}
