// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System.Threading;
    using System.Threading.Tasks;

    interface IFirewallCommands
    {
        Task<bool> RemoveDropRule(CancellationToken cs);

        Task<bool> AddDropRule(CancellationToken cs);

        Task<NetworkStatus> GetStatus(CancellationToken cs);
    }
}
