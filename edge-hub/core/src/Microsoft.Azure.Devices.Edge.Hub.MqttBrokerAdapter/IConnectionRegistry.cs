// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IConnectionRegistry
    {
        Task<Option<IDeviceListener>> GetOrCreateDeviceListenerAsync(IIdentity identity, bool directOnCreation = false);
        Task<Option<IDeviceProxy>> GetDeviceProxyAsync(IIdentity identity);

        Task UpdateNestedParentInfoAsync(IEnumerable<IIdentity> childDevices, IIdentity parentIdentity);
        Task<IReadOnlyList<IIdentity>> GetNestedConnectionsAsync(IIdentity parentIdentity);

        Task CloseConnectionAsync(IIdentity identity);
    }
}
