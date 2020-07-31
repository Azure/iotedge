// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class ModuleIdentityProviderServiceBuilderTest
    {
        [Theory]
        [InlineData("foo.azure.com", "d1", "m1", "1", "localhost")]
        [InlineData("foo.azure.com", "d1", "$edgeAgent", "1", "localhost")]
        public void Test_CreateIdentity_WithEdgelet_DefaultAuthScheme_ShouldCreateIdentity(
            string iotHubHostName, string deviceId, string moduleId, string generationId, string edgeletUri)
        {
            // Arrange
            string defaultAuthScheme = "sasToken";
            var builder = new ModuleIdentityProviderServiceBuilder(iotHubHostName, deviceId);

            // Act
            IModuleIdentity identity = builder.Create(moduleId, generationId, edgeletUri);

            // Assert
            Assert.Equal(iotHubHostName, identity.IotHubHostname);
            Assert.Equal(deviceId, identity.DeviceId);
            Assert.Equal(moduleId, identity.ModuleId);
            var creds = identity.Credentials as IdentityProviderServiceCredentials;
            Assert.NotNull(creds);
            Assert.Equal(edgeletUri, creds.ProviderUri);
            Assert.Equal(defaultAuthScheme, creds.AuthScheme);
            Assert.Equal(generationId, creds.ModuleGenerationId);
            Assert.Equal(Option.None<string>(), creds.Version);
        }

        [Theory]
        [InlineData("foo.azure.com", "d1", "m1", "1", "localhost")]
        [InlineData("foo.azure.com", "d1", "m1", "1", "localhost", "x509")]
        [InlineData("foo.azure.com", "d1", "$edgeAgent", "1", "localhost")]
        [InlineData("foo.azure.com", "d1", "$edgeAgent", "1", "localhost", "x509")]
        public void Test_CreateIdentity_WithEdgelet_SetAuthScheme_ShouldCreateIdentity(
            string iotHubHostName,
            string deviceId,
            string moduleId,
            string generationId,
            string edgeletUri,
            string authScheme = "sasToken")
        {
            // Arrange
            var builder = new ModuleIdentityProviderServiceBuilder(iotHubHostName, deviceId);

            // Act
            IModuleIdentity identity = builder.Create(moduleId, generationId, edgeletUri, authScheme);

            // Assert
            Assert.Equal(iotHubHostName, identity.IotHubHostname);
            Assert.Equal(deviceId, identity.DeviceId);
            Assert.Equal(moduleId, identity.ModuleId);
            var creds = identity.Credentials as IdentityProviderServiceCredentials;
            Assert.NotNull(creds);
            Assert.Equal(edgeletUri, creds.ProviderUri);
            Assert.Equal(authScheme, creds.AuthScheme);
            Assert.Equal(generationId, creds.ModuleGenerationId);
            Assert.Equal(Option.None<string>(), creds.Version);
        }

        [Fact]
        public void InvalidInputsTest()
        {
            Assert.Throws<ArgumentException>(() => new ModuleIdentityProviderServiceBuilder(null, "1"));
            Assert.Throws<ArgumentException>(() => new ModuleIdentityProviderServiceBuilder(string.Empty, "1"));
            Assert.Throws<ArgumentException>(() => new ModuleIdentityProviderServiceBuilder("iothub", null));
            Assert.Throws<ArgumentException>(() => new ModuleIdentityProviderServiceBuilder("iothub", string.Empty));

            var builder = new ModuleIdentityProviderServiceBuilder("foo.azure.com", "device1");

            Assert.Throws<ArgumentException>(() => builder.Create(null, "1", "xyz"));
            Assert.Throws<ArgumentException>(() => builder.Create("localhost", null, "xyz"));
            Assert.Throws<ArgumentException>(() => builder.Create("localhost", "1", null));
            Assert.Throws<ArgumentException>(() => builder.Create(null, "localhost", "1", "sasToken"));
            Assert.Throws<ArgumentException>(() => builder.Create("module1", null, "1", "sasToken"));
            Assert.Throws<ArgumentException>(() => builder.Create("module1", "localhost", null, "sasToken"));
            Assert.Throws<ArgumentException>(() => builder.Create("module1", "localhost", "1", null));
        }
    }
}
