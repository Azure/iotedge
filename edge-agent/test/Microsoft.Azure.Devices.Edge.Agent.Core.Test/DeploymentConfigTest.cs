// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using Microsoft.Azure.Devices.Edge.Util.Test.Common;
using Moq;
using Xunit;

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
    [Unit]
    public class DeploymentConfigTest
    {
        [Fact]
        public void BasicTest()
        {
            var edgeAgentModule = Mock.Of<IEdgeAgentModule>(m => m.Name == "edgeAgent");
            var edgeHubModule = Mock.Of<IEdgeHubModule>(m => m.Name == "edgeHub");
            var systemModules = new SystemModules(edgeAgentModule, edgeHubModule);

            var mod1 = new TestModule(null, string.Empty, "test", ModuleStatus.Running, new TestConfig("mod1"), RestartPolicy.Always, new ConfigurationInfo());
            var mod2 = new TestModule(null, string.Empty, "test", ModuleStatus.Running, new TestConfig("mod2"), RestartPolicy.Always, new ConfigurationInfo());

            var modules = new Dictionary<string, IModule>
            {
                ["mod1"] = mod1,
                ["mod2"] = mod2
            };

            var deploymentConfig = new DeploymentConfig("1.0", Mock.Of<IRuntimeInfo>(), systemModules, modules);

            Assert.Equal("mod1", deploymentConfig.Modules["mod1"].Name);
            Assert.Equal("mod2", deploymentConfig.Modules["mod2"].Name);

            ModuleSet moduleSet = deploymentConfig.GetModuleSet();
            Assert.NotNull(moduleSet);
            Assert.Equal(4, moduleSet.Modules.Count);
            Assert.Equal(edgeHubModule.Name, moduleSet.Modules["edgeHub"].Name);
            Assert.Equal(edgeAgentModule.Name, moduleSet.Modules["edgeAgent"].Name);
            Assert.Equal(modules["mod1"].Name, moduleSet.Modules["mod1"].Name);
            Assert.Equal(modules["mod2"].Name, moduleSet.Modules["mod2"].Name);
        }
    }
}
