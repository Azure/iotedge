// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Transport.Channels;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
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
            var connectivityAwareClient = new ConnectivityAwareClient(client, deviceConnectivityManager, Mock.Of<IIdentity>(i => i.Id == "d1"));
            connectivityAwareClient.SetConnectionStatusChangedHandler(ConnectionStatusChangedHandler);

            // Act
            deviceConnectivityManager.InvokeDeviceConnected();
            deviceConnectivityManager.InvokeDeviceDisconnected();

            // Assert
            Assert.Equal(0, connectionStatusChangedHandlerCount);

            // Act
            await connectivityAwareClient.OpenAsync();
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
        [InlineData(typeof(ConnectTimeoutException), typeof(TimeoutException), true)]
        [InlineData(typeof(TimeoutException), typeof(TimeoutException), true)]
        [InlineData(typeof(IotHubException), typeof(IotHubException), false)]
        [InlineData(typeof(InvalidOperationException), typeof(InvalidOperationException), false)]
        public async Task TestExceptionTest(Type thrownException, Type expectedException, bool isTimeout)
        {
            // Arrange
            var client = new Mock<IClient>();
            client.Setup(c => c.SendEventAsync(It.IsAny<Message>(), CancellationToken.None)).ThrowsAsync(Activator.CreateInstance(thrownException, "msg str") as Exception);
            var deviceConnectivityManager = new Mock<IDeviceConnectivityManager>();
            deviceConnectivityManager.Setup(d => d.CallTimedOut()).Returns(Task.CompletedTask);
            var connectivityAwareClient = new ConnectivityAwareClient(client.Object, deviceConnectivityManager.Object, Mock.Of<IIdentity>(i => i.Id == "d1"));
            var message = new Message();

            // Act / Assert
            await Assert.ThrowsAsync(expectedException, () => connectivityAwareClient.SendEventAsync(message));
            if (isTimeout)
            {
                deviceConnectivityManager.Verify(d => d.CallTimedOut(), Times.Once);
            }
            else
            {
                deviceConnectivityManager.Verify(d => d.CallTimedOut(), Times.Never);
            }
        }

        [Theory]
        [InlineData(typeof(ConnectTimeoutException), typeof(TimeoutException))]
        [InlineData(typeof(TimeoutException), typeof(TimeoutException))]
        [InlineData(typeof(IotHubException), typeof(IotHubException))]
        [InlineData(typeof(InvalidOperationException), typeof(InvalidOperationException))]
        public async Task TestExceptionInSetDesiredPropertyUpdateCallbackTest(Type thrownException, Type expectedException)
        {
            // Arrange
            var client = new Mock<IClient>();
            client.Setup(c => c.SetDesiredPropertyUpdateCallbackAsync(It.IsAny<DesiredPropertyUpdateCallback>(), It.IsAny<object>())).ThrowsAsync(Activator.CreateInstance(thrownException, "msg str") as Exception);
            var deviceConnectivityManager = new Mock<IDeviceConnectivityManager>();
            deviceConnectivityManager.Setup(d => d.CallTimedOut());
            var connectivityAwareClient = new ConnectivityAwareClient(client.Object, deviceConnectivityManager.Object, Mock.Of<IIdentity>(i => i.Id == "d1"));
            DesiredPropertyUpdateCallback callback = (_, __) => Task.CompletedTask;

            // Act / Assert
            await Assert.ThrowsAsync(expectedException, () => connectivityAwareClient.SetDesiredPropertyUpdateCallbackAsync(callback, null));
            deviceConnectivityManager.Verify(d => d.CallTimedOut(), Times.Never);
        }

        [Fact]
        public async Task TestNoSubscriptionWhenOpenFails()
        {
            // Arrange
            int connectionStatusChangedHandlerCount = 0;

            void ConnectionStatusChangedHandler(ConnectionStatus status, ConnectionStatusChangeReason reason)
            {
                Interlocked.Increment(ref connectionStatusChangedHandlerCount);
            }

            var deviceConnectivityManager = new DeviceConnectivityManager();
            var client = new Mock<IClient>();
            client.Setup(c => c.OpenAsync())
                .ThrowsAsync(new ArgumentException());

            var connectivityAwareClient = new ConnectivityAwareClient(client.Object, deviceConnectivityManager, Mock.Of<IIdentity>(i => i.Id == "d1"));
            connectivityAwareClient.SetConnectionStatusChangedHandler(ConnectionStatusChangedHandler);

            // Act
            deviceConnectivityManager.InvokeDeviceConnected();
            deviceConnectivityManager.InvokeDeviceDisconnected();

            // Assert
            Assert.Equal(0, connectionStatusChangedHandlerCount);

            // Act
            await Assert.ThrowsAsync<ArgumentException>(() => connectivityAwareClient.OpenAsync());
            deviceConnectivityManager.InvokeDeviceConnected();
            deviceConnectivityManager.InvokeDeviceDisconnected();

            // Assert
            Assert.Equal(0, connectionStatusChangedHandlerCount);
        }

        [Fact]
        public async Task ConnectivityChangeTest()
        {
            // Arrange
            var receivedConnectionStatuses = new List<ConnectionStatus>();
            var receivedChangeReasons = new List<ConnectionStatusChangeReason>();

            void ConnectionStatusChangedHandler(ConnectionStatus status, ConnectionStatusChangeReason reason)
            {
                receivedConnectionStatuses.Add(status);
                receivedChangeReasons.Add(reason);
            }

            var deviceConnectivityManager = new DeviceConnectivityManager();
            var client = Mock.Of<IClient>();
            Mock.Get(client).SetupSequence(c => c.SendEventAsync(It.IsAny<Message>(), CancellationToken.None))
                .Returns(Task.CompletedTask)
                .Throws(new TimeoutException());
            var connectivityAwareClient = new ConnectivityAwareClient(client, deviceConnectivityManager, Mock.Of<IIdentity>(i => i.Id == "d1"));
            connectivityAwareClient.SetConnectionStatusChangedHandler(ConnectionStatusChangedHandler);
            await connectivityAwareClient.OpenAsync();

            // Act
            await connectivityAwareClient.SendEventAsync(new Message());

            // Assert
            Assert.Single(receivedConnectionStatuses);
            Assert.Equal(ConnectionStatus.Connected, receivedConnectionStatuses[0]);
            Assert.Equal(ConnectionStatusChangeReason.Connection_Ok, receivedChangeReasons[0]);

            // Act
            await Assert.ThrowsAsync<TimeoutException>(async () => await connectivityAwareClient.SendEventAsync(new Message()));

            // Assert
            Assert.Single(receivedConnectionStatuses);
            Assert.Equal(ConnectionStatus.Connected, receivedConnectionStatuses[0]);
            Assert.Equal(ConnectionStatusChangeReason.Connection_Ok, receivedChangeReasons[0]);

            // Act
            deviceConnectivityManager.InvokeDeviceConnected();

            // Assert
            Assert.Single(receivedConnectionStatuses);
            Assert.Equal(ConnectionStatus.Connected, receivedConnectionStatuses[0]);
            Assert.Equal(ConnectionStatusChangeReason.Connection_Ok, receivedChangeReasons[0]);

            // Act
            deviceConnectivityManager.InvokeDeviceDisconnected();

            // Assert
            Assert.Equal(2, receivedConnectionStatuses.Count);
            Assert.Equal(ConnectionStatus.Disconnected, receivedConnectionStatuses[1]);
            Assert.Equal(ConnectionStatusChangeReason.No_Network, receivedChangeReasons[1]);

            // Act
            deviceConnectivityManager.InvokeDeviceDisconnected();

            // Assert
            Assert.Equal(2, receivedConnectionStatuses.Count);
            Assert.Equal(ConnectionStatus.Disconnected, receivedConnectionStatuses[1]);
            Assert.Equal(ConnectionStatusChangeReason.No_Network, receivedChangeReasons[1]);

            // Act
            await connectivityAwareClient.CloseAsync();
            deviceConnectivityManager.InvokeDeviceConnected();
            deviceConnectivityManager.InvokeDeviceDisconnected();

            // Assert
            Assert.Equal(2, receivedConnectionStatuses.Count);
            Assert.Equal(ConnectionStatus.Disconnected, receivedConnectionStatuses[1]);
            Assert.Equal(ConnectionStatusChangeReason.No_Network, receivedChangeReasons[1]);
        }

        [Fact]
        public async Task ConnectivityChangeEventTest1()
        {
            // Arrange
            int connectedStatusChangedHandlerCount = 0;
            int disconnectedStatusChangedHandlerCount = 0;

            void ConnectionStatusChangedHandler(ConnectionStatus status, ConnectionStatusChangeReason reason)
            {
                if (status == ConnectionStatus.Connected)
                {
                    Interlocked.Increment(ref connectedStatusChangedHandlerCount);
                }
                else
                {
                    Interlocked.Increment(ref disconnectedStatusChangedHandlerCount);
                }
            }

            var deviceConnectivityManager = new DeviceConnectivityManager();
            ConnectionStatusChangesHandler innerClientHandler = null;
            var client = new Mock<IClient>();
            client.Setup(c => c.SetConnectionStatusChangedHandler(It.IsAny<ConnectionStatusChangesHandler>()))
                .Callback<ConnectionStatusChangesHandler>(c => innerClientHandler = c);

            var connectivityAwareClient = new ConnectivityAwareClient(client.Object, deviceConnectivityManager, Mock.Of<IIdentity>(i => i.Id == "d1"));
            connectivityAwareClient.SetConnectionStatusChangedHandler(ConnectionStatusChangedHandler);

            // Act
            await connectivityAwareClient.OpenAsync();

            // Assert
            Assert.NotNull(innerClientHandler);

            // Act
            innerClientHandler.Invoke(ConnectionStatus.Connected, ConnectionStatusChangeReason.Connection_Ok);

            // Assert
            Assert.Equal(1, connectedStatusChangedHandlerCount);
            Assert.Equal(0, disconnectedStatusChangedHandlerCount);

            // Act
            innerClientHandler.Invoke(ConnectionStatus.Connected, ConnectionStatusChangeReason.Connection_Ok);
            deviceConnectivityManager.InvokeDeviceConnected();

            // Assert
            Assert.Equal(1, connectedStatusChangedHandlerCount);
            Assert.Equal(0, disconnectedStatusChangedHandlerCount);

            // Act
            innerClientHandler.Invoke(ConnectionStatus.Disconnected_Retrying, ConnectionStatusChangeReason.Communication_Error);

            // Assert
            Assert.Equal(1, connectedStatusChangedHandlerCount);
            Assert.Equal(0, disconnectedStatusChangedHandlerCount);

            // Act
            innerClientHandler.Invoke(ConnectionStatus.Disconnected, ConnectionStatusChangeReason.Communication_Error);

            // Assert
            Assert.Equal(1, connectedStatusChangedHandlerCount);
            Assert.Equal(0, disconnectedStatusChangedHandlerCount);

            // Act
            innerClientHandler.Invoke(ConnectionStatus.Disconnected, ConnectionStatusChangeReason.Communication_Error);
            deviceConnectivityManager.InvokeDeviceDisconnected();

            // Assert
            Assert.Equal(1, connectedStatusChangedHandlerCount);
            Assert.Equal(1, disconnectedStatusChangedHandlerCount);
        }

        [Fact]
        public async Task ConnectivityChangeEventTest2()
        {
            // Arrange
            int connectedStatusChangedHandlerCount = 0;
            int disconnectedStatusChangedHandlerCount = 0;

            void ConnectionStatusChangedHandler(ConnectionStatus status, ConnectionStatusChangeReason reason)
            {
                if (status == ConnectionStatus.Connected)
                {
                    Interlocked.Increment(ref connectedStatusChangedHandlerCount);
                }
                else
                {
                    Interlocked.Increment(ref disconnectedStatusChangedHandlerCount);
                }
            }

            var deviceConnectivityManager = new DeviceConnectivityManager();
            ConnectionStatusChangesHandler innerClientHandler = null;
            var client = new Mock<IClient>();
            client.Setup(c => c.SetConnectionStatusChangedHandler(It.IsAny<ConnectionStatusChangesHandler>()))
                .Callback<ConnectionStatusChangesHandler>(c => innerClientHandler = c);

            var connectivityAwareClient = new ConnectivityAwareClient(client.Object, deviceConnectivityManager, Mock.Of<IIdentity>(i => i.Id == "d1"));
            connectivityAwareClient.SetConnectionStatusChangedHandler(ConnectionStatusChangedHandler);

            // Act
            await connectivityAwareClient.OpenAsync();

            // Assert
            Assert.NotNull(innerClientHandler);

            // Act
            deviceConnectivityManager.InvokeDeviceConnected();

            // Assert
            Assert.Equal(1, connectedStatusChangedHandlerCount);
            Assert.Equal(0, disconnectedStatusChangedHandlerCount);

            // Act
            innerClientHandler.Invoke(ConnectionStatus.Connected, ConnectionStatusChangeReason.Connection_Ok);

            // Assert
            Assert.Equal(1, connectedStatusChangedHandlerCount);
            Assert.Equal(0, disconnectedStatusChangedHandlerCount);

            // Act
            deviceConnectivityManager.InvokeDeviceDisconnected();

            // Assert
            Assert.Equal(1, connectedStatusChangedHandlerCount);
            Assert.Equal(1, disconnectedStatusChangedHandlerCount);

            // Act
            innerClientHandler.Invoke(ConnectionStatus.Disconnected, ConnectionStatusChangeReason.Communication_Error);

            // Assert
            Assert.Equal(1, connectedStatusChangedHandlerCount);
            Assert.Equal(1, disconnectedStatusChangedHandlerCount);
        }

        class DeviceConnectivityManager : IDeviceConnectivityManager
        {
            public event EventHandler DeviceConnected;

            public event EventHandler DeviceDisconnected;

            public Task CallSucceeded() => Task.CompletedTask;

            public Task CallTimedOut() => Task.CompletedTask;

            public void InvokeDeviceConnected() => this.DeviceConnected?.Invoke(null, null);

            public void InvokeDeviceDisconnected() => this.DeviceDisconnected?.Invoke(null, null);
        }
    }
}
