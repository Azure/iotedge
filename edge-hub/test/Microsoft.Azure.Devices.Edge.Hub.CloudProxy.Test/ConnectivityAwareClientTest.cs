// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Transport.Channels;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class ConnectivityAwareClientTest
    {
        [Fact]
        public async Task DisableHandlingEventsOnCloseTest()
        {
            // Arrange
            int connectionStatusChangedHandlerCount = 0;
            void ConnectionStatusChangedHandler(ConnectionStatus status, ConnectionStatusChangeReason reason)
            {
                Interlocked.Increment(ref connectionStatusChangedHandlerCount);
            }

            var deviceConnectivityManager = new DeviceConnectivityManager();
            var client = Mock.Of<IClient>();
            var connectivityAwareClient = new ConnectivityAwareClient(client, deviceConnectivityManager);
            connectivityAwareClient.SetConnectionStatusChangedHandler(ConnectionStatusChangedHandler);

            // Act
            deviceConnectivityManager.InvokeDeviceConnected();
            deviceConnectivityManager.InvokeDeviceDisconnected();

            // Assert
            Assert.Equal(2, connectionStatusChangedHandlerCount);

            // Act
            await connectivityAwareClient.CloseAsync();
            deviceConnectivityManager.InvokeDeviceConnected();
            deviceConnectivityManager.InvokeDeviceDisconnected();

            // Assert
            Assert.Equal(2, connectionStatusChangedHandlerCount);
        }

        [Theory]
        [InlineData(typeof(ConnectTimeoutException), typeof(TimeoutException))]
        [InlineData(typeof(TimeoutException), typeof(TimeoutException))]
        [InlineData(typeof(IotHubException), typeof(IotHubException))]
        [InlineData(typeof(InvalidOperationException), typeof(InvalidOperationException))]
        public async Task TestExceptionTest(Type thrownException, Type expectedException)
        {
            // Arrange
            var client = new Mock<IClient>();
            client.Setup(c => c.SendEventAsync(It.IsAny<Message>())).ThrowsAsync(Activator.CreateInstance(thrownException, "msg str") as Exception);
            var deviceConnectivityManager = new DeviceConnectivityManager();
            var connectivityAwareClient = new ConnectivityAwareClient(client.Object, deviceConnectivityManager);
            var message = new Message();

            // Act / Assert
            await Assert.ThrowsAsync(expectedException, () => connectivityAwareClient.SendEventAsync(message));
        }

        class DeviceConnectivityManager : IDeviceConnectivityManager
        {
            public void CallSucceeded()
            {
            }

            public void CallTimedOut()
            {
            }

            public void InvokeDeviceConnected() => this.DeviceConnected?.Invoke(null, null);

            public void InvokeDeviceDisconnected() => this.DeviceDisconnected?.Invoke(null, null);

            public event EventHandler DeviceConnected;

            public event EventHandler DeviceDisconnected;
        }
    }
}
