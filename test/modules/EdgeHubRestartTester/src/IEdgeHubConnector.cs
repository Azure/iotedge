// Copyright (c) Microsoft. All rights reserved.
namespace EdgeHubRestartTester
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IEdgeHubConnectorTest
    {
        Task StartAsync(
            DateTime runExpirationTime,
            DateTime edgeHubRestartedTime,
            CancellationToken cancellationToken);
    }
}
