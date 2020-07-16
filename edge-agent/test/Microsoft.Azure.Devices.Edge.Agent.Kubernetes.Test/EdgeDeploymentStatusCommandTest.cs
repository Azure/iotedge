// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using System.Threading;
    using Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;

    using Xunit;

    [Unit]
    public class EdgeDeploymentStatiusCommandTest
    {
        [Fact]
        public async void ExecuteAyncSuccessWithNoStatus()
        {
            EdgeDeploymentStatusCommand noStatus = new EdgeDeploymentStatusCommand(Option.None<EdgeDeploymentStatus>());

            await noStatus.ExecuteAsync(CancellationToken.None);
        }

        [Fact]
        public async void ExecuteAyncSuccessWithSuccessStatus()
        {
            EdgeDeploymentStatus success = EdgeDeploymentStatus.Success("200 OK");
            EdgeDeploymentStatusCommand successStatus = new EdgeDeploymentStatusCommand(Option.Some(success));

            await successStatus.ExecuteAsync(CancellationToken.None);
        }

        [Fact]
        public async void ExecuteAsyncThrowsWithFailureStatus()
        {
            EdgeDeploymentStatus failure = EdgeDeploymentStatus.Failure("failure message");
            EdgeDeploymentStatusCommand failStatus = new EdgeDeploymentStatusCommand(Option.Some(failure));

            await Assert.ThrowsAsync<ConfigOperationFailureException>(() => failStatus.ExecuteAsync(CancellationToken.None));
        }

        [Fact]
        public void CurrentStatusReflectsDeploymentStatus()
        {
            EdgeDeploymentStatusCommand noStatus = new EdgeDeploymentStatusCommand(Option.None<EdgeDeploymentStatus>());
            EdgeDeploymentStatus success = EdgeDeploymentStatus.Success("200 OK");
            EdgeDeploymentStatusCommand successStatus = new EdgeDeploymentStatusCommand(Option.Some(success));
            EdgeDeploymentStatus failure = EdgeDeploymentStatus.Failure("failure message");
            EdgeDeploymentStatusCommand failStatus = new EdgeDeploymentStatusCommand(Option.Some(failure));

            Assert.Equal($"Report EdgeDeployment status: [{EdgeDeploymentStatusType.Success}]", noStatus.Show());
            Assert.Equal($"Report EdgeDeployment status: [{EdgeDeploymentStatusType.Success}]", successStatus.Show());
            Assert.Equal($"Report EdgeDeployment status: [{EdgeDeploymentStatusType.Failure}]", failStatus.Show());
        }
    }
}
