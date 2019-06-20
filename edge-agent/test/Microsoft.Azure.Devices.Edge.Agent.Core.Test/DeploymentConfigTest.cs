// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Test.ConfigSources;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class DeploymentConfigTest
    {
        static readonly IEdgeAgentModule TestEdgeAgent1 = new TestAgentModule(
            "edgeAgent",
            "docker",
            new TestConfig("microsoft/edgeAgent:1.0"),
            ImagePullPolicy.OnCreate,
            new ConfigurationInfo(),
            new Dictionary<string, EnvVal>());

        static readonly IEdgeAgentModule TestEdgeAgent1_1 = new TestAgentModule(
            "edgeAgent",
            "docker",
            new TestConfig("microsoft/edgeAgent:1.0"),
            ImagePullPolicy.OnCreate,
            new ConfigurationInfo(),
            new Dictionary<string, EnvVal>());

        static readonly IEdgeAgentModule TestEdgeAgent2 = new TestAgentModule(
            "edgeAgent",
            "docker",
            new TestConfig("microsoft/edgeAgent:2.0"),
            ImagePullPolicy.OnCreate,
            new ConfigurationInfo(),
            new Dictionary<string, EnvVal>());

        static readonly IEdgeAgentModule TestEdgeAgent3 = new TestAgentModule(
            "edgeAgent",
            "rkt",
            new TestConfig("microsoft/edgeAgent:1.0"),
            ImagePullPolicy.OnCreate,
            new ConfigurationInfo(),
            new Dictionary<string, EnvVal>());

        static readonly IEdgeHubModule TestEdgeHub1 = new TestHubModule(
            "edgeHub",
            "docker",
            ModuleStatus.Running,
            new TestConfig("microsoft/edgeHub:1.0"),
            RestartPolicy.Always,
            ImagePullPolicy.OnCreate,
            new ConfigurationInfo(),
            new Dictionary<string, EnvVal>());

        static readonly IEdgeHubModule TestEdgeHub1_1 = new TestHubModule(
            "edgeHub",
            "docker",
            ModuleStatus.Running,
            new TestConfig("microsoft/edgeHub:1.0"),
            RestartPolicy.Always,
            ImagePullPolicy.OnCreate,
            new ConfigurationInfo(),
            new Dictionary<string, EnvVal>());

        static readonly IEdgeHubModule TestEdgeHub2 = new TestHubModule(
            "edgeHub",
            "docker",
            ModuleStatus.Running,
            new TestConfig("microsoft/edgeHub:2.0"),
            RestartPolicy.Always,
            ImagePullPolicy.OnCreate,
            new ConfigurationInfo(),
            new Dictionary<string, EnvVal>());

        static readonly IEdgeHubModule TestEdgeHub3 = new TestHubModule(
            "edgeHub",
            "rkt",
            ModuleStatus.Running,
            new TestConfig("microsoft/edgeHub:1.0"),
            RestartPolicy.Always,
            ImagePullPolicy.OnCreate,
            new ConfigurationInfo(),
            new Dictionary<string, EnvVal>());

        static readonly IModule TestModule1 = new TestModule(
            "mod1",
            string.Empty,
            "docker",
            ModuleStatus.Running,
            new TestConfig("mod1:v0"),
            RestartPolicy.Always,
            ImagePullPolicy.OnCreate,
            new ConfigurationInfo(),
            new Dictionary<string, EnvVal>());

        static readonly IModule TestModule1_1 = new TestModule(
            "mod1",
            string.Empty,
            "docker",
            ModuleStatus.Running,
            new TestConfig("mod1:v0"),
            RestartPolicy.Always,
            ImagePullPolicy.OnCreate,
            new ConfigurationInfo(),
            new Dictionary<string, EnvVal>());

        static readonly IModule TestModule1_2 = new TestModule(
            "mod1",
            string.Empty,
            "docker",
            ModuleStatus.Running,
            new TestConfig("mod1:v2"),
            RestartPolicy.Always,
            ImagePullPolicy.OnCreate,
            new ConfigurationInfo(),
            new Dictionary<string, EnvVal>());

        static readonly IModule TestModule1_3 = new TestModule(
            "mod1",
            string.Empty,
            "docker",
            ModuleStatus.Stopped,
            new TestConfig("mod1:v0"),
            RestartPolicy.Always,
            ImagePullPolicy.OnCreate,
            new ConfigurationInfo(),
            new Dictionary<string, EnvVal>());

        static readonly IModule TestModule1_4 = new TestModule(
            "mod1",
            string.Empty,
            "docker",
            ModuleStatus.Running,
            new TestConfig("mod1:v0"),
            RestartPolicy.OnFailure,
            ImagePullPolicy.OnCreate,
            new ConfigurationInfo(),
            new Dictionary<string, EnvVal>());

        static readonly IModule TestModule1_5 = new TestModule(
            "mod1",
            string.Empty,
            "docker",
            ModuleStatus.Running,
            new TestConfig("mod1:v0"),
            RestartPolicy.Always,
            ImagePullPolicy.Never,
            new ConfigurationInfo(),
            new Dictionary<string, EnvVal>());

        static readonly IModule TestModule2 = new TestModule(
            "mod2",
            string.Empty,
            "docker",
            ModuleStatus.Running,
            new TestConfig("mod2:v0"),
            RestartPolicy.Always,
            ImagePullPolicy.Never,
            new ConfigurationInfo(),
            new Dictionary<string, EnvVal>());

        static readonly IModule TestModule2_1 = new TestModule(
            "mod2",
            string.Empty,
            "docker",
            ModuleStatus.Running,
            new TestConfig("mod2:v0"),
            RestartPolicy.Always,
            ImagePullPolicy.Never,
            new ConfigurationInfo(),
            new Dictionary<string, EnvVal>());

        static readonly DeploymentConfig Config1 = new DeploymentConfig(
            "1.0",
            new TestRuntimeInfo("docker"),
            new SystemModules(TestEdgeAgent1, TestEdgeHub1),
            new Dictionary<string, IModule>
            {
                ["mod1"] = TestModule1,
                ["mod2"] = TestModule2
            });

        static readonly DeploymentConfig Config1_1 = new DeploymentConfig(
            "1.0",
            new TestRuntimeInfo("docker"),
            new SystemModules(TestEdgeAgent1_1, TestEdgeHub1_1),
            new Dictionary<string, IModule>
            {
                ["mod1"] = TestModule1_1,
                ["mod2"] = TestModule2_1
            });

        static readonly DeploymentConfig Config2 = new DeploymentConfig(
            "2.0",
            new TestRuntimeInfo("docker"),
            new SystemModules(TestEdgeAgent1, TestEdgeHub1),
            new Dictionary<string, IModule>
            {
                ["mod1"] = TestModule1,
                ["mod2"] = TestModule2
            });

        static readonly DeploymentConfig Config3 = new DeploymentConfig(
            "1.0",
            new TestRuntimeInfo("docker1"),
            new SystemModules(TestEdgeAgent1, TestEdgeHub1),
            new Dictionary<string, IModule>
            {
                ["mod1"] = TestModule1,
                ["mod2"] = TestModule2
            });

        static readonly DeploymentConfig Config4 = new DeploymentConfig(
            "1.0",
            new TestRuntimeInfo("docker"),
            new SystemModules(TestEdgeAgent2, TestEdgeHub1),
            new Dictionary<string, IModule>
            {
                ["mod1"] = TestModule1,
                ["mod2"] = TestModule2
            });

        static readonly DeploymentConfig Config5 = new DeploymentConfig(
            "1.0",
            new TestRuntimeInfo("docker"),
            new SystemModules(TestEdgeAgent3, TestEdgeHub1),
            new Dictionary<string, IModule>
            {
                ["mod1"] = TestModule1,
                ["mod2"] = TestModule2
            });

        static readonly DeploymentConfig Config6 = new DeploymentConfig(
            "1.0",
            new TestRuntimeInfo("docker"),
            new SystemModules(TestEdgeAgent1, TestEdgeHub2),
            new Dictionary<string, IModule>
            {
                ["mod1"] = TestModule1,
                ["mod2"] = TestModule2
            });

        static readonly DeploymentConfig Config7 = new DeploymentConfig(
            "1.0",
            new TestRuntimeInfo("docker"),
            new SystemModules(TestEdgeAgent1, TestEdgeHub3),
            new Dictionary<string, IModule>
            {
                ["mod1"] = TestModule1,
                ["mod2"] = TestModule2
            });

        static readonly DeploymentConfig Config8 = new DeploymentConfig(
            "1.0",
            new TestRuntimeInfo("docker"),
            new SystemModules(TestEdgeAgent1, TestEdgeHub1),
            new Dictionary<string, IModule>
            {
                ["mod1"] = TestModule1_2,
                ["mod2"] = TestModule2
            });

        static readonly DeploymentConfig Config9 = new DeploymentConfig(
            "1.0",
            new TestRuntimeInfo("docker"),
            new SystemModules(TestEdgeAgent1, TestEdgeHub1),
            new Dictionary<string, IModule>
            {
                ["mod1"] = TestModule1_3,
                ["mod2"] = TestModule2
            });

        static readonly DeploymentConfig Config10 = new DeploymentConfig(
            "1.0",
            new TestRuntimeInfo("docker"),
            new SystemModules(TestEdgeAgent1, TestEdgeHub1),
            new Dictionary<string, IModule>
            {
                ["mod1"] = TestModule1_4,
                ["mod2"] = TestModule2
            });

        static readonly DeploymentConfig Config11 = new DeploymentConfig(
            "1.0",
            new TestRuntimeInfo("docker"),
            new SystemModules(TestEdgeAgent1, TestEdgeHub1),
            new Dictionary<string, IModule>
            {
                ["mod1"] = TestModule1
            });

        static readonly DeploymentConfig Config12 = new DeploymentConfig(
            "1.0",
            new TestRuntimeInfo("docker"),
            new SystemModules(TestEdgeAgent3, null),
            new Dictionary<string, IModule>
            {
                ["mod1"] = TestModule1,
                ["mod2"] = TestModule2
            });

        static readonly DeploymentConfig Config13 = new DeploymentConfig(
            "1.0",
            new TestRuntimeInfo("docker"),
            new SystemModules(null, TestEdgeHub1),
            new Dictionary<string, IModule>
            {
                ["mod1"] = TestModule1,
                ["mod2"] = TestModule2
            });

        static readonly DeploymentConfig Config14 = new DeploymentConfig(
            "1.0",
            new TestRuntimeInfo("docker"),
            new SystemModules(TestEdgeAgent1, TestEdgeHub1),
            new Dictionary<string, IModule>());

        static readonly DeploymentConfig Config15 = new DeploymentConfig(
            "1.1",
            new TestRuntimeInfo("docker"),
            new SystemModules(TestEdgeAgent3, TestEdgeHub3),
            new Dictionary<string, IModule>
            {
                ["mod1"] = TestModule1_4,
                ["mod2"] = TestModule2
            });

        static readonly DeploymentConfig Config16 = new DeploymentConfig(
            "1.0",
            new TestRuntimeInfo("docker"),
            new SystemModules(TestEdgeAgent1, TestEdgeHub1),
            new Dictionary<string, IModule>
            {
                ["mod1"] = TestModule1_5,
                ["mod2"] = TestModule2
            });

        public static IEnumerable<object[]> EqualityTestData()
        {
            yield return new object[] { Config1, Config1_1, true };
            yield return new object[] { Config1, Config2, false };
            yield return new object[] { Config1, Config3, false };
            yield return new object[] { Config1, Config4, false };
            yield return new object[] { Config1, Config5, false };
            yield return new object[] { Config1, Config6, false };
            yield return new object[] { Config1, Config7, false };
            yield return new object[] { Config1, Config8, false };
            yield return new object[] { Config1, Config9, false };
            yield return new object[] { Config1, Config10, false };
            yield return new object[] { Config1, Config11, false };
            yield return new object[] { Config1, Config12, false };
            yield return new object[] { Config1, Config13, false };
            yield return new object[] { Config1, Config14, false };
            yield return new object[] { Config1, Config15, false };
            yield return new object[] { Config1, Config16, false };
        }

        [Fact]
        public void BasicTest()
        {
            var edgeAgentModule = Mock.Of<IEdgeAgentModule>(m => m.Name == "edgeAgent");
            var edgeHubModule = Mock.Of<IEdgeHubModule>(m => m.Name == "edgeHub");
            var systemModules = new SystemModules(edgeAgentModule, edgeHubModule);

            var mod1 = new TestModule(null, string.Empty, "test", ModuleStatus.Running, new TestConfig("mod1"), RestartPolicy.Always, ImagePullPolicy.OnCreate, new ConfigurationInfo(), null);
            var mod2 = new TestModule(null, string.Empty, "test", ModuleStatus.Running, new TestConfig("mod2"), RestartPolicy.Always, ImagePullPolicy.OnCreate, new ConfigurationInfo(), null);

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

        [Theory]
        [MemberData(nameof(EqualityTestData))]
        public void EqualityTest(DeploymentConfig config1, DeploymentConfig config2, bool areEqual)
        {
            // Act
            bool result = config1.Equals(config2);

            // Assert
            Assert.Equal(areEqual, result);
        }
    }
}
