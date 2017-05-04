// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Device
{
    public interface IDeviceIdentity : IIdentity
    {
        string DeviceId { get; }
    }
}