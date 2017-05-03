// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;

    public class Authenticator : IAuthenticator
    {
        readonly IConnectionManager connectionManager;

        public Authenticator(IConnectionManager connectionManager)
        {
            this.connectionManager = Preconditions.CheckNotNull(connectionManager);
        }

        public async Task<bool> Authenticate(IHubDeviceIdentity hubDeviceIdentity)
        {
            // Initially we will have many modules connecting with same device ID, so this is a GetOrCreate. 
            // When we have module identity implemented, this should be CreateCloudConnection.
            Try<ICloudProxy> cloudProxyTry = await this.connectionManager.GetOrCreateCloudConnection(Preconditions.CheckNotNull(hubDeviceIdentity, nameof(hubDeviceIdentity)));
            return cloudProxyTry.Success && cloudProxyTry.Value.IsActive;
        }
    }
}
