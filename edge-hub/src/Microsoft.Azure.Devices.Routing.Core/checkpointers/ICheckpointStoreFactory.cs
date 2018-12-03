// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Routing.Core.Checkpointers
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface ICheckpointStoreFactory
    {
        Task<ICheckpointStore> CreateAsync(string hubName, CancellationToken token);
    }
}
