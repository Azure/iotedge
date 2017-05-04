// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ConnectionProvider : IConnectionProvider
    {
        readonly IConnectionManager connectionManager;
        readonly IRouter router;
        readonly IDispatcher dispatcher;
        readonly ICloudProxyProvider cloudProxyProvider;

        public ConnectionProvider(IConnectionManager connectionManager,
            IRouter router,
            IDispatcher dispatcher,
            ICloudProxyProvider cloudProxyProvider)
        {
            this.connectionManager = Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
            this.router = Preconditions.CheckNotNull(router, nameof(router));
            this.dispatcher = Preconditions.CheckNotNull(dispatcher, nameof(dispatcher));
            this.cloudProxyProvider = Preconditions.CheckNotNull(cloudProxyProvider, nameof(cloudProxyProvider));
        }

        public async Task<IDeviceListener> GetDeviceListener(IIdentity identity)
        {
            // Set up a connection to the cloud here, so that we can use it in the 
            Try<ICloudProxy> cloudProxy = await this.connectionManager.GetOrCreateCloudConnection(Preconditions.CheckNotNull(identity));
            if (!cloudProxy.Success)
            {
                throw new IotHubConnectionException($"Unable to connect to IoTHub for device {identity.Id}", cloudProxy.Exception);
            }
            IDeviceListener deviceListener = new DeviceListener(identity, this.router, this.dispatcher, this.connectionManager, cloudProxy.Value);
            return deviceListener;
        }
    }
}
