// Copyright (c) Microsoft. All rights reserved.
namespace EdgeHubRestartTester
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Extensions.Logging;

    public interface IEdgeHubConnector
    {
        Task ConnectAsync();
    }
}
