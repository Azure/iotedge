// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Routing.Core.Checkpointers;

    using Newtonsoft.Json;

    public class CheckpointStore : ICheckpointStore
    {
        readonly IEntityStore<string, CheckpointEntity> underlyingStore;

        CheckpointStore(IEntityStore<string, CheckpointEntity> underlyingStore)
        {
            this.underlyingStore = underlyingStore;
        }

        public static CheckpointStore Create(IDbStoreProvider dbStoreProvider)
        {
            IDbStore dbStore = Preconditions.CheckNotNull(dbStoreProvider, nameof(dbStoreProvider)).GetDbStore(Constants.CheckpointStorePartitionKey);
            IEntityStore<string, CheckpointEntity> underlyingStore = new EntityStore<string, CheckpointEntity>(dbStore, nameof(CheckpointEntity), 12);
            return new CheckpointStore(underlyingStore);
        }

        public Task CloseAsync(CancellationToken token) => Task.CompletedTask;

        public async Task<IDictionary<string, CheckpointData>> GetAllCheckpointDataAsync(CancellationToken token)
        {
            IDictionary<string, CheckpointData> allCheckpointData = new Dictionary<string, CheckpointData>();
            await this.underlyingStore.IterateBatch(
                int.MaxValue,
                (key, value) =>
                {
                    allCheckpointData[key] = GetCheckpointData(value);
                    return Task.CompletedTask;
                });
            return allCheckpointData;
        }

        public async Task<CheckpointData> GetCheckpointDataAsync(string id, CancellationToken token)
        {
            Preconditions.CheckNonWhiteSpace(id, nameof(id));
            Option<CheckpointEntity> checkpointEntity = await this.underlyingStore.Get(id);
            return checkpointEntity.Match(
                ce => GetCheckpointData(ce),
                () => new CheckpointData(Checkpointer.InvalidOffset));
        }

        public async Task SetCheckpointDataAsync(string id, CheckpointData checkpointData, CancellationToken token)
        {
            Preconditions.CheckNonWhiteSpace(id, nameof(id));
            Preconditions.CheckNotNull(checkpointData, nameof(checkpointData));
            await this.underlyingStore.Put(id, GetCheckpointEntity(checkpointData));
        }

        internal static CheckpointData GetCheckpointData(CheckpointEntity checkpointEntity)
        {
            return new CheckpointData(
                checkpointEntity.Offset,
                checkpointEntity.LastFailedRevivalTime.HasValue ? Devices.Routing.Core.Util.Option.Some(checkpointEntity.LastFailedRevivalTime.Value) : Devices.Routing.Core.Util.Option.None<DateTime>(),
                checkpointEntity.UnhealthySince.HasValue ? Devices.Routing.Core.Util.Option.Some(checkpointEntity.UnhealthySince.Value) : Devices.Routing.Core.Util.Option.None<DateTime>());
        }

        internal static CheckpointEntity GetCheckpointEntity(CheckpointData checkpointData)
        {
            return new CheckpointEntity(
                checkpointData.Offset,
                checkpointData.LastFailedRevivalTime.Match(v => v, () => (DateTime?)null),
                checkpointData.UnhealthySince.Match(v => v, () => (DateTime?)null));
        }

        internal class CheckpointEntity
        {
            [JsonConstructor]
            public CheckpointEntity(long offset, DateTime? lastFailedRevivalTime, DateTime? unhealthySince)
            {
                this.Offset = offset;
                this.LastFailedRevivalTime = lastFailedRevivalTime;
                this.UnhealthySince = unhealthySince;
            }

            public DateTime? LastFailedRevivalTime { get; }

            public long Offset { get; }

            public DateTime? UnhealthySince { get; }
        }
    }
}
