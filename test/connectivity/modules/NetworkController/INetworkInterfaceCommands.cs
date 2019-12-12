// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System.Threading;
    using System.Threading.Tasks;

    interface INetworkInterfaceCommands
    {
        Task<bool> Enable(CancellationToken token);

        Task<bool> Disable(CancellationToken token);
    }
}
