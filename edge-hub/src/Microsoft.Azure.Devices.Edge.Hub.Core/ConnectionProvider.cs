// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ConnectionProvider : IConnectionProvider
    {
        readonly IConnectionManager connectionManager;
        readonly IEdgeHub edgeHub;

        public ConnectionProvider(IConnectionManager connectionManager, IEdgeHub edgeHub)
        {
            this.connectionManager = Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
            this.edgeHub = Preconditions.CheckNotNull(edgeHub, nameof(edgeHub));
        }

        public async Task<IDeviceListener> GetDeviceListenerAsync(IIdentity identity)
        {
            // Set up a connection to the cloud here, so that we can use it in the 
            Try<ICloudProxy> cloudProxy = await this.connectionManager.GetOrCreateCloudConnectionAsync(Preconditions.CheckNotNull(identity));
            if (!cloudProxy.Success)
            {
                throw new EdgeHubConnectionException($"Unable to connect to IoTHub for device {identity.Id}", cloudProxy.Exception);
            }
            IDeviceListener deviceListener = new DeviceMessageHandler(identity, this.edgeHub, this.connectionManager, cloudProxy.Value);
            return deviceListener;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.edgeHub?.Dispose();
            }
        }

        public void Dispose() => this.Dispose(true);
    }
}
