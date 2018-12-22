// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Checkpointers
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Routing.Core.Util;

    /// <summary>
    /// In-memory checkpointer DAO (data access object)
    /// </summary>
    public class InMemoryCheckpointStore : ICheckpointStore
    {
        readonly ConcurrentDictionary<string, CheckpointData> checkpointDataMap;

        public InMemoryCheckpointStore()
        {
            this.checkpointDataMap = new ConcurrentDictionary<string, CheckpointData>();
        }

        public InMemoryCheckpointStore(IDictionary<string, CheckpointData> checkpointData)
        {
            Preconditions.CheckNotNull(checkpointData);
            this.checkpointDataMap = new ConcurrentDictionary<string, CheckpointData>();

            foreach (KeyValuePair<string, CheckpointData> kvp in checkpointData)
            {
                this.checkpointDataMap[kvp.Key] = kvp.Value;
            }
        }

        public Task<CheckpointData> GetCheckpointDataAsync(string id, CancellationToken token)
        {
            return Task.FromResult(!this.checkpointDataMap.ContainsKey(id) ? new CheckpointData(Checkpointer.InvalidOffset) : this.checkpointDataMap[id]);
        }

        public Task<IDictionary<string, CheckpointData>> GetAllCheckpointDataAsync(CancellationToken token)
        {
            return Task.FromResult((IDictionary <string, CheckpointData>)this.checkpointDataMap.ToDictionary(keySelector => keySelector.Key, valueSelector => valueSelector.Value));
        }

        public Task SetCheckpointDataAsync(string id, CheckpointData checkpointData, CancellationToken token)
        {
            this.checkpointDataMap[id] = checkpointData;
            return TaskEx.Done;
        }

        public Task CloseAsync(CancellationToken token) => TaskEx.Done;
    }
}
