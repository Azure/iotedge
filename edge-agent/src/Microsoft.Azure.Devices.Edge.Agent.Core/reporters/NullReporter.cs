// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Reporters
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public class NullReporter : IReporter
    {
        private NullReporter() { }

        public static NullReporter Instance { get; } = new NullReporter();

        public Task ReportAsync(CancellationToken token, ModuleSet moduleSet, IRuntimeInfo runtimeInfo, long version, Option<DeploymentStatus> status) => Task.CompletedTask;

        public Task ReportShutdown(DeploymentStatus status, CancellationToken token) => Task.CompletedTask;

    }
}
