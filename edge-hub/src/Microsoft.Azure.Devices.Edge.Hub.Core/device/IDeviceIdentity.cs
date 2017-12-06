// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Device
{
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;

    public interface IDeviceIdentity : IIdentity
    {
        string DeviceId { get; }
    }
}
