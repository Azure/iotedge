// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System.Threading;
    using System.Threading.Tasks;
    using ModuleUtil.NetworkControllerResult;

    interface INetworkController
    {
        NetworkStatus NetworkStatus { get; }

        Task<bool> SetEnabledAsync(bool enabled, CancellationToken cs);

        Task<bool> GetEnabledAsync(CancellationToken cs);
    }
}
