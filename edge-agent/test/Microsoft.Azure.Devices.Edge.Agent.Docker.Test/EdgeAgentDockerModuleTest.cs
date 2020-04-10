// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class EdgeAgentDockerModuleTest
    {
        public static IEnumerable<object[]> GetEaModules()
        {
            yield return new object[] // simple matching
            {
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo"), ImagePullPolicy.OnCreate, null, null),
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo"), ImagePullPolicy.OnCreate, null, null),
                true
            };

            yield return new object[] // full matching
            {
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo", "{\"a\": \"b\"}"), ImagePullPolicy.OnCreate, new ConfigurationInfo("c1"), new Dictionary<string, EnvVal> { ["var1"] = new EnvVal("val1") }, "version1"),
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo", "{\"a\": \"b\"}"), ImagePullPolicy.OnCreate, new ConfigurationInfo("c1"), new Dictionary<string, EnvVal> { ["var1"] = new EnvVal("val1") }, "version1"),
                true
            };

            yield return new object[] // simple vs. full mismatch
            {
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo"), ImagePullPolicy.OnCreate, null, null),
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo"), ImagePullPolicy.OnCreate, new ConfigurationInfo("c1"), new Dictionary<string, EnvVal> { ["var1"] = new EnvVal("val1") }, "version1"),
                false
            };

            yield return new object[] // image mismatch
            {
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo", "{\"a\": \"b\"}"), ImagePullPolicy.OnCreate, new ConfigurationInfo("c1"), new Dictionary<string, EnvVal> { ["var1"] = new EnvVal("val1") }, "version1"),
                new EdgeAgentDockerModule("docker", new DockerConfig("Bar", "{\"a\": \"b\"}"), ImagePullPolicy.OnCreate, new ConfigurationInfo("c1"), new Dictionary<string, EnvVal> { ["var1"] = new EnvVal("val1") }, "version1"),
                false
            };

            yield return new object[] // createOptions mismatch
            {
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo", "{\"a\": \"b\"}"), ImagePullPolicy.OnCreate, new ConfigurationInfo("c1"), new Dictionary<string, EnvVal> { ["var1"] = new EnvVal("val1") }, "version1"),
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo", "{\"a\": \"x\"}"), ImagePullPolicy.OnCreate, new ConfigurationInfo("c1"), new Dictionary<string, EnvVal> { ["var1"] = new EnvVal("val1") }, "version1"),
                false
            };

            yield return new object[] // pull policy mismatch
            {
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo", "{\"a\": \"b\"}"), ImagePullPolicy.OnCreate, new ConfigurationInfo("c1"), new Dictionary<string, EnvVal> { ["var1"] = new EnvVal("val1") }, "version1"),
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo", "{\"a\": \"b\"}"), ImagePullPolicy.Never, new ConfigurationInfo("c1"), new Dictionary<string, EnvVal> { ["var1"] = new EnvVal("val1") }, "version1"),
                false
            };

            yield return new object[] // env var mismatch
            {
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo", "{\"a\": \"b\"}"), ImagePullPolicy.OnCreate, new ConfigurationInfo("c1"), new Dictionary<string, EnvVal>(), "version1"),
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo", "{\"a\": \"b\"}"), ImagePullPolicy.OnCreate, new ConfigurationInfo("c1"), new Dictionary<string, EnvVal> { ["var1"] = new EnvVal("val1") }, "version1"),
                false
            };

            yield return new object[] // version mismatch
            {
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo", "{\"a\": \"b\"}"), ImagePullPolicy.OnCreate, new ConfigurationInfo("c1"), new Dictionary<string, EnvVal> { ["var1"] = new EnvVal("val1") }, "version1"),
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo", "{\"a\": \"b\"}"), ImagePullPolicy.OnCreate, new ConfigurationInfo("c1"), new Dictionary<string, EnvVal> { ["var1"] = new EnvVal("val1") }, "version2"),
                false
            };
        }

        [Theory]
        [MemberData(nameof(GetEaModules))]
        [Unit]
        public void EqualsTest(EdgeAgentDockerModule mod1, EdgeAgentDockerModule mod2, bool areEqual)
        {
            // Act
            bool result = mod1.Equals(mod2);

            // Assert
            Assert.Equal(areEqual, result);
        }

        [Fact]
        [Unit]
        public void MixedIModuleImplEqualsTest()
        {
            EdgeAgentDockerRuntimeModule CreateSimpleEdgeAgentDockerRuntimeModule(bool configIsEmpty) =>
                new EdgeAgentDockerRuntimeModule(new DockerConfig("Foo"), ModuleStatus.Running, 0, "desc", DateTime.Now, DateTime.Now, ImagePullPolicy.Never, new ConfigurationInfo(), null, string.Empty, configIsEmpty);

            EdgeAgentDockerRuntimeModule CreateFullEdgeAgentDockerRuntimeModule(bool configIsEmpty) =>
                new EdgeAgentDockerRuntimeModule(new DockerConfig("Foo", "{\"a\": \"b\"}"), ModuleStatus.Running, 0, "desc", DateTime.Now, DateTime.Now, ImagePullPolicy.OnCreate, new ConfigurationInfo(), new Dictionary<string, EnvVal> { ["var1"] = new EnvVal("val1") }, "version1", configIsEmpty);

            var full = new EdgeAgentDockerModule("docker", new DockerConfig("Foo", "{\"a\": \"b\"}"), ImagePullPolicy.OnCreate, new ConfigurationInfo(), new Dictionary<string, EnvVal> { ["var1"] = new EnvVal("val1") }, "version1");

            var simpleBeforeConfig = CreateSimpleEdgeAgentDockerRuntimeModule(configIsEmpty: true);
            var simpleAfterConfig = CreateSimpleEdgeAgentDockerRuntimeModule(configIsEmpty: false);

            var fullBeforeConfig = CreateFullEdgeAgentDockerRuntimeModule(configIsEmpty: true);
            var fullAfterConfig = CreateFullEdgeAgentDockerRuntimeModule(configIsEmpty: false);

            Assert.True(full.Equals(simpleBeforeConfig)); // full == simple only BEFORE config has been cached
            Assert.False(full.Equals(simpleAfterConfig));

            Assert.True(full.Equals(fullBeforeConfig));
            Assert.True(full.Equals(fullAfterConfig));
        }
    }
}
