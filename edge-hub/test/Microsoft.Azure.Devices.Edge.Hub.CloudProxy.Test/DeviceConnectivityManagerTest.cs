// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Xunit;

    [Unit]
    public class DeviceConnectivityManagerTest
    {
        [Fact]
        public async Task NoEventsTest()
        {
            // Arrange / act
            var deviceConnectivityManager = new DeviceConnectivityManager(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));

            var client = new Mock<IClient>();
            client.SetupSequence(c => c.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()))
                .Returns(Task.CompletedTask)
                .Throws<TimeoutException>()
                .Throws<TimeoutException>()
                .Returns(Task.CompletedTask);
            IClient connectivityAwareClient = new ConnectivityAwareClient(client.Object, deviceConnectivityManager);
            var cloudProxy = new CloudProxy(connectivityAwareClient, Mock.Of<IMessageConverterProvider>(), Mock.Of<IIdentity>(i => i.Id == "d1/m1"), null);
            deviceConnectivityManager.SetTestCloudProxy(cloudProxy);

            bool connected = false;
            deviceConnectivityManager.DeviceConnected += (_, __) => connected = true;
            deviceConnectivityManager.DeviceDisconnected += (_, __) => connected = false;

            // Assert
            await Task.Delay(TimeSpan.FromSeconds(4));
            Assert.True(connected);

            await Task.Delay(TimeSpan.FromSeconds(4));
            Assert.False(connected);

            await Task.Delay(TimeSpan.FromSeconds(4));
            Assert.True(connected);
        }

        [Fact]
        public async Task WithDownstreamEventsTest()
        {
            // Arrange / act
            var deviceConnectivityManager = new DeviceConnectivityManager(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
            int connectedCallbackCount = 0;
            int disconnectedCallbackCount = 0;

            void ConnectionStatusChangedHandler(ConnectionStatus status, ConnectionStatusChangeReason reason)
            {
                if (status == ConnectionStatus.Connected && reason == ConnectionStatusChangeReason.Connection_Ok)
                {
                    Interlocked.Increment(ref connectedCallbackCount);
                }
                else if (status == ConnectionStatus.Disconnected && reason == ConnectionStatusChangeReason.No_Network)
                {
                    Interlocked.Increment(ref disconnectedCallbackCount);
                }
            }

            var device1UnderlyingClient = new Mock<IClient>();
            device1UnderlyingClient.Setup(c => c.SendEventAsync(It.IsAny<Message>()))
                .Returns(Task.CompletedTask);
            var device1Client = new ConnectivityAwareClient(device1UnderlyingClient.Object, deviceConnectivityManager);
            device1Client.SetConnectionStatusChangedHandler(ConnectionStatusChangedHandler);

            var device2UnderlyingClient = new Mock<IClient>();
            device2UnderlyingClient.Setup(c => c.SendEventAsync(It.IsAny<Message>()))
                .Throws<TimeoutException>();
            var device2Client = new ConnectivityAwareClient(device2UnderlyingClient.Object, deviceConnectivityManager);
            device2Client.SetConnectionStatusChangedHandler(ConnectionStatusChangedHandler);

            var edgeHubUnderlyingClient = new Mock<IClient>();
            edgeHubUnderlyingClient.Setup(c => c.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()))
                .Throws<TimeoutException>();
            IClient edgeHubClient = new ConnectivityAwareClient(edgeHubUnderlyingClient.Object, deviceConnectivityManager);
            edgeHubClient.SetConnectionStatusChangedHandler(ConnectionStatusChangedHandler);
            var edgeHubCloudProxy = new CloudProxy(edgeHubClient, Mock.Of<IMessageConverterProvider>(), Mock.Of<IIdentity>(i => i.Id == "d1/m1"), null);
            deviceConnectivityManager.SetTestCloudProxy(edgeHubCloudProxy);

            bool connected = false;
            deviceConnectivityManager.DeviceConnected += (_, __) => connected = true;
            deviceConnectivityManager.DeviceDisconnected += (_, __) => connected = false;

            // Act
            var cts = new CancellationTokenSource();
            Task t = Task.Run(
                async () =>
                {
                    while (!cts.IsCancellationRequested)
                    {
                        await device1Client.SendEventAsync(new Message());
                        if (!cts.IsCancellationRequested)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(1));
                        }
                    }
                });

            await Task.Delay(TimeSpan.FromSeconds(5));

            // Assert
            Assert.True(connected);
            edgeHubUnderlyingClient.Verify(c => c.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()), Times.Never);
            Assert.Equal(3, connectedCallbackCount);
            Assert.Equal(0, disconnectedCallbackCount);

            cts.Cancel();
            await t;

            var cts2 = new CancellationTokenSource();
            Task t2 = Task.Run(
                async () =>
                {
                    while (!cts2.IsCancellationRequested)
                    {
                        try
                        {
                            await device2Client.SendEventAsync(new Message());
                        }
                        catch (TimeoutException)
                        {
                        }
                        if (!cts2.IsCancellationRequested)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(1));
                        }
                    }
                });

            await Task.Delay(TimeSpan.FromSeconds(5));

            // Assert
            Assert.False(connected);
            edgeHubUnderlyingClient.Verify(c => c.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()), Times.Once);
            Assert.Equal(3, connectedCallbackCount);
            Assert.Equal(3, disconnectedCallbackCount);

            cts2.Cancel();
            await t2;

            await device1Client.SendEventAsync(new Message());

            // Assert
            Assert.True(connected);
            edgeHubUnderlyingClient.Verify(c => c.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()), Times.Once);
            Assert.Equal(6, connectedCallbackCount);
            Assert.Equal(3, disconnectedCallbackCount);

        }
    }
}
