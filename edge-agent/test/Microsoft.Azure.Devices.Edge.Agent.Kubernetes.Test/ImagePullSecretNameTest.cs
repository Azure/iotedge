// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using System.Collections.Generic;
    using System.Linq;
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

        public static IEnumerable<object[]> ValidServerAddresses()
        {
            (string ipsName,
             AuthConfig auth
            )[] data =
            {
                (
                    "dockeruser-index.docker.io",
                    new AuthConfig { Username = "dockeruser", Password = "password", ServerAddress = "https://index.docker.io/v1/" }
                ),
                (
                    "dockeruser-a-registry.com-8000",
                    new AuthConfig { Username = "dockeruser", Password = "password", ServerAddress = "http://a-registry.com:8000" }
                ),
                (
                    "dockeruser-a-registry.com-8000",
                    new AuthConfig { Username = "dockeruser", Password = "password", ServerAddress = "https://a-registry.com:8000" }
                ),
                (
                    "dockeruser-a-registry.com-8000",
                    new AuthConfig { Username = "dockeruser", Password = "password", ServerAddress = "http://a-registry.com:8000/v2/" }
                ),
                (
                    "dockeruser-a-registry.com-8000",
                    new AuthConfig { Username = "dockeruser", Password = "password", ServerAddress = "HTTP://a-registry.com:8000/v2" }
                ),
                (
                    "dockeruser-docker.io",
                    new AuthConfig { Username = "dockeruser", Password = "password", ServerAddress = "docker.io" }
                ),
                (
                    "dockeruser-my-acr.io-8080",
                    new AuthConfig { Username = "dockeruser", Password = "password", ServerAddress = "my-acr.io:8080" }
                ),
                (
                    "dockeruser-any-acr-server.azurecr.io",
                    new AuthConfig { Username = "dockeruser", Password = "password", ServerAddress = "any-acr-server.azurecr.io" }
                ),
                (
                    "dockeruser-any-acr-server.azurecr.io-3030",
                    new AuthConfig { Username = "dockeruser", Password = "password", ServerAddress = "any-acr-server.azurecr.io:3030" }
                ),
                (
                    "thisisavalidusername-thisisavalidcontainerregistrynamewith50characters",
                    new AuthConfig { Username = "thisisavalidusername", Password = "password", ServerAddress = "thisisavalidcontainerregistrynamewith50characters.azurecr.io" }
                ),
            };
            return data.Select(
                d => new object[]
                {
                    d.ipsName, d.auth
                });
        }

        [Theory]
        [MemberData(nameof(ValidServerAddresses))]
        [Unit]
        public void CreatesValidImagePullNames(string ipsName, AuthConfig auth)
        {
            var name = ImagePullSecretName.Create(auth);

            Assert.Equal(ipsName, name);
        }

        public static IEnumerable<object[]> InvalidServerAddresses()
        {
            AuthConfig[] data =
            {
                new AuthConfig { Username = "--", Password = "password", ServerAddress = "--" },
                new AuthConfig { Username = "1111", Password = "password", ServerAddress = "2222" },
            };
            return data.Select(
                d => new object[]
                {
                    d
                });
        }

        [Theory]
        [MemberData(nameof(InvalidServerAddresses))]
        [Unit]
        public void ThrowsOnInvalidImagePullNames(AuthConfig auth)
        {
            Assert.Throws<InvalidKubernetesNameException>(() => ImagePullSecretName.Create(auth));
        }
    }
}
