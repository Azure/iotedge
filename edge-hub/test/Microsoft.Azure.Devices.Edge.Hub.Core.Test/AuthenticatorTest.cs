// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
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
            Assert.NotNull(new Authenticator(new TokenCredentialsAuthenticator(connectionManager, new NullCredentialsStore(), TestIotHub), "your-device", connectionManager));
        }

        [Fact]
        [Unit]
        public void AuthenticatorConstructor_NullConnectionManagerTest()
        {
            Assert.Throws<ArgumentNullException>(() => new Authenticator(null, "your-device", Mock.Of<IConnectionManager>()));
        }

        [Fact]
        [Unit]
        public void AuthenticatorConstructor_NullDeviceIdTest()
        {
            var connectionManager = Mock.Of<IConnectionManager>();
            Assert.Throws<ArgumentException>(() => new Authenticator(new TokenCredentialsAuthenticator(connectionManager, new NullCredentialsStore(), TestIotHub), null, Mock.Of<IConnectionManager>()));
        }

        [Fact]
        [Unit]
        public void AuthenticatorConstructor_EmptyDeviceIdTest()
        {
            var connectionManager = Mock.Of<IConnectionManager>();
            Assert.Throws<ArgumentException>(() => new Authenticator(new TokenCredentialsAuthenticator(connectionManager, new NullCredentialsStore(), TestIotHub), string.Empty, Mock.Of<IConnectionManager>()));
        }

        [Fact]
        [Unit]
        public async Task AuthenticateTest()
        {
            var cloudProxy = Mock.Of<ICloudProxy>();
            var connectionManager = Mock.Of<IConnectionManager>();
            var clientCredentials = Mock.Of<ITokenCredentials>(c => c.Identity == Mock.Of<IIdentity>());

            Mock.Get(connectionManager).Setup(cm => cm.CreateCloudConnectionAsync(clientCredentials)).ReturnsAsync(Try.Success(cloudProxy));
            Mock.Get(connectionManager).Setup(cm => cm.AddDeviceConnection(It.IsAny<IClientCredentials>())).Returns(Task.CompletedTask);
            Mock.Get(cloudProxy).Setup(cp => cp.IsActive).Returns(true);

            var authenticator = new Authenticator(new TokenCredentialsAuthenticator(connectionManager, new NullCredentialsStore(), TestIotHub), "your-device", connectionManager);
            Assert.Equal(true, await authenticator.AuthenticateAsync(clientCredentials));
            Mock.Get(connectionManager).Verify();
        }

        [Fact]
        [Unit]
        public async Task Authenticate_InactiveProxyTest()
        {
            var cloudProxy = Mock.Of<ICloudProxy>();
            var connectionManager = Mock.Of<IConnectionManager>();
            var clientCredentials = Mock.Of<IClientCredentials>(c => c.Identity == Mock.Of<IIdentity>());

            Mock.Get(connectionManager).Setup(cm => cm.CreateCloudConnectionAsync(clientCredentials)).ReturnsAsync(Try.Success(cloudProxy));
            Mock.Get(connectionManager).Setup(cm => cm.AddDeviceConnection(It.IsAny<IClientCredentials>())).Returns(Task.CompletedTask);
            Mock.Get(cloudProxy).Setup(cp => cp.IsActive).Returns(false);

            var authenticator = new Authenticator(new TokenCredentialsAuthenticator(connectionManager, new NullCredentialsStore(), TestIotHub), "your-device", connectionManager);
            Assert.Equal(false, await authenticator.AuthenticateAsync(clientCredentials));
            Mock.Get(connectionManager).Verify(c => c.AddDeviceConnection(It.IsAny<IClientCredentials>()), Times.Never);
        }

        [Fact]
        [Unit]
        public async Task Authenticate_ConnectionManagerThrowsTest()
        {
            var cloudProxy = Mock.Of<ICloudProxy>();
            var connectionManager = Mock.Of<IConnectionManager>();
            var clientCredentials = Mock.Of<IClientCredentials>(c => c.Identity == Mock.Of<IIdentity>());

            Mock.Get(connectionManager).Setup(cm => cm.CreateCloudConnectionAsync(clientCredentials)).ReturnsAsync(Try<ICloudProxy>.Failure(new ArgumentException()));
            Mock.Get(connectionManager).Setup(cm => cm.AddDeviceConnection(It.IsAny<IClientCredentials>())).Returns(Task.CompletedTask);
            Mock.Get(cloudProxy).Setup(cp => cp.IsActive).Returns(true);

            var authenticator = new Authenticator(new TokenCredentialsAuthenticator(connectionManager, new NullCredentialsStore(), TestIotHub), "your-device", connectionManager);
            Assert.Equal(false, await authenticator.AuthenticateAsync(clientCredentials));
            Mock.Get(connectionManager).Verify(c => c.AddDeviceConnection(It.IsAny<IClientCredentials>()), Times.Never);
        }

        [Fact]
        [Unit]
        public async Task Authenticate_NonNullIdentityTest()
        {
            var connectionManager = Mock.Of<IConnectionManager>();
            var clientCredentials = Mock.Of<IClientCredentials>(c => c.Identity == Mock.Of<IModuleIdentity>(i => i.DeviceId == "my-device"));

            var authenticator = new Authenticator(new TokenCredentialsAuthenticator(connectionManager, new NullCredentialsStore(), TestIotHub), "your-device", connectionManager);
            Assert.Equal(false, await authenticator.AuthenticateAsync(clientCredentials));
        }

        [Fact]
        [Unit]
        public async Task Authenticate_NullIdentityTest()
        {
            var connectionManager = Mock.Of<IConnectionManager>();

            var authenticator = new Authenticator(new TokenCredentialsAuthenticator(connectionManager, new NullCredentialsStore(), TestIotHub), "your-device", connectionManager);
            await Assert.ThrowsAsync<ArgumentNullException>(() => authenticator.AuthenticateAsync(null));
        }

        [Fact]
        [Unit]
        public async Task Authenticate_X509Identity()
        {
            var connectionManager = Mock.Of<IConnectionManager>();
            var clientCredentials = Mock.Of<IClientCredentials>(c =>
                c.Identity == Mock.Of<IModuleIdentity>(i => i.DeviceId == "my-device")
                && c.AuthenticationType == AuthenticationType.X509Cert);

            var authenticator = new Authenticator(new TokenCredentialsAuthenticator(connectionManager, new NullCredentialsStore(), TestIotHub), "my-device", connectionManager);
            Assert.Equal(true, await authenticator.AuthenticateAsync(clientCredentials));
        }
    }
}
