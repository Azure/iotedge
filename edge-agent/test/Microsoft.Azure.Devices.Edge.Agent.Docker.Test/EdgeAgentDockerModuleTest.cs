// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
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
            const string CreateOptions = "{\"HostConfig\":{\"PortBindings\":{\"8883/tcp\":[{\"HostPort\":\"8883\"}]}}}";
            const string Env = "{\"var1\":{\"value\":\"val1\"}}";

            var fullModule = new EdgeAgentDockerModule(
                "docker",
                new DockerConfig("Foo", CreateOptions),
                ImagePullPolicy.OnCreate,
                new ConfigurationInfo(),
                JsonConvert.DeserializeObject<IDictionary<string, EnvVal>>(Env),
                "version1");

            var simpleRuntimeModule = new EdgeAgentDockerRuntimeModule(
                new DockerConfig("Foo"),
                ModuleStatus.Running,
                0,
                "desc",
                DateTime.Now,
                DateTime.Now,
                ImagePullPolicy.Never,
                new ConfigurationInfo(),
                null,
                string.Empty);

            var fullRuntimeModule = new EdgeAgentDockerRuntimeModule(
                new DockerConfig("Foo", createOptions: "{\"ignore\": \"me\"}"),
                ModuleStatus.Running,
                0,
                "desc",
                DateTime.Now,
                DateTime.Now,
                ImagePullPolicy.OnCreate,
                new ConfigurationInfo(),
                new Dictionary<string, EnvVal> { ["hello"] = new EnvVal("world") },
                "version1");

            var runtimeModuleWithLabels = new EdgeAgentDockerRuntimeModule(
                new DockerConfig("Foo", JsonConvert.SerializeObject(new
                {
                    Labels = new Dictionary<string, object>
                    {
                        [Constants.Labels.CreateOptions] = CreateOptions,
                        [Constants.Labels.Env] = Env
                    }
                })),
                ModuleStatus.Running,
                0,
                "desc",
                DateTime.Now,
                DateTime.Now,
                ImagePullPolicy.OnCreate,
                new ConfigurationInfo(),
                new Dictionary<string, EnvVal> { ["hello"] = new EnvVal("world") },
                "version1");

            Assert.True(fullModule.Equals(simpleRuntimeModule));
            Assert.True(fullModule.Equals(fullRuntimeModule));
            Assert.True(fullModule.Equals(runtimeModuleWithLabels));
        }
    }
}