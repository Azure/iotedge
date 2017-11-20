// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Reporters
{
    using System.Threading;
    using System.Threading.Tasks;

    public class NullReporter : IReporter
    {
        private NullReporter() { }

        public static NullReporter Instance { get; } = new NullReporter();

        public Task ReportAsync(CancellationToken token, ModuleSet moduleSet, DeploymentConfigInfo deploymentConfigInfo, DeploymentStatus status) => Task.CompletedTask;

        public Task ReportShutdown(DeploymentStatus status, CancellationToken token) => Task.CompletedTask;

    }
}
