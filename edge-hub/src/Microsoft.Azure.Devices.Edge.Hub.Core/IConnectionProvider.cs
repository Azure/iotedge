// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IConnectionProvider
    {
        Task<Try<IDeviceListener>> Connect(string connectionString, IDeviceProxy deviceProxy);
    }
}
