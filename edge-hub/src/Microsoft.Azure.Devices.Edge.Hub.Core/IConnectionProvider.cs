// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;

    public interface IConnectionProvider : IDisposable
    {
        Task<IDeviceListener> GetDeviceListenerAsync(IIdentity identity);
    }
}
