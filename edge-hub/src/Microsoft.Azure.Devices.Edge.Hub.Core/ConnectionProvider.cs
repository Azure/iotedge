// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
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

        public Task<IDeviceListener> GetDeviceListenerAsync(IIdentity identity)
        {
            IDeviceListener deviceListener = new DeviceMessageHandler(Preconditions.CheckNotNull(identity, nameof(identity)), this.edgeHub, this.connectionManager);
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
