// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using System;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Xunit;

    [Unit]
    public class ImagePullSecretNameConverterTest
    {
        [Fact]
        public void SerializesImagePullSecretNameAsString()
        {
            var name = new ImagePullSecretName("secret");

            string json = JsonConvert.SerializeObject(name);

            Assert.Equal("\"secret\"", json);
        }

        [Fact]
        public void SerializesImagePullSecretNameInsideWrapperAsString()
        {
            var name = new ImagePullSecretName("secret");
            var auth = new AuthWrapper { Name = name };

            string json = JsonConvert.SerializeObject(auth);

            Assert.Equal("{\"Name\":\"secret\"}", json);
        }

        [Fact]
        public void DeserializesImagePullSecretNameFromInsideWrapper()
        {
            string json = "{\"Name\":\"secret\"}";

            AuthWrapper auth = JsonConvert.DeserializeObject<AuthWrapper>(json);

            Assert.Equal("secret", auth.Name);
        }

        [Fact]
        public void DeserializesImagePullSecretNameFromString()
        {
            string json = "\"secret\"";

            ImagePullSecretName auth = JsonConvert.DeserializeObject<ImagePullSecretName>(json);

            Assert.Equal(new ImagePullSecretName("secret"), auth);
        }

        [Theory]
        [InlineData("\"\"")]
        [InlineData("\" \"")]
        public void UnableToDeserializeImagePullSecretNameFromEmptyString(string json)
        {
            Assert.Throws<ArgumentException>(() => JsonConvert.DeserializeObject<ImagePullSecretName>(json));
        }

        class AuthWrapper
        {
            public ImagePullSecretName Name { get; set; }
        }
    }
}
