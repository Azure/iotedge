// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Test.ConfigSources;
    using Xunit;

    public class DeploymentConfigInfoTest
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
            "1.0",
            new TestRuntimeInfo("docker"),
            new SystemModules(TestEdgeAgent1, TestEdgeHub1),
            new Dictionary<string, IModule>
            {
                ["mod1"] = TestModule1
            });

        static readonly DeploymentConfigInfo ConfigInfo1 = new DeploymentConfigInfo(1, Config1);
        static readonly DeploymentConfigInfo ConfigInfo1_1 = new DeploymentConfigInfo(1, Config1_1);
        static readonly DeploymentConfigInfo ConfigInfo2 = new DeploymentConfigInfo(1, Config2);
        static readonly DeploymentConfigInfo ConfigInfo3 = new DeploymentConfigInfo(2, Config1_1);

        public static IEnumerable<object[]> EqualityTestData()
        {
            yield return new object[] { ConfigInfo1, ConfigInfo1_1, true };
            yield return new object[] { ConfigInfo1, ConfigInfo2, false };
            yield return new object[] { ConfigInfo1, ConfigInfo3, false };
        }

        [Theory]
        [MemberData(nameof(EqualityTestData))]
        public void TestEquality(DeploymentConfigInfo configInfo1, DeploymentConfigInfo configInfo2, bool areEqual)
        {
            // Act
            bool result = configInfo1.Equals(configInfo2);

            // Assert
            Assert.Equal(areEqual, result);
        }
    }
}
