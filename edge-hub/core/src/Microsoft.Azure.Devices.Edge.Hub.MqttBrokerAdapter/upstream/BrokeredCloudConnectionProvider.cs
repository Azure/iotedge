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

        public Task<Try<ICloudConnection>> Connect(IIdentity identity, Action<string, CloudConnectionStatus> connectionStatusChangedHandler)
        {
            var cloudProxy = new BrokeredCloudProxy(identity, this.cloudProxyDispatcher, connectionStatusChangedHandler);
            return Task.FromResult(new Try<ICloudConnection>(new BrokeredCloudConnection(cloudProxy)));
        }
    }
}
