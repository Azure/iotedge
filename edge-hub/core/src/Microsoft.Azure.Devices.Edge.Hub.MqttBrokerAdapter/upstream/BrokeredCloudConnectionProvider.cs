// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public class BrokeredCloudConnectionProvider : ICloudConnectionProvider
    {
        BrokeredCloudProxyDispatcher cloudProxyDispatcher;

        public BrokeredCloudConnectionProvider(BrokeredCloudProxyDispatcher cloudProxyDispatcher)
        {
            this.cloudProxyDispatcher = cloudProxyDispatcher;
        }

        public void BindEdgeHub(IEdgeHub edgeHub)
        {
            this.cloudProxyDispatcher.BindEdgeHub(edgeHub);
        }

        public Task<Try<ICloudConnection>> Connect(IClientCredentials clientCredentials, Action<string, CloudConnectionStatus> connectionStatusChangedHandler)
        {
            return this.Connect(clientCredentials.Identity, connectionStatusChangedHandler);
        }

        public async Task<Try<ICloudConnection>> Connect(IIdentity identity, Action<string, CloudConnectionStatus> connectionStatusChangedHandler)
        {
            if (!await this.IsConnected())
            {
                return new Try<ICloudConnection>(new Exception("Bridge is not connected upstream"));
            }

            var cloudProxy = new BrokeredCloudProxy(identity, this.cloudProxyDispatcher, connectionStatusChangedHandler);
            return new Try<ICloudConnection>(new BrokeredCloudConnection(cloudProxy));
        }

        async Task<bool> IsConnected()
        {
            if (!this.cloudProxyDispatcher.IsConnected)
            {
                var cntr = 50;
                while (--cntr > 0)
                {
                    await Task.Delay(100);
                    if (this.cloudProxyDispatcher.IsConnected)
                    {
                        break;
                    }
                }
            }

            return this.cloudProxyDispatcher.IsConnected;
        }
    }
}
