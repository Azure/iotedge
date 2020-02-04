// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IReporter
    {
        Task ReportAsync(CancellationToken token, ModuleSet moduleSet, IRuntimeInfo runtimeInfo, long version, DeploymentStatus status);

        Task ReportShutdown(DeploymentStatus status, CancellationToken token);
    }
}
