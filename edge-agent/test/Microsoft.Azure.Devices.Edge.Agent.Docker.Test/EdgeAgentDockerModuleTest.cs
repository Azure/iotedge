// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
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
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo", "{\"a\": \"b\"}", Option.Maybe("45b23dee08af5e43a7fea6c4cf9c25ccf269ee113168c19722f87876677c5cb2")), ImagePullPolicy.OnCreate, new ConfigurationInfo("c1"), new Dictionary<string, EnvVal> { ["var1"] = new EnvVal("val1") }, "version1"),
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo", "{\"a\": \"b\"}", Option.Maybe("45b23dee08af5e43a7fea6c4cf9c25ccf269ee113168c19722f87876677c5cb2")), ImagePullPolicy.OnCreate, new ConfigurationInfo("c1"), new Dictionary<string, EnvVal> { ["var1"] = new EnvVal("val1") }, "version1"),
                true
            };

            yield return new object[] // simple vs. full match
            {
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo"), ImagePullPolicy.OnCreate, null, null),
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo", "{\"a\": \"b\"}", Option.Maybe("45b23dee08af5e43a7fea6c4cf9c25ccf269ee113168c19722f87876677c5cb2")), ImagePullPolicy.OnCreate, new ConfigurationInfo("c1"), new Dictionary<string, EnvVal> { ["var1"] = new EnvVal("val1") }, "version1"),
                true
            };

            yield return new object[] // image mismatch
            {
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo", "{\"a\": \"b\"}", Option.None<string>()), ImagePullPolicy.OnCreate, new ConfigurationInfo("c1"), new Dictionary<string, EnvVal> { ["var1"] = new EnvVal("val1") }, "version1"),
                new EdgeAgentDockerModule("docker", new DockerConfig("Bar", "{\"a\": \"b\"}", Option.None<string>()), ImagePullPolicy.OnCreate, new ConfigurationInfo("c1"), new Dictionary<string, EnvVal> { ["var1"] = new EnvVal("val1") }, "version1"),
                false
            };

            yield return new object[] // createOptions mismatch
            {
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo", "{\"a\": \"b\"}", Option.None<string>()), ImagePullPolicy.OnCreate, new ConfigurationInfo("c1"), new Dictionary<string, EnvVal> { ["var1"] = new EnvVal("val1") }, "version1"),
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo", "{\"a\": \"x\"}", Option.None<string>()), ImagePullPolicy.OnCreate, new ConfigurationInfo("c1"), new Dictionary<string, EnvVal> { ["var1"] = new EnvVal("val1") }, "version1"),
                true
            };

            yield return new object[] // digest mismatch
            {
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo", "{\"a\": \"b\"}", Option.Some("45b23dee08af5e43a7fea6c4cf9c25ccf269ee113168c19722f87876677c5cb2")), ImagePullPolicy.OnCreate, new ConfigurationInfo("c1"), new Dictionary<string, EnvVal> { ["var1"] = new EnvVal("val1") }, "version1"),
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo", "{\"a\": \"b\"}", Option.Some("45b23dee08af5e43a7fea6c4cf9c25ccf269ee113168c19722f87876677c5cb3")), ImagePullPolicy.OnCreate, new ConfigurationInfo("c1"), new Dictionary<string, EnvVal> { ["var1"] = new EnvVal("val1") }, "version1"),
                true
            };

            yield return new object[] // pull policy mismatch
            {
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo", "{\"a\": \"b\"}", Option.None<string>()), ImagePullPolicy.OnCreate, new ConfigurationInfo("c1"), new Dictionary<string, EnvVal> { ["var1"] = new EnvVal("val1") }, "version1"),
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo", "{\"a\": \"b\"}", Option.None<string>()), ImagePullPolicy.Never, new ConfigurationInfo("c1"), new Dictionary<string, EnvVal> { ["var1"] = new EnvVal("val1") }, "version1"),
                true
            };

            yield return new object[] // env var mismatch
            {
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo", "{\"a\": \"b\"}", Option.None<string>()), ImagePullPolicy.OnCreate, new ConfigurationInfo("c1"), new Dictionary<string, EnvVal>(), "version1"),
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo", "{\"a\": \"b\"}", Option.None<string>()), ImagePullPolicy.OnCreate, new ConfigurationInfo("c1"), new Dictionary<string, EnvVal> { ["var1"] = new EnvVal("val1") }, "version1"),
                true
            };

            yield return new object[] // version mismatch
            {
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo", "{\"a\": \"b\"}", Option.None<string>()), ImagePullPolicy.OnCreate, new ConfigurationInfo("c1"), new Dictionary<string, EnvVal> { ["var1"] = new EnvVal("val1") }, "version1"),
                new EdgeAgentDockerModule("docker", new DockerConfig("Foo", "{\"a\": \"b\"}", Option.None<string>()), ImagePullPolicy.OnCreate, new ConfigurationInfo("c1"), new Dictionary<string, EnvVal> { ["var1"] = new EnvVal("val1") }, "version2"),
                true
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

        public static EdgeAgentDockerRuntimeModule CreateEdgeAgentDockerRuntimeModule(DockerConfig config) =>
            new EdgeAgentDockerRuntimeModule(
                config,
                ModuleStatus.Running,
                0,
                "desc",
                DateTime.Now,
                DateTime.Now,
                ImagePullPolicy.Never,
                new ConfigurationInfo(),
                new Dictionary<string, EnvVal> { ["hello"] = new EnvVal("world") },
                "version2");

        [Fact]
        [Unit]
        public void MixedIModuleImplEqualsTest()
        {
            const string CreateOptions = "{\"HostConfig\":{\"PortBindings\":{\"8883/tcp\":[{\"HostPort\":\"8883\"}]}}}";
            const string Env = "{\"var1\":{\"value\":\"val1\"}}";

            var fullModule = new EdgeAgentDockerModule(
                "docker",
                new DockerConfig("Foo", CreateOptions, Option.None<string>()),
                ImagePullPolicy.OnCreate,
                new ConfigurationInfo(),
                JsonConvert.DeserializeObject<IDictionary<string, EnvVal>>(Env),
                "version1");

            var simpleRuntimeModule = CreateEdgeAgentDockerRuntimeModule(new DockerConfig("Foo"));

            var fullRuntimeModule = CreateEdgeAgentDockerRuntimeModule(new DockerConfig(
                "Foo",
                "{\"ignore\": \"me\"}",
                Option.None<string>()));

            var runtimeModuleWithLabels = CreateEdgeAgentDockerRuntimeModule(new DockerConfig(
                "Foo",
                JsonConvert.SerializeObject(new
                {
                    Labels = new Dictionary<string, object>
                    {
                        [Constants.Labels.CreateOptions] = CreateOptions,
                        [Constants.Labels.Env] = Env
                    }
                }),
                Option.None<string>()));

            var runtimeModuleWithMismatchedCreateOptionsLabel = CreateEdgeAgentDockerRuntimeModule(new DockerConfig(
                "Foo",
                JsonConvert.SerializeObject(new
                {
                    Labels = new Dictionary<string, object>
                    {
                        [Constants.Labels.CreateOptions] = "{\"a\":\"b\"}",
                        [Constants.Labels.Env] = Env
                    }
                }),
                Option.None<string>()));

            var runtimeModuleWithMismatchedEnvLabel = CreateEdgeAgentDockerRuntimeModule(new DockerConfig(
                "Foo",
                JsonConvert.SerializeObject(new
                {
                    Labels = new Dictionary<string, object>
                    {
                        [Constants.Labels.CreateOptions] = CreateOptions,
                        [Constants.Labels.Env] = "{\"a\":{\"value\":\"b\"}}"
                    }
                }),
                Option.None<string>()));

            Assert.True(fullModule.Equals(simpleRuntimeModule));
            Assert.True(fullModule.Equals(fullRuntimeModule));
            Assert.True(fullModule.Equals(runtimeModuleWithLabels));

            Assert.False(fullModule.Equals(runtimeModuleWithMismatchedCreateOptionsLabel));
            Assert.False(fullModule.Equals(runtimeModuleWithMismatchedEnvLabel));
        }
    }
}
