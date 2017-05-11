// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
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
            var dispatcher = Mock.Of<IDispatcher>();
            var router = Mock.Of<IRouter>();

            Assert.NotNull(new ConnectionProvider(connectionManager, router, dispatcher));
        }

        [Fact]
        [Unit]
        public void ConnectionProviderConstructor_NullConnectionManagerTest()
        {
            var dispatcher = Mock.Of<IDispatcher>();
            var router = Mock.Of<IRouter>();

            Assert.Throws<ArgumentNullException>(() => new ConnectionProvider(null, router, dispatcher));
        }

        [Fact]
        [Unit]
        public void ConnectionProviderConstructor_NullRouterTest()
        {
            var connectionManager = Mock.Of<IConnectionManager>();
            var dispatcher = Mock.Of<IDispatcher>();

            Assert.Throws<ArgumentNullException>(() => new ConnectionProvider(connectionManager, null, dispatcher));
        }

        [Fact]
        [Unit]
        public void ConnectionProviderConstructor_NullDispatcherTest()
        {
            var connectionManager = Mock.Of<IConnectionManager>();
            var router = Mock.Of<IRouter>();

            Assert.Throws<ArgumentNullException>(() => new ConnectionProvider(connectionManager, router, null));
        }

        [Fact]
        [Unit]
        public async Task GetDeviceListenerTest()
        {
            var cloudProxy = Mock.Of<ICloudProxy>();
            var connectionManager = Mock.Of<IConnectionManager>();
            var dispatcher = Mock.Of<IDispatcher>();
            var identity = Mock.Of<IIdentity>();
            var router = Mock.Of<IRouter>();

            Mock.Get(connectionManager).Setup(cm => cm.GetOrCreateCloudConnection(identity)).ReturnsAsync(Try.Success(cloudProxy));

            var connectionProvider = new ConnectionProvider(connectionManager, router, dispatcher);
            Assert.NotNull(await connectionProvider.GetDeviceListener(identity));
        }

        [Fact]
        [Unit]
        public async Task GetDeviceListener_NullIdentityTest()
        {
            var connectionManager = Mock.Of<IConnectionManager>();
            var dispatcher = Mock.Of<IDispatcher>();
            var router = Mock.Of<IRouter>();

            var connectionProvider = new ConnectionProvider(connectionManager, router, dispatcher);
            await Assert.ThrowsAsync<ArgumentNullException>(() => connectionProvider.GetDeviceListener(null));
        }

        [Fact]
        [Unit]
        public async Task GetDeviceListener_ConnectionManagerThrowsTest()
        {
            var connectionManager = Mock.Of<IConnectionManager>();
            var dispatcher = Mock.Of<IDispatcher>();
            var identity = Mock.Of<IIdentity>();
            var router = Mock.Of<IRouter>();

            Mock.Get(connectionManager).Setup(cm => cm.GetOrCreateCloudConnection(identity)).ReturnsAsync(Try<ICloudProxy>.Failure(new ArgumentException()));

            var connectionProvider = new ConnectionProvider(connectionManager, router, dispatcher);
            await Assert.ThrowsAsync<IotHubConnectionException>(() => connectionProvider.GetDeviceListener(identity));
        }
    }
}
