// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Checkpointers
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ICheckpointStore
    {
        Task CloseAsync(CancellationToken token);

        Task<IDictionary<string, CheckpointData>> GetAllCheckpointDataAsync(CancellationToken token);

        Task<CheckpointData> GetCheckpointDataAsync(string id, CancellationToken token);

        Task SetCheckpointDataAsync(string id, CheckpointData checkpointData, CancellationToken token);
    }
}
