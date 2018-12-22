// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class DockerEnvironmentProviderTest
    {
        [Fact]
        public async Task CreateEnvironmentTest()
        {
            // Arrange
            var runtimeInfoProvider = Mock.Of<IRuntimeInfoProvider>(m => m.GetSystemInfo() == Task.FromResult(new SystemInfo("linux", "x64", "17.11.0-ce")));
            var entityStore = Mock.Of<IEntityStore<string, ModuleState>>();
            var restartPolicyManager = Mock.Of<IRestartPolicyManager>();

            // Act
            DockerEnvironmentProvider dockerEnvironmentProvider = await DockerEnvironmentProvider.CreateAsync(runtimeInfoProvider, entityStore, restartPolicyManager);

            // Assert
            Assert.NotNull(dockerEnvironmentProvider);

            // Act
            IEnvironment dockerEnvironment = dockerEnvironmentProvider.Create(DeploymentConfig.Empty);

            // Assert
            Assert.NotNull(dockerEnvironment);
        }
    }
}
