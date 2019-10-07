// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class ImagePullSecretNameTest
    {
        [Fact]
        public void CreatesImagePullSecretName()
        {
            var auth = new AuthConfig { Username = "name", Password = "password", ServerAddress = "server" };

            var name = ImagePullSecretName.Create(auth);

            Assert.Equal("name-server", name);
        }

        [Fact]
        public void EqualsImagePullSecretsByValue()
        {
            var name = new ImagePullSecretName("secret");

            Assert.True(name.Equals(name));
            Assert.True(name.Equals(new ImagePullSecretName("secret")));

            Assert.False(name.Equals(null));
            Assert.False(name.Equals(new ImagePullSecretName("not a secret")));
        }
    }
}
