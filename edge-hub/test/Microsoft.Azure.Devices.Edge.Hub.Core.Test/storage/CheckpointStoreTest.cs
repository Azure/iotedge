// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Storage;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.Checkpointers;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Xunit;

    [Unit]
    public class CheckpointStoreTest
    {
        [Fact]
        public async Task CheckpointStoreBasicTest()
        {
            ICheckpointStore checkpointStore = CheckpointStore.Create(new InMemoryDbStoreProvider());

            for (long i = 0; i < 10; i++)
            {
                var checkpointData = new CheckpointData(i);
                await checkpointStore.SetCheckpointDataAsync($"Endpoint{i}", checkpointData, CancellationToken.None);
            }

            IDictionary<string, CheckpointData> allCheckpointData = await checkpointStore.GetAllCheckpointDataAsync(CancellationToken.None);
            Assert.Equal(10, allCheckpointData.Count);
            long counter = 0;
            foreach (KeyValuePair<string, CheckpointData> checkpointValue in allCheckpointData)
            {
                Assert.Equal(counter, checkpointValue.Value.Offset);
                Assert.Equal($"Endpoint{counter}", checkpointValue.Key);
                counter++;
            }

            for (long i = 0; i < 10; i++)
            {
                CheckpointData checkpointData = await checkpointStore.GetCheckpointDataAsync($"Endpoint{i}", CancellationToken.None);
                Assert.NotNull(checkpointData);
                Assert.Equal(i, checkpointData.Offset);
            }
        }

        [Fact]
        public void GetCheckpointEntityTest()
        {
            var checkpointData1 = new CheckpointData(100);
            CheckpointStore.CheckpointEntity checkpointEntity1 = CheckpointStore.GetCheckpointEntity(checkpointData1);
            Assert.NotNull(checkpointEntity1);
            Assert.Equal(100, checkpointEntity1.Offset);
            Assert.False(checkpointEntity1.LastFailedRevivalTime.HasValue);
            Assert.False(checkpointEntity1.UnhealthySince.HasValue);

            DateTime lastFailedRevivalTime = DateTime.UtcNow;
            DateTime unhealthySinceTime = DateTime.Parse("2008-05-01 7:34:42Z");
            var checkpointData2 = new CheckpointData(100, Option.Some(lastFailedRevivalTime), Option.Some(unhealthySinceTime));
            CheckpointStore.CheckpointEntity checkpointEntity2 = CheckpointStore.GetCheckpointEntity(checkpointData2);
            Assert.NotNull(checkpointEntity2);
            Assert.Equal(100, checkpointEntity2.Offset);
            Assert.True(checkpointEntity2.LastFailedRevivalTime.HasValue);
            Assert.Equal(lastFailedRevivalTime, checkpointEntity2.LastFailedRevivalTime.Value);
            Assert.True(checkpointEntity2.UnhealthySince.HasValue);
            Assert.Equal(unhealthySinceTime, checkpointEntity2.UnhealthySince.Value);
        }

        [Fact]
        public void GetCheckpointDataTest()
        {
            var checkpointEntity1 = new CheckpointStore.CheckpointEntity(100, null, null);
            CheckpointData checkpointData1 = CheckpointStore.GetCheckpointData(checkpointEntity1);
            Assert.NotNull(checkpointData1);
            Assert.Equal(100, checkpointData1.Offset);
            Assert.False(checkpointData1.LastFailedRevivalTime.HasValue);
            Assert.False(checkpointData1.UnhealthySince.HasValue);

            DateTime lastFailedRevivalTime = DateTime.UtcNow;
            DateTime unhealthySinceTime = DateTime.Parse("2008-05-01 7:34:42Z");
            var checkpointEntity2 = new CheckpointStore.CheckpointEntity(100, lastFailedRevivalTime, unhealthySinceTime);
            CheckpointData checkpointData2 = CheckpointStore.GetCheckpointData(checkpointEntity2);
            Assert.NotNull(checkpointData2);
            Assert.Equal(100, checkpointData2.Offset);
            Assert.True(checkpointData2.LastFailedRevivalTime.HasValue);
            Assert.Equal(lastFailedRevivalTime, checkpointData2.LastFailedRevivalTime.OrDefault());
            Assert.True(checkpointData2.UnhealthySince.HasValue);
            Assert.Equal(unhealthySinceTime, checkpointData2.UnhealthySince.OrDefault());
        }
    }
}