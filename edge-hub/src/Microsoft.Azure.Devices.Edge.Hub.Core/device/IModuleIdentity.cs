// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Device
{
    public interface IModuleIdentity : IIdentity
    {
        string ModuleId { get; }

        string DeviceId { get; }
    }
}