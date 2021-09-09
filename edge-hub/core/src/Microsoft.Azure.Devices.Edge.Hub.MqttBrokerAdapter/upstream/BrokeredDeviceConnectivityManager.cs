// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Util;

    public class BrokeredDeviceConnectivityManager : IDeviceConnectivityManager, IDisposable
    {
        BrokeredCloudProxyDispatcher cloudProxyDispatcher;

        public BrokeredDeviceConnectivityManager(BrokeredCloudProxyDispatcher cloudProxyDispatcher)
        {
            this.cloudProxyDispatcher = Preconditions.CheckNotNull(cloudProxyDispatcher);
            this.cloudProxyDispatcher.ConnectionStatusChangedEvent += this.ForwardConnectivityEvent;
        }

        public event EventHandler DeviceConnected;
        public event EventHandler DeviceDisconnected;

        // These are not being used in brokered setup
        public Task CallSucceeded() => Task.CompletedTask;
        public Task CallTimedOut() => Task.CompletedTask;

        public void Dispose() => this.cloudProxyDispatcher.ConnectionStatusChangedEvent -= this.ForwardConnectivityEvent;

        void ForwardConnectivityEvent(CloudConnectionStatus status)
        {
            switch (status)
            {
                case CloudConnectionStatus.ConnectionEstablished:
                    this.DeviceConnected?.Invoke(this, EventArgs.Empty);
                    break;

                case CloudConnectionStatus.Disconnected:
                    this.DeviceDisconnected?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }
    }
}
