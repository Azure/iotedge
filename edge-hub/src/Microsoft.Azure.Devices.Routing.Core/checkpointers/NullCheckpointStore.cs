// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Checkpointers
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Routing.Core.Util;

    /// <summary>
    /// Checkpointer DAO (data access object) which ignores sets. The initial value
    /// of the offset can be set through the constructor.
    /// </summary>
    public class NullCheckpointStore : ICheckpointStore
    {
        readonly Task<CheckpointData> initialCheckpointData;
        readonly Task<IDictionary<string, CheckpointData>> initialCheckpointDataMap;

        public NullCheckpointStore()
            : this(Checkpointer.InvalidOffset)
        {
        }

        public NullCheckpointStore(long offset)
        {
            var data = new CheckpointData(offset);
            this.initialCheckpointData = Task.FromResult(data);
            this.initialCheckpointDataMap = Task.FromResult(
                new Dictionary<string, CheckpointData>()
                    {
                        { "NullCheckpoint", data }
                    }
                    as IDictionary<string, CheckpointData>);
        }

        public static NullCheckpointStore Instance { get; } = new NullCheckpointStore();

        public Task CloseAsync(CancellationToken token) => TaskEx.Done;

        public Task<IDictionary<string, CheckpointData>> GetAllCheckpointDataAsync(CancellationToken token)
        {
            return this.initialCheckpointDataMap;
        }

        public Task<CheckpointData> GetCheckpointDataAsync(string id, CancellationToken token)
        {
            return this.initialCheckpointData;
        }

        public Task SetCheckpointDataAsync(string id, CheckpointData checkpointData, CancellationToken token) => TaskEx.Done;
    }
}
