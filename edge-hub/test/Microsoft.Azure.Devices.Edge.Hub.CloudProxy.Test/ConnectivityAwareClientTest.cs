// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    public class ConnectivityAwareClientTest
    {
        [Unit]
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

        class DeviceConnectivityManager : IDeviceConnectivityManager
        {
            public void CallSucceeded() 
            {
                throw new NotImplementedException();
            }

            public void CallTimedOut()
            {
                throw new NotImplementedException();
            }

            public void InvokeDeviceConnected() => this.DeviceConnected?.Invoke(null, null);

            public void InvokeDeviceDisconnected() => this.DeviceDisconnected?.Invoke(null, null);

            public event EventHandler DeviceConnected;

            public event EventHandler DeviceDisconnected;
        }
    }
}
