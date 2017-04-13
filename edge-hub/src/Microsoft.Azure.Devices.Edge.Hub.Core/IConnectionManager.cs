// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;

    public interface IConnectionManager
    {
        void AddConnection(string deviceId, IDeviceProxy deviceProxy, ICloudProxy cloudProxy);
        Connection GetConnection(string deviceId);
    }
}
