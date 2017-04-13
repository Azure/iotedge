// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;

    public class Connection
    {
        public Connection(ICloudProxy cloudProxy, IDeviceProxy deviceProxy)
        {
            this.CloudProxy = cloudProxy;
            this.DeviceProxy = deviceProxy;
        }

        public ICloudProxy CloudProxy { get; }

        public IDeviceProxy DeviceProxy { get; }
    }
}
