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
        readonly IAuthenticator authenticator;
        readonly ICloudProxyProvider cloudProxyProvider;

        public ConnectionProvider(IConnectionManager connectionManager,
            IRouter router,
            IDispatcher dispatcher,
            IAuthenticator authenticator,
            ICloudProxyProvider cloudProxyProvider)
        {
            this.connectionManager = Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
            this.router = Preconditions.CheckNotNull(router, nameof(router));
            this.dispatcher = Preconditions.CheckNotNull(dispatcher, nameof(dispatcher));
            this.authenticator = Preconditions.CheckNotNull(authenticator, nameof(authenticator));
            this.cloudProxyProvider = Preconditions.CheckNotNull(cloudProxyProvider, nameof(cloudProxyProvider));
        }

        public async Task<Try<IDeviceListener>> Connect(string connectionString, IDeviceProxy deviceProxy)
        {
            Preconditions.CheckNonWhiteSpace(connectionString, nameof(connectionString));
            Preconditions.CheckNotNull(deviceProxy, nameof(deviceProxy));

            // TODO - For Modules, this might have to be moduleId.
            string deviceId = ConnectionStringUtil.GetDeviceIdFromConnectionString(connectionString);
            Try<bool> authenticationResult = this.authenticator.Authenticate(connectionString);
            if (!authenticationResult.Success)
            {
                await deviceProxy.Disconnect();
                return Try<IDeviceListener>.Failure(authenticationResult.Exception);
            }

            ICloudListener cloudListener = new CloudListener(deviceId, deviceProxy);
            Try<ICloudProxy> cloudProxy = await this.cloudProxyProvider.Connect(connectionString, cloudListener);
            if (!cloudProxy.Success)
            {
                await deviceProxy.Disconnect();
                return Try<IDeviceListener>.Failure(cloudProxy.Exception);
            }

            this.connectionManager.AddConnection(deviceId, deviceProxy, cloudProxy.Value);

            IDeviceListener deviceListener = new DeviceListener(deviceId, this.router, this.dispatcher, cloudProxy.Value);
            return Try.Success(deviceListener);
        }
    }
}
