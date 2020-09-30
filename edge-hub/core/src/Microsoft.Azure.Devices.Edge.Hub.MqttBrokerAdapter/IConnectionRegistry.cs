// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IConnectionRegistry
    {
        Task<Option<IDeviceListener>> GetDeviceListenerAsync(IIdentity identity);
        Task<Option<IDeviceProxy>> GetDeviceProxyAsync(IIdentity identity);

        Task CloseConnectionAsync(IIdentity identity);
    }
}
