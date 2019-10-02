// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using System;
    using System.Collections.Generic;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;
    using CreateContainerParameters = global::Microsoft.Azure.Devices.Edge.Agent.Docker.Models.CreateContainerParameters;
    using RestartPolicy = Microsoft.Azure.Devices.Edge.Agent.Core.RestartPolicy;

    [Unit]
    public class CombinedDockerModuleTest
    {
        [Fact]
        public void CreationFailsOnInvalidInput()
        {
            CombinedDockerConfig goodCombinedDockerConfig = new CombinedDockerConfig("image:tag", new CreateContainerParameters(), Option.None<AuthConfig>());
            Dictionary<string, EnvVal> goodEnv = new Dictionary<string, EnvVal>();
            ConfigurationInfo goodInfo = new ConfigurationInfo(string.Empty);
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombinedDockerModule("name", "v1", (ModuleStatus)999, RestartPolicy.Always, goodCombinedDockerConfig, goodInfo, goodEnv));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombinedDockerModule("v1", (ModuleStatus)999, RestartPolicy.Always, "docker", goodCombinedDockerConfig, goodEnv));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombinedDockerModule("name", "v1", ModuleStatus.Running, (RestartPolicy)999, goodCombinedDockerConfig, goodInfo, goodEnv));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombinedDockerModule("v1", ModuleStatus.Running, (RestartPolicy)999, "docker", goodCombinedDockerConfig, goodEnv));
            Assert.Throws<ArgumentNullException>(() => new CombinedDockerModule("name", "v1", ModuleStatus.Running, RestartPolicy.Always, null, goodInfo, goodEnv));
            Assert.Throws<ArgumentNullException>(() => new CombinedDockerModule("v1", ModuleStatus.Running, RestartPolicy.Always, "docker", null, goodEnv));
        }

        [Fact]
        public void CreationIsOkForSomeNullValues()
        {
            CombinedDockerConfig goodCombinedDockerConfig = new CombinedDockerConfig("image:tag", new CreateContainerParameters(), Option.None<AuthConfig>());
            var m1 = new CombinedDockerModule("name", null, ModuleStatus.Running, RestartPolicy.Always, goodCombinedDockerConfig, null, null);
            var m2 = new CombinedDockerModule(null, ModuleStatus.Running, RestartPolicy.Always, null, goodCombinedDockerConfig, null);

            Assert.NotNull(m1);
            Assert.NotNull(m2);
        }

        [Fact]
        public void CompareModule()
        {
            var auth1 = new AuthConfig { Username = "auth1", Password = "auth1", ServerAddress = "auth1" };
            var auth2 = new AuthConfig { Username = "auth2", Password = "auth1", ServerAddress = "auth1" };
            var auth3 = new AuthConfig { Username = "auth1", Password = "auth2", ServerAddress = "auth1" };
            var auth4 = new AuthConfig { Username = "auth1", Password = "auth1", ServerAddress = "auth2" };
            Dictionary<string, EnvVal> goodEnv = new Dictionary<string, EnvVal>();
            Dictionary<string, EnvVal> newEnv = new Dictionary<string, EnvVal> { ["a"] = new EnvVal("B") };
            IList<string> dockerEnv = new List<string> { "c=d" };
            CombinedDockerConfig goodCombinedDockerConfig = new CombinedDockerConfig("image:tag", new CreateContainerParameters(), Option.None<AuthConfig>());
            CombinedDockerConfig imageDifferent = new CombinedDockerConfig("image:newtag", new CreateContainerParameters(), Option.None<AuthConfig>());
            CombinedDockerConfig auth1Config = new CombinedDockerConfig("image:tag", new CreateContainerParameters(), Option.Some(auth1));
            CombinedDockerConfig auth2Config = new CombinedDockerConfig("image:tag", new CreateContainerParameters(), Option.Some(auth2));
            CombinedDockerConfig auth3Config = new CombinedDockerConfig("image:tag", new CreateContainerParameters(), Option.Some(auth3));
            CombinedDockerConfig auth4Config = new CombinedDockerConfig("image:tag", new CreateContainerParameters(), Option.Some(auth4));
            CombinedDockerConfig createContainerConfigDifferent = new CombinedDockerConfig("image:tag", new CreateContainerParameters { Env = dockerEnv }, Option.None<AuthConfig>());

            ConfigurationInfo goodInfo = new ConfigurationInfo(string.Empty);

            var m1 = new CombinedDockerModule("name1", "v1", ModuleStatus.Running, RestartPolicy.Always, goodCombinedDockerConfig, goodInfo, goodEnv);
            var m2 = new CombinedDockerModule("name2", "v1", ModuleStatus.Running, RestartPolicy.Always, goodCombinedDockerConfig, goodInfo, goodEnv);

            var m3 = new CombinedDockerModule("name1", "v1", ModuleStatus.Running, RestartPolicy.Always, goodCombinedDockerConfig, goodInfo, goodEnv);
            var m4 = new CombinedDockerModule("name1", "v2", ModuleStatus.Running, RestartPolicy.Always, goodCombinedDockerConfig, goodInfo, goodEnv);

            var m5 = new CombinedDockerModule("name1", "v1", ModuleStatus.Running, RestartPolicy.Always, goodCombinedDockerConfig, goodInfo, goodEnv);
            var m6 = new CombinedDockerModule("name1", "v1", ModuleStatus.Stopped, RestartPolicy.Always, goodCombinedDockerConfig, goodInfo, goodEnv);

            var m7 = new CombinedDockerModule("name1", "v1", ModuleStatus.Running, RestartPolicy.Always, goodCombinedDockerConfig, goodInfo, goodEnv);
            var m8 = new CombinedDockerModule("name1", "v1", ModuleStatus.Running, RestartPolicy.Never,  goodCombinedDockerConfig, goodInfo, goodEnv);

            var m9 = new CombinedDockerModule("name1", "v1", ModuleStatus.Running, RestartPolicy.Always, imageDifferent, goodInfo, goodEnv);
            var m10 = new CombinedDockerModule("name1", "v1", ModuleStatus.Running, RestartPolicy.Always, auth1Config, goodInfo, goodEnv);
            var m11 = new CombinedDockerModule("name1", "v1", ModuleStatus.Running, RestartPolicy.Always, auth2Config, goodInfo, goodEnv);
            var m12 = new CombinedDockerModule("name1", "v1", ModuleStatus.Running, RestartPolicy.Always, auth3Config, goodInfo, goodEnv);
            var m13 = new CombinedDockerModule("name1", "v1", ModuleStatus.Running, RestartPolicy.Always, auth4Config, goodInfo, goodEnv);
            var m14 = new CombinedDockerModule("name1", "v1", ModuleStatus.Running, RestartPolicy.Always, createContainerConfigDifferent, goodInfo, goodEnv);

            var m15 = new CombinedDockerModule("name1", "v1", ModuleStatus.Running, RestartPolicy.Always, goodCombinedDockerConfig, goodInfo, newEnv);

            Assert.NotEqual(m1, m2);
            Assert.NotEqual(m3, m4);
            Assert.NotEqual(m5, m6);
            Assert.NotEqual(m7, m8);
            Assert.NotEqual(m1, m9);
            Assert.NotEqual(m9, m1);
            Assert.NotEqual(m10, m9);
            Assert.NotEqual(m9, m10);
            Assert.NotEqual(m10, m11);
            Assert.NotEqual(m11, m10);
            Assert.NotEqual(m10, m12);
            Assert.NotEqual(m10, m13);
            Assert.NotEqual(m11, m14);
            Assert.NotEqual(m11, m15);

            Assert.True(m5.IsOnlyModuleStatusChanged(m6));

            Assert.False(m1.IsOnlyModuleStatusChanged(m2));
            Assert.False(m1.IsOnlyModuleStatusChanged(m9));
        }
    }
}
