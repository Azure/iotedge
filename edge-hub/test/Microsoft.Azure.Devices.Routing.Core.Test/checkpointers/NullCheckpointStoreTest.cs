// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Checkpointers
{
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.Checkpointers;

    using Xunit;

    public class NullCheckpointStoreTest : RoutingUnitTestBase
    {
        [Fact]
        [Unit]
        public async Task SmokeTest()
        {
            var store = new NullCheckpointStore(10L);
            Assert.Equal(10L, (await store.GetCheckpointDataAsync("id1", CancellationToken.None)).Offset);
            Assert.Equal(10L, (await store.GetCheckpointDataAsync("id2", CancellationToken.None)).Offset);

            await store.SetCheckpointDataAsync("id1", new CheckpointData(20L), CancellationToken.None);
            await store.SetCheckpointDataAsync("id2", new CheckpointData(20L), CancellationToken.None);
            Assert.Equal(10L, (await store.GetCheckpointDataAsync("id1", CancellationToken.None)).Offset);
            Assert.Equal(10L, (await store.GetCheckpointDataAsync("id2", CancellationToken.None)).Offset);

            await store.CloseAsync(CancellationToken.None);
        }

        [Fact]
        [Unit]
        public async Task TestEmptyConstructor()
        {
            var store = new NullCheckpointStore();
            Assert.Equal(Checkpointer.InvalidOffset, (await store.GetCheckpointDataAsync("id1", CancellationToken.None)).Offset);
            Assert.Equal(Checkpointer.InvalidOffset, (await store.GetCheckpointDataAsync("id2", CancellationToken.None)).Offset);
        }
    }
}
