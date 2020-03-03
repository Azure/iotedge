// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb.Test
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using RocksDbSharp;
    using Xunit;

    [Unit]
    public class RocksDbOptionsProviderTest
    {
        [Fact]
        public void RocksDbOptionsProviderCreateTest()
        {
            Assert.Throws<ArgumentNullException>(() => new RocksDbOptionsProvider(null, true, Option.None<ulong>(), Option.None<StorageLogLevel>()));
        }

        [Fact]
        public void RocksDbOptionsProviderCreatesDbOptions()
        {
            // arrange
            var env32 = new Mock<ISystemEnvironment>();
            env32.SetupGet(s => s.Is32BitProcess)
                            .Returns(() => true);

            var env64 = new Mock<ISystemEnvironment>();

            env64.SetupGet(s => s.Is32BitProcess)
                            .Returns(() => false);
            var provider32 = new RocksDbOptionsProvider(env32.Object, true, Option.None<ulong>(), Option.None<StorageLogLevel>());
            var provider64 = new RocksDbOptionsProvider(env64.Object, true, Option.None<ulong>(), Option.None<StorageLogLevel>());

            // act
            DbOptions newOptions32 = provider32.GetDbOptions();
            DbOptions newOptions64 = provider64.GetDbOptions();

            // assert
            Assert.NotNull(newOptions32);
            Assert.NotNull(newOptions64);
        }

        [Fact]
        public void RocksDbOptionsProviderCreateCfOptions()
        {
            var env32 = new Mock<ISystemEnvironment>();
            env32.SetupGet(s => s.Is32BitProcess)
                            .Returns(() => true);

            var env64 = new Mock<ISystemEnvironment>();

            env64.SetupGet(s => s.Is32BitProcess)
                            .Returns(() => false);
            var provider32 = new RocksDbOptionsProvider(env32.Object, true, Option.None<ulong>(), Option.None<StorageLogLevel>());
            var provider64 = new RocksDbOptionsProvider(env64.Object, true, Option.None<ulong>(), Option.None<StorageLogLevel>());

            // act
            ColumnFamilyOptions newOptions32 = provider32.GetColumnFamilyOptions();
            ColumnFamilyOptions newOptions64 = provider64.GetColumnFamilyOptions();

            // assert
            Assert.NotNull(newOptions32);
            Assert.NotNull(newOptions64);
        }
    }
}
