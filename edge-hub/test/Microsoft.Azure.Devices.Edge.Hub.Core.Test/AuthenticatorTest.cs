// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Authenticators;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    public class AuthenticatorTest
    {
        const string TestIotHub = "iothub1.azure.net";

        [Fact]
        [Unit]
        public void AuthenticatorConstructorTest()
        {
            var connectionManager = Mock.Of<IConnectionManager>();
            var credentialsCache = Mock.Of<ICredentialsCache>();
            var certificateAuthenticator = Mock.Of<IAuthenticator>();
            Assert.NotNull(new Authenticator(new TokenCacheAuthenticator(new CloudTokenAuthenticator(connectionManager, TestIotHub), new NullCredentialsCache(), TestIotHub), certificateAuthenticator, credentialsCache));
        }

        [Fact]
        [Unit]
        public void AuthenticatorConstructor_NullTokenAuthenticatorTest()
        {
            Assert.Throws<ArgumentNullException>(() => new Authenticator(null, Mock.Of<IAuthenticator>(), Mock.Of<ICredentialsCache>()));
        }

        [Fact]
        [Unit]
        public void AuthenticatorConstructor_NullCertificateAuthenticatorTest()
        {
            Assert.Throws<ArgumentNullException>(() => new Authenticator(Mock.Of<IAuthenticator>(), null, Mock.Of<ICredentialsCache>()));
        }

        [Fact]
        [Unit]
        public async Task AuthenticateTest()
        {
            var cloudProxy = Mock.Of<ICloudProxy>();
            var connectionManager = Mock.Of<IConnectionManager>();
            var certificateAuthenticator = Mock.Of<IAuthenticator>();
            var credentialsCache = Mock.Of<ICredentialsCache>();
            var clientCredentials = Mock.Of<ITokenCredentials>(c => c.Identity == Mock.Of<IIdentity>());

            Mock.Get(connectionManager).Setup(cm => cm.CreateCloudConnectionAsync(clientCredentials)).ReturnsAsync(Try.Success(cloudProxy));
            Mock.Get(connectionManager).Setup(cm => cm.AddDeviceConnection(It.IsAny<IIdentity>(), It.IsAny<IDeviceProxy>())).Returns(Task.CompletedTask);
            Mock.Get(cloudProxy).Setup(cp => cp.IsActive).Returns(true);

            var authenticator = new Authenticator(new TokenCacheAuthenticator(new CloudTokenAuthenticator(connectionManager, TestIotHub), new NullCredentialsCache(), TestIotHub), certificateAuthenticator, credentialsCache);
            Assert.True(await authenticator.AuthenticateAsync(clientCredentials));
            Mock.Get(connectionManager).Verify();
        }

        [Fact]
        [Unit]
        public async Task Authenticate_InactiveProxyTest()
        {
            var cloudProxy = Mock.Of<ICloudProxy>();
            var connectionManager = Mock.Of<IConnectionManager>();
            var certificateAuthenticator = Mock.Of<IAuthenticator>();
            var credentialsCache = Mock.Of<ICredentialsCache>();
            var clientCredentials = Mock.Of<IClientCredentials>(c => c.Identity == Mock.Of<IIdentity>());

            Mock.Get(connectionManager).Setup(cm => cm.CreateCloudConnectionAsync(clientCredentials)).ReturnsAsync(Try.Success(cloudProxy));
            Mock.Get(connectionManager).Setup(cm => cm.AddDeviceConnection(It.IsAny<IIdentity>(), It.IsAny<IDeviceProxy>())).Returns(Task.CompletedTask);
            Mock.Get(cloudProxy).Setup(cp => cp.IsActive).Returns(false);

            var authenticator = new Authenticator(new TokenCacheAuthenticator(new CloudTokenAuthenticator(connectionManager, TestIotHub), new NullCredentialsCache(), TestIotHub), certificateAuthenticator, credentialsCache);
            Assert.False(await authenticator.AuthenticateAsync(clientCredentials));
            Mock.Get(connectionManager).Verify(c => c.AddDeviceConnection(It.IsAny<IIdentity>(), It.IsAny<IDeviceProxy>()), Times.Never);
        }

        [Fact]
        [Unit]
        public async Task Authenticate_ConnectionManagerThrowsTest()
        {
            var cloudProxy = Mock.Of<ICloudProxy>();
            var connectionManager = Mock.Of<IConnectionManager>();
            var certificateAuthenticator = Mock.Of<IAuthenticator>();
            var credentialsCache = Mock.Of<ICredentialsCache>();
            var clientCredentials = Mock.Of<IClientCredentials>(c => c.Identity == Mock.Of<IIdentity>());

            Mock.Get(connectionManager).Setup(cm => cm.CreateCloudConnectionAsync(clientCredentials)).ReturnsAsync(Try<ICloudProxy>.Failure(new ArgumentException()));
            Mock.Get(connectionManager).Setup(cm => cm.AddDeviceConnection(It.IsAny<IIdentity>(), It.IsAny<IDeviceProxy>())).Returns(Task.CompletedTask);
            Mock.Get(cloudProxy).Setup(cp => cp.IsActive).Returns(true);

            var authenticator = new Authenticator(new TokenCacheAuthenticator(new CloudTokenAuthenticator(connectionManager, TestIotHub), new NullCredentialsCache(), TestIotHub), certificateAuthenticator, credentialsCache);
            Assert.False(await authenticator.AuthenticateAsync(clientCredentials));
            Mock.Get(connectionManager).Verify(c => c.AddDeviceConnection(It.IsAny<IIdentity>(), It.IsAny<IDeviceProxy>()), Times.Never);
        }

        [Fact]
        [Unit]
        public async Task Authenticate_NonNullIdentityTest()
        {
            var connectionManager = Mock.Of<IConnectionManager>();
            var certificateAuthenticator = Mock.Of<IAuthenticator>();
            var credentialsCache = Mock.Of<ICredentialsCache>();
            var clientCredentials = Mock.Of<IClientCredentials>(c => c.Identity == Mock.Of<IModuleIdentity>(i => i.DeviceId == "my-device"));

            var authenticator = new Authenticator(new TokenCacheAuthenticator(new CloudTokenAuthenticator(connectionManager, TestIotHub), new NullCredentialsCache(), TestIotHub), certificateAuthenticator, credentialsCache);
            Assert.False(await authenticator.AuthenticateAsync(clientCredentials));
        }

        [Fact]
        [Unit]
        public async Task Authenticate_NullIdentityTest()
        {
            var connectionManager = Mock.Of<IConnectionManager>();
            var certificateAuthenticator = Mock.Of<IAuthenticator>();
            var credentialsCache = Mock.Of<ICredentialsCache>();
            var authenticator = new Authenticator(new TokenCacheAuthenticator(new CloudTokenAuthenticator(connectionManager, TestIotHub), new NullCredentialsCache(), TestIotHub), certificateAuthenticator, credentialsCache);
            await Assert.ThrowsAsync<ArgumentNullException>(() => authenticator.AuthenticateAsync(null));
        }

        [Fact]
        [Unit]
        public async Task Authenticate_X509Identity()
        {
            var connectionManager = Mock.Of<IConnectionManager>();
            var credentialsCache = Mock.Of<ICredentialsCache>();
            var certificateAuthenticator = Mock.Of<IAuthenticator>();
            var clientCredentials = Mock.Of<ICertificateCredentials>(
                c =>
                    c.Identity == Mock.Of<IModuleIdentity>(i => i.DeviceId == "my-device")
                    && c.AuthenticationType == AuthenticationType.X509Cert);
            var tokenAuthenticator = Mock.Of<IAuthenticator>();
            Mock.Get(credentialsCache).Setup(cc => cc.Add(clientCredentials)).Returns(Task.CompletedTask);
            Mock.Get(certificateAuthenticator).Setup(ca => ca.AuthenticateAsync(clientCredentials)).ReturnsAsync(true);
            var authenticator = new Authenticator(tokenAuthenticator, certificateAuthenticator, credentialsCache);
            Assert.True(await authenticator.AuthenticateAsync(clientCredentials));
            Mock.Get(credentialsCache).Verify(cc => cc.Add(clientCredentials));
        }
    }
}
