// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class ServiceIdentitySerializationTest
    {
        [Fact]
        [Unit]
        public void RoundtripServiceIdentitySasAuthTest_Device()
        {
            // Arrange
            var serviceAuthentication = new ServiceAuthentication(new SymmetricKeyAuthentication(Guid.NewGuid().ToString(), Guid.NewGuid().ToString()));
            var serviceIdentity = new ServiceIdentity(
                "d1",
                "12345",
                new List<string> { Constants.IotEdgeIdentityCapability },
                serviceAuthentication,
                ServiceIdentityStatus.Enabled);

            // Act
            string json = serviceIdentity.ToJson();
            var roundtripServiceIdentity = json.FromJson<ServiceIdentity>();

            // Assert
            Assert.NotNull(roundtripServiceIdentity);
            Assert.Equal(serviceIdentity.DeviceId, roundtripServiceIdentity.DeviceId);
            Assert.Equal(serviceIdentity.Id, roundtripServiceIdentity.Id);
            Assert.False(serviceIdentity.ModuleId.HasValue);
            Assert.Equal(serviceIdentity.IsModule, roundtripServiceIdentity.IsModule);
            Assert.Equal(serviceIdentity.Status, roundtripServiceIdentity.Status);
            Assert.Equal(serviceIdentity.GenerationId, roundtripServiceIdentity.GenerationId);
            Assert.Equal(serviceIdentity.Capabilities, roundtripServiceIdentity.Capabilities);
            Assert.Equal(serviceIdentity.Authentication.Type, roundtripServiceIdentity.Authentication.Type);
            Assert.True(serviceIdentity.Authentication.SymmetricKey.HasValue);
            Assert.Equal(serviceIdentity.Authentication.SymmetricKey.OrDefault().PrimaryKey, roundtripServiceIdentity.Authentication.SymmetricKey.OrDefault().PrimaryKey);
            Assert.Equal(serviceIdentity.Authentication.SymmetricKey.OrDefault().SecondaryKey, roundtripServiceIdentity.Authentication.SymmetricKey.OrDefault().SecondaryKey);
        }

        [Fact]
        [Unit]
        public void RoundtripServiceIdentitySasAuthTest_Module()
        {
            // Arrange
            var serviceAuthentication = new ServiceAuthentication(new SymmetricKeyAuthentication(Guid.NewGuid().ToString(), Guid.NewGuid().ToString()));
            var serviceIdentity = new ServiceIdentity(
                "d1",
                "m1",
                "12345",
                new List<string> { Constants.IotEdgeIdentityCapability },
                serviceAuthentication,
                ServiceIdentityStatus.Enabled);

            // Act
            string json = serviceIdentity.ToJson();
            var roundtripServiceIdentity = json.FromJson<ServiceIdentity>();

            // Assert
            Assert.NotNull(roundtripServiceIdentity);
            Assert.Equal(serviceIdentity.DeviceId, roundtripServiceIdentity.DeviceId);
            Assert.Equal(serviceIdentity.Id, roundtripServiceIdentity.Id);
            Assert.Equal(serviceIdentity.IsModule, roundtripServiceIdentity.IsModule);
            Assert.Equal(serviceIdentity.ModuleId.OrDefault(), roundtripServiceIdentity.ModuleId.OrDefault());
            Assert.Equal(serviceIdentity.Status, roundtripServiceIdentity.Status);
            Assert.Equal(serviceIdentity.GenerationId, roundtripServiceIdentity.GenerationId);
            Assert.Equal(serviceIdentity.Capabilities, roundtripServiceIdentity.Capabilities);
            Assert.Equal(serviceIdentity.Authentication.Type, roundtripServiceIdentity.Authentication.Type);
            Assert.True(serviceIdentity.Authentication.SymmetricKey.HasValue);
            Assert.Equal(serviceIdentity.Authentication.SymmetricKey.OrDefault().PrimaryKey, roundtripServiceIdentity.Authentication.SymmetricKey.OrDefault().PrimaryKey);
            Assert.Equal(serviceIdentity.Authentication.SymmetricKey.OrDefault().SecondaryKey, roundtripServiceIdentity.Authentication.SymmetricKey.OrDefault().SecondaryKey);
        }

        [Fact]
        [Unit]
        public void RoundtripServiceIdentityThumbprintAuthTest_Device()
        {
            // Arrange
            var serviceAuthentication = new ServiceAuthentication(new X509ThumbprintAuthentication(Guid.NewGuid().ToString(), Guid.NewGuid().ToString()));
            var serviceIdentity = new ServiceIdentity(
                "d1",
                "12345",
                new List<string> { Constants.IotEdgeIdentityCapability },
                serviceAuthentication,
                ServiceIdentityStatus.Enabled);

            // Act
            string json = serviceIdentity.ToJson();
            var roundtripServiceIdentity = json.FromJson<ServiceIdentity>();

            // Assert
            Assert.NotNull(roundtripServiceIdentity);
            Assert.Equal(serviceIdentity.DeviceId, roundtripServiceIdentity.DeviceId);
            Assert.Equal(serviceIdentity.Id, roundtripServiceIdentity.Id);
            Assert.Equal(serviceIdentity.ModuleId.OrDefault(), roundtripServiceIdentity.ModuleId.OrDefault());
            Assert.Equal(serviceIdentity.Status, roundtripServiceIdentity.Status);
            Assert.Equal(serviceIdentity.GenerationId, roundtripServiceIdentity.GenerationId);
            Assert.Equal(serviceIdentity.Capabilities, roundtripServiceIdentity.Capabilities);
            Assert.Equal(serviceIdentity.Authentication.Type, roundtripServiceIdentity.Authentication.Type);
            Assert.True(serviceIdentity.Authentication.X509Thumbprint.HasValue);
            Assert.Equal(serviceIdentity.Authentication.X509Thumbprint.OrDefault().PrimaryThumbprint, roundtripServiceIdentity.Authentication.X509Thumbprint.OrDefault().PrimaryThumbprint);
            Assert.Equal(serviceIdentity.Authentication.X509Thumbprint.OrDefault().SecondaryThumbprint, roundtripServiceIdentity.Authentication.X509Thumbprint.OrDefault().SecondaryThumbprint);
        }

        [Fact]
        [Unit]
        public void RoundtripServiceIdentityThumbprintAuthTest_Module()
        {
            // Arrange
            var serviceAuthentication = new ServiceAuthentication(new X509ThumbprintAuthentication(Guid.NewGuid().ToString(), Guid.NewGuid().ToString()));
            var serviceIdentity = new ServiceIdentity(
                "d1",
                "m1",
                "12345",
                new List<string>(),
                serviceAuthentication,
                ServiceIdentityStatus.Disabled);

            // Act
            string json = serviceIdentity.ToJson();
            var roundtripServiceIdentity = json.FromJson<ServiceIdentity>();

            // Assert
            Assert.NotNull(roundtripServiceIdentity);
            Assert.Equal(serviceIdentity.DeviceId, roundtripServiceIdentity.DeviceId);
            Assert.Equal(serviceIdentity.Id, roundtripServiceIdentity.Id);
            Assert.Equal(serviceIdentity.IsModule, roundtripServiceIdentity.IsModule);
            Assert.Equal(serviceIdentity.ModuleId.OrDefault(), roundtripServiceIdentity.ModuleId.OrDefault());
            Assert.Equal(serviceIdentity.Status, roundtripServiceIdentity.Status);
            Assert.Equal(serviceIdentity.GenerationId, roundtripServiceIdentity.GenerationId);
            Assert.Equal(serviceIdentity.Capabilities, roundtripServiceIdentity.Capabilities);
            Assert.Equal(serviceIdentity.Authentication.Type, roundtripServiceIdentity.Authentication.Type);
            Assert.True(serviceIdentity.Authentication.X509Thumbprint.HasValue);
            Assert.Equal(serviceIdentity.Authentication.X509Thumbprint.OrDefault().PrimaryThumbprint, roundtripServiceIdentity.Authentication.X509Thumbprint.OrDefault().PrimaryThumbprint);
            Assert.Equal(serviceIdentity.Authentication.X509Thumbprint.OrDefault().SecondaryThumbprint, roundtripServiceIdentity.Authentication.X509Thumbprint.OrDefault().SecondaryThumbprint);
        }

        [Fact]
        [Unit]
        public void RoundtripServiceIdentityCertAuthorityTest()
        {
            // Arrange
            var serviceAuthentication = new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority);
            var serviceIdentity = new ServiceIdentity(
                "d1",
                "m1",
                "12345",
                new List<string> { Constants.IotEdgeIdentityCapability },
                serviceAuthentication,
                ServiceIdentityStatus.Enabled);

            // Act
            string json = serviceIdentity.ToJson();
            var roundtripServiceIdentity = json.FromJson<ServiceIdentity>();

            // Assert
            Assert.NotNull(roundtripServiceIdentity);
            Assert.Equal(serviceIdentity.DeviceId, roundtripServiceIdentity.DeviceId);
            Assert.Equal(serviceIdentity.Id, roundtripServiceIdentity.Id);
            Assert.Equal(serviceIdentity.IsModule, roundtripServiceIdentity.IsModule);
            Assert.Equal(serviceIdentity.ModuleId.OrDefault(), roundtripServiceIdentity.ModuleId.OrDefault());
            Assert.Equal(serviceIdentity.Status, roundtripServiceIdentity.Status);
            Assert.Equal(serviceIdentity.GenerationId, roundtripServiceIdentity.GenerationId);
            Assert.Equal(serviceIdentity.Capabilities, roundtripServiceIdentity.Capabilities);
            Assert.Equal(serviceIdentity.Authentication.Type, roundtripServiceIdentity.Authentication.Type);
            Assert.False(serviceIdentity.Authentication.X509Thumbprint.HasValue);
            Assert.False(serviceIdentity.Authentication.SymmetricKey.HasValue);
        }

        [Fact]
        [Unit]
        public void RoundtripServiceIdentityNoneAuthorityTest()
        {
            // Arrange
            var serviceAuthentication = new ServiceAuthentication(ServiceAuthenticationType.None);
            var serviceIdentity = new ServiceIdentity(
                "d1",
                "m1",
                "12345",
                new List<string>(),
                serviceAuthentication,
                ServiceIdentityStatus.Disabled);

            // Act
            string json = serviceIdentity.ToJson();
            var roundtripServiceIdentity = json.FromJson<ServiceIdentity>();

            // Assert
            Assert.NotNull(roundtripServiceIdentity);
            Assert.Equal(serviceIdentity.DeviceId, roundtripServiceIdentity.DeviceId);
            Assert.Equal(serviceIdentity.Id, roundtripServiceIdentity.Id);
            Assert.Equal(serviceIdentity.IsModule, roundtripServiceIdentity.IsModule);
            Assert.Equal(serviceIdentity.ModuleId.OrDefault(), roundtripServiceIdentity.ModuleId.OrDefault());
            Assert.Equal(serviceIdentity.Status, roundtripServiceIdentity.Status);
            Assert.Equal(serviceIdentity.GenerationId, roundtripServiceIdentity.GenerationId);
            Assert.Equal(serviceIdentity.Capabilities, roundtripServiceIdentity.Capabilities);
            Assert.Equal(serviceIdentity.Authentication.Type, roundtripServiceIdentity.Authentication.Type);
            Assert.False(serviceIdentity.Authentication.X509Thumbprint.HasValue);
            Assert.False(serviceIdentity.Authentication.SymmetricKey.HasValue);
        }
    }
}
