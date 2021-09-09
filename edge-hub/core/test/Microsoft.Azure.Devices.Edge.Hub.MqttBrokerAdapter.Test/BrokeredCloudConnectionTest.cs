// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.Test
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Moq;
    using Xunit;

    public class BrokeredCloudConnectionTest
    {
        const string IotHubHostName = "somehub.azure-devices.net";
        const string ConnectivityTopic = "$internal/connectivity";

        [Fact]
        public async Task AddRemoveDeviceConnection()
        {
            var deviceId = "some_device";
            var deviceCredentials = new TokenCredentials(new DeviceIdentity(IotHubHostName, deviceId), "some_token", "some_product", Option.None<string>(), Option.None<string>(), false);

            var (connectionManager, cloudProxyDispatcher) = SetupEnvironment();

            await SignalConnected(cloudProxyDispatcher);

            Try<ICloudProxy> cloudProxyTry = await connectionManager.CreateCloudConnectionAsync(deviceCredentials);
            Assert.True(cloudProxyTry.Success);
            Assert.True(cloudProxyTry.Value.IsActive);

            await connectionManager.RemoveDeviceConnection(deviceId);
            Assert.False(cloudProxyTry.Value.IsActive);
        }

        [Fact]
        public async Task DisconnectRemovesConnections()
        {
            var deviceId = "some_device";
            var deviceCredentials = new TokenCredentials(new DeviceIdentity(IotHubHostName, deviceId), "some_token", "some_product", Option.None<string>(), Option.None<string>(), false);

            var (connectionManager, cloudProxyDispatcher) = SetupEnvironment();

            await SignalConnected(cloudProxyDispatcher);

            Try<ICloudProxy> cloudProxyTry = await connectionManager.CreateCloudConnectionAsync(deviceCredentials);
            Assert.True(cloudProxyTry.Success);
            Assert.True(cloudProxyTry.Value.IsActive);

            await SignalDisconnected(cloudProxyDispatcher);

            // After the signal triggered, it is async disconnecting cloud proxies, so give some time
            await WaitUntil(() => !cloudProxyTry.Value.IsActive, TimeSpan.FromSeconds(5));

            Assert.False(cloudProxyTry.Value.IsActive);
        }

        [Fact]
        public async Task ConnectionEventHandlersMaintainedProperly()
        {
            // This test is motivated by an actual bug when new cloud proxies were added to
            // connection (C#) events but then they never got removed, causing memory leak and
            // triggering event handlers multiple times, as the old proxies kept hanging on
            // the event.

            var deviceId = "some_device";
            var deviceCredentials = new TokenCredentials(new DeviceIdentity(IotHubHostName, deviceId), "some_token", "some_product", Option.None<string>(), Option.None<string>(), false);

            var (connectionManager, cloudProxyDispatcher) = SetupEnvironment();

            await SignalConnected(cloudProxyDispatcher);

            Try<ICloudProxy> cloudProxyTry = await connectionManager.CreateCloudConnectionAsync(deviceCredentials);
            Assert.True(cloudProxyTry.Success);
            Assert.True(cloudProxyTry.Value.IsActive);

            // Disconnect/Connect a few times, this generates new and new proxies.
            var lastProxy = cloudProxyTry.Value.AsPrivateAccessible().InnerCloudProxy as BrokeredCloudProxy;
            for (var i = 0; i < 3; i++)
            {
                await SignalDisconnected(cloudProxyDispatcher);

                await WaitUntil(() => !lastProxy.IsActive, TimeSpan.FromSeconds(5));

                // As the proxy went inactive, the subscription list must be empty
                var subscribers = cloudProxyDispatcher.AsPrivateAccessible().ConnectionStatusChangedEvent as MulticastDelegate;
                Assert.NotNull(subscribers);

                var subscriberList = subscribers.GetInvocationList().Select(l => l.Target).OfType<BrokeredCloudProxy>().ToArray();
                Assert.Empty(subscriberList);

                // Bring up a new proxy
                await SignalConnected(cloudProxyDispatcher);

                var currentProxyTry = (await connectionManager.GetCloudConnection(deviceId)).GetOrElse((ICloudProxy)null);
                Assert.NotNull(currentProxyTry);
                Assert.True(currentProxyTry.IsActive);

                var currentProxy = currentProxyTry.AsPrivateAccessible().InnerCloudProxy as BrokeredCloudProxy;

                // Just to be sure that this is a new proxy (and not the old is re-activated)
                Assert.NotEqual(lastProxy, currentProxy);

                // The new proxy must have subscribed and it should be the only subscriber
                subscribers = cloudProxyDispatcher.AsPrivateAccessible().ConnectionStatusChangedEvent as MulticastDelegate;
                Assert.NotNull(subscribers);

                subscriberList = subscribers.GetInvocationList().Select(l => l.Target).OfType<BrokeredCloudProxy>().ToArray();
                Assert.Single(subscriberList);
                Assert.Equal(currentProxy, subscriberList.First());

                lastProxy = currentProxy;
            }
        }

        [Fact]
        public async Task WhenDeviceNotInScopeConnectionThrows()
        {
            var deviceId = "some_device";
            var deviceCredentials = new TokenCredentials(new DeviceIdentity(IotHubHostName, deviceId), "some_token", "some_product", Option.None<string>(), Option.None<string>(), false);

            var (connectionManager, cloudProxyDispatcher) = SetupEnvironment(true);

            await SignalConnected(cloudProxyDispatcher);

            Try<ICloudProxy> cloudProxyTry = await connectionManager.CreateCloudConnectionAsync(deviceCredentials);
            Assert.False(cloudProxyTry.Success);
            Assert.Throws<DeviceInvalidStateException>(() => cloudProxyTry.Value);
        }

        Task SignalConnected(BrokeredCloudProxyDispatcher brokeredCloudProxyDispatcher) => SignalConnectionEvent(brokeredCloudProxyDispatcher, "Connected");
        Task SignalDisconnected(BrokeredCloudProxyDispatcher brokeredCloudProxyDispatcher) => SignalConnectionEvent(brokeredCloudProxyDispatcher, "Disconnected");

        async Task SignalConnectionEvent(BrokeredCloudProxyDispatcher brokeredCloudProxyDispatcher, string status)
        {
            var packet = new MqttPublishInfo(ConnectivityTopic, Encoding.UTF8.GetBytes("{\"status\":\"" + status + "\"}"));
            await brokeredCloudProxyDispatcher.HandleAsync(packet);
        }

        (ConnectionManager, BrokeredCloudProxyDispatcher) SetupEnvironment(bool trackDeviceState = false)
        {
            var cloudProxyDispatcher = new BrokeredCloudProxyDispatcher();
            cloudProxyDispatcher.SetConnector(Mock.Of<IMqttBrokerConnector>());

            IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache = new NullDeviceScopeIdentitiesCache();

            if (trackDeviceState)
            {
                var deviceScopeIdentitiesCacheMock = new Mock<IDeviceScopeIdentitiesCache>();
                deviceScopeIdentitiesCacheMock.Setup(d => d.VerifyServiceIdentityAuthChainState(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>())).Throws(new DeviceInvalidStateException("Device is out of scope."));
                deviceScopeIdentitiesCache = deviceScopeIdentitiesCacheMock.Object;
            }

            var cloudConnectionProvider = new BrokeredCloudConnectionProvider(cloudProxyDispatcher, deviceScopeIdentitiesCache);
            cloudConnectionProvider.BindEdgeHub(Mock.Of<IEdgeHub>());

            var deviceConnectivityManager = new BrokeredDeviceConnectivityManager(cloudProxyDispatcher);

            var connectionManager = new ConnectionManager(
                                            cloudConnectionProvider,
                                            Mock.Of<ICredentialsCache>(),
                                            new IdentityProvider(IotHubHostName),
                                            deviceConnectivityManager);

            return (connectionManager, cloudProxyDispatcher);
        }

        async Task WaitUntil(Func<bool> condition, TimeSpan timeout)
        {
            var startTime = DateTime.Now;

            while (!condition() && DateTime.Now - startTime < timeout)
            {
                await Task.Delay(100);
            }
        }
    }
}
