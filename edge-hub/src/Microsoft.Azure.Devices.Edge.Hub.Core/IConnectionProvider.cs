// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;

    public interface IConnectionProvider : IDisposable
    {
        Task<IDeviceListener> GetDeviceListenerAsync(IClientCredentials clientCredentials);
    }
}
