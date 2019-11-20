// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System.Threading;
    using System.Threading.Tasks;

    interface IFirewallCommands
    {
        Task<bool> UnsetDropRule(CancellationToken cs);

        Task<bool> SetDropRule(CancellationToken cs);

        Task<NetworkStatus> GetStatus(CancellationToken cs);
    }
}
