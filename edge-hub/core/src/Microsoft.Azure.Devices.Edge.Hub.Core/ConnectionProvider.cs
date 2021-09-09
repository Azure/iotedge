// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ConnectionProvider : IConnectionProvider
    {
        readonly IConnectionManager connectionManager;
        readonly IEdgeHub edgeHub;
        readonly TimeSpan messageAckTimeout;

        public ConnectionProvider(IConnectionManager connectionManager, IEdgeHub edgeHub, TimeSpan messageAckTimeout)
        {
            this.connectionManager = Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
            this.edgeHub = Preconditions.CheckNotNull(edgeHub, nameof(edgeHub));
            this.messageAckTimeout = messageAckTimeout;
        }

        public Task<IDeviceListener> GetDeviceListenerAsync(IIdentity identity, Option<string> modelId)
        {
            IDeviceListener deviceListener = new DeviceMessageHandler(Preconditions.CheckNotNull(identity, nameof(identity)), this.edgeHub, this.connectionManager, this.messageAckTimeout, modelId);
            return Task.FromResult(deviceListener);
        }

        public void Dispose() => this.Dispose(true);

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.edgeHub?.Dispose();
            }
        }
    }
}
