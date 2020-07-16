// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes;
    using Microsoft.Azure.Devices.Edge.Util;

    public class EdgeDeploymentStatusCommand : ICommand
    {
        readonly Option<EdgeDeploymentStatus> activeStatus;

        public string Id => "EdgeDeploymentStatusCommand";

        public EdgeDeploymentStatusCommand(
            Option<EdgeDeploymentStatus> activeStatus)
        {
            this.activeStatus = activeStatus;
        }

        public Task ExecuteAsync(CancellationToken token)
        {
            // If the EdgeDeployment operator fails, report this to the plan executor
            // as an exception.
            this.activeStatus.ForEach(status =>
                {
                    if (status.State != EdgeDeploymentStatusType.Success)
                    {
                        throw new ConfigOperationFailureException(status.Message);
                    }
                });
            return Task.CompletedTask;
        }

        public Task UndoAsync(CancellationToken token) => Task.CompletedTask;

        EdgeDeploymentStatusType CurrentStatus() => this.activeStatus.Map(s => s.State).GetOrElse(() => EdgeDeploymentStatusType.Success);

        public string Show() => $"Report EdgeDeployment status: [{this.CurrentStatus()}]";

        public override string ToString() => this.Show();
    }
}