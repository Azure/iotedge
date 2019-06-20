// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class DockerRuntimeConfigTest
    {
        [Fact]
        public void EqualityTest()
        {
            var drc1 = new DockerRuntimeConfig(
                "1.0",
                new Dictionary<string, RegistryCredentials>
                {
                    ["r1"] = new RegistryCredentials("foo.azurecr.io", "foo", "foo")
                });

            var drc2 = new DockerRuntimeConfig(
                "1.0",
                new Dictionary<string, RegistryCredentials>
                {
                    ["r1"] = new RegistryCredentials("foo.azurecr.io", "foo", "foo")
                });

            Assert.Equal(drc1, drc2);
        }
    }
}
