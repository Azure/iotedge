// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
    using System;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class ModuleIdentityProviderServiceBuilderTest
    {
        [Theory]
        [InlineData("foo.azure.com", "d1", "m1", "1", "localhost")]
        [InlineData("foo.azure.com", "d1", "m1", "1", "localhost", "gateway")]
        public void TestCreateIdentity_WithEdgelet_DefaultAuthScheme_ShouldCreateIdentity(string iotHubHostName, string deviceId, string moduleId, string generationId, string edgeletUri, string gatewayHostName = null)
        {
            // Arrange
            string defaultAuthScheme = "sasToken";
            var builder = new ModuleIdentityProviderServiceBuilder(iotHubHostName, deviceId, gatewayHostName);

            // Act
            IModuleIdentity identity = builder.Create(moduleId, generationId, edgeletUri);


            // Assert
            Assert.Equal(iotHubHostName, identity.IotHubHostname);
            Assert.Equal(gatewayHostName, identity.GatewayHostname);
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
        [InlineData("foo.azure.com", "d1", "m1", "1", "localhost", "x509", "gateway")]
        public void TestCreateIdentity_WithEdgelet_SetAuthScheme_ShouldCreateIdentity(string iotHubHostName, string deviceId, string moduleId, string generationId, string edgeletUri, string authScheme = "sasToken", string gatewayHostName = null)
        {
            // Arrange
            var builder = new ModuleIdentityProviderServiceBuilder(iotHubHostName, deviceId, gatewayHostName);

            // Act
            IModuleIdentity identity = builder.Create(moduleId, generationId, edgeletUri, authScheme);


            // Assert
            Assert.Equal(iotHubHostName, identity.IotHubHostname);
            Assert.Equal(gatewayHostName, identity.GatewayHostname);
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
            Assert.Throws<ArgumentException>(() => new ModuleIdentityProviderServiceBuilder(null, "1", "gateway"));
            Assert.Throws<ArgumentException>(() => new ModuleIdentityProviderServiceBuilder("", "1", "gateway"));
            Assert.Throws<ArgumentException>(() => new ModuleIdentityProviderServiceBuilder("iothub", null, "gateway"));
            Assert.Throws<ArgumentException>(() => new ModuleIdentityProviderServiceBuilder("iothub", "", "gateway"));

            var builder = new ModuleIdentityProviderServiceBuilder("foo.azure.com", "device1", "gateway");

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
