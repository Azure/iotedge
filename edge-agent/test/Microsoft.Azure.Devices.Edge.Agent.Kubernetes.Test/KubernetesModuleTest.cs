// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class KubernetesModuleTest
    {
        [Fact]
        public void CompareModule()
        {
            Dictionary<string, EnvVal> goodEnv = new Dictionary<string, EnvVal>();
            Dictionary<string, EnvVal> newEnv = new Dictionary<string, EnvVal> { ["a"] = new EnvVal("B") };
            IReadOnlyList<string> dockerEnv = new List<string> { "c=d" };
            KubernetesConfig goodConfig = new KubernetesConfig("image:tag", CreatePodParameters.Create(), Option.None<AuthConfig>());
            KubernetesConfig imageDifferent = new KubernetesConfig("image:newtag", CreatePodParameters.Create(), Option.None<AuthConfig>());

            var auth1 = new AuthConfig("secret1");
            KubernetesConfig auth1Config = new KubernetesConfig("image:tag", CreatePodParameters.Create(), Option.Some(auth1));

            var auth2 = new AuthConfig("secret2");
            KubernetesConfig auth2Config = new KubernetesConfig("image:tag", CreatePodParameters.Create(), Option.Some(auth2));

            var auth3 = new AuthConfig("secret3");
            KubernetesConfig auth3Config = new KubernetesConfig("image:tag", CreatePodParameters.Create(), Option.Some(auth3));

            var auth4 = new AuthConfig("secret4");
            KubernetesConfig auth4Config = new KubernetesConfig("image:tag", CreatePodParameters.Create(), Option.Some(auth4));

            KubernetesConfig createContainerConfigDifferent = new KubernetesConfig("image:tag", CreatePodParameters.Create(dockerEnv), Option.None<AuthConfig>());

            ConfigurationInfo goodInfo = new ConfigurationInfo(string.Empty);

            var m1 = new KubernetesModule("name1", "v1", "docker", ModuleStatus.Running, RestartPolicy.Always, goodInfo, goodEnv, goodConfig, ImagePullPolicy.OnCreate);
            var m2 = new KubernetesModule("name2", "v1", "docker", ModuleStatus.Running, RestartPolicy.Always, goodInfo, goodEnv, goodConfig, ImagePullPolicy.OnCreate);

            var m3 = new KubernetesModule("name1", "v1", "docker", ModuleStatus.Running, RestartPolicy.Always, goodInfo, goodEnv, goodConfig, ImagePullPolicy.OnCreate);
            var m4 = new KubernetesModule("name1", "v2", "docker", ModuleStatus.Running, RestartPolicy.Always, goodInfo, goodEnv, goodConfig, ImagePullPolicy.OnCreate);

            var m5 = new KubernetesModule("name1", "v1", "docker", ModuleStatus.Running, RestartPolicy.Always, goodInfo, goodEnv, goodConfig, ImagePullPolicy.OnCreate);
            var m6 = new KubernetesModule("name1", "v1", "docker", ModuleStatus.Stopped, RestartPolicy.Always, goodInfo, goodEnv, goodConfig, ImagePullPolicy.OnCreate);

            var m7 = new KubernetesModule("name1", "v1", "docker", ModuleStatus.Running, RestartPolicy.Always, goodInfo, goodEnv, goodConfig, ImagePullPolicy.OnCreate);
            var m8 = new KubernetesModule("name1", "v1", "docker", ModuleStatus.Running, RestartPolicy.Never, goodInfo, goodEnv, goodConfig, ImagePullPolicy.OnCreate);

            var m9 = new KubernetesModule("name1", "v1", "docker", ModuleStatus.Running, RestartPolicy.Always, goodInfo, goodEnv, imageDifferent, ImagePullPolicy.OnCreate);
            var m10 = new KubernetesModule("name1", "v1", "docker", ModuleStatus.Running, RestartPolicy.Always, goodInfo, goodEnv, auth1Config, ImagePullPolicy.OnCreate);
            var m11 = new KubernetesModule("name1", "v1", "docker", ModuleStatus.Running, RestartPolicy.Always, goodInfo, goodEnv, auth2Config, ImagePullPolicy.OnCreate);
            var m12 = new KubernetesModule("name1", "v1", "docker", ModuleStatus.Running, RestartPolicy.Always, goodInfo, goodEnv, auth3Config, ImagePullPolicy.OnCreate);
            var m13 = new KubernetesModule("name1", "v1", "docker", ModuleStatus.Running, RestartPolicy.Always, goodInfo, goodEnv, auth4Config, ImagePullPolicy.OnCreate);
            var m14 = new KubernetesModule("name1", "v1", "docker", ModuleStatus.Running, RestartPolicy.Always, goodInfo, goodEnv, createContainerConfigDifferent, ImagePullPolicy.OnCreate);

            var m15 = new KubernetesModule("name1", "v1", "docker", ModuleStatus.Running, RestartPolicy.Always, goodInfo, newEnv, goodConfig, ImagePullPolicy.OnCreate);

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
