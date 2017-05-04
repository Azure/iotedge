// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;

    public class Authenticator : IAuthenticator
    {
        readonly IConnectionManager connectionManager;
        readonly string edgeDeviceId;

        public Authenticator(IConnectionManager connectionManager, string edgeDeviceId)
        {
            this.connectionManager = Preconditions.CheckNotNull(connectionManager, nameof(edgeDeviceId));
            this.edgeDeviceId = Preconditions.CheckNonWhiteSpace(edgeDeviceId, nameof(edgeDeviceId));
        }

        public async Task<bool> Authenticate(IIdentity identity)
        {
            var moduleIdentity = identity as IModuleIdentity;
            if (moduleIdentity != null && !moduleIdentity.DeviceId.Equals(this.edgeDeviceId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Initially we will have many modules connecting with same device ID, so this is a GetOrCreate. 
            // When we have module identity implemented, this should be CreateCloudConnection.
            Try<ICloudProxy> cloudProxyTry = await this.connectionManager.GetOrCreateCloudConnection(Preconditions.CheckNotNull(identity, nameof(identity)));
            return cloudProxyTry.Success && cloudProxyTry.Value.IsActive;
        }
    }
}
