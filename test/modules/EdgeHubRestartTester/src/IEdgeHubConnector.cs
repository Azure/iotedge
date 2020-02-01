// Copyright (c) Microsoft. All rights reserved.
namespace EdgeHubRestartTester
{
    using System.Threading.Tasks;

    public interface IEdgeHubConnector
    {
        Task StartAsync();
    }
}
