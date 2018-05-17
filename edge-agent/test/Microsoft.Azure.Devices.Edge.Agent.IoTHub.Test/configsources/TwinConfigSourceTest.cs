// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Test.ConfigSources
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.ConfigSources;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Extensions.Configuration;
    using Moq;
    using Xunit;

    [Unit]
    public class TwinConfigSourceTest
    {
        [Fact]
        public async void GetDeploymentConfigTest1()
        {
            // Arrange
            var edgeAgentConnection = new Mock<IEdgeAgentConnection>();
            edgeAgentConnection.Setup(e => e.GetDeploymentConfigInfoAsync()).ReturnsAsync(Option.None<DeploymentConfigInfo>());
            var configuration = Mock.Of<IConfiguration>();
            var twinConfigSource = new TwinConfigSource(edgeAgentConnection.Object, configuration);

            // Act
            DeploymentConfigInfo deploymentConfigInfo = await twinConfigSource.GetDeploymentConfigInfoAsync();

            // Assert
            Assert.NotNull(deploymentConfigInfo);
            Assert.Equal(-1, deploymentConfigInfo.Version);
            Assert.Equal(DeploymentConfig.Empty, deploymentConfigInfo.DeploymentConfig);
        }

        [Fact]
        public async void GetDeploymentConfigTest2()
        {
            // Arrange
            var runtimeInfo = Mock.Of<IRuntimeInfo>();
            var edgeHubModule = Mock.Of<IEdgeHubModule>(m => m.Name == "$edgeHub");
            var edgeAgentModule = Mock.Of<IEdgeAgentModule>(m => m.Name == "$edgeAgent");
            var systemModules = new SystemModules(edgeAgentModule, edgeHubModule);
            string customModule1Name = null;
            string customModule2Name = null;
            var customModule1 = Mock.Of<IModule>();
            var customModule2 = Mock.Of<IModule>();
            Mock.Get(customModule1).SetupSet(n => n.Name = It.IsAny<string>()).Callback<string>(n => customModule1Name = n);
            Mock.Get(customModule2).SetupSet(n => n.Name = It.IsAny<string>()).Callback<string>(n => customModule2Name = n);
            IDictionary<string, IModule> modules = new Dictionary<string, IModule>
            {
                ["module1"] = customModule1,
                ["module2"] = customModule2
            };
            var deploymentConfig = new DeploymentConfig("1.0", runtimeInfo, systemModules, modules);
            var deploymentConfigInfo = new DeploymentConfigInfo(5, deploymentConfig);

            var edgeAgentConnection = new Mock<IEdgeAgentConnection>();
            edgeAgentConnection.Setup(e => e.GetDeploymentConfigInfoAsync()).ReturnsAsync(Option.Some(deploymentConfigInfo));
            var configuration = Mock.Of<IConfiguration>();
            var twinConfigSource = new TwinConfigSource(edgeAgentConnection.Object, configuration);

            // Act
            DeploymentConfigInfo receivedDeploymentConfigInfo = await twinConfigSource.GetDeploymentConfigInfoAsync();

            // Assert
            Assert.NotNull(receivedDeploymentConfigInfo);
            Assert.NotNull(receivedDeploymentConfigInfo.DeploymentConfig);
            Assert.Equal(5, receivedDeploymentConfigInfo.Version);

            DeploymentConfig returnedDeploymentConfig = receivedDeploymentConfigInfo.DeploymentConfig;
            Assert.Equal(Option.Some(edgeAgentModule), returnedDeploymentConfig.SystemModules.EdgeAgent);
            Assert.Equal(Option.Some(edgeHubModule), returnedDeploymentConfig.SystemModules.EdgeHub);
            ModuleSet moduleSet = returnedDeploymentConfig.GetModuleSet();
            Assert.Equal(4, returnedDeploymentConfig.GetModuleSet().Modules.Count);
            Assert.Equal(customModule1.Name, moduleSet.Modules["module1"].Name);
            Assert.Equal(customModule2.Name, moduleSet.Modules["module2"].Name);
            Assert.Equal(edgeHubModule.Name, moduleSet.Modules["$edgeHub"].Name);
            Assert.Equal(edgeAgentModule.Name, moduleSet.Modules["$edgeAgent"].Name);
            Assert.Equal("module1", customModule1Name);
            Assert.Equal("module2", customModule2Name);
        }
    }
}
