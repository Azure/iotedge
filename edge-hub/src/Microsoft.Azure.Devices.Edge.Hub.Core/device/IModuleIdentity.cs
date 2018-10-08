// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Device
{
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;

    public interface IModuleIdentity : IIdentity
    {
        /// <summary>
        /// Gets the identifier of the edge device that this module is a part of.
        /// </summary>
        string DeviceId { get; }

        /// <summary>
        /// Gets this module's identifier.
        /// </summary>
        string ModuleId { get; }
    }
}
