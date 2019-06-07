// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class DockerRuntimeInfoTest
    {
        [Fact]
        public void EqualityTest()
        {
            var dri1 = new DockerRuntimeInfo(
                "docker",
                new DockerRuntimeConfig(
                    "1.0",
                    new Dictionary<string, RegistryCredentials>
                    {
                        ["r1"] = new RegistryCredentials("foo.azurecr.io", "foo", "foo")
                    }));

            var dri2 = new DockerRuntimeInfo(
                "docker",
                new DockerRuntimeConfig(
                    "1.0",
                    new Dictionary<string, RegistryCredentials>
                    {
                        ["r1"] = new RegistryCredentials("foo.azurecr.io", "foo", "foo")
                    }));

            Assert.Equal(dri1, dri2);
        }
    }
}
