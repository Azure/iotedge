// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;
    using System.Threading.Tasks;

    public class ConnectionProvider : IConnectionProvider
    {
        readonly IConnectionManager connectionManager;
        readonly IRouter router;
        readonly IDispatcher dispatcher;
        readonly IAuthenticator authenticator;
        readonly ICloudProxyProvider cloudProxyProvider;

        public ConnectionProvider(IConnectionManager connectionManager,
            IRouter router,
            IDispatcher dispatcher,
            IAuthenticator authenticator,
            ICloudProxyProvider cloudProxyProvider)
        {
            this.connectionManager = Preconditions.CheckNotNull(connectionManager);
            this.router = Preconditions.CheckNotNull(router);
            this.dispatcher = Preconditions.CheckNotNull(dispatcher);
            this.authenticator = Preconditions.CheckNotNull(authenticator);
            this.cloudProxyProvider = Preconditions.CheckNotNull(cloudProxyProvider);
        }

        public async Task<Option<IDeviceListener>> Connect(string connectionString, IDeviceProxy deviceProxy)
        {
            Preconditions.CheckNonWhiteSpace(connectionString, nameof(connectionString));
            Preconditions.CheckNotNull(deviceProxy, nameof(deviceProxy));

            string deviceId = ConnectionStringHelper.GetDeviceIdFromConnectionString(connectionString);
            bool authenticationResult = this.authenticator.Authenticate(connectionString);
            if (!authenticationResult)
            {
                await deviceProxy.Disconnect();
                return Option.None<IDeviceListener>();
            }
            ICloudListener cloudListener = new CloudListener(deviceId, deviceProxy);
            ICloudProxy cloudProxy = this.cloudProxyProvider.Connect(connectionString, cloudListener);

            this.connectionManager.AddConnection(deviceId, deviceProxy, cloudProxy);

            IDeviceListener deviceListener = new DeviceListener(deviceId, this.router, this.dispatcher, cloudProxy);
            return Option.Some(deviceListener);
        }
    }
}
