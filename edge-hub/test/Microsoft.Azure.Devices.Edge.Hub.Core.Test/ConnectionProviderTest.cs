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
        public async Task GetDeviceListenerWithSasIdentityTest()
        {
            var connectionManager = Mock.Of<IConnectionManager>();
            var edgeHub = Mock.Of<IEdgeHub>();
            var moduleCredentials = new TokenCredentials(new ModuleIdentity("hub", "device", "module"), "token", "productInfo", false);

            var connectionProvider = new ConnectionProvider(connectionManager, edgeHub);
            Assert.NotNull(await connectionProvider.GetDeviceListenerAsync(moduleCredentials));
        }

        [Fact]
        [Unit]
        public async Task GetDeviceListenerWithX509IdentityTest()
        {
            var connectionManager = Mock.Of<IConnectionManager>();
            var edgeHub = Mock.Of<IEdgeHub>();
            var moduleCredentials = new X509CertCredentials(new ModuleIdentity("hub", "device", "module"), string.Empty);

            var connectionProvider = new ConnectionProvider(connectionManager, edgeHub);
            Assert.NotNull(await connectionProvider.GetDeviceListenerAsync(moduleCredentials));
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
    }
}
