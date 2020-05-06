// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json.Linq;
    using Xunit;

    public class ImagePullSecretTest
    {
        public static IEnumerable<object[]> GenerateAuthConfig()
        {
            yield return new object[] { new AuthConfig { Username = "one", Password = "two", ServerAddress = "three" } };
        }

        [Theory]
        [Unit]
        [MemberData(nameof(GenerateAuthConfig))]
        public void ImagePullSecretTestGeneration(AuthConfig auth)
        {
            var ips = new ImagePullSecret(auth);
            Assert.Equal($"{auth.Username.ToLower()}-{auth.ServerAddress.ToLower()}", ips.Name);
            var generated = JObject.Parse(ips.GenerateSecret());

            // Validate Json structure
            Assert.NotNull(generated["auths"][auth.ServerAddress]);
            Assert.Equal(generated["auths"][auth.ServerAddress]["username"], auth.Username);
            Assert.Equal(generated["auths"][auth.ServerAddress]["password"], auth.Password);
            Assert.Equal(generated["auths"][auth.ServerAddress]["auth"], Convert.ToBase64String(Encoding.UTF8.GetBytes($"{auth.Username}:{auth.Password}")));
        }
    }
}
