// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface IReporter
    {
        Task ReportAsync(CancellationToken token, ModuleSet moduleSet, DeploymentConfigInfo deploymentConfigInfo, DeploymentStatus status);
    }
}
