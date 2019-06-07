// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class EdgeAgentDockerModuleTest
    {
        public static IEnumerable<object[]> GetEaModules()
        {
            yield return new object[]
            {
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo"), ImagePullPolicy.OnCreate, null, null),
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo"), ImagePullPolicy.OnCreate, null, null),
                true
            };

            yield return new object[]
            {
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo"), ImagePullPolicy.Never, new ConfigurationInfo("c1"), new Dictionary<string, EnvVal> { ["Env1"] = new EnvVal("EnvVal1") }, "version1"),
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo"), ImagePullPolicy.Never, null, null),
                true
            };

            yield return new object[]
            {
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo"), ImagePullPolicy.OnCreate, new ConfigurationInfo("c1"), new Dictionary<string, EnvVal> { ["Env1"] = new EnvVal("EnvVal1") }, "version1"),
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo"), ImagePullPolicy.OnCreate, new ConfigurationInfo("c2"), new Dictionary<string, EnvVal> { ["Env2"] = new EnvVal("EnvVal2") }, "version2"),
                true
            };

            yield return new object[]
            {
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo", "{}"), ImagePullPolicy.OnCreate, new ConfigurationInfo("c1"), new Dictionary<string, EnvVal> { ["Env1"] = new EnvVal("EnvVal1") }, "version1"),
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo", "{\"HostConfig\":{\"PortBindings\":{\"8883/tcp\":[{\"HostPort\":\"8883\"}],\"443/tcp\":[{\"HostPort\":\"443\"}]}}}"), ImagePullPolicy.OnCreate, new ConfigurationInfo("c2"), new Dictionary<string, EnvVal> { ["Env2"] = new EnvVal("EnvVal2") }, "version2"),
                true
            };

            yield return new object[]
            {
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo"), ImagePullPolicy.OnCreate, null, null),
                new EdgeAgentDockerModule("docker", new DockerConfig("Bar"), ImagePullPolicy.OnCreate, null, null),
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
    }
}
