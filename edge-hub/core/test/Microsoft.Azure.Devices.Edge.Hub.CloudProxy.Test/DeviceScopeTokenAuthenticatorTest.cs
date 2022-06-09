// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Authenticators;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class DeviceScopeTokenAuthenticatorTest
    {
        [Fact]
        public async Task AuthenticateTest_Device()
        {
            // Arrange
            string iothubHostName = "testiothub.azure-devices.net";
            string edgehubHostName = "edgehub1";
            string deviceId = "d1";
            var underlyingAuthenticator = Mock.Of<IAuthenticator>();
            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>();
            string key = GetKey();
            var serviceIdentity = new ServiceIdentity(deviceId, "1234", new string[0], new ServiceAuthentication(new SymmetricKeyAuthentication(key, GetKey())), ServiceIdentityStatus.Enabled);
            deviceScopeIdentitiesCache.Setup(d => d.GetServiceIdentity(It.Is<string>(i => i == deviceId)))
                .ReturnsAsync(Option.Some(serviceIdentity));
            deviceScopeIdentitiesCache.Setup(d => d.GetAuthChain(It.Is<string>(i => i == deviceId)))
                .ReturnsAsync(Option.Some(deviceId));

            IAuthenticator authenticator = new DeviceScopeTokenAuthenticator(deviceScopeIdentitiesCache.Object, iothubHostName, edgehubHostName, underlyingAuthenticator, true, true);

            var identity = Mock.Of<IDeviceIdentity>(d => d.DeviceId == deviceId && d.Id == deviceId);
            string token = GetDeviceToken(iothubHostName, deviceId, key);
            var tokenCredentials = Mock.Of<ITokenCredentials>(t => t.Identity == identity && t.Token == token);

            // Act
            bool isAuthenticated = await authenticator.AuthenticateAsync(tokenCredentials);

            // Assert
            Assert.True(isAuthenticated);
            Mock.Get(underlyingAuthenticator).VerifyAll();
        }

        [Fact]
        public async Task AuthenticateTest_DeviceUpdateServiceIdentity()
        {
            // Arrange
            string iothubHostName = "testiothub.azure-devices.net";
            string edgehubHostName = "edgehub1";
            string deviceId = "d1";
            var underlyingAuthenticator = Mock.Of<IAuthenticator>();
            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>();
            string key = GetKey();
            var serviceIdentity1 = new ServiceIdentity(deviceId, "1234", new string[0], new ServiceAuthentication(new SymmetricKeyAuthentication(GetKey(), GetKey())), ServiceIdentityStatus.Enabled);
            var serviceIdentity2 = new ServiceIdentity(deviceId, "1234", new string[0], new ServiceAuthentication(new SymmetricKeyAuthentication(key, GetKey())), ServiceIdentityStatus.Enabled);
            deviceScopeIdentitiesCache.Setup(d => d.GetServiceIdentity(It.Is<string>(i => i == deviceId)))
                .ReturnsAsync(Option.Some(serviceIdentity1));
            deviceScopeIdentitiesCache.Setup(d => d.RefreshServiceIdentity(deviceId))
                .Callback<string>((id) => deviceScopeIdentitiesCache.Setup(d => d.GetServiceIdentity(deviceId)).ReturnsAsync(Option.Some(serviceIdentity2)))
                .Returns(Task.CompletedTask);
            deviceScopeIdentitiesCache.Setup(d => d.GetAuthChain(It.Is<string>(i => i == deviceId)))
                .ReturnsAsync(Option.Some(deviceId));

            IAuthenticator authenticator = new DeviceScopeTokenAuthenticator(deviceScopeIdentitiesCache.Object, iothubHostName, edgehubHostName, underlyingAuthenticator, true, true);

            var identity = Mock.Of<IDeviceIdentity>(d => d.DeviceId == deviceId && d.Id == deviceId);
            string token = GetDeviceToken(iothubHostName, deviceId, key);
            var tokenCredentials = Mock.Of<ITokenCredentials>(t => t.Identity == identity && t.Token == token);

            // Act
            bool isAuthenticated = await authenticator.AuthenticateAsync(tokenCredentials);

            // Assert
            Assert.True(isAuthenticated);
            Mock.Get(underlyingAuthenticator).VerifyAll();
            deviceScopeIdentitiesCache.Verify(d => d.GetServiceIdentity(deviceId), Times.Exactly(2));
            deviceScopeIdentitiesCache.Verify(d => d.RefreshServiceIdentity(deviceId), Times.Once);
        }

        [Fact]
        public async Task ReauthenticateTest_DeviceUpdateServiceIdentity()
        {
            // Arrange
            string iothubHostName = "testiothub.azure-devices.net";
            string edgehubHostName = "edgehub1";
            string deviceId = "d1";
            var underlyingAuthenticator = Mock.Of<IAuthenticator>();
            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>();
            string key = GetKey();
            var serviceIdentity1 = new ServiceIdentity(deviceId, "1234", new string[0], new ServiceAuthentication(new SymmetricKeyAuthentication(GetKey(), GetKey())), ServiceIdentityStatus.Enabled);
            var serviceIdentity2 = new ServiceIdentity(deviceId, "1234", new string[0], new ServiceAuthentication(new SymmetricKeyAuthentication(key, GetKey())), ServiceIdentityStatus.Enabled);
            deviceScopeIdentitiesCache.Setup(d => d.GetServiceIdentity(deviceId))
                .ReturnsAsync(Option.Some(serviceIdentity1));
            deviceScopeIdentitiesCache.Setup(d => d.RefreshServiceIdentity(deviceId))
                .Callback<string>((id) => deviceScopeIdentitiesCache.Setup(d => d.GetServiceIdentity(deviceId)).ReturnsAsync(Option.Some(serviceIdentity2)))
                .Returns(Task.CompletedTask);
            deviceScopeIdentitiesCache.Setup(d => d.GetAuthChain(It.Is<string>(i => i == deviceId)))
                .ReturnsAsync(Option.Some(deviceId));

            IAuthenticator authenticator = new DeviceScopeTokenAuthenticator(deviceScopeIdentitiesCache.Object, iothubHostName, edgehubHostName, underlyingAuthenticator, true, true);

            var identity = Mock.Of<IDeviceIdentity>(d => d.DeviceId == deviceId && d.Id == deviceId);
            string token = GetDeviceToken(iothubHostName, deviceId, key);
            var tokenCredentials = Mock.Of<ITokenCredentials>(t => t.Identity == identity && t.Token == token);

            // Act
            bool isAuthenticated = await authenticator.ReauthenticateAsync(tokenCredentials);

            // Assert
            Assert.False(isAuthenticated);
            Mock.Get(underlyingAuthenticator).VerifyAll();
            deviceScopeIdentitiesCache.Verify(d => d.GetServiceIdentity(deviceId), Times.Once);
            deviceScopeIdentitiesCache.Verify(d => d.RefreshServiceIdentity(deviceId), Times.Never);
        }

        [Fact]
        public async Task AuthenticateTest_Module()
        {
            // Arrange
            string iothubHostName = "testiothub.azure-devices.net";
            string edgehubHostName = "edgehub1";
            string deviceId = "d1";
            string moduleId = "m1";
            var underlyingAuthenticator = Mock.Of<IAuthenticator>();
            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>();
            string key = GetKey();
            var serviceIdentity = new ServiceIdentity(deviceId, moduleId, "e1", new List<string>(), "1234", new string[0], new ServiceAuthentication(new SymmetricKeyAuthentication(key, GetKey())), ServiceIdentityStatus.Enabled);
            deviceScopeIdentitiesCache.Setup(d => d.GetServiceIdentity(It.Is<string>(i => i == $"{deviceId}/{moduleId}")))
                .ReturnsAsync(Option.Some(serviceIdentity));
            deviceScopeIdentitiesCache.Setup(d => d.GetAuthChain(It.Is<string>(i => i == $"{deviceId}/{moduleId}")))
                .ReturnsAsync(Option.Some($"{deviceId}/{moduleId}"));

            IAuthenticator authenticator = new DeviceScopeTokenAuthenticator(deviceScopeIdentitiesCache.Object, iothubHostName, edgehubHostName, underlyingAuthenticator, true, true);

            var identity = Mock.Of<IModuleIdentity>(d => d.DeviceId == deviceId && d.ModuleId == moduleId && d.Id == $"{deviceId}/{moduleId}");
            string token = GetDeviceToken(iothubHostName, deviceId, moduleId, key);
            var tokenCredentials = Mock.Of<ITokenCredentials>(t => t.Identity == identity && t.Token == token);

            // Act
            bool isAuthenticated = await authenticator.AuthenticateAsync(tokenCredentials);

            // Assert
            Assert.True(isAuthenticated);
            Mock.Get(underlyingAuthenticator).VerifyAll();
        }

        [Fact]
        public async Task AuthenticateTest_ModuleWithDeviceToken()
        {
            // Arrange
            string iothubHostName = "testiothub.azure-devices.net";
            string edgehubHostName = "edgehub1";
            string deviceId = "d1";
            string moduleId = "m1";
            var underlyingAuthenticator = Mock.Of<IAuthenticator>();
            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>();
            string key = GetKey();
            var deviceServiceIdentity = new ServiceIdentity(deviceId, "1234", new string[0], new ServiceAuthentication(new SymmetricKeyAuthentication(key, GetKey())), ServiceIdentityStatus.Enabled);
            var moduleServiceIdentity = new ServiceIdentity(deviceId, moduleId, "e1", new List<string>(), "1234", new string[0], new ServiceAuthentication(new SymmetricKeyAuthentication(GetKey(), GetKey())), ServiceIdentityStatus.Enabled);
            deviceScopeIdentitiesCache.Setup(d => d.GetServiceIdentity(It.Is<string>(i => i == $"{deviceId}/{moduleId}")))
                .ReturnsAsync(Option.Some(moduleServiceIdentity));
            deviceScopeIdentitiesCache.Setup(d => d.GetAuthChain(It.Is<string>(i => i == $"{deviceId}/{moduleId}")))
                .ReturnsAsync(Option.Some($"{deviceId}/{moduleId}"));
            deviceScopeIdentitiesCache.Setup(d => d.GetServiceIdentity(It.Is<string>(i => i == deviceId)))
                .ReturnsAsync(Option.Some(deviceServiceIdentity));
            deviceScopeIdentitiesCache.Setup(d => d.GetAuthChain(It.Is<string>(i => i == deviceId)))
                .ReturnsAsync(Option.Some(deviceId));

            IAuthenticator authenticator = new DeviceScopeTokenAuthenticator(deviceScopeIdentitiesCache.Object, iothubHostName, edgehubHostName, underlyingAuthenticator, true, true);

            var identity = Mock.Of<IModuleIdentity>(d => d.DeviceId == deviceId && d.ModuleId == moduleId && d.Id == $"{deviceId}/{moduleId}");
            string token = GetDeviceToken(iothubHostName, deviceId, key);
            var tokenCredentials = Mock.Of<ITokenCredentials>(t => t.Identity == identity && t.Token == token);

            // Act
            bool isAuthenticated = await authenticator.AuthenticateAsync(tokenCredentials);

            // Assert
            Assert.True(isAuthenticated);
            Mock.Get(underlyingAuthenticator).VerifyAll();
        }

        [Fact]
        public async Task AuthenticateTest_ModuleWithDeviceKey()
        {
            // Arrange
            string iothubHostName = "testiothub.azure-devices.net";
            string edgehubHostName = "edgehub1";
            string deviceId = "d1";
            string moduleId = "m1";
            var underlyingAuthenticator = Mock.Of<IAuthenticator>();
            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>();
            string key = GetKey();
            var deviceServiceIdentity = new ServiceIdentity(deviceId, "1234", new string[0], new ServiceAuthentication(new SymmetricKeyAuthentication(key, GetKey())), ServiceIdentityStatus.Enabled);
            var moduleServiceIdentity = new ServiceIdentity(deviceId, moduleId, "e1", new List<string>(), "1234", new string[0], new ServiceAuthentication(new SymmetricKeyAuthentication(GetKey(), GetKey())), ServiceIdentityStatus.Enabled);
            deviceScopeIdentitiesCache.Setup(d => d.GetServiceIdentity(It.Is<string>(i => i == $"{deviceId}/{moduleId}")))
                .ReturnsAsync(Option.Some(moduleServiceIdentity));
            deviceScopeIdentitiesCache.Setup(d => d.GetAuthChain(It.Is<string>(i => i == $"{deviceId}/{moduleId}")))
                .ReturnsAsync(Option.Some($"{deviceId}/{moduleId}"));
            deviceScopeIdentitiesCache.Setup(d => d.GetServiceIdentity(It.Is<string>(i => i == deviceId)))
                .ReturnsAsync(Option.Some(deviceServiceIdentity));
            deviceScopeIdentitiesCache.Setup(d => d.GetAuthChain(It.Is<string>(i => i == deviceId)))
                .ReturnsAsync(Option.Some(deviceId));

            IAuthenticator authenticator = new DeviceScopeTokenAuthenticator(deviceScopeIdentitiesCache.Object, iothubHostName, edgehubHostName, underlyingAuthenticator, true, true);

            var identity = Mock.Of<IModuleIdentity>(d => d.DeviceId == deviceId && d.ModuleId == moduleId && d.Id == $"{deviceId}/{moduleId}");
            string token = GetDeviceToken(iothubHostName, deviceId, moduleId, key);
            var tokenCredentials = Mock.Of<ITokenCredentials>(t => t.Identity == identity && t.Token == token);

            // Act
            bool isAuthenticated = await authenticator.AuthenticateAsync(tokenCredentials);

            // Assert
            Assert.True(isAuthenticated);
            Mock.Get(underlyingAuthenticator).VerifyAll();
        }

        [Fact]
        public async Task AuthenticateTest_Device_ServiceIdentityNotEnabled()
        {
            // Arrange
            string iothubHostName = "testiothub.azure-devices.net";
            string edgehubHostName = "edgehub1";
            string deviceId = "d1";
            var underlyingAuthenticator = Mock.Of<IAuthenticator>();
            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>();
            string key = GetKey();
            var serviceIdentity = new ServiceIdentity(deviceId, "1234", new string[0], new ServiceAuthentication(new SymmetricKeyAuthentication(key, GetKey())), ServiceIdentityStatus.Disabled);
            deviceScopeIdentitiesCache.Setup(d => d.GetServiceIdentity(It.Is<string>(i => i == deviceId)))
                .ReturnsAsync(Option.Some(serviceIdentity));

            IAuthenticator authenticator = new DeviceScopeTokenAuthenticator(deviceScopeIdentitiesCache.Object, iothubHostName, edgehubHostName, underlyingAuthenticator, true, true);

            var identity = Mock.Of<IDeviceIdentity>(d => d.DeviceId == deviceId && d.Id == deviceId);
            string token = GetDeviceToken(iothubHostName, deviceId, key);
            var tokenCredentials = Mock.Of<ITokenCredentials>(t => t.Identity == identity && t.Token == token);

            // Act
            bool isAuthenticated = await authenticator.AuthenticateAsync(tokenCredentials);

            // Assert
            Assert.False(isAuthenticated);
            Mock.Get(underlyingAuthenticator).VerifyAll();
        }

        [Fact]
        public async Task AuthenticateTest_Device_WrongToken()
        {
            // Arrange
            string iothubHostName = "testiothub.azure-devices.net";
            string edgehubHostName = "edgehub1";
            string deviceId = "d1";
            var underlyingAuthenticator = Mock.Of<IAuthenticator>();
            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>();
            string key = GetKey();
            var serviceIdentity = new ServiceIdentity(deviceId, "1234", new string[0], new ServiceAuthentication(new SymmetricKeyAuthentication(GetKey(), GetKey())), ServiceIdentityStatus.Enabled);
            deviceScopeIdentitiesCache.Setup(d => d.GetServiceIdentity(It.Is<string>(i => i == deviceId)))
                .ReturnsAsync(Option.Some(serviceIdentity));

            IAuthenticator authenticator = new DeviceScopeTokenAuthenticator(deviceScopeIdentitiesCache.Object, iothubHostName, edgehubHostName, underlyingAuthenticator, true, true);

            var identity = Mock.Of<IDeviceIdentity>(d => d.DeviceId == deviceId && d.Id == deviceId);
            string token = GetDeviceToken(iothubHostName, deviceId, key);
            var tokenCredentials = Mock.Of<ITokenCredentials>(t => t.Identity == identity && t.Token == token);

            // Act
            bool isAuthenticated = await authenticator.AuthenticateAsync(tokenCredentials);

            // Assert
            Assert.False(isAuthenticated);
            Mock.Get(underlyingAuthenticator).VerifyAll();
        }

        [Fact]
        public async Task AuthenticateTest_Device_TokenExpired()
        {
            // Arrange
            string iothubHostName = "testiothub.azure-devices.net";
            string edgehubHostName = "edgehub1";
            string deviceId = "d1";
            var underlyingAuthenticator = Mock.Of<IAuthenticator>();
            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>();
            string key = GetKey();
            var serviceIdentity = new ServiceIdentity(deviceId, "1234", new string[0], new ServiceAuthentication(new SymmetricKeyAuthentication(key, GetKey())), ServiceIdentityStatus.Enabled);
            deviceScopeIdentitiesCache.Setup(d => d.GetServiceIdentity(It.Is<string>(i => i == deviceId)))
                .ReturnsAsync(Option.Some(serviceIdentity));

            IAuthenticator authenticator = new DeviceScopeTokenAuthenticator(deviceScopeIdentitiesCache.Object, iothubHostName, edgehubHostName, underlyingAuthenticator, true, true);

            var identity = Mock.Of<IDeviceIdentity>(d => d.DeviceId == deviceId && d.Id == deviceId);
            string token = GetDeviceToken(iothubHostName, deviceId, key, TimeSpan.FromHours(-1));
            var tokenCredentials = Mock.Of<ITokenCredentials>(t => t.Identity == identity && t.Token == token);

            // Act
            bool isAuthenticated = await authenticator.AuthenticateAsync(tokenCredentials);

            // Assert
            Assert.False(isAuthenticated);
            Mock.Get(underlyingAuthenticator).VerifyAll();
        }

        [Fact]
        public async Task AuthenticateTest_Nested_Device()
        {
            // Arrange
            string iothubHostName = "testiothub.azure-devices.net";
            string edgehubHostName = "edgehub1";
            string actorDeviceId = "actorEdge";
            string leafDeviceId = "leaf";
            var authChain = Option.Some<string>(leafDeviceId + ";" + actorDeviceId);
            var underlyingAuthenticator = Mock.Of<IAuthenticator>();
            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>();
            string key = GetKey();
            var actorAuth = new SymmetricKeyAuthentication(key, key);
            var actorEdgeHubServiceIdentity = new ServiceIdentity(actorDeviceId, Constants.EdgeHubModuleId, null, new List<string>(), "1234", Enumerable.Empty<string>(), new ServiceAuthentication(actorAuth), ServiceIdentityStatus.Enabled);
            string actorEdgeHubId = actorEdgeHubServiceIdentity.Id;

            deviceScopeIdentitiesCache.Setup(d => d.GetAuthChain(It.Is<string>(i => i == leafDeviceId)))
                .ReturnsAsync(authChain);
            deviceScopeIdentitiesCache.Setup(d => d.GetServiceIdentity(It.Is<string>(i => i == actorEdgeHubId)))
                .ReturnsAsync(Option.Some(actorEdgeHubServiceIdentity));

            var authenticator = new DeviceScopeTokenAuthenticator(deviceScopeIdentitiesCache.Object, iothubHostName, edgehubHostName, underlyingAuthenticator, true, true, true);

            var actorEdgeHubIdentity = Mock.Of<IModuleIdentity>(d => d.DeviceId == actorDeviceId && d.ModuleId == Constants.EdgeHubModuleId && d.Id == $"{actorDeviceId}/{Constants.EdgeHubModuleId}");
            string token = GetDeviceToken(iothubHostName, actorDeviceId, Constants.EdgeHubModuleId, key);
            var tokenCredentials = Mock.Of<ITokenCredentials>(t => t.Identity == actorEdgeHubIdentity && t.Token == token && t.AuthChain == authChain);

            // Act
            bool isAuthenticated = await authenticator.AuthenticateAsync(tokenCredentials);

            // Assert
            Assert.True(isAuthenticated);
            Mock.Get(underlyingAuthenticator).VerifyAll();
        }

        [Fact]
        public async Task AuthenticateTest_Nested_Module()
        {
            // Arrange
            string iothubHostName = "testiothub.azure-devices.net";
            string edgehubHostName = "edgehub1";
            string actorEdgeId = "parentEdge";
            string nestedEdgeId = "childEdge";
            string nestedModuleId = nestedEdgeId + "/" + Constants.EdgeHubModuleId;
            var authChain = Option.Some<string>(nestedModuleId + ";" + nestedEdgeId + ";" + actorEdgeId);
            var underlyingAuthenticator = Mock.Of<IAuthenticator>();
            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>();
            string key = GetKey();
            var nestedModuleIdentity = Mock.Of<IModuleIdentity>(i => i.DeviceId == nestedEdgeId && i.ModuleId == Constants.EdgeHubModuleId && i.Id == nestedModuleId);
            var actorAuth = new SymmetricKeyAuthentication(key, key);
            var actorEdgeHubServiceIdentity = new ServiceIdentity(actorEdgeId, Constants.EdgeHubModuleId, null, new List<string>(), "1234", Enumerable.Empty<string>(), new ServiceAuthentication(actorAuth), ServiceIdentityStatus.Enabled);
            string actorEdgeHubId = actorEdgeHubServiceIdentity.Id;

            deviceScopeIdentitiesCache.Setup(d => d.GetAuthChain(It.Is<string>(i => i == nestedModuleId)))
                .ReturnsAsync(authChain);
            deviceScopeIdentitiesCache.Setup(d => d.GetServiceIdentity(It.Is<string>(i => i == actorEdgeHubId)))
                .ReturnsAsync(Option.Some(actorEdgeHubServiceIdentity));

            var authenticator = new DeviceScopeTokenAuthenticator(deviceScopeIdentitiesCache.Object, iothubHostName, edgehubHostName, underlyingAuthenticator, true, true, true);

            var actorEdgeHubIdentity = Mock.Of<IModuleIdentity>(d => d.DeviceId == actorEdgeId && d.ModuleId == Constants.EdgeHubModuleId && d.Id == $"{actorEdgeId}/{Constants.EdgeHubModuleId}");
            string token = GetDeviceToken(iothubHostName, actorEdgeId, Constants.EdgeHubModuleId, key);
            var tokenCredentials = Mock.Of<ITokenCredentials>(t => t.Identity == actorEdgeHubIdentity && t.Token == token && t.AuthChain == authChain);

            // Act
            bool isAuthenticated = await authenticator.AuthenticateAsync(tokenCredentials);

            // Assert
            Assert.True(isAuthenticated);
            Mock.Get(underlyingAuthenticator).VerifyAll();
        }

        [Fact]
        public void ValidateAudienceTest()
        {
            // Arrange
            string iothubHostName = "testiothub.azure-devices.net";
            string edgehubHostName = "edgehub1";
            string deviceId = "d1";
            var underlyingAuthenticator = Mock.Of<IAuthenticator>();
            var deviceScopeIdentitiesCache = Mock.Of<IDeviceScopeIdentitiesCache>();
            string key = GetKey();

            var authenticator = new DeviceScopeTokenAuthenticator(deviceScopeIdentitiesCache, iothubHostName, edgehubHostName, underlyingAuthenticator, true, true);

            var identity = Mock.Of<IDeviceIdentity>(d => d.DeviceId == deviceId && d.Id == deviceId);
            string token = GetDeviceToken(iothubHostName, deviceId, key);
            SharedAccessSignature sharedAccessSignature = SharedAccessSignature.Parse(iothubHostName, token);
            string audience = sharedAccessSignature.Audience;

            // Act
            bool isAuthenticated = authenticator.ValidateAudience(audience, identity);

            // Assert
            Assert.True(isAuthenticated);
            Mock.Get(underlyingAuthenticator).VerifyAll();
        }

        [Fact]
        public void ValidateAudienceWithEdgeHubHostNameTest()
        {
            // Arrange
            string iothubHostName = "testiothub.azure-devices.net";
            string edgehubHostName = "edgehub1";
            string deviceId = "d1";
            var underlyingAuthenticator = Mock.Of<IAuthenticator>();
            var deviceScopeIdentitiesCache = Mock.Of<IDeviceScopeIdentitiesCache>();
            string key = GetKey();

            var authenticator = new DeviceScopeTokenAuthenticator(deviceScopeIdentitiesCache, iothubHostName, edgehubHostName, underlyingAuthenticator, true, true);

            var identity = Mock.Of<IDeviceIdentity>(d => d.DeviceId == deviceId && d.Id == deviceId);
            string token = GetDeviceToken(edgehubHostName, deviceId, key);
            SharedAccessSignature sharedAccessSignature = SharedAccessSignature.Parse(edgehubHostName, token);
            string audience = sharedAccessSignature.Audience;

            // Act
            bool isAuthenticated = authenticator.ValidateAudience(audience, identity);

            // Assert
            Assert.True(isAuthenticated);
            Mock.Get(underlyingAuthenticator).VerifyAll();
        }

        [Fact]
        public void InvalidAudienceTest_DeviceId()
        {
            // Arrange
            string iothubHostName = "testiothub.azure-devices.net";
            string edgehubHostName = "edgehub1";
            string deviceId = "d1";

            var underlyingAuthenticator = Mock.Of<IAuthenticator>();
            var deviceScopeIdentitiesCache = Mock.Of<IDeviceScopeIdentitiesCache>();
            string key = GetKey();

            var authenticator = new DeviceScopeTokenAuthenticator(deviceScopeIdentitiesCache, iothubHostName, edgehubHostName, underlyingAuthenticator, true, true);

            var identity = Mock.Of<IDeviceIdentity>(d => d.DeviceId == deviceId && d.Id == deviceId);
            string token = GetDeviceToken(edgehubHostName, "d2", key);
            SharedAccessSignature sharedAccessSignature = SharedAccessSignature.Parse(edgehubHostName, token);
            string audience = sharedAccessSignature.Audience;

            // Act
            bool isAuthenticated = authenticator.ValidateAudience(audience, identity);

            // Assert
            Assert.False(isAuthenticated);
            Mock.Get(underlyingAuthenticator).VerifyAll();
        }

        [Fact]
        public void InvalidAudienceTest_Hostname()
        {
            // Arrange
            string iothubHostName = "testiothub.azure-devices.net";
            string edgehubHostName = "edgehub1";
            string deviceId = "d1";

            var underlyingAuthenticator = Mock.Of<IAuthenticator>();
            var deviceScopeIdentitiesCache = Mock.Of<IDeviceScopeIdentitiesCache>();
            string key = GetKey();

            var authenticator = new DeviceScopeTokenAuthenticator(deviceScopeIdentitiesCache, iothubHostName, edgehubHostName, underlyingAuthenticator, true, true);

            var identity = Mock.Of<IDeviceIdentity>(d => d.DeviceId == deviceId && d.Id == deviceId);
            string token = GetDeviceToken("edgehub2", deviceId, key);
            SharedAccessSignature sharedAccessSignature = SharedAccessSignature.Parse(edgehubHostName, token);
            string audience = sharedAccessSignature.Audience;

            // Act
            bool isAuthenticated = authenticator.ValidateAudience(audience, identity);

            // Assert
            Assert.False(isAuthenticated);
            Mock.Get(underlyingAuthenticator).VerifyAll();
        }

        [Fact]
        public void InvalidAudienceTest_Device_Format()
        {
            // Arrange
            string iothubHostName = "testiothub.azure-devices.net";
            string edgehubHostName = "edgehub1";
            string deviceId = "d1";

            var underlyingAuthenticator = Mock.Of<IAuthenticator>();
            var deviceScopeIdentitiesCache = Mock.Of<IDeviceScopeIdentitiesCache>();

            var authenticator = new DeviceScopeTokenAuthenticator(deviceScopeIdentitiesCache, iothubHostName, edgehubHostName, underlyingAuthenticator, true, true);

            var identity = Mock.Of<IDeviceIdentity>(d => d.DeviceId == deviceId && d.Id == deviceId);
            string audience = $"{iothubHostName}/devices/{deviceId}/foo";

            // Act
            bool isAuthenticated = authenticator.ValidateAudience(audience, identity);

            // Assert
            Assert.False(isAuthenticated);
            Mock.Get(underlyingAuthenticator).VerifyAll();
        }

        [Fact]
        public void InvalidAudienceTest_Module_Format()
        {
            // Arrange
            string iothubHostName = "testiothub.azure-devices.net";
            string edgehubHostName = "edgehub1";
            string deviceId = "d1";
            string moduleId = "m1";

            var underlyingAuthenticator = Mock.Of<IAuthenticator>();
            var deviceScopeIdentitiesCache = Mock.Of<IDeviceScopeIdentitiesCache>();

            var authenticator = new DeviceScopeTokenAuthenticator(deviceScopeIdentitiesCache, iothubHostName, edgehubHostName, underlyingAuthenticator, true, true);

            var identity = Mock.Of<IModuleIdentity>(d => d.DeviceId == deviceId && d.ModuleId == moduleId && d.Id == $"{deviceId}/{moduleId}");
            string audience = $"{iothubHostName}/devices/{deviceId}/modules/{moduleId}/m1";

            // Act
            bool isAuthenticated = authenticator.ValidateAudience(audience, identity);

            // Assert
            Assert.False(isAuthenticated);
            Mock.Get(underlyingAuthenticator).VerifyAll();
        }

        [Fact]
        public async Task InvalidAuthChainTest_UnauthorizedActor()
        {
            // Arrange
            string iothubHostName = "testiothub.azure-devices.net";
            string edgehubHostName = "edgehub1";
            string rootEdgeId = "rootEdge";
            string actorEdgeId = "childEdge";
            string leafDeviceId = "leaf";
            var authChain = Option.Some<string>(leafDeviceId + ";" + "NotActorEdge" + ";" + rootEdgeId);
            var underlyingAuthenticator = Mock.Of<IAuthenticator>();
            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>();
            string key = GetKey();

            deviceScopeIdentitiesCache.Setup(d => d.GetAuthChain(It.Is<string>(i => i == leafDeviceId)))
                .ReturnsAsync(authChain);

            var authenticator = new DeviceScopeTokenAuthenticator(deviceScopeIdentitiesCache.Object, iothubHostName, edgehubHostName, underlyingAuthenticator, true, true);

            var identity = Mock.Of<IModuleIdentity>(d => d.DeviceId == actorEdgeId && d.ModuleId == Constants.EdgeHubModuleId && d.Id == $"{actorEdgeId}/{Constants.EdgeHubModuleId}");
            string token = GetDeviceToken(iothubHostName, actorEdgeId, Constants.EdgeHubModuleId, key);
            var tokenCredentials = Mock.Of<ITokenCredentials>(t => t.Identity == identity && t.Token == token);

            // Act
            bool isAuthenticated = await authenticator.AuthenticateAsync(tokenCredentials);

            // Assert
            Assert.False(isAuthenticated);
        }

        [Fact]
        public async Task ValidateUnderlyingAuthenticatorErrorTest()
        {
            // Arrange
            string iothubHostName = "testiothub.azure-devices.net";
            string edgehubHostName = "edgehub1";
            string deviceId = "d1";
            var underlyingAuthenticator = Mock.Of<IAuthenticator>();
            Mock.Get(underlyingAuthenticator).Setup(u => u.AuthenticateAsync(It.IsAny<IClientCredentials>())).ThrowsAsync(new TimeoutException());
            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>();
            deviceScopeIdentitiesCache.Setup(d => d.GetServiceIdentity(It.Is<string>(i => i == deviceId)))
                .ReturnsAsync(Option.None<ServiceIdentity>());

            IAuthenticator authenticator = new DeviceScopeTokenAuthenticator(deviceScopeIdentitiesCache.Object, iothubHostName, edgehubHostName, underlyingAuthenticator, true, true);

            var identity = Mock.Of<IDeviceIdentity>(d => d.DeviceId == deviceId && d.Id == deviceId);
            string token = GetDeviceToken(iothubHostName, deviceId, GetKey());
            var tokenCredentials = Mock.Of<ITokenCredentials>(t => t.Identity == identity && t.Token == token);

            // Act
            await Assert.ThrowsAsync<TimeoutException>(() => authenticator.AuthenticateAsync(tokenCredentials));

            // Assert
            Mock.Get(underlyingAuthenticator).VerifyAll();
            Mock.Get(underlyingAuthenticator).Verify(u => u.AuthenticateAsync(It.IsAny<IClientCredentials>()), Times.Once);
        }

        static string GetDeviceToken(string iothubHostName, string deviceId, string key, TimeSpan timeToLive)
        {
            DateTime startTime = DateTime.UtcNow;
            string audience = WebUtility.UrlEncode($"{iothubHostName}/devices/{deviceId}");
            string expiresOn = SasTokenHelper.BuildExpiresOn(startTime, timeToLive);
            string data = string.Join("\n", new List<string> { audience, expiresOn });
            string signature = Sign(data, key);
            return SasTokenHelper.BuildSasToken(audience, signature, expiresOn);
        }

        static string GetDeviceToken(string iothubHostName, string deviceId, string key)
            => GetDeviceToken(iothubHostName, deviceId, key, TimeSpan.FromHours(1));

        static string GetDeviceToken(string iothubHostName, string deviceId, string moduleId, string key)
        {
            DateTime startTime = DateTime.UtcNow;
            string audience = WebUtility.UrlEncode($"{iothubHostName}/devices/{deviceId}/modules/{moduleId}");
            string expiresOn = SasTokenHelper.BuildExpiresOn(startTime, TimeSpan.FromHours(1));
            string data = string.Join("\n", new List<string> { audience, expiresOn });
            string signature = Sign(data, key);
            return SasTokenHelper.BuildSasToken(audience, signature, expiresOn);
        }

        static string Sign(string requestString, string key)
        {
            using (var algorithm = new HMACSHA256(Convert.FromBase64String(key)))
            {
                return Convert.ToBase64String(algorithm.ComputeHash(Encoding.UTF8.GetBytes(requestString)));
            }
        }

        static string GetKey() => Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()));
    }
}
