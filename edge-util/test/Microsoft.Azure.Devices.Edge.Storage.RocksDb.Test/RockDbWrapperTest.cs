// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb.Test
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;
    using System.Collections.Generic;

    [Unit]
    public class RockDbWrapperTest
    {
        [Fact]
        public void CreateWithNullPathThrowsAsync()
        {
            //Arrange
            ICollection<string> partitions = new List<string>();

            //Act
            //Assert
            Assert.Throws<ArgumentException>(() => RocksDbWrapper.Create(null, partitions));
            partitions.Clear();
        }

        [Fact]
        public void CreateWithNullPartitionsPathThrowsAsync()
        {
            //Arrange
            //Act
            //Assert
            Assert.Throws<ArgumentNullException>(() => RocksDbWrapper.Create("AnyPath", null));
        }
    }
}
