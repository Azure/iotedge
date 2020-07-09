// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Checkpointers
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ICheckpointStore
    {
        Task<CheckpointData> GetCheckpointDataAsync(string id, CancellationToken token);

        Task<IDictionary<string, CheckpointData>> GetAllCheckpointDataAsync(CancellationToken token);

        Task SetCheckpointDataAsync(string id, CheckpointData checkpointData, CancellationToken token);

        Task CloseAsync(CancellationToken token);
    }
}