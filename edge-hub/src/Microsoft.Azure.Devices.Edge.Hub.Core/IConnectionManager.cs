// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// The <c>IConnectionManager</c> maintains a dictionary that maps device IDs to
    /// <see cref="IDeviceProxy"/> and <see cref="ICloudProxy"/> objects representing
    /// devices that are connected to the edge hub. It is reponsible for making sure
    /// that there is one connection per device. The <see cref="IDeviceProxy"/>
    /// represents the connection to the device itself and contains functionality for
    /// sending messages to the device. The <see cref="ICloudProxy"/> represents the
    /// connection to this device in Azure IoT Hub and contains functionality for
    /// sending messages to IoT Hub on behalf of the device.
    /// </summary>
    public interface IConnectionManager
    {
        Task AddDeviceConnection(IClientCredentials clientCredentials);

        void BindDeviceProxy(IIdentity identity, IDeviceProxy deviceProxy);

        Task RemoveDeviceConnection(string id);

        Task<Try<ICloudProxy>> CreateCloudConnectionAsync(IClientCredentials identity);

        Option<IDeviceProxy> GetDeviceConnection(string id);

        Option<IClientCredentials> GetClientCredentials(string id);

        Task<Option<ICloudProxy>> GetCloudConnection(string id);

        void AddSubscription(string id, DeviceSubscription deviceSubscription);

        void RemoveSubscription(string id, DeviceSubscription deviceSubscription);

        Option<IReadOnlyDictionary<DeviceSubscription, bool>> GetSubscriptions(string id);

        event EventHandler<IIdentity> CloudConnectionLost;

        event EventHandler<IIdentity> CloudConnectionEstablished;

        event EventHandler<IIdentity> DeviceConnected;

        event EventHandler<IIdentity> DeviceDisconnected;
    }
}
