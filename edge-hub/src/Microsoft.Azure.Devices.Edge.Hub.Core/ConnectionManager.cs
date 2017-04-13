
// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;

    class ConnectionManager: IConnectionManager
    {
        public void AddConnection(string deviceId, IDeviceProxy deviceProxy, ICloudProxy cloudProxy)
        {
            throw new System.NotImplementedException();
        }

        public Connection GetConnection(string deviceId)
        {
            throw new System.NotImplementedException();
        }
    }
}
