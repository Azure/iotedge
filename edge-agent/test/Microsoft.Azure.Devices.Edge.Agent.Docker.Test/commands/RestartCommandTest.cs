// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test.Commands
{
    using System.Threading;
    using System.Threading.Tasks;
    using global::Docker.DotNet;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker.Commands;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    public class RestartCommandTest
    {
        [Fact]
        [Unit]
        public async Task RestartContainer()
        {
            // Arrange
            const string ModuleName = "boo";
            var module = new Mock<IRuntimeModule>();
            module.SetupGet(m => m.Name).Returns(ModuleName);

            var containerOperations = new Mock<IContainerOperations>();
            containerOperations
                .Setup(co => co.RestartContainerAsync(ModuleName, It.IsAny<ContainerRestartParameters>(), CancellationToken.None))
                .Returns(Task.CompletedTask);

            var dockerClient = new Mock<IDockerClient>();
            dockerClient.SetupGet(c => c.Containers).Returns(containerOperations.Object);

            // Act
            var command = new RestartCommand(dockerClient.Object, module.Object);
            await command.ExecuteAsync(CancellationToken.None);

            // Assert
            module.VerifyAll();
            containerOperations.VerifyAll();
            dockerClient.VerifyAll();
        }
    }
}