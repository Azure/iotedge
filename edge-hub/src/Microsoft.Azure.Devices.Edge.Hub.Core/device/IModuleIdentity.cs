// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Device
{
    public interface IModuleIdentity : IIdentity
    {
        /// <summary>
        /// Gets this module's identifier.
        /// </summary>
        string ModuleId { get; }

        /// <summary>
        /// Gets the identifier of the edge device that this module is a part of.
        /// </summary>
        string DeviceId { get; }
    }
}