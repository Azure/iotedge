// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Checkpointers
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface ICheckpointStoreFactory
    {
        Task<ICheckpointStore> CreateAsync(string hubName, CancellationToken token);
    }
}
