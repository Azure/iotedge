// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Transport.Channels;
    using Microsoft.Azure.Devices.Client;
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

            void ConnectionStatusChangedHandler(ConnectionStatusInfo statusInfo)
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
        [InlineData(typeof(IotHubClientException), typeof(IotHubClientException), false)]
        [InlineData(typeof(InvalidOperationException), typeof(InvalidOperationException), false)]
        public async Task TestExceptionTest(Type thrownException, Type expectedException, bool isTimeout)
        {
            // Arrange
            var client = new Mock<IClient>();
            client.Setup(c => c.SendTelemetryAsync(It.IsAny<TelemetryMessage>())).ThrowsAsync(Activator.CreateInstance(thrownException, "msg str") as Exception);
            var deviceConnectivityManager = new Mock<IDeviceConnectivityManager>();
            deviceConnectivityManager.Setup(d => d.CallTimedOut()).Returns(Task.CompletedTask);
            var connectivityAwareClient = new ConnectivityAwareClient(client.Object, deviceConnectivityManager.Object, Mock.Of<IIdentity>(i => i.Id == "d1"));
            var message = new TelemetryMessage(new byte[0]);

            // Act / Assert
            await Assert.ThrowsAsync(expectedException, () => connectivityAwareClient.SendTelemetryAsync(message));
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
        [InlineData(typeof(IotHubClientException), typeof(IotHubClientException))]
        [InlineData(typeof(InvalidOperationException), typeof(InvalidOperationException))]
        public async Task TestExceptionInSetDesiredPropertyUpdateCallbackTest(Type thrownException, Type expectedException)
        {
            // Arrange
            var client = new Mock<IClient>();
            client.Setup(c => c.SetDesiredPropertyUpdateCallbackAsync(It.IsAny<Func<PropertyCollection, Task>>())).ThrowsAsync(Activator.CreateInstance(thrownException, "msg str") as Exception);
            var deviceConnectivityManager = new Mock<IDeviceConnectivityManager>();
            deviceConnectivityManager.Setup(d => d.CallTimedOut());
            var connectivityAwareClient = new ConnectivityAwareClient(client.Object, deviceConnectivityManager.Object, Mock.Of<IIdentity>(i => i.Id == "d1"));
            Func<PropertyCollection, Task> callback = (_) => Task.CompletedTask;

            // Act / Assert
            await Assert.ThrowsAsync(expectedException, () => connectivityAwareClient.SetDesiredPropertyUpdateCallbackAsync(callback));
            deviceConnectivityManager.Verify(d => d.CallTimedOut(), Times.Never);
        }

        [Fact]
        public async Task TestNoSubscriptionWhenOpenFails()
        {
            // Arrange
            int connectionStatusChangedHandlerCount = 0;

            void ConnectionStatusChangedHandler(ConnectionStatusInfo statusInfo)
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
            var receivedStatuses = new List<ConnectionStatusInfo>();

            void ConnectionStatusChangedHandler(ConnectionStatusInfo statusInfo)
            {
                receivedStatuses.Add(statusInfo);
            }

            var deviceConnectivityManager = new DeviceConnectivityManager();
            var client = Mock.Of<IClient>();
            Mock.Get(client).SetupSequence(c => c.SendTelemetryAsync(It.IsAny<TelemetryMessage>()))
                .Returns(Task.CompletedTask)
                .Throws(new TimeoutException());
            var connectivityAwareClient = new ConnectivityAwareClient(client, deviceConnectivityManager, Mock.Of<IIdentity>(i => i.Id == "d1"));
            connectivityAwareClient.SetConnectionStatusChangedHandler(ConnectionStatusChangedHandler);
            await connectivityAwareClient.OpenAsync();

            // Act
            await connectivityAwareClient.SendTelemetryAsync(new TelemetryMessage(new byte[0]));

            // Assert
            Assert.Single(receivedStatuses);
            Assert.Equal(ConnectionStatus.Connected, receivedStatuses[0].Status);
            Assert.Equal(ConnectionStatusChangeReason.ConnectionOk, receivedStatuses[0].ChangeReason);

            // Act
            await Assert.ThrowsAsync<TimeoutException>(async () => await connectivityAwareClient.SendTelemetryAsync(new TelemetryMessage(new byte[0])));

            // Assert
            Assert.Single(receivedStatuses);
            Assert.Equal(ConnectionStatus.Connected, receivedStatuses[0].Status);
            Assert.Equal(ConnectionStatusChangeReason.ConnectionOk, receivedStatuses[0].ChangeReason);

            // Act
            deviceConnectivityManager.InvokeDeviceConnected();

            // Assert
            Assert.Single(receivedStatuses);
            Assert.Equal(ConnectionStatus.Connected, receivedStatuses[0].Status);
            Assert.Equal(ConnectionStatusChangeReason.ConnectionOk, receivedStatuses[0].ChangeReason);

            // Act
            deviceConnectivityManager.InvokeDeviceDisconnected();

            // Assert
            Assert.Equal(2, receivedStatuses.Count);
            Assert.Equal(ConnectionStatus.Disconnected, receivedStatuses[1].Status);
            Assert.Equal(ConnectionStatusChangeReason.CommunicationError, receivedStatuses[1].ChangeReason);

            // Act
            deviceConnectivityManager.InvokeDeviceDisconnected();

            // Assert
            Assert.Equal(2, receivedStatuses.Count);
            Assert.Equal(ConnectionStatus.Disconnected, receivedStatuses[1].Status);
            Assert.Equal(ConnectionStatusChangeReason.CommunicationError, receivedStatuses[1].ChangeReason);

            // Act
            await connectivityAwareClient.CloseAsync();
            deviceConnectivityManager.InvokeDeviceConnected();
            deviceConnectivityManager.InvokeDeviceDisconnected();

            // Assert
            Assert.Equal(2, receivedStatuses.Count);
            Assert.Equal(ConnectionStatus.Disconnected, receivedStatuses[1].Status);
            Assert.Equal(ConnectionStatusChangeReason.CommunicationError, receivedStatuses[1].ChangeReason);
        }

        [Fact]
        public async Task ConnectivityChangeEventTest1()
        {
            // Arrange
            int connectedStatusChangedHandlerCount = 0;
            int disconnectedStatusChangedHandlerCount = 0;

            void ConnectionStatusChangedHandler(ConnectionStatusInfo statusInfo)
            {
                if (statusInfo.Status == ConnectionStatus.Connected)
                {
                    Interlocked.Increment(ref connectedStatusChangedHandlerCount);
                }
                else
                {
                    Interlocked.Increment(ref disconnectedStatusChangedHandlerCount);
                }
            }

            var deviceConnectivityManager = new DeviceConnectivityManager();
            Action<ConnectionStatusInfo> innerClientHandler = null;
            var client = new Mock<IClient>();
            client.Setup(c => c.SetConnectionStatusChangedHandler(It.IsAny<Action<ConnectionStatusInfo>>()))
                .Callback<Action<ConnectionStatusInfo>>(c => innerClientHandler = c);

            var connectivityAwareClient = new ConnectivityAwareClient(client.Object, deviceConnectivityManager, Mock.Of<IIdentity>(i => i.Id == "d1"));
            connectivityAwareClient.SetConnectionStatusChangedHandler(ConnectionStatusChangedHandler);

            // Act
            await connectivityAwareClient.OpenAsync();

            // Assert
            Assert.NotNull(innerClientHandler);

            // Act
            innerClientHandler.Invoke(new ConnectionStatusInfo(ConnectionStatus.Connected, ConnectionStatusChangeReason.ConnectionOk));

            // Assert
            Assert.Equal(1, connectedStatusChangedHandlerCount);
            Assert.Equal(0, disconnectedStatusChangedHandlerCount);

            // Act
            innerClientHandler.Invoke(new ConnectionStatusInfo(ConnectionStatus.Connected, ConnectionStatusChangeReason.ConnectionOk));
            deviceConnectivityManager.InvokeDeviceConnected();

            // Assert
            Assert.Equal(1, connectedStatusChangedHandlerCount);
            Assert.Equal(0, disconnectedStatusChangedHandlerCount);

            // Act
            innerClientHandler.Invoke(new ConnectionStatusInfo(ConnectionStatus.DisconnectedRetrying, ConnectionStatusChangeReason.CommunicationError));

            // Assert
            Assert.Equal(1, connectedStatusChangedHandlerCount);
            Assert.Equal(0, disconnectedStatusChangedHandlerCount);

            // Act
            innerClientHandler.Invoke(new ConnectionStatusInfo(ConnectionStatus.Disconnected, ConnectionStatusChangeReason.CommunicationError));

            // Assert
            Assert.Equal(1, connectedStatusChangedHandlerCount);
            Assert.Equal(0, disconnectedStatusChangedHandlerCount);

            // Act
            innerClientHandler.Invoke(new ConnectionStatusInfo(ConnectionStatus.Disconnected, ConnectionStatusChangeReason.CommunicationError));
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

            void ConnectionStatusChangedHandler(ConnectionStatusInfo statusInfo)
            {
                if (statusInfo.Status == ConnectionStatus.Connected)
                {
                    Interlocked.Increment(ref connectedStatusChangedHandlerCount);
                }
                else
                {
                    Interlocked.Increment(ref disconnectedStatusChangedHandlerCount);
                }
            }

            var deviceConnectivityManager = new DeviceConnectivityManager();
            Action<ConnectionStatusInfo> innerClientHandler = null;
            var client = new Mock<IClient>();
            client.Setup(c => c.SetConnectionStatusChangedHandler(It.IsAny<Action<ConnectionStatusInfo>>()))
                .Callback<Action<ConnectionStatusInfo>>(c => innerClientHandler = c);

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
            innerClientHandler.Invoke(new ConnectionStatusInfo(ConnectionStatus.Connected, ConnectionStatusChangeReason.ConnectionOk));

            // Assert
            Assert.Equal(1, connectedStatusChangedHandlerCount);
            Assert.Equal(0, disconnectedStatusChangedHandlerCount);

            // Act
            deviceConnectivityManager.InvokeDeviceDisconnected();

            // Assert
            Assert.Equal(1, connectedStatusChangedHandlerCount);
            Assert.Equal(1, disconnectedStatusChangedHandlerCount);

            // Act
            innerClientHandler.Invoke(new ConnectionStatusInfo(ConnectionStatus.Disconnected, ConnectionStatusChangeReason.CommunicationError));

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
