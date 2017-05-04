// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IConnectionManager
    {
        void AddDeviceConnection(IIdentity identity, IDeviceProxy deviceProxy);

        Task<Try<ICloudProxy>> CreateCloudConnection(IIdentity identity);

        Task<Try<ICloudProxy>> GetOrCreateCloudConnection(IIdentity identity);

        Option<IDeviceProxy> GetDeviceConnection(string deviceId);

        Option<ICloudProxy> GetCloudConnection(string deviceId);

        Task<bool> CloseConnection(string deviceId);

    }
}
