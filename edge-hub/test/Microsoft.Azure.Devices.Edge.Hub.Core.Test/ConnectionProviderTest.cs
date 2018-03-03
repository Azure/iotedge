// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    public class ConnectionProviderTest
    {
        [Fact]
        [Unit]
        public void ConnectionProviderConstructorTest()
        {
            var connectionManager = Mock.Of<IConnectionManager>();
            var edgeHub = Mock.Of<IEdgeHub>();

            Assert.NotNull(new ConnectionProvider(connectionManager, edgeHub));
        }

        [Fact]
        [Unit]
        public void ConnectionProviderConstructor_NullConnectionManagerTest()
        {
            var edgeHub = Mock.Of<IEdgeHub>();

            Assert.Throws<ArgumentNullException>(() => new ConnectionProvider(null, edgeHub));
        }

        [Fact]
        [Unit]
        public void ConnectionProviderConstructor_NullEdgeHubTest()
        {
            var connectionManager = Mock.Of<IConnectionManager>();

            Assert.Throws<ArgumentNullException>(() => new ConnectionProvider(connectionManager, null));
        }


        [Fact]
        [Unit]
        public async Task GetDeviceListenerWithSASIdentityTest()
        {
            var cloudProxy = Mock.Of<ICloudProxy>();
            var connectionManager = Mock.Of<IConnectionManager>();
            var edgeHub = Mock.Of<IEdgeHub>();
            var identity = new ModuleIdentity("hub", "device", "module", AuthenticationScope.SasToken, "");

            Mock.Get(connectionManager).Setup(cm => cm.GetOrCreateCloudConnectionAsync(identity)).ReturnsAsync(Try.Success(cloudProxy));

            var connectionProvider = new ConnectionProvider(connectionManager, edgeHub);
            Assert.NotNull(await connectionProvider.GetDeviceListenerAsync(identity));
        }

        [Fact]
        [Unit]
        public async Task GetDeviceListenerWithX509IdentityTest()
        {
            var cloudProxy = Mock.Of<ICloudProxy>();
            var connectionManager = Mock.Of<IConnectionManager>();
            var edgeHub = Mock.Of<IEdgeHub>();
            var identity = new ModuleIdentity("hub", "device", "module", AuthenticationScope.x509Cert, "");

            var connectionProvider = new ConnectionProvider(connectionManager, edgeHub);
            Assert.NotNull(await connectionProvider.GetDeviceListenerAsync(identity));
        }

        [Fact]
        [Unit]
        public async Task GetDeviceListener_NullIdentityTest()
        {
            var connectionManager = Mock.Of<IConnectionManager>();
            var edgeHub = Mock.Of<IEdgeHub>();

            var connectionProvider = new ConnectionProvider(connectionManager, edgeHub);
            await Assert.ThrowsAsync<ArgumentNullException>(() => connectionProvider.GetDeviceListenerAsync(null));
        }

        [Fact]
        [Unit]
        public async Task GetDeviceListener_ConnectionManagerThrowsTest()
        {
            var connectionManager = Mock.Of<IConnectionManager>();
            var edgeHub = Mock.Of<IEdgeHub>();
            var identity = Mock.Of<IIdentity>();

            Mock.Get(connectionManager).Setup(cm => cm.GetOrCreateCloudConnectionAsync(identity)).ReturnsAsync(Try<ICloudProxy>.Failure(new ArgumentException()));

            var connectionProvider = new ConnectionProvider(connectionManager, edgeHub);
            await Assert.ThrowsAsync<EdgeHubConnectionException>(() => connectionProvider.GetDeviceListenerAsync(identity));
        }
    }
}
